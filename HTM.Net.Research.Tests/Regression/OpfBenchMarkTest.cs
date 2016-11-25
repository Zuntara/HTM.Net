using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Tests.Examples.Sine;
using HTM.Net.Util;
using log4net.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Regression
{
    public class OPFBenchmarkBase
    {
        protected static SwarmDefinition EXP_COMMON = new SwarmDefinition
        {
            inferenceType = InferenceType.MultiStep,
            inferenceArgs = new InferenceArgsDescription
            {
                predictedField = null,
                predictionSteps = new[] { 1 }
            }
        };

        protected int __recordsToProcess = -1;
    }

    [TestClass]
    public class OpfBenchMarkTest : OPFBenchmarkBase
    {
        [TestInitialize]
        public void Initialize()
        {
            // Initialize log4net.
            //XmlConfigurator.Configure();
        }

        /// <summary>
        /// Try running a basic experiment and permutations
        /// </summary>
        [TestMethod]
        [DeploymentItem("Resources\\swarming\\sine.csv")]
        public void RunSineOpf()
        {
            var config = BenchMarkSine(__recordsToProcess);
            config.maxModels = 1;

            uint pr = PermutationsRunner.RunWithConfig(config, null);
            var resultsDict = getResultsFromJobDB(pr);

            Assert.IsNotNull(resultsDict, "no results found!");

            Debug.WriteLine("RESULTS:");
            Debug.WriteLine("");

            foreach (var pair in resultsDict)
            {
                Debug.WriteLine($"Key     : {pair.Key}");
                if (pair.Value is IDictionary)
                {
                    Debug.WriteLine($"  Value-k : {Arrays.ToString(((IDictionary)pair.Value).Keys)}");
                    Debug.WriteLine($"  Value-v : {Arrays.ToString(((IDictionary)pair.Value).Values)}");
                }
                else
                {
                    Debug.WriteLine($"  Value : {pair.Value}");
                }
            }

            Debug.WriteLine(Json.Serialize(resultsDict));
        }

        private List<PredictionValue> _predictions = new List<PredictionValue>();
        private double _predictedValue = 0.0;

        private void RecordStep(IInference infer, string classifierField)
        {
            double newPrediction;
            if (null != infer.GetClassification(classifierField).GetMostProbableValue(1))
            {
                newPrediction = (double)infer.GetClassification(classifierField).GetMostProbableValue(1);
            }
            else
            {
                newPrediction = _predictedValue;
            }
            if (infer.GetRecordNum() > 0)
            {
                double actual = (double)((NamedTuple)infer.GetClassifierInput()[classifierField]).Get("inputValue");
                double error = Math.Abs(_predictedValue - actual);

                PredictionValue value = new PredictionValue();
                value.RecordNum = infer.GetRecordNum();
                value.ActualValue = actual;
                value.PredictionError = error;
                value.PredictedValue = newPrediction;
                value.AnomalyFactor = infer.GetAnomalyScore();
                _predictions.Add(value);
            }
            _predictedValue = newPrediction;
        }

        public double GetTotalAccurancy(double rangePct, bool fromBehind)
        {
            int totalLength = _predictions.Count;
            int takeRange = (int)(totalLength * rangePct);
            if (fromBehind)
            {
                int offset = totalLength - takeRange;
                double totalActual = _predictions.Skip(offset).Sum(p => p.ActualValue);
                double totalPredicted = _predictions.Skip(offset).Sum(p => p.PredictedValue);
                double totalError = _predictions.Skip(offset).Sum(p => Math.Abs(p.PredictionError));

                return totalPredicted / totalActual;
                //return (1.0 - (totalError / takeRange)) * 100.0;
            }
            else
            {
                double totalActual = _predictions.Take(takeRange).Sum(p => p.ActualValue);
                double totalPredicted = _predictions.Take(takeRange).Sum(p => p.PredictedValue);
                double totalError = _predictions.Take(takeRange).Sum(p => Math.Abs(p.PredictionError));

                //return (1.0 - (totalError/ takeRange))*100.0;

                return totalPredicted / totalActual;
            }
        }

        [TestMethod]
        [DeploymentItem("Resources\\swarming\\sine.csv")]
        public void RunSineInManualSetupToMatchPerformance()
        {
            _predictions = new List<PredictionValue>();

            // Get config first
            var config = BenchMarkSine(__recordsToProcess);
            config.maxModels = 1;

            // Convert config to parameters
            // set encoders in place
            var description = new ExpGenerator(config).Generate().Item1;

            Parameters p = description.GetParameters();

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Network.Network network = new Network.Network("SineNetwork", p)
                .Add(Network.Network.CreateRegion("TopRegion")
                    .Add(Network.Network.CreateLayer("TP", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier))
                        .Add(new TemporalMemory())
                        .Add(Anomaly.Create(p)))
                    .Add(Network.Network.CreateLayer("SP", p)
                        .Add(new SpatialPooler()))
                    .Add(Network.Network.CreateLayer("Sensor", p)
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(SensorParams.Keys.Path, "", "sine.csv"))))
                .Connect("TP", "SP")
                .Connect("SP", "Sensor"));

            network.Observe().Subscribe(
                i =>
                {
                    RecordStep(i, "Sine");
                    Console.Write(".");
                },
                e =>
                {
                    Console.WriteLine(e);
                },
                () =>
                {
                    Console.WriteLine(" Done");
                });

            network.Start();

            network.GetHead().GetHead().GetLayerThread().Wait(); // wait for it to finish

            Console.WriteLine("Total accurancy: {0}", GetTotalAccurancy(1.0, false));
            Console.WriteLine("Total accurancy from last 30%: {0}", GetTotalAccurancy(0.3, true));
        }

        [TestMethod]
        [DeploymentItem("Resources\\swarming\\sine.csv")]
        public void RunSineInManualSetupToMatchPerformanceOneLayer()
        {
            var metric = MetricSpec.GetModule(new MetricSpec("rmse", InferenceElement.Prediction, "Sine",
                new Map<string, object>()));
            
            // Get config first
            var config = BenchMarkSine(__recordsToProcess);
            config.maxModels = 1;

            // Convert config to parameters
            // set encoders in place
            var description = new ExpGenerator(config).Generate().Item1;

            Parameters p = description.GetParameters();

            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            p.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, 0.5);

            Network.Network network = new Network.Network("SineNetwork", p)
                .Add(Network.Network.CreateRegion("TopRegion")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier))
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                            SensorParams.Create(SensorParams.Keys.Path, "", "sine.csv"))))
                );

            double? errorScore = null;
            network.Observe().Subscribe(
                i =>
                {
                    //RecordStep(i, "Sine");
                    errorScore = metric.addInstance((double) ((NamedTuple) i.GetClassifierInput()["Sine"]).Get("inputValue"),
                        (double)i.GetClassification("Sine").GetMostProbableValue(1));
                    //Console.Write(".");
                },
                e =>
                {
                    Console.WriteLine(e);
                },
                () =>
                {
                    Console.WriteLine(" Done");
                });

            network.Start();

            network.GetHead().GetHead().GetLayerThread().Wait(); // wait for it to finish

            Console.WriteLine("Total accurancy: {0}", errorScore);
            Console.WriteLine("Total accurancy from last 30%: {0}", GetTotalAccurancy(0.3, true));
        }

        private Dictionary<string, object> getResultsFromJobDB(uint jobId)
        {
            var jobsDb = BaseClientJobDao.Create();
            var jobInfo = jobsDb.jobInfo(jobId);
            var res = jobInfo["results"] as string;
            var results = Json.Deserialize<Dictionary<string, object>>(res);
            var bestModel = results["bestModel"];

            return results;
        }


        [TestMethod]
        public void TestParameterGenerationFromDescriptionViaSwarmDefinition()
        {
            SwarmDefinition def = EXP_COMMON.Clone();

            def.streamDef = new StreamDef
            {
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] {"Sine", "angle"},
                        info = "sine.csv",
                        source = "sine.csv"
                    }
                }
            };
            def.includedFields = new List<SwarmDefinition.SwarmDefIncludedField>
            {
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName = "Sine",
                    fieldType = FieldMetaType.Float,
                    minValue = -1.0,
                    maxValue = 1.0,
                },
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName = "angle",
                    fieldType = FieldMetaType.Float,
                    minValue = 0.0,
                    maxValue = 25.0,
                }
            };
            def.inferenceArgs.predictedField = "Sine";

            var description = new ExpGenerator(def).Generate().Item1;

            Parameters p = description.GetParameters();

            Assert.IsNotNull(p);

            Console.WriteLine(p.ToString());

            Assert.IsNotNull(p.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP), "No encoders found in parameter definition");
            Map<string, Map<string, object>> encodings = (Map<string, Map<string, object>>)p.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP);
            Assert.AreEqual(3, encodings.Count, "Encoder count is incorrect");

        }

        public static SwarmDefinition BenchMarkSine(int recordsToProcess)
        {
            StreamDef streamDef = new StreamDef
            {
                info = "test_NoProviders",
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] { "angle", "Sine" },
                        info = "sine.csv",
                        source = "sine.csv",
                        last_record = new int?[] {3019*1, null}
                    }
                },
                version = 1
            };

            SwarmDefinition expDesc = EXP_COMMON.Clone();
            expDesc.inferenceArgs.predictedField = "Sine";
            expDesc.streamDef = streamDef;
            expDesc.includedFields = new List<SwarmDefinition.SwarmDefIncludedField>
            {
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName = "angle",
                    fieldType = FieldMetaType.Float,
                    minValue = 0.0,
                    maxValue = 25.0,
                },
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName = "Sine",
                    fieldType = FieldMetaType.Float,
                    minValue = -1.0,
                    maxValue = 1.0,
                }
            };
            expDesc.iterationCount = recordsToProcess;

            return expDesc;
        }

        public class PredictionValue
        {
            public int RecordNum { get; set; }
            public double ActualValue { get; set; }
            public double PredictedValue { get; set; }
            public double PredictionError { get; set; }
            public double AnomalyFactor { get; set; }
        }
    }


}