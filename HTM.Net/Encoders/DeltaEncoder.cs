using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Encoders
{
 [Serializable]
    public class DeltaEncoder : AdaptiveScalarEncoder
    {
        public double PrevAbsolute { get; set; }
        public double PrevDelta { get; set; }
        public bool StateLock { get; set; }

        /**
         * Returns a builder for building DeltaEncoder. This builder may be
         * reused to produce multiple builders
         * 
         * @return a {@code DeltaEncoder.Builder}
         */
        public static IBuilder GetDeltaBuilder()
        {
            return new Builder();
        }

        /**
         * Builder pattern for constructing a {@code DeltaEncoder}
         * 
         */
        public new class Builder : BuilderBase
        {
            internal Builder() { }

            public override IEncoder Build()
            {
                encoder = new DeltaEncoder();
                base.Build();
                ((DeltaEncoder)encoder).Init();
                return (DeltaEncoder)encoder;
            }
        }

        /**
         * Encodes inputData and puts the encoded value into the output array,
         * which is a 1-D array of length returned by {@link Connections#getW()}.
         *
         * Note: The output array is reused, so clear it before updating it.
         * @param inputData Data to encode. This should be validated by the encoder.
         * @param output 1-D array of same length returned by {@link Connections#getW()}
         */

        public override void EncodeIntoArray(double input, int[] output)
        {
            double delta = 0;
            if (double.IsNaN(input))
            {
                output = new int[n];
                Arrays.Fill(output, 0);
            }
            else {
                if (Math.Abs(PrevAbsolute) < double.Epsilon)
                {
                    PrevAbsolute = input;
                }
                delta = input - PrevAbsolute;
                base.EncodeIntoArray(input, output);
            }
            if (!StateLock)
            {
                PrevAbsolute = input;
                PrevDelta = delta;
            }
        }

        #region Overrides of ScalarEncoder

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            if (!(o is double))
            {
                throw new InvalidOperationException(
                        string.Format("Expected a Double input but got input of type {0}", o));
            }
            EncodeIntoArray((double) o, tempArray);
        }

        #endregion

        /**
         * @return the stateLock
         */
        public bool IsStateLock()
        {
            return StateLock;
        }

        /**
         * @param stateLock the stateLock to Set
         */
        public void SetStateLock(bool stateLock)
        {
            StateLock = stateLock;
        }

        public void SetFieldStats(string fieldName, string[] fieldParameters)
        {
            // TODO Auto-generated method stub
        }

        /**
         * {@inheritDoc}
         */

        public override bool IsDelta()
        {
            return true;
        }

        /**
         * {@inheritDoc}
         * @see org.numenta.nupic.encoders.AdaptiveScalarEncoder#topDownCompute(int[])
         */

        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            if (this.PrevAbsolute == 0 || this.PrevDelta == 0)
            {
                int[] initialBuckets = new int[this.n];
                Arrays.Fill(initialBuckets, 0);
                List<EncoderResult> encoderResultList = new List<EncoderResult>();
                EncoderResult encoderResult = new EncoderResult(0, 0, initialBuckets);
                encoderResultList.Add(encoderResult);
                return encoderResultList;
            }
            List<EncoderResult> erList = base.TopDownCompute(encoded);
            if (this.PrevAbsolute != 0)
            {
                double objVal = (double)(erList[0].GetValue()) + this.PrevAbsolute;
                double objScalar = erList[0].GetScalar() + this.PrevAbsolute;
                List<EncoderResult> encoderResultList = new List<EncoderResult>();
                EncoderResult encoderResult = new EncoderResult(objVal, objScalar, erList[0].GetEncoding());
                encoderResultList.Add(encoderResult);
                return encoderResultList;
            }
            return erList;
        }
    }
}