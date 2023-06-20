using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Util;
using log4net;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static HTM.Net.Algorithms.AnomalyLikelihood;

namespace HTM.Net.Algorithms;

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
 * 3. And again (make sure you use the new estimatorParams (a.k.a <see cref="NamedTuple"/>) returned 
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
[Serializable]
public class AnomalyLikelihood : Anomaly
{
    private static readonly ILog Log = LogManager.GetLogger(typeof(AnomalyLikelihood));

    private int _claLearningPeriod = 300;
    private int _estimationSamples = 300;
    private int _probationaryPeriod;
    private int _iteration;
    private int _reestimationPeriod;

    private bool _isWeighted;

    private List<Sample> _historicalScores = new List<Sample>();
    private AnomalyParams _distribution;

    public AnomalyLikelihood(bool useMovingAvg, int windowSize, bool isWeighted, int claLearningPeriod = -1, int estimationSamples = -1, int reestimationPeriod = 100)
        : base(useMovingAvg, windowSize)
    {
        this._isWeighted = isWeighted;
        this._claLearningPeriod = claLearningPeriod == VALUE_NONE ? this._claLearningPeriod : claLearningPeriod;
        this._estimationSamples = estimationSamples == VALUE_NONE ? this._estimationSamples : estimationSamples;
        this._probationaryPeriod = claLearningPeriod + estimationSamples;
        // How often we re-estimate the Gaussian distribution. The ideal is to
        // re-estimate every iteration but this is a performance hit. In general the
        // system is not very sensitive to this number as long as it is small
        // relative to the total number of records processed.
        this._reestimationPeriod = reestimationPeriod;
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
        if (_historicalScores.Count < _probationaryPeriod)
        {
            likelihoodRetval = 0.5;
        }
        else
        {
            if (_distribution == null || _iteration % _reestimationPeriod == 0)
            {
                this._distribution = EstimateAnomalyLikelihoods(
                    _historicalScores, 10, _claLearningPeriod).GetParams();
            }
            AnomalyLikelihoodMetrics metrics = UpdateAnomalyLikelihoods(new List<Sample> { dataPoint }, this._distribution);
            this._distribution = metrics.GetParams();
            likelihoodRetval = 1.0 - metrics.GetLikelihoods()[0];
        }
        _historicalScores.Add(dataPoint);
        this._iteration += 1;

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
        if (records.AveragedRecords.Count <= skipRecords)
        {
            distribution = NullDistribution();
        }
        else
        {
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
        double[] likelihoods = new double[records.AveragedRecords.Count];
        foreach (Sample sample in records.AveragedRecords)
        {
            likelihoods[i++] = NormalProbability(sample.score, distribution);
        }

        // Filter likelihood values
        double[] filteredLikelihoods = FilterLikelihoods(likelihoods);

        int len = likelihoods.Length;

        AnomalyParams @params = new AnomalyParams(
            distribution,
            new MovingAverage(records.HistoricalValues, records.Total, averagingWindow),
            len > 0
                ? Arrays.CopyOfRange(likelihoods, len - Math.Min(averagingWindow, len), len)
                : Array.Empty<double>());

        if (Log.IsDebugEnabled)
        {
            Log.Debug(
                $"Discovered params={@params} " +
                $"Number of likelihoods:{len}  " +
                $"First 20 likelihoods:{Arrays.CopyOfRange(filteredLikelihoods, 0, 20)}");
        }

        return new AnomalyLikelihoodMetrics(filteredLikelihoods, records, @params);
    }

    /**
         * Compute updated probabilities for anomalyScores using the given params.
         * 
         * @param anomalyScores     a list of records. Each record is a list with a {@link Sample} containing the
                                    following three elements: [timestamp, value, score]
         * @param params            Associative <see cref="NamedTuple"/> returned by the {@link AnomalyLikelihoodMetrics} from
         *                          {@link #estimateAnomalyLikelihoods(List, int, int)}
         * @return
         */
    public AnomalyLikelihoodMetrics UpdateAnomalyLikelihoods(List<Sample> anomalyScores, AnomalyParams @params)
    {
        int anomalySize = anomalyScores.Count;

        if (Log.IsDebugEnabled)
        {
            Log.Debug("in updateAnomalyLikelihoods");
            Log.Debug($"Number of anomaly scores: {anomalySize}");
            Log.Debug($"First 20: {anomalyScores.SubList(0, Math.Min(20, anomalySize))}");
            Log.Debug($"Params: {@params}");
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
        if ((histLikelihoods = @params.HistoricalLikelihoods) == null || histLikelihoods.Length == 0)
        {
            @params = new AnomalyParams(
                @params.Distribution,
                @params.MovingAverage,
                histLikelihoods = new double[] { 1 });
        }

        // Compute moving averages of these new scores using the previous values
        // as well as likelihood for these scores using the old estimator
        MovingAverage mvgAvg = @params.MovingAverage;
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
            likelihoods[i++] = NormalProbability(calc.GetAverage(), (Statistic)@params.Distribution);
        }

        // Filter the likelihood values. First we prepend the historical likelihoods
        // to the current set. Then we filter the values.  We peel off the likelihoods
        // to return and the last windowSize values to store for later.
        double[] likelihoods2 = ArrayUtils.Concat(histLikelihoods, likelihoods);
        double[] filteredLikelihoods = FilterLikelihoods(likelihoods2);
        likelihoods = Arrays.CopyOfRange(filteredLikelihoods, filteredLikelihoods.Length - likelihoods.Length, filteredLikelihoods.Length);
        double[] historicalLikelihoods = Arrays.CopyOf(likelihoods2, likelihoods2.Length - Math.Min(windowSize, likelihoods2.Length));

        // Update the estimator
        AnomalyParams newParams = new AnomalyParams(
            @params.Distribution,
            new MovingAverage(historicalValues, total, windowSize),
            historicalLikelihoods);

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
                else
                {
                    filteredLikelihoods[i + 1] = yellowThreshold;
                }
            }
            else
            {
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

            if (Log.IsDebugEnabled)
            {
                Log.Debug(
                    $"Aggregating input record: {record}, Result: {averagedRecordList[averagedRecordList.Count - 1]}");
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
        if (Log.IsDebugEnabled)
        {
            Log.Debug("Returning nullDistribution");
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
        retVal = _isWeighted ? retVal * (1 - probability) : 1 - probability;

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
         * @param params    a <see cref="NamedTuple"/> containing { distribution, movingAverage, historicalLikelihoods }
         * @return
         */
    public bool IsValidEstimatorParams(AnomalyParams @params)
    {
        if (@params.Distribution == null || @params.MovingAverage == null)
        {
            return false;
        }

        Statistic stat = @params.Distribution;
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
    [Serializable]
    public class AnomalyParams : Persistable
    {
        [JsonConstructor]
        [Obsolete("This constructor is only for JSON deserialization. Use the default constructor instead.")]
        public AnomalyParams()
        {
        }

        /// <summary>
        /// Constructs a new <see cref="AnomalyParams"/>
        /// </summary>
        /// <param name="distribution"></param>
        /// <param name="movingAverage"></param>
        /// <param name="historicalLikelihoods"></param>
        public AnomalyParams(
            Statistic distribution,
            MovingAverage movingAverage,
            double[] historicalLikelihoods)
        {
            if (distribution == null || movingAverage == null || historicalLikelihoods == null)
            {
                throw new ArgumentException(
                    "AnomalyParams must have \"distribution\", \"movingAverage\", and \"historicalLikelihoods\"" +
                    " parameters. keys.Length != 3 or values.Length != 3");
            }

            Distribution = distribution;
            MovingAverage = movingAverage;
            HistoricalLikelihoods = historicalLikelihoods;
            WindowSize = MovingAverage.GetWindowSize();
        }

        /// <summary>
        /// Returns the <see cref="Statistic"/> containing point calculations.
        /// </summary>
        [JsonProperty]
        public Statistic Distribution { get; private set; }

        /// <summary>
        /// Returns the <see cref="MovingAverage"/> object
        /// </summary>
        [JsonProperty]
        public MovingAverage MovingAverage { get; private set; }

        /// <summary>
        /// Returns the array of computed likelihoods
        /// </summary>
        [JsonProperty]
        public double[] HistoricalLikelihoods { get; private set; }

        /// <summary>
        /// Returns the window size of the moving average.
        /// </summary>
        [JsonProperty]
        public int WindowSize { get; private set; }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = base.GetHashCode();
            result = prime * result + ((Distribution == null) ? 0 : Distribution.GetHashCode());
            result = prime * result + Arrays.GetHashCode(HistoricalLikelihoods);
            result = prime * result + ((MovingAverage == null) ? 0 : MovingAverage.GetHashCode());
            result = prime * result + WindowSize;
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }

            /*if (!base.Equals(obj))
            {
                return false;
            }*/

            if (GetType() != obj.GetType())
            {
                return false;
            }

            AnomalyParams other = (AnomalyParams)obj;
            if (Distribution == null)
            {
                if (other.Distribution != null)
                {
                    return false;
                }
            }
            else if (!Distribution.Equals(other.Distribution))
            {
                return false;
            }

            if (!Arrays.AreEqual(HistoricalLikelihoods, other.HistoricalLikelihoods))
            {
                return false;
            }

            if (MovingAverage == null)
            {
                if (other.MovingAverage != null)
                {
                    return false;
                }
            }
            else if (!MovingAverage.Equals(other.MovingAverage))
            {
                return false;
            }

            if (WindowSize != other.WindowSize)
            {
                return false;
            }

            return true;
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

// CPP version
[Serializable]
public struct DistributionParams
{
    public string Name;
    public double Mean;
    public double Variance;
    public double Stdev;

    public DistributionParams(string name, double mean, double variance, double stdev)
    {
        Name = name;
        Mean = mean;
        Variance = variance;
        Stdev = stdev;
    }
}

[Serializable]
public class AnomalyLikelihoodCpp
{
    private const double DEFAULT_ANOMALY = 0.5;
    private const double THRESHOLD_MEAN = 0.03;
    private const double THRESHOLD_VARIANCE = 0.0003;

    private DistributionParams distribution;
    private int iteration;
    private int lastTimestamp;
    private int initialTimestamp;
    private MovingAverage averagedAnomaly;
    private SlidingWindow<double> runningLikelihoods;
    private SlidingWindow<double> runningRawAnomalyScores;
    private SlidingWindow<double> runningAverageAnomalies;

    public int LearningPeriod { get; }
    public int ReestimationPeriod { get; }
    public int ProbationaryPeriod { get; }

    public AnomalyLikelihoodCpp(int learningPeriod = 288, int estimationSamples = 100, int historicWindowSize = 8640, int reestimationPeriod = 100, int aggregationWindow = 10)
    {
        LearningPeriod = learningPeriod;
        ReestimationPeriod = reestimationPeriod;
        ProbationaryPeriod = LearningPeriod + estimationSamples;

        averagedAnomaly = new MovingAverage(aggregationWindow);
        runningLikelihoods = new SlidingWindow<double>(historicWindowSize);
        runningRawAnomalyScores = new SlidingWindow<double>(historicWindowSize);
        runningAverageAnomalies = new SlidingWindow<double>(historicWindowSize);

        iteration = 0;
        lastTimestamp = -1;
        initialTimestamp = -1;

        distribution = new DistributionParams("unknown", 0.0, 0.0, 0.0);

        Console.WriteLine("C# AnomalyLikelihood may still need some testing.");
    }

    public double AnomalyProbability(double anomalyScore, int timestamp = -1)
    {
        double likelihood = DEFAULT_ANOMALY;

        if (timestamp < 0)
        {
            timestamp = (int)iteration;
        }
        else
        {
            if (timestamp <= lastTimestamp)
                throw new ArgumentException("Timestamp must be greater than the last recorded timestamp.");

            lastTimestamp = timestamp;
        }

        if (initialTimestamp == -1)
            initialTimestamp = timestamp;

        uint timeElapsed = (uint)(timestamp - initialTimestamp);

        runningRawAnomalyScores.Append(anomalyScore);
        double newAvg = averagedAnomaly.Compute(anomalyScore);
        runningAverageAnomalies.Append(newAvg);
        iteration++;

        if (timeElapsed < ProbationaryPeriod)
        {
            runningLikelihoods.Append(likelihood);
            return DEFAULT_ANOMALY;
        }

        List<double> anomalies = runningAverageAnomalies.GetData();

        if (timeElapsed >= initialTimestamp + ReestimationPeriod || distribution.Name == "unknown")
        {
            int numSkipRecords = CalcSkipRecords(iteration, runningAverageAnomalies.Size(), LearningPeriod);
            EstimateAnomalyLikelihoods(anomalies, numSkipRecords);
            if (timeElapsed >= initialTimestamp + ReestimationPeriod)
                initialTimestamp = -1;
        }

        List<double> likelihoods = UpdateAnomalyLikelihoods(anomalies);

        if (likelihoods.Count > 0)
        {
            likelihood = likelihoods[^1];
            runningLikelihoods.Append(likelihood);
        }

        return likelihood;
    }

    private int CalcSkipRecords(int iteration, int dataSize, int learningPeriod)
    {
        int numSkipRecords = 0;

        if (dataSize > learningPeriod)
        {
            numSkipRecords = (int)(dataSize - learningPeriod);
            iteration -= numSkipRecords;
        }

        return numSkipRecords;
    }

    private void EstimateAnomalyLikelihoods(List<double> anomalyScores, int skipRecords = 0, int verbosity = 0)
    {
        List<double> normalizedAnomalyScores = NormalizeAnomalyScores(anomalyScores, skipRecords);
        distribution = EstimateNormal(normalizedAnomalyScores, true);
    }

    private List<double> UpdateAnomalyLikelihoods(List<double> anomalyScores, int verbosity = 0)
    {
        List<double> normalizedAnomalyScores = NormalizeAnomalyScores(anomalyScores);
        List<double> likelihoods = EstimateAnomalyLikelihoods_(normalizedAnomalyScores, verbosity);

        return likelihoods;
    }

    private List<double> EstimateAnomalyLikelihoods_(List<double> anomalyScores, int skipRecords = 0, int verbosity = 0)
    {
        List<double> likelihoods = new List<double>();
        int dataSize = (int)anomalyScores.Count;

        if (skipRecords > dataSize)
            return likelihoods;

        double maxLikelihood = double.MinValue;
        double minLikelihood = double.MaxValue;
        double likelihoodSum = 0.0;

        for (int i = skipRecords; i < dataSize; i++)
        {
            double anomalyScore = anomalyScores[(int)i];
            double logLikelihood = ComputeLogLikelihood(anomalyScore);

            likelihoodSum += logLikelihood;
            likelihoods.Add(logLikelihood);

            if (logLikelihood > maxLikelihood)
                maxLikelihood = logLikelihood;

            if (logLikelihood < minLikelihood)
                minLikelihood = logLikelihood;
        }

        return likelihoods;
    }

    private List<double> NormalizeAnomalyScores(List<double> anomalyScores, int skipRecords = 0)
    {
        List<double> normalizedScores = new List<double>();
        uint dataSize = (uint)anomalyScores.Count;

        if (skipRecords > dataSize)
            return normalizedScores;

        double maxScore = double.MinValue;
        double minScore = double.MaxValue;

        for (int i = skipRecords; i < dataSize; i++)
        {
            double score = anomalyScores[(int)i];

            if (score > maxScore)
                maxScore = score;

            if (score < minScore)
                minScore = score;
        }

        double scoreRange = maxScore - minScore;

        if (scoreRange <= 0)
            return normalizedScores;

        for (int i = skipRecords; i < dataSize; i++)
        {
            double score = anomalyScores[(int)i];
            double normalizedScore = (score - minScore) / scoreRange;
            normalizedScores.Add(normalizedScore);
        }

        return normalizedScores;
    }

    private DistributionParams EstimateNormal(List<double> anomalyScores, bool performLowerBoundCheck = true)
    {
        DistributionParams distributionParams;

        int dataSize = anomalyScores.Count;

        if (dataSize == 0)
        {
            distributionParams = new DistributionParams("unknown", 0.0, 0.0, 0.0);
            return distributionParams;
        }

        double mean = anomalyScores.Mean();
        double variance = 0.0;
        double stdev = 0.0;

        foreach (double score in anomalyScores)
        {
            mean += score;
            variance += score * score;
        }

        mean /= dataSize;
        variance /= dataSize;
        variance -= mean * mean;

        if (performLowerBoundCheck && variance < THRESHOLD_VARIANCE)
            variance = THRESHOLD_VARIANCE;

        stdev = Math.Sqrt(variance);

        distributionParams = new DistributionParams("normal", mean, variance, stdev);

        return distributionParams;
    }

    public double ComputeLogLikelihood(double anomalyScore)
    {
        double likelihood = 0.0;

        if (distribution.Name == "normal")
        {
            double mean = distribution.Mean;
            double stdev = distribution.Stdev;

            double exponent = -0.5 * ((anomalyScore - mean) / stdev) * ((anomalyScore - mean) / stdev);
            double coefficient = 1.0 / (stdev * Math.Sqrt(2.0 * Math.PI));

            likelihood = coefficient * Math.Exp(exponent);
        }

        return likelihood;
    }

    [Serializable]
    public class MovingAverage
    {
        private Queue<double> values;
        private int size;
        private double sum;

        public MovingAverage(int size)
        {
            this.size = size;
            values = new Queue<double>();
            sum = 0.0;
        }

        public double Compute(double value)
        {
            sum += value;
            values.Enqueue(value);

            if (values.Count > size)
            {
                double removedValue = values.Dequeue();
                sum -= removedValue;
            }

            return sum / values.Count;
        }
    }

    [Serializable]
    public class SlidingWindow<T>
    {
        private Queue<T> data;
        private int size;

        public SlidingWindow(int size)
        {
            this.size = size;
            data = new Queue<T>();
        }

        public void Append(T item)
        {
            data.Enqueue(item);

            if (data.Count > size)
                data.Dequeue();
        }

        public List<T> GetData()
        {
            return new List<T>(data);
        }

        public int Size()
        {
            return data.Count;
        }
    }
}


