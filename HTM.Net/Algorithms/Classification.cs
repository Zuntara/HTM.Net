using System;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    /// <summary>
    /// Container for the results of a classification computation by the <see cref="CLAClassifier"/> and <see cref="SDRClassifier"/>
    /// </summary>
    /// <typeparam name="T">Type of actual value output</typeparam>
    [Serializable]
    public class Classification<T> : Persistable
    {
        /// <summary>
        /// Array of actual values
        /// </summary>
        private T[] _actualValues;

        /// <summary>
        /// Map of step count -to- probabilities
        /// </summary>
        private Map<int, double[]> _probabilities = new Map<int, double[]>();

        /**
         * Utility method to copy the contents of a Classification.
         * 
         * @return  a copy of this {@code Classification} which will not be affected
         * by changes to the original.
         */
        public Classification<T> Copy()
        {
            Classification<T> retVal = new Classification<T>();
            retVal._actualValues = Arrays.CopyOf(_actualValues, _actualValues.Length);
            retVal._probabilities = new Map<int, double[]>(_probabilities);
            return retVal;
        }

        /**
         * Returns the actual value for the specified bucket index
         * 
         * @param bucketIndex
         * @return
         */
        public T GetActualValue(int bucketIndex)
        {
            if (_actualValues == null || _actualValues.Length < bucketIndex + 1)
            {
                return default(T);
            }
            return _actualValues[bucketIndex];
        }

        /**
         * Returns all actual values entered
         * 
         * @return  array of type &lt;T&gt;
         */
        public T[] GetActualValues()
        {
            return _actualValues;
        }

        /// <summary>
        /// Sets the array of actual values being entered.
        /// </summary>
        /// <param name="values"></param>
        public void SetActualValues(T[] values)
        {
            _actualValues = values;
        }

        /**
         * Returns a count of actual values entered
         * @return
         */
        public int GetActualValueCount()
        {
            return _actualValues.Length;
        }

        /**
         * Returns the probability at the specified index for the given step
         * @param step
         * @param bucketIndex
         * @return
         */
        public double GetStat(int step, int bucketIndex)
        {
            return _probabilities[step][bucketIndex];
        }

        /**
         * Sets the array of probabilities for the specified step
         * @param step
         * @param votes
         */
        public void SetStats(int step, double[] votes)
        {
            if (_probabilities.ContainsKey(step))
            {
                _probabilities[step] = votes;
            }
            else
            {
                _probabilities.Add(step, votes);
            }
        }

        /**
         * Returns the probabilities for the specified step
         * @param step
         * @return
         */
        public double[] GetStats(int step)
        {
            return _probabilities[step];
        }

        /// <summary>
        /// Returns the input value corresponding with the highest probability
        /// for the specified step.
        /// </summary>
        /// <param name="step">the step key under which the most probable value will be returned.</param>
        /// <returns></returns>
        public T GetMostProbableValue(int step)
        {
            int idx = -1;
            if (_probabilities.GetOrDefault(step, null) == null || (idx = GetMostProbableBucketIndex(step)) == -1)
            {
                return default(T);
            }
            return GetActualValue(idx);
        }

        /// <summary>
        /// Returns the bucket index corresponding with the highest probability for the specified step.
        /// </summary>
        /// <param name="step">the step key under which the most probable index will be returned.</param>
        /// <returns>-1 if there is no such entry</returns>
        public int GetMostProbableBucketIndex(int step)
        {
            if (_probabilities.GetOrDefault(step, null) == null) return -1;

            double max = 0;
            int bucketIdx = -1;
            int i = 0;
            foreach (double d in _probabilities[step])
            {
                if (d > max)
                {
                    max = d;
                    bucketIdx = i;
                }
                ++i;
            }
            return bucketIdx;
        }

        /// <summary>
        /// Returns the count of steps
        /// </summary>
        public int GetStepCount()
        {
            return _probabilities.Count;
        }

        /// <summary>
        /// Returns the count of probabilities for the specified step
        /// </summary>
        /// <param name="step">the step indexing the probability values</param>
        public int GetStatCount(int step)
        {
            return _probabilities[step].Length;
        }

        /// <summary>
        /// Returns a set of steps being recorded.
        /// </summary>
        public int[] StepSet()
        {
            return _probabilities.Keys.ToArray();
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + _actualValues.GetArrayHashCode();
            result = prime * result + (_probabilities?.GetArrayHashCode() ?? 0);
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

            Classification<T> other = (Classification<T>)obj;
            if (!_actualValues.SequenceEqual(other._actualValues))
                return false;
            if (_probabilities == null)
            {
                if (other._probabilities != null)
                    return false;
            }
            else if (!_probabilities.SequenceEqual(other._probabilities))
                return false;
            return true;
        }
    }
}