using System;
using System.Text;
using HTM.Net.Util;

namespace HTM.Net.Encoders
{
    /**
     * Tuple to represent the results of computations in different forms.
     *
     * @see {@link Encoder}
     */
    public class EncoderResult : Util.Tuple
    {
        /**
         * Constructs a new {@code EncoderResult}
         *
         * @param value    A representation of the encoded value in the same format as the input
         *                 (i.e. float for scalars, string for categories)
         * @param scalar   A representation of the encoded value as a number. All encoded values
         *                 are represented as some form of numeric value before being encoded
         *                 (e.g. for categories, this is the internal index used by the encoder)
         * @param encoding The bit-string representation of the value
         */
        public EncoderResult(object value, int scalar, int[] encoding)
                : base("EncoderResult", value, scalar, encoding)
        {
           //super("EncoderResult", value, scalar, encoding);
        }

        public EncoderResult(object value, double scalar, int[] encoding)
                : base("EncoderResult", value, scalar, encoding)
        {
            //super("EncoderResult", value, scalar, encoding);
        }

        public override string ToString()
        {
            return new StringBuilder("EncoderResult(value=").
                    Append(Item1).Append(", scalar=").Append(Item2).
                    Append(", encoding=").Append(Item3).ToString();
        }

        /**
         * Returns a representation of the encoded value in the same format as the input.
         *
         * @return the encoded value
         */
        public object GetValue()
        {
            return Item2;
        }

        /**
         * Returns the encoded value as a number.
         *
         * @return
         */
        public double GetScalar()
        {
            return (double)Convert.ChangeType(Item3, typeof(double));
        }

        /**
         * Returns the bit-string encoding of the value
         *
         * @return
         */
        public int[] GetEncoding()
        {
            return (int[])Item4;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null)
            {
                return false;
            }
            if (!(obj is EncoderResult))
            {
                return false;
            }
            EncoderResult other = (EncoderResult)obj;
            if (!this.GetScalar().Equals(other.GetScalar()))
            {
                return false;
            }
            if (!this.GetValue().Equals(other.GetValue()))
            {
                return false;
            }
            if (!Arrays.AreEqual(this.GetEncoding(), other.GetEncoding()))
            {
                return false;
            }
            return true;
        }
    }


}