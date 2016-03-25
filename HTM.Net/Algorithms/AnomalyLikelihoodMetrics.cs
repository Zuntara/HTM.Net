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
    public class AnomalyLikelihoodMetrics
    {
        private AnomalyLikelihood.AnomalyParams @params;
        private Anomaly.AveragedAnomalyRecordList aggRecordList;
        private double[] likelihoods;

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
            this.@params = @params;
            this.aggRecordList = aggRecordList;
            this.likelihoods = likelihoods;
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
                Arrays.CopyOf(likelihoods, likelihoods.Length),
                aggRecordList,
                new AnomalyLikelihood.AnomalyParams(@params.GetParameters()));
        }

        /**
         * Returns the array of computed likelihoods
         * @return
         */
        public double[] GetLikelihoods()
        {
            return likelihoods;
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
            return aggRecordList;
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
            return @params;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((aggRecordList == null) ? 0 : aggRecordList.GetHashCode());
            result = prime * result + likelihoods.GetHashCode();
            result = prime * result + ((@params == null) ? 0 : @params.GetHashCode());
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
            if (aggRecordList == null)
            {
                if (other.aggRecordList != null)
                    return false;
            }
            else if (!aggRecordList.Equals(other.aggRecordList))
                return false;
            if (!Arrays.AreEqual(likelihoods, other.likelihoods))
                return false;
            if (@params == null) {
                if (other.@params != null)
                return false;
            } else if (!@params.Equals(other.@params))
            return false;
            return true;
        }
    }
}
