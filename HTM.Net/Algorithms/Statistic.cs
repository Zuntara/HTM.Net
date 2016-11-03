using System;
using HTM.Net.Model;
using HTM.Net.Util;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Algorithms
{
    /**
 * Container to hold a specific calculation for a statistical data point.
 * 
 * Follows the form:
 * <pre>
 * {
 *    "distribution":               # describes the distribution
 *     {
 *        "name": STRING,           # name of the distribution, such as 'normal'
 *        "mean": SCALAR,           # mean of the distribution
 *        "variance": SCALAR,       # variance of the distribution
 *
 *        # There may also be some keys that are specific to the distribution
 *     }
 * </pre>
 * @author David Ray
 */
 [Serializable]
    public class Statistic : Persistable<Statistic>
    {
        public readonly double mean;
        public readonly double variance;
        public readonly double stdev;
        public readonly NamedTuple entries;

        public Statistic(double mean, double variance, double stdev)
        {
            this.mean = mean;
            this.variance = variance;
            this.stdev = stdev;

            this.entries = new NamedTuple(new string[] { "mean", "variance", "stdev" }, mean, variance, stdev);
        }

        /**
         * Creates and returns a JSON ObjectNode containing this Statistic's data.
         * 
         * @param factory
         * @return
         */
        public JObject ToJson()
        {
            JObject distribution = new JObject();
            distribution.Add("mean", mean);
            distribution.Add("variance", variance);
            distribution.Add("stdev", stdev);

            return distribution;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            long temp = BitConverter.DoubleToInt64Bits(mean);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(stdev);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(variance);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Statistic other = (Statistic)obj;
            if (BitConverter.DoubleToInt64Bits(mean) != BitConverter.DoubleToInt64Bits(other.mean))
                return false;
            if (BitConverter.DoubleToInt64Bits(stdev) != BitConverter.DoubleToInt64Bits(other.stdev))
                return false;
            if (BitConverter.DoubleToInt64Bits(variance) != BitConverter.DoubleToInt64Bits(other.variance))
                return false;
            return true;
        }
    }
}
