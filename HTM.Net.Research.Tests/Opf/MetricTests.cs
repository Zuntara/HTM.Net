using System;
using HTM.Net.Research.opf;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Opf
{
    [TestClass]
    public class MetricTests
    {
        private const double DELTA = 0.01;
        private const int VERBOSITY = 0;

        [TestMethod]
        public void TestRMSE()
        {
            var rmse = MetricSpec.GetModule(new MetricSpec("rmse", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY }
            }));
            var gt = new[] {9, 4, 5, 6};
            var p = new[] {0, 13, 8, 3};
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                rmse.addInstance(gt[i], p[i]);
            }
            var target = 6.71;
            Assert.IsTrue(Math.Abs((double)rmse.getMetric()["value"] - target) < DELTA);
        }
    }
}