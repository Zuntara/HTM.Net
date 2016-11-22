using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
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
    public class SDRClassifier : IClassifier
    {
        public int[] Steps { get; set; }
        public double Alpha { get; set; }
        private readonly double _actValueAlpha;
        public int Verbosity { get; set; }

        private int _learnIteration;
        private int? _recordNumMinusLearnIteration;
        private static readonly int VERSION = 1;
        private readonly Deque<Tuple> _patternNZHistory;
        //private Map<int, int> _activeBitHistory;
        private int _maxInputIdx;
        private int _maxBucketIdx;
        private readonly Map<int, SparseMatrix> _weightMatrix;
        private readonly List<object> _actualValues;
        private string g_debugPrefix = "SDRClassifier";

        /// <summary>
        /// SDRClassifier no-arg constructor with defaults
        /// </summary>
        public SDRClassifier()
            : this(new[] { 1 })
        {

        }

        /// <summary>
        /// Constructor for the SDR classifier.
        /// </summary>
        /// <param name="steps">(list) Sequence of the different steps of multi-step predictions to learn</param>
        /// <param name="alpha">(float) The alpha used to adapt the weight matrix during learning. A larger alpha results in faster adaptation to the data.</param>
        /// <param name="actValueAlpha">(float) Used to track the actual value within each bucket. A lower actValueAlpha results in longer term memory</param>
        /// <param name="verbosity">verbosity (int) verbosity level, can be 0, 1, or 2</param>
        public SDRClassifier(int[] steps = null, double alpha = 0.001, double actValueAlpha = 0.3, int verbosity = 0)
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

            // Init learn iteration index
            _learnIteration = 0;

            // This contains the offset between the recordNum (provided by caller) and
            // learnIteration (internal only, always starts at 0).
            _recordNumMinusLearnIteration = null;

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

            // This contains the value of the highest input number we've ever seen 
            // It is used to pre-allocate fixed size arrays that hold the weights 
            _maxInputIdx = 0;

            // This contains the value of the highest bucket index we've ever seen 
            // It is used to pre-allocate fixed size arrays that hold the weights of 
            // each bucket index during inference 
            _maxBucketIdx = 0;

            // The connection weight matrix 
            _weightMatrix = new Map<int, SparseMatrix>();

            foreach (int step in steps)
            {
                _weightMatrix[step] = SparseMatrix.Create(_maxInputIdx + 1, _maxBucketIdx + 1, 0);
            }
            //for step in this.steps: 
            //    this._weightMatrix[step] = numpy.zeros(shape = (this._maxInputIdx + 1,
            //                                     this._maxBucketIdx + 1))

            // This keeps track of the actual value to use for each bucket index. We 
            // start with 1 bucket, no actual value so that the first infer has something 
            // to return 
            _actualValues = new List<object> { null };

            // Set the version to the latest version. 
            // This is used for serialization/deserialization 
        }

        public Classification<T> Compute<T>(int recordNum, IDictionary<string, object> classification, int[] patternNZ,
            bool learn, bool infer)
        {
            if (learn == false && infer == false) throw new InvalidOperationException("learn and infer cannot be both false");

            // Save the offset between recordNum and learnIteration if this is the first 
            //  compute 
            if (_recordNumMinusLearnIteration == null)
            {
                _recordNumMinusLearnIteration = recordNum - _learnIteration;
            }
            // Update the learn iteration
            _learnIteration = recordNum - _recordNumMinusLearnIteration.GetValueOrDefault();

            if (Verbosity >= 1)
            {
                Console.WriteLine(String.Format("\n{0}: compute ", g_debugPrefix));
                Console.WriteLine(" recordNum: " + recordNum);
                Console.WriteLine(" learnIteration: " + _learnIteration);
                Console.WriteLine(String.Format(" patternNZ({0}): {1}", patternNZ.Length, Arrays.ToString(patternNZ)));
                Console.WriteLine(" classificationIn: " + classification);
            }

            // Store pattern in our history
            _patternNZHistory.Append(new Tuple(_learnIteration, new HashSet<int>(patternNZ)));

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
                    if (_weightMatrix[nSteps].NonZerosCount > 0)
                    {
                        for (int i = _maxInputIdx; i < newMaxInputIdx; i++)
                        {
                            _weightMatrix[nSteps] = (SparseMatrix) _weightMatrix[nSteps]
                                .InsertRow(_weightMatrix[nSteps].RowCount, SparseVector.Create(_maxBucketIdx + 1, 0));
                        }
                    }
                    else
                    {
                        _weightMatrix[nSteps] = SparseMatrix.Create(newMaxInputIdx+1, _maxBucketIdx + 1, 0);
                    }
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

            //------------------------------------------------------------------------
            //Learning:
            if (learn && classification.Get("bucketIdx") != null)
            {
                // Get classification info
                int bucketIdx = (int)classification["bucketIdx"];
                object actValue = classification["actValue"];

                // Update maxBucketIndex and augment weight matrix with zero padding
                if (bucketIdx > _maxBucketIdx)
                {
                    foreach (int nSteps in Steps)
                    {
                        _weightMatrix[nSteps] = (SparseMatrix)_weightMatrix[nSteps]
                            .Append(SparseMatrix.Create(_weightMatrix[nSteps].RowCount, bucketIdx - _maxBucketIdx, 0));

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
                foreach (var tuple in _patternNZHistory)
                {
                    var iteration = (int)tuple.Get(0);
                    var learnPatternNZ = (HashSet<int>)tuple.Get(1);

                    var error = CalculateError(classification);

                    int nSteps = _learnIteration - iteration;
                    if (Steps.Contains(nSteps))
                    {
                        foreach (int bit in learnPatternNZ)
                        {
                            var multipliedRow = error[nSteps]*Alpha;// ArrayUtils.Multiply(error[nSteps], Alpha);
                            _weightMatrix[nSteps].SetRow(bit, _weightMatrix[nSteps].Row(bit).Add(multipliedRow));
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

                    Object[] actual = new Object[] { retVal.GetActualValue(key) };
                    Console.WriteLine(String.Format("  {0} steps: {1}", key, PFormatArray(actual)));
                    int bestBucketIdx = retVal.GetMostProbableBucketIndex(key);
                    Console.WriteLine(String.Format("   most likely bucket idx: {0}, value: {1} ", bestBucketIdx,
                        retVal.GetActualValue(bestBucketIdx)));

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
            Classification<T> retVal = new Classification<T>();
            retVal.SetActualValues(actValues);
            //NamedTuple retVal = new NamedTuple(new[] { "actualValues" }, new object[] { actValues});

            foreach (var nSteps in Steps)
            {
                var predictDist = InferSingleStep(new HashSet<int>(patternNz), _weightMatrix[nSteps]);
                retVal.SetStats(nSteps, predictDist.ToArray());
                //retVal[nSteps.ToString()] = predictDist;
            }

            return retVal;
        }
        /// <summary>
        /// Perform inference for a single step. Given an SDR input and a weight
        /// matrix, return a predicted distribution.
        /// </summary>
        /// <param name="patternNz">list of the active indices from the output below</param>
        /// <param name="weightMatrix">array of the weight matrix</param>
        /// <returns>array of the predicted class label distribution</returns>
        private Vector<double> InferSingleStep(HashSet<int> patternNz, SparseMatrix weightMatrix)
        {
            //var filtered = weightMatrix.Where((row, index) => patternNz.Contains(index)).ToList();
            var filtered = weightMatrix.EnumerateRowsIndexed().AsParallel().Where(t => patternNz.Contains(t.Item1)).Select(t=>t.Item2).ToList();
            Vector<double> outputActivation = SparseMatrix.Build.SparseOfRowVectors(filtered).ColumnSums();

            //var outputActivation = weightMatrix[patternNZ].sum(axis: 0); // axis 0 = cols, axis 1 = rows
            // softmax normalization
            Vector<double> expOutputActivation = outputActivation.PointwiseExp();
            //var predictDist = ArrayUtils.Divide(expOutputActivation, expOutputActivation.Sum()); //expOutputActivation / expOutputActivation.Sum();
            Vector<double> predictDist = expOutputActivation/expOutputActivation.Sum();
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
        private IDictionary<int, Vector<double>> CalculateError(IDictionary<string, object> classification)
        {
            IDictionary<int, Vector<double>> error = new Map<int, Vector<double>>();

            var targetDist = SparseVector.Create(_maxBucketIdx + 1, 0); //numpy.zeros(self._maxBucketIdx + 1);
            targetDist[(int)classification["bucketIdx"]] = 1.0;

            foreach (Tuple tuple in _patternNZHistory)
            {
                int iteration = (int)tuple.Get(0);
                HashSet<int> learnPatternNZ = (HashSet<int>)tuple.Get(1);
                var nSteps = _learnIteration - iteration;
                if (Steps.Contains(nSteps))
                {
                    var predictDist = InferSingleStep(learnPatternNZ, _weightMatrix[nSteps]);
                    error[nSteps] = targetDist - predictDist;// targetDist - predictDist;
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
                sb.Append(string.Format("{0:#.00}s", t));
            }
            sb.Append(" ]");
            return sb.ToString();
        }
    }
}