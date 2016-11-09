using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Util;

namespace HTM.Net.Model
{
    [Serializable]
    public class Column : Persistable, IComparable<Column>
    {
        /// <summary>
        /// The flat non-topological index of this column
        /// </summary>
        private readonly int _index;
        /// <summary>
        /// Configuration of cell count
        /// </summary>
        private readonly int _numCells;
        /// <summary>
        /// Connects <see cref="SpatialPooler"/> input pools
        /// </summary>
        private readonly ProximalDendrite _proximalDendrite;

        private readonly Cell[] _cellList;

        private readonly int _hashcode;

        /**
        * Constructs a new {@code Column}
        * 
        * @param numCells      number of cells per column
        * @param index         the index of this column
        */
        public Column(int numCells, int index)
        {
            _numCells = numCells;
            _index = index;
            // ReSharper disable once DoNotCallOverridableMethodsInConstructor
            _hashcode = GetHashCode();
            _cellList = new Cell[numCells];
            for (int i = 0; i < numCells; i++)
            {
                _cellList[i] = new Cell(this, i);
            }
            _proximalDendrite = new ProximalDendrite(index);
        }

        /**
         * Returns the <see cref="Cell"/> residing at the specified index.
         * 
         * @param index     the index of the <see cref="Cell"/> to return.
         * @return          the <see cref="Cell"/> residing at the specified index.
         */
        public Cell GetCell(int index)
        {
            return _cellList[index];
        }

        /**
         * Returns a {@link List} view of this {@code Column}'s <see cref="Cell"/>s.
         * @return
         */
        public IList<Cell> GetCells()
        {
            return Array.AsReadOnly(_cellList);
        }

        /// <summary>
        /// Returns the index of this <see cref="Column"/>
        /// </summary>
        public int GetIndex()
        {
            return _index;
        }

        /// <summary>
        /// Returns the configured number of cells per column for
        /// all <see cref="Column"/> objects within the current <see cref="TemporalMemory"/>
        /// </summary>
        /// <returns></returns>
        public int GetNumCellsPerColumn()
        {
            return _numCells;
        }

        /**
         * Returns the <see cref="Cell"/> with the least number of {@link DistalDendrite}s.
         * 
         * @param c         the connections state of the temporal memory
         * @param random
         * @return
         */
        public Cell GetLeastUsedCell(Connections c, IRandom random)
        {
            List<Cell> leastUsedCells = new List<Cell>();
            int minNumSegments = int.MaxValue;
            
            foreach (Cell cell in _cellList)
            {
                int numSegments = cell.GetSegments(c).Count;

                if (numSegments < minNumSegments)
                {
                    minNumSegments = numSegments;
                    leastUsedCells.Clear();
                }

                if (numSegments == minNumSegments)
                {
                    leastUsedCells.Add(cell);
                }
            }
            int index = random.NextInt(leastUsedCells.Count);
            leastUsedCells.Sort();
            return leastUsedCells[index];
        }

        /**
         * Returns this {@code Column}'s single {@link ProximalDendrite}
         * @return
         */
        public ProximalDendrite GetProximalDendrite()
        {
            return _proximalDendrite;
        }

        /**
         * Delegates the potential synapse creation to the one {@link ProximalDendrite}.
         * 
         * @param c						the {@link Connections} memory
         * @param inputVectorIndexes	indexes specifying the input vector bit
         */
        public Pool CreatePotentialPool(Connections c, int[] inputVectorIndexes)
        {
            return _proximalDendrite.CreatePool(c, inputVectorIndexes);
        }

        /**
         * Sets the permanences on the {@link ProximalDendrite} {@link Synapse}s
         * 
         * @param c				the {@link Connections} memory object
         * @param permanences	floating point degree of connectedness
         */
        public void SetProximalPermanences(Connections c, double[] permanences)
        {
            _proximalDendrite.SetPermanences(c, permanences);
        }

        /**
         * Sets the permanences on the {@link ProximalDendrite} {@link Synapse}s
         * 
         * @param c				the {@link Connections} memory object
         * @param permanences	floating point degree of connectedness
         */
        public void SetProximalPermanencesSparse(Connections c, double[] permanences, int[] indexes)
        {
            _proximalDendrite.SetPermanences(c, permanences, indexes);
        }

        /**
         * Delegates the call to set synapse connected indexes to this 
         * {@code Column}'s {@link ProximalDendrite}
         * @param c
         * @param connections
         */
        public void SetProximalConnectedSynapsesForTest(Connections c, int[] connections)
        {
            _proximalDendrite.SetConnectedSynapsesForTest(c, connections);
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
            return _index.ToString();
        }

        /// <summary>
        /// Compares the current object with another object of the same type.
        /// </summary>
        /// <returns>
        /// A value that indicates the relative order of the objects being compared. The return value has the following meanings: Value Meaning Less than zero This object is less than the <paramref name="other"/> parameter.Zero This object is equal to <paramref name="other"/>. Greater than zero This object is greater than <paramref name="other"/>. 
        /// </returns>
        /// <param name="other">An object to compare with this object.</param>
        public int CompareTo(Column other)
        {
            return _index.CompareTo(other._index);
        }

        /// <summary>
        /// Serves as the default hash function. 
        /// </summary>
        /// <returns>
        /// A hash code for the current object.
        /// </returns>
        /// <filterpriority>2</filterpriority>
        public override int GetHashCode()
        {
            if (_hashcode == 0)
            {
                const int prime = 31;
                int result = 1;
                result = prime * result + _index;
                return result;
            }
            return _hashcode;
        }

        /// <summary>
        /// Determines whether the specified object is equal to the current object.
        /// </summary>
        /// <returns>
        /// true if the specified object  is equal to the current object; otherwise, false.
        /// </returns>
        /// <param name="obj">The object to compare with the current object. </param><filterpriority>2</filterpriority>
        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Column other = (Column)obj;
            if (_index != other._index)
                return false;
            return true;
        }
    }
}