using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using MathNet.Numerics;

namespace HTM.Net.Util
{
    public static class ArrayUtils
    {
        /** Empty array constant */
        private static int[] EMPTY_ARRAY = new int[0];


        public static Func<int, bool> WHERE_1 = i => i == 1;
        public static Func<double, bool> GREATER_THAN_0 = i => i > 0;
        public static Func<int, bool> INT_GREATER_THAN_0 = i => i > 0;
        public static Func<int, bool> GREATER_OR_EQUAL_0 = i => i >= 0;

        /// <summary>
        /// Returns the product of each integer in the specified array.
        /// </summary>
        /// <param name="dims"></param>
        /// <returns></returns>
        public static int Product(int[] dims)
        {
            return dims.AsParallel().Aggregate(1, (current, t) => current * t);
        }

        /// <summary>
        /// Returns an array containing the successive elements of each
        /// argument array as in [first[0], second[0], first[1], second[1], ... ].
        /// 
        /// Arrays may be of zero length, and may be of different sizes, but may not be null.
        /// </summary>
        /// <param name="first"></param>
        /// <param name="second"></param>
        /// <returns></returns>
        public static object[] Interleave<TF, TS>(TF[] first, TS[] second)
        {
            int flen, slen;
            object[] retVal = new object[(flen = first.Length) + (slen = second.Length)];
            for (int i = 0, j = 0, k = 0; i < flen || j < slen;)
            {
                if (i < flen)
                {
                    retVal[k++] = first.GetValue(i++);
                }
                if (j < slen)
                {
                    retVal[k++] = second.GetValue(j++);
                }
            }

            return retVal;
        }

        /// <summary>
        /// Return a new double[] containing the difference of each element and its
        /// succeeding element.
        /// 
        /// The first order difference is given by ``out[n] = a[n+1] - a[n]``
        /// along the given axis, higher order differences are calculated by using `diff`
        /// recursively.
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public static double[] Diff(double[] d)
        {
            double[] retVal = new double[d.Length - 1];
            for (int i = 0; i < retVal.Length; i++)
            {
                retVal[i] = d[i + 1] - d[i];
            }

            return retVal;
        }

        /**
         * Returns a flag indicating whether the container list contains an
         * array which matches the specified match array.
         *
         * @param match     the array to match
         * @param container the list of arrays to test
         * @return true if so, false if not
         */
        public static bool Contains(int[] match, List<int[]> container)
        {
            return container.Any(list => list.SequenceEqual(match));
        }

        public static IEnumerable<double> CumulativeSum(this IEnumerable<double> sequence)
        {
            double sum = 0;
            foreach (var item in sequence)
            {
                sum += item;
                yield return sum;
            }
        }

        public static IEnumerable<double?> CumulativeSum(this IEnumerable<double?> sequence)
        {
            double sum = 0;
            foreach (var item in sequence)
            {
                sum += item.GetValueOrDefault();
                yield return sum;
            }
        }

        /// <summary>
        /// Returns a new array of size first.Length + second.Length, with the
        /// contents of the first array loaded into the returned array starting
        /// at the zero'th index, and the contents of the second array appended
        /// to the returned array beginning with index first.Length.
        /// 
        /// This method is fail fast, meaning that it depends on the two arrays
        /// being non-null, and if not, an exception is thrown.
        /// </summary>
        /// <param name="first"> the data to load starting at index 0</param>
        /// <param name="second">the data to load starting at index first.Length;</param>
        /// <returns>a concatenated array</returns>
        public static double[] Concat(double[] first, double[] second)
        {
            double[] retVal = new double[first.Length + second.Length];
            Array.Copy(first, 0, retVal, 0, first.Length);
            Array.Copy(second, 0, retVal, first.Length, second.Length);
            return retVal;
        }

        /**
         * Utility to compute a flat index from coordinates.
         *
         * @param coordinates an array of integer coordinates
         * @return a flat index
         */
        public static int FromCoordinate(int[] coordinates, int[] shape)
        {
            int[] localMults = InitDimensionMultiples(shape);
            int @base = 0;
            for (int i = 0; i < coordinates.Length; i++)
            {
                @base += (localMults[i] * coordinates[i]);
            }
            return @base;
        }

        /**
         * Utility to compute a flat index from coordinates.
         *
         * @param coordinates an array of integer coordinates
         * @return a flat index
         */
        public static int FromCoordinate(int[] coordinates)
        {
            int[] localMults = InitDimensionMultiples(coordinates);
            int @base = 0;
            for (int i = 0; i < coordinates.Length; i++)
            {
                @base += (localMults[i] * coordinates[i]);
            }
            return @base;
        }

        /**
         * Initializes internal helper array which is used for multidimensional
         * index computation.
         *
         * @param dimensions
         * @return
         */
        internal static int[] InitDimensionMultiples(int[] dimensions)
        {
            int holder = 1;
            int len = dimensions.Length;
            int[] dimensionMultiples = new int[dimensions.Length];
            for (int i = 0; i < len; i++)
            {
                holder *= (i == 0 ? 1 : dimensions[len - i]);
                dimensionMultiples[len - 1 - i] = holder;
            }
            return dimensionMultiples;
        }

        /**
         * Returns a string representing an array of 0's and 1's
         *
         * @param arr an binary array (0's and 1's only)
         * @return
         */
        public static string BitsToString(int[] arr)
        {
            char[] s = new char[arr.Length + 1];
            for (int i = 0; i < s.Length; i++)
            {
                s[i] = '.';
            }
            s[0] = 'c';
            for (int i = 0; i < arr.Length; i++)
            {
                if (arr[i] == 1)
                {
                    s[i + 1] = '*';
                }
            }
            return new string(s);
        }

        /**
         * Return a list of tuples, where each tuple contains the i-th element
         * from each of the argument sequences.  The returned list is
         * truncated in length to the length of the shortest argument sequence.
         *
         * @param arg1 the first list to be the zero'th entry in the returned tuple
         * @param arg2 the first list to be the one'th entry in the returned tuple
         * @return a list of tuples
         */
        public static List<Tuple> Zip<T>(List<T> arg1, List<T> arg2)
        {
            List<Tuple> tuples = new List<Tuple>();
            int len = Math.Min(arg1.Count, arg2.Count);
            for (int i = 0; i < len; i++)
            {
                tuples.Add(new Tuple(arg1[i], arg2[i]));
            }

            return tuples;
        }

        /**
         * Return a list of tuples, where each tuple contains the i-th element
         * from each of the argument sequences.  The returned list is
         * truncated in length to the length of the shortest argument sequence.
         *
         * @param args  the array of ints to be wrapped in {@link Tuple}s
         * @return a list of tuples
         */
        public static List<Tuple> Zip(params int[][] args)
        {
            // Find the array with the minimum size
            int minLength = args.Select(i => i.Length).Min();
            //int minLength = Arrays.stream(args).mapToInt(i->i.length).min().orElse(0);

            return Range(0, minLength).Select(i =>
            {
                Tuple.Builder builder = Tuple.GetBuilder();
                foreach (int[] ia in args)
                {
                    builder.Add(ia[i]);
                }
                return builder.Build();
            }).ToList();

            //return IntStream.range(0, minLength).mapToObj(i-> {
            //    Tuple.Builder builder = Tuple.builder();
            //    for (int[] ia : args)
            //    {
            //        builder.add(ia[i]);
            //    }
            //    return builder.build();
            //}).collect(Collectors.toList());
        }

        public static List<Tuple> Zip<T1, T2>(IEnumerable<T1> col1, IEnumerable<T2> col2)
        {
            // Find the array with the minimum size
            int minLength = Math.Min(col1.Count(), col2.Count());
            List<Tuple> tuples = new List<Tuple>();
            for (int i = 0; i < minLength; i++)
            {
                Tuple.Builder builder = Tuple.GetBuilder();
                builder.Add(col1.ElementAt(i));
                builder.Add(col2.ElementAt(i));
                tuples.Add(builder.Build());
            }
            return tuples;
        }

        public static IEnumerable<Tuple> Zip(params IEnumerable[] cols)
        {
            var enumerators = cols.Select(c => c.GetEnumerator()).ToList();

            Func<bool> doMoveNext = () =>
            {
                return enumerators.All(e => e.MoveNext());
            };

            while (doMoveNext())
            {
                // All enumerators could move to the next position
                // Add their values to a Tuple
                Tuple.Builder builder = Tuple.GetBuilder();
                enumerators.ForEach(e =>
                {
                    builder.Add(e.Current);
                });
                yield return builder.Build();
            }
        }

        /**
         * Returns an array with the same shape and the contents
         * converted to integers.
         *
         * @param doubs an array of doubles.
         * @return
         */
        public static int[] ToIntArray(double[] doubs)
        {
            return doubs.Select(d => (int)d).ToArray();
        }

        public static byte[] ToByteArray(double[] doubs)
        {
            return doubs.Select(d => (byte)d).ToArray();
        }

        /// <summary>
        /// Returns an array with the same shape and the contents converted to doubles.
        /// </summary>
        /// <param name="ints">Array of ints</param>
        /// <returns></returns>
        public static double[] ToDoubleArray(int[] ints)
        {
            return ints.Select(d => (double)d).ToArray();
        }

        /**
         * Performs a modulus operation in Python style.
         *
         * @param a
         * @param b
         * @return
         */
        public static int Modulo(int a, int b)
        {
            if (b == 0) throw new ArgumentException("Division by Zero!");
            if (a > 0 && b > 0 && b > a) return a;
            bool isMinus = Math.Abs(b - (a - b)) < Math.Abs(b - (a + b));
            if (isMinus)
            {
                while (a >= b)
                {
                    a -= b;
                }
            }
            else
            {
                if (a % b == 0) return 0;

                while (a + b < b)
                {
                    a += b;
                }
            }
            return a;
        }

        /**
         * Performs a modulus on every index of the first argument using
         * the second argument and places the result in the same index of
         * the first argument.
         *
         * @param a
         * @param b
         * @return
         */
        public static int[] Modulo(int[] a, int b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                a[i] = Modulo(a[i], b);
            }
            return a;
        }

        /**
         * Returns a double array whose values are the maximum of the value
         * in the array and the max value argument.
         * @param doubs
         * @param maxValue
         * @return
         */
        public static double[] Maximum(double[] doubs, double maxValue)
        {
            double[] retVal = new double[doubs.Length];
            for (int i = 0; i < doubs.Length; i++)
            {
                retVal[i] = Math.Max(doubs[i], maxValue);
            }
            return retVal;
        }

        /**
         * Returns an int array whose values are the maximum of the value
         * in the array and the max value argument.
         * @param doubs
         * @param maxValue
         * @return
         */
        public static int[] Maximum(int[] doubs, int maxValue)
        {
            int[] retVal = new int[doubs.Length];
            for (int i = 0; i < doubs.Length; i++)
            {
                retVal[i] = Math.Max(doubs[i], maxValue);
            }
            return retVal;
        }

