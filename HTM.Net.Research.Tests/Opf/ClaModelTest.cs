using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Tests.Opf
{
    [TestClass]
    public class ClaModelTest
    {
        [TestMethod]
        public void TestRemoveUnlikelyPredictionsEmpty()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>(), 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsSingleValues()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double?> { { 1, 0.1 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?> { { 1, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.001, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsLikelihoodThresholds()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double?> { { 1, 0.1 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?> { { 1, 0.001 }, { 2, 0.002 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(2, first.Key);
            Assert.AreEqual(0.002, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?> { { 1, 0.002 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.002, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsMaxPredictions()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 0.01, 3 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            var item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.4 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(4, item.Key);
            Assert.AreEqual(0.4, item.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsComplex()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.004 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            var item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.4 }, { 5, 0.005 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(4, item.Key);
            Assert.AreEqual(0.4, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double?>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.004 }, { 5, 0.005 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
        }

        /// <summary>
        /// Simple test to assert that ModelFactory.create() with a given specific
        /// Temporal Anomaly configuration will return a model that can return
        /// inferences
        /// </summary>
        [TestMethod]
        public void TestTemporalAnomalyModelFactory()
        {
            ClaExperimentParameters exp = new TemporalAnomalyModelDescriptionParams();

            var data = new List<Tuple<Map<string, object>, string[]>>
            {
                new Tuple<Map<string, object>, string[]>(new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 5, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 5, 0, 0, 0)},
                    {"c1", 5.0}
                }, new[] {"0", "05/12/2013", "5.0"}),
                new Tuple<Map<string, object>, string[]>(new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 6, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 6, 0, 0, 0)},
                    {"c1", 6.0}
                }, new[] {"1", "05/12/2013", "6.0"}),
                new Tuple<Map<string, object>, string[]>(new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 7, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 7, 0, 0, 0)},
                    {"c1", 7.0}
                }, new[] {"2", "05/12/2013", "7.0"}),
            };

            var model = ModelFactory.Create(exp);
            model.enableLearning();
            model.enableInference(exp.Control.InferenceArgs);

            foreach (var row in data)
            {
                var result = model.run(row);
                Assert.IsInstanceOfType(result, typeof(ModelResult));

            }
        }

        //class TemporalAnomalyModelDescription : BaseDescription
        //{
        //    public TemporalAnomalyModelDescription()
        //    {
        //        var config = new ConfigModelDescription
        //        {
        //            model = "CLA",
        //            aggregationInfo = new AggregationSettings
        //            {
        //                days = 0,
        //                fields = new Map<string, object>(),
        //                hours = 0,
        //                microseconds = 0,
        //                milliseconds = 0,
        //                minutes = 0,
        //                months = 0,
        //                seconds = 0,
        //                weeks = 0,
        //                years = 0
        //            },
        //            modelParams = new ModelParamsDescription
        //            {
        //                anomalyParams = new AnomalyParamsDescription
        //                {
        //                    anomalyCacheRecords = null,
        //                    autoDetectThreshold = null,
        //                    autoDetectWaitRecords = 5030
        //                },
        //                clEnable = false,
        //                clParams = new ClassifierParamsDescription
        //                {
        //                    alpha = 0.035828933612158,
        //                    verbosity = 0,
        //                    regionName = typeof(CLAClassifier).AssemblyQualifiedName,
        //                    steps = new[] {1}
        //                },
        //                inferenceType = InferenceType.TemporalAnomaly,
        //                sensorParams = new SensorParamsDescription
        //                {
        //                    encoders = new EncoderSettingsList()
        //                    {
        //                        {
        //                            "c0_dayOfWeek", null
        //                        },
        //                        {
        //                            "c0_timeOfDay", new EncoderSetting
        //                            {
        //                                fieldName= "c0",
        //                                name= "c0",
        //                                timeOfDay= new Tuple(21, 9.49122334747737),
        //                                type= "DateEncoder"
        //                            }
        //                        },
        //                        {
        //                            "c0_weekend", null
        //                        },
        //                        {
        //                            "c1", new EncoderSetting
        //                            {
        //                                fieldName= "c1",
        //                                name= "c1",
        //                                resolution= 0.8771929824561403,
        //                                // seed=  42,
        //                                type= "RandomDistributedScalarEncoder"
        //                            }
        //                        },
        //                    },
        //                    sensorAutoReset = null,
        //                    verbosity = 0
        //                },
        //                spEnable = true,
        //                spParams = new SpatialParamsDescription
        //                {
        //                    potentialPct = 0.8,
        //                    columnCount = new[] {2048},
        //                    globalInhibition = true,
        //                    inputWidth = new[] {0},
        //                    maxBoost = 1.0,
        //                    numActiveColumnsPerInhArea = 40,
        //                    seed = 1956,
        //                    spVerbosity = 0,
        //                    synPermActiveInc = 0.0015,
        //                    synPermConnected = 0.1,
        //                    synPermInactiveDec = 0.0005,
        //                },
        //                tpEnable = true,
        //                tpParams = new TemporalParamsDescription
        //                {
        //                    activationThreshold = 13,
        //                    cellsPerColumn = 32,
        //                    columnCount = new[] {2048},
        //                    globalDecay = 0.0,
        //                    initialPerm = 0.21,
        //                    inputWidth = new[] {2048},
        //                    maxAge = 0,
        //                    maxSegmentsPerCell = 128,
        //                    maxSynapsesPerSegment = 32,
        //                    minThreshold = 10,
        //                    newSynapseCount = 20,
        //                    outputType = "normal",
        //                    pamLength = 3,
        //                    permanenceDec = 0.1,
        //                    permanenceInc = 0.1,
        //                    seed = 1960,
        //                    verbosity = 0
        //                },
        //                trainSPNetOnlyIfRequested = false
        //            },
        //            predictAheadTime = null,
        //            version = 1,
        //            inputRecordSchema = new[]
        //            {
        //                new FieldMetaInfo("c0", FieldMetaType.DateTime, SensorFlags.Timestamp),
        //                new FieldMetaInfo("c1", FieldMetaType.Float, SensorFlags.Blank)
        //            }
        //        };

        //        modelConfig = config;

        //        control = new ControlModelDescription
        //        {
        //            inferenceArgs = new InferenceArgsDescription
        //            {
        //                inputPredictedField = InputPredictedField.Auto,
        //                predictedField = "c1",
        //                predictionSteps = new[] {1},
        //            }
        //        };
        //    }

        //    #region Overrides of DescriptionBase

        //    public override Network.Network BuildNetwork()
        //    {
        //        throw new System.NotImplementedException();
        //    }

        //    public override Parameters GetParameters()
        //    {
        //        Parameters p = Parameters.Empty();
        //        p.Union(modelConfig.GetParameters());
        //        return p;
        //    }

        //    #endregion
        //}

        class TemporalAnomalyModelDescriptionParams : ClaExperimentParameters
        {
            public TemporalAnomalyModelDescriptionParams()
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
                SetParameterByKey(KEY.POTENTIAL_PCT, 0.8);
                SetParameterByKey(KEY.COLUMN_DIMENSIONS, new[] { 2048 });
                SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
                SetParameterByKey(KEY.SEED_SP, 1956);
                SetParameterByKey(KEY.RANDOM_SP, new XorshiftRandom((int)paramMap[KEY.SEED_SP]));
                SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.0015);
                SetParameterByKey(KEY.SYN_PERM_CONNECTED, 0.1);
                SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.0005);
                SetParameterByKey(KEY.MAX_BOOST, 1.0);
                // Temporal defaults
                SetParameterByKey(KEY.TM_VERBOSITY, 0);
                SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
                SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
                SetParameterByKey(KEY.SEED_TM, 1960);
                SetParameterByKey(KEY.RANDOM_TM, new XorshiftRandom((int)paramMap[KEY.SEED_TM]));
                SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
                SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
                SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, 128);
                SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.21);
                SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
                SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
                //SetParameterByKey(KEY.GLOBAL_DECAY, 0);
                //SetParameterByKey(KEY.MAX_AGE, 0);
                SetParameterByKey(KEY.MIN_THRESHOLD, 10);
                SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 13);
                //SetParameterByKey(KEY.PAM_LENGTH, 1);

                // Classifier params
                SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.035828933612158);
                SetParameterByKey(KEY.AUTO_CLASSIFY, false);
                SetParameterByKey(KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier));
                SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1 });

                SetParameterByKey(KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            }

            private void SetProperties()
            {
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

                EnableClassification = false;
                EnableSpatialPooler = true;
                EnableTemporalMemory = true;

                InferenceType = InferenceType.TemporalAnomaly;
                Control.InputRecordSchema = new[]
                {
                    new FieldMetaInfo("c0", FieldMetaType.DateTime, SensorFlags.Timestamp),
                    new FieldMetaInfo("c1", FieldMetaType.Float, SensorFlags.Blank)
                };
                Control.InferenceArgs = new InferenceArgsDescription
                {
                    inputPredictedField = InputPredictedField.Auto,
                    predictedField = "c1",
                    predictionSteps = new[] {1},
                };

                SetParameterByKey(KEY.FIELD_ENCODING_MAP, new EncoderSettingsList
                {
                    {
                        "c0_dayOfWeek", null
                    },
                    {
                        "c0_timeOfDay", new EncoderSetting
                        {
                            fieldName = "c0",
                            name = "c0",
                            timeOfDay = new Tuple(21, 9.49122334747737),
                            type = "DateEncoder"
                        }
                    },
                    {
                        "c0_weekend", null
                    },
                    {
                        "c1", new EncoderSetting
                        {
                            fieldName = "c1",
                            name = "c1",
                            resolution = 0.8771929824561403,
                            // seed=  42,
                            type = "RandomDistributedScalarEncoder"
                        }
                    },
                });
            }
        }

        [TestMethod]
        [DeploymentItem("Resources\\rec-center-hourly.csv")]
        public void TestFileHeader_BatchedCsvStream()
        {
            string fileName = "rec-center-hourly.csv";

            IStream<string> fileStream = new Stream<string>(YieldingFileReader.ReadAllLines(fileName, Encoding.UTF8).ToList());
            var inputSource = BatchedCsvStream<string>.Batch(fileStream, 20, false, 3);

            var fields = inputSource.GetHeader().GetFieldNames().ToList();

            Assert.IsNotNull(fields);
            Assert.IsTrue(fields.Any());

            var minValue = inputSource.GetFieldMin("consumption");
            Assert.IsNotNull(minValue);
            Debug.WriteLine(minValue);

            var maxValue = inputSource.GetFieldMax("consumption");
            Assert.IsNotNull(maxValue);
            Debug.WriteLine(maxValue);
        }
    }
}