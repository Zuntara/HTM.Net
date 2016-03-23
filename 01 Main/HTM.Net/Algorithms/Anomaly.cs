using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    /**
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
     * USAGE
     * -----
     *
     * The {@code Anomaly} base class follows the factory pattern and can construct an
     * appropriately configured anomaly calculator by invoking the following:
     * 
     * <pre>
     * Map<String, Object> params = new HashMap<>();
     * params.put(KEY_MODE, Mode.LIKELIHOOD);            // May be Mode.PURE or Mode.WEIGHTED
     * params.put(KEY_USE_MOVING_AVG, true);             // Instructs the Anomaly class to compute moving average
     * params.put(KEY_WINDOW_SIZE, 10);                  // #of inputs over which to compute the moving average
     * params.put(KEY_IS_WEIGHTED, true);                // Use a weighted moving average or not
     * 
     * // Instantiate the Anomaly computer
     * Anomaly anomalyComputer = Anomaly.create(params); // Returns the appropriate Anomaly
     *                                                   // implementation.
     * int[] actual = array of input columns at time t
     * int[] predicted = array of predicted columns for t+1
     * double anomaly = an.compute(
     *     actual, 
     *     predicted, 
     *     0 (inputValue = OPTIONAL, needed for likelihood calcs), 
     *     timestamp);
     *     
     * double anomalyProbability = anomalyComputer.anomalyProbability(
     *     inputValue, anomaly, timestamp);
     * </pre>
     *
     * Raw functions
     * -------------
     * 
     * There are two lower level functions, estimateAnomalyLikelihoods and
     * updateAnomalyLikelihoods. The details of these are described by the method docs.
     * 
     * For more information please see: {@link AnomalyTest} and {@link AnomalyLikelihoodTest}
     * 
     * @author Numenta
     * @author David Ray
     * @see AnomalyTest
     * @see AnomalyLikelihoodTest
     */
    public class Anomaly
    {
        private readonly Func<Anomaly, int[], int[], double, long, double> _computeFunc;
        /** Modes to use for factory creation method */
        public enum Mode { PURE, LIKELIHOOD, WEIGHTED };

        // Instantiation keys
        public const int VALUE_NONE = -1;
        //public const string KEY_MODE = "mode";
        //public const string KEY_LEARNING_PERIOD = "claLearningPeriod";
        //public const string KEY_ESTIMATION_SAMPLES = "estimationSamples";
        //public const string KEY_USE_MOVING_AVG = "useMovingAverage";
        //public const string KEY_WINDOW_SIZE = "windowSize";
        //public const string KEY_IS_WEIGHTED = "isWeighted";
        //// Configs   
        //public const string KEY_DIST = "distribution";
        //public const string KEY_MVG_AVG = "movingAverage";
        //public const string KEY_HIST_LIKE = "historicalLikelihoods";
        //public const string KEY_HIST_VALUES = "historicalValues";
        //public const string KEY_TOTAL = "total";

        //// Computational argument keys
        //public const string KEY_MEAN = "mean";
        //public const string KEY_STDEV = "stdev";
        //public const string KEY_VARIANCE = "variance";

        protected MovingAverage movingAverage;

        protected bool useMovingAverage;

        /**
         * Constructs a new {@code Anomaly}
         */
        protected Anomaly()
            : this(false, -1)
        {

        }

        /**
         * Constructs a new {@code Anomaly}
         * 
         * @param useMovingAverage  indicates whether to apply and store a moving average
         * @param windowSize        size of window to average over
         */
        protected Anomaly(bool useMovingAverage, int windowSize)
        {
            this.useMovingAverage = useMovingAverage;
            if (this.useMovingAverage)
            {
                if (windowSize < 1)
                {
                    throw new ArgumentException("Window size must be > 0, when using moving average.");
                }
                movingAverage = new MovingAverage(null, windowSize);
            }
        }

        private Anomaly(bool useMovingAverage, int windowSize, Func<Anomaly, int[], int[],double,long,double> computeFunc)
            : this(useMovingAverage, windowSize)
        {
            _computeFunc = computeFunc;
        }

        /**
         * Convenience method to create a simplistic Anomaly computer in 
         * {@link Mode#PURE}
         *  
         * @return
         */
        public static Anomaly Create()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Mode.PURE);

            return Create(@params);
        }

        /**
         * Returns an {@code Anomaly} configured to execute the type
         * of calculation specified by the {@link Mode}, and whether or
         * not to apply a moving average.
         * 
         * Must have one of "MODE" = {@link Mode#LIKELIHOOD}, {@link Mode#PURE}, {@link Mode#WEIGHTED}
         * 
         * @param   p       Parameters 
         * @return
         */
        public static Anomaly Create(Parameters @params)
        {
            bool useMovingAvg = (bool)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, false);
            int windowSize = ((int?)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, -1)).GetValueOrDefault(-1);
            if (useMovingAvg && windowSize < 1)
            {
                throw new ArgumentException("windowSize must be > 0, when using moving average.");
            }

            Mode? mode = (Mode?)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE);
            if (mode == null)
            {
                throw new ArgumentException("MODE cannot be null.");
            }

            switch (mode)
            {
                case Mode.PURE:
                {
                    return new Anomaly(useMovingAvg, windowSize, (anomaly, activeColumns, predictedColumns, inputValue, timestamp) =>
                    {
                        double retVal = ComputeRawAnomalyScore(activeColumns, predictedColumns);
                        if (anomaly.useMovingAverage)
                        {
                            retVal = anomaly.movingAverage.Next(retVal);
                        }
                        return retVal;
                    });
                }
                case Mode.LIKELIHOOD:
                case Mode.WEIGHTED:
                    {
                        bool isWeighted = (bool)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_IS_WEIGHTED, false);
                        int claLearningPeriod = (int)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_LEARNING_PERIOD, VALUE_NONE);
                        int estimationSamples = (int)@params.GetParameterByKey(Parameters.KEY.ANOMALY_KEY_ESTIMATION_SAMPLES, VALUE_NONE);

                        return new AnomalyLikelihood(useMovingAvg, windowSize, isWeighted, claLearningPeriod, estimationSamples);
                    }
                default: return null;
            }
        }

        /**
         * The raw anomaly score is the fraction of active columns not predicted.
         * 
         * @param   activeColumns           an array of active column indices
         * @param   prevPredictedColumns    array of column indices predicted in the 
         *                                  previous step
         * @return  anomaly score 0..1 
         */
        public static double ComputeRawAnomalyScore(int[] activeColumns, int[] prevPredictedColumns)
        {
            double score = 0;

            int nActiveColumns = activeColumns.Length;
            if (nActiveColumns > 0)
            {
                // Test whether each element of a 1-D array is also present in a second
                // array. Sum to get the total # of columns that are active and were
                // predicted.
                score = ArrayUtils.In1d(activeColumns, prevPredictedColumns).Length;
                // Get the percent of active columns that were NOT predicted, that is
                // our anomaly score.
                score = (nActiveColumns - score) / (double)nActiveColumns;
            }
            else if (prevPredictedColumns.Length > 0)
            {
                score = 1.0d;
            }

            return score;
        }

        /**
         * Compute the anomaly score as the percent of active columns not predicted.
         * 
         * @param activeColumns         array of active column indices
         * @param predictedColumns      array of columns indices predicted in this step
         *                              (used for anomaly in step T+1)
         * @param inputValue            (optional) value of current input to encoders 
         *                              (eg "cat" for category encoder)
         *                              (used in anomaly-likelihood)
         * @param timestamp             timestamp: (optional) date timestamp when the sample occurred
         *                              (used in anomaly-likelihood)
         * @return
         */

        public virtual double Compute(int[] activeColumns, int[] predictedColumns, double inputValue, long timestamp)
        {
            if (_computeFunc != null)
            {
                return _computeFunc(this, activeColumns, predictedColumns, inputValue, timestamp);
            }
            throw new InvalidOperationException("Implement Compute in derived class.");
        }


        //////////////////////////////////////////////////////////////////////////////////////
        //                            Inner Class Definitions                               //
        //////////////////////////////////////////////////////////////////////////////////////
        /**
         * Container to hold interim {@link AnomalyLikelihood} calculations.
         * 
         * @author David Ray
         * @see AnomalyLikelihood
         * @see MovingAverage
         */
        public class AveragedAnomalyRecordList
        {
            internal readonly List<Sample> averagedRecords;
            internal readonly List<double> historicalValues;
            internal readonly double total;

            /**
             * Constructs a new {@code AveragedAnomalyRecordList}
             * 
             * @param averagedRecords       List of samples which are { timestamp, average, value } at a data point
             * @param historicalValues      List of values of a given window size (moving average grouping)
             * @param total                 Sum of all values in the series
             */
            public AveragedAnomalyRecordList(List<Sample> averagedRecords, List<double> historicalValues, double total)
            {
                this.averagedRecords = averagedRecords;
                this.historicalValues = historicalValues;
                this.total = total;
            }

            /**
             * Returns a list of the averages in the contained averaged record list.
             * @return
             */
            public List<double> GetMetrics()
            {
                List<double> retVal = new List<double>();
                foreach (Sample s in averagedRecords)
                {
                    retVal.Add(s.score);
                }

                return retVal;
            }

            /**
             * Returns a list of the sample values in the contained averaged record list.
             * @return
             */
            public List<double> GetSamples()
            {
                List<double> retVal = new List<double>();
                foreach (Sample s in averagedRecords)
                {
                    retVal.Add(s.value);
                }

                return retVal;
            }

            /**
             * Returns the size of the count of averaged records (i.e. {@link Sample}s)
             * @return
             */
            public int Count
            {
                get { return averagedRecords.Count; } //let fail if null
            }

            public override int GetHashCode()
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + ((averagedRecords == null) ? 0 : averagedRecords.GetHashCode());
                result = prime * result + ((historicalValues == null) ? 0 : historicalValues.GetHashCode());
                long temp;
                temp = BitConverter.DoubleToInt64Bits(total);
                result = prime * result + (int)(temp ^ (int)((uint)temp >> 32));
                return result;
            }

            public override bool Equals(object obj)
            {
                if (this == obj)
                    return true;
                if (obj == null)
                    return false;
                if (GetType() != obj.GetType())
                    return false;
                AveragedAnomalyRecordList other = (AveragedAnomalyRecordList)obj;
                if (averagedRecords == null)
                {
                    if (other.averagedRecords != null)
                        return false;
                }
                else if (!averagedRecords.Equals(other.averagedRecords))
                    return false;
                if (historicalValues == null)
                {
                    if (other.historicalValues != null)
                        return false;
                }
                else if (!historicalValues.Equals(other.historicalValues))
                    return false;
                if (BitConverter.DoubleToInt64Bits(total) != BitConverter.DoubleToInt64Bits(other.total))
                    return false;
                return true;
            }
        }

    }
}