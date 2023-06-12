using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Single;
using Vector = MathNet.Numerics.LinearAlgebra.Double.Vector;

namespace HTM.Net.Model
{
    [Serializable]
    public class ProximalDendrite : Segment
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

        private Pool _pool;

        /**
         * 
         * @param index     this {@code ProximalDendrite}'s index.
         */
        public ProximalDendrite(int index)
            : base(index)
        {

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
                int synCount = c.GetProximalSynapseCount();
                _pool.SetPermanence(c, CreateSynapse(c, c.GetSynapses(this), null, _pool, synCount, inputIndexes[i]), 0);
                c.SetProximalSynapseCount(synCount + 1);
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
            //_pool.ResetConnections();
            //// Lock connections for the matrix, it's not thread safe
            //c.GetConnectedCounts().ClearStatistics(index);
            //List<Synapse> synapses = c.GetSynapses(this);

            //double synPermConnected = c.GetSynPermConnected();

            //float[] row = new float[perms.Length];
            //Parallel.ForEach(synapses, s =>
            ////foreach (Synapse s in synapses)
            //{
            //    int inputIndex = s.GetInputIndex();
            //    s.SetPermanence(c, perms[inputIndex]);
            //    if (perms[inputIndex] >= synPermConnected)
            //    {
            //        row[inputIndex] = 1;
            //        //c.GetConnectedCounts().At(index, inputIndex, 1.0);
            //    }
            //});

            //// Lock connections for the matrix, it's not thread safe
            //c.GetConnectedCounts().SetRow(index, row);

            _pool.ResetConnections();
            c.GetConnectedCounts().ClearStatistics(index);
            List<Synapse> synapses = c.GetSynapses(this);

            if (synapses.Count > 1000)
            {
                // Run in parallel
                var connectedCounts = c.GetConnectedCounts();
                var row = connectedCounts.Row(index).ToArray();
                Parallel.ForEach(synapses, s =>
                {
                    int inputIndex = s.GetInputIndex();
                    s.SetPermanence(c, perms[inputIndex]);
                    if (perms[inputIndex] >= c.GetSynPermConnected())
                    {
                        //c.GetConnectedCounts().Set(1, index, s.GetInputIndex());
                        //connectedCounts[index, s.GetInputIndex()] = 1;
                        row[inputIndex] = 1;
                    }
                });
                connectedCounts.SetRow(index, row);
            }
            else
            {
                foreach (Synapse s in synapses)
                {
                    s.SetPermanence(c, perms[s.GetInputIndex()]);
                    if (perms[s.GetInputIndex()] >= c.GetSynPermConnected())
                    {
                        //c.GetConnectedCounts().Set(1, index, s.GetInputIndex());
                        c.GetConnectedCounts()[index, s.GetInputIndex()] = 1;
                    }
                }
            }
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
                    c.GetConnectedCounts()[index, i] = 1;
                    //c.GetConnectedCounts().Set(1, index, i);
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
            return c.GetPotentialPools().Get(index).GetDenseConnected(c);
        }

        /**
         * Returns an sparse array of synapse indexes representing the connected bits.
         * @param c
         * @return
         */
        public int[] GetConnectedSynapsesSparse(Connections c)
        {
            return c.GetPotentialPools().Get(index).GetSparsePotential();
        }
    }
}