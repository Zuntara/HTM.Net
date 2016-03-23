namespace HTM.Net.Util
{
    public interface ISparseMatrix : IFlatMatrix
    {
        /// <summary>
        /// Returns a sorted array of occupied indexes.
        /// </summary>
        /// <returns></returns>
        int[] GetSparseIndices();
        /// <summary>
        /// Returns an array of all the flat indexes that can be computed from the current configuration.
        /// </summary>
        /// <returns></returns>
        int[] Get1DIndexes();
    }

    public interface ISparseMatrix<T> : IFlatMatrix<T>, ISparseMatrix
    {
        /// <summary>
        /// Uses the specified {@link TypeFactory} to return an array filled with the specified object type, according this {@code SparseMatrix}'s
        /// configured dimensions
        /// </summary>
        /// <param name="factory"></param>
        /// <returns></returns>
        T[] AsDense(ITypeFactory<T> factory);
    }
}