using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace HTM.Net.Util
{
    public static class ArrayExtentions
    {
        public static object GetDimensionData<T>(this Array givenArray, params int[] indices)
        {
            int outputArrayRank = givenArray.Rank - indices.Length;

            if (outputArrayRank == 0)
            {
                // Just return the value
                return givenArray.GetValue(indices);
            }

            int[] newArrayDimensions = new int[outputArrayRank];
            int startRankOffset = givenArray.Rank - outputArrayRank;
            for (int i = 0, j = startRankOffset; i < outputArrayRank; i++)
            {
                newArrayDimensions[i] = givenArray.GetLength(j++);
            }
            // Create new destination array
            Array newArray = Array.CreateInstance(typeof(T), newArrayDimensions);
            // Fillup the new array
            List<int> origIndices = new List<int>(indices);
            if (newArray.Rank == 1)
            {
                T[] destArray = (T[])newArray;
                int index1 = origIndices[0];
                if (givenArray.Rank == 2)
                {
                    Buffer.BlockCopy(givenArray, (index1 * newArray.Length) * Marshal.SizeOf<T>(), 
                        newArray, 0, Marshal.SizeOf<T>() * newArray.Length);

                    //T[,] srcArray = (T[,])givenArray;
                    //for (int i = 0; i < newArray.GetLength(0); i++)
                    //{
                    //    T origValue = srcArray[index1, i];
                    //    destArray[i] = origValue;
                    //}
                }
                else if (givenArray.Rank == 3)
                {
                    T[,,] srcArray = (T[,,])givenArray;
                    int index2 = origIndices[1];
                    for (int i = 0; i < newArray.GetLength(0); i++)
                    {
                        T origValue = srcArray[index1, index2, i];
                        destArray[i] = origValue;
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported");
                }
            }
            if (newArray.Rank == 2)
            {
                T[,] destArray = (T[,])newArray;
                int index1 = origIndices[0];

                if (givenArray.Rank == 3)
                {
                    T[,,] srcArray = (T[,,])givenArray;
                    for (int r0 = 0; r0 < newArray.GetLength(0); r0++)
                    {
                        for (int r1 = 0; r1 < newArray.GetLength(1); r1++)
                        {
                            T origValue = srcArray[index1, r0, r1];
                            destArray[r0,r1] = origValue;
                        }
                    }
                }
                else
                {
                    throw new NotSupportedException("Not yet supported");
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

        public static object GetDimensionData2<T>(this Array givenArray, params int[] indices)
        {
            int outputArrayRank = givenArray.Rank - indices.Length;

            if (outputArrayRank == 0)
            {
                // Just return the value
                return givenArray.GetValue(indices);
            }

            int[] newArrayDimensions = new int[outputArrayRank];
            int startRankOffset = givenArray.Rank - outputArrayRank;
            for (int i = 0, j = startRankOffset; i < outputArrayRank; i++)
            {
                newArrayDimensions[i] = givenArray.GetLength(j++);
            }
            // Create new destination array
            Array newArray = Array.CreateInstance(typeof(T), newArrayDimensions);

            List<int> origIndices = new List<int>(indices);
            if (newArray.Rank == 1)
            {
                Buffer.BlockCopy(givenArray, (indices[0] * newArray.Length) * Marshal.SizeOf<T>(), newArray, 0, Marshal.SizeOf<T>() * newArray.Length);
                return newArray;
            }
            if (newArray.Rank == 2)
            {
                Buffer.BlockCopy(givenArray, (indices[0] * newArray.Length) * Marshal.SizeOf<T>(), newArray, 0, Marshal.SizeOf<T>() * newArray.Length);
                return newArray;
            }
            throw new NotSupportedException("Not yet supported");
        }

        public static Array GetRow<T>(this Array givenArray, int level1)
        {
            if (givenArray.Rank != 2) throw new NotSupportedException("GetRow only works with 2 dimensions");
            int length = givenArray.GetLength(1);
            T[] row = new T[length];

            Buffer.BlockCopy(givenArray, (level1 * length) * Marshal.SizeOf<T>(), row, 0, Marshal.SizeOf<T>() * length);

            //for (int i = 0; i < length; i++)
            //{
            //    row[i] = (T)givenArray.GetValue(level1, i);
            //}
            return row;
        }

        public static void SetRow<T>(this Array givenArray, T value, int level1)
        {
            int length = givenArray.GetLength(1);
           
            T[] destRow = new T[length];
            for (int i = 0; i < length; i++)
            {
                destRow[i] = value;
            }
            Buffer.BlockCopy(destRow, 0, givenArray, level1 * Marshal.SizeOf<T>(), length);

            //for (int i = 0; i < length; i++)
            //{
            //    givenArray.SetValue(value, i, level1);
            //}
        }

        public static int GetArrayHashCode<T>(this T[] array)
        {
            if (array == null || array.Length == 0) return 0;
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                for (int i = 0; i < array.Length; i++)
                {
                    hash = hash * 23 + (array[i] != null ? array[i].GetHashCode() : 0);
                }
                return hash;
            }
        }

        public static int GetArrayHashCode(this IDictionary array)
        {
            if (array == null || array.Count == 0) return 0;
            unchecked // Overflow is fine, just wrap
            {
                int hash = 17;
                foreach (DictionaryEntry entry in array)
                {
                    hash = hash * 23 + entry.GetHashCode();
                }
                return hash;
            }
        }
    }
}