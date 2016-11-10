using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Opf
{
    [TestClass]
    public class MetricManagerTests
    {
        [TestMethod]
        public void TestMetricsMgr()
        {
            Debug.WriteLine("*Testing metrics managers*...");

            var onlineMetrics = new[] { new MetricSpec("aae", null, "consumption", new Map<string, object>()) };

            Debug.WriteLine("TESTING METRICS MANAGER (BASIC PLUMBING TEST)...");

            var modelFieldMetaInfo = new List<FieldMetaInfo>
            {
                new FieldMetaInfo("temperature", FieldMetaType.Float, SensorFlags.Blank),
                new FieldMetaInfo("consumption", FieldMetaType.Float, SensorFlags.Blank),
            };

            // Test to make sure that invalid InferenceElements are caught
            try
            {
                new PredictionMetricsManager(onlineMetrics, modelFieldMetaInfo, InferenceType.TemporalNextStep);
                Assert.Fail("Should throw an error for invalid inference element");
            }
            catch (Exception e)
            {
                Assert.IsNotInstanceOfType(e, typeof(AssertFailedException));
                Debug.WriteLine($"Caught bad inference element: PASS -> {e.Message}");
            }

            Debug.WriteLine("");

            onlineMetrics = new[] { new MetricSpec("aae", InferenceElement.Prediction, "consumption", new Map<string, object>()) };

            var temporalMetrics = new PredictionMetricsManager(onlineMetrics, modelFieldMetaInfo, InferenceType.TemporalNextStep);

            var inputs = new[]
            {
                new Map<string, object>
                {
                    {
                        "groundTruthRow", new[] {9, 7}
                    },
                    {
                        "predictionsDict", new Map<InferenceType, object>
                        {
                            {InferenceType.TemporalNextStep, new[] {12, 17}}
                        }
                    },
                },
                new Map<string, object>
                {
                    {
                        "groundTruthRow", new[] {12,17}
                    },
                    {
                        "predictionsDict", new Map<InferenceType, object>
                        {
                            {InferenceType.TemporalNextStep, new[] {14,19}}
                        }
                    },
                },
                new Map<string, object>
                {
                    {
                        "groundTruthRow", new[] {14,20}
                    },
                    {
                        "predictionsDict", new Map<InferenceType, object>
                        {
                            {InferenceType.TemporalNextStep, new[] {16,21}}
                        }
                    },
                },
                new Map<string, object>
                {
                    {
                        "groundTruthRow", new[] {9, 7}
                    },
                    {
                        "predictionsDict", new Map<InferenceType, object>
                        {
                            {InferenceType.TemporalNextStep, null}
                        }
                    },
                },
            };

            foreach (var element in inputs)
            {
                var groundTruthRow = element["groundTruthRow"];
                var tPredictionRow = ((Map<InferenceType, object>)element["predictionsDict"])[InferenceType.TemporalNextStep];

                var result = new ModelResult(
                    sensorInput:
                        new SensorInput(dataRow: groundTruthRow, dataEncodings: null, sequenceReset: false, category: null),
                    inferences: new Map<InferenceElement, object> { { InferenceElement.Prediction, tPredictionRow } });

                temporalMetrics.update(result);
            }

            Assert.AreEqual(15.0 / 3.0, temporalMetrics.GetMetrics().Values.ElementAt(0));
        }

        /// <summary>
        /// Test to see if the metrics manager correctly shifts records for multistep prediction cases
        /// </summary>
        [TestMethod]
        public void TestTemporalShift()
        {
            Debug.WriteLine("*Testing Multistep temporal shift*...");

            var onlineMetrics = new MetricSpec[] { };

            var modelFieldMetaInfo = new List<FieldMetaInfo>
            {
                new FieldMetaInfo("consumption", FieldMetaType.Float, SensorFlags.Blank),
            };

            var mgr = new PredictionMetricsManager(onlineMetrics, modelFieldMetaInfo, InferenceType.TemporalMultiStep);

            var groundTruths = new List<KeyValuePair<string, object>>();
            for (int i = 0; i < 10; i++) groundTruths.Add(new KeyValuePair<string, object>("consumption", i));

            var oneStepInfs = ArrayUtils.Range(0, 10).Reverse().ToArray();
            var threeStepInfs = ArrayUtils.Range(5, 15);

            foreach (var item in ArrayUtils.Zip(ArrayUtils.XRange(0, 10, 1), groundTruths, oneStepInfs, threeStepInfs))
            {
                var iterNum = (int)item.Get(0);
                var gt = (KeyValuePair<string, object>)item.Get(1);
                var os = (int)item.Get(2);
                var ts = (int)item.Get(3);

                var inferences = new Map<InferenceElement, object>
                {
                    {InferenceElement.MultiStepPredictions, new Map<object, object> {{1, os}, {3, ts}}}
                };
                var sensorInput = new SensorInput(dataDict: new Map<string, object> { gt });
                var result = new ModelResult(sensorInput: sensorInput, inferences: inferences);
                mgr.update(result);

                Assert.AreEqual(gt, ((Map<string, object>)mgr._getGroundTruth(InferenceElement.MultiStepPredictions)).ElementAt(0));
                if (iterNum < 1)
                {
                    Assert.IsNull(((Map<object, object>)mgr._getInference(InferenceElement.MultiStepPredictions))[1]);
                }
                else
                {
                    var prediction = ((Map<object, object>)mgr._getInference(InferenceElement.MultiStepPredictions))[1];
                    Assert.AreEqual(10 - iterNum, prediction);
                }

                if (iterNum < 3)
                {
                    var inference = mgr._getInference(InferenceElement.MultiStepPredictions);
                    Assert.IsTrue(inference == null || ((Map<object, object>)inference)[3] == null);
                }
                else
                {
                    var prediction = ((Map<object, object>)mgr._getInference(InferenceElement.MultiStepPredictions))[3];
                    Assert.AreEqual(iterNum + 2, prediction);
                }
            }
        }

        [TestMethod]
        public void TestMetricLabels()
        {
            var testTuples = new[]
            {
                new Tuple<MetricSpec, string>(
                    new MetricSpec("rmse", InferenceElement.Prediction, "consumption"), 
                    "prediction:rmse:field=consumption"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("rmse", InferenceElement.Classification),
                    "classification:rmse"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("rmse", InferenceElement.Encodings, "pounds", new Map<string, object> { {"window", 100} }),
                    "encodings:rmse:window=100:field=pounds"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("aae", InferenceElement.Prediction, "pounds", new Map<string, object> { {"window", 100}, {"paramA",10.2}, {"paramB", 20} }),
                    "prediction:aae:paramA=10.2:paramB=20:window=100:field=pounds"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("aae", InferenceElement.Prediction, "pounds", new Map<string, object> { {"window", 100}, {"paramA",10.2}, {"1paramB", 20} }),
                    "prediction:aae:1paramB=20:paramA=10.2:window=100:field=pounds"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("aae", InferenceElement.Prediction, "pounds", new Map<string, object> { {"window", 100}, {"paramA",10.2}, {"paramB", -20} }),
                    "prediction:aae:paramA=10.2:paramB=-20:window=100:field=pounds"),
                new Tuple<MetricSpec, string>(
                    new MetricSpec("aae", InferenceElement.Prediction, "pounds", new Map<string, object> { {"window", 100}, {"paramA",10.2}, {"paramB", "square"} }),
                    "prediction:aae:paramA=10.2:paramB=\"square\":window=100:field=pounds"),
            };

            foreach (Tuple<MetricSpec, string> test in testTuples)
            {
                Debug.WriteLine($"> Testing {test.Item2}");
                Assert.AreEqual(test.Item2, test.Item1.getLabel());
            }
        }
    }
}