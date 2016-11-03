using System;
using System.Collections.Generic;

namespace HTM.Net.Model
{
    [Serializable]
    public abstract class Segment : IComparable<Segment>
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

        protected int index;

        protected Segment(int index)
        {
            this.index = index;
        }

        /// <summary>
        /// Returns this <see cref="ProximalDendrite"/>'s index.
        /// </summary>
        /// <returns></returns>
        public int GetIndex()
        {
            return index;
        }

        /**
        * <p>
        * Creates and returns a newly created {@link Synapse} with the specified
        * source cell, permanence, and index.
        * </p><p>
        * IMPORTANT: 	<b>This method is only called for Proximal Synapses.</b> For ProximalDendrites, 
        * 				there are many synapses within a pool, and in that case, the index 
        * 				specifies the synapse's sequence order within the pool object, and may 
        * 				be referenced by that index.
        * </p>
        * @param c             the connections state of the temporal memory
        * @param sourceCell    the source cell which will activate the new {@code Synapse}
        * @param pool		    the new {@link Synapse}'s pool for bound variables.
        * @param index         the new {@link Synapse}'s index.
        * @param inputIndex	the index of this {@link Synapse}'s input (source object); be it a Cell or InputVector bit.
        * 
        * @return the newly created {@code Synapse}
        * @see Connections#createSynapse(DistalDendrite, Cell, double)
        */
        public virtual Synapse CreateSynapse(Connections c, List<Synapse> syns, Cell sourceCell, Pool pool, int index, int inputIndex)
        {
            Synapse s = new Synapse(c, sourceCell, this, pool, index, inputIndex);
            syns.Add(s);
            return s;
        }


        /// <summary>
        /// Compares the current object with another object of the same type.
        /// Note: All comparisons use the segment's index only 
        /// </summary>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(Segment other)
        {
            return index.CompareTo(other.index);
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + index;
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
            Segment other = (Segment)obj;
            if (index != other.index)
                return false;
            return true;
        }

    }
}