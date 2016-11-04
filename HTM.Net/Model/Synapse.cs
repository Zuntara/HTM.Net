using System;
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
     * @see DistalDendrite
     * @see Connections
     */
    [Serializable]
    public class Synapse : Persistable, IComparable<Synapse>
    {
        private readonly Cell _sourceCell;
        private readonly Segment _segment;
        private readonly Pool _pool;
        private readonly int _synapseIndex;
        private readonly int _inputIndex;
        private double _permanence;
        private bool _destroyed;

        /// <summary>
        /// Constructor used when setting parameters later.
        /// </summary>
        public Synapse() { }


        /// <summary>
        /// Constructs a new <see cref="Synapse"/> for a <see cref="DistalDendrite"/>
        /// </summary>
        /// <param name="presynapticCell">the <see cref="Cell"/> which will activate this <see cref="Synapse"/>;
        /// null if this Synapse is proximal</param>
        /// <param name="segment">the owning dendritic segment</param>
        /// <param name="index">The <see cref="Synapse"/>'s index</param>
        /// <param name="permanence"></param>
        public Synapse(Cell presynapticCell, Segment segment, int index, double permanence)
        {
            _sourceCell = presynapticCell;
            _segment = segment;
            _synapseIndex = index;
            _inputIndex = presynapticCell.GetIndex();
            _permanence = permanence;
        }


        /// <summary>
        /// Constructs a new <see cref="Synapse"/>
        /// </summary>
        /// <param name="c">the connections state of the temporal memory</param>
        /// <param name="sourceCell">the <see cref="Cell"/> which will activate this <see cref="Synapse"/>
        /// Null if this Synapse is proximal</param>
        /// <param name="segment">the owning dendritic segment</param>
        /// <param name="pool"> this {@link Pool} of which this synapse is a member</param>
        /// <param name="index">this {@code Synapse}'s index</param>
        /// <param name="inputIndex">the index of this {@link Synapse}'s input; be it a Cell or InputVector bit.</param>
        public Synapse(Connections c, Cell sourceCell, Segment segment, Pool pool, int index, int inputIndex)
        {
            _sourceCell = sourceCell;
            _segment = segment;
            _pool = pool;
            _synapseIndex = index;
            _inputIndex = inputIndex;
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
        public Segment GetSegment()
        {
            return _segment;
        }

        /// <summary>
        /// Returns the owning dendritic segment
        /// </summary>
        /// <returns></returns>
        public TSegment GetSegment<TSegment>()
            where TSegment : Segment
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

        /**
         * Returns the flag indicating whether this segment has been destroyed.
         * @return  the flag indicating whether this segment has been destroyed.
         */
        public bool IsDestroyed()
        {
            return _destroyed;
        }

        /**
         * Sets the flag indicating whether this segment has been destroyed.
         * @param b the flag indicating whether this segment has been destroyed.
         */
        public void SetDestroyed(bool b)
        {
            _destroyed = b;
        }

        ///// <summary>
        ///// Removes the references to this Synapse in its associated
        ///// <see cref="Pool"/> and its upstream presynapticCell's reference.
        ///// </summary>
        ///// <param name="c"></param>
        //public void Destroy(Connections c)
        //{
        //    _pool.DestroySynapse(this);
        //    if (_sourceCell != null)
        //    {
        //        c.GetSynapses((DistalDendrite)_segment).Remove(this);
        //        _sourceCell.RemoveReceptorSynapse(c, this);
        //    }
        //    else
        //    {
        //        c.GetSynapses((ProximalDendrite)_segment).Remove(this);
        //    }
        //}

        public int CompareTo(Synapse other)
        {
            return _synapseIndex.CompareTo(other._synapseIndex);
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

        #region Overrides of Object

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + _inputIndex;
            result = prime * result + (_segment?.GetHashCode() ?? 0);
            result = prime * result + (_sourceCell?.GetHashCode() ?? 0);
            result = prime * result + _synapseIndex;
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
            Synapse other = (Synapse)obj;
            if (_inputIndex != other._inputIndex)
                return false;
            if (_segment == null)
            {
                if (other._segment != null)
                    return false;
            }
            else if (!_segment.Equals(other._segment))
                return false;
            if (_sourceCell == null)
            {
                if (other._sourceCell != null)
                    return false;
            }
            else if (!_sourceCell.Equals(other._sourceCell))
                return false;
            if (_synapseIndex != other._synapseIndex)
                return false;
            return true;
        }

        #endregion
    }
}