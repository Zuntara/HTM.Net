using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
 * DOCUMENTATION TAKEN DIRECTLY FROM THE PYTHON VERSION:
 *
 * A scalar encoder encodes a numeric (floating point) value into an array
 * of bits. The output is 0's except for a contiguous block of 1's. The
 * location of this contiguous block varies continuously with the input value.
 *
 * The encoding is linear. If you want a nonlinear encoding, just transform
 * the scalar (e.g. by applying a logarithm function) before encoding.
 * It is not recommended to bin the data as a pre-processing step, e.g.
 * "1" = $0 - $.20, "2" = $.21-$0.80, "3" = $.81-$1.20, etc as this
 * removes a lot of information and prevents nearby values from overlapping
 * in the output. Instead, use a continuous transformation that scales
 * the data (a piecewise transformation is fine).
 *
 *
 * Parameters:
 * -----------------------------------------------------------------------------
 * w --        The number of bits that areSet to encode a single value - the
 *             "width" of the output signal
 *             restriction: w must be odd to avoid centering problems.
 *
 * minval --   The minimum value of the input signal.
 *
 * maxval --   The upper bound of the input signal
 *
 * periodic -- If true, then the input value "wraps around" such that minval = maxval
 *             For a periodic value, the input must be strictly less than maxval,
 *             otherwise maxval is a true upper bound.
 *
 * There are three mutually exclusive parameters that determine the overall size of
 * of the output. Only one of these should be specified to the constructor:
 *
 * n      --      The number of bits in the output. Must be greater than or equal to w
 * radius --      Two inputs separated by more than the radius have non-overlapping
 *                representations. Two inputs separated by less than the radius will
 *                in general overlap in at least some of their bits. You can think
 *                of this as the radius of the input.
 * resolution --  Two inputs separated by greater than, or equal to the resolution are guaranteed
 *                 to have different representations.
 *
 * Note: radius and resolution are specified w.r.t the input, not output. w is
 * specified w.r.t. the output.
 *
 * Example:
 * day of week.
 * w = 3
 * Minval = 1 (Monday)
 * Maxval = 8 (Monday)
 * periodic = true
 * n = 14
 * [equivalently: radius = 1.5 or resolution = 0.5]
 *
 * The following values would encode midnight -- the start of the day
 * monday (1)   -> 11000000000001
 * tuesday(2)   -> 01110000000000
 * wednesday(3) -> 00011100000000
 * ...
 * sunday (7)   -> 10000000000011
 *
 * Since the resolution is 12 hours, we can also encode noon, as
 * monday noon  -> 11100000000000
 * monday midnight-> 01110000000000
 * tuesday noon -> 00111000000000
 * etc
 *
 *
 * It may not be natural to specify "n", especially with non-periodic
 * data. For example, consider encoding an input with a range of 1-10
 * (inclusive) using an output width of 5.  If you specify resolution =
 * 1, this means that inputs of 1 and 2 have different outputs, though
 * they overlap, but 1 and 1.5 might not have different outputs.
 * This leads to a 14-bit representation like this:
 *
 * 1 ->  11111000000000  (14 bits total)
 * 2 ->  01111100000000
 * ...
 * 10->  00000000011111
 * [resolution = 1; n=14; radius = 5]
 *
 * You could specify resolution = 0.5, which gives
 * 1   -> 11111000... (22 bits total)
 * 1.5 -> 011111.....
 * 2.0 -> 0011111....
 * [resolution = 0.5; n=22; radius=2.5]
 *
 * You could specify radius = 1, which gives
 * 1   -> 111110000000....  (50 bits total)
 * 2   -> 000001111100....
 * 3   -> 000000000011111...
 * ...
 * 10  ->                           .....000011111
 * [radius = 1; resolution = 0.2; n=50]
 *
 *
 * An N/M encoding can also be used to encode a binary value,
 * where we want more than one bit to represent each state.
 * For example, we could have: w = 5, minval = 0, maxval = 1,
 * radius = 1 (which is equivalent to n=10)
 * 0 -> 1111100000
 * 1 -> 0000011111
 *
 *
 * Implementation details:
 * --------------------------------------------------------------------------
 * range = maxval - minval
 * h = (w-1)/2  (half-width)
 * resolution = radius / w
 * n = w * range/radius (periodic)
 * n = w * range/radius + 2 * h (non-periodic)
 *
 * @author metaware
 */
 [Serializable]
    public class ScalarEncoder : Encoder<double>
    {
        [NonSerialized]
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(ScalarEncoder));

        /**
	     * Constructs a new {@code ScalarEncoder}
	     */
        internal ScalarEncoder() { }

        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        /**
	     * Returns true if the underlying encoder works on deltas
	     */
        public override bool IsDelta()
        {
            return false;
        }

        /**
         * w -- number of bits toSet in output
         * minval -- minimum input value
         * maxval -- maximum input value (input is strictly less if periodic == True)
         *
         * Exactly one of n, radius, resolution must beSet. "0" is a special
         * value that means "notSet".
         *
         * n -- number of bits in the representation (must be > w)
         * radius -- inputs separated by more than, or equal to this distance will have non-overlapping
         * representations
         * resolution -- inputs separated by more than, or equal to this distance will have different
         * representations
         *
         * name -- an optional string which will become part of the description
         *
         * clipInput -- if true, non-periodic inputs smaller than minval or greater
         * than maxval will be clipped to minval/maxval
         *
         * forced -- if true, skip some safety checks (for compatibility reasons), default false
         */
        public virtual void Init()
        {
            if (GetW() % 2 == 0)
            {
                throw new InvalidOperationException("W must be an odd number (to eliminate centering difficulty)");
            }

            SetHalfWidth((GetW() - 1) / 2);

            // For non-periodic inputs, padding is the number of bits "outside" the range,
            // on each side. I.e. the representation of minval is centered on some bit, and
            // there are "padding" bits to the left of that centered bit; similarly with
            // bits to the right of the center bit of maxval
            SetPadding(IsPeriodic() ? 0 : GetHalfWidth());

            if (!double.IsNaN(GetMinVal()) && !double.IsNaN(GetMaxVal()))
            {
                if (GetMinVal() >= GetMaxVal())
                {
                    throw new InvalidOperationException("maxVal must be > minVal");
                }
                SetRangeInternal(GetMaxVal() - GetMinVal());
            }

            // There are three different ways of thinking about the representation. Handle
            // each case here.
            InitEncoder(GetW(), GetMinVal(), GetMaxVal(), GetN(), GetRadius(), GetResolution());

            //nInternal represents the output area excluding the possible padding on each side
            SetNInternal(GetN() - 2 * GetPadding());

            if (GetName() == null)
            {
                if ((GetMinVal() % ((int)GetMinVal())) > 0 ||
                    (GetMaxVal() % ((int)GetMaxVal())) > 0)
                {
                    SetName("[" + GetMinVal() + ":" + GetMaxVal() + "]");
                }
                else {
                    SetName("[" + (int)GetMinVal() + ":" + (int)GetMaxVal() + "]");
                }
            }

            //Checks for likely mistakes in encoderSettings
            if (!IsForced())
            {
                CheckReasonableSettings();
            }
            description.Add(new Tuple((name = GetName()).Equals("None") ? "[" + (int)GetMinVal() + ":" + (int)GetMaxVal() + "]" : name, 0));
        }

        /**
	     * There are three different ways of thinking about the representation.
         * Handle each case here.
         *
	     * @param c
	     * @param minVal
	     * @param maxVal
	     * @param n
	     * @param radius
	     * @param resolution
	     */
        public virtual void InitEncoder(int w, double minVal, double maxVal, int n, double radius, double resolution)
        {
            if (n != 0)
            {
                if (!double.IsNaN(minVal) && !double.IsNaN(maxVal))
                {
                    if (!IsPeriodic())
                    {
                        SetResolution(GetRangeInternal() / (GetN() - GetW()));
                    }
                    else {
                        SetResolution(GetRangeInternal() / GetN());
                    }

                    SetRadius(GetW() * GetResolution());

                    if (IsPeriodic())
                    {
                        SetRange(GetRangeInternal());
                    }
                    else {
                        SetRange(GetRangeInternal() + GetResolution());
                    }
                }
            }
            else {
                if (radius != 0)
                {
                    SetResolution(GetRadius() / w);
                }
                else if (resolution != 0)
                {
                    SetRadius(GetResolution() * w);
                }
                else {
                    throw new InvalidOperationException("One of n, radius, resolution must be specified for a ScalarEncoder");
                }

                if (IsPeriodic())
                {
                    SetRange(GetRangeInternal());
                }
                else {
                    SetRange(GetRangeInternal() + GetResolution());
                }

                double nFloat = w * (GetRange() / GetRadius()) + 2 * GetPadding();
                SetN((int)Math.Ceiling(nFloat));
            }
        }

        /**
	 * Return the bit offset of the first bit to beSet in the encoder output.
     * For periodic encoders, this can be a negative number when the encoded output
     * wraps around.
     *
	 * @param c			the memory
	 * @param input		the input data
	 * @return			an encoded array
	 */
        public int? GetFirstOnBit(double input)
        {
            if (input == SENTINEL_VALUE_FOR_MISSING_DATA)
            {
                return null;
            }
            else {
                if (input < GetMinVal())
                {
                    if (ClipInput() && !IsPeriodic())
                    {
                        LOGGER.Info("Clipped input " + GetName() + "=" + input + " to minval " + GetMinVal());
                        input = GetMinVal();
                    }
                    else {
                        throw new InvalidOperationException("input (" + input + ") less than range (" +
                           GetMinVal() + " - " + GetMaxVal());
                    }
                }
            }

            if (IsPeriodic())
            {
                if (input >= GetMaxVal())
                {
                    throw new InvalidOperationException("input (" + input + ") greater than periodic range (" +
                       GetMinVal() + " - " + GetMaxVal());
                }
            }
            else {
                if (input > GetMaxVal())
                {
                    if (ClipInput())
                    {
                        LOGGER.Info("Clipped input " + GetName() + "=" + input + " to maxval " + GetMaxVal());
                        input = GetMaxVal();
                    }
                    else {
                        throw new InvalidOperationException("input (" + input + ") greater than periodic range (" +
                           GetMinVal() + " - " + GetMaxVal());
                    }
                }
            }

            int centerbin;
            if (IsPeriodic())
            {
                centerbin = ((int)((input - GetMinVal()) * GetNInternal() / GetRange())) + GetPadding();
            }
            else {
                centerbin = ((int)(((input - GetMinVal()) + GetResolution() / 2) / GetResolution())) + GetPadding();
            }

            return centerbin - GetHalfWidth();
        }

        /**
         * Check if theSettings are reasonable for the SpatialPooler to work
         * @param c
         */
        public void CheckReasonableSettings()
        {
            if (GetW() < 21)
            {
                throw new InvalidOperationException(string.Format("Number of bits in the SDR ({0}) must be greater than 2, and recommended >= 21 (use forced=True to override)", GetW()));
            }
        }

        /**
         * {@inheritDoc}
         */
        public override HashSet<FieldMetaType> GetDecoderOutputFieldTypes()
        {
            return new HashSet<FieldMetaType> { FieldMetaType.Float, FieldMetaType.Integer };
        }

        /**
         * Should return the output width, in bits.
         */
        public override int GetWidth()
        {
            return GetN();
        }

        /**
         * {@inheritDoc}
         * NO-OP
         */
        public override int[] GetBucketIndices(string input) { return null; }

        /**
         * Returns the bucket indices.
         *
         * @param	input
         */
        public override int[] GetBucketIndices(double input)
        {
            int minbin = GetFirstOnBit(input).GetValueOrDefault();

            //For periodic encoders, the bucket index is the index of the center bit
            int bucketIdx;
            if (IsPeriodic())
            {
                bucketIdx = minbin + GetHalfWidth();
                if (bucketIdx < 0)
                {
                    bucketIdx += GetN();
                }
            }
            else {//for non-periodic encoders, the bucket index is the index of the left bit
                bucketIdx = minbin;
            }

            return new int[] { bucketIdx };
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
            if (double.IsNaN(input))
            {
                Arrays.Fill(output, 0);
                return;
            }

            int? bucketVal = GetFirstOnBit(input);
            if (bucketVal != null)
            {
                int? bucketIdx = bucketVal;
                Arrays.Fill(output, 0);
                int minbin = bucketIdx.GetValueOrDefault();
                int maxbin = minbin + 2 * GetHalfWidth();
                if (IsPeriodic())
                {
                    if (maxbin >= GetN())
                    {
                        int bottombins = maxbin - GetN()+ 1;
                        //if (bottombins > output.Length) bottombins = output.Length; // TODO: check that this is needed, added it for tests
                        int[] range = ArrayUtils.Range(0,  bottombins);
                        ArrayUtils.SetIndexesTo(output, range, 1);
                        maxbin = GetN() - 1;
                    }
                    if (minbin < 0)
                    {
                        int topbins = -minbin;
                        ArrayUtils.SetIndexesTo(output, ArrayUtils.Range(GetN() - topbins, GetN()), 1);
                        minbin = 0;
                    }
                }

                ArrayUtils.SetIndexesTo(output, ArrayUtils.Range(minbin, maxbin + 1), 1);
            }

            // Added guard against immense string concatenation
            if (LOGGER.IsDebugEnabled)
            {
                LOGGER.Debug("");
                LOGGER.Debug("input: " + input);
                LOGGER.Debug("range: " + GetMinVal() + " - " + GetMaxVal());
                LOGGER.Debug("n:" + GetN());
                LOGGER.Debug("w:" + GetW());
                LOGGER.Debug("resolution:" + GetResolution());
                LOGGER.Debug("radius:" + GetRadius());
                LOGGER.Debug("periodic:" + IsPeriodic());
                LOGGER.Debug("output: " + Arrays.ToString(output));
                LOGGER.Debug("input desc: " + Decode(output, ""));
            }
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            if (o is string)
            {
                double parsed = double.Parse((string)o, NumberFormatInfo.InvariantInfo);
                EncodeIntoArray(parsed, tempArray);
            }
            else
            {
                EncodeIntoArray((double)o, tempArray);
            }
        }

        /**
         * Returns a {@link DecodeResult} which is a tuple of range names
         * and lists of {@link RangeLists} in the first entry, and a list
         * of descriptions for each range in the second entry.
         *
         * @param encoded			the encoded bit vector
         * @param parentFieldName	the field the vector corresponds with
         * @return
         */
        public override Tuple Decode(int[] encoded, string parentFieldName) // returns DecodeResult
        {
            // For now, we simply assume any top-down output greater than 0
            // is ON. Eventually, we will probably want to incorporate the strength
            // of each top-down output.
            if (encoded == null || encoded.Length < 1)
            {
                return null;
            }
            int[] tmpOutput = Arrays.CopyOf(encoded, encoded.Length);

            // ------------------------------------------------------------------------
            // First, assume the input pool is not sampled 100%, and fill in the
            //  "holes" in the encoded representation (which are likely to be present
            //  if this is a coincidence that was learned by the SP).

            // Search for portions of the output that have "holes"
            int maxZerosInARow = GetHalfWidth();
            for (int wi = 0; wi < maxZerosInARow; wi++)
            {
                int[] searchStr = new int[wi + 3];
                Arrays.Fill(searchStr, 1);
                ArrayUtils.SetRangeTo(searchStr, 1, -1, 0);
                int subLen = searchStr.Length;

                // Does this search string appear in the output?
                if (IsPeriodic())
                {
                    for (int j = 0; j < GetN(); j++)
                    {
                        int[] outputIndices = ArrayUtils.Range(j, j + subLen);
                        outputIndices = ArrayUtils.Modulo(outputIndices, GetN());
                        if (Arrays.AreEqual(searchStr, ArrayUtils.Sub(tmpOutput, outputIndices)))
                        {
                            ArrayUtils.SetIndexesTo(tmpOutput, outputIndices, 1);
                        }
                    }
                }
                else {
                    for (int j = 0; j < GetN() - subLen + 1; j++)
                    {
                        if (Arrays.AreEqual(searchStr, ArrayUtils.Sub(tmpOutput, ArrayUtils.Range(j, j + subLen))))
                        {
                            ArrayUtils.SetRangeTo(tmpOutput, j, j + subLen, 1);
                        }
                    }
                }
            }

            LOGGER.Debug("raw output:" + Arrays.ToString(
                    ArrayUtils.Sub(encoded, ArrayUtils.Range(0, GetN()))));
            LOGGER.Debug("filtered output:" + Arrays.ToString(tmpOutput));

            // ------------------------------------------------------------------------
            // Find each run of 1's.
            //int[] nz = tmpOutput.Where(n => n > 0).ToArray();
            int[] nz = ArrayUtils.Where(tmpOutput, x => x > 0);

            //        int[] nz = ArrayUtils.Where(tmpOutput, new Condition.Adapter<Integer>() {
            //        @Override

            //        public boolean eval(int n)
            //    {
            //        return n > 0;
            //    }
            //});
            List<Tuple> runs = new List<Tuple>(); //will be tuples of (startIdx, runLength)
            Array.Sort(nz);
            int[] run = new int[] { nz[0], 1 };
            int i = 1;
            while (i < nz.Length)
            {
                if (nz[i] == run[0] + run[1])
                {
                    run[1] += 1;
                }
                else {
                    runs.Add(new Tuple(run[0], run[1]));
                    run = new int[] { nz[i], 1 };
                }
                i += 1;
            }
            runs.Add(new Tuple(run[0], run[1]));

            // If we have a periodic encoder, merge the first and last run if they
            // both go all the way to the edges
            if (IsPeriodic() && runs.Count > 1)
            {
                int l = runs.Count - 1;
                if (((int)runs[0].Get(0)) == 0 && ((int)runs[l].Get(0)) + ((int)runs[l].Get(1)) == GetN())
                {
                    runs[l] = new Tuple((int)runs[l].Get(0), ((int)runs[l].Get(1)) + ((int)runs[0].Get(1)));
                    runs = runs.SubList(1, runs.Count);
                }
            }

            // ------------------------------------------------------------------------
            // Now, for each group of 1's, determine the "left" and "right" edges, where
            // the "left" edge is inset by halfwidth and the "right" edge is inset by
            // halfwidth.
            // For a group of width w or less, the "left" and "right" edge are both at
            // the center position of the group.
            int left = 0;
            int right = 0;
            List<MinMax> ranges = new List<MinMax>();
            foreach (Tuple tupleRun in runs)
            {
                int start = (int)tupleRun.Get(0);
                int runLen = (int)tupleRun.Get(1);
                if (runLen <= GetW())
                {
                    left = right = start + runLen / 2;
                }
                else {
                    left = start + GetHalfWidth();
                    right = start + runLen - 1 - GetHalfWidth();
                }

                double inMin, inMax;
                // Convert to input space.
                if (!IsPeriodic())
                {
                    inMin = (left - GetPadding()) * GetResolution() + GetMinVal();
                    inMax = (right - GetPadding()) * GetResolution() + GetMinVal();
                }
                else {
                    inMin = (left - GetPadding()) * GetRange() / GetNInternal() + GetMinVal();
                    inMax = (right - GetPadding()) * GetRange() / GetNInternal() + GetMinVal();
                }
                // Handle wrap-around if periodic
                if (IsPeriodic())
                {
                    if (inMin >= GetMaxVal())
                    {
                        inMin -= GetRange();
                        inMax -= GetRange();
                    }
                }

                // Clip low end
                if (inMin < GetMinVal())
                {
                    inMin = GetMinVal();
                }
                if (inMax < GetMinVal())
                {
                    inMax = GetMinVal();
                }

                // If we have a periodic encoder, and the max is past the edge, break into
                // 	2 separate ranges
                if (IsPeriodic() && inMax >= GetMaxVal())
                {
                    ranges.Add(new MinMax(inMin, GetMaxVal()));
                    ranges.Add(new MinMax(GetMinVal(), inMax - GetRange()));
                }
                else {
                    if (inMax > GetMaxVal())
                    {
                        inMax = GetMaxVal();
                    }
                    if (inMin > GetMaxVal())
                    {
                        inMin = GetMaxVal();
                    }
                    ranges.Add(new MinMax(inMin, inMax));
                }
            }

            string desc = GenerateRangeDescription(ranges);
            string fieldName;
            // Return result
            if (parentFieldName != null && !string.IsNullOrWhiteSpace(parentFieldName))
            {
                fieldName = string.Format("%s.%s", parentFieldName, GetName());
            }
            else {
                fieldName = GetName();
            }

            RangeList inner = new RangeList(ranges, desc);
            Map<string, RangeList> fieldsDict = new Map<string, RangeList>();
            fieldsDict.Add(fieldName, inner);

            return new DecodeResult(fieldsDict, new List<string> { fieldName });
        }

        /**
         * Generate description from a text description of the ranges
         *
         * @param	ranges		A list of {@link MinMax}es.
         */
        public string GenerateRangeDescription(List<MinMax> ranges)
        {
            StringBuilder desc = new StringBuilder();
            int numRanges = ranges.Count;
            for (int i = 0; i < numRanges; i++)
            {
                if (ranges[i].Min() != ranges[i].Max())
                {
                    desc.Append(string.Format("{0:#.00}-{1:#.00}", ranges[i].Min(), ranges[i].Max()));
                }
                else {
                    desc.Append(string.Format("{0:#.00}", ranges[i].Min()));
                }
                if (i < numRanges - 1)
                {
                    desc.Append(", ");
                }
            }
            return desc.ToString();
        }

        /**
         * Return the internal topDownMapping matrix used for handling the
         * bucketInfo() and topDownCompute() methods. This is a matrix, one row per
         * category (bucket) where each row contains the encoded output for that
         * category.
         *
         * @param c		the connections memory
         * @return		the internal topDownMapping
         */
        public SparseObjectMatrix<int[]> GetTopDownMapping()
        {

            if (base.topDownMapping == null)
            {
                //The input scalar value corresponding to each possible output encoding
                if (IsPeriodic())
                {
                    SetTopDownValues(
                          ArrayUtils.Arrange(GetMinVal() + GetResolution() / 2.0,
                            GetMaxVal(), GetResolution()));
                }
                else {
                    //Number of values is (max-min)/resolutions
                    SetTopDownValues(
                          ArrayUtils.Arrange(GetMinVal(), GetMaxVal() + GetResolution() / 2.0,
                            GetResolution()));
                }
            }

            //Each row represents an encoded output pattern
            int numCategories = GetTopDownValues().Length;
            SparseObjectMatrix<int[]> topDownMapping;
            SetTopDownMapping(
                  topDownMapping = new SparseObjectMatrix<int[]>(
                      new int[] { numCategories }));

            double[] topDownValues = GetTopDownValues();
            int[] outputSpace = new int[GetN()];
            double minVal = GetMinVal();
            double maxVal = GetMaxVal();
            for (int i = 0; i < numCategories; i++)
            {
                double value = topDownValues[i];
                value = Math.Max(value, minVal);
                value = Math.Min(value, maxVal);
                EncodeIntoArray(value, outputSpace);
                topDownMapping.Set(i, Arrays.CopyOf(outputSpace, outputSpace.Length));
            }

            return topDownMapping;
        }

        /**
	 * {@inheritDoc}
	 *
	 * @param <S>	the input value, in this case a double
	 * @return	a list of one input double
	 */
        public override List<double> GetScalars(string d)
        {
            List<double> retVal = new List<double>();
            retVal.Add(double.Parse(d));
            return retVal;
        }

        /**
         * Returns a list of items, one for each bucket defined by this encoder.
         * Each item is the value assigned to that bucket, this is the same as the
         * EncoderResult.value that would be returned by GetBucketInfo() for that
         * bucket and is in the same format as the input that would be passed to
         * encode().
         *
         * This call is faster than calling GetBucketInfo() on each bucket individually
         * if all you need are the bucket values.
         *
         * @param	returnType 		class type parameter so that this method can return encoder
         * 							specific value types
         *
         * @return list of items, each item representing the bucket value for that
         *        bucket.
         */
        public override List<S> GetBucketValues<S>(Type t)
        {
            if (bucketValues == null)
            {
                SparseObjectMatrix<int[]> topDownMapping = GetTopDownMapping();
                int numBuckets = topDownMapping.GetMaxIndex() + 1;
                bucketValues = new List<double>();
                for (int i = 0; i < numBuckets; i++)
                {
                    ((List<double>)bucketValues).Add((double)GetBucketInfo(new[] { i })[0].Get(1));
                }
            }
            return (List<S>)bucketValues;
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            SparseObjectMatrix<int[]> topDownMapping = GetTopDownMapping();

            //The "category" is simply the bucket index
            int category = buckets[0];
            int[] encoding = topDownMapping.GetObject(category);

            //Which input value does this correspond to?
            double inputVal;
            if (IsPeriodic())
            {
                inputVal = GetMinVal() + GetResolution() / 2 + category * GetResolution();
            }
            else {
                inputVal = GetMinVal() + category * GetResolution();
            }

            return new List<EncoderResult> { new EncoderResult(inputVal, inputVal, encoding) };
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            //Get/generate the topDown mapping table
            SparseObjectMatrix<int[]> topDownMapping = GetTopDownMapping();

            // See which "category" we match the closest.
            int category = ArrayUtils.Argmax(RightVecProd(topDownMapping, encoded));

            return GetBucketInfo(new int[] { category });
        }

        /**
         * Returns a list of {@link Tuple}s which in this case is a list of
         * key value parameter values for this {@code ScalarEncoder}
         *
         * @return	a list of {@link Tuple}s
         */
        public List<Tuple> Dict()
        {
            List<Tuple> l = new List<Tuple>();
            l.Add(new Tuple("maxval", GetMaxVal()));
            l.Add(new Tuple("bucketValues", GetBucketValues<double>(typeof(double))));
            l.Add(new Tuple("nInternal", GetNInternal()));
            l.Add(new Tuple("name", GetName()));
            l.Add(new Tuple("minval", GetMinVal()));
            l.Add(new Tuple("topDownValues", Arrays.ToString(GetTopDownValues())));
            l.Add(new Tuple("clipInput", ClipInput()));
            l.Add(new Tuple("n", GetN()));
            l.Add(new Tuple("padding", GetPadding()));
            l.Add(new Tuple("range", GetRange()));
            l.Add(new Tuple("periodic", IsPeriodic()));
            l.Add(new Tuple("radius", GetRadius()));
            l.Add(new Tuple("w", GetW()));
            l.Add(new Tuple("topDownMappingM", GetTopDownMapping()));
            l.Add(new Tuple("halfwidth", GetHalfWidth()));
            l.Add(new Tuple("resolution", GetResolution()));
            l.Add(new Tuple("rangeInternal", GetRangeInternal()));

            return l;
        }

        public class Builder : BuilderBase
        {
            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new ScalarEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////

                ((ScalarEncoder)encoder).Init();

                return (ScalarEncoder)encoder;
            }
        }
    }
}