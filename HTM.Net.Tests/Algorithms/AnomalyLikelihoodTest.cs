using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class AnomalyLikelihoodTest
    {
        private AnomalyLikelihood _an;
        private static IRandom _random;

        [TestInitialize]
        public void Setup()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);
            _an = (AnomalyLikelihood)Anomaly.Create(@params);
        }

        /// <summary>
        /// Given the parameters of a distribution, generate numSamples points from it.
        /// This routine is mostly for testing.
        /// </summary>
        /// <param name="random"></param>
        /// <param name="mean"></param>
        /// <param name="variance"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static double[] SampleDistribution(IRandom random, double mean, double variance, int size)
        {
            SampleDistribution sampler = new SampleDistribution(mean, Math.Sqrt(variance), size);
            return sampler.GetSample(random);
        }
        /// <summary>
        /// Generate 1440 samples of fake metrics data with a particular distribution
        /// of anomaly scores and metric values. Here we generate values every minute.
        /// </summary>
        /// <param name="mean"></param>
        /// <param name="variance"></param>
        /// <param name="metricMean"></param>
        /// <param name="metricVariance"></param>
        /// <returns></returns>
        public static List<Sample> GenerateSampleData(double mean, double variance, double metricMean, double metricVariance)
        {
            List<Sample> retVal = new List<Sample>();
            if(_random == null) _random = new XorshiftRandom(42);
            double[] samples = SampleDistribution(_random, mean, variance, 1440);
            double[] metricValues = SampleDistribution(_random, metricMean, metricVariance, 1440);
            for (int hour = 0; hour < 24; hour++)
            {
                for (int minute = 0; minute < 60; minute++)
                {
                    retVal.Add(new Sample(new DateTime(2013, 2, 2, hour, minute, 0), metricValues[hour * 60 + minute],
                        samples[hour * 60 + minute]));
                }
            }
            return retVal;
        }

        public static bool AssertWithinEpsilon(double a, double b)
        {
            return AssertWithinEpsilon(a, b, 0.001);
        }

        public static bool AssertWithinEpsilon(double a, double b, double epsilon)
        {
            if (Math.Abs(a - b) <= epsilon)
            {
                return true;
            }
            return false;
        }

        [TestMethod]
        public void TestNormalProbability()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, 0.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 1.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, 1.0);

            // Test a standard normal distribution
            // Values taken from http://en.wikipedia.org/wiki/Standard_normal_table
            AssertWithinEpsilon(_an.NormalProbability(0.0, p), 0.5);
            AssertWithinEpsilon(_an.NormalProbability(0.3, p), 0.3820885780);
            AssertWithinEpsilon(_an.NormalProbability(1.0, p), 0.1587);
            AssertWithinEpsilon(1.0 - _an.NormalProbability(-0.3, p), 1.0 - _an.NormalProbability(0.3, p));

            // Non standard normal distribution
            // p = {"name": "normal", "mean": 1.0, "variance": 4.0, "stdev": 2.0}
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, 1.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 4.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, 2.0);
            AssertWithinEpsilon(_an.NormalProbability(1.0, p), 0.5);
            AssertWithinEpsilon(_an.NormalProbability(2.0, p), 0.3085);
            AssertWithinEpsilon(_an.NormalProbability(3.0, p), 0.1587);
            AssertWithinEpsilon(_an.NormalProbability(3.0, p), 1.0 - _an.NormalProbability(-1.0, p));
            AssertWithinEpsilon(_an.NormalProbability(0.0, p), 1.0 - _an.NormalProbability(2.0, p));

            // Non standard normal distribution
            // p = {"name": "normal", "mean": -2.0, "variance": 0.5, "stdev": math.sqrt(0.5)}
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, -2.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 0.5);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, Math.Sqrt(0.5));
            AssertWithinEpsilon(_an.NormalProbability(-2.0, p), 0.5);
            AssertWithinEpsilon(_an.NormalProbability(-1.5, p), 0.241963652);
            AssertWithinEpsilon(_an.NormalProbability(-2.5, p), 1.0 - _an.NormalProbability(-1.5, p));
        }

        /// <summary>
        /// This passes in a known set of data and ensures the estimateNormal
        /// function returns the expected results.
        /// </summary>
        [TestMethod]
        public void TestEstimateNormal()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);

            // 100 samples drawn from mean=0.4, stdev = 0.5
            double[] samples = new double[]
            {
                0.32259025, -0.44936321, -0.15784842, 0.72142628, 0.8794327,
                0.06323451, -0.15336159, -0.02261703, 0.04806841, 0.47219226,
                0.31102718, 0.57608799, 0.13621071, 0.92446815, 0.1870912,
                0.46366935, -0.11359237, 0.66582357, 1.20613048, -0.17735134,
                0.20709358, 0.74508479, 0.12450686, -0.15468728, 0.3982757,
                0.87924349, 0.86104855, 0.23688469, -0.26018254, 0.10909429,
                0.65627481, 0.39238532, 0.77150761, 0.47040352, 0.9676175,
                0.42148897, 0.0967786, -0.0087355, 0.84427985, 1.46526018,
                1.19214798, 0.16034816, 0.81105554, 0.39150407, 0.93609919,
                0.13992161, 0.6494196, 0.83666217, 0.37845278, 0.0368279,
                -0.10201944, 0.41144746, 0.28341277, 0.36759426, 0.90439446,
                0.05669459, -0.11220214, 0.34616676, 0.49898439, -0.23846184,
                1.06400524, 0.72202135, -0.2169164, 1.136582, -0.69576865,
                0.48603271, 0.72781008, -0.04749299, 0.15469311, 0.52942518,
                0.24816816, 0.3483905, 0.7284215, 0.93774676, 0.07286373,
                1.6831539, 0.3851082, 0.0637406, -0.92332861, -0.02066161,
                0.93709862, 0.82114131, 0.98631562, 0.05601529, 0.72214694,
                0.09667526, 0.3857222, 0.50313998, 0.40775344, -0.69624046,
                -0.4448494, 0.99403206, 0.51639049, 0.13951548, 0.23458214,
                1.00712699, 0.40939048, -0.06436434, -0.02753677, -0.23017904
            };
            Statistic result = _an.EstimateNormal(samples, true);
            Assert.IsTrue(AssertWithinEpsilon(result.Mean, 0.3721));
            Assert.IsTrue(AssertWithinEpsilon(result.Variance, 0.22294));
            Assert.IsTrue(AssertWithinEpsilon(result.Stdev, 0.47216));
        }

        /// <summary>
        /// Test that sampleDistribution from a generated distribution returns roughly the same parameters.
        /// </summary>
        [TestMethod]
        public void TestSampleDistribution()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, 0.5);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, Math.Sqrt(0.1));
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 0.1);

            double[] samples = SampleDistribution(new XorshiftRandom(42), 0.5, 0.1, 1000);
            Statistic np = _an.EstimateNormal(samples, true);
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN), np.Mean, 0.1));
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE), np.Variance, 0.1));
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV), np.Stdev, 0.1));
        }

        /// <summary>
        /// This calls estimateAnomalyLikelihoods to estimate the distribution on fake
        /// data and validates the results
        /// </summary>
        [TestMethod]
        public void TestEstimateAnomalyLikelihoods()
        {
            // Generate an estimate using fake distribution of anomaly scores.
            List<Sample> data = GenerateSampleData(0.2, 0.2, 0.2, 0.2).SubList(0, 1000);

            AnomalyLikelihoodMetrics metrics = _an.EstimateAnomalyLikelihoods(data, 10, 0);
            Assert.AreEqual(1000, metrics.GetLikelihoods().Length);
            Assert.AreEqual(1000, metrics.GetAvgRecordList().AveragedRecords.Count);
            Assert.IsTrue(_an.IsValidEstimatorParams(metrics.GetParams()));

            // Get the total
            double total = 0;
            foreach (Sample sample in metrics.GetAvgRecordList().AveragedRecords)
            {
                total = total + sample.Score;
            }

            // Check that the estimated mean is correct
            Statistic statistic = (Statistic)metrics.GetParams().Distribution();
            Assert.IsTrue(AssertWithinEpsilon(statistic.Mean, (total / (double)metrics.GetAvgRecordList().AveragedRecords.Count)
                )
                );

            int count = ArrayUtils.Where(metrics.GetLikelihoods(), d => d < 0.02).Length;
            Assert.IsTrue(count <= 50);
            Assert.IsTrue(count >= 1);
        }

        /// <summary>
        /// This calls estimateAnomalyLikelihoods with various values of skipRecords
        /// </summary>
        [TestMethod]
        public void TestSkipRecords()
        {
            // Generate an estimate using fake distribution of anomaly scores.
            List<Sample> data = GenerateSampleData(0.1, 0.2, 0.2, 0.2).SubList(0, 200);
            data.AddRange(GenerateSampleData(0.9, 0.2, 0.2, 0.2).SubList(0, 200));

            // skipRecords = 200
            AnomalyLikelihoodMetrics metrics = _an.EstimateAnomalyLikelihoods(data, 10, 200);
            Statistic stats = (Statistic)metrics.GetParams().Distribution();
            // Check results are correct, i.e. we are actually skipping the first 50
            AssertWithinEpsilon(stats.Mean, 0.9, 0.1);

            // Check case where skipRecords > num records
            // In this case a null distribution should be returned which makes all
            // the likelihoods reasonably high
            metrics = _an.EstimateAnomalyLikelihoods(data, 10, 500);
            Assert.AreEqual(metrics.GetLikelihoods().Length, data.Count);
            Assert.IsTrue(ArrayUtils.Sum(metrics.GetLikelihoods()) >= 0.3 * metrics.GetLikelihoods().Length);

            // Check the case where skipRecords == num records
            metrics = _an.EstimateAnomalyLikelihoods(data, 10, data.Count);
            Assert.AreEqual(metrics.GetLikelihoods().Length, data.Count);
            Assert.IsTrue(ArrayUtils.Sum(metrics.GetLikelihoods()) >= 0.3 * metrics.GetLikelihoods().Length);
        }

        /// <summary>
        /// A slight more complex test. This calls estimateAnomalyLikelihoods
        /// to estimate the distribution on fake data, followed by several calls
        /// to updateAnomalyLikelihoods.
        /// </summary>
        [TestMethod]
        public void TestUpdateAnomalyLikelihoods()
        {
            //----------------------------------------
            // Step 1. Generate an initial estimate using fake distribution of anomaly scores.
            List<Sample> data1 = GenerateSampleData(0.2, 0.2, 0.2, 0.2).SubList(0, 1000);
            AnomalyLikelihoodMetrics metrics1 = _an.EstimateAnomalyLikelihoods(data1, 5, 0);

            //----------------------------------------
            // Step 2. Generate some new data with a higher average anomaly
            // score. Using the estimator from step 1, to compute likelihoods. Now we
            // should see a lot more anomalies.
            List<Sample> data2 = GenerateSampleData(0.6, 0.2, 0.2, 0.2).SubList(0, 300);
            AnomalyLikelihoodMetrics metrics2 = _an.UpdateAnomalyLikelihoods(data2, metrics1.GetParams());
            Assert.AreEqual(metrics2.GetLikelihoods().Length, data2.Count);
            Assert.AreEqual(metrics2.GetAvgRecordList().Count, data2.Count);
            Assert.IsTrue(_an.IsValidEstimatorParams(metrics2.GetParams()));

            // The new running total should be different
            Assert.AreNotEqual(metrics1.GetAvgRecordList().Total, metrics2.GetAvgRecordList().Total);

            // We should have many more samples where likelihood is < 0.01, but not all

            int conditionCount = ArrayUtils.Where(metrics2.GetLikelihoods(), d => d < 0.01).Length;
            Assert.IsTrue(conditionCount >= 25);
            Assert.IsTrue(conditionCount <= 250);

            //----------------------------------------
            // Step 3. Generate some new data with the expected average anomaly score. We
            // should see fewer anomalies than in Step 2.
            List<Sample> data3 = GenerateSampleData(0.2, 0.2, 0.2, 0.2).SubList(0, 1000);
            AnomalyLikelihoodMetrics metrics3 = _an.UpdateAnomalyLikelihoods(data3, metrics2.GetParams());
            Assert.AreEqual(metrics3.GetLikelihoods().Length, data3.Count);
            Assert.AreEqual(metrics3.GetAvgRecordList().Count, data3.Count);
            Assert.IsTrue(_an.IsValidEstimatorParams(metrics3.GetParams()));

            // The new running total should be different
            Assert.AreNotEqual(metrics1.GetAvgRecordList().Total, metrics3.GetAvgRecordList().Total);
            Assert.AreNotEqual(metrics2.GetAvgRecordList().Total, metrics3.GetAvgRecordList().Total);

            // We should have a small number of samples where likelihood is < 0.02
            conditionCount = ArrayUtils.Where(metrics3.GetLikelihoods(), d => d < 0.01).Length;
            Assert.IsTrue(conditionCount >= 1);
            Assert.IsTrue(conditionCount <= 100);

            //------------------------------------------
            // Step 4. Validate that sending data incrementally is the same as sending
            // in one batch
            List<Sample> allData = new List<Sample>();
            allData.AddRange(data1);
            allData.AddRange(data2);
            allData.AddRange(data3);
            Anomaly.AveragedAnomalyRecordList recordList = _an.AnomalyScoreMovingAverage(allData, 5);

            double[] historicalValuesAll = new double[recordList.HistoricalValues.Count];

            for (int j = 0; j < recordList.HistoricalValues.Count; j++)
            {
                historicalValuesAll[j] = recordList.HistoricalValues[j];
            }
            
            Assert.AreEqual(ArrayUtils.Sum(historicalValuesAll), ArrayUtils.Sum(
                metrics3.GetParams().MovingAverage().GetSlidingWindow().ToArray()), 0);

            Assert.AreEqual(recordList.Total, metrics3.GetParams().MovingAverage().GetTotal(), 0);
        }

        /// <summary>
        /// This calls estimateAnomalyLikelihoods with flat distributions and ensures things don't crash.
        /// </summary>
        [TestMethod]
        public void TestFlatAnomalyScores()
        {
            // Generate an estimate using fake distribution of anomaly scores.
            List<Sample> data1 = GenerateSampleData(42, 1e-10, 0.2, 0.2).SubList(0, 1000);

            AnomalyLikelihoodMetrics metrics1 = _an.EstimateAnomalyLikelihoods(data1, 10, 0);
            Assert.AreEqual(metrics1.GetLikelihoods().Length, data1.Count);
            Assert.AreEqual(metrics1.GetAvgRecordList().Count, data1.Count);
            Assert.IsTrue(_an.IsValidEstimatorParams(metrics1.GetParams()));

            // Check that the estimated mean is correct
            Statistic stats = metrics1.GetParams().Distribution();
            AssertWithinEpsilon(stats.Mean, data1[0].Score);

            // If you deviate from the mean, you should get probability 0
            // Test this by sending in just slightly different values.
            List<Sample> data2 = GenerateSampleData(42.5, 1e-10, 0.2, 0.2);
            AnomalyLikelihoodMetrics metrics2 = _an.UpdateAnomalyLikelihoods(data2.SubList(0, 10), metrics1.GetParams());
            // The likelihoods should go to zero very quickly
            Assert.IsTrue(ArrayUtils.Sum(metrics2.GetLikelihoods()) <= 0.01);

            // Test edge case where anomaly scores are very close to 0
            // In this case we don't let likelihood to get too low. An average
            // anomaly score of 0.1 should be essentially zero, but an average
            // of 0.04 should be higher
            List<Sample> data3 = GenerateSampleData(0.01, 1e-6, 0.2, 0.2);
            AnomalyLikelihoodMetrics metrics3 = _an.EstimateAnomalyLikelihoods(data3.SubList(0, 100), 10, 0);

            List<Sample> data4 = GenerateSampleData(0.1, 1e-6, 0.2, 0.2);
            AnomalyLikelihoodMetrics metrics4 = _an.UpdateAnomalyLikelihoods(data4.SubList(0, 20), metrics3.GetParams());

            // Average of 0.1 should go to zero
            double[] likelihoods4 = Arrays.CopyOfRange(metrics4.GetLikelihoods(), 10, metrics4.GetLikelihoods().Length);
            Assert.IsTrue(ArrayUtils.Average(likelihoods4) <= 0.002);

            List<Sample> data5 = GenerateSampleData(0.05, 1e-6, 0.2, 0.2);
            AnomalyLikelihoodMetrics metrics5 = _an.UpdateAnomalyLikelihoods(data5.SubList(0, 20), metrics4.GetParams());

            // The likelihoods should be low but not near zero
            double[] likelihoods5 = Arrays.CopyOfRange(metrics5.GetLikelihoods(), 10, metrics4.GetLikelihoods().Length);
            Assert.IsTrue(ArrayUtils.Average(likelihoods5) <= 0.28);
        }

        /// <summary>
        /// This calls estimateAnomalyLikelihoods with flat metric values. In this case
        /// we should use the null distribution, which gets reasonably high likelihood
        /// for everything.
        /// </summary>
        [TestMethod]
        public void TestFlatMetricScores()
        {
            // Generate samples with very flat metric values
            List<Sample> data1 = GenerateSampleData(0.2, 0.2, 42, 1e-10).SubList(0, 1000);

            // Check that we do indeed get reasonable likelihood values
            AnomalyLikelihoodMetrics metrics1 = _an.EstimateAnomalyLikelihoods(data1, 10, 0);
            Assert.AreEqual(metrics1.GetLikelihoods().Length, data1.Count);
            double[] likelihoods = metrics1.GetLikelihoods();
            Assert.IsTrue(ArrayUtils.Sum(likelihoods) >= 0.4 * likelihoods.Length);
            metrics1.GetParams().Distribution().Equals(_an.NullDistribution());
            Assert.IsTrue(metrics1.GetParams().Distribution().Equals(_an.NullDistribution()));
        }

        /// <summary>
        /// This calls estimateAnomalyLikelihoods and updateAnomalyLikelihoods with one or no scores.
        /// </summary>
        [TestMethod]
        public void TestVeryFewScores()
        {
            // Generate an estimate using two data points
            List<Sample> data1 = GenerateSampleData(42, 1e-10, 0.2, 0.2).SubList(0, 2);
            AnomalyLikelihoodMetrics metrics1 = _an.EstimateAnomalyLikelihoods(data1, 10, 0);
            Assert.IsTrue(_an.IsValidEstimatorParams(metrics1.GetParams()));

            // Check that the estimated mean is that value
            AssertWithinEpsilon(metrics1.GetParams().Distribution().Mean, data1[0].Score);

            // Can't generate an estimate using no data points
            List<Sample> test = new List<Sample>();
            try
            {
                _an.EstimateAnomalyLikelihoods(test, 10, 0);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message.Equals("Must have at least one anomaly score."));
            }

            // Can't update without scores
            try
            {
                _an.UpdateAnomalyLikelihoods(test, metrics1.GetParams());
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.Message.Equals("Must have at least one anomaly score."));
            }
        }
        /// <summary>
        /// NOTE: Not a valid test in java. Remnant of Python ability to substitute types, so we 
        /// just do a simple test
        /// </summary>
        [TestMethod]
        public void TestFilterLikelihoodsInputType()
        {
            double[] l2 = _an.FilterLikelihoods(new double[] { 0.0, 0.0, 0.3, 0.3, 0.5 });
            double[] filtered = new double[] { 0.0, 0.001, 0.3, 0.3, 0.5 };
            int i = 0;
            foreach (double d in l2)
            {
                Assert.AreEqual(d, filtered[i++], 0.01);
            }
        }

        /// <summary>
        /// <pre>
        /// Tests _filterLikelihoods function for several cases:
        /// i.Likelihood goes straight to redzone, skipping over yellowzone, repeats
        /// ii.Case(i) with different values, and numpy array instead of float list
        /// iii.A scenario where changing the redzone from four to five 9s should
        ///      filter differently
        /// </pre>
        /// </summary>
        [TestMethod]
        public void TestFilterLikelihoods()
        {
            double redThreshold = 0.9999;
            double yellowThreshold = 0.999;

            // Case (i): values at indices 1 and 7 should be filtered to yellow zone
            double[] l = { 1.0, 1.0, 0.9, 0.8, 0.5, 0.4, 1.0, 1.0, 0.6, 0.0 };
            l = l.Select(d=> 1.0d - d).ToArray();
            double[] l2a = Arrays.CopyOf(l, l.Length);
            l2a[1] = 1 - yellowThreshold;
            l2a[7] = 1 - yellowThreshold;
            double[] l3a = _an.FilterLikelihoods(l, redThreshold, yellowThreshold);

            int successIndexes =
                ArrayUtils.Range(0, l.Length).Select(i=> { Assert.AreEqual(l2a[i], l3a[i], 0.01); return 1; }).Sum();
            Assert.AreEqual(successIndexes, l.Length);

            // Case (ii): values at indices 1-10 should be filtered to yellow zone
            l = new double[]
            {
                0.999978229, 0.999978229, 0.999999897, 1, 1, 1, 1,
                0.999999994, 0.999999966, 0.999999966, 0.999994331,
                0.999516576, 0.99744487
            };
            l = l.Select(d=> 1.0d - d).ToArray();
            double[] l2b = Arrays.CopyOf(l, l.Length);
            ArrayUtils.SetIndexesTo(l2b, ArrayUtils.Range(1, 11), 1 - yellowThreshold);
            double[] l3b = _an.FilterLikelihoods(l);

            successIndexes =
                ArrayUtils.Range(0, l.Length).Select(i=> { Assert.AreEqual(l2b[i], l3b[i], 0.01); return 1; }).Sum();
            Assert.AreEqual(successIndexes, l.Length);

            // Case (iii): redThreshold difference should be at index 2
            l = new double[]
            {
                0.999968329, 0.999999897, 1, 1, 1,
                1, 0.999999994, 0.999999966, 0.999999966,
                0.999994331, 0.999516576, 0.99744487
            };
            l = l.Select(d=> 1.0d - d).ToArray();
            double[] l2a2 = Arrays.CopyOf(l, l.Length);
            double[] l2b2 = Arrays.CopyOf(l, l.Length);
            ArrayUtils.SetIndexesTo(l2a2, ArrayUtils.Range(1, 10), 1 - yellowThreshold);
            ArrayUtils.SetIndexesTo(l2b2, ArrayUtils.Range(2, 10), 1 - yellowThreshold);
            double[] l3a2 = _an.FilterLikelihoods(l);
            double[] l3b2 = _an.FilterLikelihoods(l, 0.99999, yellowThreshold);

            successIndexes =
                ArrayUtils.Range(0, l2a2.Length).Select(i=> { Assert.AreEqual(l2a2[i], l3a2[i], 0.01); return 1; }).Sum();
            Assert.AreEqual(successIndexes, l2a2.Length);

            successIndexes =
                ArrayUtils.Range(0, l2b2.Length).Select(i=> { Assert.AreEqual(l2b2[i], l3b2[i], 0.01); return 1; }).Sum();
            Assert.AreEqual(successIndexes, l2b2.Length);
        }

        [TestMethod]
        public void TestAnomalyParamsToJson()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_DIST, new Statistic(0.38423985556178486, 0.009520602474199693, 0.09757357467162762));
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE, new double[] { 0.460172163, 0.344578258, 0.344578258, 0.382088578, 0.460172163 });
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MVG_AVG, new MovingAverage(
                new List<double>(
                    new double[] { 0.09528343752779542, 0.5432072190186226, 0.9062454498382395, 0.44264021533137254, -0.009955323005220784 }),
                1.9774209987108093, // total 
                5                   // window size
                ));

            AnomalyLikelihood.AnomalyParams @params = new AnomalyLikelihood.AnomalyParams(p);

            string expected = "{\"distribution\":{\"mean\":0.38423985556178486,\"variance\":0.0095206024741996929,\"stdev\":0.097573574671627625},\"historicalLikelihoods\":[0.460172163,0.344578258,0.344578258,0.382088578,0.460172163],\"movingAverage\":{\"windowSize\":5,\"historicalValues\":[0.095283437527795417,0.54320721901862257,0.90624544983823951,0.44264021533137254,-0.0099553230052207842],\"total\":1.9774209987108093}}";

            Assert.AreEqual(expected, @params.ToJson(false));
        }
    }
}