using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.opf
{
    public class MetricSpec : ICloneable
    {
        public string Metric { get; internal set; }
        public InferenceElement? InferenceElement { get; }
        public string Field { get; }
        public IDictionary<string, object> Params { get; }

        public MetricSpec(string metric, InferenceElement? inferenceElement, string field = null, IDictionary<string, object> @params = null)
        {
            this.Metric = metric;
            this.InferenceElement = inferenceElement;
            this.Field = field;
            this.Params = @params;
        }

        /// <summary>
        /// Helper method that generates a unique label for a MetricSpec / InferenceType pair.The label is formatted as follows:
        /// <predictionKind>:<metric type>:(paramName= value)*:field=<fieldname>
        /// For example:
        ///     classification:aae:paramA=10.2:paramB=20:window=100:field=pounds
        /// </summary>
        /// <param name="inferenceType"></param>
        /// <returns></returns>
        public string getLabel(InferenceType? inferenceType = null)
        {

            var result = new List<string>();
            if (inferenceType.HasValue)
            {
                result.Add(inferenceType.ToString());
            }
            result.Add(this.InferenceElement.ToString().ToLowerInvariant());
            result.Add(this.Metric.ToLowerInvariant());

            var @params = this.Params;
            if (@params != null)
            {
                var sortedParams = @params.Keys.ToList();
                sortedParams.Sort();
                foreach (var param in sortedParams)
                {
                    // Don't include the customFuncSource - it is too long an unwieldy
                    if (new[] { "customFuncSource", "customFuncDef", "customExpr" }.Contains(param))
                    {
                        continue;
                    }

                    var value = @params[param];
                    if (value is string)
                    {
                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0}=\"{1}\"", param, value));
                    }
                    else if (value is IEnumerable)
                    {
                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1}", param, Arrays.ToString((IEnumerable)value)));
                    }
                    else
                    {
                        result.Add(string.Format(CultureInfo.InvariantCulture, "{0}={1}", param, value));
                    }
                }
            }

            if (this.Field != null)
                result.Add("field=" + this.Field);

            return string.Join(":", result);
        }

        /// <summary>
        /// factory method to return an appropriate MetricsIface-based module
        /// </summary>
        /// <param name="metricSpec">n instance of MetricSpec
        /// metricSpec.metric must be one of:
        ///     rmse(root-mean-square error)
        ///     aae(average absolute error)
        ///     acc(accuracy, for enumerated types)
        /// </param>
        /// <returns>an appropriate Metric module</returns>
        public static MetricIFace GetModule(MetricSpec metricSpec)
        {
            string metricName = metricSpec.Metric;

            switch (metricName)
            {
                case "rmse":
                    return new MetricRMSE(metricSpec);
                case "aae":
                    return new MetricAAE(metricSpec);
                case "altMAPE":
                    return new MetricAltMAPE(metricSpec);
                case "avg_err":
                    return new MetricAveError(metricSpec);
                case "multistep":
                case "multiStep":
                    return new MetricMultiStep(metricSpec);
            }
            throw new InvalidOperationException($"Invalid metric given: {metricName}");
        }

        public object Clone()
        {
            MetricSpec spec = new MetricSpec(Metric, InferenceElement, Field, new Dictionary<string, object>(Params));
            return spec;
        }
    }

    /// <summary>
    /// A Metrics module compares a prediction Y to corresponding ground truth X and returns a single
    /// measure representing the "goodness" of the prediction.It is up to the implementation to
    /// determine how this comparison is made.
    /// </summary>
    public abstract class MetricIFace
    {
        protected MetricIFace(MetricSpec metricSpec)
        {

        }

        /// <summary>
        /// add one instance consisting of ground truth and a prediction.
        /// </summary>
        /// <param name="groundTruth">The actual measured value at the current timestep</param>
        /// <param name="prediction">The value predicted by the network at the current timestep</param>
        /// <param name="record"></param>
        /// <param name="result">An ModelResult class </param>
        /// <returns></returns>
        public abstract double? addInstance(double? groundTruth, object prediction, Map<string, object> record = null, ModelResult result = null);

        public abstract Map<string, object> getMetric();

        public const object SENTINEL_VALUE_FOR_MISSING_DATA = null;
    }

    /// <summary>
    /// Partial implementation of Metrics Interface for metrics that
    /// accumulate an error and compute an aggregate score, potentially
    /// over some window of previous data.This is a convenience class that
    /// can serve as the base class for a wide variety of metrics
    /// </summary>
    public abstract class AggregateMetric : MetricIFace
    {
        private string id;
        protected int verbosity;
        private int window;
        protected Deque<object> history;
        private double accumulatedError;
        protected double? aggregateError;
        protected int steps;
        protected MetricSpec spec;
        private bool disabled;
        protected int[] _predictionSteps;
        private Deque<double> _groundTruthHistory;
        private int? _maxRecords;
        protected List<MetricIFace> _subErrorMetrics;

        /// <summary>
        /// If the params contains the key 'errorMetric', then that is the name of
        /// another metric to which we will pass a modified groundTruth and prediction
        /// to from our addInstance() method.For example, we may compute a moving mean
        /// on the groundTruth and then pass that to the AbsoluteAveError metric
        /// </summary>
        /// <param name="metricSpec"></param>
        protected AggregateMetric(MetricSpec metricSpec)
            : base(metricSpec)
        {
            // Init default member variables
            this.id = null;
            this.verbosity = 0;
            this.window = -1;
            this.history = null;
            this.accumulatedError = 0;
            this.aggregateError = null;
            this.steps = 0;
            this.spec = metricSpec;
            this.disabled = false;

            // Number of steps ahead we are trying to predict. This is a list of
            // prediction steps are processing
            this._predictionSteps = new[] { 0 };

            // Where we store the ground truth history
            this._groundTruthHistory = new Deque<double>(-1);

            // The instances of another metric to which we will pass a possibly modified
            //  groundTruth and prediction to from addInstance(). There is one instance
            //  for each step present in this._predictionSteps
            this._subErrorMetrics = null;

            // The maximum number of records to process. After this many records have
            // been processed, the metric value never changes. This can be used
            // as the optimization metric for swarming, while having another metric without
            // the maxRecords limit to get an idea as to how well a production model
            // would do on the remaining data
            this._maxRecords = null;

            // Parse the metric's parameters
            if (metricSpec != null && metricSpec.Params != null)
            {
                this.id = (string)metricSpec.Params.Get("id", null);
                var predStepsFromParams = metricSpec.Params.Get("steps", new[] { 0 });
                // Make sure _predictionSteps is a list
                if (!(predStepsFromParams is int[]))
                {
                    this._predictionSteps = new[] { (int)predStepsFromParams };
                }
                else
                {
                    this._predictionSteps = (int[])predStepsFromParams;
                }

                this.verbosity = (int)metricSpec.Params.Get("verbosity", 0);
                this._maxRecords = (int?)metricSpec.Params.Get("maxRecords", null);

                // Get the metric window size
                if (metricSpec.Params.ContainsKey("window"))
                {
                    Debug.Assert(TypeConverter.Convert<int>(metricSpec.Params["window"]) >= 1);
                    this.window = TypeConverter.Convert<int>(metricSpec.Params["window"]);
                    this.history = new Deque<object>(-1);
                }

                // Get the name of the sub-metric to chain to from addInstance()
                if (metricSpec.Params.ContainsKey("errorMetric"))
                {
                    this._subErrorMetrics = new List<MetricIFace>();
                    foreach (var step in _predictionSteps)
                    {
                        var subSpec = (MetricSpec)metricSpec.Clone();
                        // Do all ground truth shifting before we pass onto the sub-metric
                        subSpec.Params.Remove("steps");
                        subSpec.Params.Remove("errorMetric");
                        subSpec.Metric = TypeConverter.Convert<string>(metricSpec.Params["errorMetric"]);
                        this._subErrorMetrics.Add(MetricSpec.GetModule(subSpec));
                    }

                }
            }
        }



        #region Overrides of MetricIFace

        public override double? addInstance(double? groundTruth, object prediction, Map<string, object> record = null, ModelResult result = null)
        {
            // This base class does not support time shifting the ground truth or a
            // subErrorMetric.
            Debug.Assert(this._predictionSteps.Length == 1);
            Debug.Assert(this._predictionSteps[0] == 0);
            Debug.Assert(this._subErrorMetrics == null);


            // If missing data,
            if (groundTruth == (double?)SENTINEL_VALUE_FOR_MISSING_DATA || prediction == null)
            {
                return this.aggregateError;
            }

            if (this.verbosity > 0)
            {
                Console.WriteLine("groundTruth:\n{0}\nPredictions:\n{1}\n{2}\n", groundTruth, prediction, this.getMetric());
            }

            // Ignore if we've reached maxRecords
            if (this._maxRecords != null && this.steps >= this._maxRecords)
            {
                return this.aggregateError;
            }

            // If there is a sub-metric, chain into it's addInstance
            // Accumulate the error
            this.accumulatedError = this.accumulate(groundTruth, prediction, this.accumulatedError, this.history, result);

            this.steps += 1;
            return this._compute();
        }

        private double? _compute()
        {
            this.aggregateError = aggregate(this.accumulatedError, this.history, this.steps);
            return aggregateError;
        }

        public override Map<string, object> getMetric()
        {
            return new Map<string, object>
            {
                {"value", this.aggregateError},
                {
                    "stats", new Map<string, object>
                    {
                        {"steps", this.steps}
                    }
                }
            };
        }

        #endregion

        public abstract double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer, ModelResult result);

        public abstract double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps);
    }

    /// <summary>
    /// computes root-mean-square error
    /// </summary>
    public class MetricRMSE : AggregateMetric
    {
        public MetricRMSE(MetricSpec metricSpec)
            : base(metricSpec)
        {

        }

        #region Overrides of AggregateMetric

        public override double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer,
            ModelResult result)
        {
            var error = Math.Pow(groundTruth.GetValueOrDefault() - TypeConverter.Convert<double?>(prediction).GetValueOrDefault(), 2);
            accumulatedError += error;

            if (historyBuffer != null)
            {
                historyBuffer.Append(error);
                if (historyBuffer.Length > (int)this.spec.Params["window"])
                {
                    accumulatedError -= (double)historyBuffer.TakeFirst();
                }
            }

            return accumulatedError;
        }

        public override double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps)
        {
            var n = steps;
            if (historyBuffer != null)
            {
                n = historyBuffer.Length;
            }
            return Math.Sqrt(accumulatedError / n);
        }

        #endregion
    }

    public class MetricAAE : AggregateMetric
    {
        public MetricAAE(MetricSpec metricSpec)
            : base(metricSpec)
        {

        }

        #region Overrides of AggregateMetric

        public override double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer,
            ModelResult result)
        {
            double error = Math.Abs(groundTruth.GetValueOrDefault() - TypeConverter.Convert<double?>(prediction).GetValueOrDefault());
            accumulatedError += error;

            if (historyBuffer != null)
            {
                historyBuffer.Append(error);
                if (historyBuffer.Length > TypeConverter.Convert<int>(spec.Params["window"]))
                {
                    accumulatedError -= (double)historyBuffer.TakeFirst();
                }
            }
            return accumulatedError;
        }

        public override double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps)
        {
            int n = steps;
            if (historyBuffer != null)
            {
                n = historyBuffer.Length;
            }
            return accumulatedError / (double)n;
        }

        #endregion
    }

    /// <summary>
    /// This is an "uber" metric which is used to apply one of the other basic
    /// metrics to a specific step in a multi-step prediction.
    /// 
    /// The specParams are expected to contain:
    ///     'errorMetric': name of basic metric to apply
    ///     'steps':       compare prediction['steps'] to the current ground truth.
    /// 
    /// Note that the metrics manager has already performed the time shifting
    /// for us - it passes us the prediction element from 'steps' steps ago
    /// and asks us to compare that to the current ground truth.
    /// 
    /// When multiple steps of prediction are requested, we average the results of
    /// the underlying metric for each step.
    /// </summary>
    public class MetricMultiStep : AggregateMetric
    {
        public MetricMultiStep(MetricSpec metricSpec)
            : base(metricSpec)
        {
            Debug.Assert(this._subErrorMetrics != null);
        }

        #region Overrides of AggregateMetric


        public override Map<string, object> getMetric()
        {
            return new Map<string, object>
            {
                {"value", aggregateError},
                {"stats", new Map<string, object> {{"steps", steps}}}
            };
        }

        public override double? addInstance(double? groundTruth, object prediction, Map<string, object> record = null, ModelResult result = null)
        {
            // if missing data
            if (groundTruth == (double?)SENTINEL_VALUE_FOR_MISSING_DATA)
                return aggregateError;

            // Get the prediction for this time step
            double aggErrSum = 0.0;
            try
            {
                foreach (var item in ArrayUtils.Zip(_predictionSteps, _subErrorMetrics))
                {
                    int step = (int)item.Get(0);
                    MetricIFace subErrorMetric = (MetricIFace)item.Get(1);

                    var stepPrediction = ((IDictionary)prediction)[step];
                    // Unless this is a custom_error_metric, when we have a dict of 
                    // probabilities, get the most probable one. For custom error metrics, 
                    // we pass the probabilities in so that it can decide how best to deal with
                    // them.
                    if (stepPrediction is IDictionary && !(subErrorMetric is CustomErrorMetric))
                    {
                        stepPrediction = ((IDictionary<object, object>)stepPrediction)
                            .OrderByDescending(p => p.Value)
                            .Select(p => p.Key)
                            .First();

                        //object[] predictions = ((IDictionary)stepPrediction).Values.Cast<object>().ToArray();
                        //Array.Sort(predictions);
                        //stepPrediction = predictions[1];
                    }

                    // Get sum of the errors
                    double? aggErr = subErrorMetric.addInstance(groundTruth, stepPrediction, record, result);
                    if (verbosity >= 2)
                    {
                        Console.WriteLine($"MetricMultiStep {Arrays.ToString(_predictionSteps)} aggErr for stepSize {step}: {aggErr}");
                    }
                    aggErrSum += aggErr.GetValueOrDefault();
                }

            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                throw;
            }

            // Return average aggregate error across all step sizes
            this.aggregateError = aggErrSum / _subErrorMetrics.Count;

            steps += 1;

            return this.aggregateError;
        }

        public override double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer,
            ModelResult result)
        {
            throw new NotImplementedException();
        }

        public override double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    /// <summary>
    /// Simply the inverse of the Accuracy metric
    /// More consistent with scalar metrics because
    /// they all report an error to be minimized
    /// </summary>
    public class MetricAveError : AggregateMetric
    {
        public MetricAveError(MetricSpec metricSpec)
            : base(metricSpec)
        {

        }

        #region Overrides of AggregateMetric

        public override double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps)
        {
            int n = steps;
            if (historyBuffer != null)
            {
                n = historyBuffer.Length;
            }
            return accumulatedError / (double)n;
        }

        public override double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer,
            ModelResult result)
        {
            double error = groundTruth != TypeConverter.Convert<double?>(prediction).GetValueOrDefault() ? 1.0 : 0.0;
            accumulatedError += error;

            if (historyBuffer != null)
            {
                historyBuffer.Append(error);
                if (historyBuffer.Length > TypeConverter.Convert<int>(spec.Params["window"]))
                {
                    accumulatedError -= (double)historyBuffer.TakeFirst();
                }
            }

            return accumulatedError;
        }

        #endregion
    }

    /// <summary>
    /// Computes the "Alternative" Mean Absolute Percent Error.
    /// 
    /// A generic MAPE computes the percent error for each sample, and then gets
    /// an average.This can suffer from samples where the actual value is very small
    /// or zero - this one sample can drastically alter the mean.
    /// 
    /// This metric on the other hand first computes the average of the actual values
    /// and the averages of the errors before dividing. This washes out the effects of
    /// a small number of samples with very small actual values.
    /// </summary>
    public class MetricAltMAPE : AggregateMetric
    {
        private double? _accumulatedGroundTruth;
        private double _accumulatedError;

        public MetricAltMAPE(MetricSpec metricSpec) : base(metricSpec)
        {
            _accumulatedGroundTruth = 0;
            _accumulatedError = 0;
        }

        #region Overrides of AggregateMetric

        public override double? addInstance(double? groundTruth, object prediction, Map<string, object> record = null, ModelResult result = null)
        {
            // If missing data
            if(!groundTruth .HasValue)
                return aggregateError;

            var error = Math.Abs(groundTruth.Value - TypeConverter.Convert<double?>(prediction).GetValueOrDefault());
            if (verbosity > 0)
            {
                Debug.WriteLine($"MetricAltMAPE:");
                Debug.WriteLine($"\tgroundTruth: {groundTruth}");
                Debug.WriteLine($"\tPrediction: {prediction}");
                Debug.WriteLine($"\tError: {error}");
            }

            // Update the accumulated groundTruth and aggregate error
            if (history != null)
            {
                history.Append(new Tuple(groundTruth, error));
                if (history.Length > TypeConverter.Convert<int>(spec.Params["window"]))
                {
                    Tuple tLeft = (Tuple) history.TakeFirst();
                    _accumulatedGroundTruth -= (double?) tLeft.Item1;
                    _accumulatedError -= (double) tLeft.Item2;
                }
            }
            _accumulatedGroundTruth += Math.Abs(groundTruth.GetValueOrDefault());
            _accumulatedError += error;

            // Compute aggregate pct error
            if (_accumulatedGroundTruth > 0)
            {
                aggregateError = 100.0*_accumulatedError/_accumulatedGroundTruth;
            }
            else
            {
                aggregateError = 0;
            }

            if (verbosity > 1)
            {
                Debug.WriteLine($" accumGT: {_accumulatedGroundTruth}");
                Debug.WriteLine($" accumError: {_accumulatedError}");
                Debug.WriteLine($" aggregateError: {aggregateError}");
            }
            steps += 1;
            return aggregateError;
        }

        public override double accumulate(double? groundTruth, object prediction, double accumulatedError, Deque<object> historyBuffer,
            ModelResult result)
        {
            throw new NotImplementedException();
        }

        public override double? aggregate(double accumulatedError, Deque<object> historyBuffer, int steps)
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    /// <summary>
    /// Custom Error Metric class that handles user defined error metrics
    /// </summary>
    public class CustomErrorMetric : MetricIFace
    {
        public CustomErrorMetric(MetricSpec metricSpec)
            : base(metricSpec)
        {
        }

        #region Overrides of MetricIFace

        public override double? addInstance(double? groundTruth, object prediction, Map<string, object> record = null, ModelResult result = null)
        {
            throw new NotImplementedException();
        }

        public override Map<string, object> getMetric()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    public enum InferenceElement
    {
        Prediction,
        Classification,
        ClassConfidences,
        Encodings,
        AnomalyLabel,
        AnomalyScore,
        MultiStepPredictions,
        MultiStepBestPredictions,
        MultiStepBucketLikelihoods
    }

    public static class InferenceElementHelper
    {
        private static Map<InferenceElement, string> _inferenceMap = new Map<InferenceElement, string>
        {
            { InferenceElement.Prediction, "dataRow" },
            { InferenceElement.Encodings, "dataEncodings" },
            { InferenceElement.Classification, "category" },
            { InferenceElement.ClassConfidences, "category" },
            { InferenceElement.MultiStepPredictions, "dataDict" },
            { InferenceElement.MultiStepBestPredictions, "dataDict" },
        };
        /// <summary>
        /// Returns the maximum delay for the InferenceElements in the inference dictionary
        /// </summary>
        /// <param name="inferences"></param>
        /// <returns></returns>
        public static int GetMaxDelay(Dictionary<InferenceElement, object> inferences)
        {
            int maxDelay = 0;
            foreach (KeyValuePair<InferenceElement, object> pair in inferences)
            {
                if (pair.Value is IDictionary)
                {
                    foreach (var key in ((IDictionary)pair.Value).Keys)
                    {
                        maxDelay = Math.Max(GetTemporalDelay(pair.Key, key), maxDelay);
                    }
                }
                else
                {
                    maxDelay = Math.Max(GetTemporalDelay(pair.Key), maxDelay);
                }
            }
            return maxDelay;
        }

        /// <summary>
        /// Returns the number of records that elapse between when an inference is
        /// made and when the corresponding input record will appear.For example, a
        /// multistep prediction for 3 timesteps out will have a delay of 3
        /// </summary>
        /// <param name="inferenceElement"></param>
        /// <param name="key">If the inference is a dictionary type, this specifies key for the sub-inference that is being delayed</param>
        /// <returns></returns>
        public static int GetTemporalDelay(InferenceElement inferenceElement, object key = null)
        {
            // -----------------------------------------------------------------------
            // For next step prediction, we shift by 1
            if (new[] { InferenceElement.Prediction, InferenceElement.Encodings }.Contains(inferenceElement))
            {
                return 1;
            }
            // -----------------------------------------------------------------------
            // For classification, anomaly scores, the inferences immediately succeed the
            // inputs
            if (new[] {InferenceElement.AnomalyScore,
                            InferenceElement.AnomalyLabel,
                            InferenceElement.Classification,
                            InferenceElement.ClassConfidences}.Contains(inferenceElement))
            {
                return 0;
            }
            // -----------------------------------------------------------------------
            // For multistep prediction, the delay is based on the key in the inference
            // dictionary
            if (new[] { InferenceElement.MultiStepPredictions, InferenceElement.MultiStepBestPredictions,
                        InferenceElement.MultiStepBucketLikelihoods }.Contains(inferenceElement))
            {
                return (int)key;
            }

            // -----------------------------------------------------------------------
            // default: return 0
            return 0;
        }

        /// <summary>
        /// Get the sensor input element that corresponds to the given inference
        /// element. This is mainly used for metrics and prediction logging
        /// </summary>
        /// <param name="inferenceElement"></param>
        /// <returns></returns>
        public static string GetInputElement(InferenceElement inferenceElement)
        {
            return _inferenceMap.Get(inferenceElement, null);
        }
    }
}