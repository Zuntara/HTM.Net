using System;
using HTM.Net.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class NearestNeighborTest
    {
        [TestMethod]
        public void TestInstantiation()
        {
            new NearestNeighbor(0, 40);

            try
            {
                new NearestNeighbor(-1,-1);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ArgumentOutOfRangeException));
                Assert.IsTrue(e.Message.StartsWith("The number of columns of a matrix must be positive."));
            }
        }
    }
}