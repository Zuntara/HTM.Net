using HTM.Net.Util;

namespace HTM.Net.Tests.Algorithms
{
    public class SampleDistribution
    {
        private double mean;
        private double variance;
        private int size;

        public SampleDistribution(double mean, double variance, int size)
        {
            this.mean = mean;
            this.variance = variance;
            this.size = size;
        }

        /**
         * Returns an array of normally distributed values with the configured 
         * mean and variance.
         * 
         * @return
         */
        public double[] GetSample(IRandom random)
        {
            double[] sample = new double[size];
            for (int i = 0; i < size; i++)
            {
                sample[i] = GetGaussian(random, mean, variance);
            }

            return sample;
        }

        /**
         * Return the next distributed value with the specified
         * mean and variance.
         * 
         * @param aMean         the centered location
         * @param aVariance     the 
         * @return
         */
        private double GetGaussian(IRandom random, double mean, double variance)
        {
            return mean + random.NextGaussian() * variance;
        }
    }
}