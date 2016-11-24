using System;
using System.Collections.Generic;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming
{
    public class SpatialClassificationPermutationsFile : BasePermutations
    {
        public SpatialClassificationPermutationsFile()
        {
            predictedField = "consumption";

            permutations = new PermutationModelParameters
            {
                modelParams = new PermutationModelDescriptionParams
                {
                    sensorParams = new PermutationSensorParams
                    {
                        encoders = new Map<string, object>
                        {
                            {"gym",new PermuteEncoder(fieldName: "gym", encoderClass: "SDRCategoryEncoder",kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})},
                            {"timestamp_dayOfWeek",new PermuteEncoder(fieldName: "timestamp", encoderClass: "DateEncoder.dayOfWeek",kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 3.0})}, {"w", 7}})},
                            {"timestamp_timeOfDay",new PermuteEncoder(fieldName: "timestamp", encoderClass: "DateEncoder.timeOfDay",kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 8.0})}, {"w", 7}})},
                            {"_classifierInput", new Map<string,object>
                                {
                                    { "fieldName", "consumption"},
                                    { "encoderType", "ScalarEncoder"},
                                    { "kwArgs", new KWArgsModel
                                        {
                                            {"maxval", new PermuteInt(100, 300, 25)},
                                            { "n", new PermuteInt(13, 500, 20)},
                                            { "w", 7},
                                            { "minval", 0},
                                            { "classifierOnly", true}
                                        }
                                    }
                                }},
                            {"address",new PermuteEncoder(fieldName: "address", encoderClass: "SDRCategoryEncoder",kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})},
                        }
                    },
                    tpParams = new PermutationTemporalPoolerParams
                    {
                        
                    },
                    // TODO: clParams & spParams
                }
            };

            report = new[] { ".*consumption.*" };
            minimize = @"multiStepBestPredictions:multiStep:errorMetric=""avg_err"":steps=\[0\]:window=1000:field=consumption".ToLower();
            minParticlesPerSwarm = null;
        }

        #region Implementation of IPermutionFilter

        public override IDictionary<string, object> dummyModelParams(PermutationModelParameters perm)
        {
            double errScore = 50;

            //errScore += Math.Abs((int)perm.modelParams.sensorParams.encoders["consumption"]["maxval"] - 250);
            //errScore += Math.Abs((int)perm.modelParams.sensorParams.encoders["consumption"]["n"] - 53);

            if (perm.modelParams.sensorParams.encoders["address"] != null)
            {
                errScore -= 20;
            }
            if (perm.modelParams.sensorParams.encoders["gym"] != null)
            {
                errScore -= 10;
            }
            if (perm.modelParams.sensorParams.encoders["timestamp_dayOfWeek"] != null)
            {
                errScore += 30;
            }
            if (perm.modelParams.sensorParams.encoders["timestamp_timeOfDay"] != null)
            {
                errScore += 40;
            }

            // Make models that contain the __timestamp_timeOfDay encoder run a bit
            // slower so we can test that we successfully kill running models
            //double? waitTime = null;
            //if (Environment.GetEnvironmentVariable("NTA_TEST_variableWaits") == "false")
            //{
            //    if (perm.modelParams.sensorParams.encoders["timestamp_timeOfDay"] != null)
            //        waitTime = 0.01;
            //}

            var dummyModelParams = new Map<string, object>
            {
                { "metricValue", errScore},
                { "iterations", Environment.GetEnvironmentVariable("NTA_TEST_numIterations") ?? "1"},
                { "waitTime", null},
                { "sysExitModelRange", Environment.GetEnvironmentVariable("NTA_TEST_sysExitModelRange")},
                { "errModelRange", Environment.GetEnvironmentVariable("NTA_TEST_errModelRange")},
                { "jobFailErr", bool.Parse(Environment.GetEnvironmentVariable("NTA_TEST_jobFailErr") ?? "false") }
            };

            return dummyModelParams;
        }

        public override bool permutationFilter(PermutationModelParameters perm)
        {
            //int limit = int.Parse(Environment.GetEnvironmentVariable("NTA_TEST_maxvalFilter") ?? "300");
            //if ((double)perm.modelParams.sensorParams.encoders["consumption"].maxval > limit)
            //    return false;

            return true;
        }

        #endregion
    }
}