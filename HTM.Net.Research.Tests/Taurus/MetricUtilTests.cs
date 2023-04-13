using System.Collections.Generic;
using HTM.Net.Research.Taurus.MetricCollectors;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Taurus
{
    [TestClass]
    public class MetricUtilTests
    {
        [TestMethod]
        public void TestGetConfiguration()
        {
            MetricsConfiguration config = MetricUtils.GetMetricsConfiguration();
            Assert.IsNotNull(config);
            Assert.IsTrue(config.Count>100);
        }

        [TestMethod]
        public void TestGetMetricNamesFromConfig()
        {
            MetricsConfiguration config = MetricUtils.GetMetricsConfiguration();
            Assert.IsNotNull(config);
            List<string> names = MetricUtils.GetMetricNamesFromConfig(config);
            Assert.IsNotNull(names);
            Assert.IsTrue(names.Count > 100);
            Assert.AreNotEqual(config.Count, names.Count);
        }
    }
}