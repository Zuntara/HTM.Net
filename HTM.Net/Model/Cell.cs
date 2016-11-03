using System;
using System.Collections.Generic;

namespace HTM.Net.Model
{
    [Serializable]
    public class Cell : IComparable<Cell>
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

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
     * Returns the Set of {@link Synapse}s which have this cell
     * as their source cells.
     *  
     * @param   c               the connections state of the temporal memory
     *                          return an orphaned empty set.
     * @return  the Set of {@link Synapse}s which have this cell
     *          as their source cells.
     */
        public HashSet<Synapse> GetReceptorSynapses(Connections c)
        {
            return GetReceptorSynapses(c, false);
        }

        /**
         * Returns the Set of {@link Synapse}s which have this cell
         * as their source cells.
         *  
         * @param   c               the connections state of the temporal memory
         * @param doLazyCreate      create a container for future use if true, if false
         *                          return an orphaned empty set.
         * @return  the Set of {@link Synapse}s which have this cell
         *          as their source cells.
         */
        public HashSet<Synapse> GetReceptorSynapses(Connections c, bool doLazyCreate)
        {
            return c.GetReceptorSynapses(this, doLazyCreate);
        }

        /**
         * Returns a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         * 
         * @param   c               the connections state of the temporal memory
         * @param doLazyCreate      create a container for future use if true, if false
         *                          return an orphaned empty set.
         * @return  a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         */
        public List<DistalDendrite> GetSegments(Connections c)
        {
            return GetSegments(c, false);
        }

        /**
         * Returns a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         * 
         * @param   c               the connections state of the temporal memory
         * @param doLazyCreate      create a container for future use if true, if false
         *                          return an orphaned empty set.
         * @return  a {@link List} of this {@code Cell}'s {@link DistalDendrite}s
         */
        public List<DistalDendrite> GetSegments(Connections c, bool doLazyCreate)
        {
            return c.GetSegments(this, doLazyCreate);
        }

        public override string ToString()
        {
            return index.ToString();
        }

        /**
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