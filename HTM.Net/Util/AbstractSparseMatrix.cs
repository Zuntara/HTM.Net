using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Util
{
    /// <summary>
    /// Allows storage of array data in sparse form, meaning that the indexes
    /// of the data stored are maintained while empty indexes are not.This allows
    /// savings in memory and computational efficiency because iterative algorithms
    /// need only query indexes containing valid data. The dimensions of matrix defined
    /// at construction time and immutable - matrix fixed size data structure.
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public abstract class AbstractSparseMatrix<T> : AbstractFlatMatrix<T>, ISparseMatrix<T>
    {
        /// <summary>
        /// Constructs a new {@code AbstractSparseMatrix} with the specified dimensions (defaults to row major ordering)
        /// </summary>
        /// <param name="dimensions"></param>
        protected AbstractSparseMatrix(int[] dimensions)
            : this(dimensions, false)
        {

        }
        /// <summary>
        /// Constructs a new {@code AbstractSparseMatrix} with the specified dimensions,
        /// llowing the specification of column major ordering if desired. (defaults to row major ordering)
        /// </summary>
        /// <param name="dimensions">each indexed value is a dimension size</param>
        /// <param name="useColumnMajorOrdering">if true, indicates column first iteration, otherwise row first iteration is the default (if false).</param>
        protected AbstractSparseMatrix(int[] dimensions, bool useColumnMajorOrdering)
            : base(dimensions, useColumnMajorOrdering)
        {

        }

        /// <summary>
        /// Sets the object to occupy the specified index.
        /// </summary>
        /// <typeparam name="TS"></typeparam>
        /// <param name="index">the index the object will occupy</param>
        /// <param name="value">the value to be indexed.</param>
        /// <returns></returns>
        protected virtual TS Set<TS>(int index, int value)
            where TS : AbstractSparseMatrix<T>
        {
            return null;
        }

        /// <summary>
        /// Sets the object to occupy the specified index.
        /// </summary>
        /// <typeparam name="TS"></typeparam>
        /// <param name="index">the index the object will occupy</param>
        /// <param name="value">the value to be indexed.</param>
        /// <returns></returns>
        protected virtual TS Set<TS>(int index, double value)
            where TS : AbstractSparseMatrix<T>
        {
            return null;
        }

        /// <summary>
        /// Sets the specified object to be indexed at the index
        /// </summary>
        /// <param name="coordinates">the row major coordinates [outer --> ,...,..., inner]</param>
        /// <param name="obj">the object to be indexed.</param>
        /// <returns>this {@code SparseMatrix} implementation</returns>
        public virtual new AbstractSparseMatrix<T> Set(int[] coordinates, T obj) { return null; }

        /// <summary>
        /// Sets the specified object to be indexed at the index computed from the specified coordinates.
        /// </summary>
        /// <typeparam name="TS">TS extends AbstractSparseMatrix{T}</typeparam>
        /// <param name="value">the value to be indexed.</param>
        /// <param name="coordinates">the row major coordinates [outer --> ,...,..., inner]</param>
        /// <returns></returns>
        protected virtual TS Set<TS>(int value, params int[] coordinates)
            where TS : AbstractSparseMatrix<T>
        {
            return null;
        }

        /// <summary>
        /// Sets the specified object to be indexed at the index computed from the specified coordinates.
        /// </summary>
        /// <typeparam name="TS">TS extends AbstractSparseMatrix{T}</typeparam>
        /// <param name="value">the value to be indexed.</param>
        /// <param name="coordinates">the row major coordinates [outer --> ,...,..., inner]</param>
        /// <returns></returns>
        protected virtual TS Set<TS>(double value, params int[] coordinates)
            where TS : AbstractSparseMatrix<T>
        {
            return null;
        }

        /// <summary>
        /// Returns the T at the specified index.
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public virtual T GetObject(int index) { return default(T); }

        /// <summary>
        /// Returns the T at the specified index
        /// </summary>
        /// <param name="index">the index of the T to return</param>
        /// <returns> the T at the specified index.</returns>
        public virtual int GetIntValue(int index) { return -1; }

        /// <summary>
        /// Returns the T at the specified index
        /// </summary>
        /// <param name="index">the index of the T to return</param>
        /// <returns> the T at the specified index.</returns>
        protected virtual double GetDoubleValue(int index) { return -1.0; }

        /// <summary>
        /// Returns the T at the index computed from the specified coordinates
        /// </summary>
        /// <param name="coordinates">the coordinates from which to retrieve the indexed object</param>
        /// <returns>the indexed object</returns>
        public override T Get(params int[] coordinates) { return default(T); }

        /// <summary>
        /// Returns the int value at the index computed from the specified coordinates
        /// </summary>
        /// <param name="coordinates">the coordinates from which to retrieve the indexed object</param>
        /// <returns>the indexed object</returns>
        public virtual int GetIntValue(params int[] coordinates) { return -1; }

        /// <summary>
        /// Returns the double value at the index computed from the specified coordinates
        /// </summary>
        /// <param name="coordinates">the coordinates from which to retrieve the indexed object</param>
        /// <returns>the indexed object</returns>
        protected virtual double GetDoubleValue(params int[] coordinates) { return -1.0; }

        /// <summary>
        /// Returns a sorted array of occupied indexes.
        /// </summary>
        /// <returns></returns>
        public virtual int[] GetSparseIndices()
        {
            return null;
        }

        public virtual int[] Get1DIndexes()
        {
            List<int> results = new List<int>(GetMaxIndex() + 1);
            Visit(GetDimensions(), 0, new int[GetNumDimensions()], results);
            return results.ToArray();
        }

        /// <summary>
        /// Recursively loops through the matrix dimensions to fill the results array with flattened computed array indexes.
        /// </summary>
        /// <param name="bounds"></param>
        /// <param name="currentDimension"></param>
        /// <param name="p"></param>
        /// <param name="results"></param>
        /// <returns></returns>
        private void Visit(int[] bounds, int currentDimension, int[] p, List<int> results)
        {
            for (int i = 0; i < bounds[currentDimension]; i++)
            {
                p[currentDimension] = i;
                if (currentDimension == p.Length - 1)
                {
                    results.Add(ComputeIndex(p));
                }
                else Visit(bounds, currentDimension + 1, p, results);
            }
        }

        public T[] AsDense(ITypeFactory<T> factory)
        {
            int[] dimensions = GetDimensions();
            T[] retVal = (T[])Array.CreateInstance(factory.TypeClass(), dimensions);
            Fill(factory, 0, dimensions, dimensions[0], retVal.Cast<object>().ToArray());

            return retVal;
        }

        /// <summary>
        /// Uses reflection to create and fill a dynamically created multidimensional array.
        /// </summary>
        /// <param name="f">the {@link TypeFactory}</param>
        /// <param name="dimensionIndex">the current index into <em>this class's</em> configured dimensions array <em>*NOT*</em> the dimensions used as this method's argument</param>
        /// <param name="dimensions">the array specifying remaining dimensions to create</param>
        /// <param name="count">the current dimensional size</param>
        /// <param name="arr">the array to fill</param>
        /// <returns>a dynamically created multidimensional array</returns>
        protected object[] Fill(ITypeFactory<T> f, int dimensionIndex, int[] dimensions, int count, object[] arr)
        {
            if (dimensions.Length == 1)
            {
                for (int i = 0; i < count; i++)
                {
                    arr[i] = f.Make(GetDimensions());
                }
                return arr;
            }
            else
            {
                for (int i = 0; i < count; i++)
                {
                    int[] inner = CopyInnerArray(dimensions);
                    T[] r = (T[])Array.CreateInstance(f.TypeClass(), inner);
                    arr[i] = Fill(f, dimensionIndex + 1, inner, GetDimensions()[dimensionIndex + 1], r.Cast<object>().ToArray());
                }
                return arr;
            }
        }
    }
}