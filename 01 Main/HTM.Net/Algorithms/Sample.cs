using System;
using System.Text;

namespace HTM.Net.Algorithms
{
    /**
 * A sample data point or record consisting of a timestamp, value, and score.
 * This class is used as an input value to methods in the {@link AnomalyLikelihood}
 * class.
 */
    public class Sample
    {
        public readonly DateTime date;
        /** Same thing as average */
        public readonly double score;
        /** Original value */
        public readonly double value;

        public Sample(DateTime timeStamp, double value, double score)
        {
            if (timeStamp == null)
            {
                throw new ArgumentException("Sample must have a valid date");
            }
            this.date = timeStamp;
            this.value = value;
            this.score = score;
        }

        /**
         * Returns a {@link DateTime} object representing the internal timestamp
         * @return
         */
        public DateTime TimeStamp()
        {
            return date;
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return new StringBuilder(TimeStamp().ToString()).Append(", value: ").
                Append(value).Append(", metric: ").Append(score).ToString();
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((date == null) ? 0 : date.GetHashCode());
            long temp;
            temp = BitConverter.DoubleToInt64Bits(score);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(value);
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
            Sample other = (Sample)obj;
            if (date == null)
            {
                if (other.date != null)
                    return false;
            }
            else if (!date.Equals(other.date))
                return false;
            if (BitConverter.DoubleToInt64Bits(score) != BitConverter.DoubleToInt64Bits(other.score))
                return false;
            if (BitConverter.DoubleToInt64Bits(value) != BitConverter.DoubleToInt64Bits(other.value))
                return false;
            return true;
        }


    }
}
