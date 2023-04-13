using System;
using HTM.Net.Data;
using HTM.Net.Research.opf;

namespace HTM.Net.Research.Swarming.Descriptions
{
    [Serializable]
    public class ExperimentControl
    {
        public StreamDef DatasetSpec { get; set; }
        public FieldMetaInfo[] InputRecordSchema { get; set; }
        public InferenceArgsDescription InferenceArgs { get; set; }
        /// <summary>
        /// Logged Metrics: A sequence of regular expressions that specify which of
        /// the metrics from the Inference Specifications section MUST be logged for
        /// every prediction. The regex"s correspond to the automatically generated
        /// metric labels. This is similar to the way the optimization metric is
        /// specified in permutations.py.
        /// </summary>
        public string[] LoggedMetrics { get; set; }
        /// <summary>
        /// Metrics: A list of MetricSpecs that instantiate the metrics that are
        /// computed for this experiment
        /// </summary>
        public MetricSpec[] Metrics { get; set; }
        public int? IterationCount { get; set; }
        public int? IterationCountInferOnly { get; set; }

        public ExperimentControl Clone()
        {
            ExperimentControl c = new ExperimentControl();
            c.InputRecordSchema = (FieldMetaInfo[])InputRecordSchema?.Clone();
            c.InferenceArgs = InferenceArgs?.Clone();
            c.LoggedMetrics = (string[])LoggedMetrics?.Clone();
            c.Metrics = (MetricSpec[])Metrics?.Clone();
            c.DatasetSpec = DatasetSpec;
            c.IterationCount = IterationCount;
            c.IterationCountInferOnly = IterationCountInferOnly;
            return c;
        }
    }
}