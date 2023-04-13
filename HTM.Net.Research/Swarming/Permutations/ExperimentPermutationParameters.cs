using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Data;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static HTM.Net.Parameters;

namespace HTM.Net.Research.Swarming.Permutations
{
    /// <summary>
    /// Each variable can be a PermuteVariable
    /// </summary>
    [Serializable]
    [JsonConverter(typeof(ExperimentPermutationParametersConverter))]
    public class ExperimentPermutationParameters : Parameters
    {
        public int? ModelNum { get; set; }

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

        [JsonProperty(NullValueHandling = NullValueHandling.Ignore)]
        public ExperimentPermutationParameters FastSwarmModelParams { get; set; }
        public int? MaxModels { get; set; }
        public int Generation { get; set; }

        /// <summary>
        /// Checks that this parameter container has permutable values in it
        /// </summary>
        /// <returns></returns>
        public bool HasPermutations()
        {
            bool hasPermuteParams = base.GetPermutationVars().Any();

            if (Encoders.Any())
            {
                hasPermuteParams = hasPermuteParams || Encoders.Any(e => e.Value is PermuteEncoder);
            }
            hasPermuteParams = hasPermuteParams || InferenceType is PermuteVariable;
            return hasPermuteParams;
        }

        public virtual bool PermutationFilter(ExperimentPermutationParameters parameters)
        {
            return true;
        }

        /// <summary>
        /// This function can be used for Hypersearch algorithm development. When
        /// present, Hypersearch doesn't actually run the CLA model in the OPF, but instead run
        /// a dummy model.This function returns the dummy model params that will be
        /// used.See the OPFDummyModelRunner class source code(in
        /// nupic.swarming.ModelRunner) for a description of the schema for
        /// the dummy model params.
        /// </summary>
        /// <param name="parameters"></param>
        /// <param name="forTesting">set to true when this call is just for checking that the method is defined.</param>
        /// <returns></returns>
        public virtual DummyModelParameters DummyModelParams(ExperimentPermutationParameters parameters, bool forTesting)
        {
            return null;
        }

        public ExperimentPermutationParameters Union(ExperimentPermutationParameters p)
        {
            foreach (KEY k in p.Keys())
            {
                SetParameterByKey(k, p.GetParameterByKey(k));
            }
            return this;
        }

        public new ExperimentPermutationParameters Copy()
        {
            var p = new ExperimentPermutationParameters().Union(this);
            p.ModelNum = ModelNum;
            p.AggregationInfo = p.AggregationInfo?.Clone();
            if (Encoders != null) p.Encoders = new Map<string, object>(Encoders);
            p.FastSwarmModelParams = FastSwarmModelParams;
            p.FieldPermutationLimit = FieldPermutationLimit;
            p.FixedFields = FixedFields;
            p.InferenceType = InferenceType;
            p.InputPredictedField = InputPredictedField;
            p.KillUselessSwarms = KillUselessSwarms;
            p.MaxFieldBranching = MaxFieldBranching;
            p.MaxModels = MaxModels;
            p.Maximize = Maximize;
            p.Minimize = Minimize;
            p.MinFieldContribution = MinFieldContribution;
            p.PredictedField = PredictedField;
            p.MinParticlesPerSwarm = MinParticlesPerSwarm;
            p.Report = Report;
            p.Generation = Generation;
            p.TryAll3FieldCombinations = TryAll3FieldCombinations;
            p.TryAll3FieldCombinationsWTimestamps = TryAll3FieldCombinationsWTimestamps;
            return p;
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
            return Equals((ExperimentPermutationParameters)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                int hashCode = base.GetHashCode();
                hashCode = (hashCode * 397) ^ (PredictedField != null ? PredictedField.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ FieldPermutationLimit;
                hashCode = (hashCode * 397) ^ (Report != null ? Report.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Minimize != null ? Minimize.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (Maximize != null ? Maximize.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (AggregationInfo != null ? AggregationInfo.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ InputPredictedField.GetHashCode();
                hashCode = (hashCode * 397) ^ MinFieldContribution.GetHashCode();
                hashCode = (hashCode * 397) ^ MaxFieldBranching.GetHashCode();
                hashCode = (hashCode * 397) ^ (InferenceType != null ? InferenceType.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ MinParticlesPerSwarm.GetHashCode();
                hashCode = (hashCode * 397) ^ (Encoders != null ? Encoders.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ KillUselessSwarms.GetHashCode();
                hashCode = (hashCode * 397) ^ TryAll3FieldCombinations.GetHashCode();
                hashCode = (hashCode * 397) ^ TryAll3FieldCombinationsWTimestamps.GetHashCode();
                hashCode = (hashCode * 397) ^ (FixedFields != null ? FixedFields.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ (FastSwarmModelParams != null ? FastSwarmModelParams.GetHashCode() : 0);
                hashCode = (hashCode * 397) ^ MaxModels.GetHashCode();
                return hashCode;
            }
        }

        #endregion
    }

    public class ExperimentPermutationParametersConverter : BaseObjectConverter<ExperimentPermutationParameters>
    {
    }
}