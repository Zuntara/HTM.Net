using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Web.Http;
using System.Web.Http.Results;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using HTM.Net.Taurus.Api;
using HTM.Net.Taurus.Api.Controllers;
using log4net;
using Moq;
using Newtonsoft.Json;

namespace HTM.Net.Taurus.Api.Tests.Controllers
{
    /// <summary>
    /// Implement https://github.com/numenta/numenta-apps/blob/master/htmengine/htmengine/runtime/metric_listener.py
    /// metriclistener to have the other side that fills up the datasamples
    /// goes to metric_streamer through metric_storer > database
    /// </summary>
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
                RepositoryFactory.Metric.AddMetricData(m.Uid, new List<Tuple<DateTime, double>>
                {
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-30), 100),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-25), 101),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-20), 102),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-15), 101),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-10), 80),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(-5), 120),
                    new Tuple<DateTime, double>(DateTime.Now.AddMinutes(0), 110),
                });
            };
            // Add some metric data first
            ModelsController api = new ModelsController();

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

        [TestMethod]
        public void TestCreateModelsBatch()
        {
            DataAdapterFactory.ClearRegistrations();
            DataAdapterFactory.RegisterDatasourceAdapter(new CustomDatasourceAdapter());
            RepositoryFactory.Metric = new MetricMemRepository();

            MetricsConfiguration configuration = MetricUtils.GetMetricsConfiguration();

            List<CreateModelRequest> requests = new List<CreateModelRequest>();
            foreach (KeyValuePair<string, MetricConfigurationEntry> pair in configuration)
            {
                string resName = pair.Key;
                var resVal = pair.Value;
                foreach (var metric in resVal.Metrics)
                {
                    string metricName = metric.Key;
                    var metricVal = metric.Value;

                    CreateModelRequest createRequest = new CreateModelRequest();
                    createRequest.DataSource = "custom";
                    createRequest.MetricSpec = new CustomMetricSpec
                    {
                        Metric = metricName,
                        Resource = resName,
                        UserInfo = new
                        {
                            metricVal.MetricType,
                            metricVal.MetricTypeName,
                            resVal.Symbol
                        }
                    };
                    createRequest.ModelParams = ModelParams.FromDict(metricVal.ModelParams);
                    requests.Add(createRequest);
                }
            }
            Assert.IsTrue(requests.Count > 0);

            ModelsController app = new ModelsController();
            var result = app.Put(null, requests.ToArray());
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(CreatedNegotiatedContentResult<List<Metric>>));
            CreatedNegotiatedContentResult<List<Metric>> created = (CreatedNegotiatedContentResult<List<Metric>>)result;
            var metrics = created.Content;
            Assert.IsNotNull(metrics);
            Assert.AreEqual(requests.Count, metrics.Count);
        }

        [TestMethod]
        public void TestCreateModelsSingle()
        {
            ModelsController app = new ModelsController();

            RepositoryFactory.Metric = new MetricMemRepository();

            DataAdapterFactory.ClearRegistrations();
            DataAdapterFactory.RegisterDatasourceAdapter(new CustomDatasourceAdapter());

            MetricsConfiguration configuration = MetricUtils.GetMetricsConfiguration();

            foreach (KeyValuePair<string, MetricConfigurationEntry> pair in configuration)
            {
                string resName = pair.Key;
                var resVal = pair.Value;
                foreach (var metric in resVal.Metrics)
                {
                    string metricName = metric.Key;
                    var metricVal = metric.Value;

                    CreateModelRequest createRequest = new CreateModelRequest();
                    createRequest.DataSource = "custom";
                    createRequest.MetricSpec = new CustomMetricSpec
                    {
                        Metric = metricName,
                        Resource = resName,
                        UserInfo = new
                        {
                            metricVal.MetricType,
                            metricVal.MetricTypeName,
                            resVal.Symbol
                        }
                    };
                    createRequest.ModelParams = ModelParams.FromDict(metricVal.ModelParams);

                    var result = app.Put(null, new[] { createRequest });
                    Assert.IsNotNull(result);
                    Assert.IsInstanceOfType(result, typeof(CreatedNegotiatedContentResult<List<Metric>>));
                    CreatedNegotiatedContentResult<List<Metric>> created = (CreatedNegotiatedContentResult<List<Metric>>)result;
                    var metrics = created.Content;
                    Assert.IsNotNull(metrics);
                    Assert.AreEqual(1, metrics.Count);
                    var responseModel = metrics.First();

                    Assert.AreEqual(metricName, responseModel.Name);
                    Assert.AreEqual(resName, responseModel.Server);
                    Assert.IsNotNull(responseModel.Parameters);
                    Assert.IsTrue(responseModel.Parameters.Contains("ModelParams"));
                    Assert.IsTrue(responseModel.Parameters.Contains("MinResolution"));
                    CreateModelRequest mp = JsonConvert.DeserializeObject<CreateModelRequest>(responseModel.Parameters);
                    Assert.AreEqual(metricVal.ModelParams["minResolution"], mp.ModelParams.MinResolution);
                    Assert.IsTrue(responseModel.Parameters.Contains("MetricSpec"));
                    Assert.IsTrue(responseModel.Parameters.Contains("Metric"));
                    Assert.AreEqual(metricName, mp.MetricSpec.Metric);
                    Assert.AreEqual(resName, mp.MetricSpec.Resource);
                    //Assert.AreEqual(resVal.Symbol, mp.MetricSpec.UserInfo.Symbol);

                }
            }


        }

        [TestMethod]
        public void TestDelete()
        {
            Mock<IDataSourceAdapter> mockDs = new Mock<IDataSourceAdapter>();
            mockDs.Setup(ds => ds.Datasource).Returns("custom");
            mockDs.Setup(ds => ds.UnmonitorMetric(It.IsAny<string>())).Verifiable();

            Mock<IMetricRepository> mockRepo = new Mock<IMetricRepository>();
            mockRepo.Setup(mr => mr.GetMetric(It.IsAny<string>())).Returns(new Metric
            {
                DataSource = "custom",
                Uid = "dummy"
            });
            RepositoryFactory.Metric = mockRepo.Object;

            DataAdapterFactory.ClearRegistrations();
            DataAdapterFactory.RegisterDatasourceAdapter(mockDs.Object);

            ModelsController app = new ModelsController();


            // remove it again
            var result = app.Delete("dummy");
            Assert.IsNotNull(result);
            Assert.IsInstanceOfType(result, typeof(OkResult));

            mockDs.Verify(ds => ds.UnmonitorMetric(It.IsAny<string>()));

        }
    }
}
