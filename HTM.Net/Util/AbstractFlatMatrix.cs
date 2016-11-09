using System;
using HTM.Net.Model;

// https://github.com/numenta/htm.java/
namespace HTM.Net.Util
{
    [Serializable]
    public abstract class AbstractFlatMatrix<T> : Persistable, IFlatMatrix<T>
    {
        protected int[] _dimensions;
        protected int[] dimensionMultiples;
        protected bool isColumnMajor;
        protected int numDimensions;

        /// <summary>
        /// Constructs a new <see cref="AbstractFlatMatrix{T}"/> object to be configured with specified dimensions and major ordering.
        /// </summary>
        /// <param name="dimensions">the dimensions of this matrix</param>
        protected AbstractFlatMatrix(int[] dimensions)
            : this(dimensions, false)
        {

        }

        /// <summary>
        /// Constructs a new <see cref="AbstractFlatMatrix{T}"/> object to be configured with specified dimensions and major ordering.
        /// </summary>
        /// <param name="dimensions">the dimensions of this matrix</param>
        /// <param name="useColumnMajorOrdering">flag indicating whether to use column ordering or row major ordering. if false (the default), then row major ordering will be used. If true, then column major ordering will be used.</param>
        protected AbstractFlatMatrix(int[] dimensions, bool useColumnMajorOrdering)
        {
            this._dimensions = dimensions;
            this.numDimensions = dimensions.Length;
            this.dimensionMultiples = InitDimensionMultiples(
                    useColumnMajorOrdering ? Reverse(dimensions) : dimensions);
            isColumnMajor = useColumnMajorOrdering;
        }

        /// <summary>
        /// Compute the flat index of a multidimensional array.
        /// </summary>
        /// <param name="indexes">indexes multidimensional indexes</param>
        /// <returns>the flat array index</returns>
        public int ComputeIndex(int[] indexes)
        {
            return ComputeIndex(indexes, true);
        }

        /// <summary>
        /// Returns a flat index computed from the specified coordinates which represent a "dimensioned" index.
        /// </summary>
        /// <param name="coordinates">an array of coordinates</param>
        /// <param name="doCheck">enforce validated comparison to locally stored dimensions</param>
        /// <returns>a flat index</returns>
        public int ComputeIndex(int[] coordinates, bool doCheck)
        {
            if (doCheck) CheckDims(coordinates);

            int[] localMults = isColumnMajor ? Reverse(dimensionMultiples) : dimensionMultiples;
            int @base = 0;
            for (int i = 0; i < coordinates.Length; i++)
            {
                @base += (localMults[i] * coordinates[i]);
            }
            return @base;
        }

        /// <summary>
        /// Checks the indexes specified to see whether they are within the configured bounds and size parameters of this array configuration.
        /// </summary>
        /// <param name="index">the array dimensions to check</param>
        protected void CheckDims(int[] index)
        {
            if (index.Length != numDimensions)
            {
                throw new ArgumentException("Specified coordinates exceed the configured array dimensions " +
                        "input dimensions: " + index.Length + " > number of configured dimensions: " + numDimensions);
            }
            for (int i = 0; i < index.Length - 1; i++)
            {
                if (index[i] >= _dimensions[i])
                {
                    throw new ArgumentException("Specified coordinates exceed the configured array dimensions " +
                            Print1DArray(index) + " > " + Print1DArray(_dimensions));
                }
            }
        }

        /// <summary>
        /// Returns an array of coordinates calculated from a flat index.
        /// </summary>
        /// <param name="index">specified flat index</param>
        /// <returns>a coordinate array</returns>
        public virtual int[] ComputeCoordinates(int index)
        {
            int[] returnVal = new int[GetNumDimensions()];
            int @base = index;
            for (int i = 0; i < dimensionMultiples.Length; i++)
            {
                int quotient = @base / dimensionMultiples[i];
                @base %= dimensionMultiples[i];
                returnVal[i] = quotient;
            }
            return isColumnMajor ? Reverse(returnVal) : returnVal;
        }

        /// <summary>
        /// Initializes internal helper array which is used for multidimensional index computation.
        /// </summary>
        /// <param name="dimensions">matrix dimensions</param>
        /// <returns> array for use in coordinates to flat index computation.</returns>
        protected int[] InitDimensionMultiples(int[] dimensions)
        {
            int holder = 1;
            int len = dimensions.Length;
            int[] dimensionMultiples = new int[GetNumDimensions()];
            for (int i = 0; i < len; i++)
            {
                holder *= (i == 0 ? 1 : dimensions[len - i]);
                dimensionMultiples[len - 1 - i] = holder;
            }
            return dimensionMultiples;
        }

