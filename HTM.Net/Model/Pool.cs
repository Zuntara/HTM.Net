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
    public class Pool
    {
        private readonly int _size;

        /** Allows fast removal of connected synapse indexes. */
        private List<int> _synapseConnections = new List<int>();
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
            lock (_synapsesBySourceIndex)
            {
                return _synapsesBySourceIndex[s.GetInputIndex()].GetPermanence();
            }
        }

        /**
         * Sets the specified  permanence value for the specified {@link Synapse}
         * @param s
         * @param permanence
         */
        public void SetPermanence(Connections c, Synapse s, double permanence)
        {
            UpdatePool(c, s, permanence);
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

            //Synapse foundSynapse;
            lock (_synapsesBySourceIndex)
            {
                if (!_synapsesBySourceIndex.ContainsKey(inputIndex)) // TryGetValue(inputIndex, out foundSynapse)
                {
                    _synapsesBySourceIndex.Add(inputIndex, s);
                }
            }
            lock (_synapseConnections)
            {
                if (permanence > c.GetSynPermConnected())
                {
                    _synapseConnections.Add(inputIndex);
                }
                else
                {
                    if (_synapseConnections.Count > inputIndex)
                        _synapseConnections.RemoveAt(inputIndex);
                }
            }
        }

        /**
         * Resets the current connections in preparation for new permanence
         * adjustments.
         */
        public void ResetConnections()
        {
            lock (_synapseConnections)
            {
                _synapseConnections.Clear();
            }
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
            lock (_synapsesBySourceIndex)
            {
                return _synapsesBySourceIndex[inputIndex];
            }
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
        public int[] GetSparseConnections()
        {
            int[] keys = ArrayUtils.Reverse(_synapsesBySourceIndex.Keys.ToArray());
            return keys;
        }

        /**
         * Returns a dense array representing the potential pool bits
         * with the connected bits set to 1. 
         * 
         * Note: Only called from tests for now...
         * @param c
         * @return
         */
        public int[] GetDenseConnections(Connections c)
        {
            int[] retVal = new int[c.GetNumInputs()];
            //for (int inputIndex : synapseConnections.ToArray())
            foreach (int inputIndex in _synapseConnections)
            {
                retVal[inputIndex] = 1;
            }
            return retVal;
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
            if (synapse.GetSegment<DistalDendrite>() != null)
            {
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
    }
}