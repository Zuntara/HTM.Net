using System;
using System.Collections.Generic;

namespace HTM.Net.Model
{
    public class Cell : IComparable<Cell>
    {
        /** This cell's index */
        private readonly int index;
        /** The owning <see cref="Column"/> */
        private readonly Column column;
        /** Cache this because Cells are immutable */
        private readonly int hashcode;

        /**
        * Constructs a new {@code Cell} object
        * @param column    the containing <see cref="Column"/>
        * @param colSeq    this index of this {@code Cell} within its column
        */
        public Cell(Column column, int colSeq)
        {
            this.column = column;
            this.index = column.GetIndex() * column.GetNumCellsPerColumn() + colSeq;
            this.hashcode = GetHashCode();
        }

        /**
         * Returns this {@code Cell}'s index.
         * @return
         */
        public int GetIndex()
        {
            return index;
        }

        /**
         * Returns the column within which this cell resides
         * @return
         */
        public Column GetColumn()
        {
            return column;
        }

        /**
         * Adds a {@link Synapse} which is the receiver of signals
         * from this {@code Cell}
         * 
         * @param c     the connections state of the temporal memory
         * @param s     the Synapse to add
         */
        public void AddReceptorSynapse(Connections c, Synapse s)
        {
            c.GetReceptorSynapses(this).Add(s);
        }

        /**
         * Removes a {@link Synapse} which is no longer a receiver of
         * signals from this {@code Cell}
         * 
         * @param c     the connections state of the temporal memory
         * @param s     the Synapse to remove
         */
        public void RemoveReceptorSynapse(Connections c, Synapse s)
        {
            c.GetReceptorSynapses(this).Remove(s);
            c.DecrementSynapses();
        }

        /**
         * Returns the Set of {@link Synapse}s which have this cell
         * as their source cells.
         *  
         * @param   c       the connections state of the temporal memory
         * @return  the Set of {@link Synapse}s which have this cell
         *          as their source cells.
         */
        public List<Synapse> GetReceptorSynapses(Connections c)
        {
            return c.GetReceptorSynapses(this);
        }

        /**
         * Returns a newly created {@link DistalDendrite}
         * 
         * @param   c       the connections state of the temporal memory
         * @return          a newly created {@link DistalDendrite}
         */
        public DistalDendrite CreateSegment(Connections c)
        {
            DistalDendrite dd = new DistalDendrite(this, (int) c.IncrementSegments());
            c.GetSegments(this).Add(dd);

            return dd;
        }

        /**
         * Returns a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         * 
         * @param   c   the connections state of the temporal memory
         * @return  a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         */
        public List<DistalDendrite> GetSegments(Connections c)
        {
            return c.GetSegments(this);
        }

        /**
         * {@inheritDoc}
         */
        public override string ToString()
        {
            return index.ToString();
        }

        /**
         * {@inheritDoc}
         * 
         * <em> Note: All comparisons use the cell's index only </em>
         */
        public int CompareTo(Cell arg0)
        {
            return index.CompareTo(arg0.index);
        }

        public override int GetHashCode()
        {
            if (hashcode == 0)
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + index;
                return result;
            }
            return hashcode;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Cell other = (Cell)obj;
            if (index != other.index)
                return false;
            return true;
        }
    }
}