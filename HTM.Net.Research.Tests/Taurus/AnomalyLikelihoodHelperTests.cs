using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Research.Taurus.HtmEngine;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Research.Taurus.HtmEngine.Runtime;
using HTM.Net.Util;
using log4net;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HTM.Net.Research.Tests.Taurus
{
    [TestClass]
    public class AnomalyLikelihoodHelperTests
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(AnomalyLikelihoodHelperTests));

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void GenerateAnomalyParams_EmptyArgs()
        {
            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            helper.GenerateAnomalyParams(null, null, null);
        }

        [TestMethod, ExpectedException(typeof(ArgumentNullException))]
        public void GenerateAnomalyParams_EmptySampleCache()
        {
            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            helper.GenerateAnomalyParams(Guid.NewGuid().ToString(), null, null);
        }

        [TestMethod]
        public void GenerateAnomalyParams_NoRecords()
        {
            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            var result = helper.GenerateAnomalyParams(Guid.NewGuid().ToString(), new List<MetricData>(), new Map<string, object>());
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GenerateAnomalyParams_NotEnoughRecords()
        {
            const long recordCount = 100;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            var result = helper.GenerateAnomalyParams(Guid.NewGuid().ToString(), metricData, new Map<string, object>());
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void GenerateAnomalyParams_EnoughRecords_NullDistribution()
        {
            const long recordCount = 200;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            var result = helper.GenerateAnomalyParams(metricId,
                metricData,
                new Map<string, object>());
            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result.ContainsKey("last_rowid_for_stats"));
            Assert.IsTrue(result.ContainsKey("params"));

            var lastRowId = result["last_rowid_for_stats"];
            var pars = result["params"];

            Assert.IsInstanceOfType(lastRowId, typeof(long));
            Assert.IsInstanceOfType(pars, typeof(AnomalyLikelihood.AnomalyParams));

            Assert.AreEqual(recordCount - 1, lastRowId);
            AnomalyLikelihood.AnomalyParams p = (AnomalyLikelihood.AnomalyParams)pars;
            Assert.IsNotNull(p);
            Assert.IsTrue(p.HistoricalLikelihoods.All(l => l == 0.5));
        }

        [TestMethod]
        public void GenerateAnomalyParams_EnoughRecords_Distribution()
        {
            const long recordCount = 500;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            // execute multiple times
            Map<string, object> result;
            //for (int i = 0; i < (recordCount / 10) - 1; i++)
            //{
            //    helper.GenerateAnomalyParams(metricId, metricData.Skip(i * 10).Take(10).ToList(), new Map<string, object>());
            //}
            result = helper.GenerateAnomalyParams(metricId,
                metricData,
                new Map<string, object>());

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            Assert.IsTrue(result.ContainsKey("last_rowid_for_stats"));
            Assert.IsTrue(result.ContainsKey("params"));

            var lastRowId = result["last_rowid_for_stats"];
            var pars = result["params"];

            Assert.IsInstanceOfType(lastRowId, typeof(long));
            Assert.IsInstanceOfType(pars, typeof(AnomalyLikelihood.AnomalyParams));

            Assert.AreEqual(recordCount - 1, lastRowId);
            AnomalyLikelihood.AnomalyParams p = (AnomalyLikelihood.AnomalyParams)pars;
            Assert.IsNotNull(p);
            // Assert.IsTrue(p.HistoricalLikelihoods.All(l => l > 0.5), "p.HistoricalLikelihoods.All(l => l > 0.5)");
        }

        [TestMethod, ExpectedException(typeof(MetricNotActiveError))]
        public void InitAnomalyLikelihoodModel_NotActive()
        {
            const long recordCount = 500;
            
            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;

            helper.InitAnomalyLikelihoodModel(metricObj, metricData);
        }

        [TestMethod]
        public void InitAnomalyLikelihoodModel_Active()
        {
            const long recordCount = 500;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;
            metricObj.Status = MetricStatus.Active;

            ModelParams mPars = new ModelParams();
            mPars.AnomalyLikelihoodParams = new Map<string, object>();

            metricObj.ModelParams = JsonConvert.SerializeObject(mPars);

            var result = helper.InitAnomalyLikelihoodModel(metricObj, metricData);
            Assert.IsNotNull(result);
            Assert.AreEqual(6, result.Count);
            Assert.IsNotNull(result["anomalyParams"]);
            Assert.IsNotNull(result["statsSampleCache"]);
            Assert.IsNotNull(result["startRowIndex"]);
        }

        [TestMethod]
        public void InitAnomalyLikelihoodModel_Active_NotEnoughRecords()
        {
            const long recordCount = 150;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;
            metricObj.Status = MetricStatus.Active;

            ModelParams mPars = new ModelParams();
            mPars.AnomalyLikelihoodParams = new Map<string, object>();

            metricObj.ModelParams = JsonConvert.SerializeObject(mPars);

            var result = helper.InitAnomalyLikelihoodModel(metricObj, metricData);
            Assert.IsNotNull(result);
            Assert.AreEqual(6, result.Count);
            Assert.IsNotNull(result["anomalyParams"]);
            Assert.IsNull(result["statsSampleCache"]);
            Assert.IsNotNull(result["startRowIndex"]);
        }

        [TestMethod]
        public void GetStatisticsRefreshInterval_Negative()
        {
            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            int interval = helper.GetStatisticsRefreshInterval(-1);
            Assert.AreEqual(10, interval);
        }

        [TestMethod]
        public void GetStatisticsRefreshInterval_Positive()
        {
            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);
            int interval = helper.GetStatisticsRefreshInterval(200);
            Assert.AreEqual((int)(200 * 0.1), interval);
        }

        [TestMethod]
        public void UpdateModelAnomalyScores()
        {
            const long recordCount = 500;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            // generate params for the metricObj
            var @params = helper.GenerateAnomalyParams(metricId,
                metricData,
                new Map<string, object>());

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;
            metricObj.Status = MetricStatus.Active;

            ModelParams mPars = new ModelParams();
            mPars.AnomalyLikelihoodParams = @params;

            metricObj.ModelParams = JsonConvert.SerializeObject(mPars);

            Console.WriteLine(metricObj.ModelParams);

            var map = helper.UpdateModelAnomalyScores(metricObj, metricData);
            Assert.IsNotNull(map);
        }

        [TestMethod, ExpectedException(typeof(MetricNotActiveError))]
        public void UpdateModelAnomalyScores_NotActive()
        {
            const long recordCount = 500;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;
            metricObj.Status = MetricStatus.Unmonitored;

            ModelParams mPars = new ModelParams();

            metricObj.ModelParams = JsonConvert.SerializeObject(mPars, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            helper.UpdateModelAnomalyScores(metricObj, metricData);
        }

        [TestMethod]
        public void UpdateModelAnomalyScores_NoAnomalyArgs()
        {
            const long recordCount = 500;

            string metricId = Guid.NewGuid().ToString();
            var metricData = GetMetricData(recordCount, metricId);

            AnomalyLikelihoodHelper helper = new AnomalyLikelihoodHelper(_log);

            Metric metricObj = new Metric();
            metricObj.Uid = metricId;
            metricObj.Status = MetricStatus.Active;

            ModelParams mPars = new ModelParams();

            metricObj.ModelParams = JsonConvert.SerializeObject(mPars, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });

            var map = helper.UpdateModelAnomalyScores(metricObj, metricData);
            Assert.IsNotNull(map);
        }

        private static List<MetricData> GetMetricData(long recordCount, string metricId)
        {
            IRandom random = new XorshiftRandom(42);
            var metricData = new List<MetricData>();

            for (int i = 0; i < recordCount; i++)
            {
                metricData.Add(new MetricData(metricId, DateTime.Now.AddMinutes(-(5*i)), random.NextDouble()*5,
                    random.NextDouble(), i)
                {
                    RawAnomalyScore = random.NextDouble()*5
                });
            }
            return metricData;
        }
    }
}