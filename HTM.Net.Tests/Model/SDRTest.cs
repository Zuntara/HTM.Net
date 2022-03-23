using System;
using System.Collections.Generic;
using System.Linq;

using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Model
{
    [TestClass]
    public class SDRTest
    {
        [TestMethod]
        public void TestAsCellIndices()
        {
            Connections cn = new Connections();
            cn.SetColumnDimensions(new int[] { 64, 64 });
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
            int[] inputIndexes = expectedIndexes.Select(i => i * cellsPerColumn).ToArray();
            int[] result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn);
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));

            // Test failure 
            expectedIndexes = new int[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = expectedIndexes.Select(i => i * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn); // "true" is Erroneous state
            Assert.IsFalse(Arrays.AreEqual(expectedIndexes, result));

            // Test correct state fixes above
            int[] arrInputIndexes = new int[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            expectedIndexes = new int[] { 0, 3, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = arrInputIndexes.Select(i => i * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(inputIndexes, cellsPerColumn); // "false" is correct state
            Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestAsColumnIndicesList()
        {
            int cellsPerColumn = 4;

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndexes = expectedIndexes.Select(i=> i * cellsPerColumn).ToArray();
            int[] result = SDR.AsColumnIndices(
                inputIndexes, cellsPerColumn);
             Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));

            // Test failure 
            expectedIndexes = new int[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = expectedIndexes.Select(i=> i  * cellsPerColumn).ToArray();
            result = SDR.AsColumnIndices(
                inputIndexes, cellsPerColumn); // "true" is Erroneous state
            Assert.IsFalse(Arrays.AreEqual(expectedIndexes, result));

            // Test correct state fixes above
            int[] arrInputIndexes = new int[] { 0, 3, 4, 4, 4095 }; // Has duplicate ("4")
            expectedIndexes = new int[] { 0, 3, 4, 4095 }; // Has duplicate ("4")
            inputIndexes = arrInputIndexes.Select(i => i * cellsPerColumn).ToArray();
            Console.WriteLine("result = " + Arrays.ToString(result));
            result = SDR.AsColumnIndices(
                inputIndexes, cellsPerColumn); // "false" is correct state
             Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestCellsAsColumnIndicesList()
        {
            Connections cn = new Connections();
            cn.SetColumnDimensions(new int[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            TemporalMemory.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndices = expectedIndexes.Select(i=> i  * cn.GetCellsPerColumn()).ToArray();
            List<Cell> cells = new List<Cell>(cn.GetCellSet(inputIndices));

            int[] result = SDR.CellsToColumns(cells, cn.GetCellsPerColumn());

             Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }

        [TestMethod]
        public void TestCellsAsColumnIndicesSet()
        {
            Connections cn = new Connections();
            cn.SetColumnDimensions(new int[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            TemporalMemory.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            int[] inputIndices = expectedIndexes.Select(i=> i  * cn.GetCellsPerColumn()).ToArray();
            HashSet<Cell> cells = cn.GetCellSet(inputIndices);

            int[] result = SDR.CellsAsColumnIndices(cells, cn.GetCellsPerColumn());

             Assert.IsTrue(Arrays.AreEqual(expectedIndexes, result));
        }
    }

    [TestClass]
    public class DistalDendriteTest
    {

        [TestMethod]
        public void TestGetActiveSynapses()
        {
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p = GetDefaultParameters(p, Parameters.KEY.MIN_THRESHOLD, 1);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            HashSet<Cell> prevWinnerCells = cn.GetCellSet(new int[] { 0, 1, 2, 3 });

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(4));
            cn.CreateSynapse(matchingSegment, cn.GetCell(0), 0.5);

            HashSet<Synapse> syns = matchingSegment.GetActiveSynapses(cn, prevWinnerCells);
            Assert.IsTrue(syns.Count == 1);
            Assert.IsTrue(syns.First().GetPresynapticCell().Equals(cn.GetCell(0)));
        }

        private Parameters GetDefaultParameters(Parameters p, Parameters.KEY key, Object value)
        {
            Parameters retVal = p == null ? GetDefaultParameters() : p;
            retVal.SetParameterByKey(key, value);

            return retVal;
        }

        private Parameters GetDefaultParameters()
        {
            Parameters retVal = Parameters.GetTemporalDefaultParameters();
            retVal.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 32 });
            retVal.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 4);
            retVal.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 3);
            retVal.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.21);
            retVal.SetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE, 0.5);
            retVal.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 2);
            retVal.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 3);
            retVal.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.10);
            retVal.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.10);
            retVal.SetParameterByKey(Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.0);
            retVal.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            retVal.SetParameterByKey(Parameters.KEY.SEED, 42);

            return retVal;
        }
    }
}