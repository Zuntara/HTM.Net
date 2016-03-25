using System;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class SparseByteArrayTests
    {
        [TestMethod]
        public void SparseByteArray_CreateTwoDimensionalArray()
        {
            SparseByteArray array = SparseByteArray.CreateInstance(10,20);

            Assert.AreEqual(2, array.Rank);
            Assert.AreEqual(10, array.GetLength(0));
            Assert.AreEqual(20, array.GetLength(1));
            Assert.AreEqual(10*20, array.Length);

            // Set value
            array[0, 0] = 1;
            array[5, 5] = 1;
            array[9, 9] = 1;

            // Get value
            Assert.AreEqual(1, array[0,0]);
            Assert.AreEqual(0, array[0,1]);
            Assert.AreEqual(0, array[1,0]);
            Assert.AreEqual(1, array[5,5]);
            Assert.AreEqual(1, array[9,9]);

            Assert.AreEqual(3, array.Sum);

            array[5, 5] = 0;

            Assert.AreEqual(2, array.Sum);
        }

        [TestMethod]
        public void SparseByteArray_2D_SetRowOfValues()
        {
            SparseByteArray array = SparseByteArray.CreateInstance(10, 20);

            // Set value
            array[1] = 1;

            // Get value
            Assert.AreEqual(0, array[0, 0]);
            Assert.AreEqual(0, array[0, 1]);
            Assert.AreEqual(1, array[1, 0]);
            Assert.AreEqual(1, array[1, 5]);
            Assert.AreEqual(1, array[1, 9]);
            Assert.AreEqual(0, array[2, 0]);
            Assert.AreEqual(0, array[2, 1]);

            Assert.AreEqual(20, array.GetRow(1).Sum);

        }

        [TestMethod, ExpectedException(typeof(ArgumentOutOfRangeException))]
        public void SparseByteArray_OutOfRangeIndexAddress()
        {
            SparseByteArray array = SparseByteArray.CreateInstance(10, 10);

            Assert.AreEqual(2, array.Rank);

            // Set value
            array[0, 0, 0] = 1;
            Assert.Fail("We should get an exception");
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void SparseByteArray_GetRowThrowsInvalidOperationException()
        {
            SparseByteArray o;
            SparseByteArray sparseByteArray;
            int[] ints = new int[1];
            o = SparseByteArray.CreateInstance(ints);
            sparseByteArray = o.GetRow(0);
        }

        [TestMethod]
        public void SparseByteArray_GetRow()
        {
            SparseByteArray o;
            SparseByteArray sparseByteArray;
            int[] ints = new int[2];
            o = SparseByteArray.CreateInstance(ints);
            sparseByteArray = o.GetRow(0);
            Assert.IsNull((object)sparseByteArray);
            Assert.IsNotNull(o);
            Assert.AreEqual<int>(2, ((SparseByteArray)o).Rank);
            Assert.AreEqual<int>(0, ((SparseByteArray)o).Length);
        }

    }
}