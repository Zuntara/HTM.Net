using System.Linq;

namespace HTM.Net.Util
{
    /**
 * Allows storage of array data in sparse form, meaning that the indexes
 * of the data stored are maintained while empty indexes are not. This allows
 * savings in memory and computational efficiency because iterative algorithms
 * need only query indexes containing valid data.
 * 
 * @param <T>
 */
    public class SparseObjectMatrix<T> : AbstractSparseMatrix<T>
    {
        private Map<int, T> sparseMap = new Map<int, T>();

        /**
         * Constructs a new {@code SparseObjectMatrix}
         * @param dimensions	the dimensions of this array
         */
        public SparseObjectMatrix(int[] dimensions)
            : base(dimensions, false)
        {

        }

        /**
         * Constructs a new {@code SparseObjectMatrix}
         * @param dimensions					the dimensions of this array
         * @param useColumnMajorOrdering		where inner index increments most frequently
         */
        public SparseObjectMatrix(int[] dimensions, bool useColumnMajorOrdering)
            : base(dimensions, useColumnMajorOrdering)
        {

        }

        /**
         * Sets the object to occupy the specified index.
         * 
         * @param index     the index the object will occupy
         * @param object    the object to be indexed.
         */
        public override IFlatMatrix<T> Set(int index, T obj)
        {
            if (sparseMap.ContainsKey(index))
            {
                sparseMap[index] = obj;
            }
            else
            {
                sparseMap.Add(index, (T)obj);
            }
            return this;
        }

        /**
         * Sets the specified object to be indexed at the index
         * computed from the specified coordinates.
         * @param object        the object to be indexed.
         * @param coordinates   the row major coordinates [outer --> ,...,..., inner]
         */
        public override AbstractSparseMatrix<T> Set(int[] coordinates, T obj)
        {
            Set(ComputeIndex(coordinates), obj);
            return this;
        }

        /**
         * Returns the T at the specified index.
         * 
         * @param index     the index of the T to return
         * @return  the T at the specified index.
         */
        public override T GetObject(int index)
        {
            return Get(index);
        }

        /**
         * Returns the T at the index computed from the specified coordinates
         * @param coordinates   the coordinates from which to retrieve the indexed object
         * @return  the indexed object
         */
        public override T Get(params int[] coordinates)
        {
            return Get(ComputeIndex(coordinates));
        }

        /**
         * Returns the T at the specified index.
         * 
         * @param index     the index of the T to return
         * @return  the T at the specified index.
         */

        public override T Get(int index)
        {
            if (sparseMap.ContainsKey(index))
                return sparseMap[index];
            return default(T);
        }

        /**
         * Returns a sorted array of occupied indexes.
         * @return  a sorted array of occupied indexes.
         */
        public override int[] GetSparseIndices()
        {
            return Reverse(sparseMap.Keys.ToArray());
        }

        /**
         * {@inheritDoc}
         */

        public override string ToString()
        {
            return Arrays.ToString(GetDimensions());
        }
    }
}