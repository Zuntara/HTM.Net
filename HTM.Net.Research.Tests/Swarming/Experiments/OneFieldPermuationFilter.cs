using System;
using HTM.Net.Encoders;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Swarming.Experiments
{
    public class OneFieldPermuationFilter : ExperimentPermutationParameters
    {
        public OneFieldPermuationFilter()
        {
            PredictedField = "consumption";

            Encoders = new Map<string, object>
            {
                {
                    "consumption",
                    new PermuteEncoder(
                        fieldName: "consumption",
                        encoderType: "ScalarEncoder",
                        kwArgs: new KWArgsModel
                        {
                            {"maxVal", new PermuteInt(100, 300, 1)},
                            {"n", new PermuteInt(13, 500, 1)},
                            {"w", 7},
                            {"minVal", 0},
                        })
                }
            };
            Report = new[] { ".*consumption.*" };
            Minimize = "prediction:rmse:field=consumption";

            SetParameterByKey(KEY.MIN_THRESHOLD, new PermuteInt(9, 12));
            SetParameterByKey(KEY.ACTIVATION_THRESHOLD, new PermuteInt(12, 16));
        }

        #region Overrides of ExperimentPermutationParameters

        public override DummyModelParameters DummyModelParams(ExperimentPermutationParameters parameters, bool forTesting)
        {
            if(forTesting) return new DummyModelParameters();
            double errScore = 50;
            double waitTime = 0.01;

            var dummyModelParams = new DummyModelParameters
            {
                metricValue = errScore,
                iterations = int.Parse(Environment.GetEnvironmentVariable("NTA_TEST_numIterations") ?? "5"),
                waitTime = waitTime,
                sysExitModelRange = Environment.GetEnvironmentVariable("NTA_TEST_sysExitModelRange"),
                delayModelRange = Environment.GetEnvironmentVariable("NTA_TEST_delayModelRange"),
                errModelRange = Environment.GetEnvironmentVariable("NTA_TEST_errModelRange"),
                jobFailErr = bool.Parse(Environment.GetEnvironmentVariable("NTA_TEST_jobFailErr") ?? "false")
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