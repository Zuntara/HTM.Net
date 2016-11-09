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
    public class Statistic : Persistable
    {
        public double Mean { get; }
        public double Variance { get; }
        public double Stdev { get; }
        public NamedTuple Entries { get; set; }

        public Statistic(double mean, double variance, double stdev)
        {
            Mean = mean;
            Variance = variance;
            Stdev = stdev;

            Entries = new NamedTuple(new[] { "mean", "variance", "stdev" }, mean, variance, stdev);
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
            distribution.Add("mean", Mean);
            distribution.Add("variance", Variance);
            distribution.Add("stdev", Stdev);

            return distribution;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            long temp = BitConverter.DoubleToInt64Bits(Mean);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(Stdev);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(Variance);
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
            if (BitConverter.DoubleToInt64Bits(Mean) != BitConverter.DoubleToInt64Bits(other.Mean))
                return false;
            if (BitConverter.DoubleToInt64Bits(Stdev) != BitConverter.DoubleToInt64Bits(other.Stdev))
                return false;
            if (BitConverter.DoubleToInt64Bits(Variance) != BitConverter.DoubleToInt64Bits(other.Variance))
                return false;
            return true;
        }
    }
}
