using System;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    /**
 * Container class to hold the results of {@link AnomalyLikelihood} estimations
 * and updates.
 * 
 * @author David Ray
 * @see AnomalyLikelihood
 * @see AnomalyLikelihoodTest
 */
    [Serializable]
    public class AnomalyLikelihoodMetrics : Persistable
    {
        private readonly AnomalyLikelihood.AnomalyParams _params;
        private Anomaly.AveragedAnomalyRecordList _aggRecordList;
        private double[] _likelihoods;

        /**
         * Constructs a new {@code AnomalyLikelihoodMetrics}
         * 
         * @param likelihoods       array of pre-computed estimations
         * @param aggRecordList     List of {@link Sample}s which are basically a set of date, value, average score,
         *                          a list of historical values, and a running total.
         * @param params            {@link AnomalyParams} which are a {@link Statistic}, array of likelihoods,
         *                          and a {@link MovingAverage} 
         */
        public AnomalyLikelihoodMetrics(double[] likelihoods, Anomaly.AveragedAnomalyRecordList aggRecordList, AnomalyLikelihood.AnomalyParams @params)
        {
            _params = @params;
            _aggRecordList = aggRecordList;
            _likelihoods = likelihoods;
        }

        /**
         * Utility method to copy this {@link AnomalyLikelihoodMetrics} object.
         * @return
         */
        public AnomalyLikelihoodMetrics Copy()
        {
            //List<object> vals = new List<object>();
            //foreach (var key in @params.GetParameters().Keys())
            //{
            //    vals.Add(@params.GetParameters().GetParameterByKey(key));
            //}

            return new AnomalyLikelihoodMetrics(
                Arrays.CopyOf(_likelihoods, _likelihoods.Length),
                _aggRecordList,
                new AnomalyLikelihood.AnomalyParams(_params.GetParameters()));
        }

        /**
         * Returns the array of computed likelihoods
         * @return
         */
        public double[] GetLikelihoods()
        {
            return _likelihoods;
        }

        /**
         * <pre>
         * Returns the record list which are:
         *     List of {@link Sample}s which are basically a set of date, value, average score,
         *     a list of historical values, and a running total.
         * </pre>
         * @return
         */
        public Anomaly.AveragedAnomalyRecordList GetAvgRecordList()
        {
            return _aggRecordList;
        }

        /**
         * <pre>
         * Returns the {@link AnomalyParams} which is:
         *     a {@link Statistic}, array of likelihoods,
         *     and a {@link MovingAverage}
         * </pre> 
         * @return
         */
        public AnomalyLikelihood.AnomalyParams GetParams()
        {
            return _params;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((_aggRecordList == null) ? 0 : _aggRecordList.GetHashCode());
            result = prime * result + _likelihoods.GetHashCode();
            result = prime * result + ((_params == null) ? 0 : _params.GetHashCode());
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
            AnomalyLikelihoodMetrics other = (AnomalyLikelihoodMetrics)obj;
            if (_aggRecordList == null)
            {
                if (other._aggRecordList != null)
                    return false;
            }
            else if (!_aggRecordList.Equals(other._aggRecordList))
                return false;
            if (!Arrays.AreEqual(_likelihoods, other._likelihoods))
                return false;
            if (_params == null) {
                if (other._params != null)
                return false;
            } else if (!_params.Equals(other._params))
            return false;
            return true;
        }
    }
}
