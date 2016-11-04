using System;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Encoders
{
 [Serializable]
    public class SDRPassThroughEncoder : PassThroughEncoder<int[]>
    {
        protected new static readonly ILog LOGGER = LogManager.GetLogger(typeof(SDRPassThroughEncoder));

        protected SDRPassThroughEncoder() { }

        public SDRPassThroughEncoder(int outputWidth, int outputBitsOnCount)
                : base(outputWidth, outputBitsOnCount)
        {


            LOGGER.Info(string.Format
                ("Building new SDRPassThroughEncoder overriding instance, outputWidth: {0} outputBitsOnCount: {1}",
                outputWidth, outputBitsOnCount));
        }

        /**
         * Returns a builder for building SDRPassThroughEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code SDRPassThroughEncoder.Builder}
         */
        public static IBuilder GetSptBuilder()
        {
            return new Builder();
        }

        /**
         * Check for length the same and copy input into output
         * If outputBitsOnCount (w) set, throw error if not true
         * @param <T>
         *
         * @param input
         * @param output
         */
        public override void EncodeIntoArray(int[] input, int[] output)
        {
            if (LOGGER.IsDebugEnabled)
            {
                LOGGER.Debug(string.Format("encodeIntoArray: input: {0} \nOutput: {1} ", Arrays.ToString(input), Arrays.ToString(output)));
            }

            Array.Copy(input, 0, output, 0, output.Length);
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            EncodeIntoArray((int[]) o, tempArray);
        }

        /**
         * Returns a {@link Encoder.Builder} for constructing {@link SDRPassThroughEncoder}s
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
                encoder = new SDRPassThroughEncoder();
                w = n;

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                ((SDRPassThroughEncoder)encoder).Init();

                return (SDRPassThroughEncoder)encoder;
            }
        }
    }
}