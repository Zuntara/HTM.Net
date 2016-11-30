using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Network;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra.Double;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Vision.Network
{
    public class KnnLayer : Layer<IInference>
    {
        private KnnMode? KnnMode;
        private int? SVDSampleCount, SVDDimCount;
        private int? fractionOfMax;
        private bool outputProbabilitiesByDist;
        private bool learningMode;
        private bool inferenceMode;
        private int _epoch;
        private double acceptanceProbability;
        private IRandom _rgen;
        private SparseMatrix confusion;
        private bool keepAllDistances;
        private Map<int, double[]> _protoScores;
        private int _protoScoreCount;
        private bool _useAuxiliary;
        private bool _justUseAuxiliary;
        private int verbosity;
        private int maxStoredPatterns;
        private int maxCategoryCount;
        private int _bestPrototypeIndexCount;
        private bool _doSphering;
        private double[] _normOffset;
        private double[] _normScale;
        private object _samples;
        private object _labels;
        private KNNClassifier _knn;
        private double[] _categoryDistances;

        public KnnLayer(Net.Network.Network n, Parameters p) : base(n, p)
        {
            Initialize(p);
        }

        public KnnLayer(string name, Net.Network.Network n, Parameters p) : base(name, n, p)
        {
            Initialize(p);
        }

        public KnnLayer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm, bool? autoCreateClassifiers,
            Anomaly a) : base(@params, e, sp, tm, autoCreateClassifiers, a)
        {
            Initialize(@params);
        }

        private void Initialize(Parameters p)
        {
            if (SVDSampleCount == 0)
            {
                SVDSampleCount = null;
            }
            if (SVDDimCount == -1)
            {
                KnnMode = null;
            }
            if (SVDDimCount == 0)
            {
                KnnMode = Algorithms.KnnMode.ADAPTIVE;
            }

            if (fractionOfMax == 0) fractionOfMax = null;

            // Initialize internal structures
            this.outputProbabilitiesByDist = false;
            this.learningMode = true;
            this.inferenceMode = false;
            this._epoch = 0;
            this.acceptanceProbability = 1.0;
            this._rgen = new XorshiftRandom(42); // seed
            this.confusion = new SparseMatrix(1, 1);
            this.keepAllDistances = false;
            this._protoScoreCount = 0;
            //this._useAuxiliary = useAuxiliary;
            //this._justUseAuxiliary = justUseAuxiliary;

            // Sphering normalization
            this._doSphering = false;
            this._normOffset = null;
            this._normScale = null;
            this._samples = null;
            this._labels = null;

            //Debugging
            this.verbosity = (int)p.GetParameterByKey(Parameters.KEY.SP_VERBOSITY);

            maxStoredPatterns = -1;
            maxCategoryCount = 0;
            _bestPrototypeIndexCount = 0;

            _knn = KNNClassifier.GetBuilder().Apply(p);

        }

        public override ILayer Close()
        {
            // Close base first
            base.Close();

            Func<MultiEncoder, NamedTuple> makeClassifiers = encoder =>
            {
                List<string> namesList = new List<string>();
                List<KNNClassifier> classifiers = new List<KNNClassifier>();

                int i = 0;
                foreach (EncoderTuple et in encoder.GetEncoders(encoder))
                {
                    if (et.GetEncoder() is SDRPassThroughEncoder)
                    {
                        namesList.Add(et.GetName());
                        classifiers.Add(KNNClassifier.GetBuilder().Apply(base.GetParameters()));
                    }
                    i++;
                }
                var result = new NamedTuple(namesList.ToArray(), classifiers.Select(c => (object)c).ToArray());
                return result;
            };

            // Assign classifier
            _factory.Inference.SetClassifiers(makeClassifiers(Encoder ?? ParentNetwork.GetEncoder()));

            // Adapt mask
            AlgoContentMask |= LayerMask.KnnClassifier;

            return this;
        }

        public void Clear()
        {
            _knn.Clear();
        }

        public override void Reset()
        {
            confusion = new SparseMatrix(1, 1);
        }

        /// <summary>
        /// Explicitly run inference on a vector that is passed in and return the
        /// category id. Useful for debugging.
        /// </summary>
        /// <param name="activeInput"></param>
        public int? DoInference(int[] activeInput)
        {
            var tuple = _knn.Infer(activeInput.Select(i => (double)i).ToArray());
            return (int?)tuple.Get(0);
        }

        private Map<string, object> _layerOutput=new Map<string, object>();

        public override void Compute<TInput>(TInput t)
        {
            if (t is ManualInput)
            {
                ManualInput input = TypeConverter.Convert<ManualInput>(t);

                var inputVector = ((int[])input.GetLayerInput()).Select(d => (double)d).ToArray();
                var cInputs = input.GetClassifierInput();
                Debug.Assert(cInputs.ContainsKey("categoryIn"));

                int[] categories = (int[]) cInputs["categoryIn"];

                
                List<double> categoriesOut = input.GetCategories();
                List<double> probabilitiesOut = input.GetCategoryProbabilities();
                List<int> bestPrototypeIndicesOut = input.GetBestPrototypeIndices();

                if (inferenceMode)
                {
                    if (_doSphering)
                    {
                        //inputVector = ArrayUtils.Multiply(ArrayUtils.Add(inputVector , _normOffset),_normScale);
                    }
                    int nPrototypes = 0;
                    if (bestPrototypeIndicesOut != null) // TODO: parameter?
                    {
                        nPrototypes = bestPrototypeIndicesOut.Count;
                    }

                    var inferResult = _knn.Compute(inputVector, -1, partitionId: null, learn: false, infer: true);
                    //var inferenceTuple = _knn.Infer(inputVector, partitionId: null);
                    //int? winner = (int?)inferenceTuple.Get(0);
                    //double[] inference = (double[])inferenceTuple.Get(1);
                    //double[] protoScores = (double[])inferenceTuple.Get(2);
                    //double[] categoryDistances = (double[])inferenceTuple.Get(3);

                    if (!keepAllDistances)
                    {
                        _protoScores = new Map<int, double[]>();
                        _protoScores[0] = inferResult.GetProtoDistance();
                        //throw new NotImplementedException();
                    }
                    else
                    {
                        // Keep all prototype scores in an array
                        if (_protoScores == null)
                        {
                            _protoScores = new Map<int, double[]>();
                            _protoScores[0] = inferResult.GetProtoDistance();
                            _protoScoreCount += 1;
                        }
                        else
                        {
                            // Store the new prototype score
                            _protoScores[_protoScoreCount] = inferResult.GetProtoDistance();
                            _protoScoreCount += 1;
                        }
                    }
                    _categoryDistances = inferResult.GetCategoryDistances();

                    // ----------------------------------------------------
                    // Compute the probability of each category
                    double[] scores;
                    if (outputProbabilitiesByDist)
                    {
                        scores = ArrayUtils.Sub(1.0, _categoryDistances);
                    }
                    else
                    {
                        scores = inferResult.GetInference();
                    }

                    // Probability is simply the scores/scores.sum()
                    double total = scores.Sum();
                    int numScores = 0;
                    double[] probabilities = null;
                    if (total == 0.0)
                    {
                        numScores = scores.Length;
                        probabilities = ArrayUtils.Divide(Enumerable.Range(0, numScores).Select(i => 1.0).ToArray(), numScores);
                    }
                    else
                    {
                        probabilities = ArrayUtils.Divide(scores, total);
                    }

                    // ----------------------------------------------------
                    // Fill the output vectors with our results
                    var nout = Math.Max(categoriesOut.Count, inferResult.GetInference().Length);// TODO: should be math.min ?
                    categoriesOut.Clear();
                    categoriesOut.AddRange(inferResult.GetInference().Take(nout));

                    probabilitiesOut.Clear();
                    probabilitiesOut.AddRange(probabilities.Take(nout));

                    if (verbosity >= 1)
                    {
                        Logger.Debug(string.Format("KNNLayer: categoriesOut: {0}", Arrays.ToString(categoriesOut)));
                        Logger.Debug(string.Format("KNNLayer: probabilitiesOut: {0}", Arrays.ToString(probabilitiesOut)));
                    }

                    //if (_scanInfo != null)
                    //{
                    //    _scanResults = new Tuple(inference.Take(nout).Select(o=>(object)o));
                    //}

                    // Update the stored confusion matrix.
                    foreach (var category in categories)
                    {
                        if (category >= 0)
                        {
                            int dims = Math.Max(category + 1, inferResult.GetInference().Length);
                            int oldDims = confusion.RowCount;
                            if (oldDims < dims)
                            {
                                // grow
                                int index = oldDims;
                                while (index < dims)
                                {
                                    confusion = (SparseMatrix)confusion.InsertRow(index, new SparseVector(confusion.ColumnCount));
                                    confusion = (SparseMatrix)confusion.InsertColumn(confusion.ColumnCount, new SparseVector(confusion.RowCount));
                                    index++;
                                }
                            }
                            confusion[ArrayUtils.Argmax(inferResult.GetInference()), category] += 1;
                        }
                    }

                    // Calculate the best prototype indices
                    if (nPrototypes > 1)
                    {
                        if (inferResult.GetCategoryDistances() != null)
                        {
                            var indices = ArrayUtils.Argsort(inferResult.GetCategoryDistances());
                            nout = Math.Max(indices.Length, nPrototypes);// TODO: should be math.min ?
                            bestPrototypeIndicesOut.Clear();
                            bestPrototypeIndicesOut.AddRange(indices.Take(nout));
                        }
                    }
                    else if (nPrototypes == 1)
                    {
                        if (inferResult.GetCategoryDistances() != null && inferResult.GetCategoryDistances().Length > 0)
                        {
                            bestPrototypeIndicesOut.Add(ArrayUtils.Argmin(inferResult.GetCategoryDistances()));
                        }
                        else
                        {
                            bestPrototypeIndicesOut.Add(0);
                        }
                    }
                }

                // -----------------------------------------------------------------
                // Learning mode
                if (learningMode)
                {
                    if (acceptanceProbability < 1.0 && _rgen.NextDouble() > acceptanceProbability)
                    {
                        return;
                    }
                    else
                    {
                        // Accept the input
                        foreach (var category in categories)
                        {
                            if (category >= 0)
                            {
                                // category values of -1 are to be skipped (they are non-categories)
                                if (_doSphering)
                                {
                                    // If we are sphering, then we can't provide the data to the KNN
                                    // library until we have computed per-dimension normalization
                                    // constants. So instead, we'll just store each training sample.
                                    //_storeSample(inputVector, category, partition);
                                }
                                else
                                {
                                    _knn.Compute(inputVector, category, partitionId: null, learn: true, infer: false);
                                }
                            }
                        }
                    }
                }
                _epoch += 1;
                return; // Done
            }
            throw new NotSupportedException("Input must come from another layer at this point.");
        }

        /// <summary>
        /// Does nothing. Kept here for API compatibility
        /// </summary>
        private void _finishLearning()
        {
            if (_doSphering)
            {
                throw new NotImplementedException();
                //_finishSphering();
            }
            _knn.FinishLearning();
            // self._accuracy = None
        }

        /// <summary>
        /// Get the value of the parameter.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="index"></param>
        /// <returns></returns>
        public object GetParameter(string name, int index = -1)
        {
            if (name == "patternCount")
            {
                return _knn.GetNumPatterns();
            }
            if (name == "learningMode")
            {
                return learningMode;
            }
            if (name == "inferenceMode")
            {
                return inferenceMode;
            }
            if (name == "categoryCount")
            {
                return _knn.GetCategoryList().Count;
            }
            throw new NotImplementedException();
        }

        public void SetParameter(string name, object value)
        {
            if (name == "learningMode")
            {
                learningMode = (bool) value;
                _epoch = 0;
            }
            else if (name == "inferenceMode")
            {
                _epoch = 0;
                if ((bool) value && !inferenceMode)
                    _finishLearning();
                inferenceMode = (bool) value;
            }
            else
            {
                throw new NotImplementedException();
            }
        }

    }
}