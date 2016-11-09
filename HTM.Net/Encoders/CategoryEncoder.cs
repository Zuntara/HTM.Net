using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
     * Encodes a list of discrete categories (described by strings), that aren't
     * related to each other, so we never emit a mixture of categories.
     *
     * The value of zero is reserved for "unknown category"
     *
     * Internally we use a ScalarEncoder with a radius of 1, but since we only encode
     * integers, we never get mixture outputs.
     *
     * The SDRCategoryEncoder (not yet implemented in Java) uses a different method to encode categories
     *
     * <P>
     * Typical usage is as follows:
     * <PRE>
     * CategoryEncoder.Builder builder =  ((CategoryEncoder.Builder)CategoryEncoder.builder())
     *      .w(3)
     *      .radius(0.0)
     *      .minVal(0.0)
     *      .maxVal(8.0)
     *      .periodic(false)
     *      .forced(true);
     *
     * CategoryEncoder encoder = builder.build();
     *
     * <b>Above values are <i>not</i> an example of "sane" values.</b>
     *
     * </PRE>
     *
     * @author David Ray
     * @see ScalarEncoder
     * @see Encoder
     * @see EncoderResult
     * @see Parameters
     */
    [Serializable]
    public class CategoryEncoder : Encoder<string>
    {
        [NonSerialized]
        private static readonly ILog LOG = LogManager.GetLogger(typeof(CategoryEncoder));

        protected int ncategories;

        protected Map<string, int> categoryToIndex = new Map<string, int>();
        protected Map<int, string> indexToCategory = new Map<int, string>();

        protected List<string> categoryList;

        protected int width;

        private ScalarEncoder scalarEncoder;

        /**
         * Constructs a new {@code CategoryEncoder}
         */
        private CategoryEncoder()
        {
        }

        /**
         * Returns a builder for building CategoryEncoders.
         * This builder may be reused to produce multiple builders
         *
         * @return a {@code CategoryEncoder.Builder}
         */
        public static IBuilder GetBuilder()
        {
            return new Builder();
        }


        public void Init()
        {
            // number of categories includes zero'th category: "unknown"
            ncategories = categoryList == null ? 0 : categoryList.Count + 1;
            minVal = 0;
            maxVal = ncategories - 1;

            try
            {
                scalarEncoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                    .N(this.n)
                    .W(this.w)
                    .Radius(this.radius)
                    .MinVal(this.minVal)
                    .MaxVal(this.maxVal)
                    .Periodic(this.periodic)
                    .Forced(this.forced).Build();
            }
            catch (Exception e)
            {
                string msg = null;
                int idx = -1;
                if ((idx = (msg = e.Message).IndexOf("ScalarEncoder")) != -1)
                {
                    msg = msg.Substring(0, idx) + "CategoryEncoder";
                    throw new InvalidOperationException(msg);
                }
            }

            indexToCategory.Add(0, "<UNKNOWN>");
            if (categoryList != null && categoryList.Any())
            {
                int len = categoryList.Count;
                for (int i = 0; i < len; i++)
                {
                    categoryToIndex.Add(categoryList[i], i + 1);
                    indexToCategory.Add(i + 1, categoryList[i]);
                }
            }


            width = n = w * ncategories;

            //TODO this is what the CategoryEncoder was doing before I added the ScalarEncoder delegate.
            //I'm concerned because we're changing n without calling init again on the scalar encoder.
            //In other words, if I move the scalarEncoder = ...build() from to here, the test cases fail
            //which indicates significant fragility and at some level a violation of encapsulation.
            //scalarEncoder.N = n;
            scalarEncoder.SetN(n);



            if (GetWidth() != width)
            {
                throw new InvalidOperationException(
                    "Width != w (num bits to represent output item) * #categories");
            }

            description.Add(new Tuple(name, 0));
        }

        /**
         * {@inheritDoc}
         */
        public override List<double> GetScalars(string d)
        {
            return new List<double>(new double[] { categoryToIndex[d] });
        }

        /**
         * {@inheritDoc}
         */
        public override int[] GetBucketIndices(string input)
        {
            if (input == null) return null;
            return scalarEncoder.GetBucketIndices(categoryToIndex[input]);
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(string input, int[] output)
        {
            string val = null;
            double value = 0;
            if (input == null)
            {
                val = "<missing>";
            }
            else
            {
                value = categoryToIndex[input];
                value = value == 0 ? 0 : value;
                scalarEncoder.EncodeIntoArray(value, output);
            }

            LOG.Debug(string.Format("input: {0}, val: {1}, value: {2}, output: {3}",
                        input, val, value, Arrays.ToString(output)));
        }


        public TResult Decode<TResult>(int[] encoded, string parentFieldName)
            where TResult : Tuple
        {
            return (TResult)Decode(encoded, parentFieldName);
        }
        /**
         * {@inheritDoc}
         */
        public override Tuple Decode(int[] encoded, string parentFieldName)
        {
            // Get the scalar values from the underlying scalar encoder
            DecodeResult result = (DecodeResult)scalarEncoder.Decode(encoded, parentFieldName);

            if (result.GetFields().Count == 0)
            {
                return result;
            }

            // Expect only 1 field
            if (result.GetFields().Count != 1)
            {
                throw new InvalidOperationException("Expecting only one field");
            }

            //Get the list of categories the scalar values correspond to and
            //  generate the description from the category name(s).
            Map<string, RangeList> fieldRanges = result.GetFields();
            List<MinMax> outRanges = new List<MinMax>();
            StringBuilder desc = new StringBuilder();
            foreach (string descripStr in fieldRanges.Keys)
            {
                MinMax minMax = fieldRanges[descripStr].GetRange(0);
                int minV = (int)Math.Round(minMax.Min());
                int maxV = (int)Math.Round(minMax.Max());
                outRanges.Add(new MinMax(minV, maxV));
                while (minV <= maxV)
                {
                    if (desc.Length > 0)
                    {
                        desc.Append(", ");
                    }
                    desc.Append(indexToCategory[minV]);
                    minV += 1;
                }
            }

            //Return result
            string fieldName;
            if (parentFieldName.Any())
            {
                fieldName = string.Format("{0}.{1}", parentFieldName, name);
            }
            else
            {
                fieldName = name;
            }

            Map<string, RangeList> retVal = new Map<string, RangeList>();
            retVal.Add(fieldName, new RangeList(outRanges, desc.ToString()));

            return new DecodeResult(retVal, new[] { fieldName }.ToList());
        }

        /**
         * {@inheritDoc}
         */
        public override List<double> ClosenessScores(List<double> expValues, List<double> actValues, bool fractional)
        {
            double expValue = expValues[0];
            double actValue = actValues[0];

            double closeness = expValue == actValue ? 1.0 : 0;
            if (!fractional) closeness = 1.0 - closeness;

            return new List<double>(new[] { closeness });
        }

        /**
         * Returns a list of items, one for each bucket defined by this encoder.
         * Each item is the value assigned to that bucket, this is the same as the
         * EncoderResult.value that would be returned by getBucketInfo() for that
         * bucket and is in the same format as the input that would be passed to
         * encode().
         *
         * This call is faster than calling getBucketInfo() on each bucket individually
         * if all you need are the bucket values.
         *
         * @param	returnType 		class type parameter so that this method can return encoder
         * 							specific value types
         *
         * @return list of items, each item representing the bucket value for that
         *        bucket.
         */
        public override List<T> GetBucketValues<T>(Type t)
        {
            if (bucketValues == null)
            {
                SparseObjectMatrix<int[]> topDownMapping = scalarEncoder.GetTopDownMapping();
                int numBuckets = topDownMapping.GetMaxIndex() + 1;
                bucketValues = new List<string>();
                for (int i = 0; i < numBuckets; i++)
                {
                    ((List<string>)bucketValues).Add((string)GetBucketInfo(new int[] { i })[0].GetValue());
                }
            }

            return (List<T>)bucketValues;
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> GetBucketInfo(int[] buckets)
        {
            // For the category encoder, the bucket index is the category index
            List<EncoderResult> bucketInfo = scalarEncoder.GetBucketInfo(buckets);

            int categoryIndex = (int)Math.Round((double)bucketInfo[0].GetValue());
            string category = indexToCategory[categoryIndex];

            bucketInfo[0] = new EncoderResult(category, categoryIndex, bucketInfo[0].GetEncoding());
            return bucketInfo;
        }

        /**
         * {@inheritDoc}
         */
        public override List<EncoderResult> TopDownCompute(int[] encoded)
        {
            //Get/generate the topDown mapping table
            SparseObjectMatrix<int[]> topDownMapping = scalarEncoder.GetTopDownMapping();
            // See which "category" we match the closest.
            int category = ArrayUtils.Argmax(RightVecProd(topDownMapping, encoded));
            return GetBucketInfo(new int[] { category });
        }

        public List<string> GetCategoryList()
        {
            return categoryList;
        }

        public void SetCategoryList(List<string> catList)
        {
            this.categoryList = catList;
        }

        /**
         * Returns a {@link EncoderBuilder} for constructing {@link CategoryEncoder}s
         *
         * The base class architecture is put together in such a way where boilerplate
         * initialization can be kept to a minimum for implementing subclasses, while avoiding
         * the mistake-proneness of extremely long argument lists.
         *
         * @see ScalarEncoder.Builder#setStuff(int)
         */
        public class Builder : BuilderBase
        {

            private List<string> categoryList;

            internal Builder() { }

            public override IEncoder Build()
            {
                //Must be instantiated so that super class can initialize
                //boilerplate variables.
                encoder = new CategoryEncoder();

                //Call super class here
                base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////
                if (categoryList == null)
                {
                    throw new InvalidOperationException("Category List cannot be null");
                }
                //Set CategoryEncoder specific field
                ((CategoryEncoder)encoder).SetCategoryList(this.categoryList);
                //Call init
                ((CategoryEncoder)encoder).Init();

                return (CategoryEncoder)encoder;
            }

            /**
             * Never called - just here as an example of specialization for a specific
             * subclass of Encoder.Builder
             *
             * Example specific method!!
             *
             * @param stuff
             * @return
             */
            public Builder CategoryList(List<string> categoryList)
            {
                this.categoryList = categoryList;
                return this;
            }
        }

        public override int GetWidth()
        {
            return GetN();
        }

        public override bool IsDelta()
        {
            return false;
        }


    }
}