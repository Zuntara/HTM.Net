using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using log4net.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Regression
{
    public class OPFBenchmarkBase
    {
        protected SwarmDefinition EXP_COMMON = new SwarmDefinition
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
            XmlConfigurator.Configure();
        }

        /// <summary>
        /// Try running a basic experiment and permutations
        /// </summary>
        [TestMethod]
        [DeploymentItem("Resources\\swarming\\sine.csv")]
        public void RunSineOpf()
        {
            var config = BenchMarkSine();
            config.maxModels = 1;

            uint pr = PermutationsRunner.RunWithConfig(config, null);
            var resultsDict = getResultsFromJobDB(pr);

            Assert.IsNotNull(resultsDict, "no results found!");

            Debug.WriteLine("RESULTS:");
            Debug.WriteLine("");

            foreach (var pair in resultsDict)
            {
                Debug.WriteLine($"Key     : {pair.Key}");
                Debug.WriteLine($"  Value : {pair.Value}");
            }
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
            Map<string, Map<string, object>> encodings = (Map<string, Map<string, object>>) p.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP);
            Assert.AreEqual(3, encodings.Count, "Encoder count is incorrect");

        }

        public SwarmDefinition BenchMarkSine()
        {
            StreamDef streamDef = new StreamDef
            {
                info = "test_NoProviders",
                streams = new[]
                {
                    new StreamDef.StreamItem
                    {
                        columns = new[] {"Sine", "angle"},
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
            expDesc.iterationCount = __recordsToProcess;

            return expDesc;
        }
    }


}