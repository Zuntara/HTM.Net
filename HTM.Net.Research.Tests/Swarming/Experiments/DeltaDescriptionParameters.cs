﻿using System;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Research.Tests.Examples.Random;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming.Experiments
{
    [Serializable]
    public class RandomDescriptionParameters : ExperimentParameters
    {
        public RandomDescriptionParameters()
        {
            InitializeParameters();
        }

        private void InitializeParameters()
        {
            // Properties
            SetProperties();

            // Spatial defaults
            SetParameterByKey(KEY.INPUT_DIMENSIONS, new int[] { 128 });
            SetParameterByKey(KEY.COLUMN_DIMENSIONS, new int[] { 1000/*, 20*/ }); // 300,20
            SetParameterByKey(KEY.CELLS_PER_COLUMN, 3);

            // Classifier Specific
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.0057);
            SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1/*, 2, 3, 4, 5,6,7,8,9,10*/ });

            // SpatialPooler specific
            SetParameterByKey(KEY.POTENTIAL_RADIUS, 13);//3
            SetParameterByKey(KEY.POTENTIAL_PCT, 0.81);//0.5
            SetParameterByKey(KEY.GLOBAL_INHIBITION, true);
            SetParameterByKey(KEY.LOCAL_AREA_DENSITY, -1.0);
            SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 13.0);
            SetParameterByKey(KEY.STIMULUS_THRESHOLD, 1.0);
            SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.0007);// 0.015
            SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.00015);  // 0.155
            SetParameterByKey(KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            SetParameterByKey(KEY.SYN_PERM_CONNECTED, 0.1);
            SetParameterByKey(KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.1);
            SetParameterByKey(KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.1);
            SetParameterByKey(KEY.DUTY_CYCLE_PERIOD, 9);
            SetParameterByKey(KEY.MAX_BOOST, 10.0);
            SetParameterByKey(KEY.SEED, 42);
            SetParameterByKey(KEY.RANDOM, new XorshiftRandom(42));
            SetParameterByKey(KEY.SP_VERBOSITY, 0);

            //Temporal Memory specific
            SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.2);
            SetParameterByKey(KEY.CONNECTED_PERMANENCE, 0.21);
            SetParameterByKey(KEY.MIN_THRESHOLD, 11);
            SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 6);
            SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
            SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 19);
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

            //Control.InputRecordSchema = new[]
            //{
            //    new FieldMetaInfo("value", FieldMetaType.Float, SensorFlags.Blank)
            //};
            //Control.InferenceArgs = new InferenceArgsDescription
            //{
            //    predictedField = "value",
            //    predictionSteps = new[] { 1, 5 }
            //};

            //Control.IterationCount = 20;
            //Control.Metrics = new[]
            //{
            //    new MetricSpec(field: "value", metric:"multiStep", inferenceElement: InferenceElement.MultiStepBestPredictions, @params:new Map<string, object> { {"window", 10}, {"steps", 1}, {"errorMetric", "aae"} }),
            //    new MetricSpec(field: "value", metric:"multiStep", inferenceElement: InferenceElement.MultiStepBestPredictions, @params:new Map<string, object> { {"window", 10}, {"steps", 5}, {"errorMetric", "aae"}  }),
            //};
            //Control.LoggedMetrics = new[] { ".*nupicScore.*" };

            #region Encoder setup

            SetParameterByKey(KEY.FIELD_ENCODING_MAP, NetworkDemoHarness.GetRandomDataFieldEncodingMap());

            #endregion
        }
    }

    public class RandomPermutationParameters : ExperimentPermutationParameters
    {
        public RandomPermutationParameters()
        {
            //PredictedField = "value";

            Encoders = new Map<string, object>
            {
                {
                    "Number 1", new PermuteEncoder("Number 1", "ScalarEncoder", null,
                        new KWArgsModel
                        {
                            {"w", 21},
                            {"n", new PermuteInt(28, 621)}
                        })
                }
            };

            SetParameterByKey(KEY.MIN_THRESHOLD, new RangeVariable(9, 19, 1));
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, new RangeVariable(11, 30, 1));
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, new RangeVariable(0.000100, 0.500000, 0.001));
            SetParameterByKey(KEY.DUTY_CYCLE_PERIOD, new RangeVariable(5, 20, 1));
            SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, new RangeVariable(3.0, 60.0, 1.0));

            //Report = new[] { ".*value.*" };
            //Minimize = "multiStepBestPredictions:multiStep:errorMetric=\"aae\":steps=1:window=10:field=value";
            //MinParticlesPerSwarm = null;

        }
    }

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
            SetParameterByKey(KEY.SEED, 1956);
            SetParameterByKey(KEY.RANDOM, new XorshiftRandom((int)1956));
            SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.1);
            SetParameterByKey(KEY.SYN_PERM_CONNECTED, 0.1);
            SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            // Temporal defaults
            SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
            SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
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
                new MetricSpec(field: "value", metric:"multiStep", inferenceElement: InferenceElement.MultiStepBestPredictions, @params:new Map<string, object> { {"window", 10}, {"steps", 5}, {"errorMetric", "aae"}  }),
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
                        type = EncoderTypes.ScalarSpaceEncoder,
                        w = 21
                    }
                },
                {
                    "_classifierInput", new EncoderSetting
                    {
                        name = "_classifierInput",
                        fieldName = "value",
                        classifierOnly = true,
                        type = EncoderTypes.ScalarSpaceEncoder,
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

            Encoders = new Map<string, object>
            {
                { "value", new PermuteEncoder("value", EncoderTypes.ScalarSpaceEncoder.ToString(), null, new KWArgsModel { {"space", new PermuteChoices(new [] {"delta", "absolute"})},{"clipInput", true},{"w", 21},{"n", new PermuteInt(28,521)} }) },
                { "_classifierInput", new PermuteEncoder("value", EncoderTypes.ScalarSpaceEncoder.ToString(), null, new KWArgsModel { {"space", new PermuteChoices(new [] {"delta", "absolute"})},{"clipInput", true}, { "classifierOnly", true }, {"w", 21},{"n", new PermuteInt(28,521)} }) },
            };

            SetParameterByKey(KEY.MIN_THRESHOLD, new PermuteInt(9, 12));
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, new PermuteInt(12, 16));
            SetParameterByKey(KEY.CLASSIFIER_ALPHA, new PermuteFloat(0.000100, 0.100000));

            Report = new[] { ".*value.*" };
            Minimize = "multiStepBestPredictions:multiStep:errorMetric=\"aae\":steps=1:window=10:field=value";
            MinParticlesPerSwarm = null;

        }
    }
}