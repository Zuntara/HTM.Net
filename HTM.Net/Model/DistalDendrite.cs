using System;
using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Model
{
    public class DistalDendrite : Segment
    {
        private readonly Cell cell;

        /**
     * Constructs a new {@code Segment} object with the specified owner
     * <see cref="Cell"/> and the specified index.
     * 
     * @param cell      the owner
     * @param index     this {@code Segment}'s index.
     */
        public DistalDendrite(Cell cell, int index)
            : base(index)
        {
            this.cell = cell;
            this.index = index;
        }

        /**
         * Returns the owner <see cref="Cell"/>
         * 
         * @return
         */
        public Cell GetParentCell()
        {
            return cell;
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
            Synapse s = new Synapse(c, sourceCell, this, pool, index, inputIndex);
            syns.Add(s);
            return s;
        }

        /**
         * Creates and returns a newly created {@link Synapse} with the specified
         * source cell, permanence, and index.
         * 
         * @param c             the connections state of the temporal memory
         * @param sourceCell    the source cell which will activate the new {@code Synapse}
         * @param permanence    the new {@link Synapse}'s initial permanence.
         * @param index         the new {@link Synapse}'s index.
         * 
         * @return
         */
        public Synapse CreateSynapse(Connections c, Cell sourceCell, double permanence)
        {
            Pool pool = new Pool(1);
            Synapse s = CreateSynapse(c, c.GetSynapses(this), sourceCell, pool, c.IncrementSynapses(), sourceCell.GetIndex());
            pool.SetPermanence(c, s, permanence);
            return s;
        }

        /**
         * Returns all {@link Synapse}s
         * 
         * @param c     the connections state of the temporal memory
         * @return
         */
        public List<Synapse> GetAllSynapses(Connections c)
        {
            return c.GetSynapses(this);
        }

        /**
         * Returns the synapses on a segment that are active due to lateral input
         * from active cells.
         * 
         * @param c                 the layer connectivity
         * @param activeCells       the active cells
         * @return  Set of {@link Synapse}s connected to active presynaptic cells.
         */
        public HashSet<Synapse> GetActiveSynapses(Connections c, HashSet<Cell> activeCells)
        {
            HashSet<Synapse> synapses = new HashSet<Synapse>();

            //for (Synapse synapse : c.GetSynapses(this))
            foreach (Synapse synapse in c.GetSynapses(this))
            {
                if (activeCells.Contains(synapse.GetPresynapticCell()))
                {
                    synapses.Add(synapse);
                }
            }

            return synapses;
        }

        /**
         * Called for learning {@code Segment}s so that they may adjust the
         * permanences of their synapses.
         * 
         * @param c                         the connections state of the temporal memory
         * @param activeSynapses            a set of active synapses owned by this {@code Segment} which
         *                                  will have their permanences increased. All others will have
         *                                  their permanences decreased.
         * @param permanenceIncrement       the increment by which permanences are increased.
         * @param permanenceDecrement       the increment by which permanences are decreased.
         */
        public void AdaptSegment(Connections c, HashSet<Synapse> activeSynapses, double permanenceIncrement, double permanenceDecrement)
        {
            List<Synapse> synapsesToDestroy = null;

            //for (Synapse synapse : c.getSynapses(this))
            foreach (Synapse synapse in c.GetSynapses(this))
            {
                double permanence = synapse.GetPermanence();
                if (activeSynapses.Contains(synapse))
                {
                    permanence += permanenceIncrement;
                }
                else {
                    permanence -= permanenceDecrement;
                }

                permanence = permanence < 0 ? 0 : permanence > 1.0 ? 1.0 : permanence;

                if (Math.Abs(permanence) < double.Epsilon)
                {
                    if (synapsesToDestroy == null)
                    {
                        synapsesToDestroy = new List<Synapse>();
                    }
                    synapsesToDestroy.Add(synapse);
                }
                else {
                    synapse.SetPermanence(c, permanence);
                }
            }

            if (synapsesToDestroy != null)
            {
                //for (Synapse s : synapsesToDestroy)
                foreach (Synapse s in synapsesToDestroy)
                {
                    s.Destroy(c);
                }
            }
        }

        /**
         * Returns a {@link Set} of previous winner <see cref="Cell"/>s which aren't
         * already attached to any {@link Synapse}s owned by this {@code Segment}
         * 
         * @param c                 the connections state of the temporal memory
         * @param numPickCells      the number of possible cells this segment may designate
         * @param prevWinners       the set of previous winner cells
         * @param random            the random number generator
         * @return a {@link Set} of previous winner <see cref="Cell"/>s which aren't
         *         already attached to any {@link Synapse}s owned by this
         *         {@code Segment}
         */
        public HashSet<Cell> PickCellsToLearnOn(Connections c, int numPickCells, HashSet<Cell> prevWinners, IRandom random)
        {
            // Remove cells that are already synapsed on by this segment
            List<Cell> candidates = new List<Cell>(prevWinners);
            //for (Synapse synapse : c.getSynapses(this))
            foreach (Synapse synapse in c.GetSynapses(this))
            {
                Cell sourceCell = synapse.GetPresynapticCell();
                if (candidates.Contains(sourceCell))
                {
                    candidates.Remove(sourceCell);
                }
            }

            numPickCells = Math.Min(numPickCells, candidates.Count);
            List<Cell> cands = new List<Cell>(candidates);
            cands.Sort();

            HashSet<Cell> cells = new HashSet<Cell>();
            for (int x = 0; x < numPickCells; x++)
            {
                int i = random.NextInt(cands.Count);
                var randomCell = cands[i];
                cands.Remove(randomCell);
                cells.Add(randomCell);
            }

            return cells;
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return index.ToString();
        }

        /* (non-Javadoc)
         * @see java.lang.Object#hashCode()
         */
        public override int GetHashCode()
        {
            const int prime = 31;
            int result = base.GetHashCode();
            result = prime * result + ((cell == null) ? 0 : cell.GetHashCode());
            return result;
        }

        /* (non-Javadoc)
         * @see java.lang.Object#equals(java.lang.Object)
         */
        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (!base.Equals(obj))
                return false;
            if (GetType() != obj.GetType())
                return false;
            DistalDendrite other = (DistalDendrite)obj;
            if (cell == null)
            {
                if (other.cell != null)
                    return false;
            }
            else if (!cell.Equals(other.cell))
                return false;
            return true;
        }
    }

    
}