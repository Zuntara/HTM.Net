﻿using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;

namespace HTM.Net.Research.opf
{
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
                var info = fieldInfo[0];
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
        public Map<string, double> update(ModelResult results)
        {
            _addResults(results);
            if(__metricSpecs == null || __currentInference == null) return  new Map<string, double>();

            var metricResults = new Map<string,double>();
            foreach (var item in ArrayUtils.Zip(__metrics, __metricSpecs, __metricLabels))
            {
                var metric = (MetricIFace) item.Get(0);
                var spec = (MetricSpec) item.Get(1);
                var label = (string) item.Get(2);

                var inferenceElement = spec.InferenceElement;
                var field = spec.Field;
                //var groundTruth = _getGroundTruth(inferenceElement);
                //var inference = _getInference(inferenceElement);
                //var rawRecord = _getRawGroundTruth();
                var result = __currentResult;
                if (!string.IsNullOrWhiteSpace(field))
                {
                    // TODO
                    throw new NotImplementedException();
                }
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
                // if not InferenceElement.validate(spec.inferenceElement):
                //   raise ValueError("Invalid inference element for metric spec: %r" % spec)
                __metrics.Add(MetricSpec.GetModule(spec));
                __metricLabels.Add(spec.getLabel());
            }
        }

        public Map<string, double> GetMetrics()
        {
            Map<string, double> result = new Map<string, double>();
            foreach (var item in ArrayUtils.Zip(__metrics, __metricLabels))
            {
                var value = ((MetricIFace)item.Get(0)).getMetric();
                result[(string)item.Get(1)] = (double)value["value"];
            }
            return result;
        }
    }
}