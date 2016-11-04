using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    /// <summary>
    /// This module analyzes and estimates the distribution of averaged anomaly scores
    /// from a CLA model. Given a new anomaly score `s`, estimates `P(score >= s)`.
    /// 
    /// The number `P(score >= s)` represents the likelihood of the current state of
    /// predictability. For example, a likelihood of 0.01 or 1% means we see this much
    /// predictability about one out of every 100 records. The number is not as unusual
    /// as it seems. For records that arrive every minute, this means once every hour
    /// and 40 minutes. A likelihood of 0.0001 or 0.01% means we see it once out of
    /// 10,000 records, or about once every 7 days.
    /// 
    /// USAGE
    /// -----
    /// 
    /// The <see cref="Anomaly"/> base class follows the factory pattern and can construct an
    /// appropriately configured anomaly calculator by invoking the following:
    /// 
    /// <pre>
    /// Map<string, object> params = new Map<string, object>();
    /// params.Add(KEY_MODE, Mode.LIKELIHOOD);            // May be Mode.PURE or Mode.WEIGHTED
    /// params.Add(KEY_USE_MOVING_AVG, true);             // Instructs the Anomaly class to compute moving average
    /// params.Add(KEY_WINDOW_SIZE, 10);                  // #of inputs over which to compute the moving average
    /// params.Add(KEY_IS_WEIGHTED, true);                // Use a weighted moving average or not
    /// 
    /// // Instantiate the Anomaly computer
    /// Anomaly anomalyComputer = Anomaly.Create(params); // Returns the appropriate Anomaly
    ///                                                   // implementation.
    /// int[] actual = array of input columns at time t
    /// int[] predicted = array of predicted columns for t+1
    /// double anomaly = an.Compute(
    ///     actual, 
    ///     predicted, 
    ///     0 (inputValue = OPTIONAL, needed for likelihood calcs), 
    ///     timestamp);
    ///     
    /// double anomalyProbability = anomalyComputer.AnomalyProbability(
    ///     inputValue, anomaly, timestamp);
    /// </pre>
    /// 
    /// Raw functions
    /// -------------
    /// 
    /// There are two lower level functions, estimateAnomalyLikelihoods and
    /// updateAnomalyLikelihoods. The details of these are described by the method docs.
    /// 
    /// For more information please see: <see cref="AnomalyTest"/> and <see cref="AnomalyLikelihoodTest"/>
    /// </summary>
    [Serializable]
    public class Anomaly : Persistable
    {
        private readonly Func<Anomaly, int[], int[], double, long, double> _computeFunc;
        /// <summary>
        /// Modes to use for factory creation method
        /// </summary>
        public enum Mode { PURE, LIKELIHOOD, WEIGHTED };

        // Instantiation keys
        public const int VALUE_NONE = -1;

        protected MovingAverage movingAverage;

        protected bool useMovingAverage;

        /// <summary>
        /// Constructs a new <see cref="Anomaly"/>
        /// </summary>
        protected Anomaly()
            : this(false, -1)
        {

        }

        /// <summary>
        /// Constructs a new <see cref="Anomaly"/>
        /// </summary>
        /// <param name="useMovingAverage">indicates whether to apply and store a moving average</param>
        /// <param name="windowSize">size of window to average over</param>
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

        private Anomaly(bool useMovingAverage, int windowSize, Func<Anomaly, int[], int[], double, long, double> computeFunc)
            : this(useMovingAverage, windowSize)
        {
            _computeFunc = computeFunc;
        }

        /// <summary>
        /// Convenience method to create a simplistic Anomaly computer in <see cref="Mode.PURE"/>
        /// </summary>
        /// <returns></returns>
        public static Anomaly Create()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Mode.PURE);

            return Create(@params);
        }

        /// <summary>
        /// Returns an <see cref="Anomaly"/> configured to execute the type of calculation specified by the <see cref="Mode"/>, 
        /// and whether or not to apply a moving average.
        /// 
        /// Must have one of "Mode" = <see cref="Mode.LIKELIHOOD"/>, <see cref="Mode.PURE"/>, <see cref="Mode.WEIGHTED"/>
        /// </summary>
        /// <param name="params">Parameters</param>
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

        /// <summary>
        /// The raw anomaly score is the fraction of active columns not predicted.
        /// </summary>
        /// <param name="activeColumns">an array of active column indices</param>
        /// <param name="prevPredictedColumns">array of column indices predicted in the previous step</param>
        /// <returns>anomaly score 0..1 </returns>
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
            else
            {
                score = 0.0d;
            }

            return score;
        }

        /// <summary>
        /// Compute the anomaly score as the percent of active columns not predicted.
        /// </summary>
        /// <param name="activeColumns">array of active column indices</param>
        /// <param name="predictedColumns">array of columns indices predicted in this step (used for anomaly in step T+1)</param>
        /// <param name="inputValue">(optional) value of current input to encoders (eg "cat" for category encoder) (used in anomaly-likelihood)</param>
        /// <param name="timestamp">(optional) date timestamp when the sample occurred (used in anomaly-likelihood)</param>
        /// <returns></returns>
        public virtual double Compute(int[] activeColumns, int[] predictedColumns, double inputValue, long timestamp)
        {
            if (_computeFunc != null)
            {
                return _computeFunc(this, activeColumns, predictedColumns, inputValue, timestamp);
            }
            throw new InvalidOperationException("Implement Compute in derived class.");
        }

        #region Inner Class Definitions   

        /// <summary>
        /// Container to hold interim <see cref="AnomalyLikelihood"/> calculations.
        /// </summary>
        public class AveragedAnomalyRecordList
        {
            internal readonly List<Sample> AveragedRecords;
            internal readonly List<double> HistoricalValues;
            internal readonly double Total;

            /// <summary>
            /// Constructs a new <see cref="AveragedAnomalyRecordList"/>
            /// </summary>
            /// <param name="averagedRecords">List of samples which are { timestamp, average, value } at a data point</param>
            /// <param name="historicalValues">List of values of a given window size (moving average grouping)</param>
            /// <param name="total">Sum of all values in the series</param>
            public AveragedAnomalyRecordList(List<Sample> averagedRecords, List<double> historicalValues, double total)
            {
                AveragedRecords = averagedRecords;
                HistoricalValues = historicalValues;
                Total = total;
            }

            /// <summary>
            /// Returns a list of the averages in the contained averaged record list.
            /// </summary>
            public List<double> GetMetrics()
            {
                return AveragedRecords.Select(s => s.score).ToList();
            }

            /// <summary>
            /// Returns a list of the sample values in the contained averaged record list.
            /// </summary>
            public List<double> GetSamples()
            {
                return AveragedRecords.Select(s => s.value).ToList();
            }

            /// <summary>
            /// Returns the size of the count of averaged records (i.e. <see cref="Sample"/>s)
            /// </summary>
            public int Count
            {
                get { return AveragedRecords.Count; } // let fail if null
            }

            public override int GetHashCode()
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + (AveragedRecords?.GetHashCode() ?? 0);
                result = prime * result + (HistoricalValues?.GetHashCode() ?? 0);
                long temp;
                temp = BitConverter.DoubleToInt64Bits(Total);
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
                if (AveragedRecords == null)
                {
                    if (other.AveragedRecords != null)
                        return false;
                }
                else if (!AveragedRecords.Equals(other.AveragedRecords))
                    return false;
                if (HistoricalValues == null)
                {
                    if (other.HistoricalValues != null)
                        return false;
                }
                else if (!HistoricalValues.Equals(other.HistoricalValues))
                    return false;
                if (BitConverter.DoubleToInt64Bits(Total) != BitConverter.DoubleToInt64Bits(other.Total))
                    return false;
                return true;
            }
        }

        #endregion
    }
}