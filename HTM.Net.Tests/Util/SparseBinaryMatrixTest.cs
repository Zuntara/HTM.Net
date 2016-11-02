using System;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class SparseBinaryMatrixTest
    {

        private int[] dimensions = new int[] { 5, 10 };

        [TestMethod]
        public void TestBackingStoreAndSliceAccess()
        {
            DoTestBackingStoreAndSliceAccess(new SparseBinaryMatrix(this.dimensions));
            //doTestBackingStoreAndSliceAccess(new LowMemorySparseBinaryMatrix(this.dimensions));
            //doTestBackingStoreAndSliceAccess(new FastConnectionsMatrix(this.dimensions));
        }

        private void DoTestBackingStoreAndSliceAccess(AbstractSparseBinaryMatrix sm)
        {
            int[][] connectedSynapses = new int[][]
            {
                new int[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new int[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new int[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new int[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new int[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };

            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            for (int i = 0; i < connectedSynapses.Length; i++)
            {
                for (int j = 0; j < connectedSynapses[i].Length; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], ((SparseByteArray)sm.GetSlice(i))[j], 0);
                }
            }

            //Make sure warning is proper for exact access
            try
            {
                sm.GetSlice(0, 4);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("This method only returns the array holding the specified index: " +
                        Arrays.ToString(new int[] { 0, 4 }), e.Message);
            }
        }

        [TestMethod]
        public void TestRightVecSumAtNzFast()
        {
            DoTestRightVecSumAtNzFast(new SparseBinaryMatrix(this.dimensions));
            //doTestRightVecSumAtNZFast(new LowMemorySparseBinaryMatrix(this.dimensions));
            //doTestRightVecSumAtNZFast(new FastConnectionsMatrix(this.dimensions));
        }

        private void DoTestRightVecSumAtNzFast(AbstractSparseBinaryMatrix sm)
        {
            int[] dimensions = new int[] { 5, 10 };
            int[][] connectedSynapses = new int[][]
            {
                new int[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new int[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new int[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new int[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new int[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };

            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            int[] inputVector = new int[] { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0 };
            int[] results = new int[5];
            int[] trueResults = new int[] { 1, 1, 1, 1, 1 };
            sm.RightVecSumAtNZ(inputVector, results);

            for (int i = 0; i < results.Length; i++)
            {
                Assert.AreEqual(trueResults[i], results[i]);
            }

            ///////////////////////

            connectedSynapses = new int[][]
            {
                new int[] {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                new int[] {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
                new int[] {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
                new int[] {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
                new int[] {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}
            };
            sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            results = new int[5];
            trueResults = new int[] { 10, 8, 6, 4, 2 };
            sm.RightVecSumAtNZ(inputVector, results);

            for (int i = 0; i < results.Length; i++)
            {
                Assert.AreEqual(trueResults[i], results[i]);
            }
        }

        [TestMethod]
        public void TestSetTrueCount()
        {
            DoTestSetTrueCount(new SparseBinaryMatrix(this.dimensions));
            //doTestSetTrueCount(new LowMemorySparseBinaryMatrix(this.dimensions));
            //doTestSetTrueCount(new FastConnectionsMatrix(this.dimensions));
        }

        private void DoTestSetTrueCount(AbstractSparseBinaryMatrix sm)
        {
            int[][] connectedSynapses = new int[][]
            {
                new int[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new int[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new int[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new int[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new int[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };

            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(2, sm.GetTrueCount(i));
            }
        }


        public static void FillWithSomeRandomValues(Object array, MersenneTwister r, params int[] sizes)
        {
            for (int i = 0; i < sizes[0]; i++)
                if (sizes.Length == 1)
                {
                    ((int[])array)[i] = r.NextInt(2);
                }
                else {
                    FillWithSomeRandomValues(((Array)array).GetValue(i), r, ArrayUtils.Tail(sizes));
                }
        }

        [TestMethod]
        public void TestBackingStoreAndSliceAccessManyDimensions()
        {
            /*Create 3 dimensional matrix*/
            int[] dimensions = { 5, 5, 5 };
            DoTestBackingStoreAndSliceAccessManyDimensions(new SparseBinaryMatrix(dimensions));
            //doTestBackingStoreAndSliceAccessManyDimensions(new LowMemorySparseBinaryMatrix(dimensions));
        }

        private void DoTestBackingStoreAndSliceAccessManyDimensions(AbstractSparseBinaryMatrix sm)
        {
            /*set diagonal element to true*/
            sm.Set(1, 0, 0, 0);
            sm.Set(1, 1, 1, 1);
            sm.Set(1, 2, 2, 2);
            sm.Set(1, 3, 3, 3);
            sm.Set(1, 4, 4, 4);
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        if (k == j & j == i)
                        {
                            Assert.AreEqual(1, sm.GetIntValue(i, j, k));
                        }
                    }
                }
            }
            SparseByteArray slice = (SparseByteArray)sm.GetSlice(4, 4);
            for (int i = 0; i < 5; i++)
            {
                Assert.AreEqual(1, sm.GetTrueCount(i));
            }
            Console.WriteLine("slice:" + ArrayUtils.IntArrayToString(slice));
            Assert.AreEqual(1, slice[4]);
            /*update first row to true, other to false*/
            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 5; j++)
                {
                    for (int k = 0; k < 5; k++)
                    {
                        if (0 == i)
                        {
                            sm.Set(1, i, j, k);
                        }
                        else {
                            sm.Set(0, i, j, k);
                        }
                    }
                }
            }
            Assert.AreEqual(25, sm.GetTrueCounts()[0]);
            Assert.AreEqual(0, sm.GetTrueCounts()[1]);

        }

        [TestMethod]
        public void TestArraySet()
        {
            int[] dimensions = { 5, 2 };
            DoTestArraySet(new SparseBinaryMatrix(dimensions));
            //doTestArraySet(new LowMemorySparseBinaryMatrix(dimensions));
            //doTestArraySet(new FastConnectionsMatrix(dimensions));
        }

        private void DoTestArraySet(AbstractSparseBinaryMatrix sm)
        {
            int[] expected = { 1, 0, 0, 0, 1, 0, 1, 1, 1, 0 };
            int[] values = { 1, 1, 1, 1, 1 };
            int[] indexes = { 0, 4, 6, 7, 8 };
            sm.Set(indexes, values);
            int[] dense = new int[sm.GetMaxIndex() + 1];

            for (int i = 0; i < sm.GetMaxIndex() + 1; i++)
            {
                dense[i] = sm.GetIntValue(i);
            }

            Assert.IsTrue(Arrays.AreEqual(expected, dense));
        }

        [TestMethod]
        public void TestGetSparseIndices()
        {
            DoTestGetSparseIndices(new SparseBinaryMatrix(this.dimensions));
            //doTestGetSparseIndices(new LowMemorySparseBinaryMatrix(this.dimensions));
            //doTestGetSparseIndices(new FastConnectionsMatrix(this.dimensions));
        }

        private void DoTestGetSparseIndices(AbstractSparseBinaryMatrix sm)
        {
            int[] expected = { 0, 5, 11, 16, 22, 27, 33, 38, 44, 49 };
            int[][] values = new int[][]
            {
                new int[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new int[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new int[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new int[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new int[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    sm.Set(values[i][j], new int[] { i, j });
                }
            }

            int[] sdr = sm.GetSparseIndices();
            Assert.IsTrue(Arrays.AreEqual(expected, sdr));
        }

        [TestMethod]
        public void TestSliceIndexes()
        {
            SparseBinaryMatrix sm = new SparseBinaryMatrix(this.dimensions);
            int[][] expected =  {
            new int[]    {0, 1, 2, 3, 4, 5, 6, 7, 8, 9},
            new int[]    {10, 11, 12, 13, 14, 15, 16, 17, 18, 19},
            new int[]    {20, 21, 22, 23, 24, 25, 26, 27, 28, 29},
            new int[]    {30, 31, 32, 33, 34, 35, 36, 37, 38, 39},
            new int[]    {40, 41, 42, 43, 44, 45, 46, 47, 48, 49}};

            for (int i = 0; i < this.dimensions[0]; i++)
                Assert.IsTrue(Arrays.AreEqual(expected[i], sm.GetSliceIndexes(new[] { i })));
        }

        [TestMethod]
        public void TestOr()
        {
            SparseBinaryMatrix sm = CreateDefaultMatrix();
            int[] orBits = { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };
            byte[] expected = { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            sm.Or(orBits);
            Assert.IsTrue(Arrays.AreEqual(expected, ((SparseByteArray)sm.GetSlice(0)).AsDense()), "Arrays are not equal");
        }

        [TestMethod]
        public void TestAll()
        {
            SparseBinaryMatrix sm = CreateDefaultMatrix();
            int[] all = { 0, 5, 11, 16, 22, 27, 33, 38, 44, 49 };
            Assert.IsTrue(sm.All(all));
        }

        private SparseBinaryMatrix CreateDefaultMatrix()
        {
            SparseBinaryMatrix sm = new SparseBinaryMatrix(this.dimensions);
            int[][] values =
            {
                new int[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new int[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new int[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new int[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new int[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    sm.Set(values[i][j], new int[] { i, j });
                }
            }

            return sm;
        }

        [TestMethod]
        public void TestCalculateOverlap()
        {
            DoTestCalculateOverlap(new SparseMatrix(this.dimensions[0], dimensions[1]));
            //doTestCalculateOverlap(new LowMemorySparseBinaryMatrix(this.dimensions));
            //doTestCalculateOverlap(new FastConnectionsMatrix(this.dimensions));
        }

        private void DoTestCalculateOverlap(SparseMatrix sm)
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            ///////////////////
            // test stimulsThreshold = 2
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 3.0);
            InitSp();

            int[][] connectedSynapses =
            {
                new[] {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}
            };

            for (int i = 0; i < sm.RowCount; i++)
            {
                for (int j = 0; j < sm.ColumnCount; j++)
                {
                    sm.At(i, j, connectedSynapses[i][j]);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], (int)sm.At(i, j));
                }
            }

            int[] inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            int[] overlaps = sp.CalculateOverlap(mem, inputVector);
            int[] trueOverlaps = new int[] { 10, 8, 6, 4, 0 }; // last gets squelched by stimulus threshold of 3
            double[] overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            double[] trueOverlapsPct = new double[] { 1, 1, 1, 1, 0 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));
        }

        private Parameters parameters;
        private SpatialPooler sp;
        private Connections mem;

        public void SetupParameters()
        {
            parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 5 });//5
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 5 });//5
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 3);//3
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);//0.5
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 10);
            parameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);
            parameters.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 0);
        }

        private void InitSp()
        {
            sp = new SpatialPooler();
            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);
        }
    }

    [TestClass]
    public class SparseMatrixTestLib
    {
        [TestMethod]
        public void TestUtilSparseMatrix_Vecsum()
        {
            SparseBinaryMatrix matrix1 = new SparseBinaryMatrix(new[] {1024, 1024});
            // fill half the matrix
            for (int i = 0; i < 1024; i++)
            {
                for (int j = 0; j < 512; j+=2)
                {
                    matrix1.Set(1, i, j);
                }
            }

            int[] inputVec = new int[1024]; // all zero
            int[] results = new int[1024];
            matrix1.RightVecSumAtNZ(inputVec,results,0);

            inputVec = new int[1024]; // some zero
            for (int j = 0; j < 512; j+=2)
            {
                inputVec[j] = 1;
            }
            results = new int[1024];
            matrix1.RightVecSumAtNZ(inputVec, results,0);
        }

        [TestMethod]
        public void TestMathNetSparseMatrix_Vecsum()
        {
            Matrix<double> matrix1 = new SparseMatrix(1024, 1024);
            // fill half the matrix
            for (int i = 0; i < 1024; i++)
            {
                for (int j = 0; j < 512; j += 2)
                {
                    matrix1.At(i, j, 1);
                }
            }

            SparseVector inputVec = new SparseVector(1024); // all zero
            Vector<double> results = new SparseVector(1024);
            matrix1.Multiply(inputVec, results);

            inputVec = new SparseVector(1024); // some zero
            for (int j = 0; j < 512; j += 2)
            {
                inputVec[j] = 1;
            }
            results = new SparseVector(1024);
            matrix1.Multiply(inputVec, results);
        }
    }
}