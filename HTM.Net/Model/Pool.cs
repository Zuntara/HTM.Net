using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;

namespace HTM.Net.Model
{
    /// <summary>
    /// Convenience container for "bound" {@link Synapse} values
    /// which can be dereferenced from both a Synapse and the {@link Connections} object. All Synapses will have a reference 
    /// to a {@code Pool} to retrieve relevant values.In addition, that same pool can be referenced from the Connections object externally 
    /// which will update the Synapse's internal reference.
    /// </summary>
    [Serializable]
    public class Pool
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;
        private readonly int _size;

        /** Allows fast removal of connected synapse indexes. */
        private HashSet<int> _synapseConnections = new HashSet<int>();
        /** 
         * Indexed according to the source Input Vector Bit (for ProximalDendrites),
         * and source cell (for DistalDendrites).
         */
        private Map<int, Synapse> _synapsesBySourceIndex = new Map<int, Synapse>();

        public Pool(int size)
        {
            _size = size;
        }

        /**
         * Returns the permanence value for the {@link Synapse} specified.
         * 
         * @param s	the Synapse
         * @return	the permanence
         */
        public double GetPermanence(Synapse s)
        {
            return _synapsesBySourceIndex.Get(s.GetInputIndex()).GetPermanence();
        }

        /**
         * Sets the specified  permanence value for the specified {@link Synapse}
         * @param s
         * @param permanence
         */
        public void SetPermanence(Connections c, Synapse s, double permanence)
        {
            s.SetPermanence(c, permanence);
        }

        /**
         * Updates this {@code Pool}'s store of permanences for the specified {@link Synapse}
         * @param c				the connections memory
         * @param s				the synapse who's permanence is recorded
         * @param permanence	the permanence value to record
         */
        public void UpdatePool(Connections c, Synapse s, double permanence)
        {
            int inputIndex = s.GetInputIndex();
            if (_synapsesBySourceIndex.Get(inputIndex) == null)
            {
                _synapsesBySourceIndex[inputIndex] = s;
            }
            if (permanence >= c.GetSynPermConnected())
            {
                _synapseConnections.Add(inputIndex);
            }
            else
            {
                _synapseConnections.Remove(inputIndex);
            }
        }

        /**
         * Resets the current connections in preparation for new permanence
         * adjustments.
         */
        public void ResetConnections()
        {
            _synapseConnections.Clear();
        }

        /**
         * Returns the {@link Synapse} connected to the specified input bit
         * index.
         * 
         * @param inputIndex	the input vector connection's index.
         * @return
         */
        public Synapse GetSynapseWithInput(int inputIndex)
        {
            return _synapsesBySourceIndex.Get(inputIndex);
        }

        /**
         * Returns an array of permanence values
         * @return
         */
        public double[] GetSparsePermanences()
        {
            double[] retVal = new double[_size];
            var keys = _synapsesBySourceIndex.Keys;
            for (int x = 0, j = _size - 1; x < _size; x++, j--)
            {
                retVal[j] = _synapsesBySourceIndex[keys.ElementAt(x)].GetPermanence();
            }
            return retVal;
        }

        /// <summary>
        /// Returns a dense array representing the potential pool permanences
        /// </summary>
        /// <param name="c">Connections to use</param>
        public double[] GetDensePermanences(Connections c)
        {
            double[] retVal = new double[c.GetNumInputs()];
            var keys = _synapsesBySourceIndex.Keys;
            //for (int inputIndex : keys)
            foreach (int inputIndex in keys)
            {
                retVal[inputIndex] = _synapsesBySourceIndex[inputIndex].GetPermanence();
            }
            return retVal;
        }

        /**
     * Returns an array of input bit indexes indicating the index of the source. 
     * (input vector bit or lateral cell)
     * @return the sparse array
     */
        public int[] GetSparsePotential()
        {
            return ArrayUtils.Reverse(_synapsesBySourceIndex.Keys.ToArray());
        }

        /**
         * Returns a dense binary array containing 1's where the input bits are part
         * of this pool.
         * @param c     the {@link Connections}
         * @return  dense binary array of member inputs
         */
        public int[] GetDensePotential(Connections c)
        {
            return ArrayUtils.Range(0, c.GetNumInputs())
                .Select(i=>_synapsesBySourceIndex.ContainsKey(i) ? 1 : 0)
                .ToArray();
        }

        /**
         * Returns an binary array whose length is equal to the number of inputs;
         * and where 1's are set in the indexes of this pool's assigned bits.
         * 
         * @param   c   {@link Connections}
         * @return the sparse array
         */
        public int[] GetDenseConnected(Connections c)
        {
            return ArrayUtils.Range(0, c.GetNumInputs())
                .Select(i=>_synapseConnections.Contains(i) ? 1 : 0)
                .ToArray();
        }

        /**
         * Destroys any references this {@code Pool} maintains on behalf
         * of the specified {@link Synapse}
         * 
         * @param synapse
         */
        public void DestroySynapse(Synapse synapse)
        {
            _synapseConnections.Remove(synapse.GetInputIndex());
            _synapsesBySourceIndex.Remove(synapse.GetInputIndex());
            if (synapse.GetSegment() is DistalDendrite) {
                Destroy();
            }
        }

        /**
         * Clears the state of this {@code Pool}
         */
        public void Destroy()
        {
            _synapseConnections.Clear();
            _synapsesBySourceIndex.Clear();
            _synapseConnections = null;
            _synapsesBySourceIndex = null;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + _size;
            result = prime * result + ((_synapseConnections == null) ? 0 : _synapseConnections.ToString().GetHashCode());
            result = prime * result + ((_synapsesBySourceIndex == null) ? 0 : _synapsesBySourceIndex.ToString().GetHashCode());
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
            Pool other = (Pool)obj;
            if (_size != other._size)
                return false;
            if (_synapseConnections == null)
            {
                if (other._synapseConnections != null)
                    return false;
            }
            else if ((!_synapseConnections.SetEquals(other._synapseConnections) ||
              !other._synapseConnections.SetEquals(_synapseConnections)))
                return false;
            if (_synapsesBySourceIndex == null)
            {
                if (other._synapsesBySourceIndex != null)
                    return false;
            }
            else if (!_synapsesBySourceIndex.ToString().Equals(other._synapsesBySourceIndex.ToString()))
                return false;
            return true;
        }
    }
}