namespace HTM.Net.Util
{
    public interface IMatrix
    {
        /// <summary>
        /// Returns the array describing the dimensionality of the configured array.
        /// </summary>
        /// <returns>the array describing the dimensionality of the configured array.</returns>
        int[] GetDimensions();
        /// <summary>
        /// Returns the configured number of dimensions.
        /// </summary>
        /// <returns>The configured number of dimensions.</returns>
        int GetNumDimensions();
    }

    public interface IMatrix<T> : IMatrix
    {
        
        /// <summary>
        /// Gets element at supplied index.
        /// </summary>
        /// <param name="index"> index to retrieve.</param>
        /// <returns>element at index.</returns>
        T Get(params int[] index);

        /// <summary>
        /// Puts an element to supplied index.
        /// </summary>
        /// <param name="index">index to put on.</param>
        /// <param name="value">value element.</param>
        /// <returns></returns>
        IMatrix<T> Set(int[] index, T value);
    }
}