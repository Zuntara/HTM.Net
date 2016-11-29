using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    public class ExpGenerator
    {
        public SwarmDefinition Options { get; set; }

        public ExpGenerator(SwarmDefinition definition)
        {
            Options = definition;
        }

        public Tuple<ClaExperimentDescription, ClaPermutations> Generate()
        {
            if (Options.streamDef == null) throw new InvalidOperationException("define 'streamDef' for datasource");
            // If the user specified nonTemporalClassification, make sure prediction steps is 0
            int[] predictionSteps = Options.inferenceArgs.predictionSteps;
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (predictionSteps != null && predictionSteps[0] != 0)
                {
                    throw new InvalidOperationException(
                        "When NontemporalClassification is used, prediction steps must be [0]");
                }
            }
            // If the user asked for 0 steps of prediction, then make this a spatial classification experiment
            if (predictionSteps != null && predictionSteps[0] == 0
                &&
                new List<InferenceType>
                {
                    InferenceType.NontemporalMultiStep,
                    InferenceType.TemporalMultiStep,
                    InferenceType.MultiStep
                }.Contains(Options.inferenceType))
            {
                Options.inferenceType = InferenceType.NontemporalClassification;
            }
            // If NontemporalClassification was chosen as the inferenceType, then the
            // predicted field can NOT be used as an input
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (Options.inferenceArgs.inputPredictedField.HasValue &&
                    (Options.inferenceArgs.inputPredictedField.Value == InputPredictedField.Yes ||
                     Options.inferenceArgs.inputPredictedField.Value == InputPredictedField.Auto))
                {
                    throw new InvalidOperationException(
                        "When the inference type is NontemporalClassification  inputPredictedField must be set to 'no'");
                }
                Options.inferenceArgs.inputPredictedField = InputPredictedField.No;
            }

            // Process the swarmSize setting, if provided
            var swarmSize = Options.swarmSize;

            if (swarmSize == null)
            {
                if (Options.inferenceArgs.inputPredictedField == null)
                {
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
                }
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Small)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 3;
                if (Options.iterationCount == null)
                    Options.iterationCount = 100;
                if (Options.maxModels == null)
                    Options.maxModels = 1;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Yes;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Medium)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 5;
                if (Options.iterationCount == null)
                    Options.iterationCount = 4000;
                if (Options.maxModels == null)
                    Options.maxModels = 200;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Large)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 15;
                Options.tryAll3FieldCombinationsWTimestamps = true;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
            }

            // Get token replacements
            Map<string, object> tokenReplacements = new Map<string, object>();

            // Generate the encoder related substitution strings

            var includedFields = Options.includedFields;

            var encoderTuple = _generateEncoderStringsV2(includedFields, Options);
            EncoderSettingsList encoderSpecs = encoderTuple.Item1;
            var permEncoderChoices = encoderTuple.Item2;

            // Generate the string containing the sensor auto-reset dict.
            /*
              if options['resetPeriod'] is not None:
                sensorAutoResetStr = pprint.pformat(options['resetPeriod'],
                                                     indent=2*_INDENT_STEP)
              else:
                sensorAutoResetStr = 'None'
            */
            var sensorAutoReset = Options.resetPeriod;

            // Generate the string containing the aggregation settings.

            var aggregationPeriod = new AggregationSettings();

            // Honor any overrides provided in the stream definition
            if (Options.streamDef.aggregation != null)
            {
                aggregationPeriod = Options.streamDef.aggregation;
            }
            // Do we have any aggregation at all?
            bool hasAggregation = aggregationPeriod.AboveZero();

            AggregationSettings aggregationInfo = aggregationPeriod.Clone();
            aggregationInfo.fields = aggregationPeriod.fields;

            // -----------------------------------------------------------------------
            // Generate the string defining the dataset. This is basically the
            // streamDef, but referencing the aggregation we already pulled out into the
            // config dict (which enables permuting over it)
            var datasetSpec = Options.streamDef;
            if (datasetSpec.aggregation != null)
            {
                datasetSpec.aggregation = null;
            }
            if (hasAggregation)
            {
                datasetSpec.aggregation = aggregationPeriod;
            }

            // -----------------------------------------------------------------------
            // Was computeInterval specified with Multistep prediction? If so, this swarm
            // should permute over different aggregations
            var computeInterval = Options.computeInterval;
            AggregationSettings predictAheadTime;
            if (computeInterval != null
                &&
                new List<InferenceType>
                {
                    InferenceType.NontemporalMultiStep,
                    InferenceType.TemporalMultiStep,
                    InferenceType.MultiStep
                }.Contains(Options.inferenceType))
            {
                // Compute the predictAheadTime based on the minAggregation (specified in
                // the stream definition) and the number of prediction steps
                predictionSteps = Options.inferenceArgs.predictionSteps ?? new[] { 1 };
                if (predictionSteps.Length > 1)
                {
                    throw new InvalidOperationException($"Invalid predictionSteps: {predictionSteps}. " +
                                                        "When computeInterval is specified, there can only be one stepSize in predictionSteps.");
                }
                if (!aggregationInfo.AboveZero())
                {
                    throw new InvalidOperationException(
                        $"Missing or nil stream aggregation: When computeInterval is specified, then the stream aggregation interval must be non-zero.");
                }

                // Compute the predictAheadTime
                int numSteps = predictionSteps[0];
                predictAheadTime = aggregationPeriod.Clone();
                predictAheadTime.MultiplyAllFieldsWith(numSteps);

                // This tells us to plug in a wildcard string for the prediction steps that
                // we use in other parts of the description file (metrics, inferenceArgs,
                // etc.)
                Options.dynamicPredictionSteps = true;
            }
            else
            {
                Options.dynamicPredictionSteps = false;
                predictAheadTime = null;
            }

            // -----------------------------------------------------------------------
            // Save environment-common token substitutions

            // We will run over the description template with reflection to assign the values to the correct fields,
            // the replacements will be marked in an attribute above the properties.

            // If the "uber" metric 'MultiStep' was specified, then plug in TemporalMultiStep
            // by default
            InferenceType inferenceType = Options.inferenceType;
            if (inferenceType == InferenceType.MultiStep)
            {
                inferenceType = InferenceType.TemporalMultiStep;
            }
            tokenReplacements["$INFERENCE_TYPE"] = inferenceType;

            // Nontemporal classification uses only encoder and classifier
            if (inferenceType == InferenceType.NontemporalClassification)
            {
                tokenReplacements["$SP_ENABLE"] = false;
                tokenReplacements["$TP_ENABLE"] = false;
            }
            else
            {
                tokenReplacements["$SP_ENABLE"] = true;
                tokenReplacements["$TP_ENABLE"] = true;
                tokenReplacements["$CLA_CLASSIFIER_IMPL"] = "";
            }

            tokenReplacements["$ANOMALY_PARAMS"] = Options.anomalyParams;

            tokenReplacements["$ENCODER_SPECS"] = encoderSpecs;
            tokenReplacements["$SENSOR_AUTO_RESET"] = sensorAutoReset;

            tokenReplacements["$AGGREGATION_INFO"] = aggregationInfo;

            tokenReplacements["$DATASET_SPEC"] = datasetSpec;

            if (!Options.iterationCount.HasValue)
            {
                Options.iterationCount = -1;
            }
            tokenReplacements["$ITERATION_COUNT"] = Options.iterationCount;

            tokenReplacements["$SP_POOL_PCT"] = Options.spCoincInputPoolPct;
            tokenReplacements["$HS_MIN_PARTICLES"] = Options.minParticlesPerSwarm;

            tokenReplacements["$SP_PERM_CONNECTED"] = Options.spSynPermConnected;
            tokenReplacements["$FIELD_PERMUTATION_LIMIT"] = Options.fieldPermutationLimit;

            tokenReplacements["$PERM_ENCODER_CHOICES"] = permEncoderChoices;

            predictionSteps = Options.inferenceArgs.predictionSteps ?? new[] { 1 };
            tokenReplacements["$PREDICTION_STEPS"] = predictionSteps;

            tokenReplacements["$PREDICT_AHEAD_TIME"] = predictAheadTime;

            // Option permuting over SP synapse decrement value
            //tokenReplacements["$PERM_SP_CHOICES"] = "";
            if (Options.spPermuteDecrement && Options.inferenceType != InferenceType.NontemporalClassification)
            {
                tokenReplacements["$PERM_SP_CHOICES_synPermInactiveDec"] = new PermuteFloat(0.0003, 0.1);
            }

            // The TP permutation parameters are not required for non-temporal networks
            if (Options.inferenceType == InferenceType.NontemporalMultiStep ||
                Options.inferenceType == InferenceType.NontemporalClassification)
            {
                //tokenReplacements["$PERM_TP_CHOICES"] = "";
            }
            else
            {
                tokenReplacements["$PERM_TP_CHOICES_activationThreshold"] = new PermuteInt(12, 16);
                tokenReplacements["$PERM_TP_CHOICES_minThreshold"] = new PermuteInt(9, 12);
                tokenReplacements["$PERM_TP_CHOICES_pamLength"] = new PermuteInt(1, 5);
            }

            // If the inference type is just the generic 'MultiStep', then permute over
            // temporal/nonTemporal multistep
            if (Options.inferenceType == InferenceType.MultiStep)
            {
                tokenReplacements["$PERM_INFERENCE_TYPE_CHOICES_inferenceType"] = new PermuteChoices(new double[] { (int)InferenceType.NontemporalMultiStep, (int)InferenceType.TemporalMultiStep });
            }
            else
            {
                //tokenReplacements["$PERM_INFERENCE_TYPE_CHOICES"] = "";
            }

            // The Classifier permutation parameters are only required for
            // Multi-step inference types
            if (new[] { InferenceType.NontemporalMultiStep, InferenceType.TemporalMultiStep, InferenceType.MultiStep,
                InferenceType.TemporalAnomaly, InferenceType.NontemporalClassification }.Contains(Options.inferenceType))
            {
                tokenReplacements["$PERM_CL_CHOICES_alpha"] = new PermuteFloat(0.0001, 0.1);
            }

            // The Permutations alwaysIncludePredictedField setting. 
            // * When the experiment description has 'inputPredictedField' set to 'no', we 
            // simply do not put in an encoder for the predicted field. 
            // * When 'inputPredictedField' is set to 'auto', we include an encoder for the 
            // predicted field and swarming tries it out just like all the other fields.
            // * When 'inputPredictedField' is set to 'yes', we include this setting in
            // the permutations file which informs swarming to always use the
            // predicted field (the first swarm will be the predicted field only) 
            tokenReplacements["$PERM_ALWAYS_INCLUDE_PREDICTED_FIELD"] = Options.inferenceArgs.inputPredictedField;

            // The Permutations minFieldContribution setting
            if (Options.minFieldContribution.HasValue) tokenReplacements["$PERM_MIN_FIELD_CONTRIBUTION"] = Options.minFieldContribution;
            // The Permutations killUselessSwarms setting
            if (Options.killUselessSwarms.HasValue) tokenReplacements["$PERM_KILL_USELESS_SWARMS"] = Options.killUselessSwarms;
            // The Permutations maxFieldBranching setting
            if (Options.maxFieldBranching.HasValue) tokenReplacements["$PERM_MAX_FIELD_BRANCHING"] = Options.maxFieldBranching;
            // The Permutations tryAll3FieldCombinations setting
            if (Options.tryAll3FieldCombinations.HasValue) tokenReplacements["$PERM_TRY_ALL_3_FIELD_COMBINATIONS"] = Options.tryAll3FieldCombinations;
            //The Permutations tryAll3FieldCombinationsWTimestamps setting
            if (Options.tryAll3FieldCombinationsWTimestamps.HasValue) tokenReplacements["$PERM_TRY_ALL_3_FIELD_COMBINATIONS_W_TIMESTAMPS"] = Options.tryAll3FieldCombinationsWTimestamps;

            // The Permutations fieldFields setting
            if (Options.fixedFields != null)
            {
                tokenReplacements["$PERM_FIXED_FIELDS"] = Options.fixedFields;
            }
            else
            {
                tokenReplacements["$PERM_FIXED_FIELDS"] = null;
            }

            // The Permutations fastSwarmModelParams setting
            if (Options.fastSwarmModelParams != null)
            {
                tokenReplacements["$PERM_FAST_SWARM_MODEL_PARAMS"] = Options.fastSwarmModelParams;
            }
            else
            {
                tokenReplacements["$PERM_FAST_SWARM_MODEL_PARAMS"] = null;
            }

            // The Permutations maxModels setting
            if (Options.maxModels.HasValue)
            {
                tokenReplacements["$PERM_MAX_MODELS"] = Options.maxModels;
            }
            else
            {
                tokenReplacements["$PERM_MAX_MODELS"] = null;
            }

            // --------------------------------------------------------------------------
            // The Aggregation choices have to be determined when we are permuting over
            // aggregations.
            if (Options.dynamicPredictionSteps)
            {
                throw new NotImplementedException("not yet implemented!");
            }
            else
            {
                tokenReplacements["$PERM_AGGREGATION_CHOICES"] = aggregationInfo;
            }

            // Generate the inferenceArgs replacement tokens
            _generateInferenceArgs(Options, tokenReplacements);

            // Generate the metric replacement tokens
            _generateMetricsSubstitutions(Options, tokenReplacements);

            // Generate input record schema
            _generateInputRecordSchema(Options, tokenReplacements);

            // -----------------------------------------------------------------------
            // Generate Control dictionary

            tokenReplacements["$ENVIRONMENT"] = "Nupic";

            // Generate 'files' / descriptions etc from the token replacements
            ClaExperimentDescription descr = new ClaExperimentDescription();
            TokenReplacer.ReplaceIn(descr, tokenReplacements);

            ClaPermutations perms = new ClaPermutations();
            TokenReplacer.ReplaceIn(perms, tokenReplacements);

            //Debug.WriteLine("");
            //Debug.WriteLine(JsonConvert.SerializeObject(descr, Formatting.Indented));
            //Debug.WriteLine("");
            //Debug.WriteLine(JsonConvert.SerializeObject(perms, Formatting.Indented));

            return new Tuple<ClaExperimentDescription, ClaPermutations>(descr, perms);
        }

        public Tuple<ClaExperimentParameters, ClaPermutations> GenerateParams()
        {
            if (Options.streamDef == null) throw new InvalidOperationException("define 'streamDef' for datasource");
            // If the user specified nonTemporalClassification, make sure prediction steps is 0
            int[] predictionSteps = Options.inferenceArgs.predictionSteps;
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (predictionSteps != null && predictionSteps[0] != 0)
                {
                    throw new InvalidOperationException(
                        "When NontemporalClassification is used, prediction steps must be [0]");
                }
            }
            // If the user asked for 0 steps of prediction, then make this a spatial classification experiment
            if (predictionSteps != null && predictionSteps[0] == 0
                &&
                new List<InferenceType>
                {
                    InferenceType.NontemporalMultiStep,
                    InferenceType.TemporalMultiStep,
                    InferenceType.MultiStep
                }.Contains(Options.inferenceType))
            {
                Options.inferenceType = InferenceType.NontemporalClassification;
            }
            // If NontemporalClassification was chosen as the inferenceType, then the
            // predicted field can NOT be used as an input
            if (Options.inferenceType == InferenceType.NontemporalClassification)
            {
                if (Options.inferenceArgs.inputPredictedField.HasValue &&
                    (Options.inferenceArgs.inputPredictedField.Value == InputPredictedField.Yes ||
                     Options.inferenceArgs.inputPredictedField.Value == InputPredictedField.Auto))
                {
                    throw new InvalidOperationException(
                        "When the inference type is NontemporalClassification  inputPredictedField must be set to 'no'");
                }
                Options.inferenceArgs.inputPredictedField = InputPredictedField.No;
            }

            // Process the swarmSize setting, if provided
            var swarmSize = Options.swarmSize;

            if (swarmSize == null)
            {
                if (Options.inferenceArgs.inputPredictedField == null)
                {
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
                }
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Small)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 3;
                if (Options.iterationCount == null)
                    Options.iterationCount = 100;
                if (Options.maxModels == null)
                    Options.maxModels = 1;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Yes;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Medium)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 5;
                if (Options.iterationCount == null)
                    Options.iterationCount = 4000;
                if (Options.maxModels == null)
                    Options.maxModels = 200;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
            }
            else if (swarmSize.Value == SwarmDefinition.SwarmSize.Large)
            {
                if (Options.minParticlesPerSwarm == null)
                    Options.minParticlesPerSwarm = 15;
                Options.tryAll3FieldCombinationsWTimestamps = true;
                if (Options.inferenceArgs.inputPredictedField == null)
                    Options.inferenceArgs.inputPredictedField = InputPredictedField.Auto;
            }

            // Get token replacements
            ClaExperimentParameters claParameters = ClaExperimentParameters.Default();
            Map<string, object> tokenReplacements = new Map<string, object>();

            // Generate the encoder related substitution strings

            var includedFields = Options.includedFields;

            var encoderTuple = _generateEncoderStringsV2(includedFields, Options);
            EncoderSettingsList encoderSpecs = encoderTuple.Item1;
            var permEncoderChoices = encoderTuple.Item2;

            // Generate the string containing the sensor auto-reset dict.
            var sensorAutoReset = Options.resetPeriod;

            // Generate the string containing the aggregation settings.
            var aggregationPeriod = new AggregationSettings();

            // Honor any overrides provided in the stream definition
            if (Options.streamDef.aggregation != null)
            {
                aggregationPeriod = Options.streamDef.aggregation;
            }
            // Do we have any aggregation at all?
            bool hasAggregation = aggregationPeriod.AboveZero();

            AggregationSettings aggregationInfo = aggregationPeriod.Clone();
            aggregationInfo.fields = aggregationPeriod.fields;

            // -----------------------------------------------------------------------
            // Generate the string defining the dataset. This is basically the
            // streamDef, but referencing the aggregation we already pulled out into the
            // config dict (which enables permuting over it)
            var datasetSpec = Options.streamDef;
            if (datasetSpec.aggregation != null)
            {
                datasetSpec.aggregation = null;
            }
            if (hasAggregation)
            {
                datasetSpec.aggregation = aggregationPeriod;
            }

            // -----------------------------------------------------------------------
            // Was computeInterval specified with Multistep prediction? If so, this swarm
            // should permute over different aggregations
            var computeInterval = Options.computeInterval;
            AggregationSettings predictAheadTime;
            if (computeInterval != null
                &&
                new List<InferenceType>
                {
                    InferenceType.NontemporalMultiStep,
                    InferenceType.TemporalMultiStep,
                    InferenceType.MultiStep
                }.Contains(Options.inferenceType))
            {
                // Compute the predictAheadTime based on the minAggregation (specified in
                // the stream definition) and the number of prediction steps
                predictionSteps = Options.inferenceArgs.predictionSteps ?? new[] { 1 };
                if (predictionSteps.Length > 1)
                {
                    throw new InvalidOperationException($"Invalid predictionSteps: {predictionSteps}. " +
                                                        "When computeInterval is specified, there can only be one stepSize in predictionSteps.");
                }
                if (!aggregationInfo.AboveZero())
                {
                    throw new InvalidOperationException(
                        $"Missing or nil stream aggregation: When computeInterval is specified, then the stream aggregation interval must be non-zero.");
                }

                // Compute the predictAheadTime
                int numSteps = predictionSteps[0];
                predictAheadTime = aggregationPeriod.Clone();
                predictAheadTime.MultiplyAllFieldsWith(numSteps);

                // This tells us to plug in a wildcard string for the prediction steps that
                // we use in other parts of the description file (metrics, inferenceArgs,
                // etc.)
                Options.dynamicPredictionSteps = true;
            }
            else
            {
                Options.dynamicPredictionSteps = false;
                predictAheadTime = null;
            }

            // -----------------------------------------------------------------------
            // Save environment-common token substitutions

            // We will run over the description template with reflection to assign the values to the correct fields,
            // the replacements will be marked in an attribute above the properties.

            // If the "uber" metric 'MultiStep' was specified, then plug in TemporalMultiStep
            // by default
            InferenceType inferenceType = Options.inferenceType;
            if (inferenceType == InferenceType.MultiStep)
            {
                inferenceType = InferenceType.TemporalMultiStep;
            }
            claParameters.InferenceType = inferenceType;

            // Nontemporal classification uses only encoder and classifier
            if (inferenceType == InferenceType.NontemporalClassification)
            {
                claParameters.EnableSpatialPooler = false;
                claParameters.EnableTemporalMemory = false;
            }
            else
            {
                claParameters.EnableSpatialPooler = true;
                claParameters.EnableTemporalMemory = true;
               // tokenReplacements["$CLA_CLASSIFIER_IMPL"] = "";
            }

            //tokenReplacements["$ANOMALY_PARAMS"] = Options.anomalyParams;
            claParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Options.anomalyParams?.mode);
            claParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, Options.anomalyParams?.slidingWindowSize);

            claParameters.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, encoderSpecs);

            //tokenReplacements["$SENSOR_AUTO_RESET"] = sensorAutoReset;
            claParameters.SensorAutoReset = sensorAutoReset;

            //tokenReplacements["$AGGREGATION_INFO"] = aggregationInfo;
            claParameters.AggregationInfo = aggregationInfo;

            //tokenReplacements["$DATASET_SPEC"] = datasetSpec;
            claParameters.DatasetSpec = datasetSpec;

            if (!Options.iterationCount.HasValue)
            {
                Options.iterationCount = -1;
            }
            //tokenReplacements["$ITERATION_COUNT"] = Options.iterationCount;
            claParameters.IterationCount = Options.iterationCount;

            tokenReplacements["$SP_POOL_PCT"] = Options.spCoincInputPoolPct;
            tokenReplacements["$HS_MIN_PARTICLES"] = Options.minParticlesPerSwarm;

            tokenReplacements["$SP_PERM_CONNECTED"] = Options.spSynPermConnected;
            tokenReplacements["$FIELD_PERMUTATION_LIMIT"] = Options.fieldPermutationLimit;

            tokenReplacements["$PERM_ENCODER_CHOICES"] = permEncoderChoices;

            predictionSteps = Options.inferenceArgs.predictionSteps ?? new[] { 1 };
            //tokenReplacements["$PREDICTION_STEPS"] = predictionSteps;
            claParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_STEPS, predictionSteps);

            tokenReplacements["$PREDICT_AHEAD_TIME"] = predictAheadTime;

            // Option permuting over SP synapse decrement value
            //tokenReplacements["$PERM_SP_CHOICES"] = "";
            if (Options.spPermuteDecrement && Options.inferenceType != InferenceType.NontemporalClassification)
            {
                tokenReplacements["$PERM_SP_CHOICES_synPermInactiveDec"] = new PermuteFloat(0.0003, 0.1);
            }

            // The TP permutation parameters are not required for non-temporal networks
            if (Options.inferenceType == InferenceType.NontemporalMultiStep ||
                Options.inferenceType == InferenceType.NontemporalClassification)
            {
                //tokenReplacements["$PERM_TP_CHOICES"] = "";
            }
            else
            {
                tokenReplacements["$PERM_TP_CHOICES_activationThreshold"] = new PermuteInt(12, 16);
                tokenReplacements["$PERM_TP_CHOICES_minThreshold"] = new PermuteInt(9, 12);
                tokenReplacements["$PERM_TP_CHOICES_pamLength"] = new PermuteInt(1, 5);
            }

            // If the inference type is just the generic 'MultiStep', then permute over
            // temporal/nonTemporal multistep
            if (Options.inferenceType == InferenceType.MultiStep)
            {
                tokenReplacements["$PERM_INFERENCE_TYPE_CHOICES_inferenceType"] = new PermuteChoices(new double[] { (int)InferenceType.NontemporalMultiStep, (int)InferenceType.TemporalMultiStep });
            }
            else
            {
                //tokenReplacements["$PERM_INFERENCE_TYPE_CHOICES"] = "";
            }

            // The Classifier permutation parameters are only required for
            // Multi-step inference types
            if (new[] { InferenceType.NontemporalMultiStep, InferenceType.TemporalMultiStep, InferenceType.MultiStep,
                InferenceType.TemporalAnomaly, InferenceType.NontemporalClassification }.Contains(Options.inferenceType))
            {
                tokenReplacements["$PERM_CL_CHOICES_alpha"] = new PermuteFloat(0.0001, 0.1);
            }

            // The Permutations alwaysIncludePredictedField setting. 
            // * When the experiment description has 'inputPredictedField' set to 'no', we 
            // simply do not put in an encoder for the predicted field. 
            // * When 'inputPredictedField' is set to 'auto', we include an encoder for the 
            // predicted field and swarming tries it out just like all the other fields.
            // * When 'inputPredictedField' is set to 'yes', we include this setting in
            // the permutations file which informs swarming to always use the
            // predicted field (the first swarm will be the predicted field only) 
            tokenReplacements["$PERM_ALWAYS_INCLUDE_PREDICTED_FIELD"] = Options.inferenceArgs.inputPredictedField;

            // The Permutations minFieldContribution setting
            if (Options.minFieldContribution.HasValue) tokenReplacements["$PERM_MIN_FIELD_CONTRIBUTION"] = Options.minFieldContribution;
            // The Permutations killUselessSwarms setting
            if (Options.killUselessSwarms.HasValue) tokenReplacements["$PERM_KILL_USELESS_SWARMS"] = Options.killUselessSwarms;
            // The Permutations maxFieldBranching setting
            if (Options.maxFieldBranching.HasValue) tokenReplacements["$PERM_MAX_FIELD_BRANCHING"] = Options.maxFieldBranching;
            // The Permutations tryAll3FieldCombinations setting
            if (Options.tryAll3FieldCombinations.HasValue) tokenReplacements["$PERM_TRY_ALL_3_FIELD_COMBINATIONS"] = Options.tryAll3FieldCombinations;
            //The Permutations tryAll3FieldCombinationsWTimestamps setting
            if (Options.tryAll3FieldCombinationsWTimestamps.HasValue) tokenReplacements["$PERM_TRY_ALL_3_FIELD_COMBINATIONS_W_TIMESTAMPS"] = Options.tryAll3FieldCombinationsWTimestamps;

            // The Permutations fieldFields setting
            if (Options.fixedFields != null)
            {
                tokenReplacements["$PERM_FIXED_FIELDS"] = Options.fixedFields;
            }
            else
            {
                tokenReplacements["$PERM_FIXED_FIELDS"] = null;
            }

            // The Permutations fastSwarmModelParams setting
            if (Options.fastSwarmModelParams != null)
            {
                tokenReplacements["$PERM_FAST_SWARM_MODEL_PARAMS"] = Options.fastSwarmModelParams;
            }
            else
            {
                tokenReplacements["$PERM_FAST_SWARM_MODEL_PARAMS"] = null;
            }

            // The Permutations maxModels setting
            if (Options.maxModels.HasValue)
            {
                tokenReplacements["$PERM_MAX_MODELS"] = Options.maxModels;
            }
            else
            {
                tokenReplacements["$PERM_MAX_MODELS"] = null;
            }

            // --------------------------------------------------------------------------
            // The Aggregation choices have to be determined when we are permuting over
            // aggregations.
            if (Options.dynamicPredictionSteps)
            {
                throw new NotImplementedException("not yet implemented!");
            }
            else
            {
                tokenReplacements["$PERM_AGGREGATION_CHOICES"] = aggregationInfo;
            }

            // Generate the inferenceArgs replacement tokens
            _generateInferenceArgs(Options, tokenReplacements);

            // Generate the metric replacement tokens
            _generateMetricsSubstitutions(Options, tokenReplacements);

            // Generate input record schema
            _generateInputRecordSchema(Options, tokenReplacements);

            // -----------------------------------------------------------------------
            // Generate Control dictionary

            tokenReplacements["$ENVIRONMENT"] = "Nupic";

            // Generate 'files' / descriptions etc from the token replacements
           
            ClaPermutations perms = new ClaPermutations();
            TokenReplacer.ReplaceIn(perms, tokenReplacements);

            //Debug.WriteLine("");
            //Debug.WriteLine(JsonConvert.SerializeObject(descr, Formatting.Indented));
            //Debug.WriteLine("");
            //Debug.WriteLine(JsonConvert.SerializeObject(perms, Formatting.Indented));

            return new Tuple<ClaExperimentParameters, ClaPermutations>(claParameters, perms);
        }

        /// <summary>
        /// Generate the token substitution for metrics related fields.
        /// </summary>
        /// <param name="options"></param>
        /// <param name="tokenReplacements"></param>
        private void _generateMetricsSubstitutions(SwarmDefinition options, Map<string, object> tokenReplacements)
        {
            // -----------------------------------------------------------------------
            //
            options.loggedMetrics = new[] { ".*" };
            // -----------------------------------------------------------------------
            // Generate the required metrics
            var mSpecs = _generateMetricSpecs(options);
            MetricSpec[] metricList = mSpecs.Item1;
            var optimizeMetricLabel = mSpecs.Item2;

            tokenReplacements["$PERM_OPTIMIZE_SETTING"] = optimizeMetricLabel;
            tokenReplacements["$LOGGED_METRICS"] = options.loggedMetrics;
            tokenReplacements["$METRICS"] = metricList;
        }

        /// <summary>
        /// Generates the Metrics for a given InferenceType
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private Tuple<MetricSpec[], string> _generateMetricSpecs(SwarmDefinition options)
        {
            var inferenceType = options.inferenceType;
            var inferenceArgs = options.inferenceArgs;
            var predictionSteps = inferenceArgs.predictionSteps;
            var metricWindow = options.metricWindow;
            if (!metricWindow.HasValue)
            {
                metricWindow = SwarmConfiguration.opf_metricWindow; // 1000
            }
            List<MetricSpec> metricSpecs = new List<MetricSpec>();
            string optimizeMetricLabel = "";

            metricSpecs.AddRange(_generateExtraMetricSpecs(options));

            MetricSpec optimizeMetricSpec = null;
            // If using a dynamically computed prediction steps (i.e. when swarming
            // over aggregation is requested), then we will plug in the variable
            // predictionSteps in place of the statically provided predictionSteps
            // from the JSON description.
            if (options.dynamicPredictionSteps)
            {
                Debug.Assert(predictionSteps.Length == 1);
                //predictionSteps = "";
                throw new NotImplementedException("Dynamic prediction steps is still a problem");
            }

            // Metrics for temporal prediction
            if (new[] {InferenceType.TemporalNextStep,
                InferenceType.TemporalAnomaly,
                InferenceType.TemporalMultiStep,
                InferenceType.NontemporalMultiStep,
                InferenceType.NontemporalClassification, InferenceType.MultiStep }.Contains(inferenceType))
            {
                var predFieldTuple = _getPredictedField(options);
                string predictedFieldName = predFieldTuple.Item1;
                FieldMetaType? predictedFieldType = predFieldTuple.Item2;
                bool isCategory = _isCategory(predictedFieldType);
                string[] metricNames = isCategory ? new[] { "avg_err" } : new[] { "aae", "altMAPE" }; // aae ipv rmse
                string trivialErrorMetric = isCategory ? "avg_err" : "altMAPE";
                string oneGramErrorMetric = isCategory ? "avg_err" : "altMAPE";
                string movingAverageBaselineName = isCategory ? "moving_mode" : "moving_mean";

                MetricSpec metricSpec = null;
                string metricLabel = null;
                // Multi-step metrics
                foreach (string metricName in metricNames)
                {
                    metricSpec = new MetricSpec("multistep", InferenceElement.MultiStepBestPredictions, predictedFieldName, new Map<string, object>
                    {
                        {"errorMetric", metricName },
                        {"window", metricWindow },
                        {"steps", predictionSteps }
                    });
                    metricLabel = metricSpec.getLabel();
                    metricSpecs.Add(metricSpec);
                }
                //If the custom error metric was specified, add that
                if (Options.customErrorMetric != null)
                {
                    throw new NotImplementedException("Check nupic code for impl, not yet supported.");
                }
                // If this is the first specified step size, optimize for it. Be sure to
                // escape special characters since this is a regular expression
                optimizeMetricSpec = metricSpec;
                metricLabel = metricLabel.Replace("[", "\\[").Replace("]", "\\]");
                optimizeMetricLabel = metricLabel;

                if (Options.customErrorMetric != null)
                {
                    optimizeMetricLabel = ".*custom_error_metric.*";
                }

                // Add in the trivial metrics
                if (options.runBaseLines && inferenceType != InferenceType.NontemporalClassification)
                {
                    foreach (var steps in predictionSteps)
                    {
                        metricSpecs.Add(new MetricSpec("trivial", InferenceElement.Prediction, predictedFieldName,
                            new Map<string, object> { { "window", metricWindow }, { "errorMetric", trivialErrorMetric }, { "steps", steps } }));
                        // Include the baseline moving mean/mode metric
                        if (isCategory)
                        {
                            metricSpecs.Add(new MetricSpec(movingAverageBaselineName, InferenceElement.Prediction,
                                predictedFieldName,
                                new Map<string, object>
                                {
                                    {"window", metricWindow},
                                    {"errorMetric", "avg_err"},
                                    {"steps", steps},
                                    {"mode_window", 200}
                                }));
                        }
                        else
                        {
                            metricSpecs.Add(new MetricSpec(movingAverageBaselineName, InferenceElement.Prediction,
                                predictedFieldName,
                                new Map<string, object>
                                {
                                    {"window", metricWindow},
                                    {"errorMetric", "altMAPE"},
                                    {"steps", steps},
                                    {"mean_window", 200}
                                }));
                        }
                    }
                }
            }
            else if (inferenceType == InferenceType.TemporalClassification)
            {
                var metricName = "avg_err";
                var trivialErrorMetric = "avg_err";
                var oneGramErrorMetric = "avg_err";
                var movingAverageBaselineName = "moving_mode";

                optimizeMetricSpec = new MetricSpec(metricName, InferenceElement.Classification, null,
                    new Map<string, object>
                    {
                        {"window", metricWindow},
                    });
                optimizeMetricLabel = optimizeMetricSpec.getLabel();
                metricSpecs.Add(optimizeMetricSpec);

                if (options.runBaseLines)
                {
                    // If temporal, generate the trivial predictor metric
                    if (inferenceType == InferenceType.TemporalClassification)
                    {
                        metricSpecs.Add(new MetricSpec("trivial", InferenceElement.Classification,
                            null,
                            new Map<string, object>
                            {
                                {"window", metricWindow},
                                {"errorMetric", trivialErrorMetric}
                            }));
                        metricSpecs.Add(new MetricSpec("two_gram", InferenceElement.Classification,
                            null,
                            new Map<string, object>
                            {
                                {"window", metricWindow},
                                {"errorMetric", oneGramErrorMetric}
                            }));
                        metricSpecs.Add(new MetricSpec(movingAverageBaselineName, InferenceElement.Classification,
                            null,
                            new Map<string, object>
                            {
                                {"window", metricWindow},
                                {"errorMetric", "avg_err"},
                                {"mode_window", 200},
                            }));
                    }
                }

                // Custom error metric
                if (options.customErrorMetric != null)
                {
                    throw new NotImplementedException("not yet implmented, check online");
                }
            }

            // -----------------------------------------------------------------------
            // If plug in the predictionSteps variable for any dynamically generated
            // prediction steps
            if (options.dynamicPredictionSteps)
            {
                throw new NotImplementedException("not yet implmented, check online");
            }


            return new Tuple<MetricSpec[], string>(metricSpecs.ToArray(), optimizeMetricLabel);
        }

        private bool _isCategory(FieldMetaType? fieldType)
        {
            if (fieldType == FieldMetaType.String) return true;
            return false;
        }

        /// <summary>
        /// Generates the non-default metrics specified by the expGenerator params
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private List<MetricSpec> _generateExtraMetricSpecs(SwarmDefinition options)
        {
            if (options.metrics == null) return new List<MetricSpec>();

            //if metric['logged']: options['loggedMetrics'].append(label)

            return options.metrics.ToList();
        }

        /// <summary>
        /// Generates the token substitutions related to the predicted field and the supplemental arguments for prediction
        /// </summary>
        /// <param name="options"></param>
        /// <param name="tokenReplacements"></param>
        private void _generateInferenceArgs(SwarmDefinition options, Map<string, object> tokenReplacements)
        {
            var inferenceType = options.inferenceType;
            var optionInferenceArgs = options.inferenceArgs;
            InferenceArgsDescription resultInferenceArgs = new InferenceArgsDescription();
            string predictedField = _getPredictedField(options).Item1;

            if (inferenceType == InferenceType.TemporalNextStep ||
                inferenceType == InferenceType.TemporalAnomaly)
            {
                Debug.Assert(predictedField != null, $"Inference Type '{inferenceType}' needs a predictedField specified in the inferenceArgs dictionary");
            }
            if (optionInferenceArgs != null)
            {
                // If we will be using a dynamically created predictionSteps, plug in that
                // variable name in place of the constant scalar value
                if (options.dynamicPredictionSteps)
                {
                    var altOptionInferenceArgs =
                        Json.Deserialize<InferenceArgsDescription>(Json.Serialize(optionInferenceArgs));
                    //altOptionInferenceArgs.predictionSteps = new[] {predictionSteps};
                    //resultInferenceArgs
                    throw new NotImplementedException("wtf?");
                }
                else
                {
                    resultInferenceArgs = optionInferenceArgs;
                }
            }
            tokenReplacements["$PREDICTED_FIELD"] = predictedField;
            tokenReplacements["$PREDICTED_FIELD_report"] = new[] { ".*" + predictedField + ".*" };
            tokenReplacements["$INFERENCE_ARGS"] = resultInferenceArgs;
        }

        /// <summary>
        /// Gets the predicted field and it's datatype from the options dictionary
        /// </summary>
        /// <param name="options"></param>
        /// <returns></returns>
        private Tuple<string, FieldMetaType?> _getPredictedField(SwarmDefinition options)
        {
            if (options.inferenceArgs == null || options.inferenceArgs.predictedField == null)
            {
                return new Tuple<string, FieldMetaType?>(null, null);
            }
            string predictedField = options.inferenceArgs.predictedField;
            var includedFields = options.includedFields;

            SwarmDefinition.SwarmDefIncludedField predictedFieldInfo = includedFields.FirstOrDefault(info => info.fieldName == predictedField);
            if (predictedFieldInfo == null)
            {
                throw new InvalidOperationException($"Predicted field {predictedField} does not exist in included fields.");
            }
            var predictedFieldType = predictedFieldInfo.fieldType;
            return new Tuple<string, FieldMetaType?>(predictedField, predictedFieldType);
        }


        private Tuple<EncoderSettingsList, Map<string, object>> _generateEncoderStringsV2(
            List<SwarmDefinition.SwarmDefIncludedField> includedFields, SwarmDefinition options)
        {
            int width = 21;
            List<EncoderSetting> encoderDictList = new List<EncoderSetting>();

            string classifierOnlyField = null;

            // If this is a NontemporalClassification experiment, then the
            // the "predicted" field (the classification value) should be marked to ONLY 
            // go to the classifier
            if (new List<InferenceType>
            {
                InferenceType.NontemporalMultiStep,
                InferenceType.TemporalMultiStep,
                InferenceType.MultiStep
            }.Contains(options.inferenceType))
            {
                classifierOnlyField = options.inferenceArgs.predictedField;
            }

            // ==========================================================================
            // For each field, generate the default encoding dict and PermuteEncoder
            // constructor arguments
            foreach (var fieldInfo in includedFields)
            {
                var encoderDict = new EncoderSetting();
                string fieldName = fieldInfo.fieldName;
                FieldMetaType? fieldType = fieldInfo.fieldType;

                // scalar?
                if (fieldType == FieldMetaType.Float || fieldType == FieldMetaType.Integer)
                {
                    // n=100 is reasonably hardcoded value for n when used by description.py
                    // The swarming will use PermuteEncoder below, where n is variable and
                    // depends on w
                    bool runDelta = fieldInfo.runDelta.GetValueOrDefault();
                    if (runDelta || !string.IsNullOrWhiteSpace(fieldInfo.space))
                    {
                        encoderDict = new EncoderSetting
                        {
                            type = "ScalarSpaceEncoder",
                            name = fieldName,
                            fieldName = fieldName,
                            n = 100,
                            w = width,
                            clipInput = true,
                        };
                        if (runDelta)
                        {
                            encoderDict.runDelta = true;
                        }
                    }
                    else
                    {
                        encoderDict = new EncoderSetting
                        {
                            type = "AdaptiveScalarEncoder",
                            name = fieldName,
                            fieldName = fieldName,
                            n = 100,
                            w = width,
                            clipInput = true,
                        };
                    }

                    if (fieldInfo.minValue.HasValue)
                    {
                        encoderDict.minVal = fieldInfo.minValue.Value;
                    }
                    if (fieldInfo.maxValue.HasValue)
                    {
                        encoderDict.maxVal = fieldInfo.maxValue.Value;
                    }
                    // If both min and max were specified, use a non-adaptive encoder
                    if (fieldInfo.minValue.HasValue && fieldInfo.maxValue.HasValue
                        && encoderDict.type == "AdaptiveScalarEncoder")
                    {
                        encoderDict.type = "ScalarEncoder";
                    }
                    // Defaults may have been over-ridden by specifying an encoder type
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict.type = fieldInfo.encoderType;
                    }

                    if (!string.IsNullOrWhiteSpace(fieldInfo.space))
                    {
                        encoderDict.space = fieldInfo.space;
                    }
                    encoderDictList.Add(encoderDict);
                }

                // String?
                else if (fieldType == FieldMetaType.String)
                {
                    encoderDict = new EncoderSetting
                    {
                        type = "SDRCategoryEncoder",
                        name = fieldName,
                        fieldName = fieldName,
                        n = 100 + width,
                        w = width
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict.type = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);
                }

                // DateTime?
                else if (fieldType == FieldMetaType.DateTime)
                {
                    // First, the time of day representation
                    encoderDict = new EncoderSetting
                    {
                        type = "DateEncoder",
                        name = $"{fieldName}_timeOfDay",
                        fieldName = fieldName,
                        timeOfDay = new Tuple(width, 1)
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict.type = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);

                    // Now, the day of week representation
                    encoderDict = new EncoderSetting
                    {
                        type = "DateEncoder",
                        name = $"{fieldName}_dayOfWeek",
                        fieldName = fieldName,
                        dayOfWeek = new Tuple(width, 1)
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict.type = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);

                    // Now, the weekend representation
                    encoderDict = new EncoderSetting
                    {
                        type = "DateEncoder",
                        name = $"{fieldName}_weekend",
                        fieldName = fieldName,
                        dayOfWeek = new Tuple(width)
                    };
                    if (!string.IsNullOrWhiteSpace(fieldInfo.encoderType))
                    {
                        encoderDict.type = fieldInfo.encoderType;
                    }
                    encoderDictList.Add(encoderDict);
                }
                else
                {
                    throw new InvalidOperationException("Unsupported field type " + fieldType);
                }

                // -----------------------------------------------------------------------
                // If this was the predicted field, insert another encoder that sends it
                // to the classifier only
                if (fieldName == classifierOnlyField)
                {
                    var clEncoderDict = encoderDict.Clone();
                    clEncoderDict.classifierOnly = true;
                    clEncoderDict.name = "_classifierInput";
                    encoderDictList.Add(clEncoderDict);

                    // If the predicted field needs to be excluded, take it out of the encoder lists
                    if (options.inferenceArgs.inputPredictedField == InputPredictedField.No)
                    {
                        encoderDictList.Remove(encoderDict);
                    }
                }
            }

            // Remove any encoders not in fixedFields
            //if (options.fixedFields != null)
            //{
            //    // TODO
            //}

            // ==========================================================================
            // Now generate the encoderSpecsStr and permEncoderChoicesStr strings from 
            // encoderDictsList and constructorStringList

            Map<string, object> permEncodersList = new Map<string, object>();
            EncoderSettingsList encoders = new EncoderSettingsList();
            foreach (EncoderSetting encoderDict in encoderDictList)
            {
                if (encoderDict.name.Contains("\\"))
                {
                    throw new InvalidOperationException("Illegal character in field: '\\'");
                }

                // Check for bad characters (?)

                object encoderPerms = _generatePermEncoderStr(options, encoderDict);
                string encoderKey = encoderDict.name;
                //encoderSpecsList.Add($"{encoderKey}: {encoderDict}");
                encoders.Add(encoderKey, encoderDict);
                permEncodersList.Add(encoderKey, encoderPerms);
            }

            return new Tuple<EncoderSettingsList, Map<string, object>>(encoders, permEncodersList);
        }

        private PermuteEncoder _generatePermEncoderStr(SwarmDefinition options, EncoderSetting encoderDict)
        {
            PermuteEncoder enc = new PermuteEncoder(encoderDict.fieldName, encoderDict.type, encoderDict.name);
            if (encoderDict.classifierOnly.GetValueOrDefault(false))
            {
                foreach (string origKey in encoderDict.Keys)
                {
                    string key = origKey;
                    object value = encoderDict[key];
                    if (key == "fieldname") key = "fieldName";
                    else if (key == "type") key = "encoderType";
                    if (key == "name") continue;

                    if (key == "n" && encoderDict.type != "SDRCategoryEncoder")
                    {
                        enc.n = new PermuteInt(encoderDict.w.Value + 1, (int)encoderDict.w.Value + 500);
                    }
                    else
                    {
                        // Set other props on encoder
                        enc[key] = value;
                        //typeof(PermuteEncoder).GetProperty(key).SetValue(enc, encoderDict[key]);
                    }
                }
            }
            else
            {
                // Scalar encoders
                if (new[] { "ScalarSpaceEncoder", "AdaptiveScalarEncoder",
                    "ScalarEncoder", "LogEncoder"}.Contains(encoderDict.type))
                {
                    foreach (string origKey in encoderDict.Keys)
                    {
                        string key = origKey;
                        object value = encoderDict[key];
                        if (key == "fieldname") key = "fieldName";
                        else if (key == "type") key = "encoderType";
                        else if (key == "name") continue;

                        if (key == "n")
                        {
                            enc.n = new PermuteInt(encoderDict.w.Value + 1, (int)encoderDict.w.Value + 500);
                        }
                        else if (key == "runDelta")
                        {
                            if (value != null && !encoderDict.HasSpace())
                            {
                                // enc.space = new PermuteChoices("delta", "absolute");
                            }
                            encoderDict.runDelta = null;
                        }
                        else
                        {
                            enc[key] = value;
                            // Set other props on encoder
                            //var prop = typeof(PermuteEncoder).GetProperty(key);
                            //prop.SetValue(enc, value);
                        }
                    }
                }
                // Category encoder    
                else if (new[] { "SDRCategoryEncoder" }.Contains(encoderDict.type))
                {
                    foreach (string origKey in encoderDict.Keys)
                    {
                        string key = origKey;
                        object value = encoderDict[key];
                        if (key == "fieldname") key = "fieldName";
                        else if (key == "type") key = "encoderType";
                        else if (key == "name") continue;

                        // Set other props on encoder
                        enc[key] = value;
                        //var prop = typeof(PermuteEncoder).GetProperty(key);
                        //prop.SetValue(enc, value);
                    }
                }
                // DateTime encoder    
                else if (new[] { "DateEncoder" }.Contains(encoderDict.type))
                {
                    string encoderType = (string)encoderDict["type"];
                    foreach (string origKey in encoderDict.Keys)
                    {
                        string key = origKey;
                        object value = encoderDict[key];
                        if (key == "fieldname") key = "fieldName";
                        else if (key == "name") continue;

                        if (key == "timeOfDay")
                        {
                            enc.encoderType = $"{encoderType}.timeOfDay";
                            enc.radius = new PermuteFloat(0.5, 12);
                            enc.w = ((Tuple)value).Get(0);
                        }
                        else if (key == "dayOfWeek")
                        {
                            enc.encoderType = $"{encoderType}.dayOfWeek";
                            enc.radius = new PermuteFloat(1, 6);
                            enc.w = ((Tuple)value).Get(0);
                        }
                        else if (key == "weekend")
                        {
                            enc.encoderType = $"{encoderType}.weekend";
                            enc.radius = new PermuteChoices(new double[] { 1 });
                            enc.w = ((Tuple)value).Get(0);
                        }
                        else
                        {
                            // Set other props on encoder
                            enc[key] = value;
                            //var prop = typeof(PermuteEncoder).GetProperty(key);
                            //prop.SetValue(enc, value);
                        }
                    }
                }
                else
                {
                    throw new InvalidOperationException($"Unsupported encoder type '{encoderDict.type}'");
                }
            }
            return enc;
        }

        private void _generateInputRecordSchema(SwarmDefinition options, Map<string, object> tokenReplacements)
        {
            if (options.includedFields == null)
            {
                throw new InvalidOperationException("'includedFields' are missing from swarm definition.");
            }

            FieldMetaInfo[] infos = new FieldMetaInfo[options.includedFields.Count];
            for (int i = 0; i < options.includedFields.Count; i++)
            {
                var includedField = options.includedFields[i];

                FieldMetaInfo fmi = new FieldMetaInfo(includedField.fieldName, includedField.fieldType, includedField.specialType);
                infos[i] = fmi;
            }

            tokenReplacements["$INPUT_RECORD_SCHEMA"] = infos;
        }
    }
}