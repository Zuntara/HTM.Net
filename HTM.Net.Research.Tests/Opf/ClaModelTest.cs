using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Tests.Swarming;
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
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>(), 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsSingleValues()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.1 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.001, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsLikelihoodThresholds()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.1 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.001 }, { 2, 0.002 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(2, first.Key);
            Assert.AreEqual(0.002, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.002 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.002, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsMaxPredictions()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
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

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
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
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
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

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
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

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
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
            IDescription modelConfig = new TemporalAnomalyModelDescription();

            var data = new List<Map<string, object>>
            {
                new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 5, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 5, 0, 0, 0)},
                    {"c1", 5.0}
                },
                new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 6, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 6, 0, 0, 0)},
                    {"c1", 6.0}
                },
                new Map<string, object>
                {
                    {"_category", new object[] {null}},
                    {"_reset", 0},
                    {"_sequenceId", 0},
                    {"_timestamp", new DateTime(2013, 12, 7, 0, 0, 0)},
                    {"_timestampRecordIdx", null},
                    {"c0", new DateTime(2013, 12, 7, 0, 0, 0)},
                    {"c1", 7.0}
                },
            };

            var model = ModelFactory.Create(modelConfig);
            model.enableLearning();
            model.enableInference(modelConfig.inferenceArgs);

            foreach (Map<string, object> row in data)
            {
                var result = model.run(row);
                Assert.IsInstanceOfType(result, typeof(ModelResult));

            }
        }

        class TemporalAnomalyModelDescription : DescriptionBase
        {
            public TemporalAnomalyModelDescription()
            {
                var config = new DescriptionConfigModel()
                {
                    model = "CLA",
                    aggregationInfo = new AggregationDict
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
                    },
                    modelParams = new ModelDescriptionParamsDescrModel
                    {
                        anomalyParams = new AnomalyParamsDescr
                        {
                            anomalyCacheRecords = null,
                            autoDetectThreshold = null,
                            autoDetectWaitRecords = 5030
                        },
                        clEnable = false,
                        clParams = new ClassifierParamsDescr
                        {
                            alpha = 0.035828933612158,
                            clVerbosity = 0,
                            regionName = "CLAClassifierRegion",
                            steps = 1
                        },
                        inferenceType = InferenceType.TemporalAnomaly,
                        sensorParams = new SensorParamsDescrModel
                        {
                            encoders = new Map<string, Map<string, object>>
                            {
                                {
                                    "c0_dayOfWeek", null
                                },
                                {
                                    "c0_timeOfDay", new Map<string, object>
                                    {
                                        {"fieldname", "c0"},
                                        {"name", "c0"},
                                        {"timeOfDay", new Tuple(21,9.49122334747737)},
                                        {"type", "DateEncoder"}
                                    }
                                },
                                {
                                    "c0_weekend", null
                                },
                                {
                                    "c1", new Map<string, object>
                                    {
                                        {"fieldname", "c1"},
                                        {"name", "c1"},
                                        {"resolution",  0.8771929824561403},
                                        //{"seed",  42},
                                        {"type", "RandomDistributedScalarEncoder"}
                                    }
                                },
                            },
                            sensorAutoReset = null,
                            verbosity = 0
                        },
                        spEnable = true,
                        spParams = new SpatialParamsDescr
                        {
                            potentialPct = 0.8,
                            columnCount = new[] { 2048 },
                            globalInhibition = true,
                            inputWidth = new[] { 0 },
                            maxBoost = 1.0,
                            numActiveColumnsPerInhArea = 40,
                            seed = 1956,
                            spVerbosity = 0,
                            synPermActiveInc = 0.0015,
                            synPermConnected = 0.1,
                            synPermInactiveDec = 0.0005,
                        },
                        tpEnable = true,
                        tpParams = new TemporalParamsDescr
                        {
                            activationThreshold = 13,
                            cellsPerColumn = 32,
                            columnCount = new[] { 2048 },
                            globalDecay = 0.0,
                            initialPerm = 0.21,
                            inputWidth = new[] { 2048 },
                            maxAge = 0,
                            maxSegmentsPerCell = 128,
                            maxSynapsesPerSegment = 32,
                            minThreshold = 10,
                            newSynapseCount = 20,
                            outputType = "normal",
                            pamLength = 3,
                            permanenceDec = 0.1,
                            permanenceInc = 0.1,
                            seed = 1960,
                            verbosity = 0
                        },
                        trainSPNetOnlyIfRequested = false
                    },
                    predictAheadTime = null,
                    version = 1
                };

                modelConfig = config;
                inferenceArgs = new Map<string, object>
                {
                    {"inputPredictedField", "auto" },
                    {"predictedField", "c1" },
                    {"predictionSteps", new[] {1} },
                };
                inputRecordSchema = new Map<string, Tuple<FieldMetaType, SensorFlags>>
                {
                    {"c0", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.DateTime, SensorFlags.Timestamp) },
                    {"c1", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.Float, SensorFlags.Blank) },
                };
            }

            #region Overrides of DescriptionBase

            public override IDescription Clone()
            {
                throw new System.NotImplementedException();
            }

            public override Network.Network BuildNetwork()
            {
                throw new System.NotImplementedException();
            }

            public override Parameters GetParameters()
            {
                Parameters p = Parameters.GetAllDefaultParameters();

                // Spatial pooling parameters
                SpatialParamsDescr spParams = this.modelConfig.modelParams.spParams;
                TemporalParamsDescr tpParams = this.modelConfig.modelParams.tpParams;

                Parameters.ApplyParametersFromDescription(spParams, p);
                Parameters.ApplyParametersFromDescription(tpParams, p);

                return p;
            }

            #endregion
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
            Assert.IsTrue(minValue.Length > 0);
            Debug.WriteLine(minValue);

            var maxValue = inputSource.GetFieldMax("consumption");
            Assert.IsNotNull(maxValue);
            Assert.IsTrue(maxValue.Length > 0);
            Debug.WriteLine(maxValue);
        }
    }
}