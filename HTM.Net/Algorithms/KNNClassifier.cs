using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Text;
using HTM.Net.Model;
using HTM.Net.Util;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Newtonsoft.Json.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Algorithms
{
    // https://github.com/numenta/nupic/blob/1410fcc4e31dc0907130460bcf6085a4f3badaeb/src/nupic/algorithms/KNNClassifier.py

    /// <summary>
    /// Nearest Neighbor Classifier
    ///
    ///  This class implements NuPIC's k Nearest Neighbor Classifier. KNN is very
    ///  useful as a basic classifier for many situations.This implementation contains
    ///  many enhancements that are useful for HTM experiments. These enhancements
    ///  include an optimized C++ class for sparse vectors, support for continuous
    ///  online learning, support for various distance methods(including Lp-norm and
    ///  raw overlap), support for performing SVD on the input vectors(very useful for
    ///  large vectors), support for a fixed-size KNN, and a mechanism to store custom
    ///  ID's for each vector.
    ///  :param k: (int)The number of nearest neighbors used in the classification
    ///      of patterns.Must be odd.
    ///  :param exact: (boolean)If true, patterns must match exactly when assigning
    ///      class labels
    ///  : param distanceNorm: (int) When distance method is "norm", this specifies
    ///       the p value of the Lp-norm
    ///   :param distanceMethod: (string) The method used to compute distance between
    /// 
    ///       input patterns and prototype patterns.The possible options are:
    ///      - ``norm``: When distanceNorm is 2, this is the euclidean distance,
    ///               When distanceNorm is 1, this is the manhattan distance
    ///               In general: sum(abs(x-proto) ^ distanceNorm) ^ (1/distanceNorm)
    ///              The distances are normalized such that farthest prototype from
    ///               a given input is 1.0.
    ///      - ``rawOverlap``: Only appropriate when inputs are binary. This computes:
    ///              (width of the input) - (# bits of overlap between input
    ///              and prototype).
    ///      - ``pctOverlapOfInput``: Only appropriate for binary inputs. This computes
    ///              1.0 - (# bits overlap between input and prototype) /
    ///                      (# ON bits in input)
    ///      - ``pctOverlapOfProto``: Only appropriate for binary inputs. This computes
    ///              1.0 - (# bits overlap between input and prototype) /
    ///                      (# ON bits in prototype)
    ///      - ``pctOverlapOfLarger``: Only appropriate for binary inputs. This computes
    ///              1.0 - (# bits overlap between input and prototype) /
    ///                      max(# ON bits in input, # ON bits in prototype)
    ///  :param distThreshold: (float) A threshold on the distance between learned
    ///       patterns and a new pattern proposed to be learned.The distance must be
    ///      greater than this threshold in order for the new pattern to be added to
    ///      the classifier's memory.
    ///  :param doBinarization: (boolean) If True, then scalar inputs will be
    ///      binarized.
    ///  :param binarizationThreshold: (float) If doBinarization is True, this
    ///      specifies the threshold for the binarization of inputs
    ///  :param useSparseMemory: (boolean) If True, classifier will use a sparse
    ///      memory matrix
    ///  :param sparseThreshold: (float) If useSparseMemory is True, input variables
    ///      whose absolute values are less than this threshold will be stored as
    ///      zero
    ///  :param relativeThreshold: (boolean) Flag specifying whether to multiply
    ///      sparseThreshold by max value in input
    ///  :param numWinners: (int) Number of elements of the input that are stored. If
    ///      0, all elements are stored
    ///  :param numSVDSamples: (int) Number of samples the must occur before a SVD
    ///      (Singular Value Decomposition) transformation will be performed.If 0,
    ///      the transformation will never be performed
    ///  :param numSVDDims: (string) Controls dimensions kept after SVD
    ///      transformation.If "adaptive", the number is chosen automatically
    ///  :param fractionOfMax: (float) If numSVDDims is "adaptive", this controls the
    ///      smallest singular value that is retained as a fraction of the largest
    ///      singular value
    ///  :param verbosity: (int) Console verbosity level where 0 is no output and
    ///      larger integers provide increasing levels of verbosity
    ///  :param maxStoredPatterns: (int) Limits the maximum number of the training
    ///      patterns stored. When KNN learns in a fixed capacity mode, the unused
    ///      patterns are deleted once the number of stored patterns is greater than
    ///      maxStoredPatterns.A value of -1 is no limit
    ///  :param replaceDuplicates: (bool) A boolean flag that determines whether,
    ///      during learning, the classifier replaces duplicates that match exactly,
    ///      even if distThreshold is 0. Should be True for online learning
    ///  :param cellsPerCol: (int) If >= 1, input is assumed to be organized into
    ///      columns, in the same manner as the temporal memory AND whenever a new
    ///      prototype is stored, only the start cell(first cell) is stored in any
    ///     bursting column
    ///  :param minSparsity: (float) If useSparseMemory is set, only vectors with
    ///      sparsity >= minSparsity will be stored during learning.A value of 0.0
    ///      implies all vectors will be stored. A value of 0.1 implies only vectors
    ///      with at least 10% sparsity will be stored
    /// </summary>
    [Serializable]
    public class KNNClassifier : Persistable, IClassifier
    {
        #region Fields

        /// <summary>
        /// The bit's learning iteration. This is updated each time store() gets called on this bit.
        /// </summary>
        private int _learnIteration;
        /// <summary>
        /// This contains the offset between the recordNum (provided by caller) and
        /// learnIteration (internal only, always starts at 0).
        /// </summary>
        private int _recordNumMinusLearnIteration = -1;
        /// <summary>
        /// History of the last _maxSteps activation patterns. We need to keep
        /// these so that we can associate the current iteration's classification
        /// with the activationPattern from N steps ago
        /// </summary>
        private Deque<(int iteration, int[] patternNz)> _patternNzHistory;
        /// <summary>
        /// This keeps track of the actual value to use for each bucket index. We
        /// start with 1 bucket, no actual value so that the first infer has something
        /// to return
        /// </summary>
        private readonly List<object> _actualValues = new List<object>();

        private string g_debugPrefix = "KNNClassifier";

        /// <summary>
        /// The number of nearest neighbors used in the classification of patterns. <b>Must be odd</b>
        /// </summary>
        private int _k = 1;
        /// <summary>
        /// If true, patterns must match exactly when assigning class labels
        /// </summary>
        private bool _exact = false;
        /// <summary>
        /// When distance method is "norm", this specifies the p value of the Lp-norm
        /// </summary>
        private double _distanceNorm = 2.0;
        /// <summary>
        /// The method used to compute distance between input patterns and prototype patterns.
        /// see <see cref="DistanceMethod"/>
        /// </summary>
        private DistanceMethod distanceMethod = DistanceMethod.Norm;

        /// <summary>
        /// A threshold on the distance between learned
        /// patterns and a new pattern proposed to be learned.The distance must be
        /// greater than this threshold in order for the new pattern to be added to
        /// the classifier's memory
        /// </summary>
        private double distanceThreshold = 0.0;
        /// <summary>
        /// If True, then scalar inputs will be binarized.
        /// </summary>
        private bool doBinarization = false;
        /// <summary>
        /// If doBinarization is True, this specifies the threshold for the binarization of inputs
        /// </summary>
        private double binarizationThreshold = 0.5;
        /// <summary>
        /// If True, classifier will use a sparse memory matrix
        /// </summary>
        private bool useSparseMemory = true;
        /// <summary>
        /// If useSparseMemory is True, input variables whose absolute values are
        /// less than this threshold will be stored as zero
        /// </summary>
        private double sparseThreshold = 0.1;
        /// <summary>
        /// Flag specifying whether to multiply sparseThreshold by max value in input
        /// </summary>
        private bool relativeThreshold = false;
        /// <summary>
        /// Number of elements of the input that are stored. If 0, all elements are stored
        /// </summary>
        private int numWinners = 0;

        /// <summary>
        /// Number of samples the must occur before a SVD
        /// (Singular Value Decomposition) transformation will be performed.If 0,
        /// the transformation will never be performed
        /// </summary>
        private int? numSvdSamples = null;

        /// <summary>
        /// Controls dimensions kept after SVD transformation. If "adaptive", 
        /// the number is chosen automatically
        /// </summary>
        private int? numSvdDims = null;

        /// <summary>
        /// If numSVDDims is "adaptive", this controls the
        /// smallest singular value that is retained as a fraction of the largest
        /// singular value
        /// </summary>
        private double? fractionOfMax = null;
        /// <summary>
        /// Limits the maximum number of the training
        /// patterns stored.When KNN learns in a fixed capacity mode, the unused
        /// patterns are deleted once the number of stored patterns is greater than
        /// maxStoredPatterns.A value of - 1 is no limit
        /// </summary>
        private int maxStoredPatterns = -1;
        /**
         * A boolean flag that determines whether,
         * during learning, the classifier replaces duplicates that match exactly,
         * even if distThreshold is 0. Should be TRUE for online learning
         */
        private bool replaceDuplicates = false;
        /**
         * If >= 1, input is assumed to be organized into
         * columns, in the same manner as the temporal pooler AND whenever a new
         * prototype is stored, only the start cell (first cell) is stored in any
         * bursting column
         */
        private int cellsPerCol = 0;

        /// <summary>
        /// If useSparseMemory is set, only vectors with
        /// sparsity >= minSparsity will be stored during learning.A value of 0.0
        /// implies all vectors will be stored. A value of 0.1 implies only vectors
        /// with at least 10% sparsity will be stored
        /// </summary>
        private double minSparsity = 0.0;

        ///////////////////////////////////////////////////////
        //              Internal State Variables             //
        ///////////////////////////////////////////////////////
        private NearestNeighbor _memory;

        private int _iterationIdx = -1;

        private Vector<double> _protoSizes;
        private List<int> _categoryList = new List<int>();
        private List<int> _categoryRecencyList = new List<int>();
        private bool _finishedLearning, fixedCapacity;
        private bool _specificIndexTraining;
        private Map<int, int[]> _partitionIdMap = new Map<int, int[]>();
        private int _numPatterns;
        private List<int> _partitionIdList = new List<int>();
        private List<int> _nextTrainingIndices = null;

        // Used by PCA
        private Matrix<double> _vt;
        private double[][] _nc;
        private Vector<double> _mean;
        private Matrix<double> _a;
        private Vector<double> _s;
        private NearestNeighbor _M;

        #endregion

        #region Construction

        ///////////////////////////////////////////////////////
        //                    Construction                   //
        ///////////////////////////////////////////////////////

        /**
         * Privately constructs a {@code KNNClassifier}. 
         * This method is called by the
         */
        private KNNClassifier()
        {
            // History of the last _maxSteps activation patterns. We need to keep 
            // these so that we can associate the current iteration's classification 
            // with the activationPattern from N steps ago 
            _patternNzHistory = new Deque<(int iteration, int[] patternNz)>(10);
            Steps = new[] { 1 };
        }

        /**
         * Returns a {@link Builder} used to fully construct a {@code KNNClassifier}
         * @return
         */
        public static Builder GetBuilder()
        {
            return new KNNClassifier.Builder();
        }

        #endregion

        #region Core Methods

        /// <summary>
        /// Clears the state of the KNNClassifier.
        /// </summary>
        public void Clear()
        {
            _memory = null;
            _numPatterns = 0;
            _M = null;
            _categoryList = new List<int>();
            _partitionIdList = new List<int>();
            _partitionIdMap = new Map<int, int[]>();
            _finishedLearning = false;
            _iterationIdx = -1;

            if (maxStoredPatterns > 0)
            {
                Debug.Assert(useSparseMemory, "Fixed capacity KNN is implemented only in this sparse memory mode");
                fixedCapacity = true;
                _categoryRecencyList = new List<int>();
            }
            else
            {
                fixedCapacity = false;
            }
            _protoSizes = null;

            // used by PCA
            _s = null;
            _vt = null;
            _nc = null;
            _mean = null;

            // used by network builder
            _specificIndexTraining = false;
            _nextTrainingIndices = null;
        }

        /// <summary>
        /// Process one input sample. This method is called by the runtime engine.
        /// .. note::the number of input categories may vary, but the array size is
        /// fixed to the max number of categories allowed(by a lower region), so
        /// "unused" indices of the input category array are filled with - 1s.
        /// TODO: confusion matrix does not support multi - label classification
        /// :param inputs: (dict)mapping region input names to numpy.array values
        /// :param outputs: (dict)mapping region output names to numpy.arrays that
        /// should be populated with output values by this method
        /// https://github.com/numenta/nupic/blob/b9ebedaf54f49a33de22d8d44dff7c765cdb5548/src/nupic/regions/knn_classifier_region.py#L41
        public IClassification<T> Compute<T>(
            int recordNum,
            IDictionary<string, object> classification,
            int[] patternNZ,
            bool learn,
            bool infer)
        {
            if (learn == false && infer == false) 
                throw new InvalidOperationException("learn and infer cannot be both false");

            /*if (typeof(T) != typeof(double[]))
            {
                throw new InvalidOperationException($"T must be double[] and is {typeof(T).Name}");
            }*/

            // Save the offset between recordNum and learnIteration if this is the first compute
            if (_recordNumMinusLearnIteration == -1)
            {
                _recordNumMinusLearnIteration = recordNum - _learnIteration;
            }

            // Update the learn iteration
            _learnIteration = recordNum - _recordNumMinusLearnIteration;

            if (Verbosity >= 1)
            {
                Console.WriteLine($"\n{g_debugPrefix}: compute ");
                Console.WriteLine($" recordNum: {recordNum}");
                Console.WriteLine($" learnIteration: {_learnIteration}");
                Console.WriteLine($" patternNZ({patternNZ.Length}): {Arrays.ToString(patternNZ)}");
                Console.WriteLine($" classificationIn: {classification}");
            }

            // Store pattern in our history
            _patternNzHistory.Append((_learnIteration, patternNZ));

            // To allow multi-class classification, we need to be able to run learning
            // without inference being on. So initialize retval outside
            // of the inference block.
            KnnClassification retVal = null;

            // --------------------------------------------------------------------
            // Inference:
            // For each active bit in the activationPattern, get the classification votes
            int[] encoding = (int[])classification["encoding"];
            Vector<double> inputPattern = Vector<double>.Build.DenseOfArray(encoding.Select(p=>(double)p).ToArray());
            if (infer)
            {
                retVal = Infer(inputPattern);

                var probabilities = retVal.GetInference() / retVal.GetInference().Sum();
                retVal.SetStats(1, probabilities.ToArray());
                retVal.SetActualValues(retVal.GetInference().Cast<object>().ToArray());
            }

            // --------------------------------------------------------------------
            // Learning:
            if (learn)
            {
                // An input can potentially belong to multiple categories.
                // If a category value is < 0, it means that the input does not belong to
                // that category.
                //var categories = [category for category in inputs["categoryIn"]
                //                      if category >= 0]
                var categories = ((int[])classification["categoryIn"] ?? new []{ 0 })
                    .Where(c => c >= 0)
                    .ToArray();
                if (categories.Length > 0)
                {
                    // allow to train on multiple input categories
                    foreach (var category in categories)
                    {
                        // todo : sphering
                        Learn(inputPattern, category);
                        _iterationIdx++;
                    }
                }
            }

            // ------------------------------------------------------------------------
            // Verbose print
            if (infer && Verbosity >= 1)
            {
                Console.WriteLine(" inference: combined bucket likelihoods:");
                Console.WriteLine("   actual bucket values: " + Arrays.ToString(retVal.GetActualValues()));

                foreach (int key in retVal.StepSet())
                {
                    if (retVal.GetActualValue(key) == null) continue;

                    double[] actual = { (double)retVal.GetActualValue(key) };
                    Console.WriteLine($"  {key} steps: {PFormatArray(actual)}");
                    int bestBucketIdx = retVal.GetMostProbableBucketIndex(key);
                    Console.WriteLine($"   most likely bucket idx: {bestBucketIdx}, value: {retVal.GetActualValue(bestBucketIdx)} ");
                }
            }

            return (IClassification<T>)retVal;
        }

        private KnnClassification Compute(
            Vector<double> inputPattern,
            int inputCategory,
            int? partitionId = null,
            int isSparse = 0,
            int rowId = -1,
            bool infer = true,
            bool learn = true)
        {
            KnnClassification retVal = new KnnClassification();

            if (infer)
            {
                retVal = Infer(inputPattern, partitionId);
                retVal.SetActualValues(inputPattern.ToArray().Cast<object>().ToArray());
            }

            if (learn)
            {
                int numPatterns = Learn(inputPattern, inputCategory, partitionId, isSparse, rowId);
                retVal.SetNumPatterns(numPatterns);
            }

            return retVal;
        }

        /// <summary>
        /// Train the classifier to associate specified input pattern with a particular category.
        /// </summary>
        /// <param name="inputPattern">The pattern to be assigned a category. If
        /// isSparse is 0, this should be a dense array(both ON and OFF bits
        /// present). Otherwise, if isSparse > 0, this should be a list of the
        /// indices of the non-zero bits in sorted order</param>
        /// <param name="inputCategory">The category to be associated to the training pattern</param>
        /// <param name="partitionId">partitionID allows you to associate an id with each
        /// input vector.It can be used to associate input patterns stored in the
        /// classifier with an external id. This can be useful for debugging or
        /// visualizing.Another use case is to ignore vectors with a specific id
        /// during inference (see description of infer() for details). There can be
        /// at most one partitionId per stored pattern(i.e. if two patterns are
        /// within distThreshold, only the first partitionId will be stored). This
        /// is an optional parameter.</param>
        /// <param name="isSparse">
        /// 0 if the input pattern is a dense representation.
        /// When the input pattern is a list of non-zero indices, then isSparse
        /// is the number of total bits(n). E.g. for the dense array
        /// [0, 1, 1, 0, 0, 1], isSparse should be `0`. For the equivalent sparse
        /// representation[1, 2, 5] (which specifies the indices of active bits),
        /// isSparse should be `6`, which is the total number of bits in the input
        /// space.</param>
        /// <param name="rowId">Computed internally if not specified (i.e. for tests)</param>
        /// <returns>The number of patterns currently stored in the classifier</returns>
        public int Learn(Vector<double> inputPattern, int inputCategory, int? partitionId = null, int isSparse = 0, int rowId = -1)
        {
            int inputWidth = 0;
            bool addRow = false;
            Vector<double> thresholdedInput = null;

            if (isSparse > 0)
            {
                for (int i = 0; i < inputPattern.Count - 1; i++)
                {
                    if (inputPattern[i] > inputPattern[i + 1])
                    {
                        throw new InvalidOperationException("Sparse input pattern must be sorted");
                    }
                }
            }
            
            if (rowId == -1)
            {
                rowId = _iterationIdx;
            }

            if (!useSparseMemory)
            {
                // Not supported
                Debug.Assert(cellsPerCol == 0, "not implemented for dense vectors");

                // If the input was given in sparse form, convert it to dense
                if (isSparse > 0)
                {
                    var denseInput = Vector<double>.Build.Dense(isSparse);
                    foreach (int index in inputPattern)
                    {
                        denseInput[index] = 1.0;
                    }
                    inputPattern = denseInput;
                }

                if (_specificIndexTraining && _nextTrainingIndices == null)
                {
                    // Specific index mode without any index provided - skip training
                    return _numPatterns;
                }

                if (_memory == null)
                {
                    // Initialize memory with 100 rows and numPatterns = 0
                    inputWidth = inputPattern.Count;
                    _memory = new NearestNeighbor(100, inputWidth);
                    _numPatterns = 0;
                    _M = new NearestNeighbor(0, inputWidth);
                }

                addRow = true;

                if (_vt != null)
                {
                    // Compute projection
                    inputPattern =  _vt * (inputPattern - _mean);
                }
                if (distanceThreshold > 0)
                {
                    // Check if input is too close to an existing input to be accepted
                    var dist = CalcDistance(inputPattern);
                    var minDist = dist.Min();
                    addRow = (minDist >= distanceThreshold);
                }
                if (addRow)
                {
                    _protoSizes = null;     // need to re-compute
                    if (_numPatterns == _memory.RowCount)
                    {
                        // Double the size of the memory
                        //DoubleMemoryRows();
                    }
                    if (!_specificIndexTraining)
                    {
                        // Normal learning - append the new input vector
                        _memory.InsertRow(_numPatterns, inputPattern);
                        _numPatterns += 1;
                        _categoryList.Add(inputCategory);
                    }
                    else
                    {
                        // Specific index training mode - insert vector in specified slot
                        throw new NotImplementedException();
                    }
                    // Set _M to the "active" part of _Memory
                    _M = _memory;
                    AddPartitionId(_numPatterns - 1, partitionId);
                }
            }
            // Sparse vectors
            else
            {
                // If the input was given in sparse form, convert it to dense if necessary
                if (isSparse > 0 && (_vt != null || distanceThreshold > 0 || numSvdDims != null ||
                    numSvdSamples > 0 || numWinners > 0))
                {
                    throw new NotImplementedException();
                }

                // Get the input width
                if (isSparse > 0)
                {
                    inputWidth = isSparse;
                }
                else
                {
                    inputWidth = inputPattern.Count;
                }

                // Allocate storage if this is the first training vector
                if (_memory == null)
                {
                    _memory = new NearestNeighbor(0, inputWidth);
                }

                // Support SVD if it is on
                if (_vt != null)
                {
                    inputPattern = (_vt * (inputPattern - _mean));
                }

                // Threshold the input, zeroing out entries that are too close to 0.
                //  This is only done if we are given a dense input.
                if (isSparse == 0)
                {
                    thresholdedInput = SparsifyVector(inputPattern, true);
                }

                addRow = true;

                // If given the layout of the cells, then turn on the logic that stores
                // only the start cell for bursting columns
                if (cellsPerCol >= 1)
                {
                    double[] burstingCols = ArrayUtils.Min(
                        ArrayUtils.Reshape(new double[][] { thresholdedInput.ToArray() }, cellsPerCol), 1);

                    burstingCols = ArrayUtils.ToDoubleArray(
                        ArrayUtils.Where(burstingCols, ArrayUtils.GREATER_THAN_0));

                    foreach (double col in burstingCols)
                    {
                        ArrayUtils.SetRangeTo(
                            thresholdedInput,
                            (((int)col) * cellsPerCol) + 1,
                            (((int)col) * cellsPerCol) + cellsPerCol,
                            0
                        );
                    }
                }

                // Don't learn entries that are too close to existing entries.
                if (_memory.RowCount > 0)
                {
                    Vector<double> dist = null;
                    // if this vector is a perfect match for one we already learned, then
                    // replace the category - it may have changed with online learning on.
                    if (replaceDuplicates)
                    {
                        dist = CalcDistance(thresholdedInput, 1);
                        if (dist.Min() == 0)
                        {
                            int rowIdx = ArrayUtils.Argmin(dist.ToArray());
                            _categoryList[rowIdx] = inputCategory;
                            if (fixedCapacity)
                            {
                                _categoryRecencyList[rowIdx] = rowId;
                            }
                            addRow = false;
                        }
                    }
                    // Don't add this vector if it matches closely with another we already added
                    if (distanceThreshold > 0)
                    {
                        if (dist == null || _distanceNorm != 1.0)
                        {
                            dist = CalcDistance(thresholdedInput);
                        }
                        double minDist = dist.Min();
                        addRow = (minDist >= distanceThreshold);
                        if (!addRow)
                        {
                            if (fixedCapacity)
                            {
                                int rowIdx = ArrayUtils.Argmin(dist.ToArray());
                                _categoryRecencyList[rowIdx] = rowId;
                            }
                        }
                    }
                }

                if (addRow && minSparsity > 0.0)
                {
                    double sparsity;
                    if (isSparse == 0)
                    {
                        sparsity = ((double)thresholdedInput.Count(i => i != 0)) / thresholdedInput.Count;
                    }
                    else
                    {
                        sparsity = (double)inputPattern.Count / isSparse;
                    }

                    if (sparsity < minSparsity)
                    {
                        addRow = false;
                    }
                }

                // Add the new sparse vector to our storage
                if (addRow)
                {
                    _protoSizes = null;  // Need to recompute
                    if (isSparse == 0)
                    {
                        _memory.InsertRow(_memory.RowCount, thresholdedInput);
                        //memory.addRow(thresholdedInput);
                    }
                    else
                    {
                        int[] nonZeros = new int[inputPattern.Count];
                        Arrays.Fill(nonZeros, 1);
                        _memory.AddRowNonZero(inputPattern, nonZeros);
                        //                memory.addRowNZ(
                        //                    inputPattern, DoubleStream.generate(() -> 1)
                        //                        .limit(inputPattern.length).toArray());
                    }
                    _numPatterns += 1;
                    _categoryList.Add(inputCategory);
                    AddPartitionId(_numPatterns - 1, partitionId);
                    if (fixedCapacity)
                    {
                        _categoryRecencyList.Add(rowId);
                        if (_numPatterns > maxStoredPatterns && maxStoredPatterns > 0)
                        {
                            int leastRecentlyUsedPattern = ArrayUtils.Argmin(_categoryRecencyList.ToArray());
                            _memory.RemoveRow(leastRecentlyUsedPattern);
                            _categoryList.Remove(leastRecentlyUsedPattern);
                            _categoryRecencyList.Remove(leastRecentlyUsedPattern);
                            _numPatterns -= 1;
                        }
                    }
                }
            }



            if (numSvdDims != null && numSvdSamples > 0 
                && _numPatterns == numSvdSamples)
            {
                ComputeSvd();
            }

            return _numPatterns;
        }

        /// <summary>
        /// Train the classifier to associate specified input pattern with a particular category.
        /// </summary>
        /// <param name="inputPattern">The pattern to be assigned a category. If
        /// isSparse is 0, this should be a dense array(both ON and OFF bits
        /// present). Otherwise, if isSparse > 0, this should be a list of the
        /// indices of the non-zero bits in sorted order</param>
        /// <param name="inputCategory">The category to be associated to the training pattern</param>
        /// <param name="partitionId">partitionID allows you to associate an id with each
        /// input vector.It can be used to associate input patterns stored in the
        /// classifier with an external id. This can be useful for debugging or
        /// visualizing.Another use case is to ignore vectors with a specific id
        /// during inference (see description of infer() for details). There can be
        /// at most one partitionId per stored pattern(i.e. if two patterns are
        /// within distThreshold, only the first partitionId will be stored). This
        /// is an optional parameter.</param>
        /// <param name="isSparse">
        /// 0 if the input pattern is a dense representation.
        /// When the input pattern is a list of non-zero indices, then isSparse
        /// is the number of total bits(n). E.g. for the dense array
        /// [0, 1, 1, 0, 0, 1], isSparse should be `0`. For the equivalent sparse
        /// representation[1, 2, 5] (which specifies the indices of active bits),
        /// isSparse should be `6`, which is the total number of bits in the input
        /// space.</param>
        /// <param name="rowId">Computed internally if not specified (i.e. for tests)</param>
        /// <returns>The number of patterns currently stored in the classifier</returns>
        public int Learn(double[] inputPattern, int inputCategory, int? partitionId = null, int isSparse = 0,
            int rowId = -1)
        {
            return Learn(Vector.Build.DenseOfArray(inputPattern), inputCategory, partitionId, isSparse, rowId);
        }

        /// <summary>
        /// Finds the category that best matches the input pattern. Returns the
        /// winning category index as well as a distribution over all categories.
        /// </summary>
        /// <param name="inputPattern">The pattern to be classified. This
        /// must be a dense representation of the array(e.g. [0, 0, 1, 1, 0, 1])</param>
        /// <param name="partitionId">
        /// If provided, all training vectors with partitionId
        /// equal to that of the input pattern are ignored.
        /// For example, this may be used to perform k-fold cross validation
        /// without repopulating the classifier.First partition all the data into
        /// k equal partitions numbered 0, 1, 2, ... and then call learn() for each
        /// vector passing in its partitionId.Then, during inference, by passing
        /// in the partition ID in the call to infer(), all other vectors with the
        /// same partitionId are ignored simulating the effect of repopulating the
        /// classifier while ommitting the training vectors in the same partition.</param>
        /// <returns></returns>
        public KnnClassification Infer(
            Vector<double> inputPattern, int? partitionId = null)
        {
            int? winner;
            Vector<double> inferenceResult;
            Vector<double> dist;
            double[] categoryDist;

            double sparsity = 0.0;
            if (minSparsity > 0.0)
            {
                sparsity = ((double)inputPattern.Count(i => i != 0)) / inputPattern.Count;
            }

            if (_categoryList.Count == 0 || sparsity < minSparsity)
            {
                // No categories learned yet; i.e. first inference w/ online learning.
                winner = null;
                inferenceResult = Vector.Build.Dense(1);
                dist = Vector.Build.Dense(1);
                dist.Add(1);
                //Arrays.Fill(dist, 1);
                categoryDist = new double[1];
                Arrays.Fill(categoryDist, 1);
            }
            else
            {
                int maxCategoryIdx = _categoryList.Max();
                inferenceResult = Vector.Build.Dense(maxCategoryIdx + 1);// new double[maxCategoryIdx + 1];
                dist = GetDistances(inputPattern, partitionId: partitionId);
                int validVectorCount = _categoryList.Count - _categoryList.Count(c => c == -1);
                // Loop through the indices of the nearest neighbors.
                if (_exact)
                {
                    // Is there an exact match in the distances?
                    var exactMatches = ArrayUtils.Where(dist, d => d < 0.00001);
                    if (exactMatches.Length > 0)
                    {
                        foreach (int i in exactMatches.Take(Math.Min(_k, validVectorCount)))
                        {
                            inferenceResult[_categoryList[i]] += 1.0;
                        }
                    }
                }
                else
                {
                    var sorted = ArrayUtils.Argsort(dist);
                    foreach (int i in sorted.Take(Math.Min(_k, validVectorCount)))
                    {
                        inferenceResult[_categoryList[i]] += 1.0;
                    }
                }

                // Prepare inference results.
                if (inferenceResult.Storage.EnumerateNonZero().Any())
                {
                    winner = inferenceResult.MaximumIndex();
                    inferenceResult /= inferenceResult.Sum();
                }
                else
                {
                    winner = null;
                }
                categoryDist = MinScorePerCategory(maxCategoryIdx, _categoryList, dist);
                categoryDist = ArrayUtils.Clip(categoryDist, 0.0, 1.0);
            }
            /*
                if self.verbosity >= 1:
              print "%s infer:" % (g_debugPrefix)
              print "  active inputs:",  _labeledInput(inputPattern,
                                                       cellsPerCol=self.cellsPerCol)
              print "  winner category:", winner
              print "  pct neighbors of each category:", inferenceResult
              print "  dist of each prototype:", dist
              print "  dist of each category:", categoryDist*/
            var classification = new KnnClassification();
            classification.SetInferResult(new KnnInferResult(winner, inferenceResult, dist, categoryDist));
            return classification;
        }

        public KnnClassification Infer(
            double[] inputPattern, int? partitionId = null)
        {
            return Infer(Vector.Build.DenseOfArray(inputPattern), partitionId);
        }

        /// <summary>
        /// Do sparsification, using a relative or absolute threshold
        /// </summary>
        /// <param name="inputPattern"></param>
        /// <param name="doWinners"></param>
        /// <returns></returns>
        public Vector<double> SparsifyVector(Vector<double> inputPattern, bool doWinners = false)
        {
            if (!relativeThreshold)
            {
                var vec = inputPattern.PointwiseMultiply(Vector<double>.Abs(inputPattern));
                inputPattern = Vector<double>.Build.SparseOfArray(
                    vec
                    .Select((i, idx) => (i) > sparseThreshold ? inputPattern[idx] : 0.0)
                    .ToArray());
            }
            else if (sparseThreshold > 0)
            {
                inputPattern = Vector<double>.Build.SparseOfArray(
                    inputPattern
                    .Select(i => (i * Math.Abs(i)) > sparseThreshold * inputPattern.Max() ? i : 0.0)
                    .ToArray());
            }

            // Do winner-take-all
            if (doWinners)
            {
                if ((numWinners > 0) && (numWinners < inputPattern.Where(i => i > 0).Sum()))
                {
                    Vector<double> sparseInput = Vector<double>.Build.Dense(inputPattern.Count);
                    int[] sorted = ArrayUtils.Argsort(inputPattern, 0, numWinners);
                    inputPattern = ArrayUtils.Subst(sparseInput, inputPattern, sorted);
                    // double[] sparseInput = (double[])Array.CreateInstance(typeof(double), ArrayUtils.Shape(inputPattern));
                    //int[] sorted = ArrayUtils.Argsort(inputPattern, 0, numWinners);
                    //inputPattern = ArrayUtils.Subst(sparseInput, inputPattern, sorted);
                }
            }

            if (doBinarization)
            {
                inputPattern = Vector<double>.Build.DenseOfArray(
                    inputPattern.Select(d => d > binarizationThreshold ? 1.0 : 0.0).ToArray());
            }

            return inputPattern;
        }

        public Vector<double> ClosestTrainingPattern(Vector<double> inputPattern, int cat)
        {
            var inputArr = inputPattern.ToArray();
            var dist = GetDistances(inputPattern, null);
            var sorted = ArrayUtils.Argsort(dist);
            foreach (var patIdx in sorted)
            {
                var patternCat = _categoryList[patIdx];

                // if closest pattern belongs to desired category, return it
                if (patternCat == cat)
                {
                    Vector<double> closestPattern;
                    if (useSparseMemory)
                    {
                        closestPattern = _memory.GetMatrix().Row(patIdx);
                    }
                    else
                    { 
                        closestPattern = _M.GetMatrix().Row(patIdx);
                    }

                    return closestPattern;
                }
            }

            return null;
        }

        #endregion

        #region Helper Methods

        // orig version (2 = vector version)
        private Vector<double> ComputeSvd(int numSvdSamples = 0, bool finalize = true)
        {
            if (numSvdSamples == 0)
            {
                numSvdSamples = _numPatterns;
            }

            if (!useSparseMemory)
            {
                _a = Matrix<double>.Build.DenseOfRowArrays(_memory.ToDense().Take(_numPatterns).ToArray());
            }
            else
            {
                _a = Matrix<double>.Build.DenseOfRowArrays(_memory.ToDense().Take(_numPatterns).ToArray());
            }

            _mean = Vector<double>.Build.DenseOfArray(ArrayUtils.Mean(_a.ToRowArrays(), 0));
            _a = Matrix<double>.Build.DenseOfRowArrays(ArrayUtils.Sub(_a.ToRowArrays(), _mean.ToArray()));

            //var svd = _a.SubMatrix(0, numSvdSamples, 0, _a.ColumnCount).Svd(true);
            var svd = Matrix<double>.Build.DenseOfRowArrays(_a.ToRowArrays().Take(numSvdSamples)).Svd(true);
            _vt = svd.VT;
            _s = svd.S;
            if (finalize)
            {
                FinalizeSvd();
            }

            return _s;
            /*
            if not self.useSparseMemory:
              self._a = self._Memory[:self._numPatterns]
            else:
              self._a = self._Memory.toDense()[:self._numPatterns]

            self._mean = numpy.mean(self._a, axis=0)
            self._a -= self._mean
            u,self._s,self._vt = numpy.linalg.svd(self._a[:numSVDSamples])

            if finalize:
              self.finalizeSVD()

            return self._s
            */

        }

        private void FinalizeSvd(int? numSVDDims = null)
        {
            if (numSVDDims.HasValue)
                this.numSvdDims = numSVDDims;

            if (this.numSvdDims.Value == (int)KnnMode.ADAPTIVE)
            {
                if (fractionOfMax.HasValue)
                {
                    numSvdDims = GetAdaptiveSvdDims(_s, fractionOfMax.Value);
                }
                else
                {
                    numSvdDims = GetAdaptiveSvdDims(_s);
                }
            }

            if (_vt.ToRowArrays().GetLength(0) < (int)numSvdDims.GetValueOrDefault())
            {
                Console.WriteLine("******************************************************************");
                Console.WriteLine("Warning: The requested number of PCA dimensions is more than the number of pattern dimensions.");
                Console.WriteLine("Setting numSVDDims = {0}", _vt.ToRowArrays().GetLength(0));
                Console.WriteLine("******************************************************************");
                numSvdDims = _vt.ToRowArrays().GetLength(0);
            }

            //_vt = _vt.SubMatrix(0, numSvdDims.Value, 0, _vt.ColumnCount);
            _vt = Matrix<double>.Build.DenseOfRowArrays(_vt.ToRowArrays().Take(numSvdDims.Value).ToArray());
            // Added when svd is not able to decompose vectors - uses raw spare vectors
            if (_vt.RowCount == 0) return;

            _memory = new NearestNeighbor(_numPatterns, numSvdDims.Value);
            _M = _memory;
            useSparseMemory = false;
            for (int i = 0; i < _numPatterns; i++)
            {
                _memory.GetMatrix().SetRow(i, _vt * _a.Row(i));
            }
            _a = null;
        }

        private Vector<double> ComputeSvd2(int numSvdSamples = 0, bool finalize = true)
        {
            if (numSvdSamples == 0)
            {
                numSvdSamples = _numPatterns;
            }

            if (!useSparseMemory)
            {
                _a = _memory.GetMatrix().SubMatrix(0, _numPatterns, 0, _memory.GetMatrix().ColumnCount);
                //_a = _memory.ToDense().Take(_numPatterns).ToArray();
                //throw new NotImplementedException("!useSparseMemory");
                //_a = memory.TakeRows(_numPatterns);
            }
            else
            {
                _a = _memory.GetMatrix().SubMatrix(0, _numPatterns, 0, _memory.GetMatrix().ColumnCount);
            }

            _mean = ArrayUtils.Mean(_a, 0);
            _a = ArrayUtils.Sub(_a, _mean);

            var svd = _a.SubMatrix(0, numSvdSamples, 0, _a.ColumnCount).Svd(true);
            // var svd = DenseMatrix.OfRowArrays(_a.Take(numSvdSamples)).Svd(true);
            _vt = svd.VT;
            _s = svd.S;
            if (finalize)
            {
                FinalizeSvd();
            }

            return _s;
            /*
            if not self.useSparseMemory:
              self._a = self._Memory[:self._numPatterns]
            else:
              self._a = self._Memory.toDense()[:self._numPatterns]

            self._mean = numpy.mean(self._a, axis=0)
            self._a -= self._mean
            u,self._s,self._vt = numpy.linalg.svd(self._a[:numSVDSamples])

            if finalize:
              self.finalizeSVD()

            return self._s
            */

        }

        private void FinalizeSvd2(int? numSVDDims = null)
        {
            if (numSVDDims.HasValue)
                this.numSvdDims = numSVDDims;

            if (this.numSvdDims.Value == (int)KnnMode.ADAPTIVE)
            {
                if (fractionOfMax.HasValue)
                {
                    numSvdDims = GetAdaptiveSvdDims(_s, fractionOfMax.Value);
                }
                else
                {
                    numSvdDims = GetAdaptiveSvdDims(_s);
                }
            }

            if (_vt.RowCount < (int)numSvdDims.GetValueOrDefault())
            {
                Console.WriteLine("******************************************************************");
                Console.WriteLine("Warning: The requested number of PCA dimensions is more than the number of pattern dimensions.");
                Console.WriteLine("Setting numSVDDims = {0}", _vt.RowCount);
                Console.WriteLine("******************************************************************");
                numSvdDims = _vt.RowCount;
            }

            _vt = _vt.SubMatrix(0, numSvdDims.Value, 0, _vt.ColumnCount);
            //_vt = _vt.Take(numSvdDims.Value).ToArray();
            // Added when svd is not able to decompose vectors - uses raw spare vectors
            if (_vt.RowCount == 0) return;

            _memory = new NearestNeighbor(_numPatterns, numSvdDims.Value);
            _M = _memory;
            useSparseMemory = false;
            for (int i = 0; i < _numPatterns; i++)
            {
                _memory.GetMatrix().SetRow(i, _vt * _a.Row(i));
            }
            _a = null;
        }

        private int? GetAdaptiveSvdDims(Vector<double> singularValues, double fractionOfMax = 0.001)
        {
            Vector<double> v = singularValues / singularValues[0];
            int[] idx = ArrayUtils.Where(v, d => d < fractionOfMax);
            if (idx.Length > 0)
            {
                Console.WriteLine("Number of PCA dimensions chosen: " + (idx[0] + 1) + " out of " + v.Count);
                return idx[0] + 1;
            }
            Console.WriteLine("Number of PCA dimensions chosen: " + (v.Count - 1) + " out of " + v.Count);
            return v.Count - 1;
        }

        private void DoubleMemoryRows()
        {
            var m = 2 * _memory.GetMatrix().RowCount;
            var n = _memory.GetMatrix().ColumnCount;
            _memory.ResizeMatrix(m, n);
        }

        /// <summary>
        /// Adds partition id for pattern index
        /// </summary>
        /// <param name="index"></param>
        /// <param name="partitionId"></param>
        private void AddPartitionId(int index, int? partitionId = null)
        {
            if (!partitionId.HasValue || partitionId == -1)
            {
                _partitionIdList.Add(int.MaxValue);
            }
            else
            {
                _partitionIdList.Add(partitionId.Value);
                var indices = _partitionIdMap.Get(partitionId.Value, Array.Empty<int>()).ToList();
                indices.Add(index);
                _partitionIdMap[partitionId.Value] = indices.ToArray();
            }
        }

        private double[] MinScorePerCategory(int maxCategoryIdx, List<int> categoryList, Vector<double> dist)
        {
            var categories = categoryList.ToArray();
            var distance = dist.ToArray();

            int n = maxCategoryIdx + 1;
            double[] scores = new double[n];
            Arrays.Fill(scores, double.MaxValue);

            int nScores = categories.Length;
            for (int i = 0; i < nScores; i++)
            {
                var cg = categories[i];
                var dg = distance[i];
                scores[cg] = Math.Min(scores[cg], dg);
            }
            return scores;
        }

        /// <summary>
        /// Return the distances from inputPattern to all stored patterns.
        /// </summary>
        /// <param name="inputPattern">The pattern from which distances to all other patterns are returned</param>
        /// <param name="partitionId">If provided, ignore all training vectors with this partitionId.</param>
        /// <returns></returns>
        private Vector<double> GetDistances(Vector<double> inputPattern, int? partitionId)
        {
            if (!_finishedLearning)
            {
                FinishLearning();
                _finishedLearning = true;
            }

            if (_vt != null && _vt.RowCount > 0)
            {
                // inputPattern = ArrayUtils.Dot(_vt, ArrayUtils.Sub(inputPattern, _mean));
                inputPattern = _vt * (inputPattern - _mean);
            }

            var sparseInput = SparsifyVector(inputPattern);

            // Compute distances
            Vector<double> dist = CalcDistance(sparseInput);
            // Invalidate results where category is -1
            if (_specificIndexTraining)
            {
                var invalidCategories = ArrayUtils.Where(_categoryList, i => i == -1);
                foreach (int invalidCategoryIndex in invalidCategories)
                {
                    dist[invalidCategoryIndex] = double.PositiveInfinity;
                }
            }
            // Ignore vectors with this partition id by setting their distances to infinite
            if (partitionId.HasValue)
            {
                int[] partIndexes = _partitionIdMap.Get(partitionId.Value, new int[0]);
                foreach (int partIndex in partIndexes)
                {
                    dist[partIndex] = double.PositiveInfinity;
                }
            }
            return dist;
        }

        /// <summary>
        /// Calculate the distances from inputPattern to all stored patterns.
        /// All distances are between 0.0 and 1.0
        /// </summary>
        /// <param name="inputPattern">The pattern from which distances to all other patterns are calculated</param>
        /// <param name="distanceNorm">Degree of the distance norm</param>
        /// <returns></returns>
        private Vector<double> CalcDistance(Vector<double> inputPattern, double? distanceNorm = null)
        {
            Vector<double> dist;

            if (!distanceNorm.HasValue || distanceNorm == -1)
            {
                distanceNorm = this._distanceNorm;
            }

            // Sparse memory
            if (useSparseMemory)
            {
                if (_protoSizes == null)
                {
                    _protoSizes = _memory.RowSums();
                }

                // double[] overlapsWithProtos = _memory.RightVecSumAtNz(inputPattern); // .RightVecSumAtNz(inputPattern);
                Vector<double> overlapsWithProtos = _memory.RightVecSumAtNz(inputPattern); // .RightVecSumAtNz(inputPattern);
                double inputPatternSum = inputPattern.Sum();

                if (distanceMethod == DistanceMethod.RawOverlap)
                {
                    dist = inputPatternSum - overlapsWithProtos;
                    //dist = ArrayUtils.Sub(inputPatternSum, overlapsWithProtos); // 1,4 -> overlap should be 5,2
                }
                else if (distanceMethod == DistanceMethod.PctInputOverlap)
                {
                    //dist = ArrayUtils.Sub(inputPatternSum, overlapsWithProtos);
                    dist = inputPatternSum - overlapsWithProtos;
                    if (inputPatternSum > 0)
                    {
                        dist /= inputPatternSum;
                    }
                }
                else if (distanceMethod == DistanceMethod.PctProtoOverlap)
                {
                    overlapsWithProtos /= _protoSizes;
                    dist = 1.0 - overlapsWithProtos;
                }
                else if (distanceMethod == DistanceMethod.PctLargerOverlap)
                {
                    Vector<double> maxVal = ArrayUtils.Maximum(_protoSizes, inputPatternSum);
                    if (maxVal.All(i => i > 0))
                    {
                        overlapsWithProtos /= maxVal;
                    }
                    dist = 1.0 - overlapsWithProtos;
                }
                else if (distanceMethod == DistanceMethod.Norm)
                {
                    dist = _memory.VecLpDist(distanceNorm.Value, inputPattern);
                    var distMax = dist.Max();
                    if (distMax > 0)
                    {
                        dist /= distMax;
                    }
                }
                else
                {
                    throw new InvalidOperationException("Unimplemented distance method \"" + distanceMethod + "\"");
                }
            }
            // Dense memory
            else
            {
                if (distanceMethod == DistanceMethod.Norm)
                {
                    double[][] mArr = _M.ToDense();
                    double[][] subbed = ArrayUtils.SubtractRows(mArr, inputPattern.ToArray());
                    double[][] abs = ArrayUtils.Abs(subbed);
                    double[][] powDist = ArrayUtils.Power(abs, distanceNorm.Value);

                    // dist = dist.sum(1)
                    //dist = powDist.Select(a => a.Sum()).ToArray();
                    var dist2 = ArrayUtils.Sum(powDist, 1);
                    dist2 = ArrayUtils.Power(dist2, 1.0 / distanceNorm.Value);
                    dist2 = ArrayUtils.Divide(dist2, dist2.Max());
                    dist = Vector<double>.Build.DenseOfArray(dist2);
                }
                else
                {
                    throw new NotImplementedException("Not implemented yet for dense storage....");
                }
            }
            return dist;
        }

        public void FinishLearning()
        {
            if (numSvdDims != null && _vt == null)
            {
                ComputeSvd();
            }
        }

        /// <summary>
        /// A list of row indices to remove. There are two caveats. First, this is
        /// a potentially slow operation.Second, pattern indices will shift if
        /// patterns before them are removed.
        /// </summary>
        /// <param name="rowsToRemove"></param>
        internal int RemoveRows(int[] rowsToRemove)
        {
            // Remove categories
            rowsToRemove.Reverse().ToList().ForEach(i => _categoryList.RemoveAt(i));
            if (fixedCapacity)
            {
                rowsToRemove.Reverse().ToList().ForEach(i => _categoryRecencyList.RemoveAt(i));
            }
            // Remove the partition ID, if any for these rows and rebuild the id map.
            foreach (int row in rowsToRemove.Reverse())
            {
                // Remove these patterns from partitionList
                _partitionIdList.RemoveAt(row);
            }
            RebuildPartitionIdMap(_partitionIdList);
            // Remove actual patterns
            if (useSparseMemory)
            {
                // Delete backwards
                foreach (int rowIndex in rowsToRemove.Reverse())
                {
                    _memory.RemoveRow(rowIndex);
                }
            }
            else
            {
                // _M = numpy.delete(self._M, removalArray, 0)
                throw new NotImplementedException();
            }
            int numRemoved = rowsToRemove.Length;

            // Sanity checks
            int numRowsExpected = _numPatterns - numRemoved;
            if (useSparseMemory)
            {
                if (_memory != null)
                {
                    Debug.Assert(_memory.RowCount == numRowsExpected);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
            Debug.Assert(_categoryList.Count == numRowsExpected);
            _numPatterns -= numRemoved;
            return numRemoved;
        }

        /// <summary>
        /// Rebuilds the partition Id map using the given partitionIdList
        /// </summary>
        /// <param name="partitionIdList"></param>
        private void RebuildPartitionIdMap(List<int> partitionIdList)
        {
            _partitionIdMap = new Map<int, int[]>();
            for (int index = 0; index < partitionIdList.Count; index++)
            {
                int partitionId = partitionIdList[index];
                var indices = _partitionIdMap.Get(partitionId, new int[0]).ToList();
                indices.Add(index);
                _partitionIdMap[partitionId] = indices.ToArray();
            }
        }

        /**
         * Return a string with pretty-print of an array using the given format
         * for each element
         * 
         * @param arr
         * @return
         */
        private string PFormatArray<T>(T[] arr)
        {
            if (arr == null) return "";

            StringBuilder sb = new StringBuilder("[ ");
            foreach (T t in arr)
            {
                sb.Append($"{t:#.00}s");
            }
            sb.Append(" ]");
            return sb.ToString();
        }

        #endregion

        #region Accessor Methods

        ///////////////////////////////////////////////////////
        //                  Accessor Methods                 //
        ///////////////////////////////////////////////////////

        /// <summary>
        /// Returns the partition Id associated with pattern i.
        /// Returns None if no Id is associated with it.
        /// </summary>
        /// <param name="i"></param>
        /// <returns></returns>
        public int? GetPartitionId(int i)
        {
            if (i < 0 || i >= _numPatterns) throw new IndexOutOfRangeException("index out of bounds");
            int? partitionId = _partitionIdList[i];
            if (partitionId.GetValueOrDefault(int.MaxValue) == int.MaxValue)
            {
                return null;
            }
            return partitionId;
        }
        /// <summary>
        /// Returns a list of pattern indices corresponding to this partitionId.
        /// Return an empty list if there are none
        /// </summary>
        /// <param name="partitionId"></param>
        public int[] GetPatternIndicesWithPartitionId(int partitionId)
        {
            return _partitionIdMap.Get(partitionId, new int[0]);
        }
        /// <summary>
        /// Return the number of unique partition Ids stored.
        /// </summary>
        /// <returns></returns>
        public int GetNumPartitionIds()
        {
            return _partitionIdMap.Count;
        }

        public int[] GetPartitionIdPerPattern()
        {
            return _partitionIdList.ToArray();
        }

        /// <summary>
        /// Return a list containing unique (non-None) partition Ids
        /// </summary>
        /// <returns></returns>
        public int[] GetPartitionIdList()
        {
            return _partitionIdMap.Keys.ToArray();
        }

        /**
     * Returns the number of nearest neighbors used in the classification of patterns. <b>Must be odd</b>
     * @return the k
     */
        public int GetK()
        {
            return _k;
        }

        /**
         * If true, patterns must match exactly when assigning class labels
         * @return the exact
         */
        public bool IsExact()
        {
            return _exact;
        }

        /**
         * When distance method is "norm", this specifies the p value of the Lp-norm
         * @return the distanceNorm
         */
        public double GetDistanceNorm()
        {
            return _distanceNorm;
        }

        /**
         * The method used to compute distance between input patterns and prototype patterns.
         * see({@link DistanceMethod}) 
         * 
         * @return the distanceMethod
         */
        public DistanceMethod GetDistanceMethod()
        {
            return distanceMethod;
        }

        /**
         * A threshold on the distance between learned
         * patterns and a new pattern proposed to be learned. The distance must be
         * greater than this threshold in order for the new pattern to be added to
         * the classifier's memory
         * 
         * @return the distanceThreshold
         */
        public double GetDistanceThreshold()
        {
            return distanceThreshold;
        }

        /**
         * If True, then scalar inputs will be binarized.
         * @return the doBinarization
         */
        public bool IsDoBinarization()
        {
            return doBinarization;
        }

        /**
         * If doBinarization is True, this specifies the threshold for the binarization of inputs
         * @return the binarizationThreshold
         */
        public double GetBinarizationThreshold()
        {
            return binarizationThreshold;
        }

        /**
         * If True, classifier will use a sparse memory matrix
         * @return the useSparseMemory
         */
        public bool IsUseSparseMemory()
        {
            return useSparseMemory;
        }

        /**
         * If useSparseMemory is True, input variables whose absolute values are 
         * less than this threshold will be stored as zero
         * @return the sparseThreshold
         */
        public double GetSparseThreshold()
        {
            return sparseThreshold;
        }

        /**
         * Flag specifying whether to multiply sparseThreshold by max value in input
         * @return the relativeThreshold
         */
        public bool IsRelativeThreshold()
        {
            return relativeThreshold;
        }

        /**
         * Number of elements of the input that are stored. If 0, all elements are stored
         * @return the numWinners
         */
        public int GetNumWinners()
        {
            return numWinners;
        }

        /**
         * Number of samples the must occur before a SVD
         * (Singular Value Decomposition) transformation will be performed. If 0,
         * the transformation will never be performed
         * 
         * @return the numSVDSamples
         */
        public int? GetNumSVDSamples()
        {
            return numSvdSamples;
        }

        /// <summary>
        /// Controls dimensions kept after SVD transformation. If "adaptive", the number is chosen automatically (KnnMode)
        /// </summary>
        /// <returns>The NumSVDDims</returns>
        public int? GetNumSVDDims()
        {
            return numSvdDims;
        }

        /**
         * If numSVDDims is "adaptive", this controls the
         * smallest singular value that is retained as a fraction of the largest
         * singular value
         * 
         * @return the fractionOfMax
         */
        public double? GetFractionOfMax()
        {
            return fractionOfMax;
        }

        public int GetNumPatterns()
        {
            return _numPatterns;
        }

        public List<int> GetCategoryList()
        {
            return _categoryList;
        }

        /**
         * Limits the maximum number of the training
         * patterns stored. When KNN learns in a fixed capacity mode, the unused
         * patterns are deleted once the number of stored patterns is greater than
         * maxStoredPatterns. A value of -1 is no limit
         * 
         * @return the maxStoredPatterns
         */
        public int GetMaxStoredPatterns()
        {
            return maxStoredPatterns;
        }

        /**
         * A boolean flag that determines whether,
         * during learning, the classifier replaces duplicates that match exactly,
         * even if distThreshold is 0. Should be TRUE for online learning
         * 
         * @return the replaceDuplicates
         */
        public bool IsReplaceDuplicates()
        {
            return replaceDuplicates;
        }

        /**
         * If >= 1, input is assumed to be organized into
         * columns, in the same manner as the temporal pooler AND whenever a new
         * prototype is stored, only the start cell (first cell) is stored in any
         * bursting column
         * 
         * @return the cellsPerCol
         */
        public int GetCellsPerCol()
        {
            return cellsPerCol;
        }

        #endregion

        #region Builder

        /// <summary>
        /// Implements the Builder Pattern for creating {@link KNNClassifier}s.
        /// </summary>
        public class Builder
        {
            private KNNClassifier _fieldHolder = new KNNClassifier();

            public Builder() { }

            /**
             * Returns a new KNNClassifier constructed from the fields specified
             * by this {@code Builder}
             * @return
             */
            public KNNClassifier Build()
            {
                KNNClassifier retVal = new KNNClassifier();
                foreach (FieldInfo f in _fieldHolder.GetType().GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    //f.setAccessible(true);
                    try
                    {
                        f.SetValue(retVal, f.GetValue(_fieldHolder));
                        //f.set(retVal, f.get(fieldHolder));
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                    }
                }
                retVal.Clear();
                return retVal;
            }

            /**
             * Returns a thoroughly constructed KNNClassifier using the 
             * parameters specified by the argument.
             * @param p
             * @return
             */
            public KNNClassifier Apply(Parameters p)
            {
                KNNClassifier retVal = new KNNClassifier();
                p.Apply(retVal);
                return retVal;
            }

            /// <summary>
            /// The number of nearest neighbors used in the classification of patterns. <b>Must be odd</b>
            /// </summary>
            /// <param name="k"></param>
            /// <returns></returns>
            public Builder K(int k)
            {
                if (k % 2 == 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(k), "Must be odd");
                }

                _fieldHolder._k = k;
                return this;
            }

            /// <summary>
            /// If true, patterns must match exactly when assigning class labels
            /// </summary>
            /// <param name="b"></param>
            /// <returns></returns>
            public Builder Exact(bool b)
            {
                _fieldHolder._exact = b;
                return this;
            }

            /// <summary>
            /// When distance method is "norm", this specifies the p value of the Lp-norm
            /// </summary>
            /// <param name="distanceNorm"></param>
            /// <returns></returns>
            public Builder DistanceNorm(double distanceNorm)
            {
                _fieldHolder._distanceNorm = distanceNorm;
                return this;
            }

            /// <summary>
            /// The method used to compute distance between input patterns and prototype patterns.
            /// See <see cref="DistanceMethod"/>
            /// </summary>
            /// <param name="method"></param>
            /// <returns></returns>
            public Builder DistanceMethod(DistanceMethod method)
            {
                _fieldHolder.distanceMethod = method;
                return this;
            }

            /**
             * A threshold on the distance between learned
             * patterns and a new pattern proposed to be learned. The distance must be
             * greater than this threshold in order for the new pattern to be added to
             * the classifier's memory
             * 
             * @param threshold
             * @return this Builder
             */
            public Builder DistanceThreshold(double threshold)
            {
                _fieldHolder.distanceThreshold = threshold;
                return this;
            }

            /**
             * If True, then scalar inputs will be binarized.
             * @param b
             * @return this Builder
             */
            public Builder DoBinarization(bool b)
            {
                _fieldHolder.doBinarization = b;
                return this;
            }

            /**
             * If doBinarization is True, this specifies the threshold for the binarization of inputs
             * @param threshold
             * @return  this Builder
             */
            public Builder BinarizationThreshold(double threshold)
            {
                _fieldHolder.binarizationThreshold = threshold;
                return this;
            }

            /**
             * If True, classifier will use a sparse memory matrix
             * @param b
             * @return  this Builder
             */
            public Builder UseSparseMemory(bool b)
            {
                _fieldHolder.useSparseMemory = b;
                return this;
            }

            /**
             * If useSparseMemory is True, input variables whose absolute values are 
             * less than this threshold will be stored as zero
             * @param threshold
             * @return  this Builder
             */
            public Builder SparseThreshold(double threshold)
            {
                _fieldHolder.sparseThreshold = threshold;
                return this;
            }

            /**
             * Flag specifying whether to multiply sparseThreshold by max value in input
             * @param b
             * @return  this Builder
             */
            public Builder RelativeThreshold(bool b)
            {
                _fieldHolder.relativeThreshold = b;
                return this;
            }

            /**
             * Number of elements of the input that are stored. If 0, all elements are stored
             * @param b
             * @return  this Builder
             */
            public Builder NumWinners(int num)
            {
                _fieldHolder.numWinners = num;
                return this;
            }

            /**
             * Number of samples the must occur before a SVD
             * (Singular Value Decomposition) transformation will be performed. If 0,
             * the transformation will never be performed
             * 
             * @param b
             * @return  this Builder
             */
            public Builder NumSVDSamples(int num)
            {
                _fieldHolder.numSvdSamples = num;
                return this;
            }

            /**
             * Controls dimensions kept after SVD transformation. If "adaptive", 
             * the number is chosen automatically
             * @param con
             * @return  this Builder
             */
            public Builder NumSVDDims(int constant)
            {
                _fieldHolder.numSvdDims = constant;
                return this;
            }

            /**
             * If numSVDDims is "adaptive", this controls the
             * smallest singular value that is retained as a fraction of the largest
             * singular value
             * 
             * @param fraction
             * @return  this Builder
             */
            public Builder FractionOfMax(double fraction)
            {
                _fieldHolder.fractionOfMax = fraction;
                return this;
            }

            /**
             * Limits the maximum number of the training
             * patterns stored. When KNN learns in a fixed capacity mode, the unused
             * patterns are deleted once the number of stored patterns is greater than
             * maxStoredPatterns. A value of -1 is no limit
             * 
             * @param max
             * @return  the Builder
             */
            public Builder MaxStoredPatterns(int max)
            {
                _fieldHolder.maxStoredPatterns = max;
                return this;
            }

            /**
             * A boolean flag that determines whether,
             * during learning, the classifier replaces duplicates that match exactly,
             * even if distThreshold is 0. Should be TRUE for online learning
             * @param b
             * @return  this Builder
             */
            public Builder ReplaceDuplicates(bool b)
            {
                _fieldHolder.replaceDuplicates = b;
                return this;
            }

            /**
             * If >= 1, input is assumed to be organized into
             * columns, in the same manner as the temporal pooler AND whenever a new
             * prototype is stored, only the start cell (first cell) is stored in any
             * bursting column
             * @param num
             * @return  this Builder
             */
            public Builder CellsPerCol(int num)
            {
                _fieldHolder.cellsPerCol = num;
                return this;
            }

            /// <summary>
            /// If useSparseMemory is set, only vectors with
            /// sparsity >= minSparsity will be stored during learning.A value of 0.0
            /// implies all vectors will be stored. A value of 0.1 implies only vectors
            /// with at least 10% sparsity will be stored
            /// </summary>
            public Builder MinSparsity(double minSparsity)
            {
                _fieldHolder.minSparsity = minSparsity;
                return this;
            }
        }

        #endregion

        public int Verbosity { get; set; }

        public int[] Steps { get; set; }
    }

    public record KnnInferResult(int? Winner, Vector<double> Inference, Vector<double> ProtoDistance, double[] CategoryDistance);

    [Serializable]
    public class KnnInferResult2 : NamedTuple
    {
        public KnnInferResult2(string[] keys, params object[] objects) : base(keys, objects)
        {
        }

        [Obsolete("Use specific methods", true)]
        public override object Get(string key)
        {
            throw new NotSupportedException("Use specific methods");
        }

        [Obsolete("Use specific methods", true)]
        public override object Get(int key)
        {
            throw new NotSupportedException("Use specific methods");
        }

        /// <summary>
        /// Gets the winner category
        /// </summary>
        /// <returns></returns>
        public int? GetWinner()
        {
            return (int?)base.Get("winner");
        }
        /// <summary>
        /// pct neighbors of each category
        /// </summary>
        /// <returns></returns>
        public Vector<double> GetInference()
        {
            return (Vector<double>)base.Get("inference");
        }
        /// <summary>
        /// dist of each prototype
        /// </summary>
        /// <returns></returns>
        public Vector<double> GetProtoDistance()
        {
            return (Vector<double>)base.Get("protoDistance");
        }
        /// <summary>
        /// dist of each category
        /// </summary>
        /// <returns></returns>
        public double[] GetCategoryDistances()
        {
            return (double[])base.Get("categoryDistances");
        }
    }

    //Utils
    [Serializable]
    public class NearestNeighbor
    {
        private Matrix<double> _backingMatrix;
        private int _addedRows;

        public NearestNeighbor(int rows, int cols)
        {
            Control.UseMultiThreading();
            _backingMatrix = new SparseMatrix(rows == 0 ? 1 : rows, cols);
            _addedRows = rows;
        }

        public NearestNeighbor(double[,] array)
        {
            Control.UseMultiThreading();
            _backingMatrix = SparseMatrix.OfArray(array);
            _addedRows = _backingMatrix.RowCount;
        }

        public int RowCount { get { return _addedRows; } }

        /// <summary>
        /// Adds a row of non-zeros to this SparseMatrix, from two iterators, one on
        /// a container of indices, the other on a container of values corresponding to those indices.
        /// </summary>
        /// <param name="inputPattern"></param>
        /// <param name="nonZeros"></param>
        public void AddRowNonZero(double[] inputPattern, int[] nonZeros)
        {
            if (inputPattern.Length > 0 && inputPattern.Max() > _backingMatrix.ColumnCount) 
                throw new ArgumentException("inputPattern has indices outside of matrix!");

            SparseVector vector = SparseVector.OfIndexedEnumerable(_backingMatrix.ColumnCount,
                inputPattern.Select((d, seq) => new Tuple<int, double>((int)d, nonZeros[seq])));
            if (RowCount == 0)
            {
                _backingMatrix.SetRow(_addedRows, vector);
            }
            else
            {
                _backingMatrix = (SparseMatrix)_backingMatrix.InsertRow(_addedRows, vector);
            }
            _addedRows++;
        }

        /// <summary>
        /// Adds a row of non-zeros to this SparseMatrix, from two iterators, one on
        /// a container of indices, the other on a container of values corresponding to those indices.
        /// </summary>
        /// <param name="inputPattern"></param>
        /// <param name="nonZeros"></param>
        public void AddRowNonZero(Vector<double> inputPattern, int[] nonZeros)
        {
            if (inputPattern.Count > 0 && inputPattern.Max() > _backingMatrix.ColumnCount)
                throw new ArgumentException("inputPattern has indices outside of matrix!");

            SparseVector vector = SparseVector.OfIndexedEnumerable(_backingMatrix.ColumnCount,
                inputPattern.Select((d, seq) => new Tuple<int, double>((int)d, nonZeros[seq])));
            if (RowCount == 0)
            {
                _backingMatrix.SetRow(_addedRows, vector);
            }
            else
            {
                _backingMatrix = (SparseMatrix)_backingMatrix.InsertRow(_addedRows, vector);
            }
            _addedRows++;
        }

        public double[] VecLpDist(double distanceNorm, double[] inputPattern, bool takeRoot = false)
        {
            Debug.Assert(RowCount > 0, "No vector stored yet");
            Debug.Assert(distanceNorm > 0.0, "Invalid value for parameter p: " + distanceNorm + " only positive values are supported");

            Vector<double> inputVector = Vector<double>.Build.DenseOfArray(inputPattern);

            if (distanceNorm == 0.0)
            {
                //L0Dist(x, y);
                throw new NotImplementedException(distanceNorm.ToString());
            }
            else if (distanceNorm == 1.0)
            {
                // Manhatten distance
                //L1Dist(x, y);
                double[] dist = new double[RowCount];
                foreach ((int, Vector<double>) indexedVector in _backingMatrix.EnumerateRowsIndexed())
                {
                    int index = indexedVector.Item1;
                    var vector = indexedVector.Item2;
                    dist[index] = MathNet.Numerics.Distance.Manhattan(inputVector, vector);
                }

                return dist;
            }
            else if (distanceNorm == 2.0)
            {
                // Euler distance
                //L2Dist(x, y);
                double[] dist = new double[RowCount];
                foreach ((int, Vector<double>) indexedVector in _backingMatrix.EnumerateRowsIndexed())
                {
                    int index = indexedVector.Item1;
                    var vector = indexedVector.Item2;
                    dist[index] = MathNet.Numerics.Distance.Euclidean(inputVector, vector);
                }

                return dist;
            }
            //all_rows_dist_(x, y, )
            throw new NotImplementedException(distanceNorm.ToString());
        }

        public Vector<double> VecLpDist(double distanceNorm, Vector<double> inputPattern, bool takeRoot = false)
        {
            Debug.Assert(RowCount > 0, "No vector stored yet");
            Debug.Assert(distanceNorm > 0.0, "Invalid value for parameter p: " + distanceNorm + " only positive values are supported");

            if (distanceNorm == 0.0)
            {
                //L0Dist(x, y);
                throw new NotImplementedException(distanceNorm.ToString());
            }
            else if (distanceNorm == 1.0)
            {
                // Manhatten distance
                //L1Dist(x, y);
                Vector<double> dist = Vector<double>.Build.Sparse(RowCount);
                foreach ((int, Vector<double>) indexedVector in _backingMatrix.EnumerateRowsIndexed())
                {
                    int index = indexedVector.Item1;
                    var vector = indexedVector.Item2;
                    dist[index] = MathNet.Numerics.Distance.Manhattan(inputPattern, vector);
                }

                return dist;
            }
            else if (distanceNorm == 2.0)
            {
                // Euler distance
                //L2Dist(x, y);
                Vector<double> dist = Vector<double>.Build.Sparse(RowCount);// new double[RowCount];
                foreach ((int, Vector<double>) indexedVector in _backingMatrix.EnumerateRowsIndexed())
                {
                    int index = indexedVector.Item1;
                    var vector = indexedVector.Item2;
                    dist[index] = MathNet.Numerics.Distance.Euclidean(inputPattern, vector);
                }

                return dist;
            }
            //all_rows_dist_(x, y, )
            throw new NotImplementedException(distanceNorm.ToString());
        }


        public void InsertRow(int row, Vector<double> denseVector)
        {
            if (RowCount == 0)
            {
                _backingMatrix.SetRow(row, denseVector);
            }
            else
            {
                _backingMatrix = _backingMatrix.InsertRow(row, denseVector);
            }

            _addedRows++;
        }

        public void InsertRow(int row, double[] denseVector)
        {
            if (RowCount == 0)
            {
                _backingMatrix.SetRow(row, denseVector);
            }
            else
            {
                _backingMatrix = _backingMatrix.InsertRow(row, Vector<double>.Build.DenseOfArray(denseVector));
            }

            _addedRows++;
        }

        public void RemoveRow(int row)
        {
            _backingMatrix = (SparseMatrix)_backingMatrix.RemoveRow(row);
            _addedRows--;
        }

        public double[][] ToDense()
        {
            return _backingMatrix.ToRowArrays();
        }

        public Matrix<double> GetMatrix()
        {
            return _backingMatrix;
        }

        public void ResizeMatrix(int rows, int cols)
        {
            _backingMatrix = _backingMatrix.Resize(rows, cols);
        }

        public void SetRow(int row, double[] values)
        {
            _backingMatrix.SetRow(row, values);
        }

        public Vector<double> RowSums()
        {
            return _backingMatrix.RowSums();
        }

        public double[] RightVecSumAtNz(double[] values)
        {
            return _backingMatrix.Multiply(Vector<double>.Build.DenseOfArray(values)).ToArray();
        }

        public Vector<double> RightVecSumAtNz(Vector<double> values)
        {
            return _backingMatrix.Multiply(values);
        }
    }

    [Serializable]
    public enum DistanceMethod
    {
        Norm,
        RawOverlap,
        PctInputOverlap,
        PctProtoOverlap,
        PctLargerOverlap
    }

    [Serializable]
    public enum KnnMode
    {
        ADAPTIVE = -1
    }
}