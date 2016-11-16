using System;
using System.Linq;
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
            var gt = new[] { 9, 4, 5, 6 };
            var p = new object[] { 0, 13, 8, 3 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                rmse.addInstance(gt[i], p[i]);
            }
            var target = 6.71;
            Assert.IsTrue(Math.Abs((double)rmse.getMetric()["value"] - target) < DELTA);
        }

        [TestMethod]
        public void TestWindowedRMSE()
        {
            var wrmse = MetricSpec.GetModule(new MetricSpec("rmse", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 3 }
            }));
            var gt = new[] { 9, 4, 4, 100, 44 };
            var p = new object[] { 0, 13, 4, 6, 7 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                wrmse.addInstance(gt[i], p[i]);
            }
            var target = 58.324;

            var actual = Math.Abs((double)wrmse.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        [TestMethod]
        public void TestLongWindowRMSE()
        {
            var wrmse = MetricSpec.GetModule(new MetricSpec("rmse", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 100 }
            }));
            var gt = new[] { 9, 4, 5, 6 };
            var p = new [] { 0, 13, 8, 3};
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                wrmse.addInstance(gt[i], p[i]);
            }
            var target = 6.71;

            var actual = Math.Abs((double)wrmse.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        [TestMethod]
        public void TestAAE()
        {
            var aae = MetricSpec.GetModule(new MetricSpec("aae", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY }
            }));
            var gt = new[] { 9, 4, 5, 6 };
            var p = new object[] { 0, 13, 8, 3 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                aae.addInstance(gt[i], p[i]);
            }
            var target = 6.0;
            var actual = Math.Abs((double)aae.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        /// <summary>
        /// Multistep AAE metric test
        /// </summary>
        [TestMethod]
        public void TestMultistepAAE()
        {
            var msp = MetricSpec.GetModule(new MetricSpec("multiStep", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 100 },
                {"errorMetric", "aae" },
                {"steps", 3 },
            }));
            // Make each ground truth 1 greater than the prediction
            var gt = ArrayUtils.Range(0, 100).Select(i => i + 1).ToArray();

            var p = ArrayUtils.Range(0, 100)
                .Select(i => new Map<object, object>
                {
                    {
                        3, new Map<object, object>
                        {
                            {i, 0.7},
                            {5, 0.3}
                        }
                    }
                }).ToArray();
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                msp.addInstance(gt[i], p[i]);
            }
            var target = 1.0;
            var actual = Math.Abs((double)msp.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        /// <summary>
        /// Multistep AAE metric test, predicting 2 different step sizes
        /// </summary>
        [TestMethod]
        public void TestMultistepAAEMultipleSteps()
        {
            var msp = MetricSpec.GetModule(new MetricSpec("multiStep", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 100 },
                {"errorMetric", "aae" },
                {"steps", new[] { 3,6} },
            }));

            // Make each 3 step prediction +1 over ground truth and each 6 step prediction +0.5 over ground truth
            var gt = ArrayUtils.Range(0, 100);

            var p = ArrayUtils.Range(0, 100)
                .Select(i => new Map<object, object>
                {
                    {
                        3, new Map<object, object>
                        {
                            {i + 1, 0.7},
                            {5, 0.3}
                        }
                    },
                    {
                        6, new Map<object, object>
                        {
                            {i + 0.5, 0.7},
                            {5, 0.3}
                        }
                    }
                }).ToArray();
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                msp.addInstance(gt[i], p[i]);
            }
            var target = 0.75; // average of +1 error and 0.5 error
            var actual = Math.Abs((double)msp.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        [TestMethod]
        public void TestWindowedAAE()
        {
            var wrmse = MetricSpec.GetModule(new MetricSpec("aae", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 1 }
            }));
            var gt = new[] { 9, 4, 5, 6 };
            var p = new object[] { 0, 13, 8, 3 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                wrmse.addInstance(gt[i], p[i]);
            }
            var target = 3.0;

            var actual = Math.Abs((double)wrmse.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        [TestMethod]
        public void TestAverageError()
        {
            var err = MetricSpec.GetModule(new MetricSpec("avg_err", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY }
            }));
            var gt = new[] { 1, 1, 2, 3, 4, 5 };
            var p = new [] { 0, 1, 2, 4, 5, 6 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                err.addInstance(gt[i], p[i]);
            }
            var target = (2.0 / 3.0);
            var actual = Math.Abs((double)err.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }

        [TestMethod]
        public void TestWindowedAverageError()
        {
            var wrmse = MetricSpec.GetModule(new MetricSpec("avg_err", null, null, new Map<string, object>
            {
                {"verbosity", VERBOSITY },
                {"window", 2 }
            }));
            var gt = new[] { 0, 1, 2, 3, 4, };
            var p = new [] { 0, 1, 2, 4, 5, 6 };
            foreach (var i in ArrayUtils.XRange(0, gt.Length, 1))
            {
                wrmse.addInstance(gt[i], p[i]);
            }
            var target = 1.0;

            var actual = Math.Abs((double)wrmse.getMetric()["value"] - target);
            Assert.IsTrue(actual < DELTA, $"Got {actual} instead of {DELTA}");
        }
    }
}