using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
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

        private Parameters GetDefaultParameters(Parameters p, Parameters.KEY key, Object value)
        {
            Parameters retVal = p == null ? GetDefaultParameters() : p;
            retVal.SetParameterByKey(key, value);

            return retVal;
        }

        private T DeepCopyPlain<T>(T t)
        {
            MemoryStream ms = new MemoryStream();
            BinaryFormatter formatter = new BinaryFormatter();
            formatter.Serialize(ms, t);
            ms.Position = 0;
            return (T)formatter.Deserialize(ms);
            //FSTConfiguration fastSerialConfig = FSTConfiguration.CreateDefaultConfiguration();
            //byte[] bytes = fastSerialConfig.asByteArray(t);
            //return (T)fastSerialConfig.asObject(bytes);
        }

        [TestMethod]
        public void TestActivateCorrectlyPredictiveCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell cell4 = cn.GetCell(4);
            HashSet<Cell> expectedActiveCells = new HashSet<Cell> { cell4 }; //Stream.of(cell4).collect(Collectors.toSet());

            DistalDendrite activeSegment = cn.CreateSegment(cell4);
            cn.CreateSynapse(activeSegment, cn.GetCell(0), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(1), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(2), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(3), 0.5);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.IsTrue(cc.PredictiveCells().SetEquals(expectedActiveCells));
            ComputeCycle cc2 = tm.Compute(cn, activeColumns, true);
            Assert.IsTrue(cc2.ActiveCells().SetEquals(expectedActiveCells));
        }

        [TestMethod]
        public void testBurstUnpredictedColumns()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] activeColumns = { 0 };
            HashSet<Cell> burstingCells = cn.GetCellSet(new int[] { 0, 1, 2, 3 });

            ComputeCycle cc = tm.Compute(cn, activeColumns, true);

            Assert.IsTrue(cc.ActiveCells().SetEquals(burstingCells));
        }

        [TestMethod]
        public void testZeroActiveColumns()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            Cell cell4 = cn.GetCell(4);

            DistalDendrite activeSegment = cn.CreateSegment(cell4);
            cn.CreateSynapse(activeSegment, cn.GetCell(0), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(1), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(2), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(3), 0.5);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.IsFalse(cc.ActiveCells().Count == 0);
            Assert.IsFalse(cc.WinnerCells().Count == 0);
            Assert.IsFalse(cc.PredictiveCells().Count == 0);

            int[] zeroColumns = new int[0];
            ComputeCycle cc2 = tm.Compute(cn, zeroColumns, true);
            Assert.IsTrue(cc2.ActiveCells().Count == 0);
            Assert.IsTrue(cc2.WinnerCells().Count == 0);
            Assert.IsTrue(cc2.PredictiveCells().Count == 0);
        }

        [TestMethod]
        public void testPredictedActiveCellsAreAlwaysWinners()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            List<Cell> expectedWinnerCells = new List<Cell>(cn.GetCellSet(new int[] { 4, 6 }));

            DistalDendrite activeSegment1 = cn.CreateSegment(expectedWinnerCells[0]);
            cn.CreateSynapse(activeSegment1, previousActiveCells[0], 0.5);
            cn.CreateSynapse(activeSegment1, previousActiveCells[1], 0.5);
            cn.CreateSynapse(activeSegment1, previousActiveCells[2], 0.5);

            DistalDendrite activeSegment2 = cn.CreateSegment(expectedWinnerCells[1]);
            cn.CreateSynapse(activeSegment2, previousActiveCells[0], 0.5);
            cn.CreateSynapse(activeSegment2, previousActiveCells[1], 0.5);
            cn.CreateSynapse(activeSegment2, previousActiveCells[2], 0.5);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, false); // learn=false
            cc = tm.Compute(cn, activeColumns, false); // learn=false

            Assert.IsTrue(cc.winnerCells.SetEquals(new HashSet<Cell>(expectedWinnerCells)));
        }

        [TestMethod]
        public void testReinforcedCorrectlyActiveSegments()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            p = GetDefaultParameters(p, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.PERMANENCE_DECREMENT, 0.08);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            Cell activeCell = cn.GetCell(5);

            DistalDendrite activeSegment = cn.CreateSegment(activeCell);
            Synapse as1 = cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.5);
            Synapse as2 = cn.CreateSynapse(activeSegment, previousActiveCells[1], 0.5);
            Synapse as3 = cn.CreateSynapse(activeSegment, previousActiveCells[2], 0.5);
            Synapse is1 = cn.CreateSynapse(activeSegment, cn.GetCell(81), 0.5);

            tm.Compute(cn, previousActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0.6, as1.GetPermanence(), 0.1);
            Assert.AreEqual(0.6, as2.GetPermanence(), 0.1);
            Assert.AreEqual(0.6, as3.GetPermanence(), 0.1);
            Assert.AreEqual(0.42, is1.GetPermanence(), 0.001);
        }

        [TestMethod]
        public void testReinforcedSelectedMatchingSegmentInBurstingColumn()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.PERMANENCE_DECREMENT, 0.08);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            Cell[] burstingCells = { cn.GetCell(4), cn.GetCell(5) };

            DistalDendrite activeSegment = cn.CreateSegment(burstingCells[0]);
            Synapse as1 = cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.3);
            Synapse as2 = cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.3);
            Synapse as3 = cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.3);
            Synapse is1 = cn.CreateSynapse(activeSegment, cn.GetCell(81), 0.3);

            DistalDendrite otherMatchingSegment = cn.CreateSegment(burstingCells[1]);
            cn.CreateSynapse(otherMatchingSegment, previousActiveCells[0], 0.3);
            cn.CreateSynapse(otherMatchingSegment, previousActiveCells[1], 0.3);
            cn.CreateSynapse(otherMatchingSegment, cn.GetCell(81), 0.3);

            tm.Compute(cn, previousActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0.4, as1.GetPermanence(), 0.01);
            Assert.AreEqual(0.4, as2.GetPermanence(), 0.01);
            Assert.AreEqual(0.4, as3.GetPermanence(), 0.01);
            Assert.AreEqual(0.22, is1.GetPermanence(), 0.001);
        }

        [TestMethod]
        public void testNoChangeToNonSelectedMatchingSegmentsInBurstingColumn()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.PERMANENCE_DECREMENT, 0.08);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            Cell[] burstingCells = { cn.GetCell(4), cn.GetCell(5) };

            DistalDendrite selectedMatchingSegment = cn.CreateSegment(burstingCells[0]);
            cn.CreateSynapse(selectedMatchingSegment, previousActiveCells[0], 0.3);
            cn.CreateSynapse(selectedMatchingSegment, previousActiveCells[1], 0.3);
            cn.CreateSynapse(selectedMatchingSegment, previousActiveCells[2], 0.3);
            cn.CreateSynapse(selectedMatchingSegment, cn.GetCell(81), 0.3);

            DistalDendrite otherMatchingSegment = cn.CreateSegment(burstingCells[1]);
            Synapse as1 = cn.CreateSynapse(otherMatchingSegment, previousActiveCells[0], 0.3);
            Synapse as2 = cn.CreateSynapse(otherMatchingSegment, previousActiveCells[1], 0.3);
            Synapse is1 = cn.CreateSynapse(otherMatchingSegment, cn.GetCell(81), 0.3);

            tm.Compute(cn, previousActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0.3, as1.GetPermanence(), 0.01);
            Assert.AreEqual(0.3, as2.GetPermanence(), 0.01);
            Assert.AreEqual(0.3, is1.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void testNoChangeToMatchingSegmentsInPredictedActiveColumn()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            int[] activeColumns = { 1 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            Cell expectedActiveCell = cn.GetCell(4);
            HashSet<Cell> expectedActiveCells = new HashSet<Cell> { expectedActiveCell };  // Stream.of(expectedActiveCell).collect(Collectors.toCollection(HashSet < Cell >::new));
            Cell otherBurstingCell = cn.GetCell(5);

            DistalDendrite activeSegment = cn.CreateSegment(expectedActiveCell);
            cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[1], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[2], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[3], 0.5);

            DistalDendrite matchingSegmentOnSameCell = cn.CreateSegment(expectedActiveCell);
            Synapse s1 = cn.CreateSynapse(matchingSegmentOnSameCell, previousActiveCells[0], 0.3);
            Synapse s2 = cn.CreateSynapse(matchingSegmentOnSameCell, previousActiveCells[1], 0.3);

            DistalDendrite matchingSegmentOnOtherCell = cn.CreateSegment(otherBurstingCell);
            Synapse s3 = cn.CreateSynapse(matchingSegmentOnOtherCell, previousActiveCells[0], 0.3);
            Synapse s4 = cn.CreateSynapse(matchingSegmentOnOtherCell, previousActiveCells[1], 0.3);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.IsTrue(cc.PredictiveCells().SetEquals(expectedActiveCells));
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0.3, s1.GetPermanence(), 0.01);
            Assert.AreEqual(0.3, s2.GetPermanence(), 0.01);
            Assert.AreEqual(0.3, s3.GetPermanence(), 0.01);
            Assert.AreEqual(0.3, s4.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void testNoNewSegmentIfNotEnoughWinnerCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 3);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] zeroColumns = { };
            int[] activeColumns = { 0 };

            tm.Compute(cn, zeroColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0, cn.GetNumSegments(), 0);
        }

        [TestMethod]
        public void testNewSegmentAddSynapsesToSubsetOfWinnerCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 2);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0, 1, 2 };
            int[] activeColumns = { 4 };

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);

            HashSet<Cell> prevWinnerCells = cc.WinnerCells();
            Assert.AreEqual(3, prevWinnerCells.Count);

            cc = tm.Compute(cn, activeColumns, true);

            List<Cell> winnerCells = new List<Cell>(cc.WinnerCells());
            Assert.AreEqual(1, winnerCells.Count);
            List<DistalDendrite> segments = winnerCells[0].GetSegments(cn);
            Assert.AreEqual(1, segments.Count);
            List<Synapse> synapses = cn.GetSynapses(segments[0]);
            Assert.AreEqual(2, synapses.Count);

            foreach (Synapse synapse in synapses)
            {
                Assert.AreEqual(0.21, synapse.GetPermanence(), 0.01);
                Assert.IsTrue(prevWinnerCells.Contains(synapse.GetPresynapticCell()));
            }
        }

        [TestMethod]
        public void testNewSegmentAddSynapsesToAllWinnerCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0, 1, 2 };
            int[] activeColumns = { 4 };

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            List<Cell> prevWinnerCells = new List<Cell>(cc.WinnerCells());
            Assert.AreEqual(3, prevWinnerCells.Count);

            cc = tm.Compute(cn, activeColumns, true);

            List<Cell> winnerCells = new List<Cell>(cc.WinnerCells());
            Assert.AreEqual(1, winnerCells.Count);
            List<DistalDendrite> segments = winnerCells[0].GetSegments(cn);
            Assert.AreEqual(1, segments.Count);
            List<Synapse> synapses = segments[0].GetAllSynapses(cn);

            List<Cell> presynapticCells = new List<Cell>();
            foreach (Synapse synapse in synapses)
            {
                Assert.AreEqual(0.21, synapse.GetPermanence(), 0.01);
                presynapticCells.Add(synapse.GetPresynapticCell());
            }

            presynapticCells.Sort();
            Assert.IsTrue(prevWinnerCells.SequenceEqual(presynapticCells));
        }

        [TestMethod]
        public void testMatchingSegmentAddSynapsesToSubsetOfWinnerCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p = GetDefaultParameters(p, Parameters.KEY.MIN_THRESHOLD, 1);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0, 1, 2, 3 };
            HashSet<Cell> prevWinnerCells = cn.GetCellSet(new int[] { 0, 1, 2, 3 });
            int[] activeColumns = { 4 };

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(4));
            cn.CreateSynapse(matchingSegment, cn.GetCell(0), 0.5);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.IsTrue(cc.WinnerCells().SetEquals(prevWinnerCells));
            cc = tm.Compute(cn, activeColumns, true);

            List<Synapse> synapses = cn.GetSynapses(matchingSegment);
            Assert.AreEqual(3, synapses.Count);

            synapses.Sort();
            foreach (Synapse synapse in synapses)
            {
                if (synapse.GetPresynapticCell().GetIndex() == 0) continue;

                Assert.AreEqual(0.21, synapse.GetPermanence(), 0.01);
                Assert.IsTrue(synapse.GetPresynapticCell().GetIndex() == 1 ||
                           synapse.GetPresynapticCell().GetIndex() == 2 ||
                           synapse.GetPresynapticCell().GetIndex() == 3);
            }
        }

        [TestMethod]
        public void testMatchingSegmentAddSynapsesToAllWinnerCells()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p = GetDefaultParameters(p, Parameters.KEY.MIN_THRESHOLD, 1);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0, 1 };
            HashSet<Cell> prevWinnerCells = cn.GetCellSet(new int[] { 0, 1 });
            int[] activeColumns = { 4 };

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(4));
            cn.CreateSynapse(matchingSegment, cn.GetCell(0), 0.5);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.IsTrue(cc.WinnerCells().SetEquals(prevWinnerCells));

            cc = tm.Compute(cn, activeColumns, true);

            List<Synapse> synapses = cn.GetSynapses(matchingSegment);
            Assert.AreEqual(2, synapses.Count);

            synapses.Sort();
            foreach (Synapse synapse in synapses)
            {
                if (synapse.GetPresynapticCell().GetIndex() == 0) continue;

                Assert.AreEqual(0.21, synapse.GetPermanence(), 0.01);
                Assert.AreEqual(1, synapse.GetPresynapticCell().GetIndex());
            }
        }

        /**
         * When a segment becomes active, grow synapses to previous winner cells.
         *
         * The number of grown synapses is calculated from the "matching segment"
         * overlap, not the "active segment" overlap.
         */
        [TestMethod]
        public void testActiveSegmentGrowSynapsesAccordingToPotentialOverlap()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p = GetDefaultParameters(p, Parameters.KEY.MIN_THRESHOLD, 1);
            p = GetDefaultParameters(p, Parameters.KEY.ACTIVATION_THRESHOLD, 2);
            p = GetDefaultParameters(p, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            // Use 1 cell per column so that we have easy control over the winner cells.
            int[] previousActiveColumns = { 0, 1, 2, 3, 4 };
            HashSet<Cell> prevWinnerCells = new HashSet<Cell>(Arrays.AsList(0, 1, 2, 3, 4)
                .Select(i => cn.GetCell(i))
                .ToList());
            int[] activeColumns = { 5 };

            DistalDendrite activeSegment = cn.CreateSegment(cn.GetCell(5));
            cn.CreateSynapse(activeSegment, cn.GetCell(0), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(1), 0.5);
            cn.CreateSynapse(activeSegment, cn.GetCell(2), 0.2);

            ComputeCycle cc = tm.Compute(cn, previousActiveColumns, true);
            Assert.AreEqual(prevWinnerCells, cc.WinnerCells());
            cc = tm.Compute(cn, activeColumns, true);

            HashSet<Cell> presynapticCells = new HashSet<Cell>(cn.GetSynapses(activeSegment)
                .Select(s => s.GetPresynapticCell())
                .ToList());

            Assert.IsTrue(
                presynapticCells.Count == 4 && (
                    !presynapticCells.Except(new List<Cell> { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) }).Any() ||
                    !presynapticCells.Except(new List<Cell> { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(4) }).Any()));
        }

        [TestMethod]
        public void testDestroyWeakSynapseOnWrongPrediction()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            p = GetDefaultParameters(p, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            int[] activeColumns = { 2 };
            Cell expectedActiveCell = cn.GetCell(5);

            DistalDendrite activeSegment = cn.CreateSegment(expectedActiveCell);
            cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[1], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[2], 0.5);
            // Weak Synapse
            cn.CreateSynapse(activeSegment, previousActiveCells[3], 0.015);

            tm.Compute(cn, previousActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(3, cn.GetNumSynapses(activeSegment));
        }

        [TestMethod]
        public void testDestroyWeakSynapseOnActiveReinforce()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            p = GetDefaultParameters(p, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] previousActiveColumns = { 0 };
            Cell[] previousActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            int[] activeColumns = { 2 };
            Cell expectedActiveCell = cn.GetCell(5);

            DistalDendrite activeSegment = cn.CreateSegment(expectedActiveCell);
            cn.CreateSynapse(activeSegment, previousActiveCells[0], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[1], 0.5);
            cn.CreateSynapse(activeSegment, previousActiveCells[2], 0.5);
            // Weak Synapse
            cn.CreateSynapse(activeSegment, previousActiveCells[3], 0.009);

            tm.Compute(cn, previousActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(3, cn.GetNumSynapses(activeSegment));
        }

        [TestMethod]
        public void testRecycleWeakestSynapseToMakeRoomForNewSynapse()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 100 });
            p = GetDefaultParameters(p, Parameters.KEY.MIN_THRESHOLD, 1);
            p = GetDefaultParameters(p, Parameters.KEY.PERMANENCE_INCREMENT, 0.02);
            p = GetDefaultParameters(p, Parameters.KEY.PERMANENCE_DECREMENT, 0.02);
            p.SetParameterByKey(Parameters.KEY.MAX_SYNAPSES_PER_SEGMENT, 3);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            Assert.AreEqual(3, cn.GetMaxSynapsesPerSegment());

            int[] prevActiveColumns = { 0, 1, 2 };
            HashSet<Cell> prevWinnerCells = cn.GetCellSet(new int[] { 0, 1, 2 });
            int[] activeColumns = { 4 };

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(4));
            cn.CreateSynapse(matchingSegment, cn.GetCell(81), 0.6);
            // Weakest Synapse
            cn.CreateSynapse(matchingSegment, cn.GetCell(0), 0.11);

            ComputeCycle cc = tm.Compute(cn, prevActiveColumns, true);
            Assert.AreEqual(prevWinnerCells, cc.winnerCells);
            tm.Compute(cn, activeColumns, true);

            List<Synapse> synapses = cn.GetSynapses(matchingSegment);
            Assert.AreEqual(3, synapses.Count);
            HashSet<Cell> presynapticCells = new HashSet<Cell>(synapses.Select(s => s.GetPresynapticCell()));
            Assert.IsFalse(presynapticCells.Select(cell => cell.GetIndex()).Any(i => i == 0));
        }

        [TestMethod]
        public void testRecycleLeastRecentlyActiveSegmentToMakeRoomForNewSegment()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.CELLS_PER_COLUMN, 1);
            p = GetDefaultParameters(p, Parameters.KEY.INITIAL_PERMANENCE, 0.5);
            p = GetDefaultParameters(p, Parameters.KEY.PERMANENCE_INCREMENT, 0.02);
            p = GetDefaultParameters(p, Parameters.KEY.PERMANENCE_DECREMENT, 0.02);
            p.SetParameterByKey(Parameters.KEY.MAX_SEGMENTS_PER_CELL, 2);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] prevActiveColumns1 = { 0, 1, 2 };
            int[] prevActiveColumns2 = { 3, 4, 5 };
            int[] prevActiveColumns3 = { 6, 7, 8 };
            int[] activeColumns = { 9 };
            Cell cell9 = cn.GetCell(9);

            tm.Compute(cn, prevActiveColumns1, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(1, cn.GetSegments(cell9).Count);
            DistalDendrite oldestSegment = cn.GetSegments(cell9)[0];
            tm.Reset(cn);
            tm.Compute(cn, prevActiveColumns2, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(2, cn.GetSegments(cell9).Count);

            HashSet<Cell> oldPresynaptic = new HashSet<Cell>(cn.GetSynapses(oldestSegment)
                .Select(s => s.GetPresynapticCell()));

            tm.Reset(cn);
            tm.Compute(cn, prevActiveColumns3, true);
            tm.Compute(cn, activeColumns, true);
            Assert.AreEqual(2, cn.GetSegments(cell9).Count);

            // Verify none of the segments are connected to the cells the old
            // segment was connected to.

            foreach (DistalDendrite segment in cn.GetSegments(cell9))
            {
                HashSet<Cell> newPresynaptic = new HashSet<Cell>(cn.GetSynapses(segment)
                    .Select(s => s.GetPresynapticCell()));
                //.collect(Collectors.toSet());

                Assert.IsFalse(oldPresynaptic.Overlaps(newPresynaptic));
                //Assert.IsTrue(Collections.disjoint(oldPresynaptic, newPresynaptic));
            }
        }

        [TestMethod]
        public void testDestroySegmentsWithTooFewSynapsesToBeMatching()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.INITIAL_PERMANENCE, .2);
            p = GetDefaultParameters(p, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] prevActiveColumns = { 0 };
            Cell[] prevActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            int[] activeColumns = { 2 };
            Cell expectedActiveCell = cn.GetCell(5);

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(5));
            cn.CreateSynapse(matchingSegment, prevActiveCells[0], .015);
            cn.CreateSynapse(matchingSegment, prevActiveCells[1], .015);
            cn.CreateSynapse(matchingSegment, prevActiveCells[2], .015);
            cn.CreateSynapse(matchingSegment, prevActiveCells[3], .015);

            tm.Compute(cn, prevActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0, cn.GetNumSegments(expectedActiveCell));
        }

        [TestMethod]
        public void testPunishMatchingSegmentsInInactiveColumns()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] prevActiveColumns = { 0 };
            Cell[] prevActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            int[] activeColumns = { 1 };
            Cell previousInactiveCell = cn.GetCell(81);

            DistalDendrite activeSegment = cn.CreateSegment(cn.GetCell(42));
            Synapse as1 = cn.CreateSynapse(activeSegment, prevActiveCells[0], .5);
            Synapse as2 = cn.CreateSynapse(activeSegment, prevActiveCells[1], .5);
            Synapse as3 = cn.CreateSynapse(activeSegment, prevActiveCells[2], .5);
            Synapse is1 = cn.CreateSynapse(activeSegment, previousInactiveCell, .5);

            DistalDendrite matchingSegment = cn.CreateSegment(cn.GetCell(43));
            Synapse as4 = cn.CreateSynapse(matchingSegment, prevActiveCells[0], .5);
            Synapse as5 = cn.CreateSynapse(matchingSegment, prevActiveCells[1], .5);
            Synapse is2 = cn.CreateSynapse(matchingSegment, previousInactiveCell, .5);

            tm.Compute(cn, prevActiveColumns, true);
            tm.Compute(cn, activeColumns, true);

            Assert.AreEqual(0.48, as1.GetPermanence(), 0.01);
            Assert.AreEqual(0.48, as2.GetPermanence(), 0.01);
            Assert.AreEqual(0.48, as3.GetPermanence(), 0.01);
            Assert.AreEqual(0.48, as4.GetPermanence(), 0.01);
            Assert.AreEqual(0.48, as5.GetPermanence(), 0.01);
            Assert.AreEqual(0.50, is1.GetPermanence(), 0.01);
            Assert.AreEqual(0.50, is2.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void testAddSegmentToCellWithFewestSegments()
        {
            bool grewOnCell1 = false;
            bool grewOnCell2 = false;

            for (int seed = 0; seed < 100; seed++)
            {
                TemporalMemory tm = new TemporalMemory();
                Connections cn = new Connections();
                Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
                p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
                p = GetDefaultParameters(p, Parameters.KEY.SEED, seed);
                p.Apply(cn);
                TemporalMemory.Init(cn);

                int[] prevActiveColumns = { 1, 2, 3, 4 };
                Cell[] prevActiveCells = { cn.GetCell(4), cn.GetCell(5), cn.GetCell(6), cn.GetCell(7) };
                int[] activeColumns = { 0 };
                Cell[] nonMatchingCells = { cn.GetCell(0), cn.GetCell(3) };
                HashSet<Cell> activeCells = cn.GetCellSet(new int[] { 0, 1, 2, 3 });

                DistalDendrite segment1 = cn.CreateSegment(nonMatchingCells[0]);
                cn.CreateSynapse(segment1, prevActiveCells[0], 0.5);
                DistalDendrite segment2 = cn.CreateSegment(nonMatchingCells[1]);
                cn.CreateSynapse(segment2, prevActiveCells[1], 0.5);

                tm.Compute(cn, prevActiveColumns, true);
                ComputeCycle cc = tm.Compute(cn, activeColumns, true);

                Assert.IsTrue(cc.ActiveCells().SetEquals(activeCells));

                Assert.AreEqual(3, cn.GetNumSegments());
                Assert.AreEqual(1, cn.GetNumSegments(cn.GetCell(0)));
                Assert.AreEqual(1, cn.GetNumSegments(cn.GetCell(3)));
                Assert.AreEqual(1, cn.GetNumSynapses(segment1));
                Assert.AreEqual(1, cn.GetNumSynapses(segment2));

                List<DistalDendrite> segments = new List<DistalDendrite>(cn.GetSegments(cn.GetCell(1)));
                if (segments.Count == 0)
                {
                    List<DistalDendrite> segments2 = cn.GetSegments(cn.GetCell(2));
                    Assert.IsFalse(segments2.Count == 0);
                    grewOnCell2 = true;
                    segments.AddRange(segments2);
                }
                else
                {
                    grewOnCell1 = true;
                }

                Assert.AreEqual(1, segments.Count);
                List<Synapse> synapses = segments[0].GetAllSynapses(cn);
                Assert.AreEqual(4, synapses.Count);

                HashSet<Column> columnCheckList = cn.GetColumnSet(prevActiveColumns);

                foreach (Synapse synapse in synapses)
                {
                    Assert.AreEqual(0.2, synapse.GetPermanence(), 0.01);

                    Column column = synapse.GetPresynapticCell().GetColumn();
                    Assert.IsTrue(columnCheckList.Contains(column));
                    columnCheckList.Remove(column);
                }

                Assert.AreEqual(0, columnCheckList.Count);
            }

            Assert.IsTrue(grewOnCell1);
            Assert.IsTrue(grewOnCell2);
        }

        [TestMethod]
        public void testConnectionsNeverChangeWhenLearningDisabled()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 4);
            p = GetDefaultParameters(p, Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.02);
            p = GetDefaultParameters(p, Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            int[] prevActiveColumns = { 0 };
            Cell[] prevActiveCells = { cn.GetCell(0), cn.GetCell(1), cn.GetCell(2), cn.GetCell(3) };
            int[] activeColumns = { 1, 2 };
            Cell prevInactiveCell = cn.GetCell(81);
            Cell expectedActiveCell = cn.GetCell(4);

            DistalDendrite correctActiveSegment = cn.CreateSegment(expectedActiveCell);
            cn.CreateSynapse(correctActiveSegment, prevActiveCells[0], 0.5);
            cn.CreateSynapse(correctActiveSegment, prevActiveCells[1], 0.5);
            cn.CreateSynapse(correctActiveSegment, prevActiveCells[2], 0.5);

            DistalDendrite wrongMatchingSegment = cn.CreateSegment(cn.GetCell(43));
            cn.CreateSynapse(wrongMatchingSegment, prevActiveCells[0], 0.5);
            cn.CreateSynapse(wrongMatchingSegment, prevActiveCells[1], 0.5);
            cn.CreateSynapse(wrongMatchingSegment, prevInactiveCell, 0.5);

            Map<Cell, HashSet<Synapse>> synMapBefore = DeepCopyPlain(cn.GetReceptorSynapseMapping());
            Map<Cell, List<DistalDendrite>> segMapBefore = DeepCopyPlain(cn.GetSegmentMapping());

            tm.Compute(cn, prevActiveColumns, false);
            tm.Compute(cn, activeColumns, false);

            Assert.IsTrue(synMapBefore != cn.GetReceptorSynapseMapping());
            Assert.AreEqual(synMapBefore, cn.GetReceptorSynapseMapping());
            Assert.IsTrue(segMapBefore != cn.GetSegmentMapping());
            Assert.AreEqual(segMapBefore, cn.GetSegmentMapping());
        }

        [TestMethod]
        public void testLeastUsedCell()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = GetDefaultParameters(null, Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2 });
            p = GetDefaultParameters(p, Parameters.KEY.CELLS_PER_COLUMN, 2);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            DistalDendrite dd = cn.CreateSegment(cn.GetCell(0));
            cn.CreateSynapse(dd, cn.GetCell(3), 0.3);

            for (int i = 0; i < 100; i++)
            {
                Assert.AreEqual(1, tm.LeastUsedCell(cn, cn.GetColumn(0).GetCells(), cn.GetRandom()).GetIndex());
            }
        }

        [TestMethod]
        public void testAdaptSegment()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = Parameters.GetAllDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            DistalDendrite dd = cn.CreateSegment(cn.GetCell(0));
            Synapse s1 = cn.CreateSynapse(dd, cn.GetCell(23), 0.6);
            Synapse s2 = cn.CreateSynapse(dd, cn.GetCell(37), 0.4);
            Synapse s3 = cn.CreateSynapse(dd, cn.GetCell(477), 0.9);

            tm.AdaptSegment(cn, dd, cn.GetCellSet(23, 37), cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());

            Assert.AreEqual(0.7, s1.GetPermanence(), 0.01);
            Assert.AreEqual(0.5, s2.GetPermanence(), 0.01);
            Assert.AreEqual(0.8, s3.GetPermanence(), 0.01);
        }

        [TestMethod]
        public void testAdaptSegmentToMax()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = Parameters.GetAllDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            DistalDendrite dd = cn.CreateSegment(cn.GetCell(0));
            Synapse s1 = cn.CreateSynapse(dd, cn.GetCell(23), 0.9);

            tm.AdaptSegment(cn, dd, cn.GetCellSet(23), cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.AreEqual(1.0, s1.GetPermanence(), 0.1);

            // Now permanence should be at max
            tm.AdaptSegment(cn, dd, cn.GetCellSet(23), cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.AreEqual(1.0, s1.GetPermanence(), 0.1);
        }

        [TestMethod]
        public void testAdaptSegmentToMin()
        {
            TemporalMemory tm = new TemporalMemory();
            Connections cn = new Connections();
            Parameters p = Parameters.GetAllDefaultParameters();
            p.Apply(cn);
            TemporalMemory.Init(cn);

            DistalDendrite dd = cn.CreateSegment(cn.GetCell(0));
            Synapse s1 = cn.CreateSynapse(dd, cn.GetCell(23), 0.1);
            cn.CreateSynapse(dd, cn.GetCell(1), 0.3);

            tm.AdaptSegment(cn, dd, cn.GetCellSet(), cn.GetPermanenceIncrement(), cn.GetPermanenceDecrement());
            Assert.IsFalse(cn.GetSynapses(dd).Contains(s1));
        }

        [TestMethod]
        public void testNumberOfColumns()
        {
            Connections cn = new Connections();
            Parameters p = Parameters.GetAllDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 64, 64 });
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            Assert.AreEqual(64 * 64, cn.GetNumColumns());
        }

        [TestMethod]
        public void TestNumberOfCells()
        {
            Connections cn = new Connections();
            Parameters p = Parameters.GetAllDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 64, 64 });
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
            p.Apply(cn);
            TemporalMemory.Init(cn);

            Assert.AreEqual(64 * 64 * 32, cn.GetCells().Length);
        }
    }
}