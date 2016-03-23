using System.Text;

namespace HTM.Net.Model
{
    /**
     * Represents a connection with varying strength which when above 
     * a configured threshold represents a valid connection. 
     * 
     * IMPORTANT: 	For DistalDendrites, there is only one synapse per pool, so the
     * 				synapse's index doesn't really matter (in terms of tracking its
     * 				order within the pool). In that case, the index is a global counter
     * 				of all distal dendrite synapses.
     * 
     * 				For ProximalDendrites, there are many synapses within a pool, and in
     * 				that case, the index specifies the synapse's sequence order within
     * 				the pool object, and may be referenced by that index.
     *    
     * 
     * @author Chetan Surpur
     * @author David Ray
     * 
     * @see DistalDendrite
     * @see Connections
     */
    public class Synapse
    {
        private readonly Cell _sourceCell;
        private readonly Segment _segment;
        private readonly Pool _pool;
        private readonly int _synapseIndex;
        private readonly int _inputIndex;
        private double _permanence;


        /// <summary>
        /// Constructor used when setting parameters later.
        /// </summary>
        public Synapse() { }

        /**
         * Constructs a new {@code Synapse}
         * 
         * @param c             the connections state of the temporal memory
         * @param sourceCell    the {@link Cell} which will activate this {@code Synapse};
         *                      Null if this Synapse is proximal
         * @param segment       the owning dendritic segment
         * @param pool		    this {@link Pool} of which this synapse is a member
         * @param index         this {@code Synapse}'s index
         * @param inputIndex	the index of this {@link Synapse}'s input; be it a Cell or InputVector bit.
         */

        /// <summary>
        /// Constructs a new <see cref="Synapse"/>
        /// </summary>
        /// <param name="c">the connections state of the temporal memory</param>
        /// <param name="sourceCell">the <see cref="Cell"/> which will activate this <see cref="Synapse"/>;
        /// null if this Synapse is proximal</param>
        /// <param name="segment">the owning dendritic segment</param>
        /// <param name="pool"></param>
        /// <param name="index"></param>
        /// <param name="inputIndex"></param>
        public Synapse(Connections c, Cell sourceCell, DistalDendrite segment, Pool pool, int index, int inputIndex)
        {
            _sourceCell = sourceCell;
            _segment = segment;
            _pool = pool;
            _synapseIndex = index;
            _inputIndex = inputIndex;

            // If this isn't a synapse on a proximal dendrite
            sourceCell?.AddReceptorSynapse(c, this);
        }

        /// <summary>
        /// Returns this <see cref="Synapse"/>'s index.
        /// </summary>
        /// <returns></returns>
        public int GetIndex()
        {
            return _synapseIndex;
        }

        /// <summary>
        /// Returns the index of this <see cref="Synapse"/>'s input item 
        /// whether it is a "sourceCell" or inputVector bit.
        /// </summary>
        /// <returns></returns>
        public int GetInputIndex()
        {
            return _inputIndex;
        }

        /// <summary>
        /// Returns this <see cref="Synapse"/>'s degree of connectedness.
        /// </summary>
        /// <returns></returns>
        public double GetPermanence()
        {
            return _permanence;
        }

        /// <summary>
        /// Sets this <see cref="Synapse"/>'s degree of connectedness.
        /// </summary>
        /// <param name="c"></param>
        /// <param name="perm"></param>
        public void SetPermanence(Connections c, double perm)
        {
            _permanence = perm;
            if (_sourceCell == null)
            {
                _pool.UpdatePool(c, this, perm);
            }
        }

        /// <summary>
        /// Returns the owning dendritic segment
        /// </summary>
        /// <returns></returns>
        public TSegment GetSegment<TSegment>()
            where TSegment: Segment
        {
            return _segment as TSegment;
        }

        /// <summary>
        /// Returns the containing <see cref="Cell"/>
        /// </summary>
        /// <returns></returns>
        public Cell GetPresynapticCell()
        {
            return _sourceCell;
        }

        /// <summary>
        /// Removes the references to this Synapse in its associated
        /// <see cref="Pool"/> and its upstream presynapticCell's reference.
        /// </summary>
        /// <param name="c"></param>
        public void Destroy(Connections c)
        {
            _pool.DestroySynapse(this);
            if (_sourceCell != null)
            {
                c.GetSynapses((DistalDendrite)_segment).Remove(this);
                _sourceCell.RemoveReceptorSynapse(c, this);
            }
            else {
                c.GetSynapses((ProximalDendrite)_segment).Remove(this);
            }
        }

        /// <summary>
        /// Returns a string that represents the current object.
        /// </summary>
        /// <returns>
        /// A string that represents the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override string ToString()
        {
            StringBuilder sb = new StringBuilder("synapse: [ synIdx=").Append(_synapseIndex).Append(", inIdx=")
                .Append(_inputIndex).Append(", sgmtIdx=").Append(_segment.GetIndex());
            if (_sourceCell != null)
            {
                sb.Append(", srcCellIdx=").Append(_sourceCell.GetIndex());
            }
            sb.Append(" ]");
            return sb.ToString();
        }
    }
}