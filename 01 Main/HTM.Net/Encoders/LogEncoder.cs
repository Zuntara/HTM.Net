using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{

    /**
     * DOCUMENTATION TAKEN DIRECTLY FROM THE PYTHON VERSION:
     *
     * This class wraps the ScalarEncoder class.
     * A Log encoder represents a floating point value on a logarithmic scale.
     * valueToEncode = log10(input)
     *
     *   w -- number of bits to set in output
     *   minval -- minimum input value. must be greater than 0. Lower values are
     *             reset to this value
     *   maxval -- maximum input value (input is strictly less if periodic == True)
     *   periodic -- If true, then the input value "wraps around" such that minval =
     *             maxval For a periodic value, the input must be strictly less than
     *             maxval, otherwise maxval is a true upper bound.
     *
     *   Exactly one of n, radius, resolution must be set. "0" is a special
     *   value that means "not set".
     *   n -- number of bits in the representation (must be > w)
     *   radius -- inputs separated by more than this distance in log space will have
     *             non-overlapping representations
     *   resolution -- The minimum change in scaled value needed to produce a change
     *                 in encoding. This should be specified in log space. For
     *                 example, the scaled values 10 and 11 will be distinguishable
     *                 in the output. In terms of the original input values, this
     *                 means 10^1 (1) and 10^1.1 (1.25) will be distinguishable.
     *   name -- an optional string which will become part of the description
     *   clipInput -- if true, non-periodic inputs smaller than minval or greater
     *                 than maxval will be clipped to minval/maxval
     *   forced -- (default False), if True, skip some safety checks
     */
    public class LogEncoder : Encoder<double>
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(LogEncoder));

        private ScalarEncoder _encoder;
        private double _minScaledValue, _maxScaledValue;
        /**
         * Constructs a new {@code LogEncoder}
         */
        internal LogEncoder()
        { }

        /**
         * Returns a builder for building LogEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code LogEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        /**
         *   w -- number of bits to set in output
         *   minval -- minimum input value. must be greater than 0. Lower values are
         *             reset to this value
         *   maxval -- maximum input value (input is strictly less if periodic == True)
         *   periodic -- If true, then the input value "wraps around" such that minval =
         *             maxval For a periodic value, the input must be strictly less than
         *             maxval, otherwise maxval is a true upper bound.
         *
         *   Exactly one of n, radius, resolution must be set. "0" is a special
         *   value that means "not set".
         *   n -- number of bits in the representation (must be > w)
         *   radius -- inputs separated by more than this distance in log space will have
         *             non-overlapping representations
         *   resolution -- The minimum change in scaled value needed to produce a change
         *                 in encoding. This should be specified in log space. For
         *                 example, the scaled values 10 and 11 will be distinguishable
         *                 in the output. In terms of the original input values, this
         *                 means 10^1 (1) and 10^1.1 (1.25) will be distinguishable.
         *   name -- an optional string which will become part of the description
         *   clipInput -- if true, non-periodic inputs smaller than minval or greater
         *                 than maxval will be clipped to minval/maxval
         *   forced -- (default False), if True, skip some safety checks
         */
        public void Init()
        {
            double lowLimit = 1e-07;

            // w defaults to 5
            if (GetW() == 0)
            {
                SetW(5);
            }

            // maxVal defaults to 10000.
            if (GetMaxVal() == 0.0)
            {
                SetMaxVal(10000);
            }

            if (GetMinVal() < lowLimit)
            {
                SetMinVal(lowLimit);
            }

            if (GetMinVal() >= GetMaxVal())
            {
                throw new InvalidOperationException("Max val must be larger than min val or the lower limit " +
                           "for this encoder " + string.Format("{0:#.0000000}", lowLimit));
            }

            _minScaledValue = Math.Log10(GetMinVal());
            _maxScaledValue = Math.Log10(GetMaxVal());

            if (_minScaledValue >= _maxScaledValue)
            {
                throw new InvalidOperationException("Max val must be larger, in log space, than min val.");
            }

            // There are three different ways of thinking about the representation. Handle
            // each case here.
            _encoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                                                   .W(GetW())
                                                   .MinVal(_minScaledValue)
                                                   .MaxVal(_maxScaledValue)
                                                   .Periodic(false)
                                                   .N(GetN())
                                                   .Radius(GetRadius())
                                                   .Resolution(GetResolution())
                                                   .ClipInput(ClipInput())
                                                   .Forced(IsForced())
                                                   .Name(GetName())
                                                   .Build();

            SetN(_encoder.GetN());
            SetResolution(_encoder.GetResolution());
            SetRadius(_encoder.GetRadius());
        }

        public override int GetWidth()
        {
            return _encoder.GetWidth();
        }

        public override bool IsDelta()
        {
            return _encoder.IsDelta();
        }

        public override List<Tuple> GetDescription()
        {
            return _encoder.GetDescription();
        }

        /**
         * {@inheritDoc}
         */
        public override HashSet<FieldMetaType> GetDecoderOutputFieldTypes()
        {
            return _encoder.GetDecoderOutputFieldTypes();
        }

        /**
         * Convert the input, which is in normal space, into log space
         * @param input Value in normal space.
         * @return Value in log space.
         */
        private double? GetScaledValue(double input)
        {
            if (input == SENTINEL_VALUE_FOR_MISSING_DATA)
            {
                return null;
            }
            else {
                double val = input;
                if (val < GetMinVal())
                {
                    val = GetMinVal();
                }
                else if (val > GetMaxVal())
                {
                    val = GetMaxVal();
                }

                return Math.Log10(val);
            }
        }

        /**
         * Returns the bucket indices.
         *
         * @param	input
         */
        public override int[] GetBucketIndices(double input)
        {
            double? scaledVal = GetScaledValue(input);

            if (scaledVal == null)
            {
                return new int[] { };
            }
            else {
                return _encoder.GetBucketIndices(scaledVal.GetValueOrDefault());
            }
        }

        /**
         * Encodes inputData and puts the encoded value into the output array,
         * which is a 1-D array of length returned by {@link Connections#getW()}.
         *
         * Note: The output array is reused, so clear it before updating it.
         * @param inputData Data to encode. This should be validated by the encoder.
         * @param output 1-D array of same length returned by {@link Connections#getW()}
         *
         * @return
         */
        public override void EncodeIntoArray(double input, int[] output)
        {
            double? scaledVal = GetScaledValue(input);

            if (scaledVal == null)
            {
                Arrays.Fill(output, 0);
            }
            else {
                _encoder.EncodeIntoArray(scaledVal.GetValueOrDefault(), output);

                LOG.Debug("input: " + input);
                LOG.Debug(" scaledVal: " + scaledVal);
                LOG.Debug(" output: " + Arrays.ToString(output));
            }
        }

        /**
         * {@inheritDoc}
         */
        public override Tuple Decode(int[] encoded, string parentFieldName)
        {
            // Get the scalar values from the underlying scalar encoder
            DecodeResult decodeResult = (DecodeResult)_encoder.Decode(encoded, parentFieldName);

            Map<string, RangeList> fields = decodeResult.GetFields();

            if (fields.Keys.Count == 0)
            {
                return decodeResult;
            }

            // Convert each range into normal space
            RangeList inRanges = fields.Values.ToArray()[0];
            RangeList outRanges = new RangeList(new List<MinMax>(), "");
            foreach (MinMax minMax in inRanges.GetRanges())
            {
                MinMax scaledMinMax = new MinMax(Math.Pow(10, minMax.Min()),
                                                  Math.Pow(10, minMax.Max()));
                outRanges.Add(scaledMinMax);
            }

            // Generate a text description of the ranges
            string desc = "";
            int numRanges = outRanges.Count;
            for (int i = 0; i < numRanges; i++)
            {
                MinMax minMax = outRanges.GetRange(i);
                if (minMax.Min() != minMax.Max())
                {
                    desc += string.Format("{0:#.00}-{1:#.00}", minMax.Min(), minMax.Max());
                }
                else {
                    desc += string.Format("{0:#.00}", minMax.Min());
                }
                if (i < numRanges - 1)
                {
                    desc += ", ";
                }
            }
            outRanges.SetDescription(desc);

            string fieldName;
            if (!parentFieldName.Equals(""))
            {
                fieldName = string.Format("{0}.{1}", parentFieldName, GetName());
            }
            else {
                fieldName = GetName();
            }

            Map<string, RangeList> outFields = new Map<string, RangeList>();
            outFields.Add(fieldName, outRanges);

            List<string> fieldNames = new List<string>();
            fieldNames.Add(fieldName);

            return new DecodeResult(outFields, fieldNames);
        }

        /**
         * {@inheritDoc}
         */
        public override List<TS> GetBucketValues<TS>(Type t)
        {
            // Need to re-create?
            if (bucketValues == null)
            {
                List<TS> scaledValues = _encoder.GetBucketValues<TS>(t);
                bucketValues = new List<TS>();

                foreach (TS scaledValue in scaledValues)
                {
                    double dScaledValue = (double) Convert.ChangeType(scaledValue, typeof (double));
                    double value = Math.Pow(10, dScaledValue);
                    ((List<double>)bucketValues).Add(value);
                }
            }
            return (List<TS>)bucketValues;
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            EncoderResult scaledResult = _encoder.GetBucketInfo(buckets)[0];
            double scaledValue = (double)scaledResult.GetValue();
            double value = Math.Pow(10, scaledValue);

            return new List<EncoderResult> { new EncoderResult(value, value, scaledResult.GetEncoding()) };
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            EncoderResult scaledResult = _encoder.TopDownCompute(encoded)[0];
            double scaledValue = (double)scaledResult.GetValue();
            double value = Math.Pow(10, scaledValue);

            return new List<EncoderResult> { new EncoderResult(value, value, scaledResult.GetEncoding()) };
        }

        /**
         * {@inheritDoc}
         */
        public override List<double> ClosenessScores(List<double> expValues, List<double> actValues, bool fractional)
        {
            List<double> retVal = new List<double>();

            double expValue, actValue;
            if (expValues[0] > 0)
            {
                expValue = Math.Log10(expValues[0]);
            }
            else {
                expValue = _minScaledValue;
            }
            if (actValues[0] > 0)
            {
                actValue = Math.Log10(actValues[0]);
            }
            else {
                actValue = _minScaledValue;
            }

            double closeness;
            if (fractional)
            {
                double err = Math.Abs(expValue - actValue);
                double pctErr = err / (_maxScaledValue - _minScaledValue);
                pctErr = Math.Min(1.0, pctErr);
                closeness = 1.0 - pctErr;
            }
            else {
                closeness = Math.Abs(expValue - actValue);
            }

            retVal.Add(closeness);
            return retVal;
        }

        /**
         * Returns a {@link EncoderBuilder} for constructing {@link ScalarEncoder}s
         *
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         *
         * @see ScalarEncoder.Builder#setStuff(int)
         */
        public class Builder : BuilderBase
        {

            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new LogEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                try
                {
                    ((LogEncoder)encoder).Init();
                }
                catch (Exception e)
                {
                    string msg = e.Message;
                    int idx = -1;
                    if ((idx = (msg = e.Message).IndexOf("ScalarEncoder")) != -1)
                    {
                        msg = msg.Substring(0, idx) + "LogEncoder";
                    }
                    throw new InvalidOperationException(msg);
                }

                return (LogEncoder)encoder;
            }
        }
    }
}
