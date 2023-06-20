using System;
using System.Buffers.Text;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics.X86;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using HTM.Net.Model;
using System.Xml.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json.Linq;
using Vector = System.Numerics.Vector;


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

        public static int ProductFast(int[] dims)
        {
            int length = dims.Length;

            if (length >= System.Numerics.Vector<int>.Count)
            {
                System.Numerics.Vector<int> productVector = System.Numerics.Vector<int>.One;
                int i = 0;

                for (; i < length - System.Numerics.Vector<int>.Count + 1; i += System.Numerics.Vector<int>.Count)
                {
                    System.Numerics.Vector<int> values = new System.Numerics.Vector<int>(dims, i);
                    productVector = System.Numerics.Vector.Multiply(productVector, values);
                }

                int product = productVector[0];

                for (int j = 1; j < System.Numerics.Vector<int>.Count; j++)
                {
                    product *= productVector[j];
                }

                while (i < length)
                {
                    product *= dims[i];
                    i++;
                }

                return product;
            }
            else
            {
                int product = 1;

                for (int i = 0; i < length; i++)
                {
                    product *= dims[i];
                }

                return product;
            }
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
        /// <param name="ints">Array of ints.</param>
        /// <returns>An array with the contents converted to doubles.</returns>
        public static double[] ToDoubleArray(int[] ints)
        {
            double[] doubles = new double[ints.Length];
            for (int i = 0; i < ints.Length; i++)
            {
                doubles[i] = ints[i];
            }
            return doubles;
        }

        /// <summary>
        /// Performs a modulus operation in Python style.
        /// </summary>
        /// <param name="a">The dividend.</param>
        /// <param name="b">The divisor.</param>
        /// <returns>The result of the modulus operation.</returns>
        public static int Modulo(int a, int b)
        {
            if (b == 0)
            {
                throw new ArgumentException("Division by zero!");
            }

            int result = a % b;
            return result >= 0 ? result : result + b;
        }

        /// <summary>
        /// Performs a modulus operation on every element of the array using the specified divisor.
        /// </summary>
        /// <param name="a">The array of values.</param>
        /// <param name="b">The divisor.</param>
        /// <returns>The array with each element modulo the divisor.</returns>
        public static int[] Modulo(int[] a, int b)
        {
            for (int i = 0; i < a.Length; i++)
            {
                a[i] %= b;
                if (a[i] < 0)
                {
                    a[i] += b;
                }
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

        /// <summary>
        /// Returns a new vector with each element set to the maximum of the corresponding element in the input vector and the specified maximum value.
        /// </summary>
        /// <param name="vector">The input vector.</param>
        /// <param name="maxValue">The maximum value.</param>
        /// <returns>A new vector with each element set to the maximum value.</returns>
        public static MathNet.Numerics.LinearAlgebra.Vector<double> Maximum(MathNet.Numerics.LinearAlgebra.Vector<double> vector, double maxValue)
        {
            int count = vector.Count;
            MathNet.Numerics.LinearAlgebra.Vector<double> result = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(count);

            for (int i = 0; i < count; i++)
            {
                double value = vector[i];
                result[i] = value < maxValue ? maxValue : value;
            }

            return result;
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

        /// <summary>
        /// Returns an array of identical shape containing the maximum value between each corresponding index of the input arrays.
        /// The input arrays must have the same length.
        /// </summary>
        /// <param name="arr1">The first input array.</param>
        /// <param name="arr2">The second input array.</param>
        /// <returns>An array of maximum values between corresponding indices.</returns>
        public static int[] MaxBetween(int[] arr1, int[] arr2)
        {
            if (arr1.Length != arr2.Length)
                throw new InvalidOperationException("Arrays must be the same length!");

            int[] retVal = new int[arr1.Length];

            for (int i = 0; i < arr1.Length; i++)
            {
                retVal[i] = Math.Max(arr1[i], arr2[i]);
            }

            return retVal;
        }

        /// <summary>
        /// Returns an array of identical shape containing the minimum value between each corresponding index of the input arrays.
        /// The input arrays must have the same length.
        /// </summary>
        /// <param name="arr1">The first input array.</param>
        /// <param name="arr2">The second input array.</param>
        /// <returns>An array of minimum values between corresponding indices.</returns>
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

        /// <summary>
        /// Returns an array of values that satisfy all of the specified conditions.
        /// </summary>
        /// <param name="values">The array of values to filter.</param>
        /// <param name="conditions">The conditions that must be satisfied.</param>
        /// <returns>An array of values that satisfy all of the conditions.</returns>
        public static double[] RetainLogicalAnd(double[] values, params Func<double, bool>[] conditions)
        {
            List<double> filteredValues = new List<double>();

            for (int i = 0; i < values.Length; i++)
            {
                bool satisfiesAllConditions = true;

                for (int j = 0; j < conditions.Length && satisfiesAllConditions; j++)
                {
                    satisfiesAllConditions &= conditions[j](values[i]);
                }

                if (satisfiesAllConditions)
                {
                    filteredValues.Add(values[i]);
                }
            }

            return filteredValues.ToArray();
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
        public static double[] Divide2(double[] dividend, double[] divisor,
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

        /// <summary>
        /// Divides each element of the dividend array by the corresponding element of the divisor array,
        /// applying adjustments to both dividend and divisor values.
        /// </summary>
        /// <param name="dividend">The array of dividend values.</param>
        /// <param name="divisor">The array of divisor values.</param>
        /// <param name="dividendAdjustment">The adjustment to be applied to the dividend values.</param>
        /// <param name="divisorAdjustment">The adjustment to be applied to the divisor values.</param>
        /// <returns>An array of quotients obtained by dividing the dividend elements by the divisor elements.</returns>
        public static double[] Divide(double[] dividend, double[] divisor, double dividendAdjustment, double divisorAdjustment)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }

            double[] quotient = new double[dividend.Length];

            // Process the elements in parallel using Parallel.For
            Parallel.For(0, dividend.Length, i =>
            {
                double dividendValue = dividend[i] + dividendAdjustment;
                double divisorValue = divisor[i] + divisorAdjustment;

                // Protect against division by zero
                double denom = divisorValue != 0 ? divisorValue : 1;

                quotient[i] = dividendValue / denom;
            });

            return quotient;
        }


        /// <summary>
        /// Returns an array whose elements are the quotients of the corresponding elements
        /// in the dividend array divided by the divisor array.
        /// </summary>
        /// <param name="dividend">The array of dividend values.</param>
        /// <param name="divisor">The array of divisor values.</param>
        /// <returns>An array of quotients obtained by dividing the dividend elements by the divisor elements.</returns>
        /// <exception cref="ArgumentException">Thrown when the dividend and divisor arrays are not the same length.</exception>
        public static double[] Divide(int[] dividend, int[] divisor)
        {
            if (dividend.Length != divisor.Length)
            {
                throw new ArgumentException("The dividend array and the divisor array must be the same length");
            }

            double[] quotient = new double[dividend.Length];

            for (int i = 0; i < dividend.Length; i++)
            {
                double denom = divisor[i];

                quotient[i] = denom != 0 ? dividend[i] / (double)denom : 0; // Protect against division by zero
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
        public static int[] Divide(int[] dividend, int divisor)
        {
            int[] quotient = new int[dividend.Length];
            int denom = 1;
            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = (dividend[i]) /
                              (int)((denom = divisor) == 0 ? 1 : denom); //Protect against division by 0
            }
            return quotient;
        }

        /// <summary>
        /// Returns an array whose elements are the quotients of the corresponding elements
        /// in the dividend array divided by the divisor value.
        /// </summary>
        /// <param name="dividend">The array of dividend values.</param>
        /// <param name="divisor">The divisor value.</param>
        /// <returns>An array of quotients obtained by dividing the dividend elements by the divisor value.</returns>
        public static double[] Divide(double[] dividend, double divisor)
        {
            double[] quotient = new double[dividend.Length];
            double denom = divisor;

            for (int i = 0; i < dividend.Length; i++)
            {
                quotient[i] = denom != 0 ? dividend[i] / denom : 0; // Protect against division by zero
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

        /// <summary>
        /// Returns an array whose elements are the product of the corresponding elements
        /// in the multiplicand array and the factor array, after applying the specified adjustments.
        /// </summary>
        /// <param name="multiplicand">The array of multiplicand values.</param>
        /// <param name="factor">The array of factor values.</param>
        /// <param name="multiplicandAdjustment">The adjustment to be applied to the multiplicand values.</param>
        /// <param name="factorAdjustment">The adjustment to be applied to the factor values.</param>
        /// <returns>An array of products obtained by multiplying the multiplicand and factor elements.</returns>
        /// <exception cref="ArgumentException">Thrown when the multiplicand and factor arrays have different lengths.</exception>
        public static double[] Multiply(double[] multiplicand, double[] factor, double multiplicandAdjustment, double factorAdjustment)
        {
            if (multiplicand.Length != factor.Length)
            {
                throw new ArgumentException("The multiplicand array and the factor array must be the same length");
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
         * @param multiplicand The multiplicand array.
         * @param factor       The factor array.
         * @return             The array of products.
         * @throws ArgumentException If the two argument arrays are not the same length.
         */
        public static double[] Multiply(double[] multiplicand, int[] factor)
        {
            if (multiplicand.Length != factor.Length)
            {
                throw new ArgumentException("The multiplicand array and the factor array must be the same length");
            }

            double[] product = new double[multiplicand.Length];

            for (int i = 0; i < multiplicand.Length; i++)
            {
                product[i] = multiplicand[i] * factor[i];
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

        /// <summary>
        /// Returns an integer array containing the result of subtracting the corresponding elements
        /// of the minuend array from the subtrahend array.
        /// </summary>
        /// <param name="minuend">The array of minuend values.</param>
        /// <param name="subtrahend">The array of subtrahend values.</param>
        /// <returns>An integer array of subtraction results.</returns>
        /// <exception cref="ArgumentException">Thrown when the minuend and subtrahend arrays have different lengths.</exception>
        public static int[] Subtract(int[] minuend, int[] subtrahend)
        {
            if (minuend.Length != subtrahend.Length)
            {
                throw new ArgumentException("The minuend array and the subtrahend array must be the same length");
            }

            int[] result = new int[minuend.Length];

            for (int i = 0; i < minuend.Length; i++)
            {
                result[i] = minuend[i] - subtrahend[i];
            }

            return result;
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
            int length = arr.Length;
            if (length == 0)
            {
                throw new ArgumentException("The array cannot be empty.");
            }

            long sum = 0;

            // Compute the sum of all elements in parallel
            Parallel.ForEach(
                arr,
                () => 0L, // Initialize local sum to 0
                (item, state, localSum) => localSum + item, // Add each item to the local sum
                localSum => Interlocked.Add(ref sum, localSum) // Add local sum to the global sum
            );

            // Calculate the average
            return (double)sum / length;

            //var avg = arr.AsParallel().Average();
            //return avg;
        }

        /// <summary>
        /// Returns the average of all the elements in the specified double array.
        /// </summary>
        /// <param name="arr">The array of double values.</param>
        /// <returns>The average of the array elements.</returns>
        public static double Average(double[] arr)
        {
            int length = arr.Length;
            if (length == 0)
            {
                throw new ArgumentException("The array cannot be empty.");
            }

            double sum = 0.0;

            // Compute the sum of all elements in parallel
            Parallel.ForEach(
                arr,
                () => 0.0, // Initialize local sum to 0
                (item, state, localSum) => localSum + item, // Add each item to the local sum
                localSum => Interlocked.Exchange(ref sum, sum + localSum) // Add local sum to the global sum
            );

            // Calculate the average
            return sum / length;
        }

        /// <summary>
        /// Computes and returns the variance of the specified array given the mean.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <param name="mean">The mean value of the array.</param>
        /// <returns>The variance of the array.</returns>
        public static double Variance(double[] arr, double mean)
        {
            double accum = 0.0;
            double accum2 = 0.0;

            for (int i = 0; i < arr.Length; i++)
            {
                double dev = arr[i] - mean;
                accum += dev * dev;
                accum2 += dev;
            }

            double var = (accum - (accum2 * accum2 / arr.Length)) / arr.Length;

            return var;
        }

        /// <summary>
        /// Computes and returns the variance of the specified array.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <returns>The variance of the array.</returns>
        public static double Variance(double[] arr)
        {
            double mean = Average(arr);
            return Variance(arr, mean);
        }

        /// <summary>
        /// Returns the passed-in array with every value altered by the addition of the specified amount.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <param name="amount">The amount to add to each element of the array.</param>
        /// <returns>The modified array.</returns>
        public static int[] Add(int[] arr, int amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount;
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value altered by the addition of the corresponding element in the amount array.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <param name="amount">The array containing the amounts to add to each element of the input array.</param>
        /// <returns>The modified array.</returns>
        public static int[] Add(int[] arr, int[] amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount[i];
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value altered by the addition of the corresponding element in the amount array.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <param name="amount">The array containing the amounts to add to each element of the input array.</param>
        /// <returns>The modified array.</returns>
        public static double[] Add(double[] arr, double[] amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount[i];
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value altered by the addition of the specified amount.
        /// </summary>
        /// <param name="arr">The input array.</param>
        /// <param name="amount">The amount to add to each element of the array.</param>
        /// <returns>The modified array.</returns>
        public static double[] Add(double[] arr, double amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] += amount;
            }
            return arr;
        }

        /// <summary>
        /// Returns the sum of all contents in the specified array.
        /// </summary>
        /// <param name="array">The array to calculate the sum.</param>
        /// <returns>The sum of the array contents.</returns>
        public static int Sum(int[] array)
        {
            int sum = 0;
            int length = array.Length;
            for (int i = 0; i < length; i++)
            {
                sum += array[i];
            }
            return sum;
        }

        /// <summary>
        /// Test whether each element of a 1-D array is also present in a second array.
        /// </summary>
        /// <param name="ar1">The array of values to find in the second array.</param>
        /// <param name="ar2">The array to test for the presence of elements in the first array.</param>
        /// <returns>An array containing the intersections or an empty array if none are found.</returns>
        public static int[] In1d(int[] ar1, int[] ar2)
        {
            if (ar1 == null || ar2 == null)
            {
                return EMPTY_ARRAY;
            }

            HashSet<int> retVal = new HashSet<int>(ar2);
            retVal.IntersectWith(ar1);
            return retVal.ToArray();
        }

        /// <summary>
        /// Returns the sum of all contents in the specified array.
        /// </summary>
        /// <param name="array">The array to calculate the sum.</param>
        /// <returns>The sum of the array contents.</returns>
        public static double Sum(double[] array)
        {
            double sum = 0;
            int length = array.Length;
            for (int i = 0; i < length; i++)
            {
                sum += array[i];
            }
            return sum;
        }

        /// <summary>
        /// Computes the sum of elements along the specified axis of a 2-D array.
        /// </summary>
        /// <param name="array">The 2-D array to calculate the sum.</param>
        /// <param name="axis">The axis along which to compute the sum (0 for columns, 1 for rows).</param>
        /// <returns>An array containing the sum of elements along the specified axis.</returns>
        public static double[] Sum(double[][] array, int axis)
        {
            switch (axis)
            {
                case 0: // cols
                    int rows = array.Length;
                    int cols = array[0].Length;
                    double[] result = new double[cols];
                    for (int c = 0; c < cols; c++)
                    {
                        for (int r = 0; r < rows; r++)
                        {
                            result[c] += array[r][c];
                        }
                    }
                    return result;

                case 1: // rows
                    int numElements = array[0].Length;
                    double[] rowSums = new double[array.Length];
                    for (int r = 0; r < array.Length; r++)
                    {
                        double sum = 0;
                        for (int c = 0; c < numElements; c++)
                        {
                            sum += array[r][c];
                        }
                        rowSums[r] = sum;
                    }
                    return rowSums;

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

        /// <summary>
        /// Returns an array of integers starting from the lower bounds (inclusive) and ending at the upper bounds (exclusive).
        /// </summary>
        /// <param name="lowerBounds">The lower bounds of the range (inclusive).</param>
        /// <param name="upperBounds">The upper bounds of the range (exclusive).</param>
        /// <returns>An array of integers representing the range.</returns>
        public static int[] Range(int lowerBounds, int upperBounds)
        {
            int[] result = new int[upperBounds - lowerBounds];

            for (int i = lowerBounds, j = 0; i < upperBounds; i++, j++)
            {
                result[j] = i;
            }

            return result;
        }

        /// <summary>
        /// Returns an array of doubles that starts from the lower bounds (inclusive), ends at the upper bounds (exclusive),
        /// and increments the values by the specified interval.
        /// </summary>
        /// <param name="lowerBounds">The starting value.</param>
        /// <param name="upperBounds">The maximum value (exclusive).</param>
        /// <param name="interval">The amount by which to increment the values.</param>
        /// <returns>An array of doubles representing the arranged values.</returns>
        public static double[] Arrange(double lowerBounds, double upperBounds, double interval)
        {
            int count = (int)Math.Ceiling((upperBounds - lowerBounds) / interval);
            double[] result = new double[count];

            for (int i = 0; i < count; i++)
            {
                result[i] = lowerBounds + i * interval;
            }

            return result;
        }

        /// <summary>
        /// Returns an enumerable sequence of integers that starts from the lower bounds (inclusive),
        /// ends at the upper bounds (exclusive), and increments the values by the specified step.
        /// </summary>
        /// <param name="start">The starting value.</param>
        /// <param name="stop">The maximum value (exclusive).</param>
        /// <param name="step">The amount by which to increment the values.</param>
        /// <returns>An enumerable sequence of integers representing the range of values.</returns>
        public static IEnumerable<int> XRange(int start, int stop, int step)
        {
            if (step == 0)
            {
                throw new ArgumentException("Step cannot be zero.");
            }

            int current = start;
            int count = 0;
            bool isIncreasing = step > 0;
            bool shouldContinue = isIncreasing ? current < stop : current > stop;

            while (shouldContinue)
            {
                yield return current;
                current += step;
                count++;

                if (isIncreasing)
                {
                    shouldContinue = current < stop;
                }
                else
                {
                    shouldContinue = current > stop;
                }
            }

            if (count == 0)
            {
                yield break; // Return an empty sequence if no values are generated
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

        /// <summary>
        /// Replaces the range specified by the "start" and "end" indices of the "orig" array
        /// with the values from the "replacement" array.
        /// </summary>
        /// <param name="start">Start index of the range to be replaced.</param>
        /// <param name="end">End index (exclusive) of the range to be replaced.</param>
        /// <param name="orig">The array containing entries to be replaced by "replacement".</param>
        /// <param name="replacement">The array of ints to put in "orig" at the indicated indexes.</param>
        /// <returns>The modified "orig" array.</returns>
        public static int[] Replace(int start, int end, int[] orig, int[] replacement)
        {
            if (replacement.Length != end - start)
            {
                throw new ArgumentException("The length of the replacement array must match the range being replaced.");
            }

            for (int i = start, j = 0; i < end; i++, j++)
            {
                orig[i] = replacement[j];
            }

            return orig;
        }

        /// <summary>
        /// Returns a sorted unique array of integers.
        /// </summary>
        /// <param name="nums">An unsorted array of integers with possible duplicates.</param>
        /// <returns>A sorted array of unique integers.</returns>
        public static int[] Unique(int[] nums)
        {
            int[] result = nums.Distinct().ToArray();
            Array.Sort(result);
            return result;
        }

        /**
         * Helper Class for recursive coordinate assembling
         */
        private class CoordinateAssembler
        {
            private readonly List<int[]> dimensions;
            private readonly List<int[]> result = new List<int[]>();

            public static List<int[]> Assemble(List<int[]> dimensions)
            {
                CoordinateAssembler assembler = new CoordinateAssembler(dimensions);
                assembler.Process();
                return assembler.result;
            }

            private CoordinateAssembler(List<int[]> dimensions)
            {
                this.dimensions = dimensions;
            }

            private void Process()
            {
                int[] position = new int[dimensions.Count];
                int[] maxIndices = new int[dimensions.Count];

                for (int i = 0; i < dimensions.Count; i++)
                {
                    maxIndices[i] = dimensions[i].Length - 1;
                }

                bool finished = false;
                while (!finished)
                {
                    int[] coordinates = new int[position.Length];
                    Array.Copy(position, coordinates, position.Length);
                    result.Add(coordinates);

                    int level = dimensions.Count - 1;
                    while (level >= 0)
                    {
                        position[level]++;
                        if (position[level] <= maxIndices[level])
                        {
                            break;
                        }
                        position[level] = 0;
                        level--;
                    }

                    if (level < 0)
                    {
                        finished = true;
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

        /// <summary>
        /// Sets the values in the specified values array at the indexes specified,
        /// to the value "setTo".
        /// </summary>
        /// <param name="values">The values to alter if at the specified indexes.</param>
        /// <param name="indexes">The indexes of the values array to alter.</param>
        /// <param name="setTo">The value to set at the specified indexes.</param>
        public static void SetIndexesTo(double[] values, int[] indexes, double setTo)
        {
            int valuesLength = values.Length;
            int indexesLength = indexes.Length;

            // Create a boolean array to mark the indices to be set
            bool[] indexFlags = new bool[valuesLength];

            // Iterate over the indexes array to mark the corresponding indices in indexFlags
            for (int i = 0; i < indexesLength; i++)
            {
                int index = indexes[i];
                // Check if the index is within the valid range of the values array
                if (index >= 0 && index < valuesLength)
                {
                    // Set the flag to true for the current index
                    indexFlags[index] = true;
                }
            }

            // Iterate over the values array and update values at marked indices
            for (int i = 0; i < valuesLength; i++)
            {
                // Check if the flag is true for the current index
                if (indexFlags[i])
                {
                    // Update the value at the current index to the specified setTo value
                    values[i] = setTo;
                }
            }
        }

        /// <summary>
        /// Sets the values in the specified values array at the indexes specified,
        /// to the value "setTo".
        /// </summary>
        /// <param name="values">The values to alter if at the specified indexes.</param>
        /// <param name="indexes">The indexes of the values array to alter.</param>
        /// <param name="setTo">The value to set at the specified indexes.</param>
        public static void SetIndexesTo(int[] values, int[] indexes, int setTo)
        {
            int valuesLength = values.Length;
            int indexesLength = indexes.Length;

            // Create a boolean array to mark the indices to be set
            bool[] indexFlags = new bool[valuesLength];

            // Iterate over the indexes array to mark the corresponding indices in indexFlags
            for (int i = 0; i < indexesLength; i++)
            {
                int index = indexes[i];
                // Check if the index is within the valid range of the values array
                if (index >= 0 && index < valuesLength)
                {
                    // Set the flag to true for the current index
                    indexFlags[index] = true;
                }
            }

            // Iterate over the values array and update values at marked indices
            for (int i = 0; i < valuesLength; i++)
            {
                // Check if the flag is true for the current index
                if (indexFlags[i])
                {
                    // Update the value at the current index to the specified setTo value
                    values[i] = setTo;
                }
            }
        }

        /// <summary>
        /// Sets the values in range start to stop to the value specified. If stop &lt; 0,
        /// then stop indicates the number of places counting from the length of "values" back.
        /// </summary>
        /// <param name="values">The array to alter.</param>
        /// <param name="start">The start index (inclusive).</param>
        /// <param name="stop">The end index (exclusive).</param>
        /// <param name="setTo">The value to set the indexes to.</param>
        public static void SetRangeTo(int[] values, int start, int stop, int setTo)
        {
            int length = values.Length;

            // Adjust the stop index if it's negative
            if (stop < 0)
            {
                stop = length + stop;
            }

            // Ensure that the start and stop indices are within the valid range
            start = Math.Max(0, start);
            stop = Math.Min(stop, length);

            // Set the values in the specified range to the specified value
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

        public static void SetRangeTo(MathNet.Numerics.LinearAlgebra.Vector<double> values, int start, int stop, double setTo)
        {
            stop = stop < 0 ? values.Count + stop : stop;
            for (int i = start; i < stop; i++)
            {
                values[i] = setTo;
            }
        }

        /// <summary>
        /// Returns a random, sorted, and unique array of the specified sample size of
        /// selections from the specified list of choices.
        /// </summary>
        /// <param name="sampleSize">The number of selections in the returned sample.</param>
        /// <param name="choices">The list of choices to select from.</param>
        /// <param name="random">A random number generator.</param>
        /// <returns>A sample of numbers of the specified size.</returns>
        public static int[] Sample(int[] choices, ref int[] selectedIndices, IRandom random)
        {
            List<int> supply = new List<int>(choices);

            int[] chosen = supply.SelectCombination(selectedIndices.Length, (Random) random).ToArray();

            selectedIndices = chosen; // use ref so we can replace this instance
            Array.Sort(selectedIndices);

            return selectedIndices;
        }

        /// <summary>
        /// Returns a random, sorted, and unique array of the specified sample size of
        /// selections from the specified list of choices.
        /// </summary>
        /// <param name="sampleSize">The number of selections in the returned sample</param>
        /// <param name="choices">The list of choices to select from</param>
        /// <param name="random">A random number generator</param>
        /// <returns>A sample of numbers of the specified size</returns>
        public static int[] SampleFast(int[] choices, ref int[] selectedIndices, IRandom random)
        {
            int sampleSize = selectedIndices.Length;
            if (sampleSize <= 0)
            {
                selectedIndices = Array.Empty<int>();
                return selectedIndices;
            }

            HashSet<int> supply = new HashSet<int>(choices);
            int[] chosen = supply.SelectCombination(sampleSize, (Random)random).OrderBy(n => n).ToArray();

            selectedIndices = chosen;
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

        /// <summary>
        /// Ensures that each entry in the specified array has a minimum value equal to or greater than
        /// the specified minimum and a maximum value less than or equal to the specified maximum.
        /// </summary>
        /// <param name="values">The values to clip.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The clipped array.</returns>
        public static double[] Clip2(double[] values, double min, double max)
        {
            Parallel.For(0, values.Length, i =>
            {
                // Clip the value between 0 and 1
                values[i] = Math.Min(max, Math.Max(min, values[i]));
            });

            // Sequential version using regular for loop:
            //for (int i = 0; i < values.Length; i++)
            //{
            //    values[i] = Math.Min(max, Math.Max(min, values[i]));
            //}

            return values;
        }

        /// <summary>
        /// Ensures that each entry in the specified array has a minimum value equal to or greater than
        /// the specified minimum and a maximum value less than or equal to the specified maximum.
        /// </summary>
        /// <param name="values">The values to clip.</param>
        /// <param name="min">The minimum value.</param>
        /// <param name="max">The maximum value.</param>
        /// <returns>The clipped array.</returns>
        public static double[] Clip(double[] values, double min, double max)
        {
            int length = values.Length;
            int vectorSize = System.Numerics.Vector<double>.Count;
            int endIndex = length - length % vectorSize;

            // Process the vectorizable part using SIMD
            for (int i = 0; i < endIndex; i += vectorSize)
            {
                System.Numerics.Vector<double> vector = new System.Numerics.Vector<double>(values, i);
                vector = Vector.Min(Vector.Max(vector, new System.Numerics.Vector<double>(min)), new System.Numerics.Vector<double>(max));
                vector.CopyTo(values, i);
            }

            // Process the remaining elements sequentially
            for (int i = endIndex; i < length; i++)
            {
                values[i] = Math.Min(Math.Max(values[i], min), max);
            }

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

        /// <summary>
        /// Ensures that each entry in the specified array has a minimum value equal to or greater than
        /// the minimum at the specified index and a maximum value less than or equal to the maximum at
        /// the specified index.
        /// </summary>
        /// <param name="values">The values to clip.</param>
        /// <param name="max">The maximum values.</param>
        /// <param name="adj">The adjustment amount.</param>
        /// <returns>The clipped array.</returns>
        public static int[] Clip(int[] values, int[] max, int adj)
        {
            for (int i = 0; i < values.Length; i++)
            {
                // Clip the value between 0 and (max[i] + adj)
                values[i] = Math.Max(0, Math.Min(max[i] + adj, values[i]));
            }
            return values;
        }

        /// <summary>
        /// Returns the count of values in the specified array that are greater than the specified compare value.
        /// </summary>
        /// <param name="compare">The value to compare to.</param>
        /// <param name="array">The values being compared.</param>
        /// <returns>The count of values greater than the compare value.</returns>
        public static int ValueGreaterCount(double compare, double[] array)
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                // Increment the count if the value is greater than compare
                if (array[i] > compare)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Returns the count of values in the specified array that are greater than or equal to the specified compare value.
        /// </summary>
        /// <param name="compare">The value to compare to.</param>
        /// <param name="array">The values being compared.</param>
        /// <returns>The count of values greater than or equal to the compare value.</returns>
        public static int ValueGreaterOrEqualCount(double compare, double[] array)
        {
            int count = 0;
            for (int i = 0; i < array.Length; i++)
            {
                // Increment the count if the value is greater than or equal to compare
                if (array[i] >= compare)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// Returns the count of values in the specified array, at the specified indexes, that are greater than the specified compare value.
        /// </summary>
        /// <param name="compare">The value to compare to.</param>
        /// <param name="array">The values being compared.</param>
        /// <param name="indexes">The indexes of values to consider.</param>
        /// <returns>The count of values greater than the compare value.</returns>
        public static int ValueGreaterCountAtIndex(double compare, double[] array, int[] indexes)
        {
            int count = 0;
            for (int i = 0; i < indexes.Length; i++)
            {
                // Increment the count if the value at the specified index is greater than compare
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

        /// <summary>
        /// Scans the specified values and applies the <see cref="Condition"/> to each value,
        /// returning the indexes of the values where the condition evaluates to true.
        /// </summary>
        /// <typeparam name="T">The type of values in the collection.</typeparam>
        /// <param name="values">The values to test.</param>
        /// <param name="condition">The condition used to test each value.</param>
        /// <returns>An array of indexes where the condition evaluates to true.</returns>
        public static int[] Where<T>(IEnumerable<T> values, Func<T, bool> condition)
        {
            // Create a List to collect the indexes
            List<int> indexes = new List<int>();

            int i = 0;
            foreach (T value in values)
            {
                // Check the condition and add the index to the list if true
                if (condition(value))
                {
                    indexes.Add(i);
                }

                i++;
            }

            // Convert the List to an array
            return indexes.ToArray();
        }

        /// <summary>
        /// Returns a flag indicating whether the specified array is a sparse array of 0's and 1's or not.
        /// </summary>
        /// <param name="ia">Array to check</param>
        /// <returns>True if the array is sparse, False otherwise</returns>
        public static bool IsSparse(ReadOnlySpan<int> ia)
        {
            if (ia.Length < 3)
            {
                return false;
            }

            int end = ia[ia.Length - 1];

            for (int i = ia.Length - 1, j = 0; i >= 0; i--, j++)
            {
                if (ia[i] != 0 && ia[i] != 1)
                {
                    return true;
                }
                else if (j > 0 && ia[i] == end)
                {
                    return false;
                }
            }

            return false;
        }

        /// <summary>
        /// Returns a bit vector of the specified size whose "on" bit indexes are specified in "in";
        /// basically converting a sparse array to a dense one.
        /// </summary>
        /// <param name="input">Sparse array specifying the "on" bits</param>
        /// <param name="size">Size of the dense array</param>
        /// <returns>Dense array</returns>
        public static int[] AsDense(ReadOnlySpan<int> input, int size)
        {
            int[] retVal = new int[size];
            int vectorSize = System.Numerics.Vector<int>.Count;

            // Process vector-sized chunks
            int i = 0;
            for (; i <= input.Length - vectorSize; i += vectorSize)
            {
                System.Numerics.Vector<int> indices = new System.Numerics.Vector<int>(input.Slice(i));
                System.Numerics.Vector<int> mask = System.Numerics.Vector.Equals(indices, System.Numerics.Vector<int>.Zero);
                mask.CopyTo(retVal, i);
            }

            // Process remaining elements
            for (; i < input.Length; i++)
            {
                int index = input[i];
                retVal[index] = 1;
            }

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

        /// <summary>
        /// Makes all values in the specified array which are less than or equal to the specified "x" value, equal to the specified "y".
        /// </summary>
        /// <param name="array">Array to process</param>
        /// <param name="x">The comparison value</param>
        /// <param name="y">The value to set if the comparison fails</param>
        public static void LessThanOrEqualXThanSetToY(double[] array, double x, double y)
        {
            // Iterate over each element in the array
            for (int i = 0; i < array.Length; i++)
            {
                // Check if the current element is less than or equal to x
                if (array[i] <= x)
                {
                    // Set the current element to y
                    array[i] = y;
                }
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

        /// <summary>
        /// Sets the value to "y" in "targetB" if the value in the same index in "sourceA" is bigger than "x".
        /// </summary>
        /// <param name="sourceA">Array to compare elements with x</param>
        /// <param name="targetB">Array to set elements to y</param>
        /// <param name="x">The comparison value</param>
        /// <param name="y">The value to set if the comparison fails</param>
        public static void GreaterThanXThanSetToYInB(int[] sourceA, double[] targetB, int x, double y)
        {
            // Iterate over each element in sourceA and compare it with x
            for (int i = 0; i < sourceA.Length; i++)
            {
                if (sourceA[i] > x)
                {
                    // Set the corresponding element in targetB to y
                    targetB[i] = y;
                }
            }
        }

        /// <summary>
        /// Returns the index of the max value in the specified array.
        /// </summary>
        /// <param name="array">The array to find the max value index in.</param>
        /// <returns>The index of the max value.</returns>
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

        /**
         * Returns the index of the max value in the specified array
         * @param array the array to find the max value index in
         * @return the index of the max value
         */
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

        /// <summary>
        /// Returns the maximum value in the specified array
        /// </summary>
        /// <param name="array">Array to investigate</param>
        /// <returns>The maximum value in the specified array</returns>
        public static int Max(int[] array)
        {
            int max = int.MinValue;
            int length = array.Length;

            // Unroll the loop to process multiple elements at once
            int i = 0;
            for (; i < length - 3; i += 4)
            {
                int val1 = array[i];
                int val2 = array[i + 1];
                int val3 = array[i + 2];
                int val4 = array[i + 3];

                max = Math.Max(max, Math.Max(Math.Max(val1, val2), Math.Max(val3, val4)));
            }

            // Process the remaining elements (if any)
            for (; i < length; i++)
            {
                max = Math.Max(max, array[i]);
            }

            return max;
        }

        /**
         * Returns the maximum value in the specified array
         * @param array
         * @return
         */
        public static double Max(double[] array)
        {
            int length = array.Length;

            // Check if the array length is within a range suitable for vectorization
            if (length >= System.Numerics.Vector<double>.Count)
            {
                int remaining = length % System.Numerics.Vector<double>.Count;
                int lastIndex = length - remaining;

                System.Numerics.Vector<double> maxVector = new System.Numerics.Vector<double>(double.MinValue);
                int i = 0;

                // Process vectorized blocks
                for (; i < lastIndex; i += System.Numerics.Vector<double>.Count)
                {
                    System.Numerics.Vector<double> values = new System.Numerics.Vector<double>(array, i);
                    maxVector = System.Numerics.Vector.Max(maxVector, values);
                }

                // Find the maximum value in the maxVector
                double[] tempArray = new double[System.Numerics.Vector<double>.Count];
                maxVector.CopyTo(tempArray);

                double max = tempArray[0];
                for (int j = 1; j < System.Numerics.Vector<double>.Count; j++)
                {
                    max = Math.Max(max, tempArray[j]);
                }

                // Process the remaining elements (if any)
                for (; i < length; i++)
                {
                    max = Math.Max(max, array[i]);
                }

                return max;
            }
            else
            {
                // Process the array using a simple loop for small lengths
                double max = double.MinValue;
                for (int i = 0; i < length; i++)
                {
                    max = Math.Max(max, array[i]);
                }
                return max;
            }
        }

        internal static double[][] SubtractRows(double[][] matrix, double[] vector)
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

        /// <summary>
        /// Returns the passed-in array with every value being altered
        /// by the subtraction of the specified double amount.
        /// </summary>
        /// <param name="arr">Array to subtract from</param>
        /// <param name="amount">Amount to subtract</param>
        /// <returns>Double array</returns>
        public static double[] Sub(double[] arr, double amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] -= amount;
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in matrix with every value being altered
        /// by the subtraction of the specified double amount.
        /// </summary>
        /// <param name="matrix">Matrix to subtract from</param>
        /// <param name="amount">Amount to subtract</param>
        /// <returns>Double matrix</returns>
        internal static Matrix<double> Sub(Matrix<double> matrix, Vector<double> amount)
        {
            if (matrix.RowCount == amount.Count)
            {
                int rowCount = matrix.RowCount;
                int colCount = matrix.ColumnCount;

                var result = matrix.Clone();
                for (int row = 0; row < rowCount; row++)
                {
                    for (int col = 0; col < colCount; col++)
                    {
                        result.At(row, col, result.At(row, col) - amount[col]);
                    }
                }

                return result;
            }
            else
            {
                var result = matrix.Clone();
                for (int row = 0; row < result.RowCount; row++)
                {
                    result.SetRow(row, result.Row(row).Subtract(amount));
                }

                return result;
            }
        }

        /// <summary>
        /// Returns the passed-in jagged array with every value being altered
        /// by the subtraction of the specified double amount.
        /// </summary>
        /// <param name="arr">Jagged array to subtract from</param>
        /// <param name="amount">Amount to subtract</param>
        /// <returns>Double jagged array</returns>
        internal static double[][] Sub(double[][] arr, double[] amount)
        {
            int rows = arr.Length;
            int cols = amount.Length;

            double[][] result = new double[rows][];
            for (int row = 0; row < rows; row++)
            {
                int rowLength = arr[row].Length;
                result[row] = new double[rowLength];

                if (rowLength == cols)
                {
                    for (int col = 0; col < rowLength; col++)
                    {
                        result[row][col] = arr[row][col] - amount[col];
                    }
                }
                else
                {
                    for (int col = 0; col < rowLength; col++)
                    {
                        result[row][col] = arr[row][col] - amount[row % cols];
                    }
                }
            }
            return result;
        }


        /// <summary>
        /// Returns the passed-in array with every value being altered
        /// by the subtraction of the specified double amount.
        /// </summary>
        /// <param name="amount">Amount to subtract</param>
        /// <param name="arr">Array to subtract from</param>
        /// <returns>Double array</returns>
        public static double[] Sub(double amount, double[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = amount - arr[i];
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value being altered
        /// by the subtraction of the specified int amount.
        /// </summary>
        /// <param name="arr">Array to subtract from</param>
        /// <param name="amount">Amount to subtract</param>
        /// <returns>Int array</returns>
        public static int[] Sub(int[] arr, int amount)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] -= amount;
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value being altered
        /// by the subtraction of the specified int amount.
        /// </summary>
        /// <param name="amount">Amount to subtract</param>
        /// <param name="arr">Array to subtract from</param>
        /// <returns>Int array</returns>
        public static int[] Sub(int amount, int[] arr)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                arr[i] = amount - arr[i];
            }
            return arr;
        }

        /// <summary>
        /// Returns the passed-in array with every value being altered
        /// by the subtraction of the specified double amount at the same index.
        /// </summary>
        /// <param name="arr">Array to subtract from</param>
        /// <param name="amount">Amount to subtract</param>
        /// <returns>Double array</returns>
        public static double[] Sub(double[] arr, double[] amount)
        {
            for (int i = 0; i < Math.Min(arr.Length, amount.Length); i++)
            {
                arr[i] -= amount[i];
            }
            return arr;
        }

        /// <summary>
        /// Returns a new array containing the items specified from
        /// the source array by the indexes specified.
        /// </summary>
        /// <param name="source">Source array</param>
        /// <param name="indexes">Indexes of the items to retrieve</param>
        /// <returns>Double array</returns>
        public static double[] Sub(double[] source, int[] indexes)
        {
            double[] result = new double[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                result[i] = source[indexes[i]];
            }
            return result;
        }

        /// <summary>
        /// Returns a new array containing the items specified from
        /// the source array by the indexes specified.
        /// </summary>
        /// <param name="source">Source array</param>
        /// <param name="indexes">Indexes of the items to retrieve</param>
        /// <returns>Int array</returns>
        public static int[] Sub(int[] source, int[] indexes)
        {
            int[] result = new int[indexes.Length];
            for (int i = 0; i < indexes.Length; i++)
            {
                result[i] = source[indexes[i]];
            }
            return result;
        }

        /// <summary>
        /// Takes an input array of m rows and n columns, and transposes it to form an array
        /// of n rows and m columns. The value in location [i][j] of the input array is copied
        /// into location [j][i] of the new array.
        /// </summary>
        /// <param name="array">The array to transpose.</param>
        /// <returns>The transposed array.</returns>
        public static T[][] Transpose<T>(T[][] array)
        {
            int rowCount = array.Length;
            if (rowCount == 0)
            {
                return CreateJaggedArray<T>(0, 0); // Special case: zero-length array
            }

            int colCount = array[0].Length;
            T[][] result = CreateJaggedArray<T>(colCount, rowCount);

            for (int i = 0; i < rowCount; i++)
            {
                for (int j = 0; j < colCount; j++)
                {
                    result[j][i] = array[i][j];
                }
            }

            return result;
        }

        /// <summary>
        /// Transforms a 2D matrix of doubles to a 1D array by concatenation.
        /// </summary>
        /// <param name="A">The 2D matrix of doubles</param>
        /// <returns>A new 1D array containing the concatenated elements</returns>
        public static double[] To1D(double[][] A)
        {
            int rows = A.Length;
            int cols = A[0].Length;
            double[] B = new double[rows * cols];
            int index = 0;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    B[index++] = A[i][j];
                }
            }
            return B;
        }

        /// <summary>
        /// Transforms a 2D matrix of integers to a 1D array by concatenation.
        /// </summary>
        /// <param name="A">The 2D matrix of integers</param>
        /// <returns>A new 1D array containing the concatenated elements</returns>
        public static int[] To1D(int[][] A)
        {
            int rows = A.Length;
            int cols = A[0].Length;
            int[] B = new int[rows * cols];
            int index = 0;

            for (int i = 0; i < rows; i++)
            {
                for (int j = 0; j < cols; j++)
                {
                    B[index++] = A[i][j];
                }
            }
            return B;
        }

        /// <summary>
        /// Returns the minimum value in the specified array of integers.
        /// </summary>
        /// <param name="array">The array of integers</param>
        /// <returns>The minimum value in the array</returns>
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

        /// <summary>
        /// Returns the minimum value in the specified array of doubles.
        /// </summary>
        /// <param name="array">The array of doubles</param>
        /// <returns>The minimum value in the array</returns>
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

        /// <summary>
        /// Returns a new array that is a copy of the specified array of integers in reverse order.
        /// </summary>
        /// <param name="d">The original array</param>
        /// <returns>A new array containing the elements in reverse order</returns>
        public static int[] Reverse(int[] d)
        {
            int[] clone = (int[])d.Clone();
            Array.Reverse(clone);
            return clone;
            // Alternative implementation:
            // int[] ret = new int[d.Length];
            // for (int i = 0, j = d.Length - 1; j >= 0; i++, j--)
            // {
            //     ret[i] = d[j];
            // }
            // return ret;
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

        /// <summary>
        /// Returns a new integer array containing the bitwise AND of both arg1 and arg2.
        /// </summary>
        /// <param name="arg1">The first array</param>
        /// <param name="arg2">The second array</param>
        /// <returns>A new array containing the bitwise AND of the elements</returns>
        public static int[] And(int[] arg1, int[] arg2)
        {
            int minLength = Math.Min(arg1.Length, arg2.Length);
            int[] retVal = new int[minLength];
            for (int i = 0; i < minLength; i++)
            {
                retVal[i] = arg1[i] & arg2[i];
            }
            return retVal;
        }

        /// <summary>
        /// Copies the passed array <paramref name="original"/> into a new array, excluding the first element, and returns it.
        /// </summary>
        /// <param name="original">The original array</param>
        /// <returns>A new array containing the tail from the original array</returns>
        public static int[] Tail(int[] original)
        {
            int[] range = new int[original.Length - 1];
            Array.Copy(original, 1, range, 0, range.Length);
            // Alternative: return Arrays.CopyOfRange(original, 1, original.Length);
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

        /// <summary>
        /// Assigns the specified integer value to each element of the specified multi-dimensional array of integers.
        /// </summary>
        /// <param name="array">The array to fill</param>
        /// <param name="value">The value to assign to each element</param>
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
                    // Jagged array
                    foreach (var item in array)
                    {
                        FillArray((Array)item, value);
                    }
                }
            }
            else if (array.Rank == 2)
            {
                int[,] arr = (int[,])array;
                int length0 = array.GetLength(0);
                int length1 = array.GetLength(1);
                for (int i = 0; i < length0; i++)
                {
                    for (int j = 0; j < length1; j++)
                    {
                        arr[i, j] = value;
                    }
                }
            }
            else if (array.Rank == 3)
            {
                int[,,] arr = (int[,,])array;
                int length0 = array.GetLength(0);
                int length1 = array.GetLength(1);
                int length2 = array.GetLength(2);
                for (int i = 0; i < length0; i++)
                {
                    for (int j = 0; j < length1; j++)
                    {
                        for (int k = 0; k < length2; k++)
                        {
                            arr[i, j, k] = value;
                        }
                    }
                }
            }
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

        public static string DoubleArrayToString(ICollection<double> array, string format = "{0}, ")
        {
            StringBuilder result = new StringBuilder();
            if (array.GetType().IsArray && ((Array)array).Rank > 1)
            {
                result.Append(Arrays.DeepToString((Array)array));
                // result.Append(Arrays.deepToString((Object[])array));
            }
            else if (array is SparseVector sv)
            {
                result.Append(Arrays.ToArrayString(sv.ToArray()));
            }
            else
            {
                // One dimension
                result.Append(Arrays.ToArrayString((Array)array, format));
            }
            return result.ToString();
        }

        /// <summary>
        /// Checks if all elements in the array satisfy a specified condition.
        /// </summary>
        /// <typeparam name="T">The type of the array elements</typeparam>
        /// <param name="values">The array to check</param>
        /// <param name="condition">The condition to evaluate for each element</param>
        /// <returns>True if all elements satisfy the condition, False otherwise</returns>
        public static bool All<T>(T[] values, Func<T, bool> condition)
        {
            return values.All(condition);
        }

        /// <summary>
        /// Concatenates multiple arrays into a single array.
        /// </summary>
        /// <typeparam name="T">The type of the array elements</typeparam>
        /// <param name="first">The first array</param>
        /// <param name="rest">Additional arrays to concatenate</param>
        /// <returns>The concatenated array</returns>
        public static T[] ConcatAll<T>(T[] first, params T[][] rest)
        {
            int totalLength = first.Length;

            // Calculate the total length of the concatenated array
            foreach (T[] array in rest)
            {
                totalLength += array.Length;
            }

            T[] result = new T[totalLength];
            int offset = 0;

            // Copy elements from the first array
            Array.Copy(first, 0, result, offset, first.Length);
            offset += first.Length;

            // Copy elements from the rest of the arrays
            foreach (T[] array in rest)
            {
                Array.Copy(array, 0, result, offset, array.Length);
                offset += array.Length;
            }

            return result;
        }

        /// <summary>
        /// Concatenates multiple integer arrays into a single array.
        /// </summary>
        /// <param name="first">The first array</param>
        /// <param name="rest">Additional arrays to concatenate</param>
        /// <returns>The concatenated array</returns>
        public static int[] ConcatAll(int[] first, params int[][] rest)
        {
            int totalLength = first.Length;

            // Calculate the total length of the concatenated array
            foreach (int[] array in rest)
            {
                totalLength += array.Length;
            }

            int[] result = new int[totalLength];
            int offset = 0;

            // Copy elements from the first array
            Array.Copy(first, 0, result, offset, first.Length);
            offset += first.Length;

            // Copy elements from the rest of the arrays
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

        /// <summary>
        /// Returns an array of minimum values collected from the specified axis.
        /// </summary>
        /// <param name="arr">The input array</param>
        /// <param name="axis">The axis along which to find the minimum values</param>
        /// <returns>An array of minimum values</returns>
        public static int[] Min(int[][] arr, int axis)
        {
            int numRows = arr.Length;
            int numCols = arr[0].Length;

            int[] result;

            if (axis == 0)
            {
                result = new int[numCols];

                // Iterate over each column
                for (int i = 0; i < numCols; i++)
                {
                    int min = int.MaxValue;

                    // Find the minimum value in each row for the current column
                    for (int j = 0; j < numRows; j++)
                    {
                        min = Math.Min(min, arr[j][i]);
                    }

                    result[i] = min;
                }
            }
            else if (axis == 1)
            {
                result = new int[numRows];

                // Iterate over each row
                for (int i = 0; i < numRows; i++)
                {
                    int min = int.MaxValue;
                    int[] row = arr[i];

                    // Find the minimum value in the current row
                    for (int j = 0; j < numCols; j++)
                    {
                        min = Math.Min(min, row[j]);
                    }

                    result[i] = min;
                }
            }
            else
            {
                throw new ArgumentException("axis must be either '0' or '1'");
            }

            return result;
        }

        /// <summary>
        /// Returns an array of minimum values collected from the specified axis.
        /// </summary>
        /// <param name="arr">The input array</param>
        /// <param name="axis">The axis along which to find the minimum values</param>
        /// <returns>An array of minimum values</returns>
        public static double[] Min(double[][] arr, int axis)
        {
            int numRows = arr.Length;
            int numCols = arr[0].Length;

            double[] result;

            if (axis == 0)
            {
                result = new double[numCols];

                // Iterate over each column
                for (int i = 0; i < numCols; i++)
                {
                    double min = double.MaxValue;

                    // Find the minimum value in each row for the current column
                    for (int j = 0; j < numRows; j++)
                    {
                        min = Math.Min(min, arr[j][i]);
                    }

                    result[i] = min;
                }
            }
            else if (axis == 1)
            {
                result = new double[numRows];

                // Iterate over each row
                for (int i = 0; i < numRows; i++)
                {
                    double min = double.MaxValue;
                    double[] row = arr[i];

                    // Find the minimum value in the current row
                    for (int j = 0; j < numCols; j++)
                    {
                        min = Math.Min(min, row[j]);
                    }

                    result[i] = min;
                }
            }
            else
            {
                throw new ArgumentException("axis must be either '0' or '1'");
            }

            return result;
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

        public static int[] Argsort(MathNet.Numerics.LinearAlgebra.Vector<double> @in)
        {
            return Argsort(@in.ToArray(), -1, -1);
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
        public static int[] Argsort(double[] input, int start, int end)
        {
            int[] indices = Enumerable.Range(0, input.Length).ToArray();
            Array.Sort(indices, (a, b) => input[a].CompareTo(input[b]));

            if (start == -1 || end == -1)
            {
                return input.OrderBy(d => d).Select(i => input.ToList().IndexOf(i)).ToArray(); 
                //return indices;
            }

            return indices.Skip(start).Take(end - start).ToArray();
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
        public static int[] Argsort(MathNet.Numerics.LinearAlgebra.Vector<double> input, int start, int end)
        {
            int[] indices = Enumerable.Range(0, input.Count).ToArray();

            Array.Sort(indices, (a, b) => input[a].CompareTo(input[b]));

            if (start == -1 || end == -1)
            {
                return indices;
            }

            return indices.Skip(start).Take(end - start).ToArray();
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

        public static MathNet.Numerics.LinearAlgebra.Vector<double> Subst(MathNet.Numerics.LinearAlgebra.Vector<double> source, MathNet.Numerics.LinearAlgebra.Vector<double> substitutes, int[] substInds)
        {
            List<int> l = substInds.ToList();

            return MathNet.Numerics.LinearAlgebra.Vector<double>.Build
                .Sparse(source.Count, i => l.IndexOf(i) == -1 ? source[i] : substitutes[i]);
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
            return MathNet.Numerics.Statistics.ArrayStatistics.Mean(doubles);
        }

        public static MathNet.Numerics.LinearAlgebra.Vector<double> Mean(Matrix<double> doubles, int axis = 0)
        {
            int rowCount = doubles.RowCount;
            int colCount = doubles.ColumnCount;
            MathNet.Numerics.LinearAlgebra.Vector<double> means;

            if (axis == 1)
            {
                // Calculate means along the horizontal axis
                means = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(rowCount);
                for (int row = 0; row < rowCount; row++)
                {
                    double sum = 0.0;
                    for (int col = 0; col < colCount; col++)
                    {
                        sum += doubles[row, col];
                    }
                    means[row] = sum / colCount;
                }
            }
            else
            {
                // Calculate means along the vertical axis (transposed)
                means = MathNet.Numerics.LinearAlgebra.Vector<double>.Build.Dense(colCount);
                for (int col = 0; col < colCount; col++)
                {
                    double sum = 0.0;
                    for (int row = 0; row < rowCount; row++)
                    {
                        sum += doubles[row, col];
                    }
                    means[col] = sum / rowCount;
                }
            }
            return means;
        }

        public static double[] Mean(double[][] doubles, int axis = 0)
        {
            int rowCount = doubles.Length;
            int colCount = doubles[0].Length;
            double[] means;
            if (axis == 1)
            {
                // Calculate means along the horizontal axis
                means = new double[rowCount];
                for (int row = 0; row < rowCount; row++)
                {
                    double sum = 0.0;
                    for (int col = 0; col < colCount; col++)
                    {
                        sum += doubles[row][col];
                    }
                    means[row] = sum / colCount;
                }
            }
            else
            {
                // Calculate means along the vertical axis (transposed)
                means = new double[colCount];
                for (int col = 0; col < colCount; col++)
                {
                    double sum = 0.0;
                    for (int row = 0; row < rowCount; row++)
                    {
                        sum += doubles[row][col];
                    }
                    means[col] = sum / rowCount;
                }
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
                bool adapt = ++counter % 2 == 0;
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

        /// <summary>
        /// Returns a subset of the keys that match any of the given patterns
        /// </summary>
        /// <param name="patterns">A list of regular expressions to match</param>
        /// <param name="keys">A list of keys to search for matches</param>
        /// <returns></returns>
        public static List<string> MatchPatterns(string[] patterns, string[] keys)
        {
            List<string> results = new List<string>();

            if (patterns == null || !patterns.Any()) return null;

            foreach (var pattern in patterns)
            {
                var prog = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
                results.AddRange(keys.Where(k => prog.IsMatch(k)).ToList());
            }

            return results;
        }
    }
}