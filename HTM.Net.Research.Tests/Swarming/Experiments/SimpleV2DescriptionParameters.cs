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
using HTM.Net.Util;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Tests.Swarming.Experiments
{
    [Serializable]
    [JsonConverter(typeof(SimpleV2DescriptionParametersConverter))]
    public class SimpleV2DescriptionParameters : ExperimentParameters
    {
        public SimpleV2DescriptionParameters()
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
            SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 15);
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
            SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1 });

            SetParameterByKey(KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
        }

        private void SetProperties()
        {
            // Intermediate variables used to compute fields in modelParams and also
            // referenced from the control section.
            AggregationInfo = new AggregationSettings
            {
                days = 0,
                fields = new Map<string, object>
                    {
                        {"timestamp", "first"},
                        {"gym", "first"},
                        {"consumption", "mean"},
                        {"address", "first"}
                    },
                hours = 0,
                microseconds = 0,
                milliseconds = 0,
                minutes = 0,
                months = 0,
                seconds = 0,
                weeks = 0,
                years = 0
            };

            EnableSpatialPooler = true;
            EnableClassification = true;
            EnableTemporalMemory = true;

            InferenceType = InferenceType.TemporalNextStep;
            Control.InputRecordSchema = new[]
            {
                new FieldMetaInfo("gym", FieldMetaType.String, SensorFlags.Blank),
                new FieldMetaInfo("address", FieldMetaType.String, SensorFlags.Blank),
                new FieldMetaInfo("timestamp", FieldMetaType.DateTime, SensorFlags.Timestamp),
                new FieldMetaInfo("consumption", FieldMetaType.Float, SensorFlags.Blank),
            };
            Control.InferenceArgs = new InferenceArgsDescription
            {
                inputPredictedField = InputPredictedField.Auto,
                predictedField = "consumption",
                predictionSteps = new[] { 1 }
            };
            Control.DatasetSpec = new StreamDef
            {
                info = "test_NoProviders",
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] {"*"},
                        info = "test data",
                        source = "test_data.csv"
                    }
                }
            };
            Control.Metrics = new[]
            {
                new MetricSpec(field: "consumption", inferenceElement: InferenceElement.Prediction, metric: "rmse")
            };
            Control.LoggedMetrics = new[] {".*nupicScore.*"};

            #region Encoder setup

            SetParameterByKey(KEY.FIELD_ENCODING_MAP, new EncoderSettingsList
            {
                {
                    "address", new EncoderSetting
                    {
                        fieldName = "address",
                        n = 300,
                        name = "address",
                        type = EncoderTypes.SDRCategoryEncoder,
                        w = 21,
                        categoryList = new List<string>()
                    }
                },
                {
                    "consumption", new EncoderSetting
                    {
                        clipInput = true,
                        fieldName = "consumption",
                        maxVal = 200,
                        minVal = 0,
                        n = 1500,
                        name = "consumption",
                        type = EncoderTypes.ScalarEncoder,
                        w = 21
                    }
                },
                {
                    "gym", new EncoderSetting
                    {
                        fieldName = "gym",
                        n = 300,
                        name = "gym",
                        type = EncoderTypes.SDRCategoryEncoder,
                        w = 21,
                        categoryList = new List<string>()
                    }
                },
                //{
                //    "timestamp", new EncoderSetting
                //    {
                //        fieldName = "timestamp",
                //        type = "DateEncoder",
                //        dayOfWeek = new Tuple(7, 3),
                //        timeOfDay = new Tuple(7, 8),
                //    }
                //},
                {
                    "timestamp_dayOfWeek", new EncoderSetting
                    {
                        DayOfWeek = new DayOfWeekTuple(7, 3),
                        fieldName = "timestamp",
                        name = "timestamp_dayOfWeek",
                        type = EncoderTypes.DateEncoder
                    }
                },
                {
                    "timestamp_timeOfDay", new EncoderSetting
                    {
                        fieldName = "timestamp",
                        name = "timestamp_timeOfDay",
                        TimeOfDay = new TimeOfDayTuple(7, 8),
                        type = EncoderTypes.DateEncoder
                    }
                }
            }.AsMap());

            #endregion
        }
    }

    public class SimpleV2DescriptionParametersConverter : BaseObjectConverter<SimpleV2DescriptionParameters>
    {
    }
}