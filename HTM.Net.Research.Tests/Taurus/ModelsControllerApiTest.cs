using System;
using System.Collections.Generic;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Taurus
{
    [TestClass]
    public class ModelsControllerApiTest
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(ModelsControllerApiTest));

        [TestMethod]
        public void TestPut()
        {
            RepositoryFactory.Metric.MetricAdded += m =>
            {
                // Add some data for this metric
                RepositoryFactory.Metric.AddMetricData(m.Uid, new List<Tuple<double, DateTime>>
                {
                    new Tuple<double, DateTime>(100, DateTime.Now.AddMinutes(-30)),
                    new Tuple<double, DateTime>(101, DateTime.Now.AddMinutes(-25)),
                    new Tuple<double, DateTime>(102, DateTime.Now.AddMinutes(-20)),
                    new Tuple<double, DateTime>(101, DateTime.Now.AddMinutes(-15)),
                    new Tuple<double, DateTime>(80, DateTime.Now.AddMinutes(-10)),
                    new Tuple<double, DateTime>(120, DateTime.Now.AddMinutes(-5)),
                    new Tuple<double, DateTime>(110, DateTime.Now.AddMinutes(0)),
                });
            };
            // Add some metric data first
            ModelsControllerApi api = new ModelsControllerApi();


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