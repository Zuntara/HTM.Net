using System.Collections.Generic;

namespace HTM.Net.Model
{
    public class ProximalDendrite : Segment
    {
        private Pool _pool;

        /**
         * 
         * @param index     this {@code ProximalDendrite}'s index.
         */
        public ProximalDendrite(int index) : base(index)
        {

        }

        /// <summary>
        /// Creates and returns a newly created <see cref="Synapse"/> with the specified source cell, permanence, and index.
        /// IMPORTANT: 	
        /// For DistalDendrites, there is only one synapse per pool, so the
        /// synapse's index doesn't really matter (in terms of tracking its
        /// order within the pool. In that case, the index is a global counter of all distal dendrite synapses.
        /// 
        /// For ProximalDendrites, there are many synapses within a pool, and in
        /// that case, the index specifies the synapse's sequence order within
        /// the pool object, and may be referenced by that index.
        /// </summary>
        /// <param name="c">the connections state of the temporal memory</param>
        /// <param name="syns"></param>
        /// <param name="sourceCell">the source cell which will activate the new {@code Synapse}</param>
        /// <param name="pool">the new <see cref="Synapse"/>'s pool for bound variables.</param>
        /// <param name="index">the new <see cref="Synapse"/>'s index.</param>
        /// <param name="inputIndex">the index of this <see cref="Synapse"/>'s input (source object); be it a Cell or InputVector bit.</param>
        /// <returns>Created synapse</returns>
        public override Synapse CreateSynapse(Connections c, List<Synapse> syns, Cell sourceCell, Pool pool, int index, int inputIndex)
        {
            Synapse s = new Synapse(c, sourceCell, null, pool, index, inputIndex);
            syns.Add(s);
            return s;
        }


        /**
         * Creates the pool of {@link Synapse}s representing the connection
         * to the input vector.
         * 
         * @param c					the {@link Connections} memory
         * @param inputIndexes		indexes specifying the input vector bit
         */
        public Pool CreatePool(Connections c, int[] inputIndexes)
        {
            _pool = new Pool(inputIndexes.Length);

            for (int i = 0; i < inputIndexes.Length; i++)
            {
                int synCount = c.GetSynapseCount();
                _pool.SetPermanence(c, CreateSynapse(c, c.GetSynapses(this), null, _pool, synCount, inputIndexes[i]), 0);
                c.SetSynapseCount(synCount + 1);
            }
            return _pool;
        }

        public void ClearSynapses(Connections c)
        {
            c.GetSynapses(this).Clear();
        }

        /**
         * Sets the permanences for each {@link Synapse}. The number of synapses
         * is set by the potentialPct variable which determines the number of input
         * bits a given column will be "attached" to which is the same number as the
         * number of {@link Synapse}s
         * 
         * @param c			the {@link Connections} memory
         * @param perms		the floating point degree of connectedness
         */
        public void SetPermanences(Connections c, double[] perms)
        {
            _pool.ResetConnections();
            c.GetConnectedCounts().ClearStatistics(index);
            List<Synapse> synapses = c.GetSynapses(this);

            foreach(Synapse s in synapses)
            //Parallel.ForEach(synapses, s =>
            {
                int inputIndex = s.GetInputIndex();
                s.SetPermanence(c, perms[inputIndex]);
                if (perms[inputIndex] >= c.GetSynPermConnected())
                {
                    c.GetConnectedCounts().Set(1, index, inputIndex);
                }
                else
                {
                    c.GetConnectedCounts().Set(0, index, inputIndex);
                }
            }
            //);
        }

        /**
         * Sets the permanences for each {@link Synapse} specified by the indexes
         * passed in which identify the input vector indexes associated with the
         * {@code Synapse}. The permanences passed in are understood to be in "sparse"
         * format and therefore require the int array identify their corresponding
         * indexes.
         * 
         * Note: This is the "sparse" version of this method.
         * 
         * @param c			the {@link Connections} memory
         * @param perms		the floating point degree of connectedness
         */
        public void SetPermanences(Connections c, double[] perms, int[] inputIndexes)
        {
            _pool.ResetConnections();
            c.GetConnectedCounts().ClearStatistics(index);

            for (int i = 0; i < inputIndexes.Length; i++)
            {
                _pool.SetPermanence(c, _pool.GetSynapseWithInput(inputIndexes[i]), perms[i]);
                if (perms[i] >= c.GetSynPermConnected())
                {
                    c.GetConnectedCounts().Set(1, index, i);
                }
                else
                {
                    c.GetConnectedCounts().Set(0, index, i);
                }
            }
        }

        /**
         * Sets the input vector synapse indexes which are connected (&gt;= synPermConnected)
         * @param c
         * @param connectedIndexes
         */
        public void SetConnectedSynapsesForTest(Connections c, int[] connectedIndexes)
        {
            Pool pool = CreatePool(c, connectedIndexes);
            c.GetPotentialPools().Set(index, pool);
        }

        /**
         * Returns an array of synapse indexes as a dense binary array.
         * @param c
         * @return
         */
        public int[] GetConnectedSynapsesDense(Connections c)
        {
            return c.GetPotentialPools().Get(index).GetDenseConnections(c);
        }

        /**
         * Returns an sparse array of synapse indexes representing the connected bits.
         * @param c
         * @return
         */
        public int[] GetConnectedSynapsesSparse(Connections c)
        {
            return c.GetPotentialPools().Get(index).GetSparseConnections();
        }
    }
}