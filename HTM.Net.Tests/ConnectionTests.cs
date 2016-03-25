using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests
{
    [TestClass]
    public class ConnectionsTest
    {

        [TestMethod]
        public void TestColumnForCell1D()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 2048 });
            cn.SetCellsPerColumn(5);
            tm.Init(cn);

            Assert.AreEqual(0, cn.GetCell(0).GetColumn().GetIndex());
            Assert.AreEqual(0, cn.GetCell(4).GetColumn().GetIndex());
            Assert.AreEqual(1, cn.GetCell(5).GetColumn().GetIndex());
            Assert.AreEqual(2047, cn.GetCell(10239).GetColumn().GetIndex());
        }

        [TestMethod]
        public void TestColumnForCell2D()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            Assert.AreEqual(0, cn.GetCell(0).GetColumn().GetIndex());
            Assert.AreEqual(0, cn.GetCell(3).GetColumn().GetIndex());
            Assert.AreEqual(1, cn.GetCell(4).GetColumn().GetIndex());
            Assert.AreEqual(4095, cn.GetCell(16383).GetColumn().GetIndex());
        }

        [TestMethod]
        public void TestAsCellIndexes()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 16383 };
            HashSet<Cell> cells = cn.GetCellSet(expectedIndexes);

            List<int> cellIdxList = Connections.AsCellIndexes(cells);

            // Unordered test of equality
            HashSet<int> cellIdxSet = new HashSet<int>(cellIdxList);
            HashSet<int> expectedIdxSet = new HashSet<int>(expectedIndexes);
            Assert.IsTrue(Arrays.AreEqual(cellIdxSet, expectedIdxSet));
        }

        [TestMethod]
        public void TestAsColumnIndexes()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            int[] expectedIndexes = { 0, 3, 4, 4095 };
            HashSet<Column> columns = cn.GetColumnSet(expectedIndexes);

            List<int> columnIdxList = Connections.AsColumnIndexes(columns);

            // Unordered test of equality
            HashSet<int> columnIdxSet = new HashSet<int>(columnIdxList);
            HashSet<int> expectedIdxSet = new HashSet<int>(expectedIndexes);
            Assert.IsTrue(Arrays.AreEqual(columnIdxSet,expectedIdxSet));
        }

        [TestMethod]
        public void TestAsCellObjects()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            int[] indexes = { 0, 3, 4, 16383 };
            HashSet<int> idxSet = new HashSet<int>(indexes);

            List<Cell> cells = cn.AsCellObjects(idxSet);
            foreach (Cell cell in cells)
                Assert.IsTrue(idxSet.Contains(cell.GetIndex()));
        }

        [TestMethod]
        public void TestAsColumnObjects()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 64, 64 });
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            int[] indexes = { 0, 3, 4, 4095 };
            HashSet<int> idxSet = new HashSet<int>(indexes);

            List<Column> columns = cn.AsColumnObjects(idxSet);
            foreach (Column column in columns)
                Assert.IsTrue(idxSet.Contains(column.GetIndex()));
        }

        [TestMethod]
        public void TestClear()
        {
            int[] input1 = { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
            int[] input2 = { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
            int[] input3 = { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] input4 = { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input5 = { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input6 = { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
            int[] input7 = { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };

            Parameters p = GetParameters();
            Connections con = new Connections();
            p.Apply(con);
            TemporalMemory tm = new TemporalMemory();
            tm.Init(con);

            for (int x = 0; x < 602; x++)
            {
                foreach (int[] i in inputs)
                {
                    tm.Compute(con, i.Where(n => n == 1).ToArray(), true);
                }
            }

            Assert.IsFalse(!con.GetActiveCells().Any());
            con.Clear();
            Assert.IsTrue(!con.GetActiveCells().Any());
        }

        public static Parameters GetParameters()
        {
            Parameters parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 8 });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 20 });
            parameters.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 6);

            //SpatialPooler specific
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 12);//3
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);//0.5
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 5.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLE, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLE, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 10);
            parameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);
            parameters.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 0);

            //Temporal Memory specific
            parameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            parameters.SetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE, 0.8);
            parameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 5);
            parameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 6);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 4);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            return parameters;
        }
    }
}