using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.Serialization;
using System.Text;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Encoders
{
    /**
 * <p>
 * A scalar encoder encodes a numeric (floating point) value into an array of
 * bits.
 *
 * This class maps a scalar value into a random distributed representation that
 * is suitable as scalar input into the spatial pooler. The encoding scheme is
 * designed to replace a simple ScalarEncoder. It preserves the important
 * properties around overlapping representations. Unlike ScalarEncoder the min
 * and max range can be dynamically increased without any negative effects. The
 * only required parameter is resolution, which determines the resolution of
 * input values.
 *
 * Scalar values are mapped to a bucket. The class maintains a random
 * distributed encoding for each bucket. The following properties are maintained
 * by RandomDistributedEncoder:
 * </p>
 * <ol>
 * <li>Similar scalars should have high overlap. Overlap should decrease
 * smoothly as scalars become less similar. Specifically, neighboring bucket
 * indices must overlap by a linearly decreasing number of bits.
 *
 * <li>Dissimilar scalars should have very low overlap so that the SP does not
 * confuse representations. Specifically, buckets that are more than w indices
 * apart should have at most maxOverlap bits of overlap. We arbitrarily (and
 * safely) define "very low" to be 2 bits of overlap or lower.
 *
 * Properties 1 and 2 lead to the following overlap rules for buckets i and j:<br>
 *
 * <pre>
 * {@code
 * If abs(i-j) < w then:
 * 		overlap(i,j) = w - abs(i-j);
 * else:
 * 		overlap(i,j) <= maxOverlap;
 * }
 * </pre>
 *
 * <li>The representation for a scalar must not change during the lifetime of
 * the object. Specifically, as new buckets are created and the min/max range is
 * extended, the representation for previously in-range scalars and previously
 * created buckets must not change.
 * </ol>
 *
 *
 * @author Numenta
 * @author Anubhav Chaturvedi
 */

    [Serializable]
    public class RandomDistributedScalarEncoder : Encoder<double>, ISerializable
    {


        [NonSerialized]
        private static readonly ILog LOG = LogManager.GetLogger(typeof(RandomDistributedScalarEncoder));

        public const long DEFAULT_SEED = 42;

        // Mersenne Twister RNG, same as used with numpy.random
        MersenneTwister rng;

        int maxOverlap;
        double? offset;
        long seed;
        int minIndex;
        int maxIndex;
        int numRetry;

        [NonSerialized]
        ConcurrentDictionary<int, List<int>> bucketMap;


        private RandomDistributedScalarEncoder()
        {
        }

        public RandomDistributedScalarEncoder(SerializationInfo info, StreamingContext context)
            : this()
        {
            rng = (MersenneTwister)info.GetValue(nameof(rng), typeof(MersenneTwister));
            maxOverlap = info.GetInt32(nameof(maxOverlap));
            maxBuckets = info.GetInt32(nameof(maxBuckets));
            offset = info.GetValue(nameof(offset), typeof(double?)) != null ? info.GetDouble(nameof(offset)) : null;
            seed = info.GetInt64(nameof(seed));
            minIndex = info.GetInt32(nameof(minIndex));
            maxIndex = info.GetInt32(nameof(maxIndex));
            numRetry = info.GetInt32(nameof(numRetry));

            foreach (var entry in info)
            {
                if (entry.Name.StartsWith("dict_"))
                {
                    if (bucketMap == null)
                    {
                        bucketMap = new ConcurrentDictionary<int, List<int>>();
                    }

                    var key = int.Parse(entry.Name.Replace("dict_", string.Empty));
                    var val = entry.Value as List<int>;
                    bucketMap.TryAdd(key, val);
                }
            }
        }

        public void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            info.AddValue(nameof(rng), rng, rng.GetType());
            info.AddValue(nameof(maxOverlap), maxOverlap);
            info.AddValue(nameof(maxBuckets), maxBuckets);
            info.AddValue(nameof(offset), offset);
            info.AddValue(nameof(seed), seed);
            info.AddValue(nameof(minIndex), minIndex);
            info.AddValue(nameof(maxIndex), maxIndex);
            info.AddValue(nameof(numRetry), numRetry);

            if (bucketMap != null)
            {
                foreach (var pair in bucketMap)
                {
                    info.AddValue($"dict_{pair.Key}", pair.Value);
                }
            }
        }

        public static IBuilder GetBuilder()
        {
            return new Builder();
        }

        /**
         * Perform validation on state parameters and proceed with initialization of
         * the encoder.
         *
         * @//throws InvalidOperationException
         *             //throws {@code InvalidOperationException} containing appropriate
         *             message if some validation fails.
         */
        public void Init() ////throws InvalidOperationException
        {
            if (GetW() <= 0 || GetW() % 2 == 0)
            {
                throw new InvalidOperationException(
                    "W must be an odd positive int (to eliminate centering difficulty)");
            }


            SetHalfWidth((GetW() - 1) / 2);

            if (GetResolution() <= 0)
            {
                throw new InvalidOperationException(
                    "Resolution must be a positive number");
            }

            if (n <= 6 * GetW())
            {
                throw new InvalidOperationException(
                    "n must be strictly greater than 6*w. For good results we "
                    + "recommend n be strictly greater than 11*w.");
            }


            InitEncoder(GetResolution(), GetW(), GetN(), GetOffset(), GetSeed());
        }

        /**
         * Perform the initialization of the encoder.
         *
         * @param resolution
         * @param w
         * @param n
         * @param offset
         * @param seed
         */
        // TODO why are none of these parameters used..?
        public void InitEncoder(double resolution, int w, int n, double? offset, long seed)
        {
            rng = (seed == -1) ? new MersenneTwister(DEFAULT_SEED) : new MersenneTwister(seed);

            InitializeBucketMap(GetMaxBuckets(), GetOffset());

            if (string.IsNullOrWhiteSpace(GetName()))
            {
                SetName("[" + GetResolution() + "]");
            }

            // TODO reduce logging level?
            LOG.Debug(this.ToString());
        }

        /**
         * Initialize the bucket map assuming the given number of maxBuckets.
         *
         * @param maxBuckets
         * @param offset
         */
        public void InitializeBucketMap(int maxBuckets, double? offset)
        {
            /*
             * The first bucket index will be _maxBuckets / 2 and bucket indices
             * will be allowed to grow lower or higher as long as they don't become
             * negative. _maxBuckets is required because the current CLA Classifier
             * assumes bucket indices must be non-negative. This normally does not
             * need to be changed but if altered, should be Set to an even number.
             */

            SetMaxBuckets(maxBuckets);

            SetMinIndex(maxBuckets / 2);
            SetMaxIndex(maxBuckets / 2);

            /*
             * The scalar offset used to map scalar values to bucket indices. The
             * middle bucket will correspond to numbers in the range
             * [offset-resolution/2, offset+resolution/2). The bucket index for a
             * number x will be: maxBuckets/2 + int( round( (x-offset)/resolution )
             * )
             */
            SetOffset(offset);

            /*
             * This HashMap maps a bucket index into its bit representation We
             * initialize the HashMap with a single bucket with index 0
             */
            bucketMap = new ConcurrentDictionary<int, List<int>>();
            // generate the random permutation
            List<int> temp = new List<int>(GetN());
            for (int i = 0; i < GetN(); i++)
                temp.Add(i);
            temp.Shuffle(rng);
            //java.util.Collections.shuffle(temp, rng);
            bucketMap.TryAdd(GetMinIndex(), temp.SubList(0, GetW()));

            // How often we need to retry when generating valid encodings
            SetNumRetry(0);
        }

        /**
         * Create the given bucket index. Recursively create as many in-between
         * bucket indices as necessary.
         *
         * @param index the index at which bucket needs to be created
         * @//throws InvalidOperationException
         */
        public void CreateBucket(int index) ////throws InvalidOperationException
        {
            if (index < GetMinIndex())
            {
                if (index == GetMinIndex() - 1)
                {
                    /*
                     * Create a new representation that has exactly w-1 overlapping
                     * bits as the min representation
                     */
                    bucketMap.TryAdd(index, NewRepresentation(GetMinIndex(), index));
                    SetMinIndex(index);
                }
                else {
                    // Recursively create all the indices above and then this index
                    CreateBucket(index + 1);
                    CreateBucket(index);
                }
            }
            else {
                if (index == GetMaxIndex() + 1)
                {
                    /*
                     * Create a new representation that has exactly w-1 overlapping
                     * bits as the max representation
                     */
                    bucketMap.TryAdd(index, NewRepresentation(GetMaxIndex(), index));
                    SetMaxIndex(index);
                }
                else {
                    // Recursively create all the indices below and then this index
                    CreateBucket(index - 1);
                    CreateBucket(index);
                }
            }
        }

        /**
         * Get a new representation for newIndex that overlaps with the
         * representation at index by exactly w-1 bits
         *
         * @param index
         * @param newIndex
         * @//throws InvalidOperationException
         */
        public List<int> NewRepresentation(int index, int newIndex)

        ////throws InvalidOperationException
        {
            List<int> newRepresentation = new List<int>(
                bucketMap[index]);

            /*
             * Choose the bit we will replace in this representation. We need to
             * shift this bit deterministically. If this is always chosen randomly
             * then there is a 1 in w chance of the same bit being replaced in
             * neighboring representations, which is fairly high
             */

            int ri = newIndex % GetW();

            // Now we choose a bit such that the overlap rules are satisfied.
            int newBit = rng.NextInt(GetN());
            newRepresentation[ri] = newBit;
            while (bucketMap[index].Contains(newBit)
                    || !NewRepresentationOk(newRepresentation, newIndex))
            {
                SetNumRetry(GetNumRetry() + 1);
                newBit = rng.NextInt(GetN());
                newRepresentation[ri] = newBit;
            }

            return newRepresentation;
        }

        /**
         * Check if this new candidate representation satisfies all our
         * overlap rules. Since we know that neighboring representations differ by
         * at most one bit, we compute running overlaps.
         *
         * @param newRep Encoded SDR to be considered
         * @param newIndex The index being considered
         * @return {@code true} if newRep satisfies all our overlap rules
         * @//throws InvalidOperationException
         */
        public bool NewRepresentationOk(List<int> newRep, int newIndex)
        {
            if (newRep.Count != GetW())
                return false;
            if (newIndex < GetMinIndex() - 1 || newIndex > GetMaxIndex() + 1)
            {
                throw new InvalidOperationException(
                        "newIndex must be within one of existing indices");
            }

            // A binary representation of newRep. We will use this to test
            // containment
            bool[] newRepBinary = new bool[GetN()];
            Arrays.Fill(newRepBinary, false);
            foreach (int index in newRep)
                newRepBinary[index] = true;

            // Midpoint
            int midIdx = GetMaxBuckets() / 2;

            // Start by checking the overlap at minIndex
            int runningOverlap = CountOverlap(bucketMap[GetMinIndex()], newRep);
            if (!OverlapOK(GetMinIndex(), newIndex, runningOverlap))
                return false;

            // Compute running overlaps all the way to the midpoint
            for (int i = GetMinIndex() + 1; i < midIdx + 1; i++)
            {
                // This is the bit that is going to change
                int newBit = (i - 1) % GetW();

                // Update our running overlap
                if (newRepBinary[bucketMap[i - 1][newBit]])
                    runningOverlap--;
                if (newRepBinary[bucketMap[i][newBit]])
                    runningOverlap++;

                // Verify our rules
                if (!OverlapOK(i, newIndex, runningOverlap))
                    return false;
            }

            // At this point, runningOverlap contains the overlap for midIdx
            // Compute running overlaps all the way to maxIndex
            for (int i = midIdx + 1; i <= GetMaxIndex(); i++)
            {
                int newBit = i % GetW();

                // Update our running overlap
                if (newRepBinary[bucketMap[i - 1][newBit]])
                    runningOverlap--;
                if (newRepBinary[bucketMap[i][newBit]])
                    runningOverlap++;

                // Verify our rules
                if (!OverlapOK(i, newIndex, runningOverlap))
                    return false;
            }
            return true;
        }

        /**
         * Get the overlap between two representations. rep1 and rep2 are
         * {@link List} of non-zero indices.
         *
         * @param rep1 The first representation for overlap calculation
         * @param rep2 The second representation for overlap calculation
         * @return The number of 'on' bits that overlap
         */
        public int CountOverlap(List<int> rep1, List<int> rep2)
        {
            int overlap = 0;
            foreach (int index in rep1)
            {
                foreach (int index2 in rep2)
                    if (index == index2)
                        overlap++;
            }
            return overlap;
        }

        /**
         * Get the overlap between two representations. rep1 and rep2 are arrays
         * of non-zero indices.
         *
         * @param rep1 The first representation for overlap calculation
         * @param rep2 The second representation for overlap calculation
         * @return The number of 'on' bits that overlap
         */
        public int CountOverlap(int[] rep1, int[] rep2)
        {
            int overlap = 0;
            foreach (int index in rep1)
            {
                foreach (int index2 in rep2)
                    if (index == index2)
                        overlap++;
            }
            return overlap;
        }

        /**
         * Check if the given overlap between bucket indices i and j are acceptable.
         *
         * @param i The index of the bucket to be compared
         * @param j The index of the bucket to be compared
         * @param overlap The overlap between buckets at index i and j
         * @return {@code true} if overlap is acceptable, else {@code false}
         */
        public bool OverlapOK(int i, int j, int overlap)
        {
            if (Math.Abs(i - j) < GetW() && overlap == (GetW() - Math.Abs(i - j)))
                return true;
            if (Math.Abs(i - j) >= GetW() && overlap <= GetMaxOverlap())
                return true;

            return false;
        }

        /**
         * Check if the overlap between the buckets at indices i and j are
         * acceptable. The overlap is calculate from the bucketMap.
         *
         * @param i The index of the bucket to be compared
         * @param j The index of the bucket to be compared
         * @return {@code true} if the given overlap is acceptable, else {@code false}
         * @//throws InvalidOperationException
         */
        public bool OverlapOK(int i, int j) //throws InvalidOperationException
        {
            return OverlapOK(i, j, CountOverlapIndices(i, j));
        }

        /**
         * Get the overlap between bucket at indices i and j
         *
         * @param i The index of the bucket
         * @param j The index of the bucket
         * @return the overlap between bucket at indices i and j
         * @//throws InvalidOperationException
         */
        public int CountOverlapIndices(int i, int j) //throws InvalidOperationException
        {
            bool containsI = bucketMap.ContainsKey(i);
            bool containsJ = bucketMap.ContainsKey(j);
            if (containsI && containsJ)
            {
                List<int> rep1 = bucketMap[i];
                List<int> rep2 = bucketMap[j];
                return CountOverlap(rep1, rep2);
            }
            else if (!containsI && !containsJ)
                throw new InvalidOperationException("index " + i + " and " + j + " don't exist");
            else if (!containsI)
                throw new InvalidOperationException("index " + i + " doesn't exist");
            else
                throw new InvalidOperationException("index " + j + " doesn't exist");
        }

        /**
         * Given a bucket index, return the list of indices of the 'on' bits. If the
         * bucket index does not exist, it is created. If the index falls outside
         * our range we clip it.
         *
         * @param index The bucket index
         * @return The list of active bits in the representation
         * @//throws InvalidOperationException
         */
        public List<int> MapBucketIndexToNonZeroBits(int index)

        //throws InvalidOperationException
        {
            if (index < 0)
                index = 0;

            if (index >= GetMaxBuckets())
                index = GetMaxBuckets() - 1;

            if (!bucketMap.ContainsKey(index))
            {
                LOG.Debug(string.Format("Adding additional buckets to handle index={0} ", index));
                CreateBucket(index);
            }
            return bucketMap[index];
        }

        /**
         * {@inheritDoc}
         */


        public override int[] GetBucketIndices(double x)
        {
            if (double.IsNaN(x))
                x = Encoder<double>.SENTINEL_VALUE_FOR_MISSING_DATA;

            int test = x.CompareTo(SENTINEL_VALUE_FOR_MISSING_DATA);
            if (test == 0)
                return new int[0];

            if (GetOffset() == null)
                SetOffset(x);

            /*
             * Difference in the round function behavior for Python and Java In
             * Python, the absolute value is rounded up and sign is applied in Java,
             * value is rounded to next biggest int
             *
             * so for Python, round(-0.5) => -1.0 whereas in Java, Math.round(-0.5)
             * => 0.0
             */
            double deltaIndex = (x - GetOffset().GetValueOrDefault()) / GetResolution();
            int sign = (int)(deltaIndex / Math.Abs(deltaIndex));
            int bucketIdx = (GetMaxBuckets() / 2)
                    + (sign * (int)Math.Round(Math.Abs(deltaIndex)));

            if (bucketIdx < 0)
                bucketIdx = 0;
            else if (bucketIdx >= GetMaxBuckets())
                bucketIdx = GetMaxBuckets() - 1;

            int[] bucketIdxArray = new int[1];
            bucketIdxArray[0] = bucketIdx;
            return bucketIdxArray;
        }

        /**
         * {@inheritDoc}
         */

        public override int GetWidth()
        {
            return GetN();
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

        public override void SetLearning(bool learningEnabled)
        {
            SetLearningEnabled(learningEnabled);
        }

        /**
         * {@inheritDoc}
         */

        public override List<Tuple> GetDescription()
        {
            string name = GetName();
            if (string.IsNullOrEmpty(name))
                SetName("[" + GetResolution() + "]");
            name = GetName();

            return new List<Tuple>(new[] { new Tuple(name, 0) });
        }

        /**
         * @return maxOverlap for this RDSE
         */
        public int GetMaxOverlap()
        {
            return maxOverlap;
        }

        /**
         * @return the maxBuckets for this RDSE
         */
        public override int GetMaxBuckets()
        {
            return maxBuckets;
        }

        /**
         * @return the seed for the random number generator
         */
        public long GetSeed()
        {
            return seed;
        }

        /**
         * @return the offset
         */
        public double? GetOffset()
        {
            return offset;
        }

        private int GetMinIndex()
        {
            return minIndex;
        }

        private int GetMaxIndex()
        {
            return maxIndex;
        }

        /**
         * @return the number of retry to create new bucket
         */
        public int GetNumRetry()
        {
            return numRetry;
        }

        /**
         * @param maxOverlap The maximum permissible overlap between representations
         */
        public void SetMaxOverlap(int maxOverlap)
        {
            this.maxOverlap = maxOverlap;
        }

        /**
         * @param maxBuckets the new maximum number of buckets allowed
         */
        public override void SetMaxBuckets(int maxBuckets)
        {
            this.maxBuckets = maxBuckets;
        }

        /**
         * @param seed
         */
        public void SetSeed(long seed)
        {
            this.seed = seed;
        }

        /**
         * @param offset
         */
        public void SetOffset(double? offset)
        {
            this.offset = offset;
        }

        private void SetMinIndex(int minIndex)
        {
            this.minIndex = minIndex;
        }

        private void SetMaxIndex(int maxIndex)
        {
            this.maxIndex = maxIndex;
        }

        /**
         * @param numRetry New number of retries for new representation
         */
        public void SetNumRetry(int numRetry)
        {
            this.numRetry = numRetry;
        }


        public override string ToString()
        {
            // TODO don't mix StringBuilder appending with String concatenation
            StringBuilder dumpString = new StringBuilder(50);
            dumpString.Append("RandomDistributedScalarEncoder:\n");
            dumpString.Append("  minIndex: " + GetMinIndex() + "\n");
            dumpString.Append("  maxIndex: " + GetMaxIndex() + "\n");
            dumpString.Append("  w: " + GetW() + "\n");
            dumpString.Append("  n: " + GetWidth() + "\n");
            dumpString.Append("  resolution: " + GetResolution() + "\n");
            dumpString.Append("  offset: " + GetOffset() + "\n");
            dumpString.Append("  numTries: " + GetNumRetry() + "\n");
            dumpString.Append("  name: " + GetName() + "\n");
            dumpString.Append("  buckets : \n");
            foreach (int index in bucketMap.Keys)
            {
                dumpString.Append("  [ " + index + " ]: "
                        + Arrays.DeepToString(bucketMap[index].ToArray())
                        + "\n");
            }
            return dumpString.ToString();
        }



        /**
         * <p>
         * Returns a {@link Encoder.Builder} for constructing
         * {@link RandomDistributedScalarEncoder}s.
         * </p>
         * <p>
         * The base class architecture is put together in such a way where
         * boilerplate initialization can be kept to a minimum for implementing
         * subclasses, while avoiding the mistake-proneness of extremely long
         * argument lists.
         * </p>
         *
         * @author Anubhav Chaturvedi
         */
        public class Builder : BuilderBase
        {
            private int maxOverlap;
            private double? offset;
            private long seed;
            private int minIndex;
            private int maxIndex;

            internal Builder()
            {
                N(400);
                W(21);
                seed = 42;
                maxBuckets = 1000;
                maxOverlap = 2;
                offset = null;
            }

            internal Builder(int n, int w) : this()
            {
                N(n);
                W(w);
            }


            public override IEncoder Build()
            {
                // Must be instantiated so that super class can initialize
                // boilerplate variables.
                encoder = new RandomDistributedScalarEncoder();

                // Call super class here
                RandomDistributedScalarEncoder partialBuild = (RandomDistributedScalarEncoder)base.Build();

                ////////////////////////////////////////////////////////
                //  Implementing classes would do Setting of specific //
                //  vars here together with any sanity checking       //
                ////////////////////////////////////////////////////////
                partialBuild.SetSeed(seed);
                partialBuild.SetMaxOverlap(maxOverlap);
                partialBuild.SetMaxBuckets(maxBuckets);
                partialBuild.SetOffset(offset);
                partialBuild.SetNumRetry(0);

                partialBuild.Init();

                return partialBuild;
            }

            public IBuilder SetOffset(double? offset)
            {
                this.offset = offset.GetValueOrDefault();
                return this;
            }

            public override IBuilder MaxBuckets(int maxBuckets)
            {
                this.maxBuckets = maxBuckets;
                return this;
            }

            public IBuilder SetMaxOverlap(int maxOverlap)
            {
                this.maxOverlap = maxOverlap;
                return this;
            }

            public IBuilder SetSeed(long seed)
            {
                this.seed = seed;
                return this;
            }
        }

        /**
         * {@inheritDoc}
         */
        public override void EncodeIntoArray(double inputData, int[] output)
        {
            int[] bucketIdx = GetBucketIndices(inputData);
            Arrays.Fill(output, 0);

            if (bucketIdx.Length == 0)
                return;

            if (bucketIdx[0] != int.MinValue)
            {
                List<int> indices;
                try
                {
                    indices = MapBucketIndexToNonZeroBits(bucketIdx[0]);
                    foreach (int index in indices)
                        output[index] = 1;
                }
                catch (InvalidOperationException e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        public override void EncodeIntoArrayUntyped(object o, int[] tempArray)
        {
            if (o is string)
            {
                var d = double.Parse((string)o, NumberFormatInfo.InvariantInfo);
                EncodeIntoArray(d, tempArray);
            }
            else
            {
                EncodeIntoArray((double)o, tempArray);
            }
        }

        /**
         * {@inheritDoc}
         */
        public override List<S> GetBucketValues<S>(Type returnType)
        {
            return new List<S>((ICollection<S>)this.bucketMap.Keys);
        }

        /**
         * {@inheritDoc}
         */

        public override HashSet<FieldMetaType> GetDecoderOutputFieldTypes()
        {
            return new HashSet<FieldMetaType>(new[] { FieldMetaType.Float, FieldMetaType.Integer });
        }
    }
}