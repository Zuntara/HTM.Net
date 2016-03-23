using System;
using HTM.Net.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    /**
 * Tests for anomaly score functions and classes.
 * 
 * @author David Ray
 */
    [TestClass]
    public class AnomalyTest
    {

        [TestMethod]
        public void TestComputeRawAnomalyScoreNoActiveOrPredicted()
        {
            double score = Anomaly.ComputeRawAnomalyScore(new int[0], new int[0]);
            Assert.AreEqual(score, 0.0, 0.00001);
        }

        [TestMethod]
        public void TestComputeRawAnomalyScoreNoActive()
        {
            double score = Anomaly.ComputeRawAnomalyScore(new int[0], new[] { 3, 5 });
            Assert.AreEqual(score, 1.0, 0.00001);
        }

        [TestMethod]
        public void TestComputeRawAnomalyScorePerfectMatch()
        {
            double score = Anomaly.ComputeRawAnomalyScore(new[] { 3, 5, 7 }, new[] { 3, 5, 7 });
            Assert.AreEqual(score, 0.0, 0.00001);
        }

        [TestMethod]
        public void TestComputeRawAnomalyScoreNoMatch()
        {
            double score = Anomaly.ComputeRawAnomalyScore(new[] { 2, 4, 6 }, new[] { 3, 5, 7 });
            Assert.AreEqual(score, 1.0, 0.00001);
        }

        [TestMethod]
        public void TestComputeRawAnomalyPartialNoMatch()
        {
            double score = Anomaly.ComputeRawAnomalyScore(new[] { 2, 3, 6 }, new[] { 3, 5, 7 });
            Console.WriteLine((2.0 / 3.0));
            Assert.AreEqual(score, 2.0 / 3.0, 0.001);
        }

        /////////////////////////////////////////////////////////////////

        [TestMethod]
        public void TestComputeAnomalyScoreNoActiveOrPredicted()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);
            double score = anomalyComputer.Compute(new int[0], new int[0], 0, 0);
            Assert.AreEqual(0.0, score, 0);
        }

        [TestMethod]
        public void TestComputeAnomalyScoreNoActive()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);
            double score = anomalyComputer.Compute(new int[0], new[] { 3, 5 }, 0, 0);
            Assert.AreEqual(1.0, score, 0);
        }

        [TestMethod]
        public void TestComputeAnomalyScorePerfectMatch()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);
            double score = anomalyComputer.Compute(new[] { 3, 5, 7 }, new[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(0.0, score, 0);
        }

        [TestMethod]
        public void TestComputeAnomalyScoreNoMatch()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);
            double score = anomalyComputer.Compute(new[] { 2, 4, 6 }, new[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(1.0, score, 0);
        }

        [TestMethod]
        public void TestComputeAnomalyScorePartialMatch()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);
            double score = anomalyComputer.Compute(new[] { 2, 3, 6 }, new[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(2.0 / 3.0, score, 0);
        }

        [TestMethod]
        public void TestAnomalyCumulative()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);

            Anomaly anomalyComputer = Anomaly.Create(@params);

            object[] predicted =
            {
                new[] {1, 2, 6}, new[] {1, 2, 6}, new[] {1, 2, 6},
                new[] {1, 2, 6}, new[] {1, 2, 6}, new[] {1, 2, 6},
                new[] {1, 2, 6}, new[] {1, 2, 6}, new[] {1, 2, 6}
            };
            object[] actual =
            {
                new[] {1, 2, 6}, new[] {1, 2, 6}, new[] {1, 4, 6},
                new[] {10, 11, 6}, new[] {10, 11, 12}, new[] {10, 11, 12},
                new[] {10, 11, 12}, new[] {1, 2, 6}, new[] {1, 2, 6}
            };

            double[] anomalyExpected = { 0.0, 0.0, 1.0 / 9.0, 3.0 / 9.0, 2.0 / 3.0, 8.0 / 9.0, 1.0, 2.0 / 3.0, 1.0 / 3.0 };
            for (int i = 0; i < 9; i++)
            {
                double score = anomalyComputer.Compute((int[])actual[i], (int[])predicted[i], 0, 0);
                Assert.AreEqual(anomalyExpected[i], score, 0.01);
            }
        }

    }
}