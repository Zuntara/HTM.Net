namespace HTM.Net.Util
{
    public interface IFlatMatrix : IMatrix
    {
        int ComputeIndex(int[] coordinates);

        int ComputeIndex(int[] coordinates, bool doCheck);

        /// <summary>
        /// Returns the maximum accessible flat index.
        /// </summary>
        /// <returns>the maximum accessible flat index.</returns>
        int GetMaxIndex();

        int[] GetDimensionMultiples();

        /// <summary>
        /// Returns an array of coordinates calculated from a flat index.
        /// </summary>
        /// <param name="index">specified flat index</param>
        /// <returns>a coordinate array</returns>
        int[] ComputeCoordinates(int index);
    }

    public interface IFlatMatrix<T> : IMatrix<T>, IFlatMatrix
    {
        T Get(int index);

        IFlatMatrix<T> Set(int index, T value);
    }
}