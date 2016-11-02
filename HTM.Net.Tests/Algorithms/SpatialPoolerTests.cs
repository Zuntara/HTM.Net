using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class SpatialPoolerTest
    {
        private Parameters parameters;
        private SpatialPooler sp;
        private Connections mem;

        public void SetupParameters()
        {
            parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 5 });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 5 });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 5);
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.01);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 10);
            parameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            parameters.SetRandom(new XorshiftRandom(42));
        }

        public void SetupDefaultParameters()
        {
            parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 32, 32 });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 64, 64 });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 16);
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 10.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.008);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.10);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.001);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.001);
            parameters.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 1000);
            parameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);
            parameters.SetRandom(new XorshiftRandom(42));
        }

        private void InitSp()
        {
            sp = new SpatialPooler();
            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);
        }

        [TestMethod]
        public void ConfirmSPConstruction()
        {
            SetupParameters();

            InitSp();

            Assert.AreEqual(5, mem.GetInputDimensions()[0]);
            Assert.AreEqual(5, mem.GetColumnDimensions()[0]);
            Assert.AreEqual(5, mem.GetPotentialRadius());
            Assert.AreEqual(0.5, mem.GetPotentialPct(), 0);
            Assert.AreEqual(false, mem.GetGlobalInhibition());
            Assert.AreEqual(-1.0, mem.GetLocalAreaDensity(), 0);
            Assert.AreEqual(3, mem.GetNumActiveColumnsPerInhArea(), 0);
            Assert.AreEqual(1, mem.GetStimulusThreshold(), 1);
            Assert.AreEqual(0.01, mem.GetSynPermInactiveDec(), 0);
            Assert.AreEqual(0.1, mem.GetSynPermActiveInc(), 0);
            Assert.AreEqual(0.1, mem.GetSynPermConnected(), 0);
            Assert.AreEqual(0.1, mem.GetMinPctOverlapDutyCycles(), 0);
            Assert.AreEqual(0.1, mem.GetMinPctActiveDutyCycles(), 0);
            Assert.AreEqual(10, mem.GetDutyCyclePeriod(), 0);
            Assert.AreEqual(10.0, mem.GetMaxBoost(), 0);
            Assert.AreEqual(42, mem.GetSeed());

            Assert.AreEqual(5, mem.GetNumInputs());
            Assert.AreEqual(5, mem.GetNumColumns());
        }

        /**
         * Checks that feeding in the same input vector leads to polarized
         * permanence values: either zeros or ones, but no fractions
         */
        [TestMethod]
        public void TestCompute1()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 9 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetPotentialRadius(5);

            //This is 0.3 in Python version due to use of dense 
            // permanence instead of sparse (as it should be)
            parameters.SetPotentialPct(0.5);

            parameters.SetGlobalInhibition(false);
            parameters.SetLocalAreaDensity(-1.0);
            parameters.SetNumActiveColumnsPerInhArea(3);
            parameters.SetStimulusThreshold(1);
            parameters.SetSynPermInactiveDec(0.01);
            parameters.SetSynPermActiveInc(0.1);
            parameters.SetMinPctOverlapDutyCycles(0.1);
            parameters.SetMinPctActiveDutyCycles(0.1);
            parameters.SetDutyCyclePeriod(10);
            parameters.SetMaxBoost(10);
            parameters.SetSynPermTrimThreshold(0);

            //This is 0.5 in Python version due to use of dense 
            // permanence instead of sparse (as it should be)
            parameters.SetPotentialPct(1);

            parameters.SetSynPermConnected(0.1);

            InitSp();

            Mock<SpatialPooler> mock = new Mock<SpatialPooler>();
            mock.CallBase = true;
            mock.Setup(sp => sp.InhibitColumns(It.IsAny<Connections>(), It.IsAny<double[]>()))
                .Returns(new int[] { 0, 1, 2, 3, 4 });

            int[] inputVector = new int[] { 1, 0, 1, 0, 1, 0, 0, 1, 1 };
            int[] activeArray = new int[] { 0, 0, 0, 0, 0 };
            for (int i = 0; i < 20; i++)
            {
                mock.Object.Compute(mem, inputVector, activeArray, true);
            }

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] permanences = ArrayUtils.ToIntArray(mem.GetPotentialPools().Get(i).GetDensePermanences(mem));
                Assert.IsTrue(Arrays.AreEqual(inputVector, permanences));
            }
        }

        /**
         * Checks that columns only change the permanence values for 
         * inputs that are within their potential pool
         */
        [TestMethod]
        public void TestCompute2()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetPotentialRadius(3);
            parameters.SetPotentialPct(0.3);
            parameters.SetGlobalInhibition(false);
            parameters.SetLocalAreaDensity(-1.0);
            parameters.SetNumActiveColumnsPerInhArea(3);
            parameters.SetStimulusThreshold(1);
            parameters.SetSynPermInactiveDec(0.01);
            parameters.SetSynPermActiveInc(0.1);
            parameters.SetMinPctOverlapDutyCycles(0.1);
            parameters.SetMinPctActiveDutyCycles(0.1);
            parameters.SetDutyCyclePeriod(10);
            parameters.SetMaxBoost(10);
            parameters.SetSynPermConnected(0.1);

            InitSp();

            Mock<SpatialPooler> mock = new Mock<SpatialPooler>();
            mock.CallBase = true;
            mock.Setup(sp => sp.InhibitColumns(It.IsAny<Connections>(), It.IsAny<double[]>()))
                .Returns(new int[] { 0, 1, 2, 3, 4 });

            int[] inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            int[] activeArray = new int[] { 0, 0, 0, 0, 0 };
            for (int i = 0; i < 20; i++)
            {
                mock.Object.Compute(mem, inputVector, activeArray, true);
            }

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] permanences = ArrayUtils.ToIntArray(mem.GetPotentialPools().Get(i).GetDensePermanences(mem));
                int[] potential = (int[])mem.GetConnectedCounts().Row(i).Select(d => (int)d).ToArray();
                Assert.IsTrue(Arrays.AreEqual(permanences, potential));
            }
        }

        /**
         * When stimulusThreshold is 0, allow columns without any overlap to become
         * active. This test focuses on the global inhibition code path.
         */
        [TestMethod]
        public void TestZeroOverlap_NoStimulusThreshold_GlobalInhibition()
        {
            int inputSize = 10;
            int nColumns = 20;
            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { inputSize });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { nColumns });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 10);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);

            SpatialPooler sp = new SpatialPooler();
            Connections cn = new Connections();
            parameters.Apply(cn);
            sp.Init(cn);

            int[] activeArray = new int[nColumns];
            sp.Compute(cn, new int[inputSize], activeArray, true);

            Assert.AreEqual(3, ArrayUtils.Where(activeArray, ArrayUtils.INT_GREATER_THAN_0).Length);
        }

        /**
         * When stimulusThreshold is > 0, don't allow columns without any overlap to
         * become active. This test focuses on the global inhibition code path.
         */
        [TestMethod]
        public void TestZeroOverlap_StimulusThreshold_GlobalInhibition()
        {
            int inputSize = 10;
            int nColumns = 20;
            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { inputSize });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { nColumns });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 10);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);

            SpatialPooler sp = new SpatialPooler();
            Connections cn = new Connections();
            parameters.Apply(cn);
            sp.Init(cn);

            int[] activeArray = new int[nColumns];
            sp.Compute(cn, new int[inputSize], activeArray, true);

            Assert.AreEqual(0, ArrayUtils.Where(activeArray, ArrayUtils.INT_GREATER_THAN_0).Length);
        }

        [TestMethod]
        public void TestZeroOverlap_NoStimulusThreshold_LocalInhibition()
        {
            int inputSize = 10;
            int nColumns = 20;
            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { inputSize });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { nColumns });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 5);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);

            SpatialPooler sp = new SpatialPooler();
            Connections cn = new Connections();
            parameters.Apply(cn);
            sp.Init(cn);

            // This exact number of active columns is determined by the inhibition
            // radius, which changes based on the random synapses (i.e. weird math).
            // Force it to a known number.
            cn.SetInhibitionRadius(2);

            int[] activeArray = new int[nColumns];
            sp.Compute(cn, new int[inputSize], activeArray, true);

            Assert.AreEqual(6, ArrayUtils.Where(activeArray, ArrayUtils.INT_GREATER_THAN_0).Length);
        }

        /**
         * When stimulusThreshold is > 0, don't allow columns without any overlap to
         * become active. This test focuses on the local inhibition code path.
         */
        [TestMethod]
        public void TestZeroOverlap_StimulusThreshold_LocalInhibition()
        {
            int inputSize = 10;
            int nColumns = 20;
            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { inputSize });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { nColumns });
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 10);
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            parameters.SetParameterByKey(Parameters.KEY.SEED, 42);

            SpatialPooler sp = new SpatialPooler();
            Connections cn = new Connections();
            parameters.Apply(cn);
            sp.Init(cn);

            int[] activeArray = new int[nColumns];
            sp.Compute(cn, new int[inputSize], activeArray, true);

            Assert.AreEqual(0, ArrayUtils.Where(activeArray, ArrayUtils.INT_GREATER_THAN_0).Length);
        }

        [TestMethod]
        public void TestOverlapsOutput()
        {
            parameters = Parameters.GetSpatialDefaultParameters();
            parameters.SetColumnDimensions(new int[] { 3 });
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetPotentialRadius(5);
            parameters.SetNumActiveColumnsPerInhArea(5);
            parameters.SetGlobalInhibition(true);
            parameters.SetSynPermActiveInc(0.1);
            parameters.SetSynPermInactiveDec(0.1);
            parameters.SetSeed(42);
            parameters.SetRandom(new XorshiftRandom(42));

            SpatialPooler sp = new SpatialPooler();
            Connections cn = new Connections();
            parameters.Apply(cn);
            sp.Init(cn);

            cn.SetBoostFactors(new double[] { 2.0, 2.0, 2.0 });
            int[] inputVector = { 1, 1, 1, 1, 1 };
            int[] activeArray = { 0, 0, 0 };
            int[] expOutput = { 2, 1, 0 };
            sp.Compute(cn, inputVector, activeArray, true);

            double[] boostedOverlaps = cn.GetBoostedOverlaps();
            int[] overlaps = cn.GetOverlaps();

            for (int i = 0; i < cn.GetNumColumns(); i++)
            {
                Assert.AreEqual(expOutput[i], overlaps[i]);
                Assert.AreEqual(expOutput[i] * 2, boostedOverlaps[i], 0.01);
            }
        }

        /**
         * Given a specific input and initialization params the SP should return this
         * exact output.
         *
         * Previously output varied between platforms (OSX/Linux etc) == (in Python)
         */
        [TestMethod]
        public void TestExactOutput()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 1, 188 });
            parameters.SetColumnDimensions(new int[] { 2048, 1 });
            parameters.SetPotentialRadius(94);
            parameters.SetPotentialPct(0.5);
            parameters.SetGlobalInhibition(true);
            parameters.SetLocalAreaDensity(-1.0);
            parameters.SetNumActiveColumnsPerInhArea(40);
            parameters.SetStimulusThreshold(0);
            parameters.SetSynPermInactiveDec(0.01);
            parameters.SetSynPermActiveInc(0.1);
            parameters.SetMinPctOverlapDutyCycles(0.001);
            parameters.SetMinPctActiveDutyCycles(0.001);
            parameters.SetDutyCyclePeriod(1000);
            parameters.SetMaxBoost(10);
            InitSp();

            int[] inputVector =
            {
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1,
                1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,
                0, 0, 0, 0, 0, 0, 0, 0
            };

            int[] activeArray = new int[2048];

            sp.Compute(mem, inputVector, activeArray, true);

            int[] real = ArrayUtils.Where(activeArray, n => n > 0);

            int[] expected = new int[] {
             74, 203, 237, 270, 288, 317, 479, 529, 530, 622, 659, 720, 757, 790, 924, 956, 1033,
             1041, 1112, 1332, 1386, 1430, 1500, 1517, 1578, 1584, 1651, 1664, 1717, 1735, 1747,
             1748, 1775, 1779, 1788, 1813, 1888, 1911, 1938, 1958 };

            Assert.IsTrue(Arrays.AreEqual(expected, real));
        }

        [TestMethod]
        public void TestStripNeverLearned()
        {
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 6 });
            parameters.SetInputDimensions(new int[] { 9 });
            InitSp();

            mem.UpdateActiveDutyCycles(new double[] { 0.5, 0.1, 0, 0.2, 0.4, 0 });
            int[] activeColumns = new int[] { 0, 1, 2, 4 };
            int[] stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            int[] trueStripped = new int[] { 0, 1, 4 };
            Assert.IsTrue(Arrays.AreEqual(trueStripped, stripped));

            mem.UpdateActiveDutyCycles(new double[] { 0.9, 0, 0, 0, 0.4, 0.3 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = new int[] { 0, 4, 5 };
            Assert.IsTrue(Arrays.AreEqual(trueStripped, stripped));

            mem.UpdateActiveDutyCycles(new double[] { 0, 0, 0, 0, 0, 0 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = new int[] { };
            Assert.IsTrue(Arrays.AreEqual(trueStripped, stripped));

            mem.UpdateActiveDutyCycles(new double[] { 1, 1, 1, 1, 1, 1 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = ArrayUtils.Range(0, 6);
            Assert.IsTrue(Arrays.AreEqual(trueStripped, stripped));
        }

        [TestMethod]
        public void TestMapColumn()
        {
            // Test 1D
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 4 });
            parameters.SetInputDimensions(new int[] { 12 });
            InitSp();

            Assert.AreEqual(1, sp.MapColumn(mem, 0));
            Assert.AreEqual(4, sp.MapColumn(mem, 1));
            Assert.AreEqual(7, sp.MapColumn(mem, 2));
            Assert.AreEqual(10, sp.MapColumn(mem, 3));

            // Test 1D with same dimension of columns and inputs
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 4 });
            parameters.SetInputDimensions(new int[] { 4 });
            InitSp();

            Assert.AreEqual(0, sp.MapColumn(mem, 0));
            Assert.AreEqual(1, sp.MapColumn(mem, 1));
            Assert.AreEqual(2, sp.MapColumn(mem, 2));
            Assert.AreEqual(3, sp.MapColumn(mem, 3));

            // Test 1D with dimensions of length 1
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 1 });
            parameters.SetInputDimensions(new int[] { 1 });
            InitSp();

            Assert.AreEqual(0, sp.MapColumn(mem, 0));

            // Test 2D
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 12, 4 });
            parameters.SetInputDimensions(new int[] { 36, 12 });
            InitSp();

            Assert.AreEqual(13, sp.MapColumn(mem, 0));
            Assert.AreEqual(49, sp.MapColumn(mem, 4));
            Assert.AreEqual(52, sp.MapColumn(mem, 5));
            Assert.AreEqual(58, sp.MapColumn(mem, 7));
            Assert.AreEqual(418, sp.MapColumn(mem, 47));

            // Test 2D with some input dimensions smaller than column dimensions.
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 4, 4 });
            parameters.SetInputDimensions(new int[] { 3, 5 });
            InitSp();

            Assert.AreEqual(0, sp.MapColumn(mem, 0));
            Assert.AreEqual(4, sp.MapColumn(mem, 3));
            Assert.AreEqual(14, sp.MapColumn(mem, 15));
        }

        [TestMethod]
        public void TestMapPotential1D()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 12 });
            parameters.SetColumnDimensions(new int[] { 4 });
            parameters.SetPotentialRadius(2);
            parameters.SetPotentialPct(1);
            parameters.SetParameterByKey(Parameters.KEY.WRAP_AROUND, false);
            InitSp();

            Assert.AreEqual(12, mem.GetInputDimensions()[0]);
            Assert.AreEqual(4, mem.GetColumnDimensions()[0]);
            Assert.AreEqual(2, mem.GetPotentialRadius());

            // Test without wrapAround and potentialPct = 1
            int[] expected = new int[] { 0, 1, 2, 3 };
            int[] mask = sp.MapPotential(mem, 0, false);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            expected = new int[] { 5, 6, 7, 8, 9 };
            mask = sp.MapPotential(mem, 2, false);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            // Test with wrapAround and potentialPct = 1
            mem.SetWrapAround(true);
            expected = new int[] { 0, 1, 2, 3, 11 };
            mask = sp.MapPotential(mem, 0, true);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            expected = new int[] { 0, 8, 9, 10, 11 };
            mask = sp.MapPotential(mem, 3, true);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            // Test with wrapAround and potentialPct < 1
            parameters.SetPotentialPct(0.5);
            parameters.SetParameterByKey(Parameters.KEY.WRAP_AROUND, true);
            InitSp();

            int[] supersetMask = new int[] { 0, 1, 2, 3, 11 };
            mask = sp.MapPotential(mem, 0, true);
            Assert.AreEqual(mask.Length, 3);
            List<int> unionList = new List<int>(supersetMask);
            unionList.AddRange(mask);
            int[] unionMask = ArrayUtils.Unique(unionList.ToArray());
            Assert.IsTrue(Arrays.AreEqual(unionMask, supersetMask));
        }

        [TestMethod]
        public void TestMapPotential2D()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 6, 12 });
            parameters.SetColumnDimensions(new int[] { 2, 4 });
            parameters.SetPotentialRadius(1);
            parameters.SetPotentialPct(1);
            InitSp();

            //Test without wrapAround
            int[] mask = sp.MapPotential(mem, 0, false);
            HashSet<int> trueIndices = new HashSet<int>(new int[] { 0, 1, 2, 12, 13, 14, 24, 25, 26 });
            HashSet<int> maskSet = new HashSet<int>(mask);
            Assert.IsTrue(trueIndices.SetEquals(maskSet));

            trueIndices.Clear();
            maskSet.Clear();
            trueIndices.UnionWith(new int[] { 6, 7, 8, 18, 19, 20, 30, 31, 32 });
            mask = sp.MapPotential(mem, 2, false);
            maskSet.UnionWith(mask);
            Assert.IsTrue(trueIndices.SetEquals(maskSet));

            //Test with wrapAround
            trueIndices.Clear();
            maskSet.Clear();
            parameters.SetPotentialRadius(2);
            InitSp();
            trueIndices.UnionWith(
                    new int[] { 0, 1, 2, 3, 11,
                        12, 13, 14, 15, 23,
                        24, 25, 26, 27, 35,
                        36, 37, 38, 39, 47,
                        60, 61, 62, 63, 71 });
            mask = sp.MapPotential(mem, 0, true);
            maskSet.UnionWith(mask);
            Assert.IsTrue(trueIndices.SetEquals(maskSet));

            trueIndices.Clear();
            maskSet.Clear();
            trueIndices.UnionWith(
                    new int[] { 0, 8, 9, 10, 11,
                        12, 20, 21, 22, 23,
                        24, 32, 33, 34, 35,
                        36, 44, 45, 46, 47,
                        60, 68, 69, 70, 71 });
            mask = sp.MapPotential(mem, 3, true);
            maskSet.UnionWith(mask);
            Assert.IsTrue(trueIndices.SetEquals(maskSet));
        }

        [TestMethod]
        public void TestMapPotential1Column1Input()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 1 });
            parameters.SetColumnDimensions(new int[] { 1 });
            parameters.SetPotentialRadius(2);
            parameters.SetPotentialPct(1);
            parameters.SetParameterByKey(Parameters.KEY.WRAP_AROUND, false);
            InitSp();

            //Test without wrapAround and potentialPct = 1
            int[] expectedMask = new int[] { 0 };
            int[] mask = sp.MapPotential(mem, 0, false);
            HashSet<int> trueIndices = new HashSet<int>(expectedMask);
            HashSet<int> maskSet = new HashSet<int>(mask);
            // The *position* of the one "on" bit expected. 
            // Python version returns [1] which is the on bit in the zero'th position
            Assert.IsTrue(trueIndices.SetEquals(maskSet));
        }

        [TestMethod]
        public void TestInhibitColumns()
        {
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetInhibitionRadius(10);
            InitSp();

            double density = 0;
            //Mocks to test which method gets called
            Mock<SpatialPooler> spMock = new Mock<SpatialPooler>();
            spMock.CallBase = true;
            spMock.Setup(p => p.InhibitColumnsGlobal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()))
                .Returns<Connections, double[], double>((c, ds, den) =>
                {
                    density = den;
                    return new[] { 1 };
                }).Verifiable();
            spMock.Setup(p => p.InhibitColumnsLocal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()))
                .Returns<Connections, double[], double>((c, ds, den) =>
                {
                    density = den;
                    return new[] { 2 };
                }).Verifiable();

            double[] overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            mem.SetNumActiveColumnsPerInhArea(5);
            mem.SetLocalAreaDensity(0.1);
            mem.SetGlobalInhibition(true);
            mem.SetInhibitionRadius(5);
            double trueDensity = mem.GetLocalAreaDensity();
            spMock.Object.InhibitColumns(mem, overlaps);

            spMock.Verify(p => p.InhibitColumnsGlobal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Once);
            spMock.Verify(p => p.InhibitColumnsLocal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Never);

            Assert.AreEqual(trueDensity, density, .01d);

            //////
            spMock.ResetCalls();
            mem.SetColumnDimensions(new int[] { 50, 10 });
            //Internally calculated during init, to overwrite we put after init
            mem.SetGlobalInhibition(false);
            mem.SetInhibitionRadius(7);

            double[] tieBreaker = new double[500];
            Arrays.Fill(tieBreaker, 0);
            mem.SetTieBreaker(tieBreaker);
            overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            spMock.Object.InhibitColumns(mem, overlaps);
            trueDensity = mem.GetLocalAreaDensity();
            spMock.Verify(p => p.InhibitColumnsGlobal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Never);
            spMock.Verify(p => p.InhibitColumnsLocal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Once);

            Assert.AreEqual(trueDensity, density, .01d);

            //////
            spMock.ResetCalls();
            parameters.SetInputDimensions(new int[] { 100, 10 });
            parameters.SetColumnDimensions(new int[] { 100, 10 });
            parameters.SetGlobalInhibition(false);
            parameters.SetLocalAreaDensity(-1);
            parameters.SetNumActiveColumnsPerInhArea(3);
            InitSp();

            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(4);
            tieBreaker = new double[1000];
            Arrays.Fill(tieBreaker, 0);
            mem.SetTieBreaker(tieBreaker);
            overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            spMock.Object.InhibitColumns(mem, overlaps);
            trueDensity = 3.0 / 81.0;
            spMock.Verify(p => p.InhibitColumnsGlobal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Never);
            spMock.Verify(p => p.InhibitColumnsLocal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Once);
            Assert.AreEqual(trueDensity, density, .01d);

            //////
            spMock.ResetCalls();
            mem.SetNumActiveColumnsPerInhArea(7);

            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(1);
            tieBreaker = new double[1000];
            Arrays.Fill(tieBreaker, 0);
            mem.SetTieBreaker(tieBreaker);
            overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            spMock.Object.InhibitColumns(mem, overlaps);
            trueDensity = 0.5;
            spMock.Verify(p => p.InhibitColumnsGlobal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Never);
            spMock.Verify(p => p.InhibitColumnsLocal(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<double>()), Times.Once);
            Assert.AreEqual(trueDensity, density, .01d);

        }

        [TestMethod]
        public void TestUpdateBoostFactors()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5/*Don't care*/ });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetMaxBoost(10.0);
            parameters.SetRandom(new XorshiftRandom(42));
            InitSp();

            mem.SetNumColumns(6);

            double[] minActiveDutyCycles = new double[6];
            Arrays.Fill(minActiveDutyCycles, 0.000001D);
            mem.SetMinActiveDutyCycles(minActiveDutyCycles);

            double[] activeDutyCycles = new double[] { 0.1, 0.3, 0.02, 0.04, 0.7, 0.12 };
            mem.SetActiveDutyCycles(activeDutyCycles);

            double[] trueBoostFactors = new double[] { 1, 1, 1, 1, 1, 1 };
            sp.UpdateBoostFactors(mem);
            double[] boostFactors = mem.GetBoostFactors();
            for (int i = 0; i < boostFactors.Length; i++)
            {
                Assert.AreEqual(trueBoostFactors[i], boostFactors[i], 0.1D);
            }

            ////////////////
            minActiveDutyCycles = new double[] { 0.1, 0.3, 0.02, 0.04, 0.7, 0.12 };
            mem.SetMinActiveDutyCycles(minActiveDutyCycles);
            Arrays.Fill(mem.GetBoostFactors(), 0);
            sp.UpdateBoostFactors(mem);
            boostFactors = mem.GetBoostFactors();
            for (int i = 0; i < boostFactors.Length; i++)
            {
                Assert.AreEqual(trueBoostFactors[i], boostFactors[i], 0.1D);
            }

            ////////////////
            minActiveDutyCycles = new double[] { 0.1, 0.2, 0.02, 0.03, 0.7, 0.12 };
            mem.SetMinActiveDutyCycles(minActiveDutyCycles);
            activeDutyCycles = new double[] { 0.01, 0.02, 0.002, 0.003, 0.07, 0.012 };
            mem.SetActiveDutyCycles(activeDutyCycles);
            trueBoostFactors = new double[] { 9.1, 9.1, 9.1, 9.1, 9.1, 9.1 };
            sp.UpdateBoostFactors(mem);
            boostFactors = mem.GetBoostFactors();
            for (int i = 0; i < boostFactors.Length; i++)
            {
                Assert.AreEqual(trueBoostFactors[i], boostFactors[i], 0.1D);
            }

            ////////////////
            minActiveDutyCycles = new double[] { 0.1, 0.2, 0.02, 0.03, 0.7, 0.12 };
            mem.SetMinActiveDutyCycles(minActiveDutyCycles);
            Arrays.Fill(activeDutyCycles, 0);
            mem.SetActiveDutyCycles(activeDutyCycles);
            Arrays.Fill(trueBoostFactors, 10.0);
            sp.UpdateBoostFactors(mem);
            boostFactors = mem.GetBoostFactors();
            for (int i = 0; i < boostFactors.Length; i++)
            {
                Assert.AreEqual(trueBoostFactors[i], boostFactors[i], 0.1D);
            }
        }

        [TestMethod]
        public void TestUpdateInhibitionRadius()
        {
            SetupParameters();
            InitSp();

            //Test global inhibition case
            mem.SetGlobalInhibition(true);
            mem.SetColumnDimensions(new int[] { 57, 31, 2 });
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(57, mem.GetInhibitionRadius());

            ////////////
            // ((3 * 4) - 1) / 2 => round up
            Mock<SpatialPooler> mock = new Mock<SpatialPooler>();
            mock.Setup(p => p.AvgConnectedSpanForColumnND(It.IsAny<Connections>(), It.IsAny<int>()))
                .Returns<Connections, int>((c, i) => 3);
            mock.Setup(p => p.AvgColumnsPerInput(It.IsAny<Connections>()))
                .Returns<Connections>((c) => 4);
            //SpatialPooler mock = new SpatialPooler()
            //{
            //    private static final long serialVersionUID = 1L;
            //    public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 3;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 4;
            //    }
            //};

            mem.SetGlobalInhibition(false);
            sp = mock.Object;
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(6, mem.GetInhibitionRadius());

            //////////////
            //Test clipping at 1.0
            mock = new Mock<SpatialPooler>();
            mock.Setup(p => p.AvgConnectedSpanForColumnND(It.IsAny<Connections>(), It.IsAny<int>()))
                .Returns<Connections, int>((c, i) => 0.5);
            mock.Setup(p => p.AvgColumnsPerInput(It.IsAny<Connections>()))
                .Returns<Connections>((c) => 1.2);
            //mock = new SpatialPooler()
            //{
            //    private static final long serialVersionUID = 1L;
            //    public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 0.5;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 1.2;
            //    }
            //};
            mem.SetGlobalInhibition(false);
            sp = mock.Object;
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(1, mem.GetInhibitionRadius());

            /////////////

            //Test rounding up
            mock = new Mock<SpatialPooler>();
            mock.Setup(p => p.AvgConnectedSpanForColumnND(It.IsAny<Connections>(), It.IsAny<int>()))
                .Returns<Connections, int>((c, i) => 2.4);
            mock.Setup(p => p.AvgColumnsPerInput(It.IsAny<Connections>()))
                .Returns<Connections>((c) => 2);
            //mock = new SpatialPooler()
            //{
            //        private static final long serialVersionUID = 1L;
            //    public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 2.4;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 2;
            //    }
            //};
            mem.SetGlobalInhibition(false);
            sp = mock.Object;
            //((2 * 2.4) - 1) / 2.0 => round up
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(2, mem.GetInhibitionRadius());
        }

        [TestMethod]
        public void TestAvgColumnsPerInput()
        {
            SetupParameters();
            InitSp();

            mem.SetColumnDimensions(new int[] { 2, 2, 2, 2 });
            mem.SetInputDimensions(new int[] { 4, 4, 4, 4 });
            Assert.AreEqual(0.5, sp.AvgColumnsPerInput(mem), 0);

            mem.SetColumnDimensions(new int[] { 2, 2, 2, 2 });
            mem.SetInputDimensions(new int[] { 7, 5, 1, 3 });
            double trueAvgColumnPerInput = (2.0 / 7 + 2.0 / 5 + 2.0 / 1 + 2 / 3.0) / 4.0d;
            Assert.AreEqual(trueAvgColumnPerInput, sp.AvgColumnsPerInput(mem), 0);

            mem.SetColumnDimensions(new int[] { 3, 3 });
            mem.SetInputDimensions(new int[] { 3, 3 });
            trueAvgColumnPerInput = 1;
            Assert.AreEqual(trueAvgColumnPerInput, sp.AvgColumnsPerInput(mem), 0);

            mem.SetColumnDimensions(new int[] { 25 });
            mem.SetInputDimensions(new int[] { 5 });
            trueAvgColumnPerInput = 5;
            Assert.AreEqual(trueAvgColumnPerInput, sp.AvgColumnsPerInput(mem), 0);

            mem.SetColumnDimensions(new int[] { 3, 3, 3, 5, 5, 6, 6 });
            mem.SetInputDimensions(new int[] { 3, 3, 3, 5, 5, 6, 6 });
            trueAvgColumnPerInput = 1;
            Assert.AreEqual(trueAvgColumnPerInput, sp.AvgColumnsPerInput(mem), 0);

            mem.SetColumnDimensions(new int[] { 3, 6, 9, 12 });
            mem.SetInputDimensions(new int[] { 3, 3, 3, 3 });
            trueAvgColumnPerInput = 2.5;
            Assert.AreEqual(trueAvgColumnPerInput, sp.AvgColumnsPerInput(mem), 0);
        }

        [TestMethod]
        public void TestAvgConnectedSpanForColumnND()
        {
            sp = new SpatialPooler();
            mem = new Connections();

            int[] inputDimensions = new int[] { 4, 4, 2, 5 };
            mem.SetInputDimensions(inputDimensions);
            mem.SetColumnDimensions(new int[] { 5 });
            sp.InitMatrices(mem);

            List<int> connected = new List<int>();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 1, 0, 1, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 1, 0, 1, 1 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 2, 1, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 0, 1, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 1, 0, 1, 3 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 2, 2, 1, 0 }, false));
            connected.Sort();
            //[ 45  46  48 105 125 145]
            //mem.GetConnectedSynapses().Set(0, connected.ToArray());
            mem.GetPotentialPools().Set(0, new Pool(6));
            mem.GetColumn(0).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            connected.Clear();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 2, 0, 1, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 2, 0, 0, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 0, 0, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 0, 1, 0 }, false));
            connected.Sort();
            //[ 80  85 120 125]
            //mem.GetConnectedSynapses().Set(1, connected.ToArray());
            mem.GetPotentialPools().Set(1, new Pool(4));
            mem.GetColumn(1).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            connected.Clear();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 1, 4 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 0, 3 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 0, 1 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 1, 0, 0, 2 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 1, 1 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 3, 1, 1 }, false));
            connected.Sort();
            //[  1   3   6   9  42 156]
            //mem.GetConnectedSynapses().Set(2, connected.ToArray());
            mem.GetPotentialPools().Set(2, new Pool(4));
            mem.GetColumn(2).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            connected.Clear();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 3, 1, 4 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 0, 0 }, false));
            connected.Sort();
            //[  0 159]
            //mem.GetConnectedSynapses().Set(3, connected.ToArray());
            mem.GetPotentialPools().Set(3, new Pool(4));
            mem.GetColumn(3).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            //[]
            connected.Clear();
            mem.GetPotentialPools().Set(4, new Pool(4));
            mem.GetColumn(4).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            double[] trueAvgConnectedSpan = new double[] { 11.0 / 4d, 6.0 / 4d, 14.0 / 4d, 15.0 / 4d, 0d };
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double connectedSpan = sp.AvgConnectedSpanForColumnND(mem, i);
                Assert.AreEqual(trueAvgConnectedSpan[i], connectedSpan, 0);
            }
        }

        [TestMethod]
        public void TestBumpUpWeakColumns()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 8 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            mem.SetSynPermBelowStimulusInc(0.01);
            mem.SetSynPermTrimThreshold(0.05);
            mem.SetOverlapDutyCycles(new double[] { 0, 0.009, 0.1, 0.001, 0.002 });
            mem.SetMinOverlapDutyCycles(new double[] { .01, .01, .01, .01, .01 });

            int[][] potentialPools =
            {
                new[] {1, 1, 1, 1, 0, 0, 0, 0},
                new[] {1, 0, 0, 0, 1, 1, 0, 1},
                new[] {0, 0, 1, 0, 1, 1, 1, 0},
                new[] {1, 1, 1, 0, 0, 0, 1, 0},
                new[] {1, 1, 1, 1, 1, 1, 1, 1}
            };

            double[][] permanences = new double[][]
            {
                new[] {0.200, 0.120, 0.090, 0.040, 0.000, 0.000, 0.000, 0.000},
                new[] {0.150, 0.000, 0.000, 0.000, 0.180, 0.120, 0.000, 0.450},
                new[] {0.000, 0.000, 0.014, 0.000, 0.032, 0.044, 0.110, 0.000},
                new[] {0.041, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000},
                new[] {0.100, 0.738, 0.045, 0.002, 0.050, 0.008, 0.208, 0.034}
            };

            double[][] truePermanences = new double[][]
            {
                new[] {0.210, 0.130, 0.100, 0.000, 0.000, 0.000, 0.000, 0.000},
                new[] {0.160, 0.000, 0.000, 0.000, 0.190, 0.130, 0.000, 0.460},
                new[] {0.000, 0.000, 0.014, 0.000, 0.032, 0.044, 0.110, 0.000},
                new[] {0.051, 0.000, 0.000, 0.000, 0.000, 0.000, 0.188, 0.000},
                new[] {0.110, 0.748, 0.055, 0.000, 0.060, 0.000, 0.218, 0.000}
            };

            //        Condition <?> cond = new Condition.Adapter<Integer>()
            //        {
            //        public boolean eval(int n)
            //    {
            //        return n == 1;
            //    }
            //};
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], n => n == 1);
                mem.GetColumn(i).SetProximalConnectedSynapsesForTest(mem, indexes);
                mem.GetColumn(i).SetProximalPermanences(mem, permanences[i]);
            }

            //Execute method being tested
            sp.BumpUpWeakColumns(mem);

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double[] perms = mem.GetPotentialPools().Get(i).GetDensePermanences(mem);
                for (int j = 0; j < truePermanences[i].Length; j++)
                {
                    Assert.AreEqual(truePermanences[i][j], perms[j], 0.01);
                }
            }
        }

        [TestMethod]
        public void TestUpdateMinDutyCycleLocal()
        {
            SetupDefaultParameters();
            parameters.SetInputDimensions(new[] { 5 });
            parameters.SetColumnDimensions(new[] { 8 });
            parameters.SetParameterByKey(Parameters.KEY.WRAP_AROUND, false);
            InitSp();

            mem.SetInhibitionRadius(1);
            mem.SetOverlapDutyCycles(new[] { 0.7, 0.1, 0.5, 0.01, 0.78, 0.55, 0.1, 0.001 });
            mem.SetActiveDutyCycles(new[] { 0.9, 0.3, 0.5, 0.7, 0.1, 0.01, 0.08, 0.12 });
            mem.SetMinPctActiveDutyCycles(0.1);
            mem.SetMinPctOverlapDutyCycles(0.2);
            sp.UpdateMinDutyCyclesLocal(mem);

            double[] resultMinActiveDutyCycles = mem.GetMinActiveDutyCycles();
            double[] expected0 = { 0.09, 0.09, 0.07, 0.07, 0.07, 0.01, 0.012, 0.012 };

            Array.ForEach(ArrayUtils.Range(0, expected0.Length), i => Assert.AreEqual(expected0[i], resultMinActiveDutyCycles[i], 0.01));

            double[] resultMinOverlapDutyCycles = mem.GetMinOverlapDutyCycles();
            double[] expected1 = new double[] { 0.14, 0.14, 0.1, 0.156, 0.156, 0.156, 0.11, 0.02 };
            Array.ForEach(ArrayUtils.Range(0, expected1.Length), i => Assert.AreEqual(expected1[i], resultMinOverlapDutyCycles[i], 0.01));

            // wrapAround = true
            SetupDefaultParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 8 });
            parameters.SetParameterByKey(Parameters.KEY.WRAP_AROUND, true);
            InitSp();

            mem.SetInhibitionRadius(1);
            mem.SetOverlapDutyCycles(new double[] { 0.7, 0.1, 0.5, 0.01, 0.78, 0.55, 0.1, 0.001 });
            mem.SetActiveDutyCycles(new double[] { 0.9, 0.3, 0.5, 0.7, 0.1, 0.01, 0.08, 0.12 });
            mem.SetMinPctActiveDutyCycles(0.1);
            mem.SetMinPctOverlapDutyCycles(0.2);
            sp.UpdateMinDutyCyclesLocal(mem);

            double[] resultMinActiveDutyCycles2 = mem.GetMinActiveDutyCycles();
            double[] expected2 = { 0.09, 0.09, 0.07, 0.07, 0.07, 0.01, 0.012, 0.09 };
            Array.ForEach(ArrayUtils.Range(0, expected2.Length), i => Assert.AreEqual(expected2[i], resultMinActiveDutyCycles2[i], 0.01));

            double[] resultMinOverlapDutyCycles2 = mem.GetMinOverlapDutyCycles();
            double[] expected3 = new double[] { 0.14, 0.14, 0.1, 0.156, 0.156, 0.156, 0.11, 0.14 };
            Array.ForEach(ArrayUtils.Range(0, expected3.Length), i => Assert.AreEqual(expected3[i], resultMinOverlapDutyCycles2[i], 0.01));
        }

        [TestMethod]
        public void TestUpdateMinDutyCycleGlobal()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            mem.SetMinPctOverlapDutyCycles(0.01);
            mem.SetMinPctActiveDutyCycles(0.02);
            mem.SetOverlapDutyCycles(new double[] { 0.06, 1, 3, 6, 0.5 });
            mem.SetActiveDutyCycles(new double[] { 0.6, 0.07, 0.5, 0.4, 0.3 });

            sp.UpdateMinDutyCyclesGlobal(mem);
            double[] trueMinActiveDutyCycles = new double[mem.GetNumColumns()];
            Arrays.Fill(trueMinActiveDutyCycles, 0.02 * 0.6);
            double[] trueMinOverlapDutyCycles = new double[mem.GetNumColumns()];
            Arrays.Fill(trueMinOverlapDutyCycles, 0.01 * 6);
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                //          System.out.println(i + ") " + trueMinOverlapDutyCycles[i] + "  -  " +  mem.GetMinOverlapDutyCycles()[i]);
                //          System.out.println(i + ") " + trueMinActiveDutyCycles[i] + "  -  " +  mem.GetMinActiveDutyCycles()[i]);
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
                Assert.AreEqual(trueMinActiveDutyCycles[i], mem.GetMinActiveDutyCycles()[i], 0.01);
            }

            mem.SetMinPctOverlapDutyCycles(0.015);
            mem.SetMinPctActiveDutyCycles(0.03);
            mem.SetOverlapDutyCycles(new double[] { 0.86, 2.4, 0.03, 1.6, 1.5 });
            mem.SetActiveDutyCycles(new double[] { 0.16, 0.007, 0.15, 0.54, 0.13 });
            sp.UpdateMinDutyCyclesGlobal(mem);
            Arrays.Fill(trueMinOverlapDutyCycles, 0.015 * 2.4);
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                //          System.out.println(i + ") " + trueMinOverlapDutyCycles[i] + "  -  " +  mem.GetMinOverlapDutyCycles()[i]);
                //          System.out.println(i + ") " + trueMinActiveDutyCycles[i] + "  -  " +  mem.GetMinActiveDutyCycles()[i]);
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
            }

            mem.SetMinPctOverlapDutyCycles(0.015);
            mem.SetMinPctActiveDutyCycles(0.03);
            mem.SetOverlapDutyCycles(new double[5]);
            mem.SetActiveDutyCycles(new double[5]);
            sp.UpdateMinDutyCyclesGlobal(mem);
            Arrays.Fill(trueMinOverlapDutyCycles, 0);
            Arrays.Fill(trueMinActiveDutyCycles, 0);
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                //          System.out.println(i + ") " + trueMinOverlapDutyCycles[i] + "  -  " +  mem.GetMinOverlapDutyCycles()[i]);
                //          System.out.println(i + ") " + trueMinActiveDutyCycles[i] + "  -  " +  mem.GetMinActiveDutyCycles()[i]);
                Assert.AreEqual(trueMinActiveDutyCycles[i], mem.GetMinActiveDutyCycles()[i], 0.01);
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
            }
        }

        [TestMethod]
        public void TestIsUpdateRound()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            mem.SetUpdatePeriod(50);
            mem.SetIterationNum(1);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(39);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(50);
            Assert.IsTrue(sp.IsUpdateRound(mem));
            mem.SetIterationNum(1009);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(1250);
            Assert.IsTrue(sp.IsUpdateRound(mem));

            mem.SetUpdatePeriod(125);
            mem.SetIterationNum(0);
            Assert.IsTrue(sp.IsUpdateRound(mem));
            mem.SetIterationNum(200);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(249);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(1330);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(1249);
            Assert.IsFalse(sp.IsUpdateRound(mem));
            mem.SetIterationNum(1375);
            Assert.IsTrue(sp.IsUpdateRound(mem));

        }

        [TestMethod]
        public void TestAdaptSynapses()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 8 });
            parameters.SetColumnDimensions(new int[] { 4 });
            parameters.SetSynPermInactiveDec(0.01);
            parameters.SetSynPermActiveInc(0.1);
            InitSp();

            mem.SetSynPermTrimThreshold(0.05);

            int[][] potentialPools = new int[][]
            {
                new[] {1, 1, 1, 1, 0, 0, 0, 0},
                new[] {1, 0, 0, 0, 1, 1, 0, 1},
                new[] {0, 0, 1, 0, 0, 0, 1, 0},
                new[] {1, 0, 0, 0, 0, 0, 1, 0}
            };

            double[][] permanences = new double[][]
            {
                new[] {0.200, 0.120, 0.090, 0.040, 0.000, 0.000, 0.000, 0.000},
                new[] {0.150, 0.000, 0.000, 0.000, 0.180, 0.120, 0.000, 0.450},
                new[] {0.000, 0.000, 0.014, 0.000, 0.000, 0.000, 0.110, 0.000},
                new[] {0.040, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000}
            };

            double[][] truePermanences = new double[][]
            {
                new[] {0.300, 0.110, 0.080, 0.140, 0.000, 0.000, 0.000, 0.000},
                //     Inc     Dec    Dec    Inc    -      -      -      -
                new[] {0.250, 0.000, 0.000, 0.000, 0.280, 0.110, 0.000, 0.440},
                //     Inc     -      -      -      Inc    Dec    -      Dec
                new[] {0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.210, 0.000},
                //      -      -     Trim    -      -      -      Inc    -
                new[] {0.040, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000}
                //      -      -      -      -      -      -      -      -     // Only cols 0,1,2 are active 
                // (see 'activeColumns' below)
            };

            //Condition <?> cond = new Condition.Adapter<Integer>()
            //{
            //        public boolean eval(int n)
            //    {
            //        return n == 1;
            //    }
            //};
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], n => n == 1);
                mem.GetColumn(i).SetProximalConnectedSynapsesForTest(mem, indexes);
                mem.GetColumn(i).SetProximalPermanences(mem, permanences[i]);
            }

            int[] inputVector = new int[] { 1, 0, 0, 1, 1, 0, 1, 0 };
            int[] activeColumns = new int[] { 0, 1, 2 };

            sp.AdaptSynapses(mem, inputVector, activeColumns);

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double[] perms = mem.GetPotentialPools().Get(i).GetDensePermanences(mem);
                for (int j = 0; j < truePermanences[i].Length; j++)
                {
                    Assert.AreEqual(truePermanences[i][j], perms[j], 0.01);
                }
            }

            //////////////////////////////

            potentialPools = new int[][]
            {
                new[] {1, 1, 1, 0, 0, 0, 0, 0},
                new[] {0, 1, 1, 1, 0, 0, 0, 0},
                new[] {0, 0, 1, 1, 1, 0, 0, 0},
                new[] {1, 0, 0, 0, 0, 0, 1, 0}
            };

            permanences = new double[][]
            {
                new[] {0.200, 0.120, 0.090, 0.000, 0.000, 0.000, 0.000, 0.000},
                new[] {0.000, 0.017, 0.232, 0.400, 0.180, 0.120, 0.000, 0.450},
                new[] {0.000, 0.000, 0.014, 0.051, 0.730, 0.000, 0.000, 0.000},
                new[] {0.170, 0.000, 0.000, 0.000, 0.000, 0.000, 0.380, 0.000}
            };

            truePermanences = new double[][]
            {
                new[] {0.300, 0.110, 0.080, 0.000, 0.000, 0.000, 0.000, 0.000},
                new[] {0.000, 0.000, 0.222, 0.500, 0.000, 0.000, 0.000, 0.000},
                new[] {0.000, 0.000, 0.000, 0.151, 0.830, 0.000, 0.000, 0.000},
                new[] {0.170, 0.000, 0.000, 0.000, 0.000, 0.000, 0.380, 0.000}
            };

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], n => n == 1);
                mem.GetColumn(i).SetProximalConnectedSynapsesForTest(mem, indexes);
                mem.GetColumn(i).SetProximalPermanences(mem, permanences[i]);
            }

            sp.AdaptSynapses(mem, inputVector, activeColumns);

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double[] perms = mem.GetPotentialPools().Get(i).GetDensePermanences(mem);
                for (int j = 0; j < truePermanences[i].Length; j++)
                {
                    Assert.AreEqual(truePermanences[i][j], perms[j], 0.01);
                }
            }
        }

        [TestMethod]
        public void TestRaisePermanenceThreshold()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetSynPermConnected(0.1);
            parameters.SetStimulusThreshold(3);
            parameters.SetSynPermBelowStimulusInc(0.01);
            //The following parameter is not set to "1" in the Python version
            //This is necessary to reproduce the test conditions of having as
            //many pool members as Input Bits, which would never happen under
            //normal circumstances because we want to enforce sparsity
            parameters.SetPotentialPct(1);

            InitSp();

            //We set the values on the Connections permanences here just for illustration
            SparseObjectMatrix<double[]> objMatrix = new SparseObjectMatrix<double[]>(new int[] { 5, 5 });
            objMatrix.Set(0, new[] { 0.0, 0.11, 0.095, 0.092, 0.01 });
            objMatrix.Set(1, new[] { 0.12, 0.15, 0.02, 0.12, 0.09 });
            objMatrix.Set(2, new[] { 0.51, 0.081, 0.025, 0.089, 0.31 });
            objMatrix.Set(3, new[] { 0.18, 0.0601, 0.11, 0.011, 0.03 });
            objMatrix.Set(4, new[] { 0.011, 0.011, 0.011, 0.011, 0.011 });
            mem.SetProximalPermanences(objMatrix);

            //      mem.SetConnectedSynapses(new SparseObjectMatrix<int[]>(new int[] { 5, 5 }));
            //      SparseObjectMatrix<int[]> syns = mem.GetConnectedSynapses();
            //      syns.Set(0, new int[] { 0, 1, 0, 0, 0 });
            //      syns.Set(1, new int[] { 1, 1, 0, 1, 0 });
            //      syns.Set(2, new int[] { 1, 0, 0, 0, 1 });
            //      syns.Set(3, new int[] { 1, 0, 1, 0, 0 });
            //      syns.Set(4, new int[] { 0, 0, 0, 0, 0 });

            // TODO: review this, an error will occur probably in the test!!
            //mem.SetConnectedCounts(new int[] { 1, 3, 2, 2, 0 });

            double[][] truePermanences = new double[][]
            {
                new[] {0.01, 0.12, 0.105, 0.102, 0.02}, // incremented once
                new[] {0.12, 0.15, 0.02, 0.12, 0.09}, // no change
                new[] {0.53, 0.101, 0.045, 0.109, 0.33}, // increment twice
                new[] {0.22, 0.1001, 0.15, 0.051, 0.07}, // increment four times
                new[] {0.101, 0.101, 0.101, 0.101, 0.101} // increment 9 times
            };
            //FORGOT TO SET PERMANENCES ABOVE - DON'T USE mem.SetPermanences() 
            int[] indices = mem.GetMemory().GetSparseIndices();
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double[] perm = mem.GetPotentialPools().Get(i).GetSparsePermanences();
                sp.RaisePermanenceToThreshold(mem, perm, indices);

                for (int j = 0; j < perm.Length; j++)
                {
                    Assert.AreEqual(truePermanences[i][j], perm[j], 0.001);
                }
            }
        }

        [TestMethod]
        public void TestUpdatePermanencesForColumn()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetSynPermTrimThreshold(0.05);
            //The following parameter is not set to "1" in the Python version
            //This is necessary to reproduce the test conditions of having as
            //many pool members as Input Bits, which would never happen under
            //normal circumstances because we want to enforce sparsity
            parameters.SetPotentialPct(1);
            InitSp();

            double[][] permanences = new double[][]
            {
                new[] {-0.10, 0.500, 0.400, 0.010, 0.020},
                new[] {0.300, 0.010, 0.020, 0.120, 0.090},
                new[] {0.070, 0.050, 1.030, 0.190, 0.060},
                new[] {0.180, 0.090, 0.110, 0.010, 0.030},
                new[] {0.200, 0.101, 0.050, -0.09, 1.100}
            };

            int[][] trueConnectedSynapses = new int[][]
            {
                new[] {0, 1, 1, 0, 0},
                new[] {1, 0, 0, 1, 0},
                new[] {0, 0, 1, 1, 0},
                new[] {1, 0, 1, 0, 0},
                new[] {1, 1, 0, 0, 1}
            };

            int[][] connectedDense = new int[][]
            {
                new[] {1, 2},
                new[] {0, 3},
                new[] {2, 3},
                new[] {0, 2},
                new[] {0, 1, 4}
            };

            int[] trueConnectedCounts = new int[] { 2, 2, 2, 2, 3 };

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                mem.GetColumn(i).SetProximalPermanences(mem, permanences[i]);
                sp.UpdatePermanencesForColumn(mem, permanences[i], mem.GetColumn(i), connectedDense[i], true);
                int[] dense = mem.GetColumn(i).GetProximalDendrite().GetConnectedSynapsesDense(mem);
                Assert.AreEqual(Arrays.ToString(trueConnectedSynapses[i]), Arrays.ToString(dense));
            }

            Assert.AreEqual(Arrays.ToString(trueConnectedCounts), Arrays.ToString(mem.GetConnectedCounts().GetTrueCounts()));
        }

        [TestMethod]
        public void TestCalculateOverlap()
        {
            SetupDefaultParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            int[] dimensions = new int[] { 5, 10 };
            int[][] connectedSynapses = new int[][]
            {
                new[] {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}
            };
            Matrix<double> sm = new SparseMatrix(dimensions[0], dimensions[1]);
            for (int i = 0; i < sm.RowCount; i++)
            {
                for (int j = 0; j < sm.ColumnCount; j++)
                {
                    sm[i, j] = connectedSynapses[i][j];
                    //sm.Set(connectedSynapses[i][j], i, j);
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

            int[] inputVector = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            int[] overlaps = sp.CalculateOverlap(mem, inputVector);
            int[] trueOverlaps = new int[5];
            double[] overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            double[] trueOverlapsPct = new double[5];
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            /////////////

            connectedSynapses = new int[][]
            {
                new[] {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}
            };
            sm = new SparseMatrix(dimensions[0], dimensions[1]);
            for (int i = 0; i < sm.RowCount; i++)
            {
                for (int j = 0; j < sm.ColumnCount; j++)
                {
                    sm[i, j] = connectedSynapses[i][j];
                    //sm.Set(connectedSynapses[i][j], i, j);
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

            inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 10, 8, 6, 4, 2 };
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 1, 1, 1, 1, 1 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            //////////////////

            connectedSynapses = new int[][]
            {
                new[] {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
                new[] {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}
            };
            sm = new SparseMatrix(dimensions[0], dimensions[1]);
            for (int i = 0; i < sm.RowCount; i++)
            {
                for (int j = 0; j < sm.ColumnCount; j++)
                {
                    sm[i, j] = connectedSynapses[i][j];
                    //sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.At(i, j));
                }
            }

            inputVector = new int[10];
            inputVector[9] = 1;
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 1, 1, 1, 1, 1 };
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 0.1, 0.125, 1.0 / 6, 0.25, 0.5 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            ///////////////////

            connectedSynapses = new int[][]
            {
                new[] {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
                new[] {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
                new[] {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
                new[] {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
                new[] {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}
            };
            sm = new SparseMatrix(dimensions[0], dimensions[1]);
            for (int i = 0; i < sm.RowCount; i++)
            {
                for (int j = 0; j < sm.ColumnCount; j++)
                {
                    sm[i, j] = connectedSynapses[i][j];
                    //sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.At(i, j));
                }
            }

            inputVector = new int[] { 1, 0, 1, 0, 1, 0, 1, 0, 1, 0 };
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 1, 1, 1, 1, 1 };
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 0.5, 0.5, 0.5, 0.5, 0.5 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));
        }

        /**
         * test initial permanence generation. ensure that
         * a correct amount of synapses are initialized in 
         * a connected state, with permanence values drawn from
         * the correct ranges
         */
        [TestMethod]
        public void TestInitPermanence1()
        {
            SetupParameters();

            Mock<SpatialPooler> spMock = new Mock<SpatialPooler>();
            spMock.Setup(p => p.RaisePermanenceToThreshold(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<int[]>()));
            sp = spMock.Object;

            //sp = new SpatialPooler()
            //{
            //    private static final long serialVersionUID = 1L;
            //    public void raisePermanenceToThreshold(Connections c, double[] perm, int[] maskPotential)
            //    {
            //        //Mock out
            //    }
            //};
            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);
            mem.SetNumInputs(10);

            mem.SetPotentialRadius(2);
            double connectedPct = 1;
            int[] mask = new int[] { 0, 1, 2, 8, 9 };
            double[] perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            int numcon = ArrayUtils.ValueGreaterCount(mem.GetSynPermConnected(), perm);
            Assert.AreEqual(5, numcon, 0);

            connectedPct = 0;
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            numcon = ArrayUtils.ValueGreaterCount(mem.GetSynPermConnected(), perm);
            Assert.AreEqual(0, numcon, 0);

            connectedPct = 0.5;
            mem.SetPotentialRadius(100);
            mem.SetNumInputs(100);
            mask = new int[100];
            for (int i = 0; i < 100; i++) mask[i] = i;
            double[] perma = sp.InitPermanence(mem, mask, 0, connectedPct);
            numcon = ArrayUtils.ValueGreaterOrEqualCount(mem.GetSynPermConnected(), perma);
            Assert.IsTrue(numcon > 0);
            Assert.IsTrue(numcon < mem.GetNumInputs());

            const double minThresh = 0.0;
            double maxThresh = mem.GetSynPermMax();
            double[] results = ArrayUtils.RetainLogicalAnd(perma, d => d >= minThresh, d => d < maxThresh);
            //    new Condition.Adapter<Object>() {
            //            public boolean eval(double d)
            //            {
            //                return d >= minThresh;
            //            }
            //        },
            //                    new Condition.Adapter<Object>() {
            //                        public boolean eval(double d)
            //        {
            //            return d < maxThresh;
            //        }
            //    }
            //});
            Assert.IsTrue(results.Length > 0);
        }

        /**
         * Test initial permanence generation. ensure that permanence values
         * are only assigned to bits within a column's potential pool.
         */
        [TestMethod]
        public void TestInitPermanence2()
        {
            SetupParameters();
            //sp = new SpatialPooler()
            //{
            //    private static final long serialVersionUID = 1L;
            //    public void raisePermanenceToThreshold(Connections c, double[] perm, int[] maskPotential)
            //    {
            //        //Mock out
            //    }
            //};
            Mock<SpatialPooler> spMock = new Mock<SpatialPooler>();
            spMock.Setup(p => p.RaisePermanenceToThreshold(It.IsAny<Connections>(), It.IsAny<double[]>(), It.IsAny<int[]>()));
            sp = spMock.Object;

            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);

            mem.SetNumInputs(10);
            double connectedPct = 1;
            int[] mask = new int[] { 0, 1 };
            double[] perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            int[] trueConnected = new int[] { 0, 1 };
            //    Condition<?> cond = new Condition.Adapter<Object>()
            //    {
            //            public boolean eval(double d)
            //    {
            //        return d > 0;
            //    }
            //};
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, d => d > 0)));

            connectedPct = 1;
            mask = new int[] { 4, 5, 6 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 4, 5, 6 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, d => d > 0)));

            connectedPct = 1;
            mask = new int[] { 8, 9 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 8, 9 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, d => d > 0)));

            connectedPct = 1;
            mask = new int[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, d => d > 0)));
        }

        /**
         * Tests that duty cycles are updated properly according
         * to the mathematical formula. also check the effects of
         * supplying a maxPeriod to the function.
         */
        [TestMethod]
        public void TestUpdateDutyCycleHelper()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            double[] dc = new double[5];
            Arrays.Fill(dc, 1000.0);
            double[] newvals = new double[5];
            int period = 1000;
            double[] newDc = sp.UpdateDutyCyclesHelper(mem, dc, newvals, period);
            double[] trueNewDc = new double[] { 999, 999, 999, 999, 999 };
            Assert.IsTrue(Arrays.AreEqual(trueNewDc, newDc));

            dc = new double[5];
            Arrays.Fill(dc, 1000.0);
            newvals = new double[5];
            Arrays.Fill(newvals, 1000);
            period = 1000;
            newDc = sp.UpdateDutyCyclesHelper(mem, dc, newvals, period);
            trueNewDc = Arrays.CopyOf(dc, 5);
            Assert.IsTrue(Arrays.AreEqual(trueNewDc, newDc));

            dc = new double[5];
            Arrays.Fill(dc, 1000.0);
            newvals = new double[] { 2000, 4000, 5000, 6000, 7000 };
            period = 1000;
            newDc = sp.UpdateDutyCyclesHelper(mem, dc, newvals, period);
            trueNewDc = new double[] { 1001, 1003, 1004, 1005, 1006 };
            Assert.IsTrue(Arrays.AreEqual(trueNewDc, newDc));

            dc = new double[] { 1000, 800, 600, 400, 2000 };
            newvals = new double[5];
            period = 2;
            newDc = sp.UpdateDutyCyclesHelper(mem, dc, newvals, period);
            trueNewDc = new double[] { 500, 400, 300, 200, 1000 };
            Assert.IsTrue(Arrays.AreEqual(trueNewDc, newDc));
        }

        [TestMethod]
        public void TestInhibitColumnsGlobal()
        {
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 10 });
            InitSp();
            //Internally calculated during init, to overwrite we put after init
            parameters.SetInhibitionRadius(2);
            double density = 0.3;
            double[] overlaps = new double[] { 1, 2, 1, 4, 8, 3, 12, 5, 4, 1 };
            int[] active = sp.InhibitColumnsGlobal(mem, overlaps, density);
            int[] trueActive = new int[] { 4, 6, 7 };
            Array.Sort(active);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            density = 0.5;
            mem.SetNumColumns(10);
            overlaps = ArrayUtils.Range(0, 10).Select(i => (double)i).ToArray();
            active = sp.InhibitColumnsGlobal(mem, overlaps, density);
            trueActive = ArrayUtils.Range(5, 10).ToArray();
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));
        }

        [TestMethod]
        public void TestInhibitColumnsLocal()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 10 });
            InitSp();

            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(2);
            double density = 0.5;
            double[] overlaps = new double[] { 1, 2, 7, 0, 3, 4, 16, 1, 1.5, 1.7 };
            //  L  W  W  L  L  W  W   L   W    W (wrapAround=true)
            //  L  W  W  L  L  W  W   L   L    W (wrapAround=false)

            mem.SetWrapAround(true);
            int[] trueActive = new int[] { 1, 2, 5, 6, 8, 9 };
            int[] active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            mem.SetWrapAround(false);
            trueActive = new int[] { 1, 2, 5, 6, 9 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            density = 0.5;
            mem.SetInhibitionRadius(3);
            overlaps = new double[] { 1, 2, 7, 0, 3, 4, 16, 1, 1.5, 1.7 };
            //  L  W  W  L  W  W  W   L   L    W (wrapAround=true)
            //  L  W  W  L  W  W  W   L   L    L (wrapAround=false)

            mem.SetWrapAround(true);
            trueActive = new int[] { 1, 2, 4, 5, 6, 9 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            mem.SetWrapAround(false);
            trueActive = new int[] { 1, 2, 4, 5, 6, 9 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            // Test add to winners
            density = 0.3333;
            mem.SetInhibitionRadius(3);
            overlaps = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            //  W  W  L  L  W  W  L  L  L  L (wrapAround=true)
            //  W  W  L  L  W  W  L  L  W  L (wrapAround=false)

            mem.SetWrapAround(true);
            trueActive = new int[] { 0, 1, 4, 5 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            mem.SetWrapAround(false);
            trueActive = new int[] { 0, 1, 4, 5, 8 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));
        }

        //    /**
        //     * As coded in the Python test
        //     */
        //    [TestMethod]
        //    public void testGetNeighborsND() {
        //        //This setup isn't relevant to this test
        //        setupParameters();
        //        parameters.SetInputDimensions(new int[] { 9, 5 });
        //        parameters.SetColumnDimensions(new int[] { 5, 5 });
        //        initSP();
        //        ////////////////////// Test not part of Python port /////////////////////
        //        int[] result = sp.GetNeighborsND(mem, 2, mem.GetInputMatrix(), 3, true).ToArray();
        //        int[] expected = new int[] { 
        //                0, 1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 
        //                13, 14, 15, 16, 17, 18, 19, 30, 31, 32, 33, 
        //                34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44 
        //        };
        //        for(int i = 0;i < result.Length;i++) {
        //            Assert.AreEqual(expected[i], result[i]);
        //        }
        //        /////////////////////////////////////////////////////////////////////////
        //        setupParameters();
        //        int[] dimensions = new int[] { 5, 7, 2 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        int radius = 1;
        //        int x = 1;
        //        int y = 3;
        //        int z = 2;
        //        int columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x });
        //        int[] neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        String expect = "[18, 19, 20, 21, 22, 23, 32, 33, 34, 36, 37, 46, 47, 48, 49, 50, 51]";
        //        Assert.AreEqual(expect, ArrayUtils.print1DArray(neighbors));
        //
        //        /////////////////////////////////////////
        //        setupParameters();
        //        dimensions = new int[] { 5, 7, 9 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        radius = 3;
        //        x = 0;
        //        y = 0;
        //        z = 3;
        //        columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x });
        //        neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        expect = "[0, 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26, "
        //                + "27, 28, 29, 30, 33, 34, 35, 36, 37, 38, 39, 42, 43, 44, 45, 46, 47, 48, 51, "
        //                + "52, 53, 54, 55, 56, 57, 60, 61, 62, 63, 64, 65, 66, 69, 70, 71, 72, 73, 74, "
        //                + "75, 78, 79, 80, 81, 82, 83, 84, 87, 88, 89, 90, 91, 92, 93, 96, 97, 98, 99, "
        //                + "100, 101, 102, 105, 106, 107, 108, 109, 110, 111, 114, 115, 116, 117, 118, 119, "
        //                + "120, 123, 124, 125, 126, 127, 128, 129, 132, 133, 134, 135, 136, 137, 138, 141, "
        //                + "142, 143, 144, 145, 146, 147, 150, 151, 152, 153, 154, 155, 156, 159, 160, 161, "
        //                + "162, 163, 164, 165, 168, 169, 170, 171, 172, 173, 174, 177, 178, 179, 180, 181, "
        //                + "182, 183, 186, 187, 188, 190, 191, 192, 195, 196, 197, 198, 199, 200, 201, 204, "
        //                + "205, 206, 207, 208, 209, 210, 213, 214, 215, 216, 217, 218, 219, 222, 223, 224, "
        //                + "225, 226, 227, 228, 231, 232, 233, 234, 235, 236, 237, 240, 241, 242, 243, 244, "
        //                + "245, 246, 249, 250, 251, 252, 253, 254, 255, 258, 259, 260, 261, 262, 263, 264, "
        //                + "267, 268, 269, 270, 271, 272, 273, 276, 277, 278, 279, 280, 281, 282, 285, 286, "
        //                + "287, 288, 289, 290, 291, 294, 295, 296, 297, 298, 299, 300, 303, 304, 305, 306, "
        //                + "307, 308, 309, 312, 313, 314]";
        //        Assert.AreEqual(expect, ArrayUtils.print1DArray(neighbors));
        //
        //        /////////////////////////////////////////
        //        setupParameters();
        //        dimensions = new int[] { 5, 10, 7, 6 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //
        //        radius = 4;
        //        int w = 2;
        //        x = 5;
        //        y = 6;
        //        z = 2;
        //        columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x, w });
        //        neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        HashSet<int> trueNeighbors = new HashSet<int>();
        //        for(int i = -radius;i <= radius;i++) {
        //            for(int j = -radius;j <= radius;j++) {
        //                for(int k = -radius;k <= radius;k++) {
        //                    for(int m = -radius;m <= radius;m++) {
        //                        int zprime = (int)ArrayUtils.positiveRemainder((z + i), dimensions[0]);
        //                        int yprime = (int)ArrayUtils.positiveRemainder((y + j), dimensions[1]);
        //                        int xprime = (int)ArrayUtils.positiveRemainder((x + k), dimensions[2]);
        //                        int wprime = (int)ArrayUtils.positiveRemainder((w + m), dimensions[3]);
        //                        trueNeighbors.Add(mem.GetInputMatrix().ComputeIndex(new int[] { zprime, yprime, xprime, wprime }));
        //                    }
        //                }
        //            }
        //        }
        //        trueNeighbors.remove(columnIndex);
        //        int[] tneighbors = ArrayUtils.unique(trueNeighbors.ToArray());
        //        Assert.AreEqual(ArrayUtils.print1DArray(tneighbors), ArrayUtils.print1DArray(neighbors));
        //
        //        /////////////////////////////////////////
        //        //Tests from getNeighbors1D from Python unit test
        //        setupParameters();
        //        dimensions = new int[] { 8 };
        //        parameters.SetColumnDimensions(dimensions);
        //        parameters.SetInputDimensions(dimensions);
        //        initSP();
        //        AbstractSparseBinaryMatrix sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        sbm.Set(new int[] { 2, 4 }, new int[] { 1, 1 }, true);
        //        radius = 1;
        //        columnIndex = 3;
        //        int[] mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        List<int> msk = new List<int>(mask);
        //        List<int> neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //////
        //        setupParameters();
        //        dimensions = new int[] { 8 };
        //        parameters.SetInputDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        sbm.Set(new int[] { 1, 2, 4, 5 }, new int[] { 1, 1, 1, 1 }, true);
        //        radius = 2;
        //        columnIndex = 3;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //Wrap around
        //        setupParameters();
        //        dimensions = new int[] { 8 };
        //        parameters.SetInputDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        sbm.Set(new int[] { 1, 2, 6, 7 }, new int[] { 1, 1, 1, 1 }, true);
        //        radius = 2;
        //        columnIndex = 0;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //Radius too big
        //        setupParameters();
        //        dimensions = new int[] { 8 };
        //        parameters.SetInputDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        sbm.Set(new int[] { 0, 1, 2, 3, 4, 5, 7 }, new int[] { 1, 1, 1, 1, 1, 1, 1 }, true);
        //        radius = 20;
        //        columnIndex = 6;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //These are all the same tests from 2D
        //        setupParameters();
        //        dimensions = new int[] { 6, 5 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        int[][] input = new int[][] { 
        //            {0, 0, 0, 0, 0},
        //            {0, 0, 0, 0, 0},
        //            {0, 1, 1, 1, 0},
        //            {0, 1, 0, 1, 0},
        //            {0, 1, 1, 1, 0},
        //            {0, 0, 0, 0, 0}};
        //            for(int i = 0;i < input.Length;i++) {
        //                for(int j = 0;j < input[i].Length;j++) {
        //                    if(input[i][j] == 1) 
        //                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), 1);
        //                }
        //            }
        //        radius = 1;
        //        columnIndex = 3*5 + 2;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        ////////
        //        setupParameters();
        //        dimensions = new int[] { 6, 5 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        input = new int[][] { 
        //            {0, 0, 0, 0, 0},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 0, 1, 1},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 1, 1, 1}};
        //        for(int i = 0;i < input.Length;i++) {
        //            for(int j = 0;j < input[i].Length;j++) {
        //                if(input[i][j] == 1) 
        //                    sbm.Set(sbm.ComputeIndex(new int[] { i, j }), 1);
        //            }
        //        }
        //        radius = 2;
        //        columnIndex = 3*5 + 2;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //Radius too big
        //        setupParameters();
        //        dimensions = new int[] { 6, 5 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        input = new int[][] { 
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 0, 1, 1},
        //            {1, 1, 1, 1, 1},
        //            {1, 1, 1, 1, 1}};
        //            for(int i = 0;i < input.Length;i++) {
        //                for(int j = 0;j < input[i].Length;j++) {
        //                    if(input[i][j] == 1) 
        //                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), 1);
        //                }
        //            }
        //        radius = 7;
        //        columnIndex = 3*5 + 2;
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //
        //        //Wrap-around
        //        setupParameters();
        //        dimensions = new int[] { 6, 5 };
        //        parameters.SetInputDimensions(dimensions);
        //        parameters.SetColumnDimensions(dimensions);
        //        initSP();
        //        sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
        //        input = new int[][] { 
        //            {1, 0, 0, 1, 1},
        //            {0, 0, 0, 0, 0},
        //            {0, 0, 0, 0, 0},
        //            {0, 0, 0, 0, 0},
        //            {1, 0, 0, 1, 1},
        //            {1, 0, 0, 1, 0}};
        //        for(int i = 0;i < input.Length;i++) {
        //            for(int j = 0;j < input[i].Length;j++) {
        //                if(input[i][j] == 1) 
        //                    sbm.Set(sbm.ComputeIndex(new int[] { i, j }), 1);
        //            }
        //        }
        //        radius = 1;
        //        columnIndex = sbm.GetMaxIndex();
        //        mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
        //        msk = new List<int>(mask);
        //        neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
        //        neg.removeAll(msk);
        //        Assert.IsTrue(sbm.all(mask));
        //        Assert.IsFalse(sbm.any(neg));
        //    }

        [TestMethod]
        public void TestInit()
        {
            SetupParameters();
            parameters.SetNumActiveColumnsPerInhArea(0);
            parameters.SetLocalAreaDensity(0);

            Connections c = new Connections();
            parameters.Apply(c);

            SpatialPooler sp = new SpatialPooler();

            // Local Area Density cannot be 0
            try
            {
                sp.Init(c);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Inhibition parameters are invalid", e.Message);
                Assert.AreEqual(typeof(SpatialPooler.InvalidSpatialPoolerParamValueException), e.GetType());
            }

            // Local Area Density can't be above 0.5
            parameters.SetLocalAreaDensity(0.51);
            c = new Connections();
            parameters.Apply(c);
            try
            {
                sp.Init(c);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Inhibition parameters are invalid", e.Message);
                Assert.AreEqual(typeof(SpatialPooler.InvalidSpatialPoolerParamValueException), e.GetType());
            }

            // Local Area Density should be sane here
            parameters.SetLocalAreaDensity(0.5);
            c = new Connections();
            parameters.Apply(c);
            try
            {
                sp.Init(c);
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }

            // Num columns cannot be 0
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 0 });
            c = new Connections();
            parameters.Apply(c);
            try
            {
                sp.Init(c);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Invalid number of columns: 0", e.Message);
                Assert.AreEqual(typeof(SpatialPooler.InvalidSpatialPoolerParamValueException), e.GetType());
            }

            // Reset column dims
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 5 });

            // Num columns cannot be 0
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 0 });
            c = new Connections();
            parameters.Apply(c);
            try
            {
                sp.Init(c);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Invalid number of inputs: 0", e.Message);
                Assert.AreEqual(typeof(SpatialPooler.InvalidSpatialPoolerParamValueException), e.GetType());
            }
        }

        [TestMethod]
        public void testComputeInputMismatch()
        {
            SetupParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 2, 4 });
            parameters.SetColumnDimensions(new int[] { 5, 1 });

            Connections c = new Connections();
            parameters.Apply(c);

            int misMatchedDims = 6; // not 8
            SpatialPooler sp = new SpatialPooler();
            sp.Init(c);
            try
            {
                sp.Compute(c, new int[misMatchedDims], new int[25], true);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Input array must be same size as the defined number"
                    + " of inputs: From Params: 8, From Input Vector: 6", e.Message);
                Assert.AreEqual(typeof(SpatialPooler.InvalidSpatialPoolerParamValueException), e.GetType());
            }


            // Now Do the right thing
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 2, 4 });
            parameters.SetColumnDimensions(new int[] { 5, 1 });

            c = new Connections();
            parameters.Apply(c);

            int matchedDims = 8; // same as input dimension multiplied, above
            sp.Init(c);
            try
            {
                sp.Compute(c, new int[matchedDims], new int[25], true);
            }
            catch (Exception e)
            {
                Assert.Fail();
            }
        }
    }
}