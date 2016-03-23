namespace HTM.Net.Util
{
    /// <summary>
    /// Implementation of a sparse matrix which contains binary byte values only.
    /// </summary>
    public class SparseBinaryMatrix : AbstractSparseBinaryMatrix
    {
        protected readonly SparseByteArray _backingArray;

        /**
         * Constructs a new {@code SparseBinaryMatrix} with the specified
         * dimensions (defaults to row major ordering)
         * 
         * @param dimensions    each indexed value is a dimension size
         */
        public SparseBinaryMatrix(int[] dimensions) : this(dimensions, false)
        {

        }

        /**
         * Constructs a new {@code SparseBinaryMatrix} with the specified dimensions,
         * allowing the specification of column major ordering if desired. 
         * (defaults to row major ordering)
         * 
         * @param dimensions                each indexed value is a dimension size
         * @param useColumnMajorOrdering    if true, indicates column first iteration, otherwise
         *                                  row first iteration is the default (if false).
         */
        public SparseBinaryMatrix(int[] dimensions, bool useColumnMajorOrdering)
                : base(dimensions, useColumnMajorOrdering)
        {
            _backingArray = SparseByteArray.CreateInstance(dimensions);
        }

        /**
         * Called during mutation operations to simultaneously set the value
         * on the backing array dynamically.
         * @param val
         * @param coordinates
         */
        private void Back(byte val, params int[] coordinates)
        {
            //ArrayUtils.SetValue(_backingArray, val, coordinates);
            _backingArray[coordinates] = val;
            //update true counts
            var backingArrayLocal = _backingArray;
            SparseByteArray coordArray = (SparseByteArray)backingArrayLocal.GetDimensionData(coordinates[0]);
            //SetTrueCount(coordinates[0], ArrayUtils.AggregateArray(coordArray));
            SetTrueCount(coordinates[0], coordArray.AggregateSum());
        }

        /// <summary>
        /// Returns the slice specified by the passed in coordinates.
        /// The array is returned as an object, therefore it is the caller's
        /// responsibility to cast the array to the appropriate dimensions.
        /// </summary>
        /// <param name="coordinates">the coordinates which specify the returned array</param>
        /// <returns>the array specified</returns>
        /// <exception cref="ArgumentException">if the specified coordinates address an actual value instead of the array holding it.</exception>
        public override object GetSlice(params int[] coordinates)
        {
            object slice = _backingArray.GetDimensionData(coordinates);
            //Ensure return value is of type Array
            if (!slice.GetType().IsArray && slice.GetType() != typeof(SparseByteArray))
            {
                SliceError(coordinates);
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
        //public override void RightVecSumAtNZ(int[] inputVector, int[] results)
        //{
        //    for (int i = 0; i < _dimensions[0]; i++)
        //    {
        //        SparseArray<byte> slice = (SparseArray<byte>)(_dimensions.Length > 1 ? GetSlice(i) : _backingArray);
        //        for (int j = 0; j < slice.Length; j++)
        //        {
        //            results[i] += (inputVector[j] * slice[j]);
        //        }
        //    }
        //}

        public override void RightVecSumAtNZ(int[] inputVector, int[] results)
        {
            ////////

            var mainMatrixPositions = _backingArray.GetSparseIndices();

            foreach (int matrixRowIndex in mainMatrixPositions)
            {
                SparseByteArray slice = (SparseByteArray)(_dimensions.Length > 1 ? GetSlice(matrixRowIndex) : _backingArray);
                var sliceIndices = slice.GetSparseIndices();

                foreach (int index in sliceIndices)
                {
                    results[matrixRowIndex] += (inputVector[index] * slice[index]);
                }
            }

            ////////
        }

        /**
         * Fills the specified results array with the result of the 
         * matrix vector multiplication.
         * 
         * @param inputVector       the right side vector
         * @param results           the results array
         */
        public override void RightVecSumAtNZ(int[] inputVector, int[] results, double stimulusThreshold)
        {
            ////////

            var mainMatrixPositions = _backingArray.GetSparseIndices();

            foreach (int matrixRowIndex in mainMatrixPositions)
            {
                SparseByteArray slice = (SparseByteArray)(_dimensions.Length > 1 ? GetSlice(matrixRowIndex) : _backingArray);
                var sliceIndices = slice.GetSparseIndices();

                foreach (int index in sliceIndices)
                {
                    results[matrixRowIndex] += (inputVector[index] * slice[index]);
                }
                results[matrixRowIndex] -= results[matrixRowIndex] < stimulusThreshold ? results[matrixRowIndex] : 0;
            }

            ////////
            /// 
            //int dimension = _dimensions[0];
            //int[] inputVectorIndices = ArrayUtils.Where(inputVector, ArrayUtils.WHERE_1);

            ////for (int i = 0; i < dimension; i++)
            ////{
            //Parallel.For(0, dimension, i =>
            //{
            //    SparseArray<byte> slice = (SparseArray<byte>)(_dimensions.Length > 1 ? GetSlice(i) : _backingArray);
            //    var sliceIndices = slice.GetSparseIndices();

            //    int j = 0;
            //    foreach (int index in inputVectorIndices.Union(sliceIndices))
            //    {
            //        results[i] += inputVector[index] * slice[index];
            //        if (j == slice.Length - 1)
            //        {
            //            results[i] -= results[i] < stimulusThreshold ? results[i] : 0;
            //        }
            //        j++;
            //    }
            //});
        }
        //public override void RightVecSumAtNZ(int[] inputVector, int[] results, double stimulusThreshold)
        //{
        //    for (int i = 0; i < _dimensions[0]; i++)
        //    {
        //        SparseArray<byte> slice = (SparseArray<byte>)(_dimensions.Length > 1 ? GetSlice(i) : _backingArray);
        //        byte[] denseSlice = slice.AsDense().ToArray();
        //        for (int j = 0; j < denseSlice.Length; j++)
        //        {
        //            results[i] += (inputVector[j] * denseSlice[j]);
        //            if (j == denseSlice.Length - 1)
        //            {
        //                results[i] -= results[i] < stimulusThreshold ? results[i] : 0;
        //            }
        //        }
        //    }
        //}

        /**
         * Sets the value at the specified index.
         * 
         * @param index     the index the object will occupy
         * @param object    the object to be indexed.
         */
        public override IFlatMatrix<int> Set(int index, int value)
        {
            int[] coordinates = ComputeCoordinates(index);
            return Set(value, coordinates);
        }

        /**
         * Sets the value to be indexed at the index
         * computed from the specified coordinates.
         * @param coordinates   the row major coordinates [outer --> ,...,..., inner]
         * @param object        the object to be indexed.
         */
        public override AbstractSparseBinaryMatrix Set(int value, params int[] coordinates)
        {
            Back((byte)value, coordinates);
            return this;
        }

        /**
         * Sets the specified values at the specified indexes.
         * 
         * @param indexes   indexes of the values to be set
         * @param values    the values to be indexed.
         * 
         * @return this {@code SparseMatrix} implementation
         */
        public override AbstractSparseBinaryMatrix Set(int[] indexes, int[] values)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                Set(index: indexes[i], value: values[i]);
            }
            return this;
        }

        //public virtual AbstractSparseBinaryMatrix Set(int index, object value)
        //{
        //    Set(index, (int)(value));
        //    return this;
        //}

        /**
         * Clears the true counts prior to a cycle where they're
         * being set
         */
        public override void ClearStatistics(int row)
        {
            SetTrueCount(row, 0);

            SparseByteArray slice = _backingArray.GetRow(row);
            if (slice != null)
            {
                slice.Fill(0);
            }
            //Arrays.Fill(slice, 0);
        }

        public override int Get(int index)
        {
            int[] coordinates = ComputeCoordinates(index);
            if (coordinates.Length == 1)
            {
                return _backingArray[index];
            }
            return _backingArray[coordinates];
        }

        public override AbstractSparseBinaryMatrix SetForTest(int index, int value)
        {
            //ArrayUtils.SetValue(_backingArray, (byte)value, ComputeCoordinates(index));
            _backingArray[ComputeCoordinates(index)] = (byte)value;
            return this;
        }

        /// <summary>
        /// Append a row to the matrix
        /// </summary>
        /// <param name="row"></param>
        protected int AddRow(SparseByteArray row)
        {
            int added = _backingArray.AppendRow(row);
            _dimensions = _backingArray.GetDimensions();
            dimensionMultiples = InitDimensionMultiples(
                    isColumnMajor ? Reverse(_dimensions) : _dimensions);
            return added;
        }
    }
}