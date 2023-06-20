using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Serialization;

namespace HTM.Net.Util
{
    [Serializable]
    public abstract class Generator<T> : IEnumerator<T>, IEnumerable<T>
    {
        /**
         * Returns the value returned by the last call to {@link #next()}
         * or the initial value if no previous call to {@code #next()} was made.
         * @return
         */
        public virtual int Get() { return -1; }

        /**
         * Returns the configured size or distance between the initialized
         * upper and lower bounds.
         * @return
         */
        public virtual int Count
        {
            get { return -1; }
        }

        #region Implementation of IDisposable

        public void Dispose()
        {
            
        }

        #endregion

        #region Implementation of IEnumerator

        public virtual bool MoveNext()
        {
            throw new NotImplementedException("Implement in deriving class");
        }

        public virtual void Reset()
        {
            
        }

        public virtual T Current { get; protected set; }

        object IEnumerator.Current
        {
            get { return Current; }
        }

        #endregion

        #region Implementation of IEnumerable

        public IEnumerator<T> GetEnumerator()
        {
            return this;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        #endregion

        public static Generator<T> Of(List<T> l, Generator<int> i)
        {
            return new DefaultGenerator(l, i);
        }

        private class DefaultGenerator : Generator<T>
        {
            private readonly List<T> _list;
            private readonly Generator<int> _generator;

            public DefaultGenerator(List<T> list, Generator<int> generator)
            {
                _list = list;
                _generator = generator;
            }

            #region Overrides of Generator<T>

            public override T Current
            {
                get { return _list[_generator.Current]; }
            }

            public override bool MoveNext()
            {
                return _generator.MoveNext();
            }

            #endregion
        }
    }

    //public abstract class Generator2<T> : IEnumerator<T>, IEnumerable<T>
    //{
    //    /**
    //     * Returns the value returned by the last call to {@link #next()}
    //     * or the initial value if no previous call to {@code #next()} was made.
    //     * @return
    //     */
    //    public virtual int Get() { return -1; }

    //    /**
    //     * Returns the configured size or distance between the initialized
    //     * upper and lower bounds.
    //     * @return
    //     */
    //    public virtual int Count
    //    {
    //        get { return -1; }
    //    }

    //    /**
    //     * Returns the state of this generator to its initial state so 
    //     * that it can be reused.
    //     */
    //    public virtual void Reset() { }

    //    public bool MoveNext()
    //    {
    //        if (HasNext())
    //        {
    //            Current = Next();
    //            return true;
    //        }
    //        return false;
    //    }

    //    public T Current { get; protected set; }

    //    object IEnumerator.Current
    //    {
    //        get { return Current; }
    //    }

    //    /**
    //     * Returns a flag indicating whether another iteration
    //     * of processing may occur.
    //     * 
    //     * @return  true if so, false if not
    //     */
    //    public abstract bool HasNext();

    //    /**
    //     * Returns the object of type &lt;T&gt; which is the
    //     * result of one iteration of processing.
    //     * 
    //     * @return   the object of type &lt;T&gt; to return
    //     */
    //    public abstract T Next();

    //    #region Implementation of IDisposable

    //    public void Dispose()
    //    {

    //    }

    //    #endregion

    //    #region Implementation of IEnumerable

    //    public IEnumerator<T> GetEnumerator()
    //    {
    //        return this;
    //    }

    //    IEnumerator IEnumerable.GetEnumerator()
    //    {
    //        return GetEnumerator();
    //    }

    //    #endregion

    //    public static Generator<T> Of(List<T> l, Generator<int> i)
    //    {
    //        return new DefaultGenerator(l, i);
    //    }

    //    private class DefaultGenerator : Generator<T>
    //    {
    //        private readonly List<T> _list;
    //        private readonly Generator<int> _generator;

    //        public DefaultGenerator(List<T> list, Generator<int> generator)
    //        {
    //            _list = list;
    //            _generator = generator;
    //        }

    //        #region Overrides of Generator<T>

    //        public override T Next()
    //        {
    //            return _list[_generator.Next()];
    //        }

    //        public override bool HasNext()
    //        {
    //            return _generator.HasNext();
    //        }

    //        #endregion
    //    }
    //}

    [Serializable]
    public class IntGenerator : Generator<int>
    {
        private const long serialVersionUID = 1L;

        protected int _i;
        protected int lower;
        protected int upper;

        public IntGenerator(int lower, int upper)
        {
            this.lower = lower;
            this.upper = upper;
            _i = lower;
            Current = _i;
        }

        public override int Get()
        {
            return _i;
        }

        public override int Count
        {
            get { return upper - lower; }
        }


        #region Overrides of Generator<int>

        public override void Reset()
        {
            _i = lower;
        }

        public override bool MoveNext()
        {
            if (_i < upper)
            {
                int retVal = _i;
                _i = ++_i > upper ? upper : _i;
                Current = retVal;
            }
            else
            {
                return false;
            }
            return true;
        }

        #endregion

        public static IntGenerator Of(int lower, int upper)
        {
            return new IntGenerator(lower, upper);
        }
    }

    [Serializable]
    public class Coordinator
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

        protected int[] dimensions;
        protected int[] dimensionMultiples;
        protected bool isColumnMajor;
        protected int numDimensions;

        /**
         * Constructs a new {@link Coordinator} object to be configured with specified
         * dimensions and major ordering.
         * @param shape  the dimensions of this matrix 
         */
        public Coordinator(int[] shape)
            : this(shape, false)
        {

        }

        /**
         * Constructs a new {@link Coordinator} object to be configured with specified
         * dimensions and major ordering.
         * 
         * @param shape                     the dimensions of this sparse array 
         * @param useColumnMajorOrdering    flag indicating whether to use column ordering or
         *                                  row major ordering. if false (the default), then row
         *                                  major ordering will be used. If true, then column major
         *                                  ordering will be used.
         */
        public Coordinator(int[] shape, bool useColumnMajorOrdering)
        {
            dimensions = shape;
            numDimensions = shape.Length;
            dimensionMultiples = InitDimensionMultiples(
                useColumnMajorOrdering ? Reverse(shape) : shape);
            isColumnMajor = useColumnMajorOrdering;
        }

        /**
         * Returns a flat index computed from the specified coordinates
         * which represent a "dimensioned" index.
         * 
         * @param   coordinates     an array of coordinates
         * @return  a flat index
         */
        public int ComputeIndex(int[] coordinates)
        {
            int[] localMults = isColumnMajor ? Reverse(dimensionMultiples) : dimensionMultiples;
            int @base = 0;
            for (int i = 0; i < coordinates.Length; i++)
            {
                @base += (localMults[i] * coordinates[i]);
            }
            return @base;
        }

        /**
         * Returns an array of coordinates calculated from
         * a flat index.
         * 
         * @param   index   specified flat index
         * @return  a coordinate array
         */
        public int[] ComputeCoordinates(int index)
        {
            int[] returnVal = new int[numDimensions];
            int @base = index;
            for (int i = 0; i < dimensionMultiples.Length; i++)
            {
                int quotient = @base / dimensionMultiples[i];
                @base %= dimensionMultiples[i];
                returnVal[i] = quotient;
            }
            return isColumnMajor ? Reverse(returnVal) : returnVal;
        }

        /**
         * Initializes internal helper array which is used for multidimensional
         * index computation.
         * @param dimensions matrix dimensions
         * @return array for use in coordinates to flat index computation.
         */
        protected int[] InitDimensionMultiples(int[] dimensions)
        {
            int holder = 1;
            int len = dimensions.Length;
            int[] dimensionMultiples = new int[numDimensions];
            for (int i = 0; i < len; i++)
            {
                holder *= (i == 0 ? 1 : dimensions[len - i]);
                dimensionMultiples[len - 1 - i] = holder;
            }
            return dimensionMultiples;
        }

        /**
         * Reverses the specified array.
         * @param input
         * @return
         */
        public static int[] Reverse(int[] input)
        {
            int[] retVal = new int[input.Length];
            for (int i = input.Length - 1, j = 0; i >= 0; i--, j++)
            {
                retVal[j] = input[i];
            }
            return retVal;
        }
    }

    [Serializable]
    public class Topology : Coordinator
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

        private IntGenerator[] igs;
        private int[] centerPosition;

        /**
     * Constructs a new {@link AbstractFlatMatrix} object to be configured with specified
     * dimensions and major ordering.
     * @param shape  the dimensions of this matrix 
     */
        public Topology(params int[] shape)
            : base(shape, false)
        {
        }

        /**
         * Translate an index into coordinates, using the given coordinate system.
         * 
         * @param index     The index of the point. The coordinates are expressed as a single index by
         *                  using the dimensions as a mixed radix definition. For example, in dimensions
         *                  42x10, the point [1, 4] is index 1*420 + 4*10 = 460.
         * @return          A array of coordinates of length len(dimensions).
         */
        public int[] CoordinatesFromIndex(int index)
        {
            return ComputeCoordinates(index);
        }

        /**
         * Translate coordinates into an index, using the given coordinate system.
         * 
         * @param coordinates       A array of coordinates of length dimensions.size().
         * @param shape             The coordinate system.
         * @return                  The index of the point. The coordinates are expressed as a single index by
         *                          using the dimensions as a mixed radix definition. For example, in dimensions
         *                          42x10, the point [1, 4] is index 1*420 + 4*10 = 460.
         */
        public int IndexFromCoordinates(params int[] coordinates)
        {
            return ComputeIndex(coordinates);
        }

        /**
         * Get the points in the neighborhood of a point.
         *
         * A point's neighborhood is the n-dimensional hypercube with sides ranging
         * [center - radius, center + radius], inclusive. For example, if there are two
         * dimensions and the radius is 3, the neighborhood is 6x6. Neighborhoods are
         * truncated when they are near an edge.
         * 
         * @param centerIndex       The index of the point. The coordinates are expressed as a single index by
         *                          using the dimensions as a mixed radix definition. For example, in dimensions
         *                          42x10, the point [1, 4] is index 1*420 + 4*10 = 460.
         * @param radius            The radius of this neighborhood about the centerIndex.
         * @return  The points in the neighborhood, including centerIndex.
         */
        public int[] Neighborhood(int centerIndex, int radius)
        {
            centerPosition = CoordinatesFromIndex(centerIndex);

            igs = ArrayUtils.Range(0, dimensions.Length)
                .Select(i => IntGenerator.Of(Math.Max(0, centerPosition[i] - radius), Math.Min(dimensions[i] - 1, centerPosition[i] + radius) + 1))
                .ToArray();//IntGenerator[].@new

            List<List<int>> result = new List<List<int>>();
            result.Add(new List<int>());
            List<List<int>> interim = new List<List<int>>();
            foreach (IntGenerator pool in igs)
            {
                int size = result.Count;
                interim.Clear();
                interim.AddRange(result);
                result.Clear();
                for (int x = 0; x < size; x++)
                {
                    List<int> lx = interim[x];
                    pool.Reset();
                    for (int y = 0; y < pool.Count; y++)
                    {
                        pool.MoveNext();
                        int py = pool.Current;
                        List<int> tl = new List<int>();
                        tl.AddRange(lx);
                        tl.Add(py);
                        result.Add(tl);
                    }
                }
            }

            return result.Select(tl => IndexFromCoordinates(tl.ToArray())).ToArray();
        }

        /**
         * Like {@link #neighborhood(int, int)}, except that the neighborhood isn't truncated when it's
         * near an edge. It wraps around to the other side.
         * 
         * @param centerIndex       The index of the point. The coordinates are expressed as a single index by
         *                          using the dimensions as a mixed radix definition. For example, in dimensions
         *                          42x10, the point [1, 4] is index 1*420 + 4*10 = 460.
         * @param radius            The radius of this neighborhood about the centerIndex.
         * @return  The points in the neighborhood, including centerIndex.
         */
        public int[] WrappingNeighborhood(int centerIndex, int radius)
        {
            int[] cp = CoordinatesFromIndex(centerIndex);

            IntGenerator[] igs = ArrayUtils.Range(0, dimensions.Length)
                .Select(i => new IntGenerator(cp[i] - radius, Math.Min((cp[i] - radius) + dimensions[i] - 1, cp[i] + radius) + 1))
                .ToArray();//IntGenerator[]::new

            List<List<int>> result = new List<List<int>>();
            result.Add(new List<int>());
            List<List<int>> interim = new List<List<int>>();
            for (int i = 0; i < igs.Length; i++)
            {
                IntGenerator pool = igs[i];
                int size = result.Count;
                interim.Clear();
                interim.AddRange(result);
                result.Clear();
                for (int x = 0; x < size; x++)
                {
                    List<int> lx = interim[x];
                    pool.Reset();
                    for (int y = 0; y < pool.Count; y++)
                    {
                        pool.MoveNext();
                        int py = ArrayUtils.Modulo(pool.Current, dimensions[i]);
                        List<int> tl = new List<int>();
                        tl.AddRange(lx);
                        tl.Add(py);
                        result.Add(tl);
                    }
                }
            }

            return result.AsParallel()
                         .Select(tl => IndexFromCoordinates(tl.ToArray())).ToArray();
        }
    }
}