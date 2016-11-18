using System.Collections.Generic;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
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
                predictionSteps = new[] { 0 }
            }
        };

        protected int __recordsToProcess = -1;
    }

    [TestClass]
    public class OpfBenchMarkTest : OPFBenchmarkBase
    {
        /// <summary>
        /// Try running a basic experiment and permutations
        /// </summary>
        [TestMethod]
        [DeploymentItem("Resources\\swarming\\sine.csv")]
        public void RunSineOpf()
        {
            var config = BenchMarkSine();

            PermutationsRunner.RunWithConfig(config, null);
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

            SwarmDefinition expDesc = (SwarmDefinition)EXP_COMMON.Clone();
            expDesc.inferenceArgs.predictedField = "Sine";
            expDesc.streamDef = streamDef;
            expDesc.includedFields = new List<SwarmDefinition.SwarmDefIncludedField>
            {
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName= "Sine",
                    fieldType= "float",
                    minValue= -1.0,
                    maxValue= 1.0,
                },
                new SwarmDefinition.SwarmDefIncludedField
                {
                    fieldName= "angle",
                    fieldType= "float",
                    minValue=0.0,
                    maxValue=25.0,
                }
            };
            expDesc.iterationCount = __recordsToProcess;

            return expDesc;
        }
    }


}