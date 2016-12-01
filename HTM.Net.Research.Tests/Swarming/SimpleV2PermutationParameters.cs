using System;
using System.Collections.Generic;
using HTM.Net.Encoders;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming
{
    [Serializable]
    public class SimpleV2PermutationParameters : ExperimentPermutationParameters
    {
        public SimpleV2PermutationParameters()
        {
            PredictedField = "consumption";
            Encoders = new Map<string, object>
            {
                {
                    "gym",
                    new PermuteEncoder(fieldName: "gym", encoderType: "SDRCategoryEncoder",
                        kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})
                },
                {
                    "timestamp_dayOfWeek",
                    new PermuteEncoder(fieldName: "timestamp", encoderType: "DateEncoder.dayOfWeek",
                        kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 3.0})}, {"w", 7}})
                },
                {
                    "timestamp_timeOfDay",
                    new PermuteEncoder(fieldName: "timestamp", encoderType: "DateEncoder.timeOfDay",
                        kwArgs: new KWArgsModel {{"radius", new PermuteChoices(new[] {1.0, 8.0})}, {"w", 7}})
                },
                {
                    "consumption",
                    new PermuteEncoder(fieldName: "consumption", encoderType: "ScalarEncoder",
                        kwArgs:
                            new KWArgsModel
                            {
                                {"maxval", new PermuteInt(100, 300, 25)},
                                {"n", new PermuteInt(13, 500, 20)},
                                {"w", 7},
                                {"minval", 0},
                                {"forced", true}
                            })
                },
                {
                    "address",
                    new PermuteEncoder(fieldName: "address", encoderType: "SDRCategoryEncoder",
                        kwArgs: new KWArgsModel {{"w", 7}, {"n", 100}})
                },
            };

            SetParameterByKey(KEY.MIN_THRESHOLD, new PermuteInt(9, 12));
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, new PermuteInt(12, 16));

            Report = new[] { ".*consumption.*" };
            Minimize = "prediction:rmse:field=consumption";
        }

        #region Overrides of ExperimentPermutationParameters

        public override IDictionary<string, object> DummyModelParams(ExperimentPermutationParameters parameters)
        {
            double errScore = 50;

            errScore += Math.Abs((int)((PermuteEncoder)parameters.Encoders["consumption"]).maxval - 250);
            errScore += Math.Abs((int)((PermuteEncoder)parameters.Encoders["consumption"]).n - 53);

            if (parameters.Encoders["address"] != null)
            {
                errScore -= 20;
            }
            if (parameters.Encoders["gym"] != null)
            {
                errScore -= 10;
            }

            // Make models that contain the __timestamp_timeOfDay encoder run a bit
            // slower so we can test that we successfully kill running models
            double? waitTime = null;
            if (Environment.GetEnvironmentVariable("NTA_TEST_variableWaits") == "false")
            {
                if (parameters.Encoders["timestamp_timeOfDay"] != null)
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

        public override bool PermutationFilter(ExperimentPermutationParameters parameters)
        {
            int limit = int.Parse(Environment.GetEnvironmentVariable("NTA_TEST_maxvalFilter") ?? "300");
            if (((EncoderSetting)parameters.Encoders["consumption"])?.maxVal > limit)
                return false;

            return true;
        }

        #endregion
    }
}