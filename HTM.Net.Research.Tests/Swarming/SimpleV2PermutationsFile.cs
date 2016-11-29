using System;
using System.Collections.Generic;
using HTM.Net.Research.Swarming;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming
{
    public class SimpleV2PermutationsFile : BasePermutations
    {
        public SimpleV2PermutationsFile()
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
                            {"gym",new PermuteEncoder(fieldName: "gym", encoderType: "SDRCategoryEncoder",kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})},
                            {"timestamp_dayOfWeek",new PermuteEncoder(fieldName: "timestamp", encoderType: "DateEncoder.dayOfWeek",kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 3.0})}, {"w", 7}})},
                            {"timestamp_timeOfDay",new PermuteEncoder(fieldName: "timestamp", encoderType: "DateEncoder.timeOfDay",kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 8.0})}, {"w", 7}})},
                            {"consumption",new PermuteEncoder(fieldName: "consumption", encoderType: "ScalarEncoder",kwArgs:new KWArgsModel{{"maxval", new PermuteInt(100, 300, 25)},{"n", new PermuteInt(13, 500, 20)},{"w", 7},{"minval", 0}})},
                            {"address",new PermuteEncoder(fieldName: "address", encoderType: "SDRCategoryEncoder",kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})},
                        }
                    },
                    tpParams = new PermutationTemporalPoolerParams
                    {
                        minThreshold = new PermuteInt(9, 12),
                        activationThreshold = new PermuteInt(12, 16),
                    }
                }
            };

            report = new[] { ".*consumption.*" };
            minimize = "prediction:rmse:field=consumption";
            
        }

        #region Implementation of IPermutionFilter

        public override IDictionary<string, object> dummyModelParams(PermutationModelParameters perm)
        {
            double errScore = 50;

            errScore += Math.Abs((int)((PermuteEncoder)perm.modelParams.sensorParams.encoders["consumption"])["maxval"] - 250);
            errScore += Math.Abs((int)((PermuteEncoder)perm.modelParams.sensorParams.encoders["consumption"])["n"] - 53);

            if (perm.modelParams.sensorParams.encoders["address"] != null)
            {
                errScore -= 20;
            }
            if (perm.modelParams.sensorParams.encoders["gym"] != null)
            {
                errScore -= 10;
            }

            // Make models that contain the __timestamp_timeOfDay encoder run a bit
            // slower so we can test that we successfully kill running models
            double? waitTime = null;
            if (Environment.GetEnvironmentVariable("NTA_TEST_variableWaits") == "false")
            {
                if (perm.modelParams.sensorParams.encoders["timestamp_timeOfDay"] != null)
                    waitTime = 0.01;
            }

            var dummyModelParams = new Map<string, object>
            {
                { "metricValue", errScore},
                { "iterations", Environment.GetEnvironmentVariable("NTA_TEST_numIterations") ?? "1"},
                { "waitTime", waitTime},
                { "sysExitModelRange", Environment.GetEnvironmentVariable("NTA_TEST_sysExitModelRange")},
                { "errModelRange", Environment.GetEnvironmentVariable("NTA_TEST_errModelRange")},
                { "jobFailErr", bool.Parse(Environment.GetEnvironmentVariable("NTA_TEST_jobFailErr") ?? "false") }
            };

            return dummyModelParams;
        }

        public override bool permutationFilter(PermutationModelParameters perm)
        {
            int limit = int.Parse(Environment.GetEnvironmentVariable("NTA_TEST_maxvalFilter") ?? "300");
            if ((double)((PermuteEncoder)perm.modelParams.sensorParams.encoders["consumption"]).maxval > limit)
                return false;

            return true;
        }

        #endregion
    }
}