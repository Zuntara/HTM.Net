using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Util
{
    /// <summary>
    /// Base class for matrices containing specifically binary (0 or 1) integer values
    /// </summary>
    [Serializable]
    public abstract class AbstractSparseBinaryMatrix : AbstractSparseMatrix<int>
    {
        private int[] trueCounts;

        /// <summary>
        /// Constructs a new {@code AbstractSparseBinaryMatrix} with the specified dimensions (defaults to row major ordering)
        /// </summary>
        /// <param name="dimensions"></param>
        protected AbstractSparseBinaryMatrix(int[] dimensions)
            : this(dimensions, false)
        {

        }

        /// <summary>
        /// Constructs a new {@code AbstractSparseBinaryMatrix} with the specified dimensions, allowing the specification of column major ordering if desired.
        /// (defaults to row major ordering)
        /// </summary>
        /// <param name="dimensions">each indexed value is a dimension size</param>
        /// <param name="useColumnMajorOrdering">if true, indicates column first iteration, otherwise row first iteration is the default (if false).</param>
        protected AbstractSparseBinaryMatrix(int[] dimensions, bool useColumnMajorOrdering)
            : base(dimensions, useColumnMajorOrdering)
        {
            trueCounts = new int[dimensions[0]];
        }

        /// <summary>
        /// Returns the slice specified by the passed in coordinates.
        /// The array is returned as an object, therefore it is the caller's
        /// responsibility to cast the array to the appropriate dimensions.
        /// </summary>
        /// <param name="coordinates">the coordinates which specify the returned array</param>
        /// <returns>the array specified</returns>
        /// <exception cref="ArgumentException">if the specified coordinates address an actual value instead of the array holding it.</exception>
        public abstract object GetSlice(params int[] coordinates);

        /// <summary>
        /// Launch getSlice error, to share it with subclass <see cref="GetSlice"/> implementations.
        /// </summary>
        /// <param name="coordinates">Coordinates to take note of in the error</param>
        protected void SliceError(params int[] coordinates)
        {
            throw new ArgumentException("This method only returns the array holding the specified index: " + Arrays.ToString(coordinates));
        }

        /**
     * Calculate the flat indexes of a slice
     * @return the flat indexes array
     */
        internal protected int[] GetSliceIndexes(int[] coordinates)
        {
            int[] dimensions = GetDimensions();
            // check for valid coordinates
            if (coordinates.Length >= dimensions.Length)
                SliceError(coordinates);

            int sliceDimensionsLength = dimensions.Length - coordinates.Length;
            int[] sliceDimensions = new int[sliceDimensionsLength];

            for (int i = coordinates.Length; i < dimensions.Length; i++)
            {
                sliceDimensions[i - coordinates.Length] = dimensions[i];
            }

            int[] elementCoordinates = Arrays.CopyOf(coordinates, coordinates.Length + 1);

            int sliceSize = sliceDimensions.Reduce();
            //int sliceSize = Arrays.stream(sliceDimensions).reduce((n, i) ->n * i).getAsInt();
            int[] slice = new int[sliceSize];

            if (coordinates.Length + 1 == dimensions.Length)
            {
                // last slice 
                for (int d = 0; d < dimensions[coordinates.Length]; d++)
                {
                    elementCoordinates[coordinates.Length] = d;
                    //Array.Set(slice, i, ComputeIndex(elementCoordinates));
                    slice[d] = ComputeIndex(elementCoordinates);
                }
            }
            else {
                for (int d = 0; d < dimensions[sliceDimensionsLength]; d++)
                {
                    elementCoordinates[coordinates.Length] = d;
                    int[] indexes = GetSliceIndexes(elementCoordinates);
                    //System.arraycopy(indexes, 0, slice, i * indexes.Length, indexes.Length);
                    Array.Copy(indexes, 0, slice, d * indexes.Length, indexes.Length);
                }
            }

            return slice;
        }

        /**
         * Fills the specified results array with the result of the 
         * matrix vector multiplication.
         * 
         * @param inputVector		the right side vector
         * @param results			the results array
         */
        public abstract void RightVecSumAtNZ(int[] inputVector, int[] results);

        /**
         * Fills the specified results array with the result of the 
         * matrix vector multiplication.
         * 
         * @param inputVector       the right side vector
         * @param results           the results array
         */
        public abstract void RightVecSumAtNZ(int[] inputVector, int[] results, double stimulusThreshold);

        /**
         * Sets the value at the specified index.
         * 
         * @param index     the index the object will occupy
         * @param object    the object to be indexed.
         */
        public override IFlatMatrix<int> Set(int index, int value)
        {
            int[] coordinates = ComputeCoordinates(index);
            return Set<AbstractSparseBinaryMatrix>(value, coordinates);
        }

        /**
         * Sets the value to be indexed at the index
         * computed from the specified coordinates.
         * @param coordinates   the row major coordinates [outer --> ,...,..., inner]
         * @param object        the object to be indexed.
         */

        public abstract AbstractSparseBinaryMatrix Set(int value, params int[] coordinates);

        /**
         * Sets the specified values at the specified indexes.
         * 
         * @param indexes   indexes of the values to be set
         * @param values    the values to be indexed.
         * 
         * @return this {@code SparseMatrix} implementation
         */
        public virtual AbstractSparseBinaryMatrix Set(int[] indexes, int[] values)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                Set(indexes[i], values[i]);
            }
            return this;
        }

        public override int Get(params int[] coordinates)
        {
            return Get(ComputeIndex(coordinates));
        }

        public override abstract int Get(int index);

        /**
         * Sets the value at the specified index skipping the automatic
         * truth statistic tallying of the real method.
         * 
         * @param index     the index the object will occupy
         * @param object    the object to be indexed.
         */
        public abstract AbstractSparseBinaryMatrix SetForTest(int index, int value);

        /**
         * Call This for TEST METHODS ONLY
         * Sets the specified values at the specified indexes.
         * 
         * @param indexes   indexes of the values to be set
         * @param values    the values to be indexed.
         * 
         * @return this {@code SparseMatrix} implementation
         */
        public AbstractSparseBinaryMatrix Set(int[] indexes, int[] values, bool isTest)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                if (isTest) SetForTest(indexes[i], values[i]);
                else Set(indexes[i],value: values[i]);
            }
            return this;
        }

        /**
     * Returns the count of 1's set on the specified row.
     * @param index
     * @return
     */
        public int GetTrueCount(int index)
        {
            return trueCounts[index];
        }

        /**
         * Sets the count of 1's on the specified row.
         * @param index
         * @param count
         */
        public void SetTrueCount(int index, int count)
        {
            trueCounts[index] = count;
        }

        /**
         * Get the true counts for all outer indexes.
         * @return
         */
        public int[] GetTrueCounts()
        {
            return trueCounts;
        }

        /**
         * Clears the true counts prior to a cycle where they're
         * being set
         */
        public virtual void ClearStatistics(int row)
        {
            trueCounts[row] = 0;

            var sliced = GetSliceIndexes(new[] { row });

            foreach (int index in sliced)
            {
                Set(index, 0);
            }
        }

        /**
         * Returns the int value at the index computed from the specified coordinates
         * @param coordinates   the coordinates from which to retrieve the indexed object
         * @return  the indexed object
         */
        public override int GetIntValue(params int[] coordinates)
        {
            return Get(ComputeIndex(coordinates));
        }

        /**
         * Returns the T at the specified index.
         * 
         * @param index     the index of the T to return
         * @return  the T at the specified index.
         */
        public override int GetIntValue(int index)
        {
            return Get(index);
        }

        /**
         * Returns a sorted array of occupied indexes.
         * @return  a sorted array of occupied indexes.
         */
        public override int[] GetSparseIndices()
        {
            List<int> indexes = new List<int>();
            for (int i = 0; i <= GetMaxIndex(); i++)
            {
                if (Get(i) > 0)
                    indexes.Add(i);
            }

            return indexes.ToArray();
        }

        /**
         * This {@code SparseBinaryMatrix} will contain the operation of or-ing
         * the inputMatrix with the contents of this matrix; returning this matrix
         * as the result.
         * 
         * @param inputMatrix   the matrix containing the "on" bits to or
         * @return  this matrix
         */
        public AbstractSparseBinaryMatrix Or(AbstractSparseBinaryMatrix inputMatrix)
        {
            int[] mask = inputMatrix.GetSparseIndices();
            int[] ones = new int[mask.Length];
            Arrays.Fill(ones, 1);
            return Set(mask, ones);
        }

        /**
         * This {@code SparseBinaryMatrix} will contain the operation of or-ing
         * the sparse list with the contents of this matrix; returning this matrix
         * as the result.
         * 
         * @param onBitIndexes  the matrix containing the "on" bits to or
         * @return  this matrix
         */
        public AbstractSparseBinaryMatrix Or(IEnumerable<int> onBitIndexes)
        {
            int[] ones = new int[onBitIndexes.Count()];
            Arrays.Fill(ones, 1);
            return Set(onBitIndexes.ToArray(), ones);
        }

        /**
         * This {@code SparseBinaryMatrix} will contain the operation of or-ing
         * the sparse array with the contents of this matrix; returning this matrix
         * as the result.
         * 
         * @param onBitIndexes  the int array containing the "on" bits to or
         * @return  this matrix
         */
        public AbstractSparseBinaryMatrix Or(int[] onBitIndexes)
        {
            int[] ones = new int[onBitIndexes.Length];
            Arrays.Fill(ones, 1);
            return Set(onBitIndexes, ones);
        }



        protected HashSet<int> GetSparseSet()
        {
            return new HashSet<int>(GetSparseIndices());
        }

        /**
         * Returns true if the on bits of the specified matrix are
         * matched by the on bits of this matrix. It is allowed that 
         * this matrix have more on bits than the specified matrix.
         * 
         * @param matrix
         * @return
         */
        public bool All(AbstractSparseBinaryMatrix matrix)
        {
            return ContainsAll(GetSparseSet(), matrix.GetSparseIndices());
            //return GetSparseSet().SetEquals(matrix.GetSparseIndices());
        }

        /**
         * Returns true if the on bits of the specified list are
         * matched by the on bits of this matrix. It is allowed that 
         * this matrix have more on bits than the specified matrix.
         * 
         * @param matrix
         * @return
         */
        public bool All(IEnumerable<int> onBits)
        {
            return ContainsAll(GetSparseSet(), onBits);
            //return GetSparseSet().SetEquals(onBits);
        }

        /**
         * Returns true if the on bits of the specified array are
         * matched by the on bits of this matrix. It is allowed that 
         * this matrix have more on bits than the specified matrix.
         * 
         * @param matrix
         * @return
         */
        public bool All(int[] onBits)
        {
            return ContainsAll(GetSparseSet(), onBits);
            //return GetSparseSet().SetEquals(onBits);
        }

        /**
         * Returns true if any of the on bits of the specified matrix are
         * matched by the on bits of this matrix. It is allowed that 
         * this matrix have more on bits than the specified matrix.
         * 
         * @param matrix
         * @return
         */
        public bool Any(AbstractSparseBinaryMatrix matrix)
        {
            HashSet<int> keySet = GetSparseSet();

            foreach (int i in matrix.GetSparseIndices())
            {
                if (keySet.Contains(i)) return true;
            }
            return false;
        }

        /**
         * Returns true if any of the on bit indexes of the specified collection are
         * matched by the on bits of this matrix. It is allowed that 
         * this matrix have more on bits than the specified matrix.
         * 
         * @param matrix
         * @return
         */
        public bool Any(IEnumerable<int> onBits)
        {
            HashSet<int> keySet = GetSparseSet();

            //for (TIntIterator i = onBits.iterator(); i.hasNext();)
            foreach (int i in onBits)
            {
                if (keySet.Contains(i)) return true;
            }
            return false;
        }

        private bool ContainsAll(HashSet<int> src, IEnumerable<int> toBeContained)
        {
            bool ok = false;

            foreach (int value in toBeContained)
            {
                if (!src.Contains(value))
                {
                    ok = false;
                    break;
                }
                ok = true;
            }

            return ok;
        }

        ///**
        // * Returns true if any of the on bit indexes of the specified matrix are
        // * matched by the on bits of this matrix. It is allowed that 
        // * this matrix have more on bits than the specified matrix.
        // * 
        // * @param matrix
        // * @return
        // */
        //public bool Any(int[] onBits)
        //{
        //    HashSet<int> keySet = GetSparseSet();

        //    foreach (int i in onBits)
        //    {
        //        if (keySet.Contains(i)) return true;
        //    }
        //    return false;
        //}
    }
}