using System.Collections.Generic;
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

        // Map from inference element to sensor input element. This helps us find the
        // appropriate ground truth field for a given inference element
        /// <summary>
        /// 
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
        //public PredictionMetricsManager(IEnumerable<MetricSpec> metricSpecs, List<Tuple> fieldInfo, InferenceType inferenceType)
        //{
        //    this.__metricSpecs = new List<MetricSpec>();
        //    this.__metrics = new List<MetricSpec>();
        //    this.__metricLabels = new List<string>();

        //    // Maps field names to indices. Useful for looking up input/predictions by
        //    // field name
        //    //this.__fieldNameIndexMap = dict( [(info.name, i) \
        //    //                          for i, info in enumerate(fieldInfo)] )
        //    this.__fieldNameIndexMap = new Map<string,int>();
        //    for (int i = 0; i < fieldInfo.Count; i++)
        //    {
        //        var info = fieldInfo[0];
        //        __fieldNameIndexMap.Add(info.Item1, i);
        //    }
        //    //this.__constructMetricsModules(metricSpecs)
        //    //this.__currentGroundTruth = None
        //    //this.__currentInference = None
        //    //this.__currentResult = None

        //    this.__isTemporal = InferenceType.isTemporal(inferenceType)
        //    //if this.__isTemporal:
        //    //  this.__inferenceShifter = InferenceShifter()
        //}
    }
}