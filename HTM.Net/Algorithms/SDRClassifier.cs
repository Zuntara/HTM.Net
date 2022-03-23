using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Model;
using HTM.Net.Util;

using MathNet.Numerics.LinearAlgebra.Double;

using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Algorithms
{
    // https://github.com/numenta/nupic/blob/master/src/nupic/algorithms/sdr_classifier.py

    /// <summary>
    /// Implementation of a SDR classifier.
    /// 
    /// The SDR classifier takes the form of a single layer classification network
    /// that takes SDRs as input and outputs a predicted distribution of classes.
    /// 
    /// The SDR Classifier accepts a binary input pattern from the 
    /// level below(the "activationPattern") and information from the sensor and
    /// encoders(the "classification") describing the true (target) input.
    /// 
    /// The SDR classifier maps input patterns to class labels. There are as many 
    /// output units as the number of class labels or buckets(in the case of scalar 
    /// encoders). The output is a probabilistic distribution over all class labels. 
    /// 
    /// During inference, the output is calculated by first doing a weighted summation 
    /// of all the inputs, and then perform a softmax nonlinear function to get
    /// the predicted distribution of class labels
    /// 
    /// During learning, the connection weights between input units and output units
    /// are adjusted to maximize the likelihood of the model 
    /// 
    /// The SDR Classifier is a variation of the previous CLAClassifier which was
    /// not based on the references below.
    /// 
    /// Example Usage: 
    /// 
    /// c = SDRClassifier(steps =[1], alpha = 0.1, actValueAlpha = 0.1, verbosity = 0) 
    /// 
    /// // learning 
    /// c.compute(recordNum= 0, patternNZ=[1, 5, 9],
    ///           classification={ "bucketIdx": 4, "actValue": 34.7}, 
    ///           learn=True, infer=False) 
    /// 
    /// // inference 
    /// result = c.compute(recordNum=1, patternNZ=[1, 5, 9], 
    ///                    classification={"bucketIdx": 4, "actValue": 34.7}, 
    ///                    learn=False, infer=True) 
    /// 
    /// // Print the top three predictions for 1 steps out. 
    /// topPredictions = sorted(zip(result[1],
    ///                         result["actualValues"]), reverse=True)[:3] 
    /// for probability, value in topPredictions: 
    ///   print "Prediction of {} has probability of {}.".format(value,
    ///                                                          probability*100.0)
    /// 
    /// References: 
    ///   Alex Graves.Supervised Sequence Labeling with Recurrent Neural Networks 
    ///   PhD Thesis, 2008 
    /// 
    ///   J.S.Bridle.Probabilistic interpretation of feedforward classification
    ///   network outputs, with relationships to statistical pattern recognition. 
    ///   In F. Fogleman-Soulie and J.Herault, editors, Neurocomputing: Algorithms,
    ///   Architectures and Applications, pp 227-236, Springer-Verlag, 1990 
    /// 
    /// </summary>
    public class SDRClassifier : Persistable, IClassifier
    {
        public int[] Steps { get; set; }
        
        public double Alpha { get; set; } = 0.001d;

        private readonly double _actValueAlpha = 0.3d;

        public int Verbosity { get; set; } = 0;

        private int _learnIteration;
        private int _recordNumMinusLearnIteration = -1;
        private static readonly int VERSION = 1;
        private Deque<Tuple> _patternNZHistory;
        //private Map<int, int> _activeBitHistory;
        private int _maxInputIdx = 0;
        private int _maxBucketIdx;
        private Map<int, SparseMatrix> _weightMatrix;
        private readonly List<object> _actualValues;
        private string g_debugPrefix = "SDRClassifier";

        /// <summary>
        /// SDRClassifier no-arg constructor with defaults
        /// </summary>
        public SDRClassifier()
            : this(new []{1}, 0.001d, 0.3d, 0)
        {

        }

        /// <summary>
        /// Constructor for the SDR classifier.
        /// </summary>
        /// <param name="steps">(list) Sequence of the different steps of multi-step predictions to learn</param>
        /// <param name="alpha">(float) The alpha used to adapt the weight matrix during learning. A larger alpha results in faster adaptation to the data.</param>
        /// <param name="actValueAlpha">(float) Used to track the actual value within each bucket. A lower actValueAlpha results in longer term memory</param>
        /// <param name="verbosity">verbosity (int) verbosity level, can be 0, 1, or 2</param>
        public SDRClassifier(int[] steps, double alpha = 0.001, double actValueAlpha = 0.3, int verbosity = 0)
        {
            if (steps == null || steps.Length == 0) throw new ArgumentException("Steps cannot be empty");
            if (steps.Any(s => s < 0)) throw new ArgumentException("steps must be a list of non-negative ints");
            if (alpha < 0) throw new ArgumentException("alpha (learning rate) must be a positive number");
            if (actValueAlpha < 0 || actValueAlpha >= 1) throw new ArgumentOutOfRangeException(nameof(actValueAlpha), "actValueAlpha be a number between 0 and 1");

            // Save constructor args
            Steps = steps;
            Alpha = alpha;
            _actValueAlpha = actValueAlpha;
            Verbosity = verbosity;

            // Max // of steps of prediction we need to support
            int maxSteps = steps.Max() + 1;

            // History of the last _maxSteps activation patterns. We need to keep 
            // these so that we can associate the current iteration's classification 
            // with the activationPattern from N steps ago 
            _patternNZHistory = new Deque<Tuple>(maxSteps);

            // These are the bit histories. Each one is a BitHistory instance, stored in 
            // this dict, where the key is (bit, nSteps). The 'bit' is the index of the 
            // bit in the activation pattern and nSteps is the number of steps of 
            // prediction desired for that bit. 
            //_activeBitHistory = new Map<int, int>();

            // The connection weight matrix 
            _weightMatrix = new Map<int, SparseMatrix>();

            foreach (int step in steps)
            {
                _weightMatrix.Add(step, new SparseMatrix(_maxBucketIdx + 1, _maxInputIdx + 1));
                //_weightMatrix[step] = ArrayUtils.CreateJaggedArray<double>(_maxInputIdx + 1, _maxBucketIdx + 1);
            }
            //for step in this.steps: 
            //    this._weightMatrix[step] = numpy.zeros(shape = (this._maxInputIdx + 1,
            //                                     this._maxBucketIdx + 1))

            // This keeps track of the actual value to use for each bucket index. We 
            // start with 1 bucket, no actual value so that the first infer has something 
            // to return 
            _actualValues = new List<object>();
            _actualValues.Add(null);
            // Set the version to the latest version. 
            // This is used for serialization/deserialization 
        }

        /// <summary>
        /// Process one input sample.
        /// This method is called by outer loop code outside the nupic-engine.
        /// We use this instead of the nupic engine compute() because our inputs and
        /// outputs aren't fixed size vectors of reals.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="recordNum">
        /// Record number of this input pattern. Record numbers normally increase
        /// sequentially by 1 each time unless there are missing records in the
        /// dataset.Knowing this information ensures that we don't get confused by
        /// missing records.</param>
        /// <param name="classification">
        /// <see cref="Map{TKey,TValue}"/> of the classification information:
        /// <p>&emsp;"bucketIdx" - index of the encoder bucket
        /// <p>&emsp;"actValue" -  actual value doing into the encoder</param>
        /// <param name="patternNZ">List of the active indices from the output below. When the output is from the TemporalMemory, this array should be the indices of the active cells.</param>
        /// <param name="learn">If true, learn this sample.</param>
        /// <param name="infer">If true, perform inference. If false, null will be returned.</param>
        /// <returns>
        /// <see cref="Classification{T}"/> containing inference results if {@code learn} param is true,
        /// otherwise, will return {@code null}. The Classification
        /// contains the computed probability distribution(relative likelihood for each
        /// bucketIdx starting from bucketIdx 0) for each step in {@code steps}. Each bucket's
        /// likelihood can be accessed individually, or all the buckets' likelihoods can
        /// be obtained in the form of a double array.</returns>
        /// <exception cref="InvalidOperationException"></exception>
        /// <example>
        /// <code>
        /// // Get likelihood val for bucket 0, 5 steps in future
        /// classification.getStat(5, 0);
        /// 
        /// //Get all buckets' likelihoods as double[] where each
        /// //index is the likelihood for that bucket
        /// //(e.g. [0] contains likelihood for bucketIdx 0)
        /// classification.getStats(5);
        /// }</pre>
        ///
        /// 
        /// The Classification also contains the average actual value for each bucket.
        /// The average values for the buckets can be accessed individually, or altogether
        /// as a double[].
        ///  
        ///  <pre>{
        ///        
        ///  // Get average actual val for bucket 0
        ///  classification.getActualValue(0);
        ///  
        ///  // Get average vals for all buckets as double[], where
        ///  // each index is the average val for that bucket
        ///  // (e.g. [0] contains average val for bucketIdx 0)
        ///  classification.getActualValues();
        ///   }</pre>
        ///
        ///        
        /// The Classification can also be queried for the most probable bucket(the bucket
        /// with the highest associated likelihood value), as well as the average input value
        ///
        ///  that corresponds to that bucket.
        ///    
        ///  <pre>{
        ///        
        ///  // Get index of most probable bucket
        ///  classification.getMostProbableBucketIndex();
        ///       
        ///  // Get the average actual val for that bucket
        ///  classification.getMostProbableValue();
        ///  }</pre>
        /// </code>
        /// </example>
        public Classification<T> Compute<T>(int recordNum, IDictionary<string, object> classification, int[] patternNZ,
            bool learn, bool infer)
        {
            if (learn == false && infer == false) throw new InvalidOperationException("learn and infer cannot be both false");

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
            _patternNZHistory.Append(new Tuple(_learnIteration, patternNZ));

            // To allow multi-class classification, we need to be able to run learning
            // without inference being on. So initialize retval outside
            // of the inference block.
            Classification<T> retVal = null;

            // Update maxInputIdx and augment weight matrix with zero padding
            if (patternNZ.Max() > _maxInputIdx)
            {
                int newMaxInputIdx = patternNZ.Max();
                foreach (int nSteps in Steps)
                {
                    for (int i = _maxInputIdx; i < newMaxInputIdx; i++)
                    {
                        var matrix = _weightMatrix.Get(nSteps);

                        matrix = (SparseMatrix)matrix.InsertColumn(matrix.ColumnCount, new SparseVector(_maxBucketIdx + 1));
                        _weightMatrix[nSteps] = matrix;
                    }

                    //var subMatrix = ArrayUtils.CreateJaggedArray<double>(newMaxInputIdx - _maxInputIdx, _maxBucketIdx + 1);
                    //_weightMatrix[nSteps] = ArrayUtils.Concatinate(_weightMatrix[nSteps], subMatrix, 0);
                }
                _maxInputIdx = newMaxInputIdx;
            }

            // --------------------------------------------------------------------
            // Inference:
            // For each active bit in the activationPattern, get the classification votes
            if (infer)
            {
                retVal = Infer<T>(patternNZ, classification);
            }

            if (learn && classification["bucketIdx"] != null)
            {
                // Get classification info
                int bucketIdx = (int)classification["bucketIdx"];
                object actValue = classification["actValue"];

                // Update maxBucketIndex and augment weight matrix with zero padding
                if (bucketIdx > _maxBucketIdx)
                {
                    foreach (int nSteps in Steps)
                    {
                        for (int i = _maxBucketIdx; i < bucketIdx; i++)
                        {
                            var matrix = _weightMatrix.Get(nSteps);
                            matrix = (SparseMatrix)matrix.InsertRow(matrix.RowCount, new DenseVector(_maxInputIdx + 1));
                            _weightMatrix[nSteps] = matrix;
                        }
                        //var subMatrix = ArrayUtils.CreateJaggedArray<double>(_maxInputIdx + 1, bucketIdx - _maxBucketIdx);
                        //_weightMatrix[nSteps] = ArrayUtils.Concatinate(_weightMatrix[nSteps], subMatrix, 1);
                    }
                    _maxBucketIdx = bucketIdx;
                }

                // Update rolling average of actual values if it's a scalar. If it's
                // not, it must be a category, in which case each bucket only ever
                // sees one category so we don't need a running average.
                while (_maxBucketIdx > _actualValues.Count - 1)
                {
                    _actualValues.Add(null);
                }
                if (_actualValues[bucketIdx] == null)
                {
                    _actualValues[bucketIdx] = actValue;
                }
                else
                {
                    if (actValue is int || actValue is double || actValue is long)
                    {
                        if (actValue is int)
                        {
                            _actualValues[bucketIdx] = ((1.0 - _actValueAlpha) * TypeConverter.Convert<double>(_actualValues[bucketIdx]) +
                                                        _actValueAlpha * (int)actValue);
                        }
                        if (actValue is double)
                        {
                            _actualValues[bucketIdx] = ((1.0 - _actValueAlpha) * (double)_actualValues[bucketIdx] +
                                                        _actValueAlpha * (double)actValue);
                        }
                        if (actValue is long)
                        {
                            _actualValues[bucketIdx] = ((1.0 - _actValueAlpha) * TypeConverter.Convert<double>(_actualValues[bucketIdx]) +
                                                        _actValueAlpha * (long)actValue);
                        }
                    }
                    else
                    {
                        _actualValues[bucketIdx] = actValue;
                    }
                }

                int iteration = 0;
                int[] learnPatternNZ = null;
                foreach (var tuple in _patternNZHistory)
                {
                    iteration = (int)tuple.Get(0);
                    learnPatternNZ = (int[])tuple.Get(1);

                    var error = CalculateError(classification);

                    int nSteps = _learnIteration - iteration;
                    if (Steps.Contains(nSteps))
                    {
                        for (int row = 0; row <= _maxBucketIdx; row++)
                        {
                            foreach (int bit in learnPatternNZ)
                            {
                                var matrix = _weightMatrix.Get(nSteps);
                                var rowVec = matrix.Row(row);
                                rowVec.At(bit, (Alpha * error[nSteps][row]) + rowVec.At(bit));
                                matrix.SetRow(row, rowVec);
                                //var multipliedRow = ArrayUtils.Multiply(error[nSteps], Alpha);
                                //_weightMatrix[nSteps][bit] = ArrayUtils.Add(multipliedRow, _weightMatrix[nSteps][bit]);
                            }
                        }
                        
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

                    T[] actual = { retVal.GetActualValue(key) };
                    Console.WriteLine($"  {key} steps: {PFormatArray(actual)}");
                    int bestBucketIdx = retVal.GetMostProbableBucketIndex(key);
                    Console.WriteLine($"   most likely bucket idx: {bestBucketIdx}, value: {retVal.GetActualValue(bestBucketIdx)} ");

                }
                /*
                  print "  inference: combined bucket likelihoods:"
                  print "    actual bucket values:", retval["actualValues"]
                  for (nSteps, votes) in retval.items():
                    if nSteps == "actualValues":
                      continue
                    print "    %d steps: " % (nSteps), _pFormatArray(votes)
                    bestBucketIdx = votes.argmax()
                    print ("      most likely bucket idx: "
                           "%d, value: %s" % (bestBucketIdx,
                                              retval["actualValues"][bestBucketIdx]))
                  print
                */
            }
            return retVal;
        }

        /// <summary>
        /// Return the inference value from one input sample. The actual learning happens in compute().
        /// </summary>
        /// <param name="patternNz">list of the active indices from the output below</param>
        /// <param name="classification">
        /// dict of the classification information:
        /// - bucketIdx: index of the encoder bucket
        /// - actValue:  actual value going into the encoder
        /// </param>
        /// <returns>
        /// dict containing inference results, one entry for each step in
        /// self.steps.The key is the number of steps, the value is an
        /// array containing the relative likelihood for each bucketIdx
        /// starting from bucketIdx 0.
        /// 
        ///        for example:
        ///          {'actualValues': [0.0, 1.0, 2.0, 3.0]
        ///            1 : [0.1, 0.3, 0.2, 0.7]
        ///            4 : [0.2, 0.4, 0.3, 0.5]
        ///         }
        /// </returns>
        public Classification<T> Infer<T>(int[] patternNz, IDictionary<string, object> classification)
        {
            Classification<T> retVal = new Classification<T>();
            // Return value dict. For buckets which we don't have an actual value
            // for yet, just plug in any valid actual value. It doesn't matter what
            // we use because that bucket won't have non-zero likelihood anyways.
            // 
            // NOTE: If doing 0-step prediction, we shouldn't use any knowledge
            // of the classification input during inference.
            object defaultValue;
            if (Steps[0] == 0 || classification == null)
            {
                defaultValue = 0;
            }
            else
            {
                defaultValue = classification["actValue"];
            }
            var actValues = _actualValues.Select(x => (T)(x ?? TypeConverter.Convert<T>(defaultValue))).ToArray();
            for (int i = 0; i < _actualValues.Count; i++)
            {
                actValues[i] = (T)(_actualValues[i] == null ? TypeConverter.Convert<T>(defaultValue) : _actualValues[i]);
            }

            retVal.SetActualValues(actValues);
           
            foreach (var nSteps in Steps)
            {
                var predictDist = InferSingleStep(patternNz, _weightMatrix[nSteps]);
                retVal.SetStats(nSteps, predictDist);
            }

            return retVal;
        }

        /// <summary>
        /// Perform inference for a single step. Given an SDR input and a weight
        /// matrix, return a predicted distribution.
        /// </summary>
        /// <param name="patternNZ">list of the active indices from the output below</param>
        /// <param name="weightMatrix">array of the weight matrix</param>
        /// <returns>array of the predicted class label distribution</returns>
        private double[] InferSingleStep(int[] patternNZ, SparseMatrix weightMatrix)
        {
            // Compute the output activation "level" for each bucket (matrix row)
            // we've seen so far and store in double[]
            double[] outputActivation = new double[_maxBucketIdx + 1];
            for (int row = 0; row <= _maxBucketIdx; row++)
            {
                // Output activation for this bucket is computed as the sum of
                // the weights for the the active bits in patternNZ, for current
                // row of matrix.
                foreach (int bit in patternNZ)
                {
                    outputActivation[row] += weightMatrix.At(row, bit);
                }
            }

            // Softmax normalization
            double[] expOutputActivation = new double[outputActivation.Length];
            for (int i = 0; i < expOutputActivation.Length; i++)
            {
                expOutputActivation[i] = Math.Exp(outputActivation[i]);
            }

            double[] predictDist = new double[outputActivation.Length];
            for (int i = 0; i < predictDist.Length; i++)
            {
                predictDist[i] = expOutputActivation[i] / ArrayUtils.Sum(expOutputActivation);
            }

            return predictDist;
        }

        /// <summary>
        /// Calculate error signal
        /// </summary>
        /// <param name="classification">
        /// dict of the classification information:
        /// - bucketIdx: index of the encoder bucket
        /// - actValue:  actual value going into the encoder
        /// </param>
        /// <returns>dict containing error. 
        /// The key is the number of steps The value is a numpy array of error at the output layer</returns>
        private IDictionary<int, double[]> CalculateError(IDictionary<string, object> classification)
        {
            IDictionary<int, double[]> error = new Map<int, double[]>();

            var targetDist = new int[_maxBucketIdx + 1];
            targetDist[(int)classification["bucketIdx"]] = 1;

            int iteration = 0;
            int[] learnPatternNZ = null;
            int nSteps = 0;
            foreach (Tuple tuple in _patternNZHistory)
            {
                iteration = (int)tuple.Get(0);
                learnPatternNZ = (int[])tuple.Get(1);
                nSteps = _learnIteration - iteration;
                if (Steps.Contains(nSteps))
                {
                    double[] predictDist = InferSingleStep(learnPatternNZ, _weightMatrix[nSteps]);
                    double[] targetDistMinusPredictDist = new double[_maxBucketIdx + 1];
                    for (int i = 0; i <= _maxBucketIdx; i++)
                    {
                        targetDistMinusPredictDist[i] = targetDist[i] - predictDist[i];
                    }
                    error.Add(nSteps, targetDistMinusPredictDist);
                }
            }

            return error;
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
    }
}