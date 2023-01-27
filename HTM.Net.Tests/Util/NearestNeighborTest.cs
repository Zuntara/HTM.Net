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

            double[,] connectedSynapses = new double[,]{
                {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}};
            var nn = new NearestNeighbor(connectedSynapses);
            Assert.AreEqual(5, nn.RowCount);
        }

        [TestMethod]
        public void TestRightVecSumAtNZ()
        {
            double[,] connectedSynapses = new double[,]{
                {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}};

            double[] inputVector = new double[] { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0 };
            double[] trueResults = new double[] { 1, 1, 1, 1, 1 };

            NearestNeighbor nn = new NearestNeighbor(connectedSynapses);

            double[] result = nn.RightVecSumAtNz(inputVector);

            for (int i = 0; i < result.Length; i++)
            {
                Assert.AreEqual(trueResults[i], result[i]);
            }
        }
    }
}