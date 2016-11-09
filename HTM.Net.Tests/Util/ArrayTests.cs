using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class ArrayTests
    {
        [TestMethod]
        public void TestGetDimensionData_1D()
        {
            Array array = new[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 };

            object data = array.GetDimensionData<int>(2);

            Assert.IsInstanceOfType(data, typeof(int));
            Assert.AreEqual(2, data);
        }

        [TestMethod]
        public void TestGetDimensionData_2D_Row()
        {
            Array array = new[,]
            {
                { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }
            };

            object data = array.GetDimensionData<int>(1);

            Assert.IsInstanceOfType(data, typeof(int[]));
            int[] row = (int[])data;
            Assert.AreEqual(10, row[0]);
            Assert.AreEqual(11, row[1]);
            Assert.AreEqual(19, row[9]);
        }

        [TestMethod]
        public void TestGetDimensionData_2D_Exact()
        {
            Array array = new[,]
            {
                { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9 },
                { 10, 11, 12, 13, 14, 15, 16, 17, 18, 19 }
            };

            object data = array.GetDimensionData<int>(1, 0);

            Assert.IsInstanceOfType(data, typeof(int));
            Assert.AreEqual(10, data);
        }

        [TestMethod]
        public void TestGetDimensionData_3D_Exact()
        {
            Array array = new[, ,]
            {
                {
                    {0,1,2,3,4}
                },
                {
                    {5,6,7,8,9}
                },
            };

            object data = array.GetDimensionData<int>(1, 0, 0);

            Assert.IsInstanceOfType(data, typeof(int));
            Assert.AreEqual(5, data);
        }

        [TestMethod]
        public void TestGetDimensionData_3D_Row()
        {
            Array array = new[, ,]
            {
                {
                    {0,1,2,3,4}
                },
                {
                    {5,6,7,8,9}
                },
            };

            object data = array.GetDimensionData<int>(1, 0);

            Assert.IsInstanceOfType(data, typeof(int[]));
            int[] row = (int[])data;
            Assert.AreEqual(5, row.Length);
            Assert.AreEqual(5, row[0]);
            Assert.AreEqual(9, row[4]);
        }

        [TestMethod]
        public void TestGetDimensionData_3D_Array()
        {
            Array array = new[, ,]
            {
                {
                    {0,1,2,3,4},
                    {5,6,7,8,9}
                },
                {
                    {5,6,7,8,9},
                    {15,16,17,18,19}
                },
            };

            object data = array.GetDimensionData<int>(1);

            Assert.IsInstanceOfType(data, typeof(int[,]));
            int[,] row = (int[,])data;
            Assert.AreEqual(2, row.Rank);
            Assert.AreEqual(5, row[0, 0]);
            Assert.AreEqual(9, row[0, 4]);
        }

        [TestMethod]
        public void TestHugeDimensionData2D_Row()
        {
            Array array = Array.CreateInstance(typeof(int), new[] { 2100, 2100 });

            object data = array.GetDimensionData<int>(1000);
            Assert.IsInstanceOfType(data, typeof(int[]));
            int[] row = (int[])data;
            Assert.AreEqual(0, row[0]);
            Assert.AreEqual(0, row[2099]);
        }

        [TestMethod]
        public void TestHugeDimensionData3D_Array()
        {
            Array array = Array.CreateInstance(typeof(int), new[] { 10, 2100, 2100 });

            int[] values = ArrayUtils.Range(0, 10 * 2100 * 2100);

            Buffer.BlockCopy(values, 0, array, 0, Marshal.SizeOf<int>() * array.Length);

            object data = array.GetDimensionData<int>(5);
            Assert.IsInstanceOfType(data, typeof(int[,]));
            int[,] row = (int[,])data;
            Assert.AreEqual(2, row.Rank);
            Assert.AreEqual(5 * 2100 * 2100, row[0, 0]);
            Assert.AreEqual((5 * 2100 * 2100) + 2099, row[0, 2099]);
        }

        [TestMethod]
        public void TestFillNormal()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            int[] myArray = new int[10000000];

            Arrays.Fill(myArray, 1);

            watch.Stop();

            Debug.WriteLine("Speed: " + watch.ElapsedMilliseconds);

            Assert.IsTrue(myArray.All(i => i == 1));
        }

        [TestMethod]
        public void TestFillNormalByte()
        {
            Stopwatch watch = new Stopwatch();
            watch.Start();

            byte[] myArray = new byte[10000000];

            Arrays.Fill(myArray, 1);

            watch.Stop();

            Debug.WriteLine("Speed: " + watch.ElapsedMilliseconds);

            Assert.IsTrue(myArray.All(i => i == 1));
        }

        [TestMethod]
        public void TestArrayToString_Normal()
        {
            int[] values = new[] {1, 2, 3};
            string s = Arrays.ToString(values);
            Assert.AreEqual("[1, 2, 3]", s);

            List<int> ints = new List<int> {1,2,3};
            s = Arrays.ToString(ints);
            Assert.AreEqual("[1, 2, 3]", s);

            List<object>  objs = new List<object> { 1, 2, 3 };
            s = Arrays.ToString(ints);
            Assert.AreEqual("[1, 2, 3]", s);

            List<object> strings = new List<object> { "myString", 2.1, 3 };
            s = Arrays.ToString(strings);
            Assert.AreEqual("[myString, 2.1, 3]", s);
        }

        [TestMethod]
        public void TestArrayToString_Nested()
        {
            List<int[]> ints = new List<int[]> { new [] {1,2}, new[] { 3, 4 }, new[] { 5, 6 } };
            string s = Arrays.ToString(ints);
            Assert.AreEqual("[[1, 2], [3, 4], [5, 6]]", s);
        }

        [TestMethod]
        public void TestArrayToString_Combined()
        {
            List<object> objs = new List<object> { new[] { 1, 2 }, new List<int>{ 3, 4 }, new List<object> { 5, 6.1 } };
            string s = Arrays.ToString(objs);
            Assert.AreEqual("[[1, 2], [3, 4], [5, 6.1]]", s);
        }
    }
}