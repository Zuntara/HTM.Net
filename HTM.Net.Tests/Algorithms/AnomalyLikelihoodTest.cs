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
        private AnomalyLikelihood an;

        [TestInitialize]
        public void Setup()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);
            an = (AnomalyLikelihood)Anomaly.Create(@params);
        }

        /**
         * Given the parameters of a distribution, generate numSamples points from it.
         * This routine is mostly for testing.
         * 
         * @param mean
         * @param variance
         * @return
         */
        public static double[] SampleDistribution(IRandom random, double mean, double variance, int size)
        {
            SampleDistribution sampler = new SampleDistribution(mean, Math.Sqrt(variance), size);
            return sampler.GetSample(random);
        }

        /**
        * Generate 1440 samples of fake metrics data with a particular distribution
        * of anomaly scores and metric values. Here we generate values every minute.
        * 
        * @param mean
        * @param variance
        * @param metricMean
        * @param metricVariance
        * @return
        */
        public static List<Sample> GenerateSampleData(double mean, double variance, double metricMean, double metricVariance)
        {
            List<Sample> retVal = new List<Sample>();

            IRandom random = new MersenneTwister(42);
            double[] samples = SampleDistribution(random, mean, variance, 1440);
            double[] metricValues = SampleDistribution(random, metricMean, metricVariance, 1440);
            foreach (int hour in ArrayUtils.Range(0, 24))
            {
                foreach (int minute in ArrayUtils.Range(0, 60))
                {
                    retVal.Add(
                        new Sample(
                            new DateTime(2013, 2, 2, hour, minute, 0),
                            metricValues[hour * 60 + minute],
                            samples[hour * 60 + minute]
                        )
                    );
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
            AssertWithinEpsilon(an.NormalProbability(0.0, p), 0.5);
            AssertWithinEpsilon(an.NormalProbability(0.3, p), 0.3820885780);
            AssertWithinEpsilon(an.NormalProbability(1.0, p), 0.1587);
            AssertWithinEpsilon(1.0 - an.NormalProbability(1.0, p), an.NormalProbability(-1.0, p));
            AssertWithinEpsilon(an.NormalProbability(-0.3, p), 1.0 - an.NormalProbability(0.3, p));

            // Non standard normal distribution
            // p = {"name": "normal", "mean": 1.0, "variance": 4.0, "stdev": 2.0}
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, 1.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 4.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, 2.0);
            AssertWithinEpsilon(an.NormalProbability(1.0, p), 0.5);
            AssertWithinEpsilon(an.NormalProbability(2.0, p), 0.3085);
            AssertWithinEpsilon(an.NormalProbability(3.0, p), 0.1587);
            AssertWithinEpsilon(an.NormalProbability(3.0, p), 1.0 - an.NormalProbability(-1.0, p));
            AssertWithinEpsilon(an.NormalProbability(0.0, p), 1.0 - an.NormalProbability(2.0, p));

            // Non standard normal distribution
            // p = {"name": "normal", "mean": -2.0, "variance": 0.5, "stdev": math.sqrt(0.5)}
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, -2.0);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 0.5);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, Math.Sqrt(0.5));
            AssertWithinEpsilon(an.NormalProbability(-2.0, p), 0.5);
            AssertWithinEpsilon(an.NormalProbability(-1.5, p), 0.241963652);
            AssertWithinEpsilon(an.NormalProbability(-2.5, p), 1.0 - an.NormalProbability(-1.5, p));
        }

        /**
         * This passes in a known set of data and ensures the estimateNormal
         * function returns the expected results.
         */
        [TestMethod]
        public void TestEstimateNormal()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);

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

            Statistic result = an.EstimateNormal(samples, true);
            Assert.IsTrue(AssertWithinEpsilon(result.mean, 0.3721));
            Assert.IsTrue(AssertWithinEpsilon(result.variance, 0.22294));
            Assert.IsTrue(AssertWithinEpsilon(result.stdev, 0.47216));
        }

        /**
         * Test that sampleDistribution from a generated distribution returns roughly
         * the same parameters.
         */
        [TestMethod]
        public void TestSampleDistribution()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN, 0.5);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE, 0.1);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV, Math.Sqrt(0.1));

            double[] samples = SampleDistribution(new MersenneTwister(), 0.5, 0.1, 1000);

            Statistic np = an.EstimateNormal(samples, true);
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN), np.mean, 0.1));
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE), np.variance, 0.1));
            Assert.IsTrue(AssertWithinEpsilon((double)p.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV), np.stdev, 0.1));
        }

        /**
         * This calls estimateAnomalyLikelihoods to estimate the distribution on fake
         * data and validates the results
         */

        [TestMethod]
        public void TestEstimateAnomalyLikelihoods()
        {
            // Generate an estimate using fake distribution of anomaly scores.
            List<Sample> data = GenerateSampleData(0.2, 0.2, 0.2, 0.2).Take(1000).ToList();

            AnomalyLikelihoodMetrics metrics = an.EstimateAnomalyLikelihoods(data, 10, 0);
            Assert.AreEqual(1000, metrics.GetLikelihoods().Length);
            Assert.AreEqual(1000, metrics.GetAvgRecordList().AveragedRecords.Count);
            Assert.IsTrue(an.IsValidEstimatorParams(metrics.GetParams()));

            // Get the total
            double total = 0;
            foreach (Sample sample in metrics.GetAvgRecordList().AveragedRecords)
            {
                total = total + sample.score;
            }

            // Check that the estimated mean is correct
            Statistic statistic = (Statistic)metrics.GetParams().Distribution();
            Assert.IsTrue(
                AssertWithinEpsilon(
                    statistic.mean, (total / (double)metrics.GetAvgRecordList().AveragedRecords.Count)
                    )
                );

            int count = ArrayUtils.Where(metrics.GetLikelihoods(), d => d < 0.02).Length;
            Assert.IsTrue(count <= 50);
            Assert.IsTrue(count >= 1);
        }
    }
}