using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Model;
using HTM.Net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Algorithms
{
    /// <summary>
    /// A CLA classifier accepts a binary input from the level below (the
    /// "activationPattern") and information from the sensor and encoders (the
    /// "classification") describing the input to the system at that time step.
    /// 
    /// When learning, for every bit in activation pattern, it records a history of the
    /// classification each time that bit was active. The history is weighted so that
    /// more recent activity has a bigger impact than older activity. The alpha
    /// parameter controls this weighting.
    /// 
    /// For inference, it takes an ensemble approach. For every active bit in the
    /// activationPattern, it looks up the most likely classification(s) from the
    /// history stored for that bit and then votes across these to get the resulting
    /// classification(s).
    /// 
    /// This classifier can learn and infer a number of simultaneous classifications
    /// at once, each representing a shift of a different number of time steps. For
    /// example, say you are doing multi-step prediction and want the predictions for
    /// 1 and 3 time steps in advance. The CLAClassifier would learn the associations
    /// between the activation pattern for time step T and the classifications for
    /// time step T+1, as well as the associations between activation pattern T and
    /// the classifications for T+3. The 'steps' constructor argument specifies the
    /// list of time-steps you want.
    /// </summary>
    [Serializable]
    public class CLAClassifier : Persistable, IClassifier
    {
        #region Fields

        private double _actValueAlpha = 0.3d;

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
        /// This contains the value of the highest bucket index we've ever seen
        /// It is used to pre-allocate fixed size arrays that hold the weights of
        /// each bucket index during inference 
        /// </summary>
        private int _maxBucketIdx;

        /// <summary>
        /// History of the last _maxSteps activation patterns. We need to keep
        /// these so that we can associate the current iteration's classification
        /// with the activationPattern from N steps ago
        /// </summary>
        private Deque<Tuple> _patternNzHistory;

        /// <summary>
        /// These are the bit histories. Each one is a BitHistory instance, stored in
        /// this dict, where the key is (bit, nSteps). The 'bit' is the index of the
        /// bit in the activation pattern and nSteps is the number of steps of
        /// prediction desired for that bit.
        /// </summary>
        [JsonIgnore]
        private readonly Map<Tuple, BitHistory> _activeBitHistory = new Map<Tuple, BitHistory>();

        /// <summary>
        /// This keeps track of the actual value to use for each bucket index. We
        /// start with 1 bucket, no actual value so that the first infer has something
        /// to return
        /// </summary>
        private readonly List<object> _actualValues = new List<object>();

        private string g_debugPrefix = "CLAClassifier";

        #endregion

        #region Constructor(s)

        /// <summary>
        /// CLAClassifier no-arg constructor with defaults
        /// </summary>
        public CLAClassifier()
            : this(new[] { 1 }, 0.001, 0.3, 0)
        {

        }

        /// <summary>
        /// Constructor for the CLA classifier
        /// </summary>
        /// <param name="steps">sequence of the different steps of multi-step predictions to learn</param>
        /// <param name="alpha">The alpha used to compute running averages of the bucket duty
        /// cycles for each activation pattern bit. A lower alpha results in longer term memory.
        /// </param>
        /// <param name="actValueAlpha">Used to track the actual value within each bucket. A lower actValueAlpha results in longer term memory</param>
        /// <param name="verbosity">verbosity level, can be 0, 1, or 2</param>
        public CLAClassifier(int[] steps, double alpha, double actValueAlpha, int verbosity)
        {
            Steps = steps;
            Alpha = alpha;
            _actValueAlpha = actValueAlpha;
            Verbosity = verbosity;
            _actualValues.Add(null);
            _patternNzHistory = new Deque<Tuple>(ArrayUtils.Max(steps.ToArray()) + 1);
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process one input sample.
        /// This method is called by outer loop code outside the nupic-engine. We
        /// use this instead of the nupic engine compute() because our inputs and
        /// outputs aren't fixed size vectors of reals.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="recordNum">Record number of this input pattern. Record numbers should
        /// normally increase sequentially by 1 each time unless there
        /// are missing records in the dataset. Knowing this information
        /// insures that we don't get confused by missing records.</param>
        /// <param name="classification">Map of the classification information:
        /// bucketIdx: index of the encoder bucket
        /// actValue:  actual value going into the encoder</param>
        /// <param name="patternNZ">list of the active indices from the output below</param>
        /// <param name="learn">if true, learn this sample</param>
        /// <param name="infer">if true, perform inference</param>
        /// <returns>dict containing inference results, there is one entry for each
        /// step in steps, where the key is the number of steps, and
        /// the value is an array containing the relative likelihood for
        /// each bucketIdx starting from bucketIdx 0.
        /// 
        /// There is also an entry containing the average actual value to
        /// use for each bucket. The key is 'actualValues'.
        /// 
        /// for example:
        /// {	
        /// 	1 :             [0.1, 0.3, 0.2, 0.7],
        /// 	4 :             [0.2, 0.4, 0.3, 0.5],
        /// 	'actualValues': [1.5, 3,5, 5,5, 7.6],
        /// }
        /// </returns>
        public IClassification<T> Compute<T>(int recordNum, IDictionary<string, object> classification, int[] patternNZ, bool learn, bool infer)
        {
            Classification<T> retVal = new Classification<T>();
            List<object> actualValues = _actualValues.Select(av => av == null ? null : av).ToList();

            // Save the offset between recordNum and learnIteration if this is the first
            // compute
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

            _patternNzHistory.Append(new Tuple(_learnIteration, patternNZ));

            //------------------------------------------------------------------------
            // Inference:
            // For each active bit in the activationPattern, get the classification
            // votes
            //
            // Return value dict. For buckets which we don't have an actual value
            // for yet, just plug in any valid actual value. It doesn't matter what
            // we use because that bucket won't have non-zero likelihood anyways.
            if (infer)
            {
                // NOTE: If doing 0-step prediction, we shouldn't use any knowledge
                //		 of the classification input during inference.
                object defaultValue = null;
                if (Steps[0] == 0)
                {
                    defaultValue = 0;
                }
                else
                {
                    defaultValue = classification.Get("actValue");
                }

                T[] actValues = new T[this._actualValues.Count];
                for (int i = 0; i < actualValues.Count; i++)
                {
                    actValues[i] = (T)(actualValues[i] == null ? TypeConverter.Convert<T>(defaultValue) : actualValues[i]);
                }

                retVal.SetActualValues(actValues);

                // For each n-step prediction...
                foreach (int nSteps in Steps.ToArray())
                {
                    // Accumulate bucket index votes and actValues into these arrays
                    double[] sumVotes = new double[_maxBucketIdx + 1];
                    double[] bitVotes = new double[_maxBucketIdx + 1];

                    foreach (int bit in patternNZ)
                    {
                        Tuple key = new Tuple(bit, nSteps);
                        BitHistory history = _activeBitHistory.Get(key);
                        if (history == null) continue;

                        history.Infer(_learnIteration, bitVotes);

                        sumVotes = ArrayUtils.Add(sumVotes, bitVotes);
                    }

                    // Return the votes for each bucket, normalized
                    double total = ArrayUtils.Sum(sumVotes);
                    if (total > 0)
                    {
                        sumVotes = ArrayUtils.Divide(sumVotes, total);
                    }
                    else
                    {
                        // If all buckets have zero probability then simply make all of the
                        // buckets equally likely. There is no actual prediction for this
                        // timestep so any of the possible predictions are just as good.
                        if (sumVotes.Length > 0)
                        {
                            Arrays.Fill(sumVotes, 1.0 / (double)sumVotes.Length);
                        }
                    }

                    retVal.SetStats(nSteps, sumVotes);
                }
            }

            // ------------------------------------------------------------------------
            // Learning:
            // For each active bit in the activationPattern, store the classification
            // info. If the bucketIdx is None, we can't learn. This can happen when the
            // field is missing in a specific record.
            if (learn && classification.Get("bucketIdx") != null)
            {
                // Get classification info
                int bucketIdx = (int)classification["bucketIdx"];
                object actValue = classification["actValue"];

                // Update maxBucketIndex
                _maxBucketIdx = Math.Max(_maxBucketIdx, bucketIdx);

                // Update rolling average of actual values if it's a scalar. If it's
                // not, it must be a category, in which case each bucket only ever
                // sees one category so we don't need a running average.
                while (_maxBucketIdx > _actualValues.Count - 1)
                {
                    _actualValues.Add(null);
                }

                if (_actualValues[bucketIdx] == null)
                {
                    _actualValues[bucketIdx] = TypeConverter.Convert<T>(actValue);
                }
                else
                {
                    if (typeof(double).IsAssignableFrom(actValue.GetType()))
                    {
                        Double val = ((1.0 - _actValueAlpha) * (TypeConverter.Convert<double>(_actualValues[bucketIdx])) +
                            _actValueAlpha * (TypeConverter.Convert<double>(actValue)));
                        _actualValues[bucketIdx] = TypeConverter.Convert<T>(val);
                    }
                    else
                    {
                        _actualValues[bucketIdx] = TypeConverter.Convert<T>(actValue);
                    }
                }

                // Train each pattern that we have in our history that aligns with the
                // steps we have in steps
                int nSteps = -1;
                int iteration = 0;
                int[] learnPatternNZ = null;
                foreach (int n in Steps.ToArray())
                {
                    nSteps = n;
                    // Do we have the pattern that should be assigned to this classification
                    // in our pattern history? If not, skip it
                    bool found = false;
                    foreach (Tuple t in _patternNzHistory)
                    {
                        iteration = TypeConverter.Convert<int>(t.Get(0));
                        
                        var tuplePos1 = t.Get(1);
                        if (tuplePos1 is JArray)
                        {
                            JArray arr = (JArray)tuplePos1;
                            learnPatternNZ = arr.Values<int>().ToArray();
                        }
                        else
                        {
                            learnPatternNZ = (int[])t.Get(1);
                        }

                        if (iteration == _learnIteration - nSteps)
                        {
                            found = true;
                            break;
                        }
                        iteration++;
                    }
                    if (!found) continue;

                    // Store classification info for each active bit from the pattern
                    // that we got nSteps time steps ago.
                    foreach (int bit in learnPatternNZ)
                    {
                        // Get the history structure for this bit and step
                        Tuple key = new Tuple(bit, nSteps);
                        BitHistory history = _activeBitHistory.GetOrDefault(key, null);
                        if (history == null)
                        {
                            _activeBitHistory.Add(key, history = new BitHistory(this, bit, nSteps));
                        }
                        history.Store(_learnIteration, bucketIdx);
                    }
                }
            }

            if (infer && Verbosity >= 1)
            {
                Console.WriteLine(" inference: combined bucket likelihoods:");
                Console.WriteLine($"   actual bucket values: {Arrays.ToString((T[])retVal.GetActualValues())}");

                foreach (int key in retVal.StepSet())
                {
                    if (retVal.GetActualValue(key) == null) continue;

                    Object[] actual = new Object[] { (T)retVal.GetActualValue(key) };
                    Console.WriteLine($"  {key} steps: {PFormatArray(actual)}");
                    int bestBucketIdx = retVal.GetMostProbableBucketIndex(key);
                    Console.WriteLine(
                        $"   most likely bucket idx: {bestBucketIdx}, value: {retVal.GetActualValue(bestBucketIdx)} ");

                }
            }

            return retVal;
        }

        /// <summary>
        /// Return a string with pretty-print of an array using the given format for each element
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="arr"></param>
        /// <returns></returns>
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

        #region Properties

        public int Verbosity { get; set; }

        /// <summary>
        /// The alpha used to compute running averages of the bucket duty
        /// cycles for each activation pattern bit.A lower alpha results
        /// in longer term memory.
        /// </summary>
        public double Alpha { get; set; } = 0.001;

        /// <summary>
        /// The sequence different steps of multi-step predictions
        /// </summary>
        public int[] Steps { get; set; }

        #endregion
    }
}