using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class ArrayUtilsTest
    {

        [TestMethod]
        public void TestProduct()
        {
            int[] dim = new[] { 20, 20 };
            int result = ArrayUtils.Product(dim);
            Assert.AreEqual(20 * 20, result);

            dim = new[] { 20, 20, 20 };
            result = ArrayUtils.Product(dim);
            Assert.AreEqual(20 * 20 * 20, result);

            dim = new[] { 20, 20, 20, 20, 20, 20, 20 };
            result = ArrayUtils.Product(dim);
            Assert.AreEqual(20 * 20 * 20 * 20 * 20 * 20 * 20, result);
        }

        [TestMethod]
        public void TestProductFast()
        {
            int[] dim = new[] { 20, 20 };
            int result = ArrayUtils.ProductFast(dim);
            Assert.AreEqual(20 * 20, result);

            dim = new[] { 20, 20, 20 };
            result = ArrayUtils.ProductFast(dim);
            Assert.AreEqual(20 * 20 * 20, result);

            dim = new[] { 20, 20, 20, 20, 20, 20, 20 };
            result = ArrayUtils.ProductFast(dim);
            Assert.AreEqual(20 * 20 * 20 * 20 * 20 * 20 * 20, result);
        }

        [TestMethod]
        public void TestAdd()
        {
            int[] ia = { 1, 1, 1, 1 };
            int[] expected = { 2, 2, 2, 2 };
            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.Add(ia, 1)));

            // add one array to another
            expected = new int[] { 4, 4, 4, 4 };
            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.Add(ia, ia)));

            ///////// double version //////////
            double[] da = { 1.0, 1.0, 1.0, 1.0 };
            double[] d_expected = { 2.0, 2.0, 2.0, 2.0 };
            Assert.IsTrue(Arrays.AreEqual(d_expected, ArrayUtils.Add(da, 1.0)));

            // add one array to another
            d_expected = new double[] { 4.0, 4.0, 4.0, 4.0 };
            Assert.IsTrue(Arrays.AreEqual(d_expected, ArrayUtils.Add(da, da)));
        }

        [TestMethod]
        public void TestDSubtract()
        {
            double[] da = { 2.0, 2.0, 2.0, 2.0 };
            double[] d_expected = { 1.5, 1.5, 1.5, 1.5 };
            Assert.IsTrue(Arrays.AreEqual(d_expected, ArrayUtils.Sub(da, 0.5)));

            da = new double[] { 2.0, 2.0, 2.0, 2.0 };
            double[] sa = new double[] { 1.0, 1.0, 1.0, 1.0 };
            Assert.IsTrue(Arrays.AreEqual(sa, ArrayUtils.Sub(da, sa)));
        }

        [TestMethod]
        public void TestTranspose_int()
        {
            int[][] a = { new[] { 1, 2, 3, 4 }, new[] { 5, 6, 7, 8 } };
            int[][] expected = { new[] { 1, 5 }, new[] { 2, 6, }, new[] { 3, 7, }, new[] { 4, 8 } };

            int[][] result = ArrayUtils.Transpose(a);
            for (int i = 0; i < expected.Length; i++)
            {
                for (int j = 0; j < expected[i].Length; j++)
                {
                    Assert.AreEqual(expected[i][j], result[i][j]);
                }
            }

            int[][] zero = ArrayUtils.CreateJaggedArray<int>(0, 0);
            expected = ArrayUtils.CreateJaggedArray<int>(0, 0);
            result = ArrayUtils.Transpose(zero);
            Assert.AreEqual(expected.Length, result.Length);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void TestTranspose_double()
        {
            double[][] a = { new double[] { 1, 2, 3, 4 }, new double[] { 5, 6, 7, 8 } };
            double[][] expected = { new double[] { 1, 5 }, new double[] { 2, 6, }, new double[] { 3, 7, }, new double[] { 4, 8 } };

            double[][] result = ArrayUtils.Transpose(a);
            for (int i = 0; i < expected.Length; i++)
            {
                for (int j = 0; j < expected[i].Length; j++)
                {
                    Assert.AreEqual(expected[i][j], result[i][j], 0.0);
                }
            }

            double[][] zero = ArrayUtils.CreateJaggedArray<double>(0, 0);
            expected = ArrayUtils.CreateJaggedArray<double>(0, 0);
            result = ArrayUtils.Transpose(zero);
            Assert.AreEqual(expected.Length, result.Length);
            Assert.AreEqual(0, result.Length);
        }

        [TestMethod]
        public void TestDot_int()
        {
            int[][] a = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };
            int[][] b = new int[][] { new int[] { 1, 1 }, new int[] { 1, 1 } };

            int[][] c = ArrayUtils.Dot(a, b);

            Assert.AreEqual(3, c[0][0]);
            Assert.AreEqual(3, c[0][1]);
            Assert.AreEqual(7, c[1][0]);
            Assert.AreEqual(7, c[1][1]);

            // Single dimension
            int[][] x = new int[][] { new int[] { 2, 2, 2 } };
            b = new int[][] { new int[] { 3 }, new int[] { 3 }, new int[] { 3 } };

            c = ArrayUtils.Dot(x, b);

            Assert.IsTrue(c.Length == 1);
            Assert.IsTrue(c[0].Length == 1);
            Assert.AreEqual(c[0][0], 18);


            // Ensure un-aligned dimensions get reported
            b = new int[][] { new int[] { 0, 0 }, new int[] { 0, 0 }, new int[] { 0, 0 } };
            try
            {
                ArrayUtils.Dot(a, b);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("Matrix inner dimensions must agree.", e.Message);
            }

            // Test 2D.1d
            a = new int[][] { new int[] { 1, 2 }, new int[] { 3, 4 } };
            int[] b2 = new int[] { 2, 2 };
            int[] result = ArrayUtils.Dot(a, b2);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 6, 14 }, result));
        }

        [TestMethod]
        public void TestDot_double()
        {
            double[][] a = { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 } };
            double[][] b = { new[] { 1.0, 1.0 }, new[] { 1.0, 1.0 } };

            double[][] c = ArrayUtils.Dot(a, b);

            Assert.AreEqual(3, c[0][0], 0.0);
            Assert.AreEqual(3, c[0][1], 0.0);
            Assert.AreEqual(7, c[1][0], 0.0);
            Assert.AreEqual(7, c[1][1], 0.0);

            // Single dimension
            double[][] x = new double[][] { new[] { 2.0, 2.0, 2.0 } };
            b = new double[][] { new[] { 3.0 }, new[] { 3.0 }, new[] { 3.0 } };

            c = ArrayUtils.Dot(x, b);

            Assert.IsTrue(c.Length == 1);
            Assert.IsTrue(c[0].Length == 1);
            Assert.AreEqual(c[0][0], 18.0, 0.0);


            // Ensure un-aligned dimensions get reported
            b = new double[][] { new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 }, new[] { 0.0, 0.0 } };
            try
            {
                ArrayUtils.Dot(a, b);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("Matrix inner dimensions must agree.", e.Message);
            }

            // Test 2D.1d
            a = new double[][] { new[] { 1.0, 2.0 }, new[] { 3.0, 4.0 } };
            double[] b2 = new double[] { 2.0, 2.0 };
            double[] result = ArrayUtils.Dot(a, b2);
            Assert.IsTrue(Arrays.AreEqual(new double[] { 6.0, 14.0 }, result));
        }

        [TestMethod]
        public void TestZip()
        {
            int[] t1 = { 1, 2, 3 };
            int[] t2 = { 4, 5, 6 };
            List<Tuple> tuples = ArrayUtils.Zip(t1, t2);
            Assert.AreEqual(3, tuples.Count);
            Assert.IsTrue(
                ((int)tuples[0].Get(0)) == 1 &&
                ((int)tuples[0].Get(1)) == 4 &&
                ((int)tuples[1].Get(0)) == 2 &&
                ((int)tuples[1].Get(1)) == 5 &&
                ((int)tuples[2].Get(0)) == 3 &&
                ((int)tuples[2].Get(1)) == 6);
        }

        [TestMethod]
        public void TestZipList()
        {
            List<int> t1 =new List<int> { 1, 2, 3 };
            List<int> t2 = new List<int> { 4, 5, 6 };
            List<Tuple> tuples = ArrayUtils.Zip(t1, t2);
            Assert.AreEqual(3, tuples.Count);
            Assert.IsTrue(
                ((int)tuples[0].Get(0)) == 1 &&
                ((int)tuples[0].Get(1)) == 4 &&
                ((int)tuples[1].Get(0)) == 2 &&
                ((int)tuples[1].Get(1)) == 5 &&
                ((int)tuples[2].Get(0)) == 3 &&
                ((int)tuples[2].Get(1)) == 6);
        }


        [TestMethod]
        public void TestTo1D()
        {
            int[][] test = { new int[] { 1, 2 }, new int[] { 3, 4 } };
            int[] expected = { 1, 2, 3, 4 };
            int[] result = ArrayUtils.To1D(test);
            Assert.IsTrue(Arrays.AreEqual(expected, result));

            // Test double version
            double[][] d_test = { new double[] { 1.0, 2.0 }, new double[] { 3.0, 4.0 } };
            double[] d_expected = { 1.0, 2.0, 3.0, 4.0 };
            double[] d_result = ArrayUtils.To1D(d_test);
            Assert.IsTrue(Arrays.AreEqual(d_expected, d_result));
        }

        [TestMethod]
        public void TestFromCoordinate()
        {
            int[] shape = { 2, 2 };
            int[] testCoord = { 1, 1 };
            int result = ArrayUtils.FromCoordinate(testCoord, shape);
            Assert.AreEqual(3, result);
        }

        [TestMethod]
        public void TestReshape_int()
        {
            int[][] test =
            {
                new[] {0, 1, 2, 3, 4, 5},
                new[] {6, 7, 8, 9, 10, 11}
            };

            int[][] expected =
            {
                new[] {0, 1, 2},
                new[] {3, 4, 5},
                new[] {6, 7, 8},
                new[] {9, 10, 11}
            };

            int[][] result = ArrayUtils.Reshape(test, 3);
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    Assert.AreEqual(expected[i][j], result[i][j]);
                }
            }

            // Unhappy case
            try
            {
                ArrayUtils.Reshape(test, 5);
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("12 is not evenly divisible by 5", e.Message);
            }

            // Test zero-length case
            int[][] result4 = ArrayUtils.Reshape(new int[0][], 5);
            Assert.IsNotNull(result4);
            Assert.IsTrue(result4.Length == 0);

            // Test empty array arg
            test = new int[][] { };
            expected = new int[][] { };//new int[0][0];
            result = ArrayUtils.Reshape(test, 1);
            Assert.IsTrue(expected.SequenceEqual(result));//  Arrays.AreEqual(expected, result));
        }

        [TestMethod]
        public void TestReshape_double()
        {
            double[][] test =
            {
               new double[]  {0, 1, 2, 3, 4, 5},
               new double[]  {6, 7, 8, 9, 10, 11}
            };

            double[][] expected =
            {
               new double[]  {0, 1, 2},
               new double[]  {3, 4, 5},
               new double[]  {6, 7, 8},
              new double[]   {9, 10, 11}
            };

            double[][] result = ArrayUtils.Reshape(test, 3);
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    Assert.AreEqual(expected[i][j], result[i][j], 0.0);
                }
            }

            // Unhappy case
            try
            {
                ArrayUtils.Reshape(test, 5);
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("12 is not evenly divisible by 5", e.Message);
            }

            // Test zero-length case
            double[][] result4 = ArrayUtils.Reshape(new double[0][], 5);
            Assert.IsNotNull(result4);
            Assert.IsTrue(result4.Length == 0);

            // Test empty array arg
            test = new double[][] { };
            expected = ArrayUtils.CreateJaggedArray<double>(0, 0);// new double[0][0];
            result = ArrayUtils.Reshape(test, 1);
            Assert.IsTrue(expected.SequenceEqual(result)); //Arrays.AreEqual(expected, result));
        }

        [TestMethod]
        public void TestRavelAndUnRavel()
        {
            int[] test = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 };
            int[][] expected =
            {
                new[] {0, 1, 2, 3, 4, 5},
                new[] {6, 7, 8, 9, 10, 11}
            };

            int[][] result = ArrayUtils.Ravel(test, 6);
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    Assert.AreEqual(expected[i][j], result[i][j]);
                }
            }

            int[] result2 = ArrayUtils.Unravel(result);
            for (int i = 0; i < result2.Length; i++)
            {
                Assert.AreEqual(test[i], result2[i]);
            }

            // Unhappy case
            try
            {
                ArrayUtils.Ravel(test, 5);
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("12 is not evenly divisible by 5", e.Message);
            }

            // Test zero-length case
            int[] result4 = ArrayUtils.Unravel(new int[0][]);
            Assert.IsNotNull(result4);
            Assert.IsTrue(result4.Length == 0);
        }

        [TestMethod]
        public void TestRotateRight()
        {
            int[][] test = new int[][]
            {
                new[] {1, 0, 1, 0},
                new[] {1, 0, 1, 0},
                new[] {1, 0, 1, 0},
                new[] {1, 0, 1, 0}
            };

            int[][] expected = new int[][]
            {
                new[] {1, 1, 1, 1},
                new[] {0, 0, 0, 0},
                new[] {1, 1, 1, 1},
                new[] {0, 0, 0, 0}
            };

            int[][] result = ArrayUtils.RotateRight(test);
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    Assert.AreEqual(result[i][j], expected[i][j]);
                }
            }

            // Test empty array arg
            test = new int[][] { };
            expected = ArrayUtils.CreateJaggedArray<int>(0, 0); //new int[0][0];
            result = ArrayUtils.RotateRight(test);
            Assert.IsTrue(expected.SequenceEqual(result));// Arrays.AreEqual(expected, result));
        }

        [TestMethod]
        public void TestRotateLeft()
        {
            int[][] test = new int[][] {
           new[] { 1, 0, 1, 0 },
           new[] { 1, 0, 1, 0 },
           new[] { 1, 0, 1, 0 },
           new[] { 1, 0, 1, 0 }
        };

            int[][] expected = new int[][] {
          new[]  { 0, 0, 0, 0 },
          new[]  { 1, 1, 1, 1 },
          new[]  { 0, 0, 0, 0 },
          new[]  { 1, 1, 1, 1 }
        };

            int[][] result = ArrayUtils.RotateLeft(test);
            for (int i = 0; i < result.Length; i++)
            {
                for (int j = 0; j < result[i].Length; j++)
                {
                    Assert.AreEqual(result[i][j], expected[i][j]);
                }
            }

            // Test empty array arg
            test = new int[][] { };
            expected = ArrayUtils.CreateJaggedArray<int>(0, 0); //new int[0][0];
            result = ArrayUtils.RotateLeft(test);
            Assert.IsTrue(expected.SequenceEqual(result));//.AreEqual(expected, result));
        }

        [TestMethod]
        public void TestSubst()
        {
            int[] original = new int[] { 30, 30, 30, 30, 30 };
            int[] substitutes = new int[] { 0, 1, 2, 3, 4 };
            int[] substInds = new int[] { 4, 1, 3 };

            int[] expected = { 30, 1, 30, 3, 4 };

            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.Subst(original, substitutes, substInds)));
        }

        [TestMethod]
        public void TestSubst_doubles()
        {
            double[] original = new double[] { 30, 30, 30, 30, 30 };
            double[] substitutes = new double[] { 0, 1, 2, 3, 4 };
            int[] substInds = new int[] { 4, 1, 3 };

            double[] expected = { 30, 1, 30, 3, 4 };

            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.Subst(original, substitutes, substInds)));
        }

        [TestMethod]
        public void TestMin_int()
        {
            int[][] protoA = { new[] { 49, 2, 3, 4, 5, 6, 7, 8, 9, 10 } };
            int[][] resh = ArrayUtils.Reshape(protoA, 5);
            int[] a = ArrayUtils.Min(resh, 0);
            int[] b = ArrayUtils.Min(resh, 1);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 6, 2, 3, 4, 5 }, a));
            Assert.IsTrue(Arrays.AreEqual(new int[] { 2, 6 }, b));

            try
            {
                ArrayUtils.Min(resh, 3);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("axis must be either '0' or '1'", e.Message);
            }
        }

        [TestMethod]
        public void TestMin_double()
        {
            double[][] protoA = { new double[] { 49, 2, 3, 4, 5, 6, 7, 8, 9, 10 } };
            double[][] resh = ArrayUtils.Reshape(protoA, 5);
            double[] a = ArrayUtils.Min(resh, 0);
            double[] b = ArrayUtils.Min(resh, 1);
            Assert.IsTrue(Arrays.AreEqual(new double[] { 6, 2, 3, 4, 5 }, a));
            Assert.IsTrue(Arrays.AreEqual(new double[] { 2, 6 }, b));

            try
            {
                ArrayUtils.Min(resh, 3);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentException));
                Assert.AreEqual("axis must be either '0' or '1'", e.Message);
            }
        }

        /**
         * This test has two purposes: 
         * 1. Test specific branch in KNNClassifier learn() method
         * 2. Test its name sake: ArrayUtils.SetRangeTo()
         */
        [TestMethod]
        public void TestSetRangeTo()
        {
            int[] thresholdedInput = { 49, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int cellsPerCol = 5;

            // Make thresholdedInput into 2D array for calling ArrayUtils.Min()
            // thresholdedInput = { { 49, 2, 3, 4, 5 }, { 6, 7, 8, 9, 10 } };
            int[][] burstingCols = ArrayUtils.Reshape(new int[][] { thresholdedInput }, cellsPerCol);
            // get minimum values in each row = { 2, 6 }
            int[] bc = ArrayUtils.Min(burstingCols, 1);
            // get indexes of min = { 0, 1 }
            bc = ArrayUtils.Where(bc, ArrayUtils.INT_GREATER_THAN_0);

            // Use produced indexes to test setRangeTo in complicated manner, 
            // setting each value not in the calculated indexes to "0"
            foreach (double col in bc)
            {
                ArrayUtils.SetRangeTo(
                    thresholdedInput,
                    (((int)col) * cellsPerCol) + 1,
                    (((int)col) * cellsPerCol) + cellsPerCol,
                    0
                );
            }
            // Every index set to zero except range start - 1 and stop
            Assert.IsTrue(Arrays.AreEqual(new int[] { 49, 0, 0, 0, 0, 6, 0, 0, 0, 0 }, thresholdedInput));
        }

        [TestMethod]
        public void TestMaxIndex()
        {
            int max = ArrayUtils.MaxIndex(new int[] { 2, 4, 5 });
            Assert.AreEqual(39, max);
        }

        [TestMethod]
        public void TestToCoordinates()
        {
            int[] coords = ArrayUtils.ToCoordinates(19, new int[] { 2, 4, 5 }, false);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 0, 3, 4 }, coords));

            coords = ArrayUtils.ToCoordinates(19, new int[] { 2, 4, 5 }, true);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 3, 0 }, coords));
        }

        [TestMethod]
        public void TestArgsort()
        {
            int[] args = ArrayUtils.Argsort(new int[] { 11, 2, 3, 7, 0 });
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2, 3, 0 }, args));

            args = ArrayUtils.Argsort(new int[] { 11, 2, 3, 7, 0 }, -1, -1);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2, 3, 0 }, args));

            args = ArrayUtils.Argsort(new int[] { 11, 2, 3, 7, 0 }, 0, 3);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2 }, args));

            // Test double version
            int[] d_args = ArrayUtils.Argsort(new double[] { 11, 2, 3, 7, 0 }, 0, 3);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2 }, d_args));

            d_args = ArrayUtils.Argsort(new double[] { 11, 2, 3, 7, 0 }, -1, 3);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2, 3, 0 }, d_args));

            // Test Vector version
            int[] v_args = ArrayUtils.Argsort(Vector<double>.Build.SparseOfArray(new double[] { 11, 2, 3, 7, 0 }), 0, 3);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2 }, v_args));

            v_args = ArrayUtils.Argsort(Vector<double>.Build.SparseOfArray(new double[] { 11, 2, 3, 7, 0 }), -1, 3);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 4, 1, 2, 3, 0 }, v_args));
        }

        [TestMethod]
        public void TestShape()
        {
            int[][] inputPattern = { new[] { 2, 3, 4, 5 }, new[] { 6, 7, 8, 9 } };
            int[] shape = ArrayUtils.Shape(inputPattern);
            Assert.IsTrue(Arrays.AreEqual(new int[] { 2, 4 }, shape));
        }

        [TestMethod]
        public void TestConcat()
        {
            // Test happy path
            double[] one = new double[] { 1.0, 2.0, 3.0 };
            double[] two = new double[] { 4.0, 5.0, 6.0 };
            double[] retVal = ArrayUtils.Concat(one, two);
            Assert.AreEqual(6, retVal.Length);
            for (int i = 0; i < retVal.Length; i++)
            {
                Assert.AreEqual(i + 1, retVal[i], 0);
            }

            // Test unequal sizes
            one = new double[] { 1.0, 2.0 };
            retVal = ArrayUtils.Concat(one, two);
            Assert.AreEqual(5, retVal.Length);
            for (int i = 0; i < retVal.Length; i++)
            {
                if (i == 2) continue;
                Assert.AreEqual(i + 1, retVal[i > 2 ? i - 1 : i], 0);
            }

            one = new double[] { 1.0, 2.0, 3.0 };
            two = new double[] { 4.0, 5.0 };
            retVal = ArrayUtils.Concat(one, two);
            Assert.AreEqual(5, retVal.Length);
            for (int i = 0; i < retVal.Length; i++)
            {
                Assert.AreEqual(i + 1, retVal[i], 0);
            }

            //Test zero length
            one = new double[0];
            two = new double[] { 4.0, 5.0, 6.0 };
            retVal = ArrayUtils.Concat(one, two);
            Assert.AreEqual(3, retVal.Length);
            for (int i = 0; i < retVal.Length; i++)
            {
                Assert.AreEqual(i + 4, retVal[i], 0);
            }

            one = new double[] { 1.0, 2.0, 3.0 };
            two = new double[0];
            retVal = ArrayUtils.Concat(one, two);
            Assert.AreEqual(3, retVal.Length);
            for (int i = 0; i < retVal.Length; i++)
            {
                Assert.AreEqual(i + 1, retVal[i], 0);
            }

        }

        [TestMethod]
        public void TestInterleave()
        {
            string[] f = { "0" };
            double[] s = { 0.8 };

            // Test most simple interleave of equal length arrays
            object[] result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual("0", result[0]);
            Assert.AreEqual(0.8, result[1]);

            // Test simple interleave of larger array
            f = new string[] { "0", "1" };
            s = new double[] { 0.42, 2.5 };
            result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual("0", result[0]);
            Assert.AreEqual(0.42, result[1]);
            Assert.AreEqual("1", result[2]);
            Assert.AreEqual(2.5, result[3]);

            // Test complex interleave of larger array
            f = new string[] { "0", "1", "bob", "harry", "digit", "temperature" };
            s = new double[] { 0.42, 2.5, .001, 1e-2, 34.0, .123 };
            result = ArrayUtils.Interleave(f, s);
            for (int i = 0, j = 0; j < result.Length; i++, j += 2)
            {
                Assert.AreEqual(f[i], result[j]);
                Assert.AreEqual(s[i], result[j + 1]);
            }

            // Test interleave with zero length of first
            f = new string[0];
            s = new double[] { 0.42, 2.5 };
            result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual(0.42, result[0]);
            Assert.AreEqual(2.5, result[1]);

            // Test interleave with zero length of second
            f = new string[] { "0", "1" };
            s = new double[0];
            result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual("0", result[0]);
            Assert.AreEqual("1", result[1]);

            // Test complex unequal length: left side smaller
            f = new string[] { "0", "1", "bob" };
            s = new double[] { 0.42, 2.5, .001, 1e-2, 34.0, .123 };
            result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual("0", result[0]);
            Assert.AreEqual(0.42, result[1]);
            Assert.AreEqual("1", result[2]);
            Assert.AreEqual(2.5, result[3]);
            Assert.AreEqual("bob", result[4]);
            Assert.AreEqual(.001, result[5]);
            Assert.AreEqual(1e-2, result[6]);
            Assert.AreEqual(34.0, result[7]);
            Assert.AreEqual(.123, result[8]);

            // Test complex unequal length: right side smaller
            f = new string[] { "0", "1", "bob", "harry", "digit", "temperature" };
            s = new double[] { 0.42, 2.5, .001 };
            result = ArrayUtils.Interleave(f, s);
            Assert.AreEqual("0", result[0]);
            Assert.AreEqual(0.42, result[1]);
            Assert.AreEqual("1", result[2]);
            Assert.AreEqual(2.5, result[3]);
            Assert.AreEqual("bob", result[4]);
            Assert.AreEqual(.001, result[5]);
            Assert.AreEqual("harry", result[6]);
            Assert.AreEqual("digit", result[7]);
            Assert.AreEqual("temperature", result[8]);

            // Negative testing
            try
            {
                f = null;
                s = new double[] { 0.42, 2.5, .001 };
                result = ArrayUtils.Interleave(f, s);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(NullReferenceException));
            }
        }

        [TestMethod]
        public void TestIn1D()
        {
            int[] ar1 = { 0, 1, 5, 9, 3, 1000 };
            int[] ar2 = Arrays.CopyOf(ar1, ar1.Length);
            Assert.IsTrue(Arrays.AreEqual(ar1, ar2));
            int[] retVal = ArrayUtils.In1d(ar1, ar2);
            Assert.IsTrue(Arrays.AreEqual(ar1, (retVal)));

            ar1 = new int[] { 0, 2, 1000 };
            int[] expected = { 0, 1000 };
            Assert.IsTrue(Arrays.AreEqual(expected, (ArrayUtils.In1d(ar1, ar2))));

            ar1 = new int[] { 2, 6, 4 };
            expected = new int[0];
            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.In1d(ar1, ar2)));

            // Test none in the second
            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.In1d(ar1, expected)));
            // Test none in both
            Assert.IsTrue(Arrays.AreEqual(expected, ArrayUtils.In1d(expected, expected)));
        }

        [TestMethod]
        public void TestRecursiveCoordinatesAssemble() //throws InterruptedException
        {
            /*Create huge 5 dimensional matrix*/
            int dimSize = 14, dimNumber = 5;
            int[] dimCoordinates = new int[dimSize];
            List<int[]> dimensions = new List<int[]>();

            for (int i = 0; i < dimNumber; i++)
            {
                for (int j = 0; j < dimSize; j++)
                {
                    dimCoordinates[j] = j;
                }
                dimensions.Add(dimCoordinates);
            }

            Stopwatch watch = new Stopwatch();
            watch.Start();
            List<int[]> neighborList = ArrayUtils.DimensionsToCoordinateList(dimensions);
            watch.Stop();

            Console.WriteLine("Execute in:" + watch.ElapsedMilliseconds + " milliseconds");

            Assert.AreEqual(neighborList.Count, 537824);
        }

        [TestMethod]
        public void TestContains()
        {
            int[] sequence = new[] {10, 20, 30};

            List<int[]> otherSequences = new List<int[]>
            {
                new[] {11, 20, 30},
                new[] {12, 22, 30},
                new[] {13, 23, 34},
                new[] {14, 0, 10},
            };

            Assert.IsFalse(ArrayUtils.Contains(sequence, otherSequences));
            otherSequences.Add(new[] { 20, 10, 30 });
            Assert.IsFalse(ArrayUtils.Contains(sequence, otherSequences));
            otherSequences.Add(new[] { 10, 20, 30 });
            Assert.IsTrue(ArrayUtils.Contains(sequence, otherSequences));
        }

        /**
         * Python does modulus operations differently than the rest of the world
         * (C++ or Java) so...
         */
        [TestMethod]
        public void TestModulo()
        {
            int a = -7;
            int n = 5;
            Assert.AreEqual(3, ArrayUtils.Modulo(a, n));

            //Example A
            a = 5;
            n = 2;
            Assert.AreEqual(1, ArrayUtils.Modulo(a, n));

            //Example B
            a = 5;
            n = 3;
            Assert.AreEqual(2, ArrayUtils.Modulo(a, n));

            //Example C
            a = 10;
            n = 3;
            Assert.AreEqual(1, ArrayUtils.Modulo(a, n));

            //Example D
            a = 9;
            n = 3;
            Assert.AreEqual(0, ArrayUtils.Modulo(a, n));

            //Example E
            a = 3;
            n = 0;
            try
            {
                Assert.AreEqual(3, ArrayUtils.Modulo(a, n));
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Division by zero!", e.Message);
            }

            //Example F
            a = 2;
            n = 10;
            Assert.AreEqual(2, ArrayUtils.Modulo(a, n));
        }

        [TestMethod]
        public void TestAnd()
        {
            int[] a = new int[] { 0, 0, 0, 0, 1, 1, 1 };
            int[] b = new int[] { 0, 0, 0, 0, 1, 1, 1 };
            int[] result = ArrayUtils.And(a, b);
            Assert.IsTrue(Arrays.AreEqual(a, result));

            a = new int[] { 0, 0, 0, 0, 1, 0, 1 };
            result = ArrayUtils.And(a, b);
            Assert.IsTrue(Arrays.AreEqual(a, result));

            a = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            result = ArrayUtils.And(a, b);
            Assert.IsTrue(Arrays.AreEqual(a, result));

            a = new int[] { 1, 1, 1, 1, 0, 0, 0 };
            int[] expected = new int[] { 0, 0, 0, 0, 0, 0, 0 };
            result = ArrayUtils.And(a, b);
            Assert.IsTrue(Arrays.AreEqual(expected, result));
        }

        [TestMethod]
        public void TestBitsToString()
        {
            string expected = "c....***";
            string result = ArrayUtils.BitsToString(new int[] { 0, 0, 0, 0, 1, 1, 1 });
            Assert.AreEqual(expected, result);
        }

        [TestMethod]
        public void TestDiff()
        {
            double[] t = new double[] { 5, 4, 3, 2, 1, 0 };
            double[] result = ArrayUtils.Diff(t);
            Assert.AreEqual(5, result.Length);
            Assert.IsTrue(Arrays.AreEqual(new double[] { -1, -1, -1, -1, -1 }, result));
            Assert.AreEqual(-5, ArrayUtils.Sum(result), 0);
        }

        [TestMethod]
        public void TestMultiDimensionArrayOperation()
        {
            int[] dimensions = { 5, 5, 5 };
            Array multiDimArray = CreateMultiDimensionArray(dimensions);
            ArrayUtils.FillArray(multiDimArray, 1);
            Assert.AreEqual(125, ArrayUtils.AggregateArray(multiDimArray));
        }

        [TestMethod]
        public void TestMultiDimensionArrayOperationJagged()
        {
            //int[] dimensions = { 5, 5, 5 };
            Array multiDimArray = ArrayUtils.CreateJaggedArray<int>(5, 5, 5);
            ArrayUtils.FillArray(multiDimArray, 1);
            Assert.AreEqual(125, ArrayUtils.AggregateArray(multiDimArray));
        }

        private Array CreateMultiDimensionArray(int[] sizes)
        {
            return Array.CreateInstance(typeof(int), sizes);
        }

        [TestMethod]
        public void TestConcatAll()
        {
            Assert.IsTrue(Arrays.AreEqual(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 },
                ArrayUtils.ConcatAll(new int[] { 1, 2 }, new int[] { 3, 4, 5, 6, 7 }, new int[] { 8, 9, 0 })));
        }

        [TestMethod]
        public void TestReplace()
        {
            Assert.IsTrue(Arrays.AreEqual(new int[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 0 },
                ArrayUtils.Replace(5, 10, new int[] { 1, 2, 3, 4, 5, -1, -1, -1, -1, -1 }, new int[] { 6, 7, 8, 9, 0 })));

        }

        [TestMethod]
        public void TestIsSparse()
        {
            int[] t = new int[] { 0, 1, 0 };
            int[] t1 = new int[] { 4, 5, 6, 7 };

            Assert.IsFalse(ArrayUtils.IsSparse(t));
            Assert.IsTrue(ArrayUtils.IsSparse(t1));
        }

        [TestMethod]
        public void TestNGreatest()
        {
            double[] overlaps = new double[] { 1, 2, 1, 4, 8, 3, 12, 5, 4, 1 };
            Assert.IsTrue(Arrays.AreEqual(new int[] { 6, 4, 7 }, ArrayUtils.NGreatest(overlaps, 3)));
        }

        [TestMethod]
        public void TestMean()
        {
            double[] a = new[] {1,2,4.0};
            double mean = ArrayUtils.Mean(a);
            Assert.AreEqual(2.3333, mean, 0.0001);
        }

        [TestMethod]
        public void TestMeanMatrixAxis0()
        {
            double[][] a = new[]
            {
                new[] { 1.0, 2.0 },
                new[] { 3.0, 4.0 },
            };
            double[] mean = ArrayUtils.Mean(a, 0);
            
            Assert.AreEqual(2.0, mean[0]);
            Assert.AreEqual(3.0, mean[1]);
        }

        [TestMethod]
        public void TestMeanMatrixAxis1()
        {
            double[][] a = new[]
            {
                new[] { 1.0, 2.0 },
                new[] { 3.0, 4.0 },
            };
            double[] mean = ArrayUtils.Mean(a, 1);

            Assert.AreEqual(1.5, mean[0]);
            Assert.AreEqual(3.5, mean[1]);
        }

        [TestMethod]
        public void Sample_ReturnsUniqueRandomSample()
        {
            // Arrange
            int[] choices = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10 };
            int[] selectedIndices = new int[5];
            IRandom random = new XorshiftRandom(42);

            // Act
            int[] result = ArrayUtils.Sample(choices, ref selectedIndices, random);

            // Assert

            // Ensure the result is not null
            Assert.IsNotNull(result);

            // Ensure the result has the same length as selectedIndices
            Assert.AreEqual(selectedIndices.Length, result.Length);

            // Ensure all values in the result are within the range of choices
            foreach (int index in result)
            {
                Assert.IsTrue(choices.Contains(index));
            }

            // Ensure the result array contains unique values
            Assert.AreEqual(result.Length, result.Distinct().Count());

            // Ensure the selectedIndices array has been modified
            CollectionAssert.AreEqual(result, selectedIndices);
        }

        [TestMethod]
        public void Sample_LargeArray_Performance()
        {
            // Arrange
            int sampleSize = 10000;
            List<int> choices = new List<int>();
            for (int i = 0; i < 100000; i++)
            {
                choices.Add(i);
            }
            IRandom random = new XorshiftRandom(42);

            // Act
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int[] result = ArrayUtils.Sample(sampleSize, choices, random);
            watch.Stop();

            // Assert
            Console.WriteLine($"Elapsed time: {watch.ElapsedMilliseconds} ms");
            Assert.AreEqual(sampleSize, result.Length);

            // Ensure the result array contains unique values
            Assert.AreEqual(result.Length, result.Distinct().Count());
        }

        [TestMethod]
        public void SampleFast_LargeArray_Performance()
        {
            // Arrange
            int sampleSize = 10000;
            int[] choices = new int[100000];
            for (int i = 0; i < choices.Length; i++)
            {
                choices[i] = i;
            }
            int[] selectedIndices = new int[sampleSize];
            IRandom random = new XorshiftRandom(42);

            // Act
            var watch = System.Diagnostics.Stopwatch.StartNew();
            int[] result = ArrayUtils.SampleFast(choices, ref selectedIndices, random);
            watch.Stop();

            // Assert
            Console.WriteLine($"Elapsed time: {watch.ElapsedMilliseconds} ms");
            Assert.AreEqual(sampleSize, result.Length);

            // Ensure the result array contains unique values
            Assert.AreEqual(result.Length, result.Distinct().Count());
        }
    }
}