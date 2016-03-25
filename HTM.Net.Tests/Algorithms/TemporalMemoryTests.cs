using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    /// <summary>
    /// Basic unit test for <see cref="TemporalMemory"/>
    /// </summary>
    [TestClass]
    public class TemporalMemoryTest
    {

        [TestMethod]
        public void TestActivateCorrectlyPredictiveCells()
        {

            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            ComputeCycle c = new ComputeCycle();

            int[] prevPredictiveCells = { 0, 237, 1026, 26337, 26339, 55536 };
            int[] activeColumns = { 32, 47, 823 };
            HashSet<Cell> prevMatchingCells = new HashSet<Cell>();

            tm.ActivateCorrectlyPredictiveCells(
                cn, c, cn.GetCellSet(prevPredictiveCells), prevMatchingCells, cn.GetColumnSet(activeColumns));
            HashSet<Cell> activeCells = cn.GetActiveCells();
            HashSet<Cell> winnerCells = cn.GetWinnerCells();
            HashSet<Column> predictedColumns = cn.GetSuccessfullyPredictedColumns();

            int[] expectedActiveWinners = { 1026, 26337, 26339 };
            int[] expectedPredictCols = { 32, 823 };
            int idx = 0;
            foreach(Cell cell in activeCells)
            {
                Assert.AreEqual(expectedActiveWinners[idx++], cell.GetIndex());
            }
            idx = 0;
            foreach(Cell cell in winnerCells)
            {
                Assert.AreEqual(expectedActiveWinners[idx++], cell.GetIndex());
            }
            idx = 0;
            foreach(Column col in predictedColumns)
            {
                Assert.AreEqual(expectedPredictCols[idx++], col.GetIndex());
            }

            Assert.IsFalse(c.PredictedInactiveCells().Any());
        }

        [TestMethod]
        public void TestActivateCorrectlyPredictiveCellsEmpty()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            // No previous predictive cells, no active columns

            ComputeCycle c = new ComputeCycle();

            int[] prevPredictiveCells = { };
            int[] activeColumns = { };
            HashSet<Cell> prevMatchingCells = new HashSet<Cell>();

            tm.ActivateCorrectlyPredictiveCells(
                cn, c, cn.GetCellSet(prevPredictiveCells), prevMatchingCells, cn.GetColumnSet(activeColumns));
            HashSet<Cell> activeCells = c.ActiveCells();
            HashSet<Cell> winnerCells = c.WinnerCells();
            HashSet<Column> predictedColumns = c.SuccessfullyPredictedColumns();

            Assert.IsFalse(activeCells.Any());
            Assert.IsFalse(winnerCells.Any());
            Assert.IsFalse(predictedColumns.Any());
            Assert.IsFalse(c.PredictedInactiveCells().Any());

            // No previous predictive cells, with active columns

            c = new ComputeCycle();

            prevPredictiveCells = new int[] { };
            activeColumns = new[] { 32, 47, 823 };
            prevMatchingCells = new HashSet<Cell>();

            tm.ActivateCorrectlyPredictiveCells(
                cn, c, cn.GetCellSet(prevPredictiveCells), prevMatchingCells, cn.GetColumnSet(activeColumns));
            activeCells = c.ActiveCells();
            winnerCells = c.WinnerCells();
            predictedColumns = c.SuccessfullyPredictedColumns();

            Assert.IsFalse(activeCells.Any());
            Assert.IsFalse(winnerCells.Any());
            Assert.IsFalse(predictedColumns.Any());
            Assert.IsFalse(c.PredictedInactiveCells().Any());

            // No active columns, with previously predictive cells

            c = new ComputeCycle();

            prevPredictiveCells = new[] { 0, 237, 1026, 26337, 26339, 55536 };
            activeColumns = new int[] { };
            tm.ActivateCorrectlyPredictiveCells(
                cn, c, cn.GetCellSet(prevPredictiveCells), prevMatchingCells, cn.GetColumnSet(activeColumns));
            activeCells = c.ActiveCells();
            winnerCells = c.WinnerCells();
            predictedColumns = c.SuccessfullyPredictedColumns();

            Assert.IsFalse(activeCells.Any());
            Assert.IsFalse(winnerCells.Any());
            Assert.IsFalse(predictedColumns.Any());
            Assert.IsFalse(c.PredictedInactiveCells().Any());
        }

        [TestMethod]
        public void TestActivateCorrectlyPredictiveCellsOrphan()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            cn.SetPredictedSegmentDecrement(0.001);

            ComputeCycle c = new ComputeCycle();

            int[] prevPredictiveCells = { };
            int[] activeColumns = { 32, 47, 823 };
            int[] prevMatchingCells = { 32, 47 };

            tm.ActivateCorrectlyPredictiveCells(
                cn, c, cn.GetCellSet(prevPredictiveCells),
                    cn.GetCellSet(prevMatchingCells),
                        cn.GetColumnSet(activeColumns));
            HashSet<Cell> activeCells = c.ActiveCells();
            HashSet<Cell> winnerCells = c.WinnerCells();
            HashSet<Column> predictedColumns = c.SuccessfullyPredictedColumns();
            HashSet<Cell> predictedInactiveCells = c.PredictedInactiveCells();

            Assert.IsFalse(activeCells.Any());
            Assert.IsFalse(winnerCells.Any());
            Assert.IsFalse(predictedColumns.Any());

            int[] expectedPredictedInactives = { 32, 47 };
            Assert.IsTrue(Arrays.AreEqual(
                expectedPredictedInactives, Connections.AsCellIndexes(predictedInactiveCells).ToArray()));

        }

        [TestMethod]
        public void TestBurstColumns()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetCellsPerColumn(4);
            cn.SetConnectedPermanence(0.50);
            cn.SetMinThreshold(1);
            cn.SetSeed(42);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            dd.CreateSynapse(cn, cn.GetCell(37), 0.4);
            dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            DistalDendrite dd2 = cn.GetCell(0).CreateSegment(cn);
            dd2.CreateSynapse(cn, cn.GetCell(49), 0.9);
            dd2.CreateSynapse(cn, cn.GetCell(3), 0.8);

            DistalDendrite dd3 = cn.GetCell(1).CreateSegment(cn);
            dd3.CreateSynapse(cn, cn.GetCell(733), 0.7);

            DistalDendrite dd4 = cn.GetCell(108).CreateSegment(cn);
            dd4.CreateSynapse(cn, cn.GetCell(486), 0.9);

            int[] activeColumns = { 0, 1, 26 };
            int[] predictedColumns = { 26 };
            int[] prevActiveCells = { 23, 37, 49, 733 };
            int[] prevWinnerCells = { 23, 37, 49, 733 };

            ComputeCycle cycle = new ComputeCycle();
            tm.BurstColumns(cycle, cn, cn.GetColumnSet(activeColumns),
                cn.GetColumnSet(predictedColumns), cn.GetCellSet(prevActiveCells), cn.GetCellSet(prevWinnerCells));

            List<Cell> activeCells = new List<Cell>(cycle.ActiveCells());
            List<Cell> winnerCells = new List<Cell>(cycle.WinnerCells());
            List<DistalDendrite> learningSegments = new List<DistalDendrite>(cycle.LearningSegments());

            Assert.AreEqual(8, activeCells.Count);
            for (int i = 0; i < 8; i++)
            {
                Assert.AreEqual(i, activeCells[i].GetIndex());
            }
            Assert.AreEqual(2, winnerCells.Count);
            Assert.AreEqual(0, winnerCells[0].GetIndex());
            Assert.AreEqual(5, winnerCells[1].GetIndex());

            Assert.AreEqual(2, learningSegments.Count);

            Assert.AreEqual(dd, learningSegments[0]);
            //Test that one of the learning Dendrites was created during call to burst...
            Assert.IsTrue(!dd.Equals(learningSegments[1]));
            Assert.IsTrue(!dd2.Equals(learningSegments[1]));
            Assert.IsTrue(!dd3.Equals(learningSegments[1]));
            Assert.IsTrue(!dd4.Equals(learningSegments[1]));

            // assertTrue(!cn.getSegments(cn.getCell(5)).isEmpty() && cn.getSegments(cn.getCell(5)).get(0).getIndex() == 4);
            
            // Check that new segment was added to winner cell (6) in column 1
            bool segmentCell6Exists = cn.GetSegments(cn.GetCell(5)).Any();
            Assert.IsTrue(segmentCell6Exists, "Segment for cell 6 does not exist!");

            var fifthCell = cn.GetCell(5);
            var fifthCellSegments = cn.GetSegments(fifthCell);
            int indexOfFirstSegmentCell5 = fifthCellSegments.First().GetIndex();
            Assert.AreEqual(4, indexOfFirstSegmentCell5);
        }

        [TestMethod]
        public void TestBurstColumnsEmpty()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetCellsPerColumn(4);
            tm.Init(cn);

            int[] activeColumns = { };
            int[] predictedColumns = { };
            int[] prevActiveCells = { };
            int[] prevWinnerCells = { };

            ComputeCycle cycle = new ComputeCycle();
            tm.BurstColumns(cycle, cn, cn.GetColumnSet(activeColumns),
                cn.GetColumnSet(predictedColumns), cn.GetCellSet(prevActiveCells), cn.GetCellSet(prevWinnerCells));

            Assert.IsFalse(cycle.ActiveCells().Any());
            Assert.IsFalse(cycle.WinnerCells().Any());
            Assert.IsFalse(cycle.LearningSegments().Any());
        }

        [TestMethod]
        public void TestLearnOnSegments()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetMaxNewSynapseCount(2);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(37), 0.4);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            DistalDendrite dd1 = cn.GetCell(1).CreateSegment(cn);
            Synapse s3 = dd1.CreateSynapse(cn, cn.GetCell(733), 0.7);

            DistalDendrite dd2 = cn.GetCell(8).CreateSegment(cn);
            Synapse s4 = dd2.CreateSynapse(cn, cn.GetCell(486), 0.9);

            DistalDendrite dd3 = cn.GetCell(100).CreateSegment(cn);

            HashSet<DistalDendrite> prevActiveSegments = new HashSet<DistalDendrite>();
            prevActiveSegments.Add(dd);
            prevActiveSegments.Add(dd2);

            HashSet<DistalDendrite> learningSegments = new HashSet<DistalDendrite>();
            learningSegments.Add(dd1);
            learningSegments.Add(dd3);
            HashSet<Cell> prevActiveCells = cn.GetCellSet(new[] { 23, 37, 733 });
            HashSet<Cell> winnerCells = new HashSet<Cell>();
            winnerCells.Add(cn.GetCell(0));
            HashSet<Cell> prevWinnerCells = new HashSet<Cell>();
            prevWinnerCells.Add(cn.GetCell(10));
            prevWinnerCells.Add(cn.GetCell(11));
            prevWinnerCells.Add(cn.GetCell(12));
            prevWinnerCells.Add(cn.GetCell(13));
            prevWinnerCells.Add(cn.GetCell(14));
            HashSet<Cell> predictedInactiveCells = new HashSet<Cell>();
            HashSet<DistalDendrite> prevMatchingSegments = new HashSet<DistalDendrite>();

            //Before

            //Check segment 0
            Assert.AreEqual(0.6, s0.GetPermanence(), 0.01);
            Assert.AreEqual(0.4, s1.GetPermanence(), 0.01);
            Assert.AreEqual(0.9, s2.GetPermanence(), 0.01);

            //Check segment 1
            Assert.AreEqual(0.7, s3.GetPermanence(), 0.01);
            Assert.AreEqual(1, dd1.GetAllSynapses(cn).Count, 0);

            //Check segment 2
            Assert.AreEqual(0.9, s4.GetPermanence(), 0.01);
            Assert.AreEqual(1, dd2.GetAllSynapses(cn).Count, 0);

            //Check segment 3
            Assert.AreEqual(0, dd3.GetAllSynapses(cn).Count, 0);

            // The tested method
            tm.LearnOnSegments(cn, prevActiveSegments, learningSegments, prevActiveCells, winnerCells, prevWinnerCells, predictedInactiveCells, prevMatchingSegments);

            //After

            //Check segment 0
            Assert.AreEqual(0.7, s0.GetPermanence(), 0.01); //was 0.6
            Assert.AreEqual(0.5, s1.GetPermanence(), 0.01); //was 0.4
            Assert.AreEqual(0.8, s2.GetPermanence(), 0.01); //was 0.9

            //Check segment 1
            Assert.AreEqual(0.8, s3.GetPermanence(), 0.01); //was 0.7
            Assert.AreEqual(2, dd1.GetAllSynapses(cn).Count, 0); // was 1

            //Check segment 2
            Assert.AreEqual(0.9, s4.GetPermanence(), 0.01); //unchanged
            Assert.AreEqual(1, dd2.GetAllSynapses(cn).Count, 0); //unchanged

            //Check segment 3
            Assert.AreEqual(2, dd3.GetAllSynapses(cn).Count, 0);// was 0

            //Check total synapse count
            Assert.AreEqual(8, cn.GetSynapseCount());

        }

        [TestMethod]
        public void TestComputePredictiveCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetActivationThreshold(2);
            cn.SetMinThreshold(2);
            cn.SetPredictedSegmentDecrement(0.004);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(37), 0.5);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            DistalDendrite dd1 = cn.GetCell(1).CreateSegment(cn);
            Synapse s3 = dd1.CreateSynapse(cn, cn.GetCell(733), 0.7);
            Synapse s4 = dd1.CreateSynapse(cn, cn.GetCell(733), 0.4);

            DistalDendrite dd2 = cn.GetCell(1).CreateSegment(cn);
            Synapse s5 = dd2.CreateSynapse(cn, cn.GetCell(974), 0.9);

            DistalDendrite dd3 = cn.GetCell(8).CreateSegment(cn);
            Synapse s6 = dd3.CreateSynapse(cn, cn.GetCell(486), 0.9);

            DistalDendrite dd4 = cn.GetCell(100).CreateSegment(cn);

            HashSet<Cell> activeCells = cn.GetCellSet(new[] { 733, 37, 974, 23 });

            ComputeCycle cycle = new ComputeCycle();
            tm.ComputePredictiveCells(cn, cycle, activeCells);

            Assert.IsTrue(cycle.ActiveSegments().Contains(dd) && cycle.ActiveSegments().Count == 1);
            Assert.IsTrue(cycle.PredictiveCells().Contains(cn.GetCell(0)) && cycle.PredictiveCells().Count == 1);
            Assert.IsTrue(cycle.MatchingSegments().Contains(dd) && cycle.MatchingSegments().Contains(dd1));
            Assert.IsTrue(cycle.MatchingCells().Contains(cn.GetCell(0)) && cycle.MatchingCells().Contains(cn.GetCell(1)));
        }

        [TestMethod]
        public void TestGetBestMatchingCell()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetConnectedPermanence(0.50);
            cn.SetMinThreshold(1);
            cn.SetSeed(42);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(37), 0.4);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            DistalDendrite dd1 = cn.GetCell(0).CreateSegment(cn);
            Synapse s3 = dd1.CreateSynapse(cn, cn.GetCell(49), 0.9);
            Synapse s4 = dd1.CreateSynapse(cn, cn.GetCell(3), 0.8);

            DistalDendrite dd2 = cn.GetCell(1).CreateSegment(cn);
            Synapse s5 = dd2.CreateSynapse(cn, cn.GetCell(733), 0.7);

            DistalDendrite dd3 = cn.GetCell(1).CreateSegment(cn);
            Synapse s6 = dd3.CreateSynapse(cn, cn.GetCell(486), 0.9);

            HashSet<Cell> activeCells = cn.GetCellSet(new[] { 733, 37, 974, 23 });

            TemporalMemory.CellSearch result = tm.GetBestMatchingCell(cn, cn.GetColumn(0).GetCells(), activeCells);
            Assert.AreEqual(dd, result.BestSegment);
            Assert.AreEqual(0, result.BestCell.GetIndex());

            result = tm.GetBestMatchingCell(cn, cn.GetColumn(3).GetCells(), activeCells);
            Assert.IsNull(result.BestSegment);
            Assert.AreEqual(107, result.BestCell.GetIndex());

            result = tm.GetBestMatchingCell(cn, cn.GetColumn(999).GetCells(), activeCells);
            Assert.IsNull(result.BestSegment);
            Assert.AreEqual(31993, result.BestCell.GetIndex());

        }

        [TestMethod]
        public void TestGetBestMatchingCellFewestSegments()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 2 });
            cn.SetCellsPerColumn(2);
            cn.SetConnectedPermanence(0.50);
            cn.SetMinThreshold(1);
            cn.SetSeed(42);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(3), 0.3);

            HashSet<Cell> activeCells = new HashSet<Cell>();

            //Never pick cell 0, always pick cell 1
            for (int i = 0; i < 100; i++)
            {
                TemporalMemory.CellSearch result = tm.GetBestMatchingCell(cn, cn.GetColumn(0).GetCells(), activeCells);
                Assert.AreEqual(1, result.BestCell.GetIndex());
            }
        }

        [TestMethod]
        public void TestGetBestMatchingSegment()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetConnectedPermanence(0.50);
            cn.SetMinThreshold(1);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(37), 0.4);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            DistalDendrite dd1 = cn.GetCell(0).CreateSegment(cn);
            Synapse s3 = dd1.CreateSynapse(cn, cn.GetCell(49), 0.9);
            Synapse s4 = dd1.CreateSynapse(cn, cn.GetCell(3), 0.8);

            DistalDendrite dd2 = cn.GetCell(1).CreateSegment(cn);
            Synapse s5 = dd2.CreateSynapse(cn, cn.GetCell(733), 0.7);

            DistalDendrite dd3 = cn.GetCell(8).CreateSegment(cn);
            Synapse s6 = dd3.CreateSynapse(cn, cn.GetCell(486), 0.9);

            HashSet<Cell> activeCells = cn.GetCellSet(new[] { 733, 37, 974, 23 });

            TemporalMemory.SegmentSearch result = tm.GetBestMatchingSegment(cn, cn.GetCell(0), activeCells);
            Assert.AreEqual(dd, result.BestSegment);
            Assert.AreEqual(2, result.NumActiveSynapses);

            result = tm.GetBestMatchingSegment(cn, cn.GetCell(1), activeCells);
            Assert.AreEqual(dd2, result.BestSegment);
            Assert.AreEqual(1, result.NumActiveSynapses);

            result = tm.GetBestMatchingSegment(cn, cn.GetCell(8), activeCells);
            Assert.AreEqual(null, result.BestSegment);
            Assert.AreEqual(0, result.NumActiveSynapses);

            result = tm.GetBestMatchingSegment(cn, cn.GetCell(100), activeCells);
            Assert.AreEqual(null, result.BestSegment);
            Assert.AreEqual(0, result.NumActiveSynapses);

        }

        [TestMethod]
        public void TestGetLeastUsedCell()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            cn.SetColumnDimensions(new[] { 2 });
            cn.SetCellsPerColumn(2);
            cn.SetSeed(42);
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(3), 0.3);

            Column column0 = cn.GetColumn(0);
            IRandom random = cn.GetRandom();
            // Never pick cell 0, always pick cell 1
            for (int i = 0; i < 100; i++)
            {
                Cell leastUsed = column0.GetLeastUsedCell(cn, cn.GetRandom());
                Assert.AreEqual(1, leastUsed.GetIndex());
            }
        }

        [TestMethod]
        public void TestAdaptSegment()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(37), 0.4);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            HashSet<Synapse> activeSynapses = new HashSet<Synapse>();
            activeSynapses.Add(s0);
            activeSynapses.Add(s1);
            dd.AdaptSegment(cn, activeSynapses, cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());

            Assert.AreEqual(0.7, s0.GetPermanence(), 0.01);
            Assert.AreEqual(0.5, s1.GetPermanence(), 0.01);
            Assert.AreEqual(0.8, s2.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void TestSegmentSynapseDeletion()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.6);
            Synapse s1 = dd.CreateSynapse(cn, cn.GetCell(23), 0.4);
            Synapse s2 = dd.CreateSynapse(cn, cn.GetCell(477), 0.9);

            Assert.IsTrue(cn.GetSynapses(dd).Contains(s0));
            s0.Destroy(cn);
            Assert.IsFalse(cn.GetSynapses(dd).Contains(s0));
            Assert.AreEqual(2, cn.GetSynapseCount());
            Assert.AreEqual(2, cn.GetSynapses(dd).Count);

            s1.Destroy(cn);
            Assert.IsFalse(cn.GetSynapses(dd).Contains(s1));
            Assert.AreEqual(1, cn.GetSynapseCount());
            Assert.AreEqual(1, cn.GetSynapses(dd).Count);

            s2.Destroy(cn);
            Assert.IsFalse(cn.GetSynapses(dd).Contains(s2));
            Assert.AreEqual(0, cn.GetSynapseCount());
            Assert.AreEqual(0, cn.GetSynapses(dd).Count);
        }

        [TestMethod]
        public void TestAdaptSegmentToMax()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.9);

            HashSet<Synapse> activeSynapses = new HashSet<Synapse>();
            activeSynapses.Add(s0);

            dd.AdaptSegment(cn, activeSynapses, cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.AreEqual(1.0, s0.GetPermanence(), 0.01);

            dd.AdaptSegment(cn, activeSynapses, cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.AreEqual(1.0, s0.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void TestAdaptSegmentToMin()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            Synapse s0 = dd.CreateSynapse(cn, cn.GetCell(23), 0.1);

            HashSet<Synapse> activeSynapses = new HashSet<Synapse>();


            // Changed due to new algorithm implementation
            dd.AdaptSegment(cn, activeSynapses, cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.IsFalse(cn.GetSynapses(dd).Contains(s0));
        }

        [TestMethod]
        public void TestPickCellsToLearnOn()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);

            HashSet<Cell> winnerCells = new HashSet<Cell>();
            winnerCells.Add(cn.GetCell(4));
            winnerCells.Add(cn.GetCell(47));
            winnerCells.Add(cn.GetCell(58));
            winnerCells.Add(cn.GetCell(93));

            List<Cell> learnCells = new List<Cell>(dd.PickCellsToLearnOn(cn, 2, winnerCells, cn.GetRandom()));
            Assert.AreEqual(2, learnCells.Count);
            Assert.IsTrue(learnCells.Contains(cn.GetCell(47)));
            Assert.IsTrue(learnCells.Contains(cn.GetCell(93)));

            learnCells = new List<Cell>(dd.PickCellsToLearnOn(cn, 100, winnerCells, cn.GetRandom()));
            Assert.AreEqual(4, learnCells.Count);

            Assert.IsTrue(learnCells.Any(c=>c.GetIndex() == 93), "Incorrect learnindex chosen, expected " + 93 + " to be there");
            Assert.IsTrue(learnCells.Any(c=>c.GetIndex() == 58), "Incorrect learnindex chosen, expected " + 58 + " to be there");
            Assert.IsTrue(learnCells.Any(c=>c.GetIndex() == 47), "Incorrect learnindex chosen, expected " + 47 + " to be there");
            Assert.IsTrue(learnCells.Any(c=>c.GetIndex() == 4), "Incorrect learnindex chosen, expected " + 4 + " to be there");

            learnCells = new List<Cell>(dd.PickCellsToLearnOn(cn, 0, winnerCells, cn.GetRandom()));
            Assert.AreEqual(0, learnCells.Count);
        }

        [TestMethod]
        public void TestPickCellsToLearnOnAvoidDuplicates()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            tm.Init(cn);

            DistalDendrite dd = cn.GetCell(0).CreateSegment(cn);
            dd.CreateSynapse(cn, cn.GetCell(23), 0.6);

            HashSet<Cell> winnerCells = new HashSet<Cell>();
            winnerCells.Add(cn.GetCell(23));

            // Ensure that no additional (duplicate) cells were picked
            List<Cell> learnCells = new List<Cell>(dd.PickCellsToLearnOn(cn, 2, winnerCells, cn.GetRandom()));
            Assert.IsFalse(learnCells.Any());
        }

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
    }
}