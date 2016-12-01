using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Data;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;

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
        /// The maximum number of fields, including meta-fields (e.g., "timeOfDay",
        /// "dayOfWeek"), allowed in any given permutation (enforced by the filter() function.
        /// Set to 0 (zero) to suppress this check.
        /// </summary>
        public int FieldPermutationLimit { get; set; }

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
        public InputPredictedField? InputPredictedField { get; set; }
        public double? MinFieldContribution { get; set; }
        public int? MaxFieldBranching { get; set; }

        /// <summary>
        /// Possible permutations for the inferences
        /// </summary>
        public object InferenceType { get; set; }

        public int? MinParticlesPerSwarm { get; set; }
        public Map<string, object> Encoders { get; set; }
        public bool? KillUselessSwarms { get; set; }
        public bool? TryAll3FieldCombinations { get; set; }
        public bool? TryAll3FieldCombinationsWTimestamps { get; set; }
        public string[] FixedFields { get; set; }
        public ExperimentPermutationParameters FastSwarmModelParams { get; set; }
        public int? MaxModels { get; set; }

        /// <summary>
        /// Checks that this parameter container has permutable values in it
        /// </summary>
        /// <returns></returns>
        public bool HasPermutations()
        {
            bool hasPermuteParams = base.GetPermutationVars().Any();

            if (Encoders.Any())
            {
                hasPermuteParams = hasPermuteParams || Encoders.Any(e=>e.Value is PermuteEncoder);
            }
            hasPermuteParams = hasPermuteParams || InferenceType is PermuteVariable;
            return hasPermuteParams;
        }

        public virtual bool PermutationFilter(ExperimentPermutationParameters parameters)
        {
            return true;
        }

        public virtual IDictionary<string, object> DummyModelParams(ExperimentPermutationParameters parameters)
        {
            return null;
        }

        #region Equality members

        protected bool Equals(ExperimentPermutationParameters other)
        {
            return base.Equals(other) && string.Equals(PredictedField, other.PredictedField) &&
                   FieldPermutationLimit == other.FieldPermutationLimit && Equals(Report, other.Report) &&
                   string.Equals(Minimize, other.Minimize) && string.Equals(Maximize, other.Maximize) &&
                   Equals(AggregationInfo, other.AggregationInfo) && InputPredictedField == other.InputPredictedField &&
                   MinFieldContribution.Equals(other.MinFieldContribution) &&
                   MaxFieldBranching == other.MaxFieldBranching && Equals(InferenceType, other.InferenceType) &&
                   MinParticlesPerSwarm == other.MinParticlesPerSwarm && Equals(Encoders, other.Encoders) &&
                   KillUselessSwarms == other.KillUselessSwarms &&
                   TryAll3FieldCombinations == other.TryAll3FieldCombinations &&
                   TryAll3FieldCombinationsWTimestamps == other.TryAll3FieldCombinationsWTimestamps &&
                   Equals(FixedFields, other.FixedFields) && Equals(FastSwarmModelParams, other.FastSwarmModelParams) &&
                   MaxModels == other.MaxModels;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((ExperimentPermutationParameters) obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode*397) ^ (PredictedField != null ? PredictedField.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ FieldPermutationLimit;
                hashCode = (hashCode*397) ^ (Report != null ? Report.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Minimize != null ? Minimize.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (Maximize != null ? Maximize.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (AggregationInfo != null ? AggregationInfo.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ InputPredictedField.GetHashCode();
                hashCode = (hashCode*397) ^ MinFieldContribution.GetHashCode();
                hashCode = (hashCode*397) ^ MaxFieldBranching.GetHashCode();
                hashCode = (hashCode*397) ^ (InferenceType != null ? InferenceType.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MinParticlesPerSwarm.GetHashCode();
                hashCode = (hashCode*397) ^ (Encoders != null ? Encoders.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ KillUselessSwarms.GetHashCode();
                hashCode = (hashCode*397) ^ TryAll3FieldCombinations.GetHashCode();
                hashCode = (hashCode*397) ^ TryAll3FieldCombinationsWTimestamps.GetHashCode();
                hashCode = (hashCode*397) ^ (FixedFields != null ? FixedFields.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ (FastSwarmModelParams != null ? FastSwarmModelParams.GetHashCode() : 0);
                hashCode = (hashCode*397) ^ MaxModels.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }
}