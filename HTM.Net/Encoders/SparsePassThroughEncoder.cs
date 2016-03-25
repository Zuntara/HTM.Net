using System;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Encoders
{
    /**
 * Sparse Pass Through Encoder
 * Convert a bitmap encoded as array indices to an SDR
 * Each encoding is an SDR in which w out of n bits are turned on.
 * The input should be an array or string of indices to turn on
 * Note: the value for n must equal input length * w
 * i.e. for n=8 w=1 [0,2,5] => 101001000
 * or for n=8 w=1 "0,2,5" => 101001000
 * i.e. for n=24 w=3 [0,2,5] => 111000111000000111000000000
 * or for n=24 w=3 "0,2,5" => 111000111000000111000000000
 *
 * @author wilsondy (from Python original)
 */
    public class SparsePassThroughEncoder : PassThroughEncoder<int[]>
    {
        private SparsePassThroughEncoder()
        {
            
        }

        private new static readonly ILog LOGGER = LogManager.GetLogger(typeof(SparsePassThroughEncoder));

        public SparsePassThroughEncoder(int outputWidth, int outputBitsOnCount)
                : base(outputWidth, outputBitsOnCount)
        {

            LOGGER.Info(string.Format("Building new SparsePassThroughEncoder instance, outputWidth: {0} outputBitsOnCount: {1}", outputWidth, outputBitsOnCount));
        }

        /**
         * Returns a builder for building SparsePassThroughEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code SparsePassThroughEncoder.Builder}
         */
        public static IBuilder GetSparseBuilder()
        {
            return new Builder();
        }

        /**
         * Convert the array of indices to a bit array and then pass to parent.
         */
        public override void EncodeIntoArray(int[] input, int[] output)
        {

            int[] denseInput = new int[output.Length];
            foreach (int i in input)
            {
                if (i > denseInput.Length)
                    throw new ArgumentException(string.Format("Output bit count set too low, need at least {0} bits", i));
                denseInput[i] = 1;
            }
            base.EncodeIntoArray(denseInput, output);
            LOGGER.Debug(string.Format("Input: {0} \nOutput: {1} \n", Arrays.ToString(input), Arrays.ToString(output)));
        }

        /**
         * Returns a {@link Encoder.Builder} for constructing {@link SparsePassThroughEncoder}s
         * <p/>
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         */
        public new class Builder : BuilderBase
        {
            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new SparsePassThroughEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                ((SparsePassThroughEncoder)encoder).Init();

                return (SparsePassThroughEncoder)encoder;
            }
        }
    }
}