using System.Globalization;
using System.Text;
using HTM.Net.Model;

namespace HTM.Net.Util
{
    /// <summary>
    /// Holds two values, a min and a max. Can later be developed to
    /// employ operations on those values (i.e. distance etc.)
    /// </summary>
    public class MinMax : Persistable<MinMax>
    {
        private readonly double _min;
        private readonly double _max;

        /**
         * Constructs a new {@code MinMax} instance
         */
        public MinMax() { }
        /**
         * Constructs a new {@code MinMax} instance
         * 
         * @param min	the minimum or lower bound
         * @param max	the maximum or upper bound
         */
        public MinMax(double min, double max)
        {
            _min = min;
            _max = max;
        }

        /**
         * Returns the configured min value
         */
        public double Min()
        {
            return _min;
        }

        /**
         * Returns the configured max value
         */
        public double Max()
        {
            return _max;
        }

        public override string ToString()
        {
            return new StringBuilder().Append(_min.ToString("0.0",NumberFormatInfo.InvariantInfo)).
                Append(", ").Append(_max.ToString("0.0", NumberFormatInfo.InvariantInfo)).ToString();
        }
    }
}