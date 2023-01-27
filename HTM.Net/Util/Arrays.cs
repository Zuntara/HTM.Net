using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.InteropServices;
using System.Text;

namespace HTM.Net.Util
{
    public static class Arrays
    {
        /// <summary>
        /// Copies the specified array, truncating or padding with nulls (if necessary) so the copy has the specified length.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="array"></param>
        /// <param name="newLength"></param>
        /// <returns></returns>
        public static T[] CopyOf<T>(T[] array, int newLength)
        {
            T[] newArray = new T[newLength];
            if (newArray.Length >= array.Length)
            {
                if (typeof(T) == typeof(string))
                {
                    // append with null or 0
                    for (int i = 0; i < array.Length; i++)
                    {
                        newArray[i] = array[i];
                    }
                }
                else
                {
                    Buffer.BlockCopy(array, 0, newArray, 0, array.Length * Marshal.SizeOf<T>());
                }
                return newArray;
            }

            // shrinking of the array
            if (newArray.Length < array.Length)
            {
                if (typeof(T) == typeof(string))
                {
                    for (int i = 0; i < newArray.Length; i++)
                    {
                        newArray[i] = array[i];
                    }
                }
                else
                {
                    Buffer.BlockCopy(array, 0, newArray, 0, newArray.Length * Marshal.SizeOf<T>());
                }
                return newArray;
            }
            throw new NotImplementedException();
        }

        public static T[] CopyOfRange<T>(T[] source, int from, int to)
        {
            return source.Skip(from).Take(to - from).ToArray();
        }

        public static int Reduce(this int[] array)
        {
            //return Arrays.stream(this._dimensions).reduce((n, i) ->n * i).getAsInt();
            return array.Aggregate(1, (current, t) => current * t);
        }

        public static string ToArrayString(Array v, string format = "{0}, ")
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            foreach (var item in v)
            {
                sb.AppendFormat(format, item);
            }
            string result = sb.ToString().TrimEnd(',', ' ');
            result += "]";
            return result;
        }

        public static string ToString<T>(IEnumerable<T> v)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            foreach (var item in v)
            {
                if (item is IEnumerable && !(item is string))
                {
                    sb.AppendFormat("{0}, ", ToString(item as IEnumerable));
                }
                else
                {
                    if (item is double)
                    {
                        sb.AppendFormat("{0}, ", ((double)Convert.ToDouble(item)).ToString(NumberFormatInfo.InvariantInfo));
                    }
                    else
                    {
                        sb.AppendFormat("{0}, ", item);
                    }
                }
            }
            string result = sb.ToString().TrimEnd(',', ' ');
            result += "]";
            return result;
        }

        public static string ToString(IEnumerable v)
        {
            StringBuilder sb = new StringBuilder();
            sb.Append("[");
            foreach (var item in v)
            {
                if (item is IEnumerable && !(item is string))
                {
                    sb.AppendFormat("{0}, ", ToString(item as IEnumerable));
                }
                else
                {
                    if (item is double)
                    {
                        sb.AppendFormat("{0}, ", ((double)Convert.ToDouble(item)).ToString(NumberFormatInfo.InvariantInfo));
                    }
                    else
                    {
                        sb.AppendFormat("{0}, ", item);
                    }
                }
            }
            string result = sb.ToString().TrimEnd(',', ' ');
            result += "]";
            return result;
        }

        public static void Fill<T>(T[] array, T i)
        {
            for (int j = 0; j < array.Length; j++)
            {
                array[j] = i;
            }
        }

        public static void Fill(byte[] array, byte i)
        {
            MemsetUtil.Memset(array, i, array.Length);
        }

        //public unsafe static void Fill(byte[] array, byte i)
        //{
        //    fixed (byte* arr = array)
        //    {
        //        byte* pArr = arr;
        //        for (int j = 0; j < array.Length; j++)
        //        {
        //            *pArr = i;
        //            pArr++;
        //        }
        //    }
        //}

        public unsafe static void Fill(int[] array, int i)
        {
            fixed (int* arr = array)
            {
                int* pArr = arr;
                for (int j = 0; j < array.Length; j++)
                {
                    *pArr = i;
                    pArr++;
                }
            }
        }

        public unsafe static void Fill(double[] array, double i)
        {
            fixed (double* arr = array)
            {
                double* pArr = arr;
                for (int j = 0; j < array.Length; j++)
                {
                    *pArr = i;
                    pArr++;
                }
            }
        }

        internal static void Fill(bool[] array, bool b)
        {
            MemsetUtil.Memset(array, b, array.Length);
            //fixed (bool* arr = array)
            //{
            //    bool* pArr = arr;
            //    for (int j = 0; j < array.Length; j++)
            //    {
            //        *pArr = b;
            //        pArr++;
            //    }
            //}
        }

        public static bool AreEqual<T>(IEnumerable<T> arr1, IEnumerable<T> arr2, bool nullAlsoIsEqual = true)
            where T : IComparable<T>
        {
            if (arr1 == null && arr2 == null && nullAlsoIsEqual) return true;
            if (arr1 == null || arr2 == null) return false;

            IList<T> list1 = new List<T>(arr1);
            IList<T> list2 = new List<T>(arr2);
            if (list1.Count != list2.Count) return false;

            for (int i = 0; i < list1.Count; i++)
            {
                if (list1[i].CompareTo(list2[i]) != 0) return false;
            }
            return true;
        }

        public static bool AreEqualList(IList arr1, IList arr2)
        {
            if (arr1 == null || arr2 == null) return false;

            if (arr1.Count != arr2.Count) return false;

            for (int i = 0; i < arr1.Count; i++)
            {
                if (!arr1[i].Equals(arr2[i])) return false;
            }
            return true;
        }

        public static string DeepToString(int[] toArray)
        {
            return ToString(toArray);
        }

        public static string DeepToString(Array toArray)
        {
            throw new NotImplementedException();
        }

        public static List<int> AsList(params int[] args)
        {
            return new List<int>(args);
        }
        public static List<int[]> AsList(params int[][] args)
        {
            return new List<int[]>(args);
        }

        public static int GetHashCode<T>(T[] o)
        {
            if (o == null)
                return 0;

            int result = 1;
            foreach (T element in o)
                result = 31 * result + element.GetHashCode();

            return result;
        }
    }

    public static class MemsetUtil
    {
        private static readonly Action<IntPtr, bool, int> MemsetDelegateBool;
        private static readonly Action<IntPtr, byte, int> MemsetDelegateByte;

        static MemsetUtil()
        {
            CreateDyamicDelegate(out MemsetDelegateBool);
            CreateDyamicDelegate(out MemsetDelegateByte);
        }

        private static void CreateDyamicDelegate<T>(out Action<IntPtr, T, int> memDelegate)
        {
            var dynamicMethod = new DynamicMethod("Memset", MethodAttributes.Public | MethodAttributes.Static,
                CallingConventions.Standard,
                null, new[] { typeof(IntPtr), typeof(T), typeof(int) }, typeof(MemsetUtil), true);

            ILGenerator generator = dynamicMethod.GetILGenerator();
            generator.Emit(OpCodes.Ldarg_0);
            generator.Emit(OpCodes.Ldarg_1);
            generator.Emit(OpCodes.Ldarg_2);
            generator.Emit(OpCodes.Initblk);
            generator.Emit(OpCodes.Ret);

            memDelegate = (Action<IntPtr, T, int>)dynamicMethod.CreateDelegate(typeof(Action<IntPtr, T, int>));
        }

        public static void Memset(bool[] array, bool what, int length)
        {
            GCHandle gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
            MemsetDelegateBool(gcHandle.AddrOfPinnedObject(), what, length);
            gcHandle.Free();
        }

        public static void Memset(byte[] array, byte what, int length)
        {
            GCHandle gcHandle = GCHandle.Alloc(array, GCHandleType.Pinned);
            MemsetDelegateByte(gcHandle.AddrOfPinnedObject(), what, length);
            gcHandle.Free();
        }
    }
}