        /**
         * Returns an array of identical shape containing the maximum
         * of the values between each corresponding index. Input arrays
         * must be the same length.
         *
         * @param arr1
         * @param arr2
         * @return
         */
        public static int[] MaxBetween(int[] arr1, int[] arr2)
        {
            if (arr1.Length != arr2.Length) throw new InvalidOperationException("Arrays must be the same length!");
            int[] retVal = new int[arr1.Length];
            for (int i = 0; i < arr1.Length; i++)
            {
                retVal[i] = Math.Max(arr1[i], arr2[i]);
            }
            return retVal;
        }



        /**
         * Returns an array of identical shape containing the minimum
         * of the values between each corresponding index. Input arrays
         * must be the same length.
         *
         * @param arr1
         * @param arr2
         * @return
         */
        public static int[] MinBetween(int[] arr1, int[] arr2)
        {
            int[] retVal = new int[arr1.Length];
            for (int i = 0; i < arr1.Length; i++)
            {
                retVal[i] = Math.Min(arr1[i], arr2[i]);
            }
            return retVal;
        }

        /**
         * Returns an array of values that test true for all of the
         * specified {@link Condition}s.
         *
         * @param values
         * @param conditions
         * @return
         */
        public static int[] RetainLogicalAnd(int[] values, params Func<int, bool>[] conditions)
        {
            List<int> l = new List<int>();
            for (int i = 0; i < values.Length; i++)
            {
                bool result = true;
                for (int j = 0; j < conditions.Length && result; j++)
                {
                    result &= conditions[j](values[i]);
                }
                if (result) l.Add(values[i]);
            }
            return l.ToArray();
        }

