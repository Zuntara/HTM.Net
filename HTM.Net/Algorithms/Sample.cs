using System;
using System.Globalization;
using System.Text;

namespace HTM.Net.Algorithms
{
    /// <summary>
    /// A sample data point or record consisting of a timestamp, value, and score.
    /// This class is used as an input value to methods in the <see cref="AnomalyLikelihood"/> class.
    /// </summary>
    public class Sample
    {
        public readonly DateTime? Date;
        /** Same thing as average */
        public readonly double Score;
        /** Original value */
        public readonly double Value;

        public Sample(DateTime timeStamp, double value, double score)
        {
            if (timeStamp == null)
            {
                throw new ArgumentException("Sample must have a valid date");
            }
            Date = timeStamp;
            Value = value;
            Score = score;
        }

        /**
         * Returns a {@link DateTime} object representing the internal timestamp
         * @return
         */
        public DateTime? TimeStamp()
        {
            return Date;
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return new StringBuilder(TimeStamp().GetValueOrDefault().ToString(CultureInfo.InvariantCulture)).Append(", value: ").
                Append(Value).Append(", metric: ").Append(Score).ToString();
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((Date == null) ? 0 : Date.GetHashCode());
            long temp;
            temp = BitConverter.DoubleToInt64Bits(Score);
            result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(Value);
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
            if (Date == null)
            {
                if (other.Date != null)
                    return false;
            }
            else if (!Date.Equals(other.Date))
                return false;
            if (BitConverter.DoubleToInt64Bits(Score) != BitConverter.DoubleToInt64Bits(other.Score))
                return false;
            if (BitConverter.DoubleToInt64Bits(Value) != BitConverter.DoubleToInt64Bits(other.Value))
                return false;
            return true;
        }


    }
}
