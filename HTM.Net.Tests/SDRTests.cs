using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests
{
    [TestClass]
    public class SdrTest
    {

        [TestMethod]
        public void TestAsCellIndices()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            TemporalMemory.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 16383 };
            HashSet<Cell> cells = cn.GetCellSet(expectedIndexes);

            int[] cellIndices = SDR.AsCellIndices(cells);

            Assert.IsTrue(Arrays.AreEqual(cellIndices, expectedIndexes));
        }

        [TestMethod]
        public void TestAsColumnIndices()
        {
            int cellsPerColumn = 4;

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndexes = expectedIndexes.Select(i=>i * cellsPerColumn).ToArray();
            int[] result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn);
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));

            // Test failure 
            expectedIndexes = new[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = expectedIndexes.Select(i=>i * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn); // "true" is Erroneous state
            Assert.IsFalse(Arrays.AreEqual(expectedIndexes, result));

            // Test correct state fixes above
            int[] arrInputIndexes = { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            expectedIndexes = new[] { 0, 3, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = arrInputIndexes.Select(i=>i * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn); // "false" is correct state
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestAsColumnIndicesList()
        {
            int cellsPerColumn = 4;

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndexes = expectedIndexes.Select(i=>i * cellsPerColumn).ToArray();
            int[] result = SDR.AsColumnIndices(inputIndexes.ToList(), cellsPerColumn);
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));

            // Test failure 
            expectedIndexes = new[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = expectedIndexes.Select(i=>i * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(inputIndexes.ToList(), cellsPerColumn); // "true" is Erroneous state
            Assert.IsFalse(Arrays.AreEqual(expectedIndexes, result));

            // Test correct state fixes above
            int[] arrInputIndexes = { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            expectedIndexes = new[] { 0, 3, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = arrInputIndexes.Select(i=>i * cellsPerColumn).ToArray();
            Console.WriteLine("result = " + Arrays.ToString(result));
            result = SDR.AsColumnIndices(inputIndexes.ToList(), cellsPerColumn); // "false" is correct state
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestCellsAsColumnIndicesList()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            TemporalMemory.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndices = expectedIndexes.Select(i=>i * cn.GetCellsPerColumn()).ToArray();
            List<Cell> cells = new List<Cell>(cn.GetCellSet(inputIndices));

            int[] result = SDR.CellsToColumns(cells, cn.GetCellsPerColumn());

            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestCellsAsColumnIndicesSet()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            TemporalMemory.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndices = expectedIndexes.Select(i=>i * cn.GetCellsPerColumn()).ToArray();
            HashSet<Cell> cells = cn.GetCellSet(inputIndices);

            int[] result = SDR.CellsAsColumnIndices(cells, cn.GetCellsPerColumn());

            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

    }
}
