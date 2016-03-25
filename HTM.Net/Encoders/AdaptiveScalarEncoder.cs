using System;
using System.Collections.Generic;
using System.Diagnostics;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Encoders
{
    /**
 * This is an implementation of the scalar encoder that adapts the min and
 * max of the scalar encoder dynamically. This is essential to the streaming
 * model of the online prediction framework.
 * 
 * Initialization of an adaptive encoder using resolution or radius is not
 * supported; it must be initialized with n. This n is kept constant while
 * the min and max of the encoder changes.
 * 
 * The adaptive encoder must be have periodic set to false.
 * 
 * The adaptive encoder may be initialized with a minval and maxval or with
 * `None` for each of these. In the latter case, the min and max are set as
 * the 1st and 99th percentile over a window of the past 100 records.
 * 
 * *Note:** the sliding window may record duplicates of the values in the
 * data set, and therefore does not reflect the statistical distribution of
 * the input data and may not be used to calculate the median, mean etc.
 */
    public class AdaptiveScalarEncoder : ScalarEncoder
    {


        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(AdaptiveScalarEncoder));

        private int recordNum = 0;
        private bool learningEnabled = true;
        private double[] slidingWindow = new double[0];
        private int windowSize = 300;
        //public double? bucketValues;

        /**
         * {@inheritDoc}
         *
         * @see org.numenta.nupic.encoders.ScalarEncoder#init()
         */
        public override void Init()
        {
            this.SetPeriodic(false);
            base.Init();
        }

        /**
         * {@inheritDoc}
         *
         * @see org.numenta.nupic.encoders.ScalarEncoder#initEncoder(int, double,
         * double, int, double, double)
         */
        public override void InitEncoder(int w, double minVal, double maxVal, int n,
                double radius, double resolution)
        {
            this.SetPeriodic(false);
            this.encLearningEnabled = true;
            if (this.periodic)
            {
                throw new InvalidOperationException("Adaptive scalar encoder does not encode periodic inputs");
            }
            Debug.Assert(n != 0);
            base.InitEncoder(w, minVal, maxVal, n, radius, resolution);
        }

        /**
         * Constructs a new {@code AdaptiveScalarEncoder}
         */
        internal AdaptiveScalarEncoder()
        {
        }

        /**
         * Returns a builder for building AdaptiveScalarEncoder. This builder may be
         * reused to produce multiple builders
         *
         * @return a {@code AdaptiveScalarEncoder.Builder}
         */
        public static AdaptiveScalarEncoder.Builder GetAdaptiveBuilder()
        {
            return new Builder();
        }

        /**
         * Constructs a new {@link Builder} suitable for constructing
         * {@code AdaptiveScalarEncoder}s.
         */
        public new class Builder : BuilderBase
        {
            internal Builder() { }

            public override IEncoder Build()
            {
                encoder = new AdaptiveScalarEncoder();
                base.Build();
                ((AdaptiveScalarEncoder)encoder).Init();
                return (AdaptiveScalarEncoder)encoder;
            }
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            if (this.GetMinVal() == 0 || this.GetMaxVal() == 0)
            {
                List<EncoderResult> res = new List<EncoderResult>();
                int[] enArray = new int[this.GetN()];
                Arrays.Fill(enArray, 0);
                EncoderResult ecResult = new EncoderResult(0, 0, enArray);
                res.Add(ecResult);
                return res;
            }
            return base.TopDownCompute(encoded);
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(double input, int[] output)
        {
            this.recordNum += 1;
            bool learn = false;
            if (!this.encLearningEnabled)
            {
                learn = true;
            }
            if (input == AdaptiveScalarEncoder.SENTINEL_VALUE_FOR_MISSING_DATA)
            {
                Arrays.Fill(output, 0);
            }
            else if (!double.IsNaN(input))
            {
                this.SetMinAndMax(input, learn);
            }
            base.EncodeIntoArray(input, output);
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            EncodeIntoArray((double) o, tempArray);
        }

        private void SetMinAndMax(double input, bool learn)
        {
            if (slidingWindow.Length >= windowSize)
            {
                slidingWindow = DeleteItem(slidingWindow, 0);
            }
            slidingWindow = AppendItem(slidingWindow, input);

            if (this.minVal == this.maxVal)
            {
                this.minVal = input;
                this.maxVal = input + 1;
                SetEncoderParams();
            }
            else {
                double[] sorted = Arrays.CopyOf(slidingWindow, slidingWindow.Length);
                Array.Sort(sorted);
                double minOverWindow = sorted[0];
                double maxOverWindow = sorted[sorted.Length - 1];
                if (minOverWindow < this.minVal)
                {
                    LOGGER.Debug(string.Format("Input {0}={1} smaller than minVal {2}. Adjusting minVal to {3}",
                                this.name, input, this.minVal, minOverWindow));
                    this.minVal = minOverWindow;
                    SetEncoderParams();
                }
                if (maxOverWindow > this.maxVal)
                {
                    LOGGER.Debug(string.Format("Input {0}={1} greater than maxVal {2}. Adjusting maxVal to {3}",
                            this.name, input, this.minVal, minOverWindow));
                    this.maxVal = maxOverWindow;
                    SetEncoderParams();
                }
            }
        }

        private void SetEncoderParams()
        {
            this.rangeInternal = this.maxVal - this.minVal;
            this.resolution = this.rangeInternal / (this.n - this.w);
            this.radius = this.w * this.resolution;
            this.range = this.rangeInternal + this.resolution;
            this.nInternal = this.n - 2 * this.padding;
            this.bucketValues = null;
        }

        private double[] AppendItem(double[] a, double input)
        {
            a = Arrays.CopyOf(a, a.Length + 1);
            a[a.Length - 1] = input;
            return a;
        }

        private double[] DeleteItem(double[] a, int i)
        {
            a = Arrays.CopyOfRange(a, 1, a.Length - 1);
            return a;
        }

        /**
         * {@inheritDoc}
         */
        public override int[] GetBucketIndices(string inputString)
        {
            double input = double.Parse(inputString);
            return CalculateBucketIndices(input);
        }

        /**
         * {@inheritDoc}
         */
        public override int[] GetBucketIndices(double input)
        {
            return CalculateBucketIndices(input);
        }

        private int[] CalculateBucketIndices(double? input)
        {
            this.recordNum += 1;
            bool learn = false;
            if (!this.encLearningEnabled)
            {
                learn = true;
            }
            if ((double.IsNaN(input.GetValueOrDefault())) && (input.HasValue))
            {
                input = AdaptiveScalarEncoder.SENTINEL_VALUE_FOR_MISSING_DATA;
            }
            if (input == AdaptiveScalarEncoder.SENTINEL_VALUE_FOR_MISSING_DATA)
            {
                return new int[this.n];
            }
            else {
                this.SetMinAndMax(input.GetValueOrDefault(), learn);
            }
            return base.GetBucketIndices(input.GetValueOrDefault());
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            if (this.minVal == 0 || this.maxVal == 0)
            {
                int[] initialBuckets = new int[this.n];
                Arrays.Fill(initialBuckets, 0);
                List<EncoderResult> encoderResultList = new List<EncoderResult>();
                EncoderResult encoderResult = new EncoderResult(0, 0, initialBuckets);
                encoderResultList.Add(encoderResult);
                return encoderResultList;
            }
            return base.GetBucketInfo(buckets);
        }
    }
}
