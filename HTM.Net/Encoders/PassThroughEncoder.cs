using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
 * Pass an encoded SDR straight to the model.
 * Each encoding is an SDR in which w out of n bits are turned on.
 *
 * @author wilsondy (from Python original)
 */
 [Serializable]
    public class PassThroughEncoder<T> : Encoder<T>
    {

        [NonSerialized]
        protected readonly ILog LOGGER = LogManager.GetLogger(typeof(PassThroughEncoder<T>));

        protected PassThroughEncoder() { }

        public PassThroughEncoder(int outputWidth, int outputBitsOnCount)
        {
            base.SetW(outputBitsOnCount);
            base.SetN(outputWidth);
            base.SetForced(false);

            LOGGER.Info(string.Format("Building new PassThroughEncoder instance, outputWidth: {0} outputBitsOnCount: {1}", outputWidth, outputBitsOnCount));
        }

        /**
         * Returns a builder for building PassThroughEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code PassThroughEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        public void Init()
        {
            SetForced(false);
        }

        /**
         * Does a bitwise compare of the two bitmaps and returns a fractional
         * value between 0 and 1 of how similar they are.
         * 1 => identical
         * 0 => no overlapping bits
         * IGNORES difference in length (only compares bits of shorter list)  e..g 11 and 1100101010 are "identical"
         * @see org.numenta.nupic.encoders.Encoder#closenessScores(gnu.trove.list.TDoubleList, gnu.trove.list.TDoubleList, boolean)
         */
        public override List<double> ClosenessScores(List<double> expValues, List<double> actValues, bool fractional)
        {
            List<double> result = new List<double>();

            double ratio = 1.0d;
            double expectedSum = expValues.Sum();
            double actualSum = actValues.Sum();

            if (actualSum > expectedSum)
            {
                double diff = actualSum - expectedSum;
                if (diff < expectedSum)
                    ratio = 1 - diff / expectedSum;
                else
                    ratio = 1 / diff;
            }

            int[] expectedInts = ArrayUtils.ToIntArray(expValues.ToArray());
            int[] actualInts = ArrayUtils.ToIntArray(actValues.ToArray());

            int[] overlap = ArrayUtils.And(expectedInts, actualInts);

            int overlapSum = ArrayUtils.Sum(overlap);
            double r = 0.0;
            if (expectedSum != 0)
                r = overlapSum / expectedSum;
            r = r * ratio;

            if (LOGGER.IsDebugEnabled)
            {
                LOGGER.Debug(string.Format("closenessScores for expValues: {0} and actValues: {1} is: {2}", Arrays.ToString(expectedInts), actualInts, r));
            }

            result.Add(r);
            return result;
        }

        public override int GetWidth()
        {
            return n;
        }

        public override bool IsDelta()
        {
            return false;
        }

        /**
         * Check for length the same and copy input into output
         * If outputBitsOnCount (w) set, throw error if not true
         *
         * @param input
         * @param output
         */
        public override void EncodeIntoArray(T t, int[] output)
        {
            int[] input = TypeConverter.Convert<int[]>(t);
            if (input.Length != output.Length)
                throw new ArgumentException(string.Format("Different input ({0}) and output ({1}) sizes", input.Length, output.Length));
            if (ArrayUtils.Sum(input) != w)
                throw new ArgumentException(string.Format("Input has {0} bits but w was set to {1}.", ArrayUtils.Sum(input), w));

            Array.Copy(input, 0, output, 0, input.Length);
            if (LOGGER.IsDebugEnabled)
            {
                LOGGER.Debug(string.Format("encodeIntoArray: Input: {0} \nOutput: {1} ", Arrays.ToString(input), Arrays.ToString(output)));
            }
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            EncodeIntoArray((T)o, tempArray);
        }

        /**
         * Not much real work to do here as this concept doesn't really apply.
         */
        public override Tuple Decode(int[] encoded, string parentFieldName)
        {
            //TODO: these methods should be properly implemented (this comment in Python)
            string fieldName = this.name;
            if (parentFieldName != null && parentFieldName.Length > 0 && LOGGER.IsDebugEnabled)
                LOGGER.Debug(string.Format("Decoding Field: {0}.{1}", parentFieldName, this.name));

            List<MinMax> ranges = new List<MinMax>();
            ranges.Add(new MinMax(0, 0));
            RangeList inner = new RangeList(ranges, "input");
            Map<string, RangeList> fieldsDict = new Map<string, RangeList>();
            fieldsDict.Add(fieldName, inner);

            return new DecodeResult(fieldsDict, new List<string> { fieldName });
        }

        public override void SetLearning(bool learningEnabled)
        {
            //NOOP
        }

        public override List<S> GetBucketValues<S>(Type returnType)
        {
            return null;
        }

        /**
         * Returns a {@link Encoder.Builder} for constructing {@link PassThroughEncoder}s
         * <p/>
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         */
        public class Builder : BuilderBase
        {
            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new PassThroughEncoder<int[]>();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                ((PassThroughEncoder<int[]>)encoder).Init();

                return (PassThroughEncoder<int[]>)encoder;
            }
        }
    }
}
