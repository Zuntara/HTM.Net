using System.Collections;
using System;
using System.Collections.Generic;
using HTM.Net.Data;
using HTM.Net.Network;
using System.Linq;
using System.Reflection;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;

namespace HTM.Net.Research.opf;

// MetricValueElement class
//
// Represents an individual metric value element in a list returned by
// PredictionMetricsManager.getMetrics()
//
// spec:           A MetricSpec value (a copy) that was used to construct
// the metric instance that generated the metric value
// value:          The metric value
public class MetricValueElement : NamedTuple
{
    public MetricValueElement()
        : base(new[] { "spec", "value" }, null)
    {
    }
}

/// <summary>
/// This is a class to handle the computation of metrics properly. 
/// This class takes in an inferenceType, and it assumes that it is associcated with a single model
/// </summary>
public class PredictionMetricsManager
{
    private List<MetricSpec> __metricSpecs;
    private Map<string, int> __fieldNameIndexMap;
    private bool __isTemporal;
    private List<MetricIFace> __metrics;
    private List<string> __metricLabels;
    private InferenceShifter __inferenceShifter;
    private ModelResult __currentResult;
    private Map<InferenceElement, object> __currentInference;
    private ModelResult __currentGroundTruth;

    // Map from inference element to sensor input element. This helps us find the
    // appropriate ground truth field for a given inference element
    /// <summary>
    /// Constructs a Metrics Manager
    /// </summary>
    /// <param name="metricSpecs">A sequence of MetricSpecs that specify which metrics should be calculated</param>
    /// <param name="fieldInfo"></param>
    /// <param name="inferenceType">
    /// An opfutils.inferenceType value that specifies the inference
    /// type of the associated model.This affects how metrics are
    /// calculated.FOR EXAMPLE, temporal models save the inference
    /// from the previous timestep to match it to the ground truth
    /// value in the current timestep
    /// </param>
    public PredictionMetricsManager(IEnumerable<MetricSpec> metricSpecs, List<FieldMetaInfo> fieldInfo, InferenceType inferenceType)
    {
        this.__metricSpecs = new List<MetricSpec>();
        this.__metrics = new List<MetricIFace>();
        this.__metricLabels = new List<string>();

        // Maps field names to indices. Useful for looking up input/predictions by
        // field name
        //this.__fieldNameIndexMap = dict( [(info.name, i) \
        //                          for i, info in enumerate(fieldInfo)] )
        this.__fieldNameIndexMap = new Map<string, int>();
        for (int i = 0; i < fieldInfo.Count; i++)
        {
            var info = fieldInfo[i];
            __fieldNameIndexMap.Add(info.name, i);
        }
        this.__constructMetricsModules(metricSpecs);
        this.__currentGroundTruth = null;
        this.__currentInference = null;
        this.__currentResult = null;

        this.__isTemporal = InferenceTypeHelper.IsTemporal(inferenceType);
        if (this.__isTemporal)
            this.__inferenceShifter = new InferenceShifter();
    }

    /// <summary>
    /// Compute the new metrics values, given the next inference/ground-truth values
    /// </summary>
    /// <param name="results">ModelResult object that was computed during the last iteration of the model</param>
    /// <returns>A dictionary where each key is the metric-name, and the values are it scalar value.</returns>
    public Map<string, double?> update(ModelResult results)
    {
        _addResults(results);
        if (__metricSpecs == null || __currentInference == null) return new Map<string, double?>();

        var metricResults = new Map<string, double?>();
        foreach (var item in ArrayUtils.Zip(__metrics, __metricSpecs, __metricLabels))
        {
            var metric = (MetricIFace)item.Get(0);
            var spec = (MetricSpec)item.Get(1);
            var label = (string)item.Get(2);

            var inferenceElement = spec.InferenceElement;
            var field = spec.Field;
            var groundTruth = _getGroundTruth(inferenceElement.GetValueOrDefault());
            var inference = _getInference(inferenceElement.GetValueOrDefault());
            var rawRecord = _getRawGroundTruth();
            var result = __currentResult;
            if (!string.IsNullOrWhiteSpace(field))
            {
                if (inference is IList)
                {
                    if (__fieldNameIndexMap.ContainsKey(field))
                    {
                        // NOTE: If the predicted field is not fed in at the bottom, we won't have it in our fieldNameIndexMap
                        int fieldIndex = __fieldNameIndexMap[field];
                        inference = ((IList)inference)[fieldIndex];
                    }
                    else
                    {
                        inference = null;
                    }
                }
                else if (inference is Util.Tuple)
                {
                    if (__fieldNameIndexMap.ContainsKey(field))
                    {
                        // NOTE: If the predicted field is not fed in at the bottom, we won't have it in our fieldNameIndexMap
                        int fieldIndex = __fieldNameIndexMap[field];
                        inference = ((Util.Tuple)inference).Get(fieldIndex);
                    }
                    else
                    {
                        inference = null;
                    }
                }

                if (groundTruth != null)
                {
                    if (groundTruth is IList)
                    {
                        if (__fieldNameIndexMap.ContainsKey(field))
                        {
                            // NOTE: If the predicted field is not fed in at the bottom, we won't have it in our fieldNameIndexMap
                            int fieldIndex = __fieldNameIndexMap[field];
                            groundTruth = ((IList)groundTruth)?[fieldIndex];
                        }
                        else
                        {
                            groundTruth = null;
                        }
                    }
                    else if (groundTruth is Util.Tuple)
                    {
                        if (__fieldNameIndexMap.ContainsKey(field))
                        {
                            // NOTE: If the predicted field is not fed in at the bottom, we won't have it in our fieldNameIndexMap
                            int fieldIndex = __fieldNameIndexMap[field];
                            groundTruth = ((Util.Tuple)groundTruth)?.Get(fieldIndex);
                        }
                        else
                        {
                            groundTruth = null;
                        }
                    }
                    else if (groundTruth is ManualInput)
                    {
                        groundTruth = null; // to be revised
                    }
                    else
                    {
                        // groundTruth could be a dict based off of field names
                        groundTruth = ((IDictionary)groundTruth)[field];
                    }
                }
            }

            metric.addInstance(
                groundTruth: (groundTruth != null ? Convert.ToDouble(groundTruth) : (double?)null),
                prediction: inference,
                record: rawRecord,
                result: result);

            metricResults[label] = (double?)metric.getMetric()["value"];
        }
        return metricResults;
    }

    /// <summary>
    /// Stores the current model results in the manager's internal store
    /// </summary>
    /// <param name="results">A ModelResults object that contains the current timestep's input/inferences</param>
    private void _addResults(ModelResult results)
    {
        // -----------------------------------------------------------------------
        // If the model potentially has temporal inferences.
        if (__isTemporal)
        {
            var shiftedInferences = __inferenceShifter.Shift(results).inferences;
            __currentResult = results.Clone();
            __currentResult.inferences = shiftedInferences;
            __currentInference = shiftedInferences;
        }
        // -----------------------------------------------------------------------
        // The current model has no temporal inferences.
        else
        {
            __currentResult = results.Clone();
            __currentInference = new Map<InferenceElement, object>(results.inferences);
        }
        // -----------------------------------------------------------------------
        // Save the current ground-truth results
        __currentGroundTruth = results.Clone();
    }

    /// <summary>
    /// Creates the required metrics modules
    /// </summary>
    /// <param name="metricSpecs"></param>
    private void __constructMetricsModules(IEnumerable<MetricSpec> metricSpecs)
    {
        if (metricSpecs == null) return;
        __metricSpecs = metricSpecs.ToList();
        foreach (MetricSpec spec in __metricSpecs)
        {
            if (!spec.InferenceElement.HasValue)
            {
                throw new InvalidOperationException($"Invalid inference element for metric spec: {spec}");
            }
            // if not InferenceElement.validate(spec.inferenceElement):
            //   raise ValueError("Invalid inference element for metric spec: %r" % spec)
            __metrics.Add(MetricSpec.GetModule(spec));
            __metricLabels.Add(spec.getLabel());
        }
    }

    /// <summary>
    /// Gets the current metric values
    /// </summary>
    /// <returns>
    /// A dictionary where each key is the metric-name, and the values are it scalar value. 
    /// Same as the output of <see cref="update"/>
    /// </returns>
    public Map<string, double?> GetMetrics()
    {
        Map<string, double?> result = new Map<string, double?>();
        foreach (var item in ArrayUtils.Zip(__metrics, __metricLabels))
        {
            var value = ((MetricIFace)item.Get(0)).getMetric();
            result[(string)item.Get(1)] = (double?)value["value"];
        }
        return result;
    }

    /// <summary>
    /// Get the actual value for this field
    /// </summary>
    /// <param name="inferenceElement">The inference element (part of the inference) that is being used for this metric</param>
    /// <returns></returns>
    internal object _getGroundTruth(InferenceElement inferenceElement)
    {
        string sensorInputElement = InferenceElementHelper.GetInputElement(inferenceElement);
        if (sensorInputElement == null)
            return null;

        var sensorElementProp = typeof(SensorInput).GetProperty(sensorInputElement, BindingFlags.IgnoreCase | BindingFlags.Instance | BindingFlags.Public);

        return sensorElementProp.GetValue(__currentGroundTruth.sensorInput);
    }

    /// <summary>
    /// Get what the inferred value for this field was
    /// </summary>
    /// <param name="inferenceElement">The inference element (part of the inference) that is being used for this metric</param>
    /// <returns></returns>
    internal object _getInference(InferenceElement inferenceElement)
    {
        if (__currentInference != null)
        {
            return __currentInference.Get(inferenceElement, null);
        }
        return null;
    }

    /// <summary>
    /// Get what the inferred value for this field was
    /// </summary>
    /// <returns></returns>
    internal Map<string, object> _getRawGroundTruth()
    {
        return __currentGroundTruth.rawInput;
    }

    /// <summary>
    /// Return the list of labels for the metrics that are being calculated
    /// </summary>
    /// <returns></returns>
    public string[] getMetricLabels()
    {
        return __metricLabels.ToArray();
    }
}