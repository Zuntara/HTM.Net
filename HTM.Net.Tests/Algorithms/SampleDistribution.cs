using HTM.Net.Util;

namespace HTM.Net.Tests.Algorithms
{
    public class SampleDistribution
    {
        private double _mean, _variance;
        private int _size;

        public SampleDistribution(double mean, double variance, int size)
        {
            _mean = mean;
            _variance = variance;
            _size = size;
        }
        /// <summary>
        /// Returns an array of normally distributed values with the configured mean and variance.
        /// </summary>
        /// <param name="random"></param>
        /// <returns></returns>
        public double[] GetSample(IRandom random)
        {
            double[] sample = new double[_size];
            for (int i = 0; i < _size; i++)
            {
                sample[i] = GetGaussian(random, _mean, _variance);
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