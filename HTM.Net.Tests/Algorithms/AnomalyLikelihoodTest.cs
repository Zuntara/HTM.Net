using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class AnomalyLikelihoodTest
    {
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
    }
}