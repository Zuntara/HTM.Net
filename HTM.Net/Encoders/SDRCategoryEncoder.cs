using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using log4net;
using MathNet.Numerics.Random;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
 * Encodes a list of discrete categories (described by strings), that aren't
 * related to each other.
 * Each  encoding is an SDR in which w out of n bits are turned on.
 * <p/>
 * Unknown categories are encoded as a single
 *
 * @see Encoder
 * @see EncoderResult
 */
 [Serializable]
    public class SDRCategoryEncoder : Encoder<string>
    {

        private static readonly ILog LOG = LogManager.GetLogger(typeof(SDRCategoryEncoder));

        private MathNet.Numerics.Random.RandomSource _random;
        private int _thresholdOverlap;
        private readonly SDRByCategoryMap _sdrByCategory = new SDRByCategoryMap();

        /**
         * Inner class for keeping Categories and SDRs in ordered way
         */
        private sealed class SDRByCategoryMap : Map<string, int[]>
        {
            public int[] GetSdr(int index)
            {
                KeyValuePair<string, int[]>? entry = GetEntry(index);
                if (entry == null) return null;
                return entry.GetValueOrDefault().Value;
            }

            public string GetCategory(int index)
            {
                KeyValuePair<string, int[]>? entry = GetEntry(index);
                if (entry == null) return null;
                return entry.GetValueOrDefault().Key;
            }

            public int GetIndexByCategory(string category)
            {
                List<string> categories = Keys.ToList();
                int inx = 0;
                foreach (string s in categories)
                {
                    if (s.Equals(category))
                    {
                        return inx;
                    }
                    inx++;
                }
                return 0;
            }

            private KeyValuePair<string, int[]>? GetEntry(int i)
            {
                List<KeyValuePair<string, int[]>> entries = this.ToList();
                if (i < 0 || i > entries.Count)
                {
                    throw new ArgumentException("Index should be in following range:[0," + entries.Count + "]");
                }
                int j = 0;
                foreach (KeyValuePair<string, int[]> entry in entries)
                    if (j++ == i) return entry;

                return null;
            }

        }

        /**
         * Returns a builder for building {@code SDRCategoryEncoder}s.
         * This is the only way to instantiate {@code SDRCategoryEncoder}
         *
         * @return a {@code SDRCategoryEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        internal SDRCategoryEncoder()
        {
        }

        /* Python mapping
        def __init__(self, n, w, categoryList = None, name="category", verbosity=0,
                   encoderSeed=1, forced=False):
        */
        private void Init(int n, int w, List<string> categoryList, string name,
                          int encoderSeed, bool forced)
        {

            /*Python ref: n is  total bits in output
            w is the number of bits that are turned on for each rep
            categoryList is a list of strings that define the categories.
            If "none" then categories will automatically be added as they are encountered.
            forced (default False) : if True, skip checks for parameters' settings; see encoders/scalar.py for details*/
            this.n = n;
            this.w = w;
            this.encLearningEnabled = true;
            this._random = new SystemRandomSource();
            if (encoderSeed != -1)
            {
                this._random = new SystemRandomSource(encoderSeed);
            }
            if (!forced)
            {
                if (n / w < 2)
                {
                    throw new ArgumentException(string.Format(
                            "Number of ON bits in SDR ({0}) must be much smaller than the output width ({1})", w, n));
                }
                if (w < 21)
                {
                    throw new ArgumentException(string.Format(
                            "Number of bits in the SDR ({0}) must be greater than 2, and should be >= 21, pass forced=True to init() to override this check",
                            w));
                }

            }
            /*
            #Calculate average overlap of SDRs for decoding
            #Density is fraction of bits on, and it is also the
            #probability that any individual bit is on.
            */
            double density = (double)this.w / this.n;
            double averageOverlap = w * density;
            /*
            # We can do a better job of calculating the threshold. For now, just
            # something quick and dirty, which is the midway point between average
            # and full overlap. averageOverlap is always < w,  so the threshold
            # is always < w.
            */
            this._thresholdOverlap = (int)(averageOverlap + this.w) / 2;
            /*
            #  1.25 -- too sensitive for decode test, so make it less sensitive
            */
            if (this._thresholdOverlap < this.w - 3)
            {
                this._thresholdOverlap = this.w - 3;
            }
            this.description.Add(new Tuple(name, 0));
            this.name = name;
            /*
            # Always include an 'unknown' category for
            # edge cases
            */
            this.AddCategory("<UNKNOWN>");
            if (categoryList == null || categoryList.Count == 0)
            {
                this.SetLearningEnabled(true);
            }
            else {
                this.SetLearningEnabled(false);
                foreach (string category in categoryList)
                {
                    this.AddCategory(category);
                }
            }
        }

        /**
         * {@inheritDoc}
         */
        public override int GetWidth()
        {
            return this.GetN();
        }

        /**
         * {@inheritDoc}
         */
        public override bool IsDelta()
        {
            return false;
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(string input, int[] output)
        {
            int index;
            if (string.IsNullOrEmpty(input))
            {
                Arrays.Fill(output, 0);
                index = 0;
            }
            else {
                index = GetBucketIndices(input)[0];
                int[] categoryEncoding = _sdrByCategory.GetSdr(index);
                Array.Copy(categoryEncoding, 0, output, 0, categoryEncoding.Length);
            }
            LOG.Debug("input:" + input + ", index:" + index + ", output:" + ArrayUtils.IntArrayToString(output));
            LOG.Debug("decoded:" + DecodedToStr(Decode(output, "")));
        }

        #region Overrides of Encoder<string>

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            if (o is string)
            {
                EncodeIntoArray(o as string, tempArray);
            }
            else
            {
                throw new NotImplementedException();
            }
        }

        #endregion

        /**
         * {@inheritDoc}
         */
        public override HashSet<FieldMetaType> GetDecoderOutputFieldTypes()
        {
            return new HashSet<FieldMetaType>(new[] { FieldMetaType.List, FieldMetaType.String });
        }

        /**
         * {@inheritDoc}
         */
        public override int[] GetBucketIndices(string input)
        {
            return new int[] { (int)GetScalars(input)[0] };
        }

        /**
         * {@inheritDoc}
         */
        public List<double> GetScalars<S>(S input)
        {
            string inputCasted = TypeConverter.Convert<string>( input);
            int index = 0;
            List<double> result = new List<double>();
            if (string.IsNullOrEmpty(inputCasted))
            {
                result.Add(0);
                return result;
            }
            if (!_sdrByCategory.ContainsKey(inputCasted))
            {
                if (IsEncoderLearningEnabled())
                {
                    index = _sdrByCategory.Count;
                    AddCategory(inputCasted);
                }
            }
            else {
                index = _sdrByCategory.GetIndexByCategory(inputCasted);
            }
            result.Add(index);
            return result;
        }


        /**
         * No parentFieldName parameter method overload for the {@link #decode(int[], String)}.
         *
         * @param encoded - bit array to be decoded
         * @return
         */
        public DecodeResult Decode(int[] encoded)
        {
            return (DecodeResult) Decode(encoded, null);
        }

        /**
         * {@inheritDoc}
         */
        public override Tuple Decode(int[] encoded, string parentFieldName)
        {
            //Debug.Assert(ArrayUtils.All(encoded, new Condition.Adapter<Integer>() {
            //        public boolean eval(int i)
            //    {
            //        return i <= 1;
            //    }
            //});
            Debug.Assert(ArrayUtils.All(encoded, n => n <= 1));

            //overlaps =  (self.sdrs * encoded[0:self.n]).sum(axis=1)
            int[] overlap = new int[_sdrByCategory.Count];
            for (int i = 0; i < _sdrByCategory.Count; i++)
            {
                int[] sdr = _sdrByCategory.GetSdr(i);
                for (int j = 0; j < sdr.Length; j++)
                {
                    if (sdr[j] == encoded[j] && encoded[j] == 1)
                    {
                        overlap[i]++;
                    }
                }
            }
            LOG.Debug("Overlaps for decoding:");
            if (LOG.IsDebugEnabled)
            {
                int inx = 0;
                foreach (string category in _sdrByCategory.Keys)
                {
                    LOG.Debug(overlap[inx] + " " + category);
                    inx++;
                }
            }
            //matchingCategories =  (overlaps > self.thresholdOverlap).nonzero()[0]

            int[] matchingCategories = ArrayUtils.Where(overlap, overlaps=> overlaps > _thresholdOverlap);

            //int[] matchingCategories = ArrayUtils.where(overlap, new Condition.Adapter<Integer>() {
            //        @Override
            //        public boolean eval(int overlaps)
            //    {
            //        return overlaps > thresholdOverlap;
            //    }
            //});

            //int[] matchingCategories = overlap.Where(overlaps => overlaps > _thresholdOverlap).ToArray();

            StringBuilder resultString = new StringBuilder();
            List<MinMax> resultRanges = new List<MinMax>();
            string fieldName;
            foreach (int index in matchingCategories)
            {
                if (resultString.Length != 0)
                {
                    resultString.Append(" ");
                }
                resultString.Append(_sdrByCategory.GetCategory(index));
                resultRanges.Add(new MinMax(index, index));
            }
            if (string.IsNullOrEmpty(parentFieldName))
            {
                fieldName = GetName() ?? String.Empty;
            }
            else {
                fieldName = string.Format("{0}.{1}", parentFieldName, GetName());
            }
            Map<string, RangeList> fieldsDict = new Map<string, RangeList>();
            fieldsDict.Add(fieldName, new RangeList(resultRanges, resultString.ToString()));
            // return ({fieldName: (resultRanges, resultString)}, [fieldName])
            return new DecodeResult(fieldsDict, new List<string> { fieldName });
        }


        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            if (_sdrByCategory.Count == 0)
            {
                return new List<EncoderResult>();
            }
            //TODO the rightVecProd method belongs to SparseBinaryMatrix in Nupic Core, In python this method call stack: topDownCompute [sdrcategory.py:317]/rightVecProd [math.py:4474] -->return _math._SparseMatrix32_rightVecProd(self, *args)
            int categoryIndex = ArrayUtils.Argmax(RightVecProd(GetTopDownMapping(), encoded));
            return GetEncoderResultsByIndex(GetTopDownMapping(), categoryIndex);
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            if (_sdrByCategory.Count == 0)
            {
                return new List<EncoderResult>();
            }
            int categoryIndex = buckets[0];
            return GetEncoderResultsByIndex(GetTopDownMapping(), categoryIndex);
        }

        /**
         * Return the internal topDownMapping matrix used for handling the
         * {@link #getBucketInfo(int[])}  and {@link #topDownCompute(int[])} methods. This is a matrix, one row per
         * category (bucket) where each row contains the encoded output for that
         * category.
         *
         * @return {@link SparseObjectMatrix}
         */
        public SparseObjectMatrix<int[]> GetTopDownMapping()
        {
            if (topDownMapping == null)
            {
                topDownMapping = new SparseObjectMatrix<int[]>(new int[] { _sdrByCategory.Count });
                int[] outputSpace = new int[GetN()];
                List<string> categories = _sdrByCategory.Keys.ToList();
                int inx = 0;
                foreach (string category in categories)
                {
                    EncodeIntoArray(category, outputSpace);
                    topDownMapping.Set(inx, Arrays.CopyOf(outputSpace, outputSpace.Length));
                    inx++;
                }
            }
            return topDownMapping;
        }

        /**
         * {@inheritDoc}
         */
        public override List<S> GetBucketValues<S>(Type returnType)
        {
            return new List<S>(_sdrByCategory.Keys.Cast<S>().ToList());
        }

        /**
         * Returns list of registered SDRs for this encoder
         *
         * @return {@link Collection}
         */
        public ICollection<int[]> GetSDRs()
        {
            return _sdrByCategory.Values.ToList();
        }


        private List<EncoderResult> GetEncoderResultsByIndex(SparseObjectMatrix<int[]> topDownMapping, int categoryIndex)
        {
            List<EncoderResult> result = new List<EncoderResult>();
            string category = _sdrByCategory.GetCategory(categoryIndex);
            int[] encoding = topDownMapping.GetObject(categoryIndex);
            result.Add(new EncoderResult(category, categoryIndex, encoding));
            return result;
        }

        private void AddCategory(string category)
        {
            if (this._sdrByCategory.ContainsKey(category))
            {
                throw new ArgumentException(string.Format("Attempt to add encoder category '{0}' that already exists",
                                                                 category));
            }
            _sdrByCategory.Add(category, NewRep());
            //reset topDown mapping
            topDownMapping = null;
        }

        //replacement for Python sorted(self.random.sample(xrange(self.n), self.w))
        private int[] getSortedSample(int populationSize, int sampleLength)
        {
            HashSet<int> resultSet = new HashSet<int>();
            while (resultSet.Count < sampleLength)
            {
                resultSet.Add(_random.Next(populationSize));
            }
            int[] result = resultSet.ToArray();
            Array.Sort(result);
            return result;
        }


        private int[] NewRep()
        {
            int maxAttempts = 1000;
            bool foundUnique = true;
            int[] oneBits;
            int[] sdr = new int[n];
            for (int index = 0; index < maxAttempts; index++)
            {
                foundUnique = true;
                oneBits = getSortedSample(n, w);
                sdr = new int[n];
                for (int i = 0; i < oneBits.Length; i++)
                {
                    int oneBitInx = oneBits[i];
                    sdr[oneBitInx] = 1;
                }
                foreach (int[] existingSdr in this._sdrByCategory.Values)
                {
                    if (Arrays.AreEqual(sdr, existingSdr))
                    {
                        foundUnique = false;
                        break;
                    }
                }
                if (foundUnique)
                {
                    break;
                }
            }
            if (!foundUnique)
            {
                throw new ApplicationException(string.Format("Error, could not find unique pattern {0} after {1} attempts",
                                                         _sdrByCategory.Count, maxAttempts));
            }
            return sdr;
        }

        /**
         * Builder class for {@code SDRCategoryEncoder}
         * <p>N is  total bits in output</p>
         * <p>W is the number of bits that are turned on for each rep</p>
         * <p>categoryList is a list of strings that define the categories.If no categories provided, then they will automatically be added as they are encountered.</p>
         * <p>forced (default false) : if true, skip checks for parameters settings</p>
         */
        public sealed class Builder : BuilderBase
        {
            private List<string> categoryList = new List<string>();
            private int encoderSeed = 1;

            public override IEncoder Build()
            {
                if (n == 0)
                {
                    throw new InvalidOperationException("\"N\" should be set");
                }
                if (w == 0)
                {
                    throw new InvalidOperationException("\"W\" should be set");
                }
                if (categoryList == null)
                {
                    throw new InvalidOperationException("Category List cannot be null");
                }
                SDRCategoryEncoder sdrCategoryEncoder = new SDRCategoryEncoder();
                sdrCategoryEncoder.Init(n, w, categoryList, name, encoderSeed, forced);

                return sdrCategoryEncoder;
            }

            public Builder CategoryList(List<string> categoryList)
            {
                this.categoryList = categoryList;
                return this;
            }

            public Builder EncoderSeed(int encoderSeed)
            {
                this.encoderSeed = encoderSeed;
                return this;
            }

            public override IBuilder Radius(double radius)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }


            public override IBuilder Resolution(double resolution)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }

            public override IBuilder Periodic(bool periodic)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }

            public override IBuilder ClipInput(bool clipInput)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }

            public override IBuilder MaxVal(double maxVal)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }

            public override IBuilder MinVal(double minVal)
            {
                throw new InvalidOperationException("Not supported for this SDRCategoryEncoder");
            }
        }
    }
}