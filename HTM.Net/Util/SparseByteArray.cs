using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Util
{
    /// <summary>
    /// Represents an array with dimensions (recursivly), only with concrete data, empty spaces are not recorded
    /// </summary>
    [Serializable]
    public class SparseByteArray
    {
        private readonly int[] _dimensions;
        private readonly ConcurrentDictionary<int, object> _concreteArray;
        private readonly int[] _dimensionSums;  // sums of the values in each dimension

        private SparseByteArray(params int[] dimensions)
        {
            _dimensions = dimensions;
            _dimensionSums = new int[dimensions.Length];
            _concreteArray = new ConcurrentDictionary<int, object>();
            Rank = _dimensions.Length;
            Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
        }

        private SparseByteArray(ConcurrentDictionary<int, object> row, int[] dimensions, int[] dimensionSums)
        {
            _dimensions = dimensions;
            _concreteArray = row;
            _dimensionSums = dimensionSums;
            Rank = _dimensions.Length;
            Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
        }

        public byte this[params int[] indexes]
        {
            set { SetValue(indexes, value); }
            get { return GetValue(indexes); }
        }

        private byte GetValue(params int[] indexes)
        {
            if (indexes.Length > Rank) throw new ArgumentOutOfRangeException("indexes", "too many indexes given for array");
            ConcurrentDictionary<int, object> prevLevel = null;

            if (Rank == 1 && indexes.Length == 1)
            {
                if (_concreteArray.ContainsKey(indexes[0]))
                    return TypeConverter.Convert<byte>(_concreteArray[indexes[0]]);
                return default(byte);
            }

            for (int iLevel = 0; iLevel < indexes.Length; iLevel++)
            {
                int index = indexes[iLevel];
                if (iLevel < Rank - 1)
                {
                    if (!_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        break;
                    }
                    if (prevLevel != null && !prevLevel.ContainsKey(index))
                    {
                        break;
                    }

                    if (_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        prevLevel = _concreteArray[index] as ConcurrentDictionary<int, object>;
                    }
                    else if (prevLevel != null && prevLevel.ContainsKey(index))
                    {
                        prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                    }
                }
                else
                {
                    if (prevLevel != null && prevLevel.ContainsKey(index))
                    {
                        return TypeConverter.Convert<byte>(prevLevel[index]);
                    }
                    break;
                }
            }
            return default(byte);
        }

        private void SetValue(int[] indexes, byte value)
        {
            if (indexes.Length > Rank) throw new ArgumentOutOfRangeException("indexes", "too many indexes given for array");

            ConcurrentDictionary<int, object> prevLevelParent = null;
            ConcurrentDictionary<int, object> prevLevel = null;
            bool fillRow = Rank - indexes.Length == 1;

            // 1D array to be set
            if (Rank == 1 && indexes.Length == 1)
            {
                int index = indexes[0];
                if (!_concreteArray.ContainsKey(index) && !value.Equals(default(byte)))
                {
                    _dimensionSums[0] += 1;
                    _concreteArray[index] = value;
                }
                else if (_concreteArray.ContainsKey(index) && value.Equals(default(byte)))
                {
                    _dimensionSums[0] -= 1;
                    object removedObj;
                    _concreteArray.TryRemove(index, out removedObj);
                }
                // Else: Do nothing, no update required
                return;
            }

            // nD array to be set
            for (int iLevel = 0; iLevel < indexes.Length; iLevel++)
            {
                int index = indexes[iLevel];
                if (iLevel < Rank - 1)
                {
                    if (!_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        // First level not existing, add new dimension
                        prevLevelParent = _concreteArray;
                        _concreteArray[index] = prevLevel = new ConcurrentDictionary<int, object>();
                        if (fillRow)
                        {
                            // We need to fill this row with the given value
                            if (!value.Equals(default(byte)))
                            {
                                // Fill up the next dimension
                                int nextDimension = iLevel + 1;
                                for (int i = 0; i < _dimensions[nextDimension]; i++)
                                {
                                    prevLevel[i] = value;
                                    _dimensionSums[nextDimension] += 1;
                                }
                            }
                        }
                    }
                    else if (prevLevel != null && !prevLevel.ContainsKey(index))
                    {
                        // We are a level deeper and the key does not yet exist, create it
                        prevLevel[index] = new ConcurrentDictionary<int, object>();
                        if (fillRow)
                        {
                            if (!value.Equals(default(byte)))
                            {
                                int nextDimension = iLevel + 1;
                                for (int i = 0; i < _dimensions[iLevel + 1]; i++)
                                {
                                    prevLevel[i] = value;
                                    _dimensionSums[nextDimension] += 1;
                                }
                            }
                        }
                        prevLevelParent = prevLevel;
                        prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                    }
                    else
                    {
                        if (prevLevel != null)
                        {
                            prevLevelParent = prevLevel;
                            prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                        }
                        else
                        {
                            prevLevelParent = _concreteArray;
                            prevLevel = _concreteArray[index] as ConcurrentDictionary<int, object>;
                        }
                    }
                }
                else
                {
                    if (prevLevel == null) throw new InvalidOperationException("We should have a level here!");
                    //if (prevLevel.ContainsKey(index))
                    //{
                    //    // Replace when it exists

                    //    //prevLevel[index] = value;
                    //}
                    //else
                    //{
                    //    if (!value.Equals(default(T)))
                    //    {
                    //        // Only set when not the default
                    //        prevLevel[index] = value;
                    //    }
                    //}
                    if (!prevLevel.ContainsKey(index) && !value.Equals(default(byte)))
                    {
                        _dimensionSums[0] += 1;
                        prevLevel[index] = value;
                    }
                    else if (prevLevel.ContainsKey(index) && value.Equals(default(byte)))
                    {
                        _dimensionSums[0] -= 1;
                        object removedObj;
                        prevLevel.TryRemove(index, out removedObj);
                        if (prevLevel.Count == 0 && prevLevelParent != null)
                        {
                            // Remove this level entirly
                            prevLevelParent.TryRemove(indexes[iLevel - 1], out removedObj);
                        }
                    }
                }
            }
        }

        public int AppendRow(SparseByteArray row)
        {
            if (Rank != 2) throw new InvalidOperationException("This operation only works on 2D matrices");
            int nextId = _concreteArray.Keys.Any() ? _concreteArray.Keys.Max() + 1 : 0;
            _concreteArray.TryAdd(nextId, row._concreteArray);
            if (nextId >= _dimensions[0])
            {
                _dimensions[0] = nextId + 1;
                Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
                _dimensionSums[1] = row._concreteArray.Select(i => (int)i.Value).Sum();
            }
            return nextId;
        }

        public void SetRow(int row, SparseByteArray srow)
        {
            if (Rank != 2) throw new InvalidOperationException("This operation only works on 2D matrices");
            _concreteArray[row] = srow._concreteArray;
            if (row >= _dimensions[0])
            {
                _dimensions[0] = row + 1;
                Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
                _dimensionSums[1] = srow._concreteArray.Select(i => (int)i.Value).Sum();
            }
        }

        public void RemoveRow(int rowIndex)
        {
            if (Rank != 2) throw new InvalidOperationException("This operation only works on 2D matrices");
            object removed;
            _concreteArray.TryRemove(rowIndex, out removed);
            _dimensions[0] -= 1;
            Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
        }

        public int[] GetRowSums()
        {
            if (Rank != 2) throw new InvalidOperationException("This operation only works on 2D matrices");

            int[] sums = new int[GetLength(0)];

            for (int i = 0; i < sums.Length; i++)
            {
                if (_concreteArray.ContainsKey(i))
                {
                    sums[i] = ((ConcurrentDictionary<int, object>)_concreteArray[i]).Select(kvp => (int)kvp.Value).Sum();
                }
                else
                {
                    sums[i] = 0;
                }
            }
            return sums;
        }

        public SparseByteArray GetRow(int row)
        {
            if (Rank == 1)
            {
                throw new InvalidOperationException("Can only get a row of an array that has more then 1 dimension.");
            }
            if (Rank == 2)
            {
                if (_concreteArray.ContainsKey(row))
                {
                    return new SparseByteArray((ConcurrentDictionary<int, object>)_concreteArray[row], _dimensions.Skip(1).ToArray(), _dimensionSums.Skip(1).ToArray());
                }
                return null;
            }
            throw new NotSupportedException();
        }

        public object GetDimensionData(params int[] indices)
        {
            if (indices == null || indices.Length == 0) throw new ArgumentOutOfRangeException("indices", "Cannot get a dimension-date of nothing (too few indices given)");
            if (indices.Length > Rank) throw new ArgumentOutOfRangeException("indices", "Cannot get a dimension-date of dimensions who are not there! (too many indices given)");

            int outputArrayRank = Rank - indices.Length;

            if (outputArrayRank == 0)
            {
                // Just return the value
                return GetValue(indices);
            }

            int[] newArrayDimensions = new int[outputArrayRank];
            int startRankOffset = Rank - outputArrayRank;
            for (int i = 0, j = startRankOffset; i < outputArrayRank; i++)
            {
                newArrayDimensions[i] = GetLength(j++);
            }
            // Create new destination array
            SparseByteArray newArray = CreateInstance(newArrayDimensions);
            // Fillup the new array
            List<int> origIndices = new List<int>(indices);
            if (newArray.Rank == 1) // to 1D
            {
                //var destArray = newArray;
                int index1 = origIndices[0];
                if (Rank == 2) // from 2D
                {
                    //Buffer.BlockCopy(givenArray, (index1 * newArray.Length) * Marshal.SizeOf<T>(),
                    //    newArray, 0, Marshal.SizeOf<T>() * newArray.Length);
                    newArray = GetRow(index1);
                }
                else if (Rank == 3)
                {
                    //T[,,] srcArray = (T[,,])givenArray;
                    int index2 = origIndices[1];
                    for (int i = 0; i < newArray.GetLength(0); i++)
                    {
                        byte origValue = this[index1, index2, i];
                        newArray[i] = origValue;
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported");
                }
            }
            else if (newArray.Rank == 2)
            {
                var destArray = newArray;
                int index1 = origIndices[0];

                if (Rank == 3)
                {
                    //T[,,] srcArray = (T[,,])givenArray;
                    for (int r0 = 0; r0 < newArray.GetLength(0); r0++)
                    {
                        for (int r1 = 0; r1 < newArray.GetLength(1); r1++)
                        {
                            byte origValue = this[index1, r0, r1];
                            destArray[r0, r1] = origValue;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported (Rank " + newArray.Rank + ")");
                }

                //for (int r0 = 0; r0 < newArray.GetLength(0); r0++)
                //{
                //    for (int r1 = 0; r1 < newArray.GetLength(1); r1++)
                //    {
                //        int[] rankList = { r0, r1 };
                //        int[] rankListGet = new int[0];

                //        if (origIndices.Count == 1)
                //        {
                //            rankListGet = new[] { origIndices[0], r0, r1 };
                //        }
                //        else if (origIndices.Count == 2)
                //        {
                //            rankListGet = new[] { origIndices[0], origIndices[1], r0, r1 };
                //        }

                //        T origValue = (T)givenArray.GetValue(rankListGet);

                //        newArray.SetValue(origValue, rankList);
                //    }
                //}
            }

            return newArray;
        }

        public void Fill(byte value)
        {
            if (Rank == 1)
            {
                if (value.Equals(default(byte)))
                {
                    _concreteArray.Clear();
                }
                else
                {
                    for (int i = 0; i < _dimensions[0]; i++)
                    {
                        SetValue(new[] { i }, value);
                    }
                }
                return;
            }
            throw new NotSupportedException();
        }

        public int AggregateSum()
        {
            if (Rank == 1)
            {
                int sum = 0;
                foreach (var entry in _concreteArray)
                {
                    sum += (byte)entry.Value;
                }
                return sum;
            }
            else
            {
                int sum = 0;
                foreach (var entry in _concreteArray)
                {
                    sum += AggregateSumOfDicts(entry.Value as ConcurrentDictionary<int, object>);
                }
                return sum;
            }
        }

        private int AggregateSumOfDicts(ConcurrentDictionary<int, object> source)
        {
            if (source == null) return 0;

            int sum = 0;
            foreach (var entry in source)
            {
                if (entry.Value is ConcurrentDictionary<int, object>)
                {
                    sum += AggregateSumOfDicts(entry.Value as ConcurrentDictionary<int, object>);
                }
                else
                {
                    sum += (byte)entry.Value;
                }
            }
            return sum;
        }
        /// <summary>
        /// Gets the rank (dimensions) of this array
        /// </summary>
        public int Rank { get; }
        /// <summary>
        /// Gets the total length of this array
        /// </summary>
        public int Length { get; private set; }
        /// <summary>
        /// Returns the sum of the elements of this array
        /// </summary>
        public int Sum { get { return _dimensionSums.First(); } }
        /// <summary>
        /// Gets the length of a given dimension
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int GetLength(int index)
        {
            if (index > _dimensions.Length - 1 || index < 0) throw new ArgumentOutOfRangeException("index");
            return _dimensions[index];
        }
        /// <summary>
        /// Create a new sparse array with the given dimensions
        /// </summary>
        /// <param name="dimensions"></param>
        /// <returns></returns>

        public static SparseByteArray CreateInstance(params int[] dimensions)
        {
            if (dimensions == null || dimensions.Length == 0) throw new ArgumentOutOfRangeException("dimensions", "Cannot create an array without dimensions");
            return new SparseByteArray(dimensions);
        }
        /// <summary>
        /// Create sparseArray from given int array
        /// </summary>
        /// <param name="array"></param>
        /// <returns></returns>
        public static SparseByteArray FromArray(byte[] array)
        {
            if (array == null || array.Length == 0) throw new ArgumentOutOfRangeException("array");
            var indices = ArrayUtils.Where(array, b => b == 1);
            var indexMap = new ConcurrentDictionary<int, object>(indices.ToDictionary(k => k, v => (object)1));
            var spArray = new SparseByteArray(indexMap, new[] { array.Length }, new[] { indices.Length });
            return spArray;
        }
        /// <summary>
        /// Return a dense representation of this array
        /// </summary>
        /// <returns></returns>
        public IEnumerable<byte> AsDense()
        {
            if (Rank == 1)
            {
                for (int i = 0; i < _dimensions[0]; i++)
                {
                    yield return GetValue(i);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Return the indices where a value is located
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> GetSparseIndices()
        {
            if (Rank == 1)
            {
                return _concreteArray.Keys;
            }
            return _concreteArray.Keys; // keys of first dimension
        }

        /// <summary>
        /// Return the values where a value is located
        /// </summary>
        /// <returns></returns>
        public IEnumerable<byte> GetSparseValues()
        {
            if (Rank == 1)
            {
                return _concreteArray.Values.Select(TypeConverter.Convert<byte>);
            }
            throw new NotSupportedException("Only supported on 1D arrays");
        }

        public int[] GetDimensions()
        {
            return _dimensions;
        }

        
    }

    /// <summary>
    /// Represents an array with dimensions (recursivly), only with concrete data, empty spaces are not recorded
    /// </summary>
    public class SparseDoubleArray
    {
        private readonly int[] _dimensions;
        private readonly ConcurrentDictionary<int, object> _concreteArray;
        private readonly double[] _dimensionSums;  // sums of the values in each dimension

        protected SparseDoubleArray(params int[] dimensions)
        {
            _dimensions = dimensions;
            _dimensionSums = new double[dimensions.Length];
            _concreteArray = new ConcurrentDictionary<int, object>();
            Rank = _dimensions.Length;
            Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
        }

        private SparseDoubleArray(ConcurrentDictionary<int, object> row, int[] dimensions, double[] dimensionSums)
        {
            _dimensions = dimensions;
            _concreteArray = row;
            _dimensionSums = dimensionSums;
            Rank = _dimensions.Length;
            Length = _dimensions.Aggregate(1, (i, i1) => i * i1);
        }

        public double this[params int[] indexes]
        {
            set { SetValue(indexes, value); }
            get { return GetValue(indexes); }
        }

        private double GetValue(params int[] indexes)
        {
            if (indexes.Length > Rank) throw new ArgumentOutOfRangeException("indexes", "too many indexes given for array");
            ConcurrentDictionary<int, object> prevLevel = null;

            if (Rank == 1 && indexes.Length == 1)
            {
                if (_concreteArray.ContainsKey(indexes[0]))
                    return (double)_concreteArray[indexes[0]];
                return default(double);
            }

            for (int iLevel = 0; iLevel < indexes.Length; iLevel++)
            {
                int index = indexes[iLevel];
                if (iLevel < Rank - 1)
                {
                    if (!_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        break;
                    }
                    if (prevLevel != null && !prevLevel.ContainsKey(index))
                    {
                        break;
                    }

                    if (_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        prevLevel = _concreteArray[index] as ConcurrentDictionary<int, object>;
                    }
                    else if (prevLevel != null && prevLevel.ContainsKey(index))
                    {
                        prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                    }
                }
                else
                {
                    if (prevLevel != null && prevLevel.ContainsKey(index))
                    {
                        return (double)prevLevel[index];
                    }
                    break;
                }
            }
            return default(double);
        }

        private void SetValue(int[] indexes, double value)
        {
            if (indexes.Length > Rank) throw new ArgumentOutOfRangeException("indexes", "too many indexes given for array");

            ConcurrentDictionary<int, object> prevLevelParent = null;
            ConcurrentDictionary<int, object> prevLevel = null;
            bool fillRow = Rank - indexes.Length == 1;

            // 1D array to be set
            if (Rank == 1 && indexes.Length == 1)
            {
                int index = indexes[0];
                if (!_concreteArray.ContainsKey(index) && !value.Equals(default(double)))
                {
                    _dimensionSums[0] += 1;
                    _concreteArray[index] = value;
                }
                else if (_concreteArray.ContainsKey(index) && value.Equals(default(double)))
                {
                    _dimensionSums[0] -= 1;
                    object removedObj;
                    _concreteArray.TryRemove(index, out removedObj);
                }
                // Else: Do nothing, no update required
                return;
            }

            // nD array to be set
            for (int iLevel = 0; iLevel < indexes.Length; iLevel++)
            {
                int index = indexes[iLevel];
                if (iLevel < Rank - 1)
                {
                    if (!_concreteArray.ContainsKey(index) && prevLevel == null)
                    {
                        // First level not existing, add new dimension
                        prevLevelParent = _concreteArray;
                        _concreteArray[index] = prevLevel = new ConcurrentDictionary<int, object>();
                        if (fillRow)
                        {
                            // We need to fill this row with the given value
                            if (!value.Equals(default(double)))
                            {
                                // Fill up the next dimension
                                int nextDimension = iLevel + 1;
                                for (int i = 0; i < _dimensions[nextDimension]; i++)
                                {
                                    prevLevel[i] = value;
                                    _dimensionSums[nextDimension] += 1;
                                }
                            }
                        }
                    }
                    else if (prevLevel != null && !prevLevel.ContainsKey(index))
                    {
                        // We are a level deeper and the key does not yet exist, create it
                        prevLevel[index] = new ConcurrentDictionary<int, object>();
                        if (fillRow)
                        {
                            if (!value.Equals(default(double)))
                            {
                                int nextDimension = iLevel + 1;
                                for (int i = 0; i < _dimensions[iLevel + 1]; i++)
                                {
                                    prevLevel[i] = value;
                                    _dimensionSums[nextDimension] += 1;
                                }
                            }
                        }
                        prevLevelParent = prevLevel;
                        prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                    }
                    else
                    {
                        if (prevLevel != null)
                        {
                            prevLevelParent = prevLevel;
                            prevLevel = prevLevel[index] as ConcurrentDictionary<int, object>;
                        }
                        else
                        {
                            prevLevelParent = _concreteArray;
                            prevLevel = _concreteArray[index] as ConcurrentDictionary<int, object>;
                        }
                    }
                }
                else
                {
                    if (prevLevel == null) throw new InvalidOperationException("We should have a level here!");
                    //if (prevLevel.ContainsKey(index))
                    //{
                    //    // Replace when it exists

                    //    //prevLevel[index] = value;
                    //}
                    //else
                    //{
                    //    if (!value.Equals(default(T)))
                    //    {
                    //        // Only set when not the default
                    //        prevLevel[index] = value;
                    //    }
                    //}
                    if (!prevLevel.ContainsKey(index) && !value.Equals(default(double)))
                    {
                        _dimensionSums[0] += 1;
                        prevLevel[index] = value;
                    }
                    else if (prevLevel.ContainsKey(index) && value.Equals(default(double)))
                    {
                        _dimensionSums[0] -= 1;
                        object removedObj;
                        prevLevel.TryRemove(index, out removedObj);
                        if (prevLevel.Count == 0 && prevLevelParent != null)
                        {
                            // Remove this level entirly
                            prevLevelParent.TryRemove(indexes[iLevel - 1], out removedObj);
                        }
                    }
                }
            }
        }

        public SparseDoubleArray GetRow(int row)
        {
            if (Rank == 1)
            {
                throw new InvalidOperationException("Can only get a row of an array that has more then 1 dimension.");
            }
            if (Rank == 2)
            {
                if (_concreteArray.ContainsKey(row))
                {
                    return new SparseDoubleArray((ConcurrentDictionary<int, object>)_concreteArray[row], _dimensions.Skip(1).ToArray(), _dimensionSums.Skip(1).ToArray());
                }
                return null;
            }
            throw new NotSupportedException();
        }

        public object GetDimensionData(params int[] indices)
        {
            if (indices == null || indices.Length == 0) throw new ArgumentOutOfRangeException("indices", "Cannot get a dimension-date of nothing (too few indices given)");
            if (indices.Length > Rank) throw new ArgumentOutOfRangeException("indices", "Cannot get a dimension-date of dimensions who are not there! (too many indices given)");

            int outputArrayRank = Rank - indices.Length;

            if (outputArrayRank == 0)
            {
                // Just return the value
                return GetValue(indices);
            }

            int[] newArrayDimensions = new int[outputArrayRank];
            int startRankOffset = Rank - outputArrayRank;
            for (int i = 0, j = startRankOffset; i < outputArrayRank; i++)
            {
                newArrayDimensions[i] = GetLength(j++);
            }
            // Create new destination array
            SparseDoubleArray newArray = CreateInstance(newArrayDimensions);
            // Fillup the new array
            List<int> origIndices = new List<int>(indices);
            if (newArray.Rank == 1) // to 1D
            {
                //var destArray = newArray;
                int index1 = origIndices[0];
                if (Rank == 2) // from 2D
                {
                    //Buffer.BlockCopy(givenArray, (index1 * newArray.Length) * Marshal.SizeOf<T>(),
                    //    newArray, 0, Marshal.SizeOf<T>() * newArray.Length);
                    newArray = GetRow(index1);
                }
                else if (Rank == 3)
                {
                    //T[,,] srcArray = (T[,,])givenArray;
                    int index2 = origIndices[1];
                    for (int i = 0; i < newArray.GetLength(0); i++)
                    {
                        double origValue = this[index1, index2, i];
                        newArray[i] = origValue;
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported");
                }
            }
            else if (newArray.Rank == 2)
            {
                var destArray = newArray;
                int index1 = origIndices[0];

                if (Rank == 3)
                {
                    //T[,,] srcArray = (T[,,])givenArray;
                    for (int r0 = 0; r0 < newArray.GetLength(0); r0++)
                    {
                        for (int r1 = 0; r1 < newArray.GetLength(1); r1++)
                        {
                            double origValue = this[index1, r0, r1];
                            destArray[r0, r1] = origValue;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported (Rank " + newArray.Rank + ")");
                }

                //for (int r0 = 0; r0 < newArray.GetLength(0); r0++)
                //{
                //    for (int r1 = 0; r1 < newArray.GetLength(1); r1++)
                //    {
                //        int[] rankList = { r0, r1 };
                //        int[] rankListGet = new int[0];

                //        if (origIndices.Count == 1)
                //        {
                //            rankListGet = new[] { origIndices[0], r0, r1 };
                //        }
                //        else if (origIndices.Count == 2)
                //        {
                //            rankListGet = new[] { origIndices[0], origIndices[1], r0, r1 };
                //        }

                //        T origValue = (T)givenArray.GetValue(rankListGet);

                //        newArray.SetValue(origValue, rankList);
                //    }
                //}
            }

            return newArray;
        }

        public void Fill(double value)
        {
            if (Rank == 1)
            {
                if (value.Equals(default(double)))
                {
                    _concreteArray.Clear();
                }
                else
                {
                    for (int i = 0; i < _dimensions[0]; i++)
                    {
                        SetValue(new[] { i }, value);
                    }
                }
                return;
            }
            throw new NotSupportedException();
        }

        public double AggregateSum()
        {
            if (Rank == 1)
            {
                double sum = 0;
                foreach (var entry in _concreteArray)
                {
                    sum += (double)entry.Value;
                }
                return sum;
            }
            else
            {
                double sum = 0;
                foreach (var entry in _concreteArray)
                {
                    sum += AggregateSumOfDicts(entry.Value as ConcurrentDictionary<int, object>);
                }
                return sum;
            }
        }

        private double AggregateSumOfDicts(ConcurrentDictionary<int, object> source)
        {
            if (source == null) return 0;

            double sum = 0;
            foreach (var entry in source)
            {
                if (entry.Value is ConcurrentDictionary<int, object>)
                {
                    sum += AggregateSumOfDicts(entry.Value as ConcurrentDictionary<int, object>);
                }
                else
                {
                    sum += (double)entry.Value;
                }
            }
            return sum;
        }
        /// <summary>
        /// Gets the rank (dimensions) of this array
        /// </summary>
        public int Rank { get; }
        /// <summary>
        /// Gets the total length of this array
        /// </summary>
        public int Length { get; }
        public int Rows { get { return _dimensions.First(); } }
        public int Cols { get { return _dimensions.Last(); } }
        /// <summary>
        /// Returns the sum of the elements of this array
        /// </summary>
        public double Sum { get { return _dimensionSums.First(); } }
        /// <summary>
        /// Gets the length of a given dimension
        /// </summary>
        /// <param name="index"></param>
        /// <returns></returns>
        public int GetLength(int index)
        {
            if (index > _dimensions.Length - 1 || index < 0) throw new ArgumentOutOfRangeException("index");
            return _dimensions[index];
        }
        /// <summary>
        /// Create a new sparse array with the given dimensions
        /// </summary>
        /// <param name="dimensions"></param>
        /// <returns></returns>

        public static SparseDoubleArray CreateInstance(params int[] dimensions)
        {
            if (dimensions == null || dimensions.Length == 0) throw new ArgumentOutOfRangeException("dimensions", "Cannot create an array without dimensions");
            return new SparseDoubleArray(dimensions);
        }

        /// <summary>
        /// Return a dense representation of this array
        /// </summary>
        /// <returns></returns>
        public IEnumerable<double> AsDense()
        {
            if (Rank == 1)
            {
                for (int i = 0; i < _dimensions[0]; i++)
                {
                    yield return GetValue(i);
                }
            }
            else
            {
                throw new NotImplementedException();
            }
        }
        /// <summary>
        /// Return the indices where a value is located
        /// </summary>
        /// <returns></returns>
        public IEnumerable<int> GetSparseIndices()
        {
            if (Rank == 1)
            {
                return _concreteArray.Keys;
            }
            return _concreteArray.Keys; // keys of first dimension
        }
    }
}