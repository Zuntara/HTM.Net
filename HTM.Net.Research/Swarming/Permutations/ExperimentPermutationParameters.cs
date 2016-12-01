using System;
using HTM.Net.Data;

namespace HTM.Net.Research.Swarming.Permutations
{
    /// <summary>
    /// Each variable can be a PermuteVariable
    /// </summary>
    [Serializable]
    public class ExperimentPermutationParameters : Parameters
    {
        /// <summary>
        /// The name of the field being predicted.  Any allowed permutation MUST contain the prediction field.
        /// </summary>
        public string PredictedField { get; set; }
        /// <summary>
        /// Fields selected for final hypersearch report;
        /// NOTE: These values are used as regular expressions by RunPermutations.py's report generator
        /// (fieldname values generated from PERM_PREDICTED_FIELD_NAME)
        /// </summary>
        public string[] Report { get; set; }
        /// <summary>
        /// Permutation optimization setting: either minimize or maximize metric used by RunPermutations.
        /// NOTE: These values are used as regular expressions by RunPermutations.py's report generator
        /// (generated from minimize = 'prediction:rmse:field=consumption')
        /// </summary>
        public string Minimize { get; set; }
        public string Maximize { get; set; }

        public AggregationSettings AggregationInfo { get; set; }

        /// <summary>
        /// Possible permutations for the inferences
        /// </summary>
        public object InferenceType { get; set; }

        public virtual bool PermutationFilter(AggregationSettings aggregationInfo, ExperimentPermutationParameters parameters)
        {
            return true;
        }
    }
}