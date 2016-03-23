using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Algorithms
{
    /**
 * <p>The anomaly likelihood computer.</p>
 * 
 * <p>From {@link Anomaly}:</p>
 * 
 * This module analyzes and estimates the distribution of averaged anomaly scores
 * from a CLA model. Given a new anomaly score `s`, estimates `P(score >= s)`.
 * 
 * The number `P(score >= s)` represents the likelihood of the current state of
 * predictability. For example, a likelihood of 0.01 or 1% means we see this much
 * predictability about one out of every 100 records. The number is not as unusual
 * as it seems. For records that arrive every minute, this means once every hour
 * and 40 minutes. A likelihood of 0.0001 or 0.01% means we see it once out of
 * 10,000 records, or about once every 7 days.
 * 
 * <p>
 * <em>
 * USAGE FOR LOW-LEVEL FUNCTIONS
 * -----------------------------
 * </em>
 * <pre>
 * There are two primary interface routines:
 *
 * estimateAnomalyLikelihoods: batch routine, called initially and once in a
 *                                while
 * updateAnomalyLikelihoods: online routine, called for every new data point
 *
 * 1. Initially::
 *
 *    {@link AnomalyLikelihoodMetrics} { likelihoods, avgRecordList, estimatorParams } = 
 *        {@link #estimateAnomalyLikelihoods(List, int, int)}
 *
 * 2. Whenever you get new data::
 *
 *    {@link AnomalyLikelihoodMetrics} { likelihoods, avgRecordList, estimatorParams } = 
 *       {@link #updateAnomalyLikelihoods(List, NamedTuple)}
 *
 * 3. And again (make sure you use the new estimatorParams (a.k.a {@link NamedTuple}) returned 
 *    in the above {@link AnomalyLikelihoodMetrics} call to updateAnomalyLikelihoods!)
 *
 *    {@link AnomalyLikelihoodMetrics} { likelihoods, avgRecordList, estimatorParams } = 
 *       {@link #updateAnomalyLikelihoods(List, NamedTuple)}
 *
 * 4. Every once in a while update estimator with a lot of recent data
 *
 *    {@link AnomalyLikelihoodMetrics} { likelihoods, avgRecordList, estimatorParams } = 
 *        {@link #estimateAnomalyLikelihoods(List, int, int)}
 * </pre>
 * </p>
 * 
 * @author Numenta
 * @see Anomaly
 * @see AnomalyLikelihoodMetrics
 * @see Sample
 * @see AnomalyParams
 * @see Statistic
 * @see MovingAverage
 */
    public class AnomalyLikelihood : Anomaly
    {
        private static readonly ILog LOG = LogManager.GetLogger(typeof(AnomalyLikelihood));

        private int claLearningPeriod = 300;
        private int estimationSamples = 300;
        private int probationaryPeriod;
        private int iteration;
        private int reestimationPeriod;

        private bool isWeighted;

        private List<Sample> historicalScores = new List<Sample>();
        private AnomalyParams distribution;

        public AnomalyLikelihood(bool useMovingAvg, int windowSize, bool isWeighted, int claLearningPeriod, int estimationSamples)
                : base(useMovingAvg, windowSize)
        {
            this.isWeighted = isWeighted;
            this.claLearningPeriod = claLearningPeriod == VALUE_NONE ? this.claLearningPeriod : claLearningPeriod;
            this.estimationSamples = estimationSamples == VALUE_NONE ? this.estimationSamples : estimationSamples;
            this.probationaryPeriod = claLearningPeriod + estimationSamples;
            // How often we re-estimate the Gaussian distribution. The ideal is to
            // re-estimate every iteration but this is a performance hit. In general the
            // system is not very sensitive to this number as long as it is small
            // relative to the total number of records processed.
            this.reestimationPeriod = 100;
        }

        /**
         * Compute a log scale representation of the likelihood value. Since the
         * likelihood computations return low probabilities that often go into four 9's
         * or five 9's, a log value is more useful for visualization, thresholding,
         * etc.
         * 
         * @param likelihood
         * @return
         */
        public static double ComputeLogLikelihood(double likelihood)
        {
            return Math.Log(1.0000000001 - likelihood) / -23.02585084720009;
        }

        /**
         * Return the probability that the current value plus anomaly score represents
         * an anomaly given the historical distribution of anomaly scores. The closer
         * the number is to 1, the higher the chance it is an anomaly.
         *
         * Given the current metric value, plus the current anomaly score, output the
         * anomalyLikelihood for this record.
         * 
         * @param value             input value
         * @param anomalyScore      current anomaly score
         * @param timestamp         (optional) timestamp
         * @return  Given the current metric value, plus the current anomaly score, output the
         * anomalyLikelihood for this record.
         */
        public double AnomalyProbability(double value, double anomalyScore, DateTime timestamp)
        {
            if (timestamp == null)
            {
                timestamp = new DateTime();
            }

            Sample dataPoint = new Sample(timestamp, value, anomalyScore);
            double likelihoodRetval;
            if (historicalScores.Count < probationaryPeriod)
            {
                likelihoodRetval = 0.5;
            }
            else {
                if (distribution == null || iteration % reestimationPeriod == 0)
                {
                    this.distribution = EstimateAnomalyLikelihoods(
                        historicalScores, 10, claLearningPeriod).GetParams();
                }
                AnomalyLikelihoodMetrics metrics = UpdateAnomalyLikelihoods(new List<Sample> { dataPoint }, this.distribution);
                this.distribution = metrics.GetParams();
                likelihoodRetval = 1.0 - metrics.GetLikelihoods()[0];
            }
            historicalScores.Add(dataPoint);
            this.iteration += 1;

            return likelihoodRetval;
        }

        /**
         * Given a series of anomaly scores, compute the likelihood for each score. This
         * function should be called once on a bunch of historical anomaly scores for an
         * initial estimate of the distribution. It should be called again every so often
         * (say every 50 records) to update the estimate.
         * 
         * @param anomalyScores
         * @param averagingWindow
         * @param skipRecords
         * @return
         */
        public AnomalyLikelihoodMetrics EstimateAnomalyLikelihoods(List<Sample> anomalyScores, int averagingWindow, int skipRecords)
        {
            if (anomalyScores.Count == 0)
            {
                throw new ArgumentException("Must have at least one anomaly score.");
            }

            // Compute averaged anomaly scores
            AveragedAnomalyRecordList records = AnomalyScoreMovingAverage(anomalyScores, averagingWindow);

            // Estimate the distribution of anomaly scores based on aggregated records
            Statistic distribution;
            if (records.averagedRecords.Count <= skipRecords)
            {
                distribution = NullDistribution();
            }
            else {
                List<double> samples = records.GetMetrics();
                distribution = EstimateNormal(samples.Skip(skipRecords).Take(samples.Count).ToArray(), true);

                /*  Taken from the Python Documentation

                 # HACK ALERT! The CLA model currently does not handle constant metric values
                 # very well (time of day encoder changes sometimes lead to unstable SDR's
                 # even though the metric is constant). Until this is resolved, we explicitly
                 # detect and handle completely flat metric values by reporting them as not
                 # anomalous.

                 */
                samples = records.GetSamples();
                Statistic metricDistribution = EstimateNormal(samples.Skip(skipRecords).Take(samples.Count).ToArray(), false);

                if (metricDistribution.variance < 1.5e-5)
                {
                    distribution = NullDistribution();
                }
            }

            // Estimate likelihoods based on this distribution
            int i = 0;
            double[] likelihoods = new double[records.averagedRecords.Count];
            foreach (Sample sample in records.averagedRecords)
            {
                likelihoods[i++] = NormalProbability(sample.score, distribution);
            }

            // Filter likelihood values
            double[] filteredLikelihoods = FilterLikelihoods(likelihoods);

            int len = likelihoods.Length;

            Parameters anomalyParameters = Parameters.Empty();
            anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_DIST, distribution);
            anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MVG_AVG, new MovingAverage(records.historicalValues, records.total, averagingWindow));
            anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE, len > 0 ? Arrays.CopyOfRange(likelihoods, len - Math.Min(averagingWindow, len), len) : new double[0]);

            AnomalyParams @params = new AnomalyParams(anomalyParameters);

            //AnomalyParams @params = new AnomalyParams(
            //    new string[] { "distribution", "movingAverage", "historicalLikelihoods" },
            //        distribution,
            //        new MovingAverage(records.historicalValues, records.total, averagingWindow),
            //        len > 0 ?
            //            Arrays.CopyOfRange(likelihoods, len - Math.Min(averagingWindow, len), len) :
            //                new double[0]);

            if (LOG.IsDebugEnabled)
            {
                LOG.Debug(string.Format("Discovered params={0} Number of likelihoods:{1}  First 20 likelihoods:{2}",
                        @params, len, Arrays.CopyOfRange(filteredLikelihoods, 0, 20)));
            }

            return new AnomalyLikelihoodMetrics(filteredLikelihoods, records, @params);
        }

        /**
         * Compute updated probabilities for anomalyScores using the given params.
         * 
         * @param anomalyScores     a list of records. Each record is a list with a {@link Sample} containing the
                                    following three elements: [timestamp, value, score]
         * @param params            Associative {@link NamedTuple} returned by the {@link AnomalyLikelihoodMetrics} from
         *                          {@link #estimateAnomalyLikelihoods(List, int, int)}
         * @return
         */
        public AnomalyLikelihoodMetrics UpdateAnomalyLikelihoods(List<Sample> anomalyScores, AnomalyParams @params)
        {
            int anomalySize = anomalyScores.Count;

            if (LOG.IsDebugEnabled)
            {
                LOG.Debug("in updateAnomalyLikelihoods");
                LOG.Debug(string.Format("Number of anomaly scores: {0}", anomalySize));
                LOG.Debug(string.Format("First 20: {0}", anomalyScores.SubList(0, Math.Min(20, anomalySize))));
                LOG.Debug(string.Format("Params: {0}", @params));
            }

            if (anomalyScores.Count == 0)
            {
                throw new ArgumentException("Must have at least one anomaly score.");
            }

            if (!IsValidEstimatorParams(@params))
            {
                throw new ArgumentException("\"params\" is not a valid parameter structure");
            }




            double[] histLikelihoods;
            if ((histLikelihoods = @params.HistoricalLikelihoods()) == null || histLikelihoods.Length == 0)
            {
                Parameters anomalyParameters = Parameters.Empty();
                anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_DIST, @params.Distribution());
                anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MVG_AVG, @params.MovingAverage());
                anomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE, histLikelihoods = new double[] { 1 });

                @params = new AnomalyParams(anomalyParameters);

                //@params = new NamedTuple(
                //    new string[] { "distribution", "movingAverage", "historicalLikelihoods" },
                //        @params.Distribution(),
                //        @params.MovingAverage(),
                //        histLikelihoods = new double[] { 1 });
            }

            // Compute moving averages of these new scores using the previous values
            // as well as likelihood for these scores using the old estimator
            MovingAverage mvgAvg = (MovingAverage)@params.MovingAverage();
            List<double> historicalValues = mvgAvg.GetSlidingWindow();
            double total = mvgAvg.GetTotal();
            int windowSize = mvgAvg.GetWindowSize();

            List<Sample> aggRecordList = new List<Sample>(anomalySize);
            double[] likelihoods = new double[anomalySize];
            int i = 0;
            foreach (Sample sample in anomalyScores)
            {
                MovingAverage.Calculation calc = MovingAverage.Compute(historicalValues, total, sample.score, windowSize);
                aggRecordList.Add(
                    new Sample(
                        sample.date,
                        sample.value,
                        calc.GetAverage()));
                total = calc.GetTotal();
                likelihoods[i++] = NormalProbability(calc.GetAverage(), (Statistic)@params.Distribution());
            }

            // Filter the likelihood values. First we prepend the historical likelihoods
            // to the current set. Then we filter the values.  We peel off the likelihoods
            // to return and the last windowSize values to store for later.
            double[] likelihoods2 = ArrayUtils.Concat(histLikelihoods, likelihoods);
            double[] filteredLikelihoods = FilterLikelihoods(likelihoods2);
            likelihoods = Arrays.CopyOfRange(filteredLikelihoods, filteredLikelihoods.Length - likelihoods.Length, filteredLikelihoods.Length);
            double[] historicalLikelihoods = Arrays.CopyOf(likelihoods2, likelihoods2.Length - Math.Min(windowSize, likelihoods2.Length));

            // Update the estimator
            Parameters newAnomalyParameters = Parameters.Empty();
            newAnomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_DIST, @params.Distribution());
            newAnomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MVG_AVG, new MovingAverage(historicalValues, total, windowSize));
            newAnomalyParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE, historicalLikelihoods);

            AnomalyParams newParams = new AnomalyParams(newAnomalyParameters);

            //AnomalyParams newParams = new AnomalyParams(
            //    new string[] { "distribution", "movingAverage", "historicalLikelihoods" },
            //        @params.Distribution(),
            //        new MovingAverage(historicalValues, total, windowSize),
            //        historicalLikelihoods);

            return new AnomalyLikelihoodMetrics(
                likelihoods,
                new AveragedAnomalyRecordList(aggRecordList, historicalValues, total),
                newParams);
        }

        /**
         * Filter the list of raw (pre-filtered) likelihoods so that we only preserve
         * sharp increases in likelihood. 'likelihoods' can be a numpy array of floats or
         * a list of floats.
         * 
         * @param likelihoods
         * @return
         */
        public double[] FilterLikelihoods(double[] likelihoods)
        {
            return FilterLikelihoods(likelihoods, 0.99999, 0.999);
        }

        /**
         * Filter the list of raw (pre-filtered) likelihoods so that we only preserve
         * sharp increases in likelihood. 'likelihoods' can be an array of floats or
         * a list of floats.
         * 
         * @param likelihoods
         * @param redThreshold
         * @param yellowThreshold
         * @return
         */
        public double[] FilterLikelihoods(double[] likelihoods, double redThreshold, double yellowThreshold)
        {
            redThreshold = 1.0 - redThreshold;
            yellowThreshold = 1.0 - yellowThreshold;

            // The first value is untouched
            double[] filteredLikelihoods = new double[likelihoods.Length];
            filteredLikelihoods[0] = likelihoods[0];

            for (int i = 0; i < likelihoods.Length - 1; i++)
            {
                double v = likelihoods[i + 1];
                if (v <= redThreshold)
                {
                    // If value is in redzone
                    if (likelihoods[i] > redThreshold)
                    {
                        // Previous value is not in redzone, so leave as-is
                        filteredLikelihoods[i + 1] = v;
                    }
                    else {
                        filteredLikelihoods[i + 1] = yellowThreshold;
                    }
                }
                else {
                    // Value is below the redzone, so leave as-is
                    filteredLikelihoods[i + 1] = v;
                }
            }

            return filteredLikelihoods;
        }

        /**
         * Given a list of anomaly scores return a list of averaged records.
         * anomalyScores is assumed to be a list of records of the form:
         * <pre>
         *      Sample:
         *           dt = Tuple(2013, 8, 10, 23, 0) --> Date Fields
         *           sample = (double) 6.0
         *           metric(avg) = (double) 1.0
         * </pre>
         *           
         * @param anomalyScores     List of {@link Sample} objects (described contents above)
         * @param windowSize        Count of historical items over which to compute the average
         * 
         * @return Each record in the returned list contains [datetime field, value, averaged score]
         */
        public AveragedAnomalyRecordList AnomalyScoreMovingAverage(List<Sample> anomalyScores, int windowSize)
        {
            List<double> historicalValues = new List<double>();
            double total = 0.0;
            List<Sample> averagedRecordList = new List<Sample>();
            foreach (Sample record in anomalyScores)
            {
                ////////////////////////////////////////////////////////////////////////////////////////////
                // Python version has check for malformed records here, but can't happen in java version. //
                ////////////////////////////////////////////////////////////////////////////////////////////

                MovingAverage.Calculation calc = MovingAverage.Compute(historicalValues, total, record.score, windowSize);

                Sample avgRecord = new Sample(
                    record.date,
                    record.value,
                    calc.GetAverage());
                averagedRecordList.Add(avgRecord);
                total = calc.GetTotal();

                if (LOG.IsDebugEnabled)
                {
                    LOG.Debug(string.Format("Aggregating input record: {0}, Result: {1}", record, averagedRecordList[averagedRecordList.Count - 1]));
                }
            }

            return new AveragedAnomalyRecordList(averagedRecordList, historicalValues, total);
        }

        /**
         * A Map containing the parameters of a normal distribution based on
         * the sampleData.
         * 
         * @param sampleData
         * @param performLowerBoundCheck
         * @return
         */
        public Statistic EstimateNormal(double[] sampleData, bool performLowerBoundCheck)
        {
            double d = ArrayUtils.Average(sampleData);
            double v = ArrayUtils.Variance(sampleData, d);

            if (performLowerBoundCheck)
            {
                if (d < 0.03)
                {
                    d = 0.03;
                }
                if (v < 0.0003)
                {
                    v = 0.0003;
                }
            }

            // Compute standard deviation
            double s = v > 0 ? Math.Sqrt(v) : 0.0;

            return new Statistic(d, v, s);
        }

        /**
         * Returns a distribution that is very broad and makes every anomaly
         * score between 0 and 1 pretty likely
         * 
         * @return
         */
        public Statistic NullDistribution()
        {
            if (LOG.IsDebugEnabled)
            {
                LOG.Debug("Returning nullDistribution");
            }
            return new Statistic(0.5, 1e6, 1e3);
        }

        /**
         * Given the normal distribution specified in distributionParams, return
         * the probability of getting samples > x
         * This is essentially the Q-function
         * 
         * @param x
         * @param named
         * @return
         */
        public double NormalProbability(double x, Parameters parameters)
        {
            return NormalProbability(x, new Statistic((double)parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_MEAN),
                (double)parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_VARIANCE),
                (double)parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_STDEV)));
        }

        /**
         * Given the normal distribution specified in distributionParams, return
         * the probability of getting samples > x
         * This is essentially the Q-function
         * 
         * @param x
         * @param named
         * @return
         */
        public double NormalProbability(double x, Statistic s)
        {
            // Distribution is symmetrical around mean
            if (x < s.mean)
            {
                double xp = 2 * s.mean - x;
                return 1.0 - NormalProbability(xp, s);
            }

            // How many standard deviations above the mean are we - scaled by 10X for table
            double xs = 10 * (x - s.mean) / s.stdev;

            xs = Math.Round(xs);
            if (xs > 70)
            {
                return 0.0;
            }
            return Q[(int)xs];
        }

        /**
         * {@inheritDoc}
         */
        public override double Compute(int[] activeColumns, int[] predictedColumns, double inputValue, long timestamp)
        {
            if (inputValue == 0)
            {
                throw new ArgumentException("Selected anomaly mode Mode.LIKELIHOOD requires an \"inputValue\" to " +
                    "the compute() method.");
            }

            DateTime time = timestamp > 0 ? new DateTime(timestamp) : new DateTime();
            // First compute raw anomaly score
            double retVal = ComputeRawAnomalyScore(activeColumns, predictedColumns);

            // low likelihood -> high anomaly
            double probability = AnomalyProbability(inputValue, retVal, time);

            // Apply weighting if configured
            retVal = isWeighted ? retVal * (1 - probability) : 1 - probability;

            // Last, do moving-average if windowSize was specified
            if (useMovingAverage)
            {
                retVal = movingAverage.Next(retVal);
            }

            return retVal;
        }

        /**
         * Returns a flag indicating whether the specified params are valid.
         * true if so, false if not
         * 
         * @param params    a {@link NamedTuple} containing { distribution, movingAverage, historicalLikelihoods }
         * @return
         */
        public bool IsValidEstimatorParams(AnomalyParams @params)
        {
            if (@params.Distribution() == null || @params.MovingAverage() == null)
            {
                return false;
            }

            Statistic stat = @params.Distribution();
            if (stat.mean == 0 || stat.variance == 0 || stat.stdev == 0)
            {
                return false;
            }
            return true;
        }



        /////////////////////////////////////////////////////////////////////////////
        //                     AnomalyParams Class Definition                      //
        /////////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///  <p>
        ///  Extends the dictionary key lookup functionality of the parent class to add
        ///  definite typing for parameter retrieval.Also handles output formatting of
        ///  the contents to a serializable JSON format.
        ///  </p>
        ///  <p>
        ///  <pre>
        ///  {
        ///   *"distribution":               # describes the distribution is-a {@link Statistic}
        ///       {
        ///       *"name": STRING,           # name of the distribution, such as 'normal'
        /// "mean": SCALAR,           # mean of the distribution
        /// "variance": SCALAR,       # variance of the distribution
        /// 
        ///         # There may also be some keys that are specific to the distribution
        ///       },
        /// 
        /// "historicalLikelihoods": []   # Contains the last windowSize likelihood
        ///                                   # values returned
        /// 
        /// "movingAverage":              # stuff needed to compute a rolling average is-a {@link MovingAverage}
        ///                                   # of the anomaly scores
        ///       {
        ///         "windowSize": SCALAR,     # the size of the averaging window
        ///         "historicalValues": [],   # list with the last windowSize anomaly
        ///                                   # scores
        ///         "total": SCALAR,          # the total of the values in historicalValues
        ///       },
        /// 
        /// 
        /// 
        ///   </pre>
        ///   </p>
        /// 
        ///  @author David Ray
        ///
        /// </summary>
        public class AnomalyParams
        {
            private readonly Parameters _parameters;
            /** Cached Json formatting. Possible because Objects of this class is immutable */
            private JObject cachedNode;

            private readonly Statistic distribution;
            private readonly MovingAverage movingAverage;
            private readonly double[] historicalLikelihoods;
            private readonly int windowSize;

            /// <summary>
            /// Constructs a new <see cref="AnomalyParams"/>
            /// </summary>
            /// <param name="parameters"></param>
            public AnomalyParams(Parameters parameters)
            {
                if (parameters.Size() != 3)
                {
                    throw new ArgumentException("AnomalyParams must have \"distribution\", \"movingAverage\", and \"historicalLikelihoods\"" +
                        " parameters. keys.Length != 3 or values.Length != 3");
                }
                _parameters = parameters;

                distribution = (Statistic)parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_DIST);
                movingAverage = (MovingAverage)parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_MVG_AVG);
                historicalLikelihoods = (double[])parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE);
                windowSize = movingAverage.GetWindowSize();
            }

            /**
             * Returns the {@link Statistic} containing point calculations.
             * @return
             */
            public Statistic Distribution()
            {
                return distribution;
            }

            /**
             * Returns the {@link MovingAverage} object
             * @return
             */
            public MovingAverage MovingAverage()
            {
                return movingAverage;
            }

            /**
             * Returns the array of computed likelihoods
             * @return
             */
            public double[] HistoricalLikelihoods()
            {
                return historicalLikelihoods;
            }

            /**
             * Returns the window size of the moving average.
             * @return
             */
            public int WindowSize()
            {
                return windowSize;
            }

            public Parameters GetParameters() { return _parameters; }

            /**
             * Lazily creates and returns a JSON ObjectNode containing this {@code AnomalyParams}' data.
             * 
             * @param factory
             * @return
             */
            public JObject ToJsonNode()
            {
                if (cachedNode == null)
                {
                    JObject distribution = new JObject();
                    distribution.Add(Parameters.KEY.ANOMALY_KEY_MEAN.GetFieldName(), this.distribution.mean);
                    distribution.Add(Parameters.KEY.ANOMALY_KEY_VARIANCE.GetFieldName(), this.distribution.variance);
                    distribution.Add(Parameters.KEY.ANOMALY_KEY_STDEV.GetFieldName(), this.distribution.stdev);

                    double[] historicalLikelihoods = (double[])_parameters.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_HIST_LIKE);
                    JArray historics = new JArray(historicalLikelihoods);

                    JObject mvgAvg = new JObject();
                    mvgAvg.Add(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE.GetFieldName(), windowSize);

                    List<double> hVals = this.movingAverage.GetSlidingWindow();
                    JArray histVals = new JArray(hVals);

                    mvgAvg.Add(Parameters.KEY.ANOMALY_KEY_HIST_VALUES.GetFieldName(), histVals);
                    mvgAvg.Add(Parameters.KEY.ANOMALY_KEY_TOTAL.GetFieldName(), this.movingAverage.GetTotal());

                    cachedNode = new JObject();
                    cachedNode.Add(Parameters.KEY.ANOMALY_KEY_DIST.GetFieldName(), distribution);
                    cachedNode.Add(Parameters.KEY.ANOMALY_KEY_HIST_LIKE.GetFieldName(), historics);
                    cachedNode.Add(Parameters.KEY.ANOMALY_KEY_MVG_AVG.GetFieldName(), mvgAvg);
                }

                return cachedNode;
            }

            /**
             * Returns the processed Json Node with possible pretty print indentation
             * formatting if the flag specified is true.
             * 
             * @param doPrettyPrint
             * @return
             */
            public string ToJson(bool doPrettyPrint)
            {
                try
                {
                    string result = JsonConvert.SerializeObject(ToJsonNode(), doPrettyPrint ? Formatting.Indented : Formatting.None);
                    return result;
                }
                catch (JsonException e)
                {
                    LOG.Error("Error while writing json", e);
                }
                catch (IOException e)
                {
                    LOG.Error("Error while writing json", e);
                }

                return "Fault in serializing";
            }

            /**
             * Returns the processed Json Node as a String
             * @return
             */
            public string ToJson()
            {
                return ToJson(false);
            }
        }

        // Table lookup for Q function, from wikipedia
        // http://en.wikipedia.org/wiki/Q-function
        private static double[] Q { get; set; }

        static AnomalyLikelihood()
        {
            Q = new double[71];
            Q[0] = 0.500000000;
            Q[1] = 0.460172163;
            Q[2] = 0.420740291;
            Q[3] = 0.382088578;
            Q[4] = 0.344578258;
            Q[5] = 0.308537539;
            Q[6] = 0.274253118;
            Q[7] = 0.241963652;
            Q[8] = 0.211855399;
            Q[9] = 0.184060125;
            Q[10] = 0.158655254;
            Q[11] = 0.135666061;
            Q[12] = 0.115069670;
            Q[13] = 0.096800485;
            Q[14] = 0.080756659;
            Q[15] = 0.066807201;
            Q[16] = 0.054799292;
            Q[17] = 0.044565463;
            Q[18] = 0.035930319;
            Q[19] = 0.028716560;
            Q[20] = 0.022750132;
            Q[21] = 0.017864421;
            Q[22] = 0.013903448;
            Q[23] = 0.010724110;
            Q[24] = 0.008197536;
            Q[25] = 0.006209665;
            Q[26] = 0.004661188;
            Q[27] = 0.003466974;
            Q[28] = 0.002555130;
            Q[29] = 0.001865813;
            Q[30] = 0.001349898;
            Q[31] = 0.000967603;
            Q[32] = 0.000687138;
            Q[33] = 0.000483424;
            Q[34] = 0.000336929;
            Q[35] = 0.000232629;
            Q[36] = 0.000159109;
            Q[37] = 0.000107800;
            Q[38] = 0.000072348;
            Q[39] = 0.000048096;
            Q[40] = 0.000031671;

            // From here on use the approximation in http://cnx.org/content/m11537/latest/
            Q[41] = 0.000021771135897;
            Q[42] = 0.000014034063752;
            Q[43] = 0.000008961673661;
            Q[44] = 0.000005668743475;
            Q[45] = 0.000003551942468;
            Q[46] = 0.000002204533058;
            Q[47] = 0.000001355281953;
            Q[48] = 0.000000825270644;
            Q[49] = 0.000000497747091;
            Q[50] = 0.000000297343903;
            Q[51] = 0.000000175930101;
            Q[52] = 0.000000103096834;
            Q[53] = 0.000000059836778;
            Q[54] = 0.000000034395590;
            Q[55] = 0.000000019581382;
            Q[56] = 0.000000011040394;
            Q[57] = 0.000000006164833;
            Q[58] = 0.000000003409172;
            Q[59] = 0.000000001867079;
            Q[60] = 0.000000001012647;
            Q[61] = 0.000000000543915;
            Q[62] = 0.000000000289320;
            Q[63] = 0.000000000152404;
            Q[64] = 0.000000000079502;
            Q[65] = 0.000000000041070;
            Q[66] = 0.000000000021010;
            Q[67] = 0.000000000010644;
            Q[68] = 0.000000000005340;
            Q[69] = 0.000000000002653;
            Q[70] = 0.000000000001305;
        }
    }
}