        /**
         * Returns an array of values that test true for all of the
         * specified {@link Condition}s.
         *
         * @param values
         * @param conditions
         * @return
         */
        public static double[] RetainLogicalAnd(double[] values, params Func<double, bool>[] conditions)
        {
            List<double> l = new List<double>();
            for (int i = 0; i < values.Length; i++)
            {
                bool result = true;
                for (int j = 0; j < conditions.Length && result; j++)
                {
                    result &= conditions[j](values[i]);
                }
                if (result) l.Add(values[i]);
            }
            return l.ToArray();
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor array values.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Divide(double[] dividend, double[] divisor,
            double dividendAdjustment, double divisorAdjustment)
        {

            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }
            double[] quotient = new double[dividend.Length];
            double denom = 1;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i] + dividendAdjustment) /
                              ((denom = divisor[i] + divisorAdjustment) == 0 ? 1 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor array values.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Divide(int[] dividend, int[] divisor)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }
            double[] quotient = new double[dividend.Length];
            double denom = 1;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) /
                              (double)((denom = divisor[i]) == 0 ? 1 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor array values.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Divide(double[] dividend, double[] divisor)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }
            double[] quotient = new double[dividend.Length];
            double denom = 1;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) /
                              (double)((denom = divisor[i]) == 0 ? 1 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor array values.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Divide(double[] dividend, int[] divisor)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }
            double[] quotient = new double[dividend.Length];
            double denom = 1.0;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) /
                              (double)((denom = divisor[i]) == 0.0 ? 1.0 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor value.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Divide(double[] dividend, double divisor)
        {
            double[] quotient = new double[dividend.Length];
            double denom = 1;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) /
                              (double)((denom = divisor) == 0 ? 1 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /**
        * Performs matrix multiplication on the two specified
        * matrices throwing an exception if the two inner dimensions
        * are not in agreement.
        * 
        * @param a     the first matrix
        * @param b     the second matrix
        * @return  the dot product of the two matrices
        * @throws  IllegalArgumentException if the two inner dimensions
        *          are not in agreement
        */
        public static int[][] Dot(int[][] a, int[][] b)
        {
            if (a[0].Length != b.Length)
            {
                throw new ArgumentException("Matrix inner dimensions must agree.");
            }

            //int[][] c = new int[a.Length][b[0].Length];
            int[][] c = CreateJaggedArray<int>(a.Length, b[0].Length);
            int[] bColj = new int[a[0].Length];
            for (int j = 0; j < b[0].Length; j++)
            {
                for (int k = 0; k < a[0].Length; k++)
                {
                    bColj[k] = b[k][j];
                }

                for (int i = 0; i < a.Length; i++)
                {
                    int[] aRowi = a[i];
                    int s = 0;
                    for (int k = 0; k < a[0].Length; k++)
                    {
                        s += aRowi[k] * bColj[k];
                    }
                    c[i][j] = s;
                }
            }

            return c;
        }

        /**
         * Performs matrix multiplication on the two specified
         * matrices throwing an exception if the two inner dimensions
         * are not in agreement. The 1D array is first transposed 
         * into a columnal array and the result is transposed back
         * to yield a 1D array.
         * 
         * @param a
         * @param b
         * @return
         */
        public static int[] Dot(int[][] a, int[] b)
        {
            int[][] result = Dot(a, Transpose(new int[][] { b }));
            return Transpose(result)[0];
        }

        /**
         * Performs matrix multiplication on the two specified
         * matrices throwing an exception if the two inner dimensions
         * are not in agreement. The 1D array is first transposed 
         * into a columnal array and the result is transposed back
         * to yield a 1D array.
         * 
         * @param a
         * @param b
         * @return
         */
        public static double[] Dot(double[][] a, double[] b)
        {
            double[][] result = Dot(a, Transpose(new double[][] { b }));
            return Transpose(result)[0];
        }

        /**
         * Performs matrix multiplication on the two specified
         * matrices throwing an exception if the two inner dimensions
         * are not in agreement.
         * 
         * @param a     the first matrix
         * @param b     the second matrix
         * @return  the dot product of the two matrices
         * @throws  IllegalArgumentException if the two inner dimensions
         *          are not in agreement
         */
        public static double[][] Dot(double[][] a, double[][] b)
        {
            if (a[0].Length != b.Length)
            {
                throw new ArgumentException(
                    "Matrix inner dimensions must agree.");
            }

            //double[][] c = new double[a.Length][b[0].Length];
            double[][] c = CreateJaggedArray<double>(a.Length, b[0].Length);
            double[] bColj = new double[a[0].Length];
            for (int j = 0; j < b[0].Length; j++)
            {
                for (int k = 0; k < a[0].Length; k++)
                {
                    bColj[k] = b[k][j];
                }

                for (int i = 0; i < a.Length; i++)
                {
                    double[] aRowi = a[i];
                    double s = 0;
                    for (int k = 0; k < a[0].Length; k++)
                    {
                        s += aRowi[k] * bColj[k];
                    }
                    c[i][j] = s;
                }
            }

            return c;
        }

        /**
         * Returns an array whose members are the quotient of the dividend array
         * values and the divisor array values.
         *
         * @param dividend
         * @param divisor
         * @param dividend adjustment
         * @param divisor  adjustment
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] RoundDivide(double[] dividend, double[] divisor, int scale)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }
            double[] quotient = new double[dividend.Length];
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) / (divisor[i] == 0 ? 1 : divisor[i]); //Protect against division by 0
                //quotient[i] = new decimal(quotient[i]).Round( new MathContext(scale, RoundingMode.HALF_UP)).doubleValue();
                quotient[i] = (double)Math.Round(new decimal(quotient[i]), scale, MidpointRounding.AwayFromZero);
            }
            return quotient;
        }

        /**
         * Returns an array whose members are the product of the multiplicand array
         * values and the factor array values.
         *
         * @param multiplicand
         * @param factor
         * @param multiplicand adjustment
         * @param factor       adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Multiply(
            double[] multiplicand, double[] factor, double multiplicandAdjustment, double factorAdjustment)
        {

            if (multiplicand.Length != factor.Length)
            {
                throw new ArgumentException(
                    "The multiplicand array and the factor array must be the same length");
            }
            double[] product = new double[multiplicand.Length];
            for (int i = 0; i < multiplicand.Length; i++)
            {
                product[i] = (multiplicand[i] + multiplicandAdjustment) * (factor[i] + factorAdjustment);
            }
            return product;
        }

        /**
         * Returns an array whose members are the product of the multiplicand array
         * values and the factor array values.
         *
         * @param multiplicand
         * @param factor
         * @param multiplicand adjustment
         * @param factor       adjustment
         *
         * @return
         * @throws ArgumentException if the two argument arrays are not the same length
         */
        public static double[] Multiply(double[] multiplicand, int[] factor)
        {

            if (multiplicand.Length != factor.Length)
            {
                throw new ArgumentException(
                    "The multiplicand array and the factor array must be the same length");
            }
            double[] product = new double[multiplicand.Length];
            for (int i = 0; i < multiplicand.Length; i++)
            {
                product[i] = (multiplicand[i]) * (factor[i]);
            }
            return product;
        }

        /**
         * Returns a new array containing the result of multiplying
         * each index of the specified array by the 2nd parameter.
         *
         * @param array
         * @param d
         * @return
         */
        public static int[] Multiply(int[] array, int d)
        {
            int[] product = new int[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                product[i] = array[i] * d;
            }
            return product;
        }

        /**
         * Returns a new array containing the result of multiplying
         * each index of the specified array by the 2nd parameter.
         *
         * @param array
         * @param d
         * @return
         */
        public static double[] Multiply(double[] array, double d)
        {
            double[] product = new double[array.Length];
            for (int i = 0; i < array.Length; i++)
            {
                product[i] = array[i] * d;
            }
            return product;
        }

        public static int MaxIndex(int[] shape)
        {
            return shape[0] * Math.Max(1, InitDimensionMultiples(shape)[0]) - 1;
        }

        /**
         * Returns an integer array containing the result of subtraction
         * operations between corresponding indexes of the specified arrays.
         *
         * @param minuend
         * @param subtrahend
         * @return
         */
        public static int[] Subtract(int[] minuend, int[] subtrahend)
        {
            int[] retVal = new int[minuend.Length];
            for (int i = 0; i < minuend.Length; i++)
            {
                retVal[i] = minuend[i] - subtrahend[i];
            }
            return retVal;
        }

        public static double[] Subtract(double[] minuend, double[] subtrahend)
        {
            double[] retVal = new double[minuend.Length];
            for (int i = 0; i < Math.Min(minuend.Length, subtrahend.Length); i++)
            {
                retVal[i] = minuend[i] - subtrahend[i];
            }
            return retVal;
        }

        /**
         * Subtracts the contents of the first argument from the last argument's list.
         *
         * <em>NOTE: Does not destroy/alter the argument lists. </em>
         *
         * @param minuend
         * @param subtrahend
         * @return
         */
        public static List<int> Subtract(List<int> subtrahend, List<int> minuend)
        {
            List<int> sList = new List<int>(minuend);
            sList.RemoveAll(subtrahend.Contains);
            //sList.RemoveAll(subtrahend);
            return new List<int>(sList);
        }

        /**
         * Returns the average of all the specified array contents.
         * @param arr
         * @return
         */
        public static double Average(int[] arr)
        {
            return arr.AsParallel().Average();
            //int sum = 0;
            //for (int i = 0; i < arr.Length; i++)
            //{
            //    sum += arr[i];
            //}
            //return sum / (double)arr.Length;
        }

        /**
         * Returns the average of all the specified array contents.
         * @param arr
         * @return
         */
        public static double Average(double[] arr)
        {
            return arr.AsParallel().Average();
            //double sum = 0;
            //for (int i = 0; i < arr.Length; i++)
            //{
            //    sum += arr[i];
            //}
            //return sum / (double)arr.Length;
        }

        /**
         * Computes and returns the variance.
         * @param arr
         * @param mean
         * @return
         */
        public static double Variance(double[] arr, double mean)
        {
            double accum = 0.0;
            double dev = 0.0;
            double accum2 = 0.0;
            for (int i = 0; i < arr.Length; i++)
            {
                dev = arr[i] - mean;
                accum += dev * dev;
                accum2 += dev;
            }

            double var = (accum - (accum2 * accum2 / arr.Length)) / arr.Length;

            return var;
        }

        /**
         * Computes and returns the variance.
         * @param arr
         * @return
         */
        public static double Variance(double[] arr)
        {
            return Variance(arr, Average(arr));
        }

        /**
         * Returns the passed in array with every value being altered
         * by the addition of the specified amount.
         *
         * @param arr
         * @param amount
         * @return
         */
        public static int[] Add(int[] arr, int amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount;
            }
            return arr;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the addition of the specified double amount at the same
         * index
         *
         * @param arr
         * @param amount
         * @return
         */
        public static int[] Add(int[] arr, int[] amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount[i];
            }
            return arr;
        }

        public static double[] Add(int[] arr, double[] amount)
        {
            double[] results = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                results[i] = arr[i] + amount[i];
            }
            return results;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the addition of the specified double amount at the same
         * index
         *
         * @param arr
         * @param amount
         * @return
         */
        public static double[] Add(double[] arr, double[] amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount[i];
            }
            return arr;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the addition of the specified double amount
         *
         * @param arr
         * @param amount
         * @return
         */
        public static double[] Add(double[] arr, double amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount;
            }
            return arr;
        }

        /**
         * Returns the sum of all contents in the specified array.
         * @param array
         * @return
         */
        public static int Sum(int[] array)
        {
            return array.AsParallel().Sum();
            //int sum = 0;
            //for (int i = 0; i < array.Length; i++)
            //{
            //    sum += array[i];
            //}
            //return sum;
        }

        /**
         * Test whether each element of a 1-D array is also present in a second 
         * array.
         *
         * Returns a int array whose length is the number of intersections.
         * 
         * @param ar1   the array of values to find in the second array 
         * @param ar2   the array to test for the presence of elements in the first array.
         * @return  an array containing the intersections or an empty array if none are found.
         */
        public static int[] In1d(int[] ar1, int[] ar2)
        {
            if (ar1 == null || ar2 == null)
            {
                return EMPTY_ARRAY;
            }

            HashSet<int> retVal = new HashSet<int>(ar2);
            retVal.IntersectWith(ar1);
            //retVal.RetainAll(ar1);
            return retVal.ToArray();
        }

        /**
         * Returns the sum of all contents in the specified array.
         * @param array
         * @return
         */
        public static double Sum(double[] array)
        {
            return array.AsParallel().Sum();
            //double sum = 0;
            //for (int i = 0; i < array.Length; i++)
            //{
            //    sum += array[i];
            //}
            //return sum;
        }

        public static double[] Sum(double[][] array, int axis)
        {
            /*
            >>> np.sum([[0, 1], [0, 5]], axis=0)
            array([0, 6])
            >>> np.sum([[0, 1], [0, 5]], axis=1)
            array([1, 5])
            */
            switch (axis)
            {
                case 0: // cols
                    {
                        int cols = array[0].Length;
                        double[] result = new double[cols];
                        for (int c = 0; c < cols; c++)
                        {
                            for (int r = 0; r < array.Length; r++)
                            {
                                result[c] += array[r][c];
                            }
                        }
                        return result;
                    }
                case 1: // rows
                    {
                        double[] result = new double[array.Length];
                        for (int r = 0; r < array.Length; r++)
                        {
                            result[r] += array[r].Sum();
                        }
                        return result;
                    }
                default:
                    throw new ArgumentException("axis must be either '0' or '1'");
            }
        }

        /**
         * Sparse or due to the arrays containing the indexes of "on bits",
         * the <em>or</em> of which is equal to the mere combination of the two
         * arguments - eliminating duplicates and sorting.
         *
         * @param arg1
         * @param arg2
         * @return
         */
        public static int[] SparseBinaryOr(int[] arg1, int[] arg2)
        {
            List<int> t = new List<int>(arg1);
            t.AddRange(arg2);
            return Unique(t.ToArray());
        }

        /**
         * Prints the specified array to a returned String.
         *
         * @param aObject the array object to print.
         * @return the array in string form suitable for display.
         */
        public static string Print1DArray(Array array)
        {
            if (array.Rank != 1) throw new ArgumentException("Only 1D arrays supported", nameof(array));

            string joined = "";
            for (int i = 0; i < array.Length; i++)
            {
                var val = array.GetValue(i);
                if (val != null)
                    joined += val.ToString() + ", ";
                else
                    joined += "- ";
            }

            return "[" + joined.TrimEnd(',', ' ') + "]";
        }

        /**
         * Another utility to account for the difference between Python and Java.
         * Here the modulo operator is defined differently.
         *
         * @param n
         * @param divisor
         * @return
         */
        public static double PositiveRemainder(double n, double divisor)
        {
            if (n >= 0)
            {
                return n % divisor;
            }
            else
            {
                double val = divisor + (n % divisor);
                return val == divisor ? 0 : val;
            }
        }

        /**
         * Returns an array which starts from lowerBounds (inclusive) and
         * ends at the upperBounds (exclusive).
         *
         * @param lowerBounds
         * @param upperBounds
         * @return
         */
        public static int[] Range(int lowerBounds, int upperBounds)
        {
            int[] ints = new int[upperBounds - lowerBounds];
            for (int i = lowerBounds, j = 0; i < upperBounds; i++, j++)
            {
                ints[j] = i;
            }
            //return Enumerable.Range(lowerBounds, upperBounds - lowerBounds).ToArray();
            return ints.ToArray();
        }

        /**
         * Returns an array which starts from lowerBounds (inclusive) and
         * ends at the upperBounds (exclusive).
         *
         * @param lowerBounds the starting value
         * @param upperBounds the maximum value (exclusive)
         * @param interval    the amount by which to increment the values
         * @return
         */
        public static double[] Arrange(double lowerBounds, double upperBounds, double interval)
        {
            List<double> doubs = new List<double>();
            for (double i = lowerBounds; i < upperBounds; i += interval)
            {
                doubs.Add(i);
            }
            return doubs.ToArray();
        }

        /**
         * Returns an array which starts from lowerBounds (inclusive) and
         * ends at the upperBounds (exclusive).
         *
         * @param lowerBounds the starting value
         * @param upperBounds the maximum value (exclusive)
         * @param interval    the amount by which to increment the values
         * @return
         */
        public static IEnumerable<int> XRange(int start, int stop, int step)
        {
            // go up
            if (start < stop && stop != -1)
            {
                for (int i = start; i < stop; i += step)
                {
                    yield return i;
                }
            }
            else if (start < stop && stop == -1)
            {
                for (int i = start; ; i += step)
                {
                    yield return i;
                }
            }
            // go down
            else if (start > stop && stop != -1)
            {
                for (int i = start; i > stop; i += step)
                {
                    yield return i;
                }
            }
            else if (start > stop && stop == -1)
            {
                for (int i = start; ; i += step)
                {
                    yield return i;
                }
            }
        }

        /**
         * Fisher-Yates implementation which shuffles the array contents.
         * 
         * @param array     the array of ints to shuffle.
         * @return shuffled array
         */
        public static int[] Shuffle(int[] array)
        {
            int index;
            Random random = new Random(42);
            for (int i = array.Length - 1; i > 0; i--)
            {
                index = random.Next(i + 1);
                if (index != i)
                {
                    array[index] ^= array[i];
                    array[i] ^= array[index];
                    array[index] ^= array[i];
                }
            }
            return array;
        }

        /**
         * Replaces the range specified by "start" and "end" of "orig" with the 
         * array of replacement ints found in "replacement".
         * 
         * @param start         start index of "orig" to be replaced
         * @param end           end index of "orig" to be replaced
         * @param orig          the array containing entries to be replaced by "replacement"
         * @param replacement   the array of ints to put in "orig" in the indicated indexes
         * @return
         */
        public static int[] Replace(int start, int end, int[] orig, int[] replacement)
        {
            for (int i = start, j = 0; i < end; i++, j++)
            {
                orig[i] = replacement[j];
            }
            return orig;
        }

        /**
         * Returns a sorted unique array of integers
         *
         * @param nums an unsorted array of integers with possible duplicates.
         * @return
         */
        public static int[] Unique(int[] nums)
        {
            //HashSet<int> set = new HashSet<int>(nums);
            //int[] result = set.ToArray();
            //Array.Sort(result);
            //return result;
            int[] result = nums.Distinct().ToArray();
            Array.Sort(result);
            return result;
        }

        /**
         * Helper Class for recursive coordinate assembling
         */
        private class CoordinateAssembler
        {
            private readonly int[] position;
            private readonly List<int[]> dimensions;
            private readonly List<int[]> result = new List<int[]>();

            public static List<int[]> Assemble(List<int[]> dimensions)
            {
                CoordinateAssembler assembler = new CoordinateAssembler(dimensions);
                assembler.Process(dimensions.Count);
                return assembler.result;
            }

            private CoordinateAssembler(List<int[]> dimensions)
            {
                this.dimensions = dimensions;
                position = new int[dimensions.Count];
            }

            private void Process(int level)
            {
                if (level == 0)
                {// terminating condition
                    int[] coordinates = new int[position.Length];
                    Array.Copy(position, 0, coordinates, 0, position.Length);
                    result.Add(coordinates);
                }
                else
                {// inductive condition
                    int index = dimensions.Count - level;
                    int[] currentDimension = dimensions[index];
                    for (int i = 0; i < currentDimension.Length; i++)
                    {
                        position[index] = currentDimension[i];
                        Process(level - 1);
                    }
                }
            }
        }


        /**
         * Called to merge a list of dimension arrays into a sequential row-major indexed
         * list of coordinates.
         *
         * @param dimensions a list of dimension arrays, each array being a dimension
         *                   of an n-dimensional array.
         * @return a list of n-dimensional coordinates in row-major format.
         */
        public static List<int[]> DimensionsToCoordinateList(List<int[]> dimensions)
        {
            return CoordinateAssembler.Assemble(dimensions);
        }

        /**
         * Sets the values in the specified values array at the indexes specified,
         * to the value "setTo".
         *
         * @param values  the values to alter if at the specified indexes.
         * @param indexes the indexes of the values array to alter
         * @param setTo   the value to set at the specified indexes.
         */
        public static void SetIndexesTo(double[] values, int[] indexes, double setTo)
        {
            foreach (int index in indexes)
            {
                values[index] = setTo;
            }
        }

        /**
         * Sets the values in the specified values array at the indexes specified,
         * to the value "setTo".
         *
         * @param values  the values to alter if at the specified indexes.
         * @param indexes the indexes of the values array to alter
         * @param setTo   the value to set at the specified indexes.
         */
        public static void SetIndexesTo(int[] values, int[] indexes, int setTo)
        {
            foreach (int index in indexes)
            {
                values[index] = setTo;
            }
        }

        /**
         * Sets the values in range start to stop to the value specified. If
         * stop &lt; 0, then stop indicates the number of places counting from the
         * length of "values" back.
         *
         * @param values the array to alter
         * @param start  the start index (inclusive)
         * @param stop   the end index (exclusive)
         * @param setTo  the value to set the indexes to
         */
        public static void SetRangeTo(int[] values, int start, int stop, int setTo)
        {
            stop = stop < 0 ? values.Length + stop : stop;
            for (int i = start; i < stop; i++)
            {
                values[i] = setTo;
            }
        }

        /**
         * Sets the values in range start to stop to the value specified. If
         * stop &lt; 0, then stop indicates the number of places counting from the
         * length of "values" back.
         *
         * @param values the array to alter
         * @param start  the start index (inclusive)
         * @param stop   the end index (exclusive)
         * @param setTo  the value to set the indexes to
         */
        public static void SetRangeTo(double[] values, int start, int stop, double setTo)
        {
            stop = stop < 0 ? values.Length + stop : stop;
            for (int i = start; i < stop; i++)
            {
                values[i] = setTo;
            }
        }

        /**
         * Returns a random, sorted, and  unique array of the specified sample size of
         * selections from the specified list of choices.
         *
         * @param sampleSize the number of selections in the returned sample
         * @param choices    the list of choices to select from
         * @param random     a random number generator
         * @return a sample of numbers of the specified size
         */
        public static int[] Sample(int[] choices, ref int[] selectedIndices, IRandom random)
        {
            List<int> supply = new List<int>(choices);

            int[] chosen = supply.SelectCombination(selectedIndices.Length, (Random) random).ToArray();

            //List<int> choiceSupply = new List<int>(choices);
            //int upperBound = choices.Length;
            //for (int i = 0; i < selectedIndices.Length; i++)
            //{
            //    int randomIdx = random.NextInt(upperBound);
            //    int removedVal = choiceSupply[randomIdx];
            //    choiceSupply.RemoveAt(randomIdx);
            //    selectedIndices[i] = removedVal;
            //    upperBound--;
            //}
            selectedIndices = chosen; // use ref so we can replace this instance
            Array.Sort(selectedIndices);

            return selectedIndices;
        }

        /**
         * Returns a random, sorted, and  unique array of the specified sample size of
         * selections from the specified list of choices.
         *
         * @param sampleSize the number of selections in the returned sample
         * @param choices    the list of choices to select from
         * @param random     a random number generator
         * @return a sample of numbers of the specified size
         */
        public static int[] Sample(int sampleSize, List<int> choices, IRandom random)
        {
            HashSet<int> temp = new HashSet<int>();
            int upperBound = choices.Count;
            for (int i = 0; i < sampleSize; i++)
            {
                int randomIdx = random.NextInt(upperBound);
                while (temp.Contains(choices[randomIdx]))
                {
                    randomIdx = random.NextInt(upperBound);
                }
                temp.Add(choices[randomIdx]);
            }
            List<int> al = new List<int>(temp);
            al.Sort();
            return al.ToArray();
        }

        /**
         * Returns a double[] filled with random doubles of the specified size.
         * @param sampleSize
         * @param random
         * @return
         */
        public static double[] Sample(int sampleSize, IRandom random)
        {
            double[] sample = new double[sampleSize];
            for (int i = 0; i < sampleSize; i++)
            {
                sample[i] = random.NextDouble();
            }
            return sample;
        }

        /**
         * Ensures that each entry in the specified array has a min value
         * equal to or greater than the specified min and a maximum value less
         * than or equal to the specified max.
         *
         * @param values the values to clip
         * @param min    the minimum value
         * @param max    the maximum value
         */
        public static double[] Clip(double[] values, double min, double max)
        {
            Parallel.For(0, values.Length, i =>
            {
                values[i] = Math.Min(1, Math.Max(0, values[i]));
            });


            //for (int i = 0; i < values.Length; i++)
            //{
            //    values[i] = Math.Min(1, Math.Max(0, values[i]));
            //}
            return values;
        }

        /**
         * Ensures that each entry in the specified array has a min value
         * equal to or greater than the specified min and a maximum value less
         * than or equal to the specified max.
         *
         * @param values the values to clip
         * @param min    the minimum value
         * @param max    the maximum value
         */
        public static int[] Clip(int[] values, int min, int max)
        {
            Parallel.For(0, values.Length, i =>
            {
                values[i] = Math.Min(max, Math.Max(min, values[i]));
            });


            //for (int i = 0; i < values.Length; i++)
            //{
            //    values[i] = Math.Min(1, Math.Max(0, values[i]));
            //}
            return values;
        }

        /**
         * Ensures that each entry in the specified array has a min value
         * equal to or greater than the min at the specified index and a maximum value less
         * than or equal to the max at the specified index.
         *
         * @param values the values to clip
         * @param min    the minimum value
         * @param max    the maximum value
         */
        public static int[] Clip(int[] values, int[] min, int[] max)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(min[i], Math.Min(max[i], values[i]));
            }
            return values;
        }

        /**
         * Ensures that each entry in the specified array has a min value
         * equal to or greater than the min at the specified index and a maximum value less
         * than or equal to the max at the specified index.
         *
         * @param values the values to clip
         * @param max    the minimum value
         * @param adj    the adjustment amount
         */
        public static int[] Clip(int[] values, int[] max, int adj)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] = Math.Max(0, Math.Min(max[i] + adj, values[i]));
            }
            return values;
        }

        /**
         * Returns the count of values in the specified array that are
         * greater than the specified compare value
         *
         * @param compare the value to compare to
         * @param array   the values being compared
         *
         * @return the count of values greater
         */
        public static int ValueGreaterCount(double compare, double[] array)
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > compare)
                {
                    count++;
                }
            }

            return count;
        }

        public static int ValueGreaterOrEqualCount(double compare, double[] array)
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] >= compare)
                {
                    count++;
                }
            }

            return count;
        }

        /**
         * Returns the count of values in the specified array that are
         * greater than the specified compare value
         *
         * @param compare the value to compare to
         * @param array   the values being compared
         *
         * @return the count of values greater
         */
        public static int ValueGreaterCountAtIndex(double compare, double[] array, int[] indexes)
        {
            int count = 0;
            for (int i = 0; i < indexes.Length; i++)
            {
                if (array[indexes[i]] > compare)
                {
                    count++;
                }
            }

            return count;
        }

        /**
         * Returns an array containing the n greatest values.
         * @param array
         * @param n
         * @return
         */
        public static int[] NGreatest(double[] array, int n)
        {
            Map<double, int> places = new Map<double, int>();
            int i;
            double key;
            for (int j = 1; j < array.Length; j++)
            {
                key = array[j];
                for (i = j - 1; i >= 0 && array[i] < key; i--)
                {
                    array[i + 1] = array[i];
                }
                array[i + 1] = key;
                places.Add(key, j);
            }

            int[] retVal = new int[n];
            for (i = 0; i < n; i++)
            {
                retVal[i] = places[array[i]];
            }
            return retVal;
        }

        /**
         * Raises the values in the specified array by the amount specified
         * @param amount the amount to raise the values
         * @param values the values to raise
         */
        public static void RaiseValuesBy(double amount, double[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] += amount;
            }
        }

        /**
         * Raises the values at the indexes specified by the amount specified.
         * @param amount the amount to raise the values
         * @param values the values to raise
         */
        public static void RaiseValuesBy(double amount, double[] values, int[] indexesToRaise)
        {
            for (int i = 0; i < indexesToRaise.Length; i++)
            {
                values[indexesToRaise[i]] += amount;
            }
        }

        /**
         * Raises the values at the indexes specified by the amount specified.
         * @param amounts the amounts to raise the values
         * @param values the values to raise
         */
        public static void RaiseValuesBy(double[] amounts, double[] values)
        {
            for (int i = 0; i < values.Length; i++)
            {
                values[i] += amounts[i];
            }
        }

        /**
         * Raises the values at the indicated indexes, by the amount specified
         *
         * @param amount
         * @param indexes
         * @param values
         */
        public static void RaiseValuesBy(int amount, int[] indexes, int[] values)
        {
            for (int i = 0; i < indexes.Length; i++)
            {
                values[indexes[i]] += amount;
            }
        }

        ///**
        // * Scans the specified values and applies the {@link Condition} to each
        // * value, returning the indexes of the values where the condition evaluates
        // * to true.
        // *
        // * @param values the values to test
        // * @param c      the condition used to test each value
        // * @return
        // */
        //public static int[] Where<T>(double[] values, ICondition<T> c)
        //{
        //    List<int> retVal = new List<int>();
        //    int len = values.Length;
        //    for (int i = 0; i < len; i++)
        //    {
        //        if (c.eval(values[i]))
        //        {
        //            retVal.Add(i);
        //        }
        //    }
        //    return retVal.ToArray();
        //}

        /**
         * Scans the specified values and applies the {@link Condition} to each
         * value, returning the indexes of the values where the condition evaluates
         * to true.
         *
         * @param values the values to test
         * @param c      the condition used to test each value
         * @return
         */
        public static int[] Where<T>(IEnumerable<T> values, Func<T, bool> condition)
        {
            return values
                .Select((n, i) => new { Index = i, Value = n })
                .Where(a => condition(a.Value))
                .Select(a => a.Index).ToArray();

            //List<int> retVal = new List<int>();
            //int len = values.Length;
            //for (int i = 0; i < len; i++)
            //{
            //    if (c.eval(values[i]))
            //    {
            //        retVal.Add(i);
            //    }
            //}
            //return retVal.ToArray();
        }

        /**
         * Returns a flag indicating whether the specified array
         * is a sparse array of 0's and 1's or not.
         * 
         * @param ia
         * @return
         */
        public static bool IsSparse(int[] ia)
        {
            if (ia == null || ia.Length < 3) return false;
            int end = ia[ia.Length - 1];
            for (int i = ia.Length - 1, j = 0; i >= 0; i--, j++)
            {
                if (ia[i] > 1) return true;
                else if (j > 0 && ia[i] == end) return false;
            }

            return false;
        }

        /**
         * Returns a bit vector of the specified size whose "on" bit
         * indexes are specified in "in"; basically converting a sparse
         * array to a dense one.
         * 
         * @param in       the sparse array specifying the on bits of the returned array
         * @param size    the size of the dense array to be returned.
         * @return
         */
        public static int[] AsDense(int[] @in, int size)
        {
            int[] retVal = new int[size];
            new List<int>(@in).AsParallel().ForAll(i => retVal[i] = 1);
            //Arrays.stream(in).forEach(i-> { retVal[i] = 1; });
            return retVal;
        }

        ///**
        // * Scans the specified values and applies the {@link Condition} to each
        // * value, returning the indexes of the values where the condition evaluates
        // * to true.
        // *
        // * @param values the values to test
        // * @param c      the condition used to test each value
        // * @return
        // */
        //public static int[] Where<T>(List<T> values, ICondition<T> c)
        //{
        //    List<int> retVal = new List<int>();
        //    int len = values.Count;
        //    for (int i = 0; i < len; i++)
        //    {
        //        if (c.eval(values[i]))
        //        {
        //            retVal.Add(i);
        //        }
        //    }
        //    return retVal.ToArray();
        //}

        ///**
        // * Scans the specified values and applies the {@link Condition} to each
        // * value, returning the indexes of the values where the condition evaluates
        // * to true.
        // *
        // * @param values the values to test
        // * @param c      the condition used to test each value
        // * @return
        // */
        //public static int[] Where<T>(T[] values, ICondition<T> c)
        //{
        //    List<int> retVal = new List<int>();
        //    for (int i = 0; i < values.Length; i++)
        //    {
        //        if (c.eval(values[i]))
        //        {
        //            retVal.Add(i);
        //        }
        //    }
        //    return retVal.ToArray();
        //}

        /**
         * Makes all values in the specified array which are less than or equal to the specified
         * "x" value, equal to the specified "y".
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void LessThanOrEqualXThanSetToY(double[] array, double x, double y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] <= x) array[i] = y;
            }
        }

        /**
         * Makes all values in the specified array which are less than the specified
         * "x" value, equal to the specified "y".
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void LessThanXThanSetToY(double[] array, double x, double y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < x) array[i] = y;
            }
        }

        /**
         * Makes all values in the specified array which are less than the specified
         * "x" value, equal to the specified "y".
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void LessThanXThanSetToY(int[] array, int x, int y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < x) array[i] = y;
            }
        }

        /**
         * Makes all values in the specified array which are greater than or equal to the specified
         * "x" value, equal to the specified "y".
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void GreaterThanOrEqualXThanSetToY(double[] array, double x, double y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] >= x) array[i] = y;
            }
        }

        /**
         * Makes all values in the specified array which are greater than the specified
         * "x" value, equal to the specified "y".
         *
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void GreaterThanXThanSetToY(double[] array, double x, double y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > x) array[i] = y;
            }
        }

        /**
         * Makes all values in the specified array which are greater than the specified
         * "x" value, equal to the specified "y".
         * @param array
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void GreaterThanXThanSetToY(int[] array, int x, int y)
        {
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > x) array[i] = y;
            }
        }

        /**
         * Sets value to "y" in "targetB" if the value in the same index in "sourceA" is bigger than "x".
         * @param sourceA array to compare elements with X
         * @param targetB array to set elements to Y
         * @param x     the comparison
         * @param y     the value to set if the comparison fails
         */
        public static void GreaterThanXThanSetToYInB(int[] sourceA, double[] targetB, int x, double y)
        {
            for (int i = 0; i < sourceA.Length; i++)
            {
                if (sourceA[i] > x)
                    targetB[i] = y;
            }
        }

        /// <summary>
        /// Returns the index of the max value in the specified array
        /// </summary>
        /// <param name="array">the array to find the max value index in</param>
        /// <returns>the index of the max value</returns>
        public static int Argmax(int[] array)
        {
            int index = -1;
            int max = int.MinValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > max)
                {
                    max = array[i];
                    index = i;
                }
            }
            return index;
        }

        /**
         * Returns the index of the min value in the specified array
         * @param array the array to find the min value index in
         * @return the index of the min value
         */
        public static int Argmin(int[] array)
        {
            int index = -1;
            int min = int.MaxValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < min)
                {
                    min = array[i];
                    index = i;
                }
            }
            return index;
        }

        /// <summary>
        /// Returns the index of the max value in the specified array
        /// </summary>
        /// <param name="array">the array to find the max value index in</param>
        /// <returns>the index of the max value</returns>
        public static int Argmax(double[] array)
        {
            int index = -1;
            double max = double.MinValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] > max)
                {
                    max = array[i];
                    index = i;
                }
            }
            return index;
        }

        /// <summary>
        /// Returns the index of the max value in the specified array
        /// </summary>
        /// <param name="array">the array to find the max value index in</param>
        /// <returns>the index of the max value</returns>
        public static int Argmax(IEnumerable<double> array)
        {
            int index = -1;
            double max = double.MinValue;

            int i = 0;
            foreach (double value in array)
            {
                if (value > max)
                {
                    max = value;
                    index = i;
                }
                i++;
            }
            return index;
        }

        /**
         * Returns the index of the min value in the specified array
         * @param array the array to find the min value index in
         * @return the index of the min value
         */
        public static int Argmin(double[] array)
        {
            int index = -1;
            double min = double.MaxValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < min)
                {
                    min = array[i];
                    index = i;
                }
            }
            return index;
        }

        /**
         * Returns the maximum value in the specified array
         * @param array
         * @return
         */
        public static int Max(int[] array)
        {
            return array.AsParallel().Max();
            //int max = int.MinValue;
            //for (int i = 0; i < array.Length; i++)
            //{
            //    if (array[i] > max)
            //    {
            //        max = array[i];
            //    }
            //}
            //return max;
        }

        /**
         * Returns the maximum value in the specified array
         * @param array
         * @return
         */
        public static double Max(double[] array)
        {
            return array.AsParallel().Max();
            //double max = double.MinValue;
            //for (int i = 0; i < array.Length; i++)
            //{
            //    if (array[i] > max)
            //    {
            //        max = array[i];
            //    }
            //}
            //return max;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the subtraction of the specified double amount
         *
         * @param arr
         * @param amount
         * @return
         */
        public static double[] Sub(double[] arr, double amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] -= amount;
            }
            return arr;
        }

        internal static double[][] SubstractRows(double[][] matrix, double[] vector)
        {
            double[][] retVal = (double[][])matrix.Clone();
            for (int row = 0; row < retVal.Length; row++)
            {
                for (int col = 0; col < retVal[row].Length; col++)
                {
                    retVal[row][col] = matrix[row][col] - vector[col];
                }
            }
            return retVal;
        }

        internal static double[][] Sub(double[][] arr, double[] amount)
        {
            double[][] retVal = (double[][])arr.Clone();
            for (int row = 0; row < retVal.Length; row++)
            {
                for (int col = 0; col < retVal[row].Length; col++)
                {
                    if (retVal.Length == amount.Length)
                        retVal[row][col] = arr[row][col] - amount[row];
                    else
                        retVal[row][col] = arr[row][col] - amount[col];
                }
            }
            return retVal;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the subtraction of the specified double amount
         *
         * @param arr
         * @param amount
         * @return
         */
        public static double[] Sub(double amount, double[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = amount - arr[i];
            }
            return arr;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the subtraction of the specified double amount
         *
         * @param arr
         * @param amount
         * @return
         */
        public static int[] Sub(int[] arr, int amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] -= amount;
            }
            return arr;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the subtraction of the specified double amount
         *
         * @param arr
         * @param amount
         * @return
         */
        public static int[] Sub(int amount, int[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = amount - arr[i];
            }
            return arr;
        }

        /**
         * Returns the passed in array with every value being altered
         * by the subtraction of the specified double amount at the same
         * index
         *
         * @param arr
         * @param amount
         * @return
         */
        public static double[] Sub(double[] arr, double[] amount)
        {
            for (int i = 0; i < Math.Min(arr.Length, amount.Length); i++)
            {
                arr[i] -= amount[i];
            }
            return arr;
        }

        /**
         * Returns a new array containing the items specified from
         * the source array by the indexes specified.
         *
         * @param source
         * @param indexes
         * @return
         */
        public static double[] Sub(double[] source, int[] indexes)
        {
            double[] retVal = new double[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                retVal[i] = source[indexes[i]];
            }
            return retVal;
        }

        /**
         * Returns a new array containing the items specified from
         * the source array by the indexes specified.
         *
         * @param source
         * @param indexes
         * @return
         */
        public static int[] Sub(int[] source, int[] indexes)
        {
            int[] retVal = new int[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                retVal[i] = source[indexes[i]];
            }
            return retVal;
        }

        /**
         * Returns a new 2D array containing the items specified from
         * the source array by the indexes specified.
         *
         * @param source
         * @param indexes
         * @return
         */
        public static int[][] Sub(int[][] source, int[] indexes)
        {
            int[][] retVal = new int[indexes.Length][];
            for (int i = 0; i < indexes.Length; i++)
            {
                retVal[i] = source[indexes[i]];
            }
            return retVal;
        }

        /**
         * Takes an input array of m rows and n columns, and transposes it to form an array
         * of n rows and m columns. The value in location [i][j] of the input array is copied
         * into location [j][i] of the new array.
         * 
         * @param array The array to transpose.
         * @return The transposed array.
         */
        public static int[][] Transpose(int[][] array)
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<int>(0, 0); //new int[0][0]; // Special case: zero-length array
            }
            int c = array[0].Length;
            //int[][] result = new int[c][r];
            int[][] result = CreateJaggedArray<int>(c, r);
            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[j][i] = array[i][j];
                }
            }
            return result;
        }

        /**
         * Takes an input array of m rows and n columns, and transposes it to form an array
         * of n rows and m columns. The value in location [i][j] of the input array is copied
         * into location [j][i] of the new array.
         * 
         * @param array The array to transpose.
         * @return The transposed array.
         */
        public static double[][] Transpose(double[][] array)
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<double>(0, 0); //new double[0][0]; // Special case: zero-length array
            }
            int c = array[0].Length;
            //double[][] result = new double[c][r];
            double[][] result = CreateJaggedArray<double>(c, r);
            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[j][i] = array[i][j];
                }
            }
            return result;
        }

        /**
         * Transforms 2D matrix of doubles to 1D by concatenation
         * @param A
         * @return
         */
        public static double[] To1D(double[][] A)
        {

            double[] B = new double[A.Length * A[0].Length];
            int index = 0;

            for (int i = 0; i < A.Length; i++)
            {
                for (int j = 0; j < A[0].Length; j++)
                {
                    B[index++] = A[i][j];
                }
            }
            return B;
        }

        /**
         * Transforms 2D matrix of integers to 1D by concatenation
         * @param A
         * @return
         */
        public static int[] To1D(int[][] A)
        {

            int[] B = new int[A.Length * A[0].Length];
            int index = 0;

            for (int i = 0; i < A.Length; i++)
            {
                for (int j = 0; j < A[0].Length; j++)
                {
                    B[index++] = A[i][j];
                }
            }
            return B;
        }

        /**
         * Returns the minimum value in the specified array
         * @param array
         * @return
         */
        public static int Min(int[] array)
        {
            int min = int.MaxValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < min)
                {
                    min = array[i];
                }
            }
            return min;
        }

        /**
         * Returns the minimum value in the specified array
         * @param array
         * @return
         */
        public static double Min(double[] array)
        {
            double min = double.MaxValue;
            for (int i = 0; i < array.Length; i++)
            {
                if (array[i] < min)
                {
                    min = array[i];
                }
            }
            return min;
        }

        /**
         * Returns a copy of the specified integer array in
         * reverse order
         *
         * @param d
         * @return
         */
        public static int[] Reverse(int[] d)
        {
            int[] clone = (int[])d.Clone();
            Array.Reverse(clone);
            return clone;
            //int[] ret = new int[d.Length];
            //for (int i = 0, j = d.Length - 1; j >= 0; i++, j--)
            //{
            //    ret[i] = d[j];
            //}
            //return ret;
        }

        /**
         * Returns a copy of the specified double array in
         * reverse order
         *
         * @param d
         * @return
         */
        public static double[] Reverse(double[] d)
        {
            double[] ret = new double[d.Length];
            for (int i = 0, j = d.Length - 1; j >= 0; i++, j--)
            {
                ret[i] = d[j];
            }
            return ret;
        }

        /**
         * Returns a new int array containing the or'd on bits of
         * both arg1 and arg2.
         *
         * @param arg1
         * @param arg2
         * @return
         */
        public static int[] Or(int[] arg1, int[] arg2)
        {
            int[] retVal = new int[Math.Max(arg1.Length, arg2.Length)];
            for (int i = 0; i < arg1.Length; i++)
            {
                retVal[i] = arg1[i] > 0 || arg2[i] > 0 ? 1 : 0;
            }
            return retVal;
        }

        /**
         * Returns a new int array containing the and'd bits of
         * both arg1 and arg2.
         *
         * @param arg1
         * @param arg2
         * @return
         */
        public static int[] And(int[] arg1, int[] arg2)
        {
            int[] retVal = new int[Math.Max(arg1.Length, arg2.Length)];
            for (int i = 0; i < arg1.Length; i++)
            {
                retVal[i] = arg1[i] > 0 && arg2[i] > 0 ? 1 : 0;
            }
            return retVal;
        }

        /**
         * Copies the passed array <tt>original</tt>  into a new array except first element and returns it
         *
         * @param original the array from which a tail is taken
         * @return a new array containing the tail from the original array
         */
        public static int[] Tail(int[] original)
        {
            int[] range = new int[original.Length - 1];
            Array.Copy(original, 1, range, 0, range.Length);
            //return Arrays.CopyOfRange(original, 1, original.Length);
            return range;
        }

        /**
         * Set <tt></tt>value for <tt>array</tt> at specified position <tt>indexes</tt>
         *
         * @param array
         * @param value
         * @param indexes
         */
        public static void SetValue<T>(Array array, T value, params int[] indexes)
            where T : struct
        {
            if (indexes.Length == 1)
            {
                if (array.Rank == 1)
                {
                    Buffer.BlockCopy(new[] { value }, 0, array, indexes[0] * Marshal.SizeOf<T>(), 1);
                    array.SetValue(value, indexes[0]);
                }
                if (array.Rank == 2)
                {
                    array.SetRow(value, indexes[0]);
                }
            }
            if (indexes.Length == 2)
            {
                T[,] arr = (T[,])array;
                arr[indexes[0], indexes[1]] = value;
            }
            else
            {
                //SetValue((Array) array.GetValue(indexes[0]), value, Tail(indexes));
                array.SetValue(value, indexes);
                //throw new NotImplementedException("Take a look at the line below and the arguments comming through, array is object in java");
                //SetValue(Array.get(array, indexes[0]), value, tail(indexes));
            }
        }

        public static void SetValueToFlatMatrix<T>(T[] array, T value, int[] dimensions, params int[] indexes)
        {
            int setDimensions = dimensions.Length - indexes.Length;
            if (setDimensions == 0 && indexes.Length == 2)
            {
                array[dimensions[0] * indexes[0] + indexes[1]] = value;
            }
            if (setDimensions == 1 && indexes.Length == 2)
            {
                T[] setVal = new T[dimensions.Last()];
                for (int i = 0; i < setVal.Length; i++)
                {
                    setVal[i] = value;
                }
                Buffer.BlockCopy(setVal, 0, array, (indexes[0] * dimensions.First()) * Marshal.SizeOf<T>(),
                    Marshal.SizeOf<T>() * setVal.Length);
            }
            throw new NotImplementedException();
        }

        /**
         * Get <tt>value</tt> for <tt>array</tt> at specified position <tt>indexes</tt>
         *
         * @param array
         * @param indexes
         */
        public static object GetValue<T>(Array array, params int[] indexes)
        {
            //Array slice = array;
            //for (int i = 0; i < indexes.Length; i++)
            //{
            //    //slice = Array.get(slice, indexes[i]);
            //    //slice = slice.GetValue(slice, indexes[i]);
            //    throw new NotImplementedException("Take a look at the line above and the arguments comming through, array is object in java");
            //}
            return array.GetDimensionData<T>(indexes);
            //if (array.Rank == 1)
            //{
            //    return array.GetValue(indexes[0]);
            //}
            //if (array.Rank == 2)
            //{
            //    if (indexes.Length == array.Rank)
            //    {
            //        return array.GetValue(indexes.First(), indexes.Last());
            //    }
            //    if (indexes.Length < array.Rank)
            //    {
            //        return array.GetDimensionData<T>(indexes);
            //    }

            //    throw new InvalidOperationException("To many indexes given? " + indexes.Length);
            //}

            //return slice;
        }

        public static object GetValueFromFlatMatrix<T>(T[] array, int[] dimensions, params int[] indexes)
        {
            int retValDimensions = dimensions.Length - indexes.Length;
            if (retValDimensions == 0 && dimensions.Length == 2)
            {
                return array[dimensions[0] * indexes[0] + indexes[1]];
            }
            if (retValDimensions == 1 && dimensions.Length == 2)
            {
                T[] retVal = new T[dimensions.Last()];
                Buffer.BlockCopy(array, (indexes[0] * dimensions.First()) * Marshal.SizeOf<T>(),
                        retVal, 0, Marshal.SizeOf<T>() * retVal.Length);
                return retVal;
            }
            throw new NotImplementedException();
        }

        /**
         *Assigns the specified int value to each element of the specified any dimensional array
         * of ints.
         * @param array
         * @param value
         */
        public static void FillArray(Array array, int value)
        {
            if (array.Rank == 1)
            {
                if (array is int[])
                {
                    Arrays.Fill((int[])array, value);
                }
                else
                {
                    // Jagged
                    foreach (var item in array)
                    {
                        FillArray((Array)item, value);
                    }
                }
            }
            if (array.Rank == 2)
            {
                int[,] arr = (int[,])array;
                for (int i = 0; i < array.GetLength(0); i++)
                {
                    for (int j = 0; j < array.GetLength(1); j++)
                    {
                        arr[i, j] = value;
                    }
                }
            }
            if (array.Rank == 3)
            {
                int[,,] arr = (int[,,])array;
                for (int i = 0; i < array.GetLength(0); i++)
                {
                    for (int j = 0; j < array.GetLength(1); j++)
                    {
                        for (int k = 0; k < array.GetLength(2); k++)
                        {
                            arr[i, j, k] = value;
                        }
                    }
                }
            }
            //throw new NotImplementedException("Check implementation");
            //if (array instanceof int[]) {
            //    Arrays.fill((int[])array, value);
            //} else {
            //    for (Object agr : (Object[])array)
            //    {
            //        fillArray(agr, value);
            //    }
            //}
        }

        /**
        * Aggregates all element of multi dimensional array of ints
        * @param array
        * @return sum of all array elements
*/
        public static int AggregateArray(object array)
        {
            int sum = 0;
            if (array is int)
            {
                return (int)array;
            }
            else if (array is short)
            {
                return (short)array;
            }
            else if (array is byte)
            {
                return (byte)array;
            }
            else if (array is byte[])
            {
                byte[] set = (byte[])array;
                //for (int element : set)
                foreach (int element in set)
                {
                    sum += element;
                }
                return sum;
            }
            else if (array is int[])
            {
                int[] set = (int[])array;
                //for (int element : set)
                foreach (int element in set)
                {
                    sum += element;
                }
                return sum;
            }
            else if (array is short[])
            {
                short[] set = (short[])array;
                //for (int element : set)
                foreach (int element in set)
                {
                    sum += element;
                }
                return sum;
            }
            else
            {
                //for (Object agr : (Object[])array)
                foreach (object agr in (Array)array)
                {
                    sum += AggregateArray(agr);
                }
                return sum;
            }
        }

        /**
         * Convert multidimensional array to readable String
         * @param array
         * @return String representation of array
         */
        public static string IntArrayToString(object array)
        {
            StringBuilder result = new StringBuilder();
            if (array.GetType().IsArray && ((Array)array).Rank > 1)
            {
                result.Append(Arrays.DeepToString((Array)array));
                // result.Append(Arrays.deepToString((Object[])array));
            }
            else
            {
                // One dimension
                result.Append(Arrays.ToArrayString((Array)array));
            }
            return result.ToString();
        }

        public static string IntArrayToString(SparseByteArray array)
        {
            StringBuilder result = new StringBuilder();
            if (array.Rank > 1)
            {
                throw new NotImplementedException();
                //result.Append(Arrays.DeepToString((Array)array));
                // result.Append(Arrays.deepToString((Object[])array));
            }
            else
            {
                // One dimension
                result.Append(Arrays.ToArrayString((Array)array.AsDense().ToArray()));
            }
            return result.ToString();
        }

        /**
         * Return True if all elements of the  <tt>values</tt> have evaluated to true with <tt>condition</tt>
         * @param values
         * @param condition
         * @param <T>
         * @return
         */
        public static bool All<T>(T[] values, Func<T, bool> condition)
        {
            return values.All(condition);
            //for (int element : values)
            //{
            //    if (!condition.eval(element))
            //    {
            //        return false;
            //    }
            //}
            //return true;
        }

        /**
         * Concat arrays
         *
         * @return The concatenated array
         *
         * http://stackoverflow.com/a/784842
         */
        public static T[] ConcatAll<T>(T[] first, params T[][] rest)
        {
            int totalLength = first.Length;
            foreach (T[] array in rest)
            //for (T[] array : rest)
            {
                totalLength += array.Length;
            }
            T[] result = Arrays.CopyOf(first, totalLength);
            int offset = first.Length;
            //for (T[] array : rest)
            foreach (T[] array in rest)
            {
                Array.Copy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }
            return result;
        }

        /**
         * Concat int arrays
         *
         * @return The concatenated array
         *
         * http://stackoverflow.com/a/784842
         */
        public static int[] ConcatAll(int[] first, params int[][] rest)
        {
            int totalLength = first.Length;
            //for (int[] array : rest)
            foreach (int[] array in rest)
            {
                totalLength += array.Length;
            }
            int[] result = Arrays.CopyOf(first, totalLength);
            int offset = first.Length;
            //for (int[] array : rest)
            foreach (int[] array in rest)
            {
                Array.Copy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }
            return result;
        }

        /**
         * Takes a two-dimensional input array and returns a new array which is "rotated"
         * a quarter-turn clockwise.
         * 
         * @param array The array to rotate.
         * @return The rotated array.
         */
        public static int[][] RotateRight(int[][] array)
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<int>(0, 0); //new int[0][0]; // Special case: zero-length array
            }
            int c = array[0].Length;
            //int[][] result = new int[c][r];
            int[][] result = CreateJaggedArray<int>(c, r);
            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[j][r - 1 - i] = array[i][j];
                }
            }
            return result;
        }


        /**
         * Takes a two-dimensional input array and returns a new array which is "rotated"
         * a quarter-turn counterclockwise.
         * 
         * @param array The array to rotate.
         * @return The rotated array.
         */
        public static int[][] RotateLeft(int[][] array)
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<int>(0, 0); //new int[0][0]; // Special case: zero-length array
            }
            int c = array[0].Length;
            //int[][] result = new int[c][r];
            int[][] result = CreateJaggedArray<int>(c, r);
            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[c - 1 - j][i] = array[i][j];
                }
            }
            return result;
        }

        /**
         * Takes a one-dimensional input array of m  n  numbers and returns a two-dimensional
         * array of m rows and n columns. The first n numbers of the given array are copied
         * into the first row of the new array, the second n numbers into the second row,
         * and so on. This method throws an IllegalArgumentException if the length of the input
         * array is not evenly divisible by n.
         * 
         * @param array The values to put into the new array.
         * @param n The number of desired columns in the new array.
         * @return The new m  n array.
         * @throws IllegalArgumentException If the length of the given array is not
         *  a multiple of n.
         */
        public static int[][] Ravel(int[] array, int n) //throws IllegalArgumentException
        {
            if (array.Length % n != 0)
            {
                throw new ArgumentException(array.Length + " is not evenly divisible by " + n);
            }
            int length = array.Length;
            //int[][] result = new int[length / n][n];
            int[][] result = CreateJaggedArray<int>(length / n, n);
            for (int i = 0; i < length; i++)
            {
                result[i / n][i % n] = array[i];
            }
            return result;
        }

        /**
         * Takes a m by n two dimensional array and returns a one-dimensional array of size m  n
         * containing the same numbers. The first n numbers of the new array are copied from the
         * first row of the given array, the second n numbers from the second row, and so on.
         * 
         * @param array The array to be unraveled.
         * @return The values in the given array.
         */
        public static int[] Unravel(int[][] array)
        {
            int r = array.Length;
            if (r == 0)
            {
                return new int[0]; // Special case: zero-length array
            }
            int c = array[0].Length;
            int[] result = new int[r * c];
            int index = 0;
            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[index] = array[i][j];
                    index++;
                }
            }
            return result;
        }

        /**
         * Takes a two-dimensional array of r rows and c columns and reshapes it to
         * have (r*c)/n by n columns. The value in location [i][j] of the input array
         * is copied into location [j][i] of the new array.
         * 
         * @param array The array of values to be reshaped.
         * @param n The number of columns in the created array.
         * @return The new (r*c)/n by n array.
         * @throws IllegalArgumentException If r*c  is not evenly divisible by n.
         */
        public static int[][] Reshape(int[][] array, int n) //throws IllegalArgumentException
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<int>(0, 0);//new int[0][0]; // Special case: zero-length array
            }
            if ((array.Length * array[0].Length) % n != 0)
            {
                int size = array.Length * array[0].Length;
                throw new ArgumentException(size + " is not evenly divisible by " + n);
            }
            int c = array[0].Length;
            //int[][] result = new int[(r * c) / n][n];
            int[][] result = CreateJaggedArray<int>((r * c) / n, n);
            int ii = 0;
            int jj = 0;

            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[ii][jj] = array[i][j];
                    jj++;
                    if (jj == n)
                    {
                        jj = 0;
                        ii++;
                    }
                }
            }
            return result;
        }

        public static int[][] Reshape(int[] flatArray, int dimWidth)
        {
            int rows = flatArray.Length / dimWidth;
            int[][] jagged = CreateJaggedArray<int>(rows, dimWidth);

            int i = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < dimWidth; c++)
                {
                    jagged[r][c] = flatArray[i++];
                }
            }
            return jagged;
        }
        public static int[][] ReshapeAverage(byte[] flatArray, int dimWidth, int avgWidth, int offsetWidth)
        {
            int rows = (flatArray.Length / (avgWidth + offsetWidth)) / dimWidth;
            int[][] jagged = CreateJaggedArray<int>(rows, dimWidth);

            int i = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < dimWidth; c++)
                {
                    for (int a = 0; a < avgWidth; a++)
                        jagged[r][c] += flatArray[i++];
                    jagged[r][c] /= avgWidth;
                    for (int o = 0; o < offsetWidth; o++) i++;
                }
            }
            return jagged;
        }

        public static int[] Reshape(int[][] matrix)
        {
            int rows = matrix.Length;
            int cols = matrix[0].Length;

            int[] flatArray = new int[rows * cols];
            int i = 0;
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    flatArray[i++] = matrix[r][c];
                }
            }
            return flatArray;
        }

        /**
         * Takes a two-dimensional array of r rows and c columns and reshapes it to
         * have (r*c)/n by n columns. The value in location [i][j] of the input array
         * is copied into location [j][i] of the new array.
         * 
         * @param array The array of values to be reshaped.
         * @param n The number of columns in the created array.
         * @return The new (r*c)/n by n array.
         * @throws IllegalArgumentException If r*c  is not evenly divisible by n.
         */
        public static double[][] Reshape(double[][] array, int n) //throws IllegalArgumentException
        {
            int r = array.Length;
            if (r == 0)
            {
                return CreateJaggedArray<double>(0, 0);// new double[0][0]; // Special case: zero-length array
            }
            if ((array.Length * array[0].Length) % n != 0)
            {
                int size = array.Length * array[0].Length;
                throw new ArgumentException(size + " is not evenly divisible by " + n);
            }
            int c = array[0].Length;
            //double[][] result = new double[(r * c) / n][n];
            double[][] result = CreateJaggedArray<double>((r * c) / n, n);
            int ii = 0;
            int jj = 0;

            for (int i = 0; i < r; i++)
            {
                for (int j = 0; j < c; j++)
                {
                    result[ii][jj] = array[i][j];
                    jj++;
                    if (jj == n)
                    {
                        jj = 0;
                        ii++;
                    }
                }
            }
            return result;
        }

        /**
         * Returns an array of minimum values collected from the specified axis.
         * <p>
         * <pre>
         * int[] a = min(new int[][] { { 49, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 0)
         * output:
         * a = { 6, 2, 3, 4, 5 }
         * 
         * int[] a = min(new int[][] { { 49, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 1)
         * output:
         * a = { 6, 2 }
         * 
         * @param arr
         * @param axis
         * @return
         */
        public static int[] Min(int[][] arr, int axis)
        {
            switch (axis)
            {
                case 0:
                    return Range(0, arr[0].Length).Select(i => arr.Select(ia => ia[i]).Min()).ToArray();
                //return IntStream.range(0, arr[0].length).map(i->Arrays.stream(arr).mapToInt(ia->ia[i]).min().getAsInt()).toArray();
                case 1:
                    return arr.Select(i => i.Min()).ToArray();
                //return Arrays.stream(arr).mapToInt(i->Arrays.stream(i).min().getAsInt()).toArray();

                default: throw new ArgumentException("axis must be either '0' or '1'");
            }
        }

        /**
         * Returns an array of minimum values collected from the specified axis.
         * <p>
         * <pre>
         * // axis = 0
         * double[] a = min(new double[][] { { 49, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 0)
         * output:
         * a = { 6, 2, 3, 4, 5 }
         * 
         * // axis = 1
         * double[] a = min(new double[][] { { 49, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } }, 1)
         * output:
         * a = { 2, 6 }
         * </pre>
         * @param arr
         * @param axis
         * @return
         */
        public static double[] Min(double[][] arr, int axis)
        {
            switch (axis)
            {
                case 0:
                    return Range(0, arr[0].Length).Select(i => arr.Select(ia => ia[i]).AsParallel().Min()).ToArray();
                //return IntStream.range(0, arr[0].length).mapToDouble(i->Arrays.stream(arr).mapToDouble(ia->ia[i]).min().getAsDouble()).toArray();
                case 1:
                    return arr.Select(i => i.AsParallel().Min()).ToArray();
                //return Arrays.stream(arr).mapToDouble(i->Arrays.stream(i).min().getAsDouble()).toArray();

                default: throw new ArgumentException("axis must be either '0' or '1'");
            }
        }

        /**
         * Returns an int[] with the dimensions of the input.
         * @param inputArray
         * @return
         */
        public static int[] Shape(object inputArray)
        {
            if (inputArray.GetType().IsArray)
            {
                Array oa = (Array)inputArray;

                if (oa.Length == 0) return new[] { 0 };

                object value = oa.GetValue(0);

                int[] deeperShape = Shape(value);
                if (deeperShape == null)
                {
                    return new[] { oa.Length };
                }

                List<int> result = new List<int> { oa.Length };
                result.AddRange(deeperShape);
                return result.ToArray();
            }


            return null;
            //int nr = 1 + inputArray.GetType().Name.LastIndexOf('[');
            //Array oa = (Array)inputArray;
            //int[] l = new int[nr];
            //for (int i = 0; i < nr; i++)
            //{
            //    int len = l[i] = oa.GetLength(0);//  Array.getLength(oa);
            //    if (0 < len)
            //    {
            //        oa = (Array)oa.GetValue(0); //Array.get(oa, 0); 
            //    }
            //}

            //return l;
        }

        /**
         * Sorts the array, then returns an array containing the indexes of
         * those sorted items in the original array.
         * <p>
         * int[] args = argsort(new int[] { 11, 2, 3, 7, 0 });
         * contains:
         * [4, 1, 2, 3, 0]
         * 
         * @param in
         * @return
         */
        public static int[] Argsort(int[] @in)
        {
            return Argsort(@in, -1, -1);
        }

        public static int[] Argsort(double[] @in)
        {
            return Argsort(@in, -1, -1);
        }

        /**
         * Sorts the array, then returns an array containing the indexes of
         * those sorted items in the original array which are between the
         * given bounds (start=inclusive, end=exclusive)
         * <p>
         * int[] args = argsort(new int[] { 11, 2, 3, 7, 0 }, 0, 3);
         * contains:
         * [4, 1, 2]
         * 
         * @param in
         * @return  the indexes of input elements filtered in the way specified
         * 
         * @see #argsort(int[])
         */
        public static int[] Argsort(int[] @in, int start, int end)
        {
            if (start == -1 || end == -1)
            {
                return @in.OrderBy(d => d).Select(i => @in.ToList().IndexOf(i)).ToArray();
                //return IntStream.of(in).sorted().map(i->Arrays.stream(in).boxed().collect(Collectors.toList()).indexOf(i)).toArray();
            }
            return @in.OrderBy(d => d).Select(i => @in.ToList().IndexOf(i)).Skip(start).Take(end).ToArray();
            //return IntStream.of(in).sorted().map(i->Arrays.stream(in).boxed().collect(Collectors.toList()).indexOf(i)).skip(start).limit(end).toArray();
        }

        /**
         * Sorts the array, then returns an array containing the indexes of
         * those sorted items in the original array which are between the
         * given bounds (start=inclusive, end=exclusive)
         * <p>
         * double[] args = argsort(new double[] { 11, 2, 3, 7, 0 }, 0, 3);
         * contains:
         * [4, 1, 2]
         * 
         * @param in
         * @return  the indexes of input elements filtered in the way specified
         * 
         * @see #argsort(int[])
         */
        public static int[] Argsort(double[] @in, int start, int end)
        {
            if (start == -1 || end == -1)
            {
                return @in.OrderBy(d => d).Select(i => @in.ToList().IndexOf(i)).ToArray();
                //return DoubleStream.of(in).sorted().mapToInt(i->Arrays.stream(in).boxed().collect(Collectors.toList()).indexOf(i)).toArray();
            }
            return @in.OrderBy(d => d).Select(i => @in.ToList().IndexOf(i)).Skip(start).Take(end).ToArray();
            //return DoubleStream.of(in).sorted().mapToInt(i->Arrays.stream(in).boxed().collect(Collectors.toList()).indexOf(i)).skip(start).limit(end).toArray();
        }

        public static T[][] CreateJaggedArray<T>(int rows, int cols)
        {
            T[][] array = new T[rows][];
            for (int i = 0; i < rows; i++)
            {
                array[i] = new T[cols];
            }
            return array;
        }

        public static T[][][] CreateJaggedArray<T>(int rows, int cols, int dim3)
        {
            T[][][] array = new T[rows][][];
            for (int i = 0; i < rows; i++)
            {
                array[i] = new T[cols][];
                for (int j = 0; j < cols; j++)
                {
                    array[i][j] = new T[dim3];
                }
            }
            return array;
        }

        /**
         * Returns an array of coordinates calculated from
         * a flat index.
         * 
         * @param   index   specified flat index
         * @param   shape   the array specifying the size of each dimension
         * @param   isColumnMajor   increments row first then column (default: false)
         * 
         * @return  a coordinate array
         */
        public static int[] ToCoordinates(int index, int[] shape, bool isColumnMajor)
        {
            int[] dimensionMultiples = InitDimensionMultiples(shape);
            int[] returnVal = new int[shape.Length];
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
         * Returns a new array containing the source array contents with 
         * substitutions from "substitutes" whose indexes reside in "substInds".
         * 
         * @param source        the original array
         * @param substitutes   the replacements whose indexes must be in substInds to be used.
         * @param substInds     the indexes of "substitutes" to replace in "source"
         * @return  a new array with the specified indexes replaced with "substitutes"
         */
        public static int[] Subst(int[] source, int[] substitutes, int[] substInds)
        {
            List<int> l = substInds.ToList();
            return Range(0, source.Length).ToList().Select(i => l.IndexOf(i) == -1 ? source[i] : substitutes[i]).ToArray();
            //List<int> l = Arrays.stream(substInds).boxed().collect(Collectors.toList());
            //return IntStream.range(0, source.length).map(
            //    i->l.indexOf(i) == -1 ? source[i] : substitutes[i]).toArray();
        }

        public static double[] Subst(double[] source, double[] substitutes, int[] substInds)
        {
            List<int> l = substInds.ToList();
            return Range(0, source.Length).ToList().Select(i => l.IndexOf(i) == -1 ? source[i] : substitutes[i]).ToArray();
        }

        public static IEnumerable<T[]> Combinations<T>(IList<T> argList, int argSetSize)
        {
            /*
            def combinations(iterable, r):
                // combinations('ABCD', 2) --> AB AC AD BC BD CD
                // combinations(range(4), 3) --> 012 013 023 123
                pool = tuple(iterable)
                n = len(pool)
                if r > n:
                    return
                indices = range(r)
                yield tuple(pool[i] for i in indices)
                while True:
                    for i in reversed(range(r)):
                        if indices[i] != i + n - r:
                            break
                    else:
                        return
                    indices[i] += 1
                    for j in range(i+1, r):
                        indices[j] = indices[j-1] + 1
                    yield tuple(pool[i] for i in indices)
            */
            // combinations('ABCD', 2) --> AB AC AD BC BD CD
            // combinations(range(4), 3) --> 012 013 023 123
            if (argList == null) throw new ArgumentNullException("argList");
            if (argSetSize <= 0) throw new ArgumentException("argSetSize Must be greater than 0", "argSetSize");
            return combinationsImpl(argList, 0, argSetSize - 1);
        }

        private static IEnumerable<T[]> combinationsImpl<T>(IList<T> argList, int argStart, int argIteration, List<int> argIndicies = null)
        {
            argIndicies = argIndicies ?? new List<int>();
            for (int i = argStart; i < argList.Count; i++)
            {
                argIndicies.Add(i);
                if (argIteration > 0)
                {
                    foreach (var array in combinationsImpl(argList, i + 1, argIteration - 1, argIndicies))
                    {
                        yield return array;
                    }
                }
                else
                {
                    var array = new T[argIndicies.Count];
                    for (int j = 0; j < argIndicies.Count; j++)
                    {
                        array[j] = argList[argIndicies[j]];
                    }

                    yield return array;
                }
                argIndicies.RemoveAt(argIndicies.Count - 1);
            }
        }
        /// <summary>
        /// returns absolute value of input pattern
        /// </summary>
        /// <param name="inputPattern"></param>
        /// <returns></returns>
        public static double[] Abs(double[] inputPattern)
        {
            double[] retVal = new double[inputPattern.Length];
            for (int i = 0; i < inputPattern.Length; i++)
            {
                retVal[i] = Math.Abs(inputPattern[i]);
            }
            return retVal;
        }
        public static double[][] Abs(double[][] inputPattern)
        {
            double[][] retVal = new double[inputPattern.Length][];
            for (int row = 0; row < inputPattern.Length; row++)
            {
                retVal[row] = new double[inputPattern[row].Length];
                for (int col = 0; col < inputPattern[row].Length; col++)
                {
                    retVal[row][col] = Math.Abs(inputPattern[row][col]);
                }
            }
            return retVal;
        }

        public static double Mean(double[] doubles)
        {
            if (doubles.Length == 0) return double.NaN;
            double mean = doubles.Sum() / doubles.Length;
            return mean;
        }

        public static double[] Mean(double[][] doubles)
        {
            double[] means = new double[doubles.GetLength(0)];
            for (int row = 0; row < means.Length; row++)
            {
                means[row] = Mean(doubles[row]);
            }
            return means;
        }

        public static double[] Power(double[] arr, double pow)
        {
            double[] retVal = new double[arr.Length];
            for (int i = 0; i < arr.Length; i++)
            {
                retVal[i] = Math.Pow(arr[i], pow);
            }
            return retVal;
        }

        public static double[][] Power(double[][] arr, double pow)
        {
            double[][] retVal = new double[arr.Length][];
            for (int row = 0; row < arr.Length; row++)
            {
                retVal[row] = new double[arr[row].Length];
                for (int col = 0; col < arr[row].Length; col++)
                {
                    retVal[row][col] = Math.Pow(arr[row][col], pow);
                }
            }
            return retVal;
        }

        public static IEnumerable<double> Exp(IEnumerable<double> outputActivation)
        {
            return outputActivation.Select(Math.Exp);
        }

        public static double[][] Concatinate(double[][] matrix, double[][] subMatrix, int axis)
        {
            if (axis == 0)
            {
                // concatinate on rows
                double[][] newArray = CreateJaggedArray<double>(matrix.Length + subMatrix.Length, matrix[0].Length);
                int i = 0;
                foreach (double[] row in matrix)
                {
                    newArray[i++] = row;
                }
                foreach (double[] row in subMatrix)
                {
                    newArray[i++] = row;
                }
                return newArray;
            }
            else if (axis == 1)
            {
                // concatinate on columns
                double[][] newArray = CreateJaggedArray<double>(matrix.Length, matrix[0].Length + subMatrix[0].Length);

                for (int rowNr = 0; rowNr < matrix.Length; rowNr++)
                {
                    int colNr = 0;
                    for (int c = 0; c < matrix[rowNr].Length; c++)
                    {
                        newArray[rowNr][colNr++] = matrix[rowNr][c];
                    }
                    for (int c = 0; c < subMatrix[rowNr].Length; c++)
                    {
                        newArray[rowNr][colNr++] = subMatrix[rowNr][c];
                    }
                }

                return newArray;
            }
            throw new NotImplementedException();
        }

        public static IEnumerable<double> LinearRange(double start, double stop, double step)
        {
            double pVal = start;
            yield return pVal;
            while (pVal < stop)
            {
                pVal += step;
                yield return pVal;
            }
        }

        public static IEnumerable<int> LinearRange(int start, int stop, int step)
        {
            int pVal = start;
            yield return pVal;
            while (pVal < stop)
            {
                pVal += step;
                yield return pVal;
            }
        }

        public static IEnumerable<int> RoundToInt(IEnumerable<double> collection)
        {
            return collection.Select(value => (int)Math.Round(value));
        }

        // merges the second collection with the first collection after x positions
        public static byte[] MergeGrouped(byte[] first, byte[] second, int firstRepeated, int positionOffset)
        {
            byte[] result = new byte[(first.Length * firstRepeated) + second.Length];

            int s = 0;
            int f = 0;
            int i = 0;
            int counter = 0;
            // alpha on 3, 7, 11, 15
            // img on 0 1 2  4 5 6    8 9 10   12 13 14 

            while (i < result.Length)
            {
                bool adapt = ++counter%2 == 0;
                if (adapt && i > 0)
                {
                    result[i++] = second[s++];
                }
                else
                {
                    for (int j = 0; j < firstRepeated; j++)
                    {
                        result[i++] = first[f];
                    }
                    f++;
                }
            }

            return result;
        }
    }
}