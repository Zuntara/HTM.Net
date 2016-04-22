using System;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Taurus
{
    [TestClass]
    public class ModelsControllerApiTest
    {
        [TestMethod]
        public void TestPut()
        {
            ModelsControllerApi api = new ModelsControllerApi();

            string metricId = Guid.NewGuid().ToString();

            api.Put(null, new[]
            {
                new CreateModelRequest
                {
                    DataSource = "custom",
                    MetricSpec = new CustomMetricSpec()
                    {
                        Metric = "metric1",
                        Resource = "i_dont_know"
                    },
                    ModelParams =new ModelParams
                    {
                        Max = 5000,
                        Min = 10,
                        MinResolution = 0.2
                    }
                }
            });
        }
    }
}