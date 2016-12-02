using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming.Experiments
{
    [Serializable]
    public class DeltaDescriptionParameters : ExperimentParameters
    {
        public DeltaDescriptionParameters()
        {
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            // Properties
            SetProperties();

            // Spatial defaults
            SetParameterByKey(KEY.SP_VERBOSITY, 0);
            SetParameterByKey(KEY.GLOBAL_INHIBITION, true);
            SetParameterByKey(KEY.POTENTIAL_PCT, 0.5);
            SetParameterByKey(KEY.COLUMN_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
            SetParameterByKey(KEY.SEED_SP, 1956);
            SetParameterByKey(KEY.RANDOM_SP, new XorshiftRandom((int)paramMap[KEY.SEED_SP]));
            SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.1);
            SetParameterByKey(KEY.SYN_PERM_CONNECTED, 0.1);
            SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            // Temporal defaults
            SetParameterByKey(KEY.TM_VERBOSITY, 0);
            SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
            SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
            SetParameterByKey(KEY.SEED_TM, 1960);
            SetParameterByKey(KEY.RANDOM_TM, new XorshiftRandom((int)paramMap[KEY.SEED_TM]));
            SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
            // Maximum number of synapses per segment
            //  > 0 for fixed-size CLA
            // -1 for non-fixed-size CLA
            SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
            // Maximum number of segments per cell
            //  > 0 for fixed-size CLA
            // -1 for non-fixed-size CLA
            SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, 128);
            SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.21);
            SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
            SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
            //SetParameterByKey(KEY.GLOBAL_DECAY, 0);
            //SetParameterByKey(KEY.MAX_AGE, 0);
            SetParameterByKey(KEY.MIN_THRESHOLD, 12);
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 16);
            //SetParameterByKey(KEY.PAM_LENGTH, 1);

            // Classifier params
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.001);
            SetParameterByKey(KEY.AUTO_CLASSIFY, true);
            SetParameterByKey(KEY.AUTO_CLASSIFY_TYPE, typeof(SDRClassifier));
            SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1, 5 });

            SetParameterByKey(KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
        }

        private void SetProperties()
        {
            // Intermediate variables used to compute fields in modelParams and also
            // referenced from the control section.
            AggregationInfo = new AggregationSettings
            {
                days = 0,
                fields = new Map<string, object>(),
                hours = 0,
                microseconds = 0,
                milliseconds = 0,
                minutes = 0,
                months = 0,
                seconds = 0,
                weeks = 0,
                years = 0
            };

            PredictAheadTime = null;

            EnableSpatialPooler = true;
            EnableClassification = true;
            EnableTemporalMemory = true;

            InferenceType = InferenceType.TemporalMultiStep;

            Control.InputRecordSchema = new[]
            {
                new FieldMetaInfo("value", FieldMetaType.Float, SensorFlags.Blank)
            };
            Control.InferenceArgs = new InferenceArgsDescription
            {
                predictedField = "value",
                predictionSteps = new[] { 1, 5 }
            };
            Control.DatasetSpec = new StreamDef
            {
                info = "sawtooth test",
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] {"value"},
                        info = "sawtooth",
                        source = "sawtooth.csv"
                    }
                }
            };
            Control.IterationCount = 20;
            Control.Metrics = new[]
            {
                new MetricSpec(field: "value", metric:"multiStep", inferenceElement: InferenceElement.MultiStepBestPredictions, @params:new Map<string, object> { {"window", 10}, {"steps", 1}, {"errorMetric", "aae"} }),
                new MetricSpec(field: "value", metric:"multiStep", inferenceElement: InferenceElement.MultiStepBestPredictions, @params:new Map<string, object> { {"window", 10}, {"steps", 1}, {"errorMetric", "aae"}  }),
            };
            Control.LoggedMetrics = new[] { ".*nupicScore.*" };

            #region Encoder setup

            SetParameterByKey(KEY.FIELD_ENCODING_MAP, new EncoderSettingsList
            {
                {
                    "value", new EncoderSetting
                    {
                        clipInput = true,
                        fieldName = "value",
                        n = 100,
                        name = "value",
                        type = "ScalarSpaceEncoder",
                        w = 21
                    }
                },
                {
                    "_classifierInput", new EncoderSetting
                    {
                        name = "_classifierInput",
                        fieldName = "value",
                        classifierOnly = true,
                        type = "ScalarSpaceEncoder",
                        n = 100,
                        w = 21
                    }
                }
            });

            #endregion
        }
    }

    [Serializable]
    public class DeltaPermutationParameters : ExperimentPermutationParameters
    {
        public DeltaPermutationParameters()
        {
            PredictedField = "value";

            Encoders=new Map<string, object>
            {
                { "value", new PermuteEncoder("value", "ScalarSpaceEncoder", null, new KWArgsModel { {"space", new PermuteChoices(new [] {"delta", "absolute"})},{"clipInput", true},{"w", 21},{"n", new PermuteInt(28,521)} }) },
                { "_classifierInput", new PermuteEncoder("value", "ScalarSpaceEncoder", null, new KWArgsModel { {"space", new PermuteChoices(new [] {"delta", "absolute"})},{"clipInput", true}, { "classifierOnly", true }, {"w", 21},{"n", new PermuteInt(28,521)} }) },
            };

            SetParameterByKey(KEY.MIN_THRESHOLD, new PermuteInt(9,12));
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, new PermuteInt(12,16));
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, new PermuteFloat(0.000100,0.100000));

            Report = new[] {".*value.*"};
            Minimize = "multiStepBestPredictions:multiStep:errorMetric='aae':steps=1:window=10:field=value";
            MinParticlesPerSwarm = null;

        }
    }
}