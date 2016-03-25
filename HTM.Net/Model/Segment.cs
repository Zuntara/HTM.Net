using System;
using System.Collections.Generic;

namespace HTM.Net.Model
{
    public abstract class Segment : IComparable<Segment>
    {
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
        public virtual Synapse CreateSynapse(Connections c, List<Synapse> syns, Cell sourceCell, Pool pool, int index, int inputIndex)
        {
            throw new NotImplementedException("Must be implemented by derived class!");
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