        /// <summary>
        /// Utility method to shrink a single dimension array by one index.
        /// </summary>
        /// <param name="array">the array to shrink</param>
        protected int[] CopyInnerArray(int[] array)
        {
            if (array.Length == 1) return array;

            int[] retVal = new int[array.Length - 1];

            Array.Copy(array, 1, retVal, 0, array.Length - 1);
            //System.arraycopy(array, 1, retVal, 0, array.Length - 1);
            return retVal;
        }

        /// <summary>
        /// Reverses the specified array.
        /// </summary>
        /// <param name="input"></param>
        public static int[] Reverse(int[] input)
        {
            int[] retVal = new int[input.Length];
            for (int i = input.Length - 1, j = 0; i >= 0; i--, j++)
            {
                retVal[j] = input[i];
            }
            return retVal;
        }

        /// <summary>
        /// Prints the specified array to a returned String.
        /// </summary>
        /// <param name="array">the array object to print.</param>
        /// <returns>the array in string form suitable for display.</returns>
        public static string Print1DArray(Array array)
        {
            if (array.Rank != 1) throw new ArgumentException("Only 1D arrays supported", nameof(array));

            string joined = "";
            for (int i = 0; i < array.Length; i++)
            {
                var val = array.GetValue(i);
                if (val != null)
                    joined += val.ToString() + " ";
                else
                    joined += "- ";
            }

            return "[" + joined.TrimEnd() + "]";
            //if (aObject.GetType().IsArray)
            //{
            //    if (aObject is object[]) // can we cast to Object[]
            //        return string.Join(" ", ((object[]) aObject).Select(o => o.ToString()));
            //        //return Arrays.toString((Object[])aObject);
            //    else
            //    {  
            //        // we can't cast to Object[] - case of primitive arrays
            //        int length = Array.GetLength(aObject);

            //        object[] objArr = new object[length];
            //        for (int i = 0; i < length; i++)
            //            objArr[i] = Array.get(aObject, i);
            //        return Arrays.toString(objArr);
            //    }
            //}
            //return "[]";
        }

        public abstract T Get(int index);

        public abstract IFlatMatrix<T> Set(int index, T value);

        public virtual T Get(params int[] indexes)
        {
            return Get(ComputeIndex(indexes));
        }

        public IMatrix<T> Set(int[] indexes, T value)
        {
            Set(ComputeIndex(indexes), value);
            return this;
        }

        public int GetSize()
        {
            return _dimensions.Reduce(); // TODO: check that this does the same
            //return Arrays.stream(this._dimensions).reduce((n, i) ->n * i).getAsInt();
        }

        public virtual int GetMaxIndex()
        {
            return GetDimensions()[0] * Math.Max(1, GetDimensionMultiples()[0]) - 1;
        }

        public virtual int[] GetDimensions()
        {
            return _dimensions;
        }

        public void SetDimensions(int[] dimensions)
        {
            _dimensions = dimensions;
        }

        public virtual int GetNumDimensions()
        {
            return _dimensions.Length;
        }


        public virtual int[] GetDimensionMultiples()
        {
            return dimensionMultiples;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + Arrays.GetHashCode(dimensionMultiples);
            result = prime * result + Arrays.GetHashCode(_dimensions);
            result = prime * result + (isColumnMajor ? 1231 : 1237);
            result = prime * result + numDimensions;
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            AbstractFlatMatrix<T> other = (AbstractFlatMatrix<T>)obj;
            if (!Arrays.AreEqual(dimensionMultiples, other.dimensionMultiples))
                return false;
            if (!Arrays.AreEqual(_dimensions, other._dimensions))
                return false;
            if (isColumnMajor != other.isColumnMajor)
                return false;
            if (numDimensions != other.numDimensions)
                return false;
            return true;
        }
    }

    /**
 * Implemented to be used as arguments in other operations.
 * see {@link ArrayUtils#retainLogicalAnd(int[], Condition[])};
 * {@link ArrayUtils#retainLogicalAnd(double[], Condition[])}.
 */
    /**
    * Convenience adapter to remove verbosity
    * @author metaware
    *
    */

    /**
    * Utilities to match some of the functionality found in Python's Numpy.
    * @author David Ray
*/
}
