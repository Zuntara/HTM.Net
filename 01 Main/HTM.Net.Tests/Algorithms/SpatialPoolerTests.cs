using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

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
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 5 });//5
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 5 });//5
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 3);//3
            parameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.5);//0.5
            parameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, false);
            parameters.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0);
            parameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 3.0);
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
        }

        private void InitSp()
        {
            sp = new SpatialPooler();
            mem = new Connections();
            parameters.Apply(mem);
            sp.Init(mem);
        }

        [TestMethod]
        public void ConfirmSpConstruction()
        {
            SetupParameters();

            InitSp();

            Assert.AreEqual(5, mem.GetInputDimensions()[0]);
            Assert.AreEqual(5, mem.GetColumnDimensions()[0]);
            Assert.AreEqual(3, mem.GetPotentialRadius());
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
            Assert.AreEqual(0, mem.GetSpVerbosity());

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
            parameters.SetMinPctOverlapDutyCycle(0.1);
            parameters.SetMinPctActiveDutyCycle(0.1);
            parameters.SetDutyCyclePeriod(10);
            parameters.SetMaxBoost(10);
            parameters.SetSynPermTrimThreshold(0);

            //This is 0.5 in Python version due to use of dense 
            // permanence instead of sparse (as it should be)
            parameters.SetPotentialPct(1);

            parameters.SetSynPermConnected(0.1);

            InitSp();

            SpatialPoolerMockCompute1 mock = new SpatialPoolerMockCompute1();
            //        {
            //            public int[] inhibitColumns(Connections c, double[] overlaps)
            //    {
            //        return new int[] { 0, 1, 2, 3, 4 };
            //    }
            //};

            int[] inputVector = new int[] { 1, 0, 1, 0, 1, 0, 0, 1, 1 };
            int[] activeArray = new int[] { 0, 0, 0, 0, 0 };
            for (int i = 0; i < 20; i++)
            {
                mock.Compute(mem, inputVector, activeArray, true, true);
            }

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                Console.WriteLine("Slice " + Arrays.ToString(((SparseByteArray)mem.GetConnectedCounts().GetSlice(i)).AsDense()));
                Console.WriteLine("DensePerm" + Arrays.ToString(mem.GetPotentialPools().Get(i).GetDensePermanences(mem)));
                Console.WriteLine("Input" + Arrays.ToString(inputVector));
                Assert.IsTrue(Arrays.AreEqual(inputVector.Select(d=>(byte)d).ToArray(), (((SparseByteArray)mem.GetConnectedCounts().GetSlice(i)).AsDense())));
            }
        }

        public class SpatialPoolerMockCompute1 : SpatialPooler
        {
            public override int[] InhibitColumns(Connections c, double[] overlaps)
            {
                return new int[] { 0, 1, 2, 3, 4 };
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
            parameters.SetMinPctOverlapDutyCycle(0.1);
            parameters.SetMinPctActiveDutyCycle(0.1);
            parameters.SetDutyCyclePeriod(10);
            parameters.SetMaxBoost(10);
            parameters.SetSynPermConnected(0.1);

            InitSp();

            SpatialPoolerMockCompute1 mock = new SpatialPoolerMockCompute1();
            //        {
            //        public int[] inhibitColumns(Connections c, double[] overlaps)
            //    {
            //        return new int[] { 0, 1, 2, 3, 4 };
            //    }
            //};

            int[] inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            int[] activeArray = new int[] { 0, 0, 0, 0, 0 };
            for (int i = 0; i < 20; i++)
            {
                mock.Compute(mem, inputVector, activeArray, true, true);
            }

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                //     		System.out.Println(Arrays.ToString((int[])mem.GetConnectedCounts().GetSlice(i)));
                //     		System.out.Println(Arrays.ToString(mem.GetPotentialPools().GetObject(i).GetDensePermanences(mem)));
                byte[] permanences = ArrayUtils.ToByteArray(mem.GetPotentialPools().Get(i).GetDensePermanences(mem));
                byte[] potential = ((SparseByteArray)mem.GetConnectedCounts().GetSlice(i)).AsDense().ToArray();
                Assert.IsTrue(Arrays.AreEqual(permanences, potential));
            }
        }

        /// <summary>
        /// Given a specific input and initialization params the SP should return this exact output.
        /// 
        /// Previously output varied between platforms (OSX/Linux etc) == (in Python)
        /// </summary>
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
            parameters.SetStimulusThreshold(1);
            parameters.SetSynPermInactiveDec(0.01);
            parameters.SetSynPermActiveInc(0.1);
            parameters.SetMinPctOverlapDutyCycle(0.1);
            parameters.SetMinPctActiveDutyCycle(0.1);
            parameters.SetDutyCyclePeriod(1000);
            parameters.SetMaxBoost(10);
            parameters.SetSynPermConnected(0.1);
            parameters.SetSynPermTrimThreshold(0);
            parameters.SetRandom(new MersenneTwister(42));
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

            sp.Compute(mem, inputVector, activeArray, true, false);

            int[] real = ArrayUtils.Where(activeArray, n => n > 0);

            int[] expected = new int[] {
                48, 152, 323, 368, 418, 425, 426, 520, 531, 540, 553, 561, 652, 690, 732, 759, 769, 867, 909,
                987, 1019, 1105, 1147, 1169, 1224, 1265, 1281, 1350, 1397, 1434, 1465, 1498, 1542, 1552, 1608,
                1614, 1648, 1649, 1651, 2047 };

            Debug.WriteLine(Arrays.ToString(real));
            Debug.WriteLine(Arrays.ToString(expected));

            Assert.AreEqual(expected.Length, real.Length, "Output length is not the same");

            Assert.IsTrue(Arrays.AreEqual(expected, real));
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

            // Test 1D with same dimensions of length 1
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
            List<int> stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            List<int> trueStripped = new List<int>(new int[] { 0, 1, 4 });
            Assert.IsTrue(trueStripped.SequenceEqual(stripped));

            mem.UpdateActiveDutyCycles(new double[] { 0.9, 0, 0, 0, 0.4, 0.3 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = new List<int>(new int[] { 0, 4, 5 });
            Assert.IsTrue(trueStripped.SequenceEqual(stripped));

            mem.UpdateActiveDutyCycles(new double[] { 0, 0, 0, 0, 0, 0 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = new List<int>();
            Assert.IsTrue(trueStripped.SequenceEqual(stripped));

            mem.UpdateActiveDutyCycles(new double[] { 1, 1, 1, 1, 1, 1 });
            activeColumns = ArrayUtils.Range(0, 6);
            stripped = sp.StripUnlearnedColumns(mem, activeColumns);
            trueStripped = new List<int>(ArrayUtils.Range(0, 6));
            Assert.IsTrue(trueStripped.SequenceEqual(stripped));
        }

        [TestMethod]
        public void TestMapPotential1D()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 12 });
            parameters.SetColumnDimensions(new int[] { 4 });
            parameters.SetPotentialRadius(2);
            parameters.SetPotentialPct(1);
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
            expected = new int[] { 0, 1, 2, 3, 11 };
            mask = sp.MapPotential(mem, 0, true);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            expected = new int[] { 0, 8, 9, 10, 11 };
            mask = sp.MapPotential(mem, 3, true);
            Assert.IsTrue(Arrays.AreEqual(expected, mask));

            // Test with wrapAround and potentialPct < 1
            parameters.SetPotentialPct(0.5);
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

        //////////////////////////////////////////////////////////////
        /**
         * Local test apparatus for {@link #testInhibitColumns()}
         */
        bool globalCalled = false;
        bool localCalled = false;
        double _density = 0;
        public void Reset()
        {
            this.globalCalled = false;
            this.localCalled = false;
            this._density = 0;
        }
        public void SetGlobalCalled(bool b)
        {
            this.globalCalled = b;
        }
        public void SetLocalCalled(bool b)
        {
            this.localCalled = b;
        }
        //////////////////////////////////////////////////////////////

        public class SpatialPoolerMockGlobal : SpatialPooler
        {
            private readonly Func<Connections, double[], double, int[]> _overrideFunc;
            public SpatialPoolerMockGlobal(Func<Connections, double[], double, int[]> overrideFunc)
            {
                _overrideFunc = overrideFunc;
            }

            public override int[] InhibitColumnsGlobal(Connections c, double[] overlap, double density)
            {
                return _overrideFunc(c, overlap, density);
            }
        }

        public class SpatialPoolerMockLocal : SpatialPooler
        {
            private readonly Func<Connections, double[], double, int[]> _overrideFunc;

            public SpatialPoolerMockLocal(Func<Connections, double[], double, int[]> overrideFunc)
            {
                _overrideFunc = overrideFunc;
            }

            public override int[] InhibitColumnsLocal(Connections c, double[] overlap, double density)
            {
                return _overrideFunc(c, overlap, density);
            }
        }

        [TestMethod]
        public void TestInhibitColumns()
        {
            SetupParameters();
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetInhibitionRadius(10);
            InitSp();

            //Mocks to test which method gets called
            SpatialPooler inhibitColumnsGlobal = new SpatialPoolerMockGlobal((c, overlap, density) =>
            {
                SetGlobalCalled(true);
                _density = density;
                return new int[] { 1 };
            });
            SpatialPooler inhibitColumnsLocal = new SpatialPoolerMockLocal((c, overlap, density) =>
            {
                SetLocalCalled(true);
                _density = density;
                return new int[] { 2 };
            });


            double[] overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            mem.SetNumActiveColumnsPerInhArea(5);
            mem.SetLocalAreaDensity(0.1);
            mem.SetGlobalInhibition(true);
            mem.SetInhibitionRadius(5);
            double trueDensity = mem.GetLocalAreaDensity();
            inhibitColumnsGlobal.InhibitColumns(mem, overlaps);
            Assert.IsTrue(globalCalled);
            Assert.IsTrue(!localCalled);
            Assert.AreEqual(trueDensity, _density, .01d);

            //////
            Reset();
            parameters.SetInputDimensions(new int[] { 50, 10 });
            parameters.SetColumnDimensions(new int[] { 50, 10 });
            parameters.SetGlobalInhibition(false);
            parameters.SetLocalAreaDensity(0.1);
            InitSp();
            //Internally calculated during init, to overwrite we put after init
            parameters.SetInhibitionRadius(7);

            double[] tieBreaker = new double[500];
            Arrays.Fill(tieBreaker, 0);
            mem.SetTieBreaker(tieBreaker);
            overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            inhibitColumnsLocal.InhibitColumns(mem, overlaps);
            trueDensity = mem.GetLocalAreaDensity();
            Assert.IsTrue(!globalCalled);
            Assert.IsTrue(localCalled);
            Assert.AreEqual(trueDensity, _density, .01d);

            //////
            Reset();
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
            inhibitColumnsLocal.InhibitColumns(mem, overlaps);
            trueDensity = 3.0 / 81.0;
            Assert.IsTrue(!globalCalled);
            Assert.IsTrue(localCalled);
            Assert.AreEqual(trueDensity, _density, .01d);

            //////
            Reset();
            parameters.SetInputDimensions(new int[] { 100, 10 });
            parameters.SetColumnDimensions(new int[] { 100, 10 });
            parameters.SetGlobalInhibition(false);
            parameters.SetLocalAreaDensity(-1);
            parameters.SetNumActiveColumnsPerInhArea(7);
            InitSp();

            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(1);
            tieBreaker = new double[1000];
            Arrays.Fill(tieBreaker, 0);
            mem.SetTieBreaker(tieBreaker);
            overlaps = ArrayUtils.Sample(mem.GetNumColumns(), mem.GetRandom());
            inhibitColumnsLocal.InhibitColumns(mem, overlaps);
            trueDensity = 0.5;
            Assert.IsTrue(!globalCalled);
            Assert.IsTrue(localCalled);
            Assert.AreEqual(trueDensity, _density, .01d);

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
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));
        }

        [TestMethod]
        public void TestInhibitColumnsLocal()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 10 });
            InitSp();

            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(2);
            double density = 0.5;
            double[] overlaps = new double[] { 1, 2, 7, 0, 3, 4, 16, 1, 1.5, 1.7 };
            int[] trueActive = new int[] { 1, 2, 5, 6, 9 };
            int[] active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 10 });
            InitSp();
            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(3);
            overlaps = new double[] { 1, 2, 7, 0, 3, 4, 16, 1, 1.5, 1.7 };
            trueActive = new int[] { 1, 2, 5, 6 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            //Commented out in Python version because it is wrong?
            //Assert.IsTrue(Arrays.AreEqual(trueActive, active));

            // Test add to winners
            density = 0.3333;
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 10 });
            InitSp();
            //Internally calculated during init, to overwrite we put after init
            mem.SetInhibitionRadius(3);
            overlaps = new double[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            trueActive = new int[] { 0, 1, 4, 5, 8 };
            active = sp.InhibitColumnsLocal(mem, overlaps, density);
            Assert.IsTrue(Arrays.AreEqual(trueActive, active));
        }

        [TestMethod]
        public void TestUpdateBoostFactors()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 6/*Don't care*/ });
            parameters.SetColumnDimensions(new int[] { 6 });
            parameters.SetMaxBoost(10.0);
            InitSp();

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
        public void TestAvgConnectedSpanForColumnNd()
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
            connected.Sort(/*0, connected.Count*/);
            //[ 45  46  48 105 125 145]
            //mem.GetConnectedSynapses().Set(0, connected.ToArray());
            mem.GetPotentialPools().Set(0, new Pool(6));
            mem.GetColumn(0).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            connected.Clear();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 2, 0, 1, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 2, 0, 0, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 0, 0, 0 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 0, 1, 0 }, false));
            connected.Sort(/*0, connected.Count*/);
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
            connected.Sort(/*0, connected.Count*/);
            //[  1   3   6   9  42 156]
            //mem.GetConnectedSynapses().Set(2, connected.ToArray());
            mem.GetPotentialPools().Set(2, new Pool(4));
            mem.GetColumn(2).SetProximalConnectedSynapsesForTest(mem, connected.ToArray());

            connected.Clear();
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 3, 3, 1, 4 }, false));
            connected.Add(mem.GetInputMatrix().ComputeIndex(new int[] { 0, 0, 0, 0 }, false));
            connected.Sort(/*0, connected.Count*/);
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

            int[][] potentialPools = new int[][]
            {
                new int[] {1, 1, 1, 1, 0, 0, 0, 0},
                new int[] {1, 0, 0, 0, 1, 1, 0, 1},
                new int[] {0, 0, 1, 0, 1, 1, 1, 0},
                new int[] {1, 1, 1, 0, 0, 0, 1, 0},
                new int[] {1, 1, 1, 1, 1, 1, 1, 1}
            };

            double[][] permanences = new double[][]
            {
                new double[] {0.200, 0.120, 0.090, 0.040, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.150, 0.000, 0.000, 0.000, 0.180, 0.120, 0.000, 0.450},
                new double[] {0.000, 0.000, 0.014, 0.000, 0.032, 0.044, 0.110, 0.000},
                new double[] {0.041, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000},
                new double[] {0.100, 0.738, 0.045, 0.002, 0.050, 0.008, 0.208, 0.034}
            };

            double[][] truePermanences = new double[][]
            {
                new double[] {0.210, 0.130, 0.100, 0.000, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.160, 0.000, 0.000, 0.000, 0.190, 0.130, 0.000, 0.460},
                new double[] {0.000, 0.000, 0.014, 0.000, 0.032, 0.044, 0.110, 0.000},
                new double[] {0.051, 0.000, 0.000, 0.000, 0.000, 0.000, 0.188, 0.000},
                new double[] {0.110, 0.748, 0.055, 0.000, 0.060, 0.000, 0.218, 0.000}
            };

            //        Condition <?> cond = new Condition.Adapter<Integer>()
            //        {
            //        public bool eval(int n)
            //    {
            //        return n == 1;
            //    }
            //};
            Func<int, bool> cond = n => n == 1;

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], cond);
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

        public class SpatialPoolerMockNeighborsND : SpatialPooler
        {
            private readonly Func<Connections, int, ISparseMatrix, int, bool, List<int>> _overrideFunc;
            public SpatialPoolerMockNeighborsND(Func<Connections, int, ISparseMatrix, int, bool, List<int>> overrideFunc)
            {
                _overrideFunc = overrideFunc;
            }

            public override List<int> GetNeighborsND(Connections c, int columnIndex, ISparseMatrix topology, int radius, bool wrapAround)
            {
                return _overrideFunc(c, columnIndex, topology, radius, wrapAround);

            }
        }

        [TestMethod]
        public void TestUpdateMinDutyCycleLocal()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 5 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            int returnIndex = 0;
            int[][] returnVals = new int[][]
            {
                       new  int[]   {0, 1, 2},
                       new  int[]   {1, 2, 3},
                       new  int[]   {2, 3, 4},
                       new  int[]   {0, 2, 3},
                       new  int[]   {0, 1, 3}
            };

            SpatialPoolerMockNeighborsND mockSP = new SpatialPoolerMockNeighborsND((c, columnIndex, topology, rad, wrapArnd) =>
            {
                return new List<int>(returnVals[returnIndex++]);
            });
            //SpatialPooler mockSP = new SpatialPooler() {
            //        int returnIndex = 0;
            //        int[][] returnVals = new int[][]
            //        {
            //           new  int[]   {0, 1, 2},
            //           new  int[]   {1, 2, 3},
            //           new  int[]   {2, 3, 4},
            //           new  int[]   {0, 2, 3},
            //           new  int[]   {0, 1, 3}
            //        };
            //        @Override
            //        public List<int> getNeighborsND(
            //                Connections c, int columnIndex, SparseMatrix<?> topology, int radius, bool wrapAround)
            //    {
            //        return new List<int>(returnVals[returnIndex++]);
            //    }
            //};

            mem.SetMinPctOverlapDutyCycles(0.04);
            mem.SetOverlapDutyCycles(new double[] { 1.4, 0.5, 1.2, 0.8, 0.1 });
            double[] trueMinOverlapDutyCycles = new double[] {
                0.04*1.4, 0.04*1.2, 0.04*1.2, 0.04*1.4, 0.04*1.4 };

            mem.SetMinPctActiveDutyCycles(0.02);
            mem.SetActiveDutyCycles(new double[] { 0.4, 0.5, 0.2, 0.18, 0.1 });
            double[] trueMinActiveDutyCycles = new double[] {
                0.02*0.5, 0.02*0.5, 0.02*0.2, 0.02*0.4, 0.02*0.5 };

            double[] mins = new double[mem.GetNumColumns()];
            Arrays.Fill(mins, 0);
            mem.SetMinOverlapDutyCycles(mins);
            mem.SetMinActiveDutyCycles(Arrays.CopyOf(mins, mins.Length));
            mockSP.UpdateMinDutyCyclesLocal(mem);
            for (int i = 0; i < trueMinOverlapDutyCycles.Length; i++)
            {
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
                Assert.AreEqual(trueMinActiveDutyCycles[i], mem.GetMinActiveDutyCycles()[i], 0.01);
            }

            ///////////////////////

            SetupParameters();
            parameters.SetInputDimensions(new int[] { 8 });
            parameters.SetColumnDimensions(new int[] { 8 });
            InitSp();


            returnIndex = 0;
            returnVals = new int[][]
            {
                new int[] {0, 1, 2, 3, 4},
                new int[] {1, 2, 3, 4, 5},
                new int[] {2, 3, 4, 6, 7},
                new int[] {0, 2, 4, 6},
                new int[] {1, 6},
                new int[] {3, 5, 7},
                new int[] {1, 4, 5, 6},
                new int[] {2, 3, 6, 7}
            };

            mockSP = new SpatialPoolerMockNeighborsND((c, columnIndex, topology, rad, wrapArnd) =>
            {
                return new List<int>(returnVals[returnIndex++]);
            });

            //            mockSP = new SpatialPooler()
            //{
            //    int returnIndex = 0;
            //            int[][] returnVals =  {
            //                    {0, 1, 2, 3, 4},
            //                    {1, 2, 3, 4, 5},
            //                    {2, 3, 4, 6, 7},
            //                    {0, 2, 4, 6},
            //                    {1, 6},
            //                    {3, 5, 7},
            //                    {1, 4, 5, 6},
            //                    {2, 3, 6, 7}};
            //            @Override
            //            public List<int> getNeighborsND(
            //                    Connections c, int columnIndex, SparseMatrix<?> topology, int radius, bool wrapAround)
            //        {
            //            return new List<int>(returnVals[returnIndex++]);
            //        }
            //    };

            mem.SetMinPctOverlapDutyCycles(0.01);
            mem.SetOverlapDutyCycles(new double[] { 1.2, 2.7, 0.9, 1.1, 4.3, 7.1, 2.3, 0.0 });
            trueMinOverlapDutyCycles = new double[] {
                0.01*4.3, 0.01*7.1, 0.01*4.3, 0.01*4.3,
                0.01*2.7, 0.01*7.1, 0.01*7.1, 0.01*2.3 };

            mem.SetMinPctActiveDutyCycles(0.03);
            mem.SetActiveDutyCycles(new double[] { 0.14, 0.25, 0.125, 0.33, 0.27, 0.11, 0.76, 0.31 });
            trueMinActiveDutyCycles = new double[] {
                0.03*0.33, 0.03*0.33, 0.03*0.76, 0.03*0.76,
                0.03*0.76, 0.03*0.33, 0.03*0.76, 0.03*0.76 };

            mins = new double[mem.GetNumColumns()];
            Arrays.Fill(mins, 0);
            mem.SetMinOverlapDutyCycles(mins);
            mem.SetMinActiveDutyCycles(Arrays.CopyOf(mins, mins.Length));
            mockSP.UpdateMinDutyCyclesLocal(mem);
            for (int i = 0; i < trueMinOverlapDutyCycles.Length; i++)
            {
                //    		System.out.Println(i + ") " + trueMinOverlapDutyCycles[i] + "  -  " +  mem.GetMinOverlapDutyCycles()[i]);
                //    		System.out.Println(i + ") " + trueMinActiveDutyCycles[i] + "  -  " +  mem.GetMinActiveDutyCycles()[i]);
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
                Assert.AreEqual(trueMinActiveDutyCycles[i], mem.GetMinActiveDutyCycles()[i], 0.01);
            }
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
                //    		System.out.Println(i + ") " + trueMinOverlapDutyCycles[i] + "  -  " +  mem.GetMinOverlapDutyCycles()[i]);
                //    		System.out.Println(i + ") " + trueMinActiveDutyCycles[i] + "  -  " +  mem.GetMinActiveDutyCycles()[i]);
                Assert.AreEqual(trueMinOverlapDutyCycles[i], mem.GetMinOverlapDutyCycles()[i], 0.01);
                Assert.AreEqual(trueMinActiveDutyCycles[i], mem.GetMinActiveDutyCycles()[i], 0.01);
            }
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
                new int[] {1, 1, 1, 1, 0, 0, 0, 0},
                new int[] {1, 0, 0, 0, 1, 1, 0, 1},
                new int[] {0, 0, 1, 0, 0, 0, 1, 0},
                new int[] {1, 0, 0, 0, 0, 0, 1, 0}
            };

            double[][] permanences = new double[][]
            {
                new double[] {0.200, 0.120, 0.090, 0.040, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.150, 0.000, 0.000, 0.000, 0.180, 0.120, 0.000, 0.450},
                new double[] {0.000, 0.000, 0.014, 0.000, 0.000, 0.000, 0.110, 0.000},
                new double[] {0.040, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000}
            };

            double[][] truePermanences = new double[][]
            {
                new double[] {0.300, 0.110, 0.080, 0.140, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.250, 0.000, 0.000, 0.000, 0.280, 0.110, 0.000, 0.440},
                new double[] {0.000, 0.000, 0.000, 0.000, 0.000, 0.000, 0.210, 0.000},
                new double[] {0.040, 0.000, 0.000, 0.000, 0.000, 0.000, 0.178, 0.000}
            };

            //    Condition <?> cond = new Condition.Adapter<Integer>()
            //    {
            //            public bool eval(int n)
            //{
            //    return n == 1;
            //}
            //        };
            Func<int, bool> cond = n => n == 1;
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], cond);
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
                new int[] {1, 1, 1, 0, 0, 0, 0, 0},
                new int[] {0, 1, 1, 1, 0, 0, 0, 0},
                new int[] {0, 0, 1, 1, 1, 0, 0, 0},
                new int[] {1, 0, 0, 0, 0, 0, 1, 0}
            };

            permanences = new double[][]
            {
                new double[] {0.200, 0.120, 0.090, 0.000, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.000, 0.017, 0.232, 0.400, 0.180, 0.120, 0.000, 0.450},
                new double[] {0.000, 0.000, 0.014, 0.051, 0.730, 0.000, 0.000, 0.000},
                new double[] {0.170, 0.000, 0.000, 0.000, 0.000, 0.000, 0.380, 0.000}
            };

            truePermanences = new double[][]
            {
                new double[] {0.300, 0.110, 0.080, 0.000, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.000, 0.000, 0.222, 0.500, 0.000, 0.000, 0.000, 0.000},
                new double[] {0.000, 0.000, 0.000, 0.151, 0.830, 0.000, 0.000, 0.000},
                new double[] {0.170, 0.000, 0.000, 0.000, 0.000, 0.000, 0.380, 0.000}
            };

            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                int[] indexes = ArrayUtils.Where(potentialPools[i], cond);
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

        public class SpatialPoolerMockConnectedSpanForColumnND : SpatialPooler
        {
            private readonly Func<Connections, int, double> _overrideFunc1;
            private readonly Func<Connections, double> _overrideFunc2;
            public SpatialPoolerMockConnectedSpanForColumnND(
                Func<Connections, int, double> overrideFunc1,
                Func<Connections, double> overrideFunc2)
            {
                _overrideFunc1 = overrideFunc1;
                _overrideFunc2 = overrideFunc2;
            }

            public override double AvgConnectedSpanForColumnND(Connections c, int columnIndex)
            {
                return _overrideFunc1(c, columnIndex);
            }

            public override double AvgColumnsPerInput(Connections c)
            {
                return _overrideFunc2(c);
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

            // ((3 * 4) - 1) / 2 => round up
            SpatialPoolerMockConnectedSpanForColumnND mock = new SpatialPoolerMockConnectedSpanForColumnND((c, columnIndex) =>
            {
                return 3;
            }, (c) =>
            {
                return 4;
            });
            //        SpatialPooler mock = new SpatialPooler()
            //        {
            //        public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 3;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 4;
            //    }
            //};
            mem.SetGlobalInhibition(false);
            sp = mock;
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(6, mem.GetInhibitionRadius());

            //Test clipping at 1.0
            mock = new SpatialPoolerMockConnectedSpanForColumnND((c, columnIndex) =>
            {
                return 0.5;
            }, (c) =>
            {
                return 1.2;
            });
            //        {
            //        public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 0.5;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 1.2;
            //    }
            //};
            mem.SetGlobalInhibition(false);
            sp = mock;
            sp.UpdateInhibitionRadius(mem);
            Assert.AreEqual(1, mem.GetInhibitionRadius());

            //Test rounding up
            mock = new SpatialPoolerMockConnectedSpanForColumnND((c, columnIndex) =>
            {
                return 2.4;
            }, (c) =>
            {
                return 2;
            });
            //    {
            //            public double avgConnectedSpanForColumnND(Connections c, int columnIndex)
            //    {
            //        return 2.4;
            //    }

            //    public double avgColumnsPerInput(Connections c)
            //    {
            //        return 2;
            //    }
            //};
            mem.SetGlobalInhibition(false);
            sp = mock;
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

        /**
         * As coded in the Python test
         */
        [TestMethod]
        public void TestGetNeighborsNd()
        {
            /////////////////////////////////////////
            //Tests from getNeighbors1D from Python unit test
            SetupParameters();
            int[] dimensions = new int[] { 8 };
            parameters.SetColumnDimensions(dimensions);
            parameters.SetInputDimensions(dimensions);
            InitSp();
            AbstractSparseBinaryMatrix sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            sbm.Set(new int[] { 2, 4 }, new int[] { 1, 1 }, true);
            int radius = 1;
            int columnIndex = 3;
            int[] mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            List<int> msk = new List<int>(mask);
            List<int> neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //////
            SetupParameters();
            dimensions = new int[] { 8 };
            parameters.SetInputDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            sbm.Set(new int[] { 1, 2, 4, 5 }, new int[] { 1, 1, 1, 1 }, true);
            radius = 2;
            columnIndex = 3;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //Wrap around
            SetupParameters();
            dimensions = new int[] { 8 };
            parameters.SetInputDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            sbm.Set(new int[] { 1, 2, 6, 7 }, new int[] { 1, 1, 1, 1 }, true);
            radius = 2;
            columnIndex = 0;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //Radius too big
            SetupParameters();
            dimensions = new int[] { 8 };
            parameters.SetInputDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            sbm.Set(new int[] { 0, 1, 2, 3, 4, 5, 7 }, new int[] { 1, 1, 1, 1, 1, 1, 1 }, true);
            radius = 20;
            columnIndex = 6;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //These are all the same tests from 2D
            SetupParameters();
            dimensions = new int[] { 6, 5 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            int[][] input = new int[][]
            {
                new int[] {0, 0, 0, 0, 0},
                new int[] {0, 0, 0, 0, 0},
                new int[] {0, 1, 1, 1, 0},
                new int[] {0, 1, 0, 1, 0},
                new int[] {0, 1, 1, 1, 0},
                new int[] {0, 0, 0, 0, 0}
            };
            for (int i = 0; i < input.Length; i++)
            {
                for (int j = 0; j < input[i].Length; j++)
                {
                    if (input[i][j] == 1)
                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), value: 1);
                }
            }
            radius = 1;
            columnIndex = 3 * 5 + 2;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            ////////
            SetupParameters();
            dimensions = new int[] { 6, 5 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            input = new int[][]
            {
                new int[] {0, 0, 0, 0, 0},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 0, 1, 1},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 1, 1, 1}
            };
            for (int i = 0; i < input.Length; i++)
            {
                for (int j = 0; j < input[i].Length; j++)
                {
                    if (input[i][j] == 1)
                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), value: 1);
                }
            }
            radius = 2;
            columnIndex = 3 * 5 + 2;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //Radius too big
            SetupParameters();
            dimensions = new int[] { 6, 5 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            input = new int[][]
            {
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 0, 1, 1},
                new int[] {1, 1, 1, 1, 1},
                new int[] {1, 1, 1, 1, 1}
            };
            for (int i = 0; i < input.Length; i++)
            {
                for (int j = 0; j < input[i].Length; j++)
                {
                    if (input[i][j] == 1)
                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), value: 1);
                }
            }
            radius = 7;
            columnIndex = 3 * 5 + 2;
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));

            //Wrap-around
            SetupParameters();
            dimensions = new int[] { 6, 5 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            sbm = (AbstractSparseBinaryMatrix)mem.GetInputMatrix();
            input = new int[][]
            {
               new int[] {1, 0, 0, 1, 1},
               new int[] {0, 0, 0, 0, 0},
               new int[] {0, 0, 0, 0, 0},
               new int[] {0, 0, 0, 0, 0},
               new int[] {1, 0, 0, 1, 1},
               new int[] {1, 0, 0, 1, 0}
            };
            for (int i = 0; i < input.Length; i++)
            {
                for (int j = 0; j < input[i].Length; j++)
                {
                    if (input[i][j] == 1)
                        sbm.Set(sbm.ComputeIndex(new int[] { i, j }), value: 1);
                }
            }
            radius = 1;
            columnIndex = sbm.GetMaxIndex();
            mask = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            msk = new List<int>(mask);
            neg = new List<int>(ArrayUtils.Range(0, dimensions[0]));
            neg.RemoveAll(p => msk.Contains(p));
            Assert.IsTrue(sbm.All(mask));
            Assert.IsFalse(sbm.Any(neg));
        }

        [TestMethod]
        public void TestGetNeighborsNd_Dim_9_5()
        {
            //This setup isn't relevant to this test
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 9, 5 });
            parameters.SetColumnDimensions(new int[] { 5, 5 });
            InitSp();
            ////////////////////// Test not part of Python port /////////////////////
            int[] result = sp.GetNeighborsND(mem, 2, mem.GetInputMatrix(), 3, true).ToArray();
            int[] expected = new int[]
            {
                0, 1, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12,
                13, 14, 15, 16, 17, 18, 19, 30, 31, 32, 33,
                34, 35, 36, 37, 38, 39, 40, 41, 42, 43, 44
            };
            for (int i = 0; i < result.Length; i++)
            {
                Assert.AreEqual(expected[i], result[i]);
            }
        }

        [TestMethod]
        public void TestGetNeighborsNd_Dim_5_7_2()
        {
            /////////////////////////////////////////////////////////////////////////
            SetupParameters();
            int[] dimensions = new int[] { 5, 7, 2 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            int radius = 1;
            int x = 1;
            int y = 3;
            int z = 2;
            int columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x });
            int[] neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            String expect = "[18, 19, 20, 21, 22, 23, 32, 33, 34, 36, 37, 46, 47, 48, 49, 50, 51]";
            Assert.AreEqual(expect, ArrayUtils.Print1DArray(neighbors));
        }

        [TestMethod]
        public void TestGetNeighborsNd_Dim_5_7_9()
        {
            /////////////////////////////////////////
            SetupParameters();
            int[] dimensions = new int[] { 5, 7, 9 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();
            int radius = 3;
            int x = 0;
            int y = 0;
            int z = 3;
            int columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x });
            int[] neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            string expect = "[0, 1, 2, 3, 6, 7, 8, 9, 10, 11, 12, 15, 16, 17, 18, 19, 20, 21, 24, 25, 26, "
                     + "27, 28, 29, 30, 33, 34, 35, 36, 37, 38, 39, 42, 43, 44, 45, 46, 47, 48, 51, "
                     + "52, 53, 54, 55, 56, 57, 60, 61, 62, 63, 64, 65, 66, 69, 70, 71, 72, 73, 74, "
                     + "75, 78, 79, 80, 81, 82, 83, 84, 87, 88, 89, 90, 91, 92, 93, 96, 97, 98, 99, "
                     + "100, 101, 102, 105, 106, 107, 108, 109, 110, 111, 114, 115, 116, 117, 118, 119, "
                     + "120, 123, 124, 125, 126, 127, 128, 129, 132, 133, 134, 135, 136, 137, 138, 141, "
                     + "142, 143, 144, 145, 146, 147, 150, 151, 152, 153, 154, 155, 156, 159, 160, 161, "
                     + "162, 163, 164, 165, 168, 169, 170, 171, 172, 173, 174, 177, 178, 179, 180, 181, "
                     + "182, 183, 186, 187, 188, 190, 191, 192, 195, 196, 197, 198, 199, 200, 201, 204, "
                     + "205, 206, 207, 208, 209, 210, 213, 214, 215, 216, 217, 218, 219, 222, 223, 224, "
                     + "225, 226, 227, 228, 231, 232, 233, 234, 235, 236, 237, 240, 241, 242, 243, 244, "
                     + "245, 246, 249, 250, 251, 252, 253, 254, 255, 258, 259, 260, 261, 262, 263, 264, "
                     + "267, 268, 269, 270, 271, 272, 273, 276, 277, 278, 279, 280, 281, 282, 285, 286, "
                     + "287, 288, 289, 290, 291, 294, 295, 296, 297, 298, 299, 300, 303, 304, 305, 306, "
                     + "307, 308, 309, 312, 313, 314]";
            Assert.AreEqual(expect, ArrayUtils.Print1DArray(neighbors));
        }

        //[TestMethod]
        public void TestGetNeighborsNd_Dim_5_10_7_6()
        {
            /////////////////////////////////////////
            SetupParameters();
            int[] dimensions = new int[] { 5, 10, 7, 6 };
            parameters.SetInputDimensions(dimensions);
            parameters.SetColumnDimensions(dimensions);
            InitSp();

            int radius = 4;
            int w = 2;
            int x = 5;
            int y = 6;
            int z = 2;
            int columnIndex = mem.GetInputMatrix().ComputeIndex(new int[] { z, y, x, w });
            int[] neighbors = sp.GetNeighborsND(mem, columnIndex, mem.GetInputMatrix(), radius, true).ToArray();
            HashSet<int> trueNeighbors = new HashSet<int>();
            for (int i = -radius; i <= radius; i++)
            {
                for (int j = -radius; j <= radius; j++)
                {
                    for (int k = -radius; k <= radius; k++)
                    {
                        for (int m = -radius; m <= radius; m++)
                        {
                            int zprime = (int)ArrayUtils.PositiveRemainder((z + i), dimensions[0]);
                            int yprime = (int)ArrayUtils.PositiveRemainder((y + j), dimensions[1]);
                            int xprime = (int)ArrayUtils.PositiveRemainder((x + k), dimensions[2]);
                            int wprime = (int)ArrayUtils.PositiveRemainder((w + m), dimensions[3]);
                            trueNeighbors.Add(mem.GetInputMatrix().ComputeIndex(new int[] { zprime, yprime, xprime, wprime }));
                        }
                    }
                }
            }
            trueNeighbors.Remove(columnIndex);
            int[] tneighbors = ArrayUtils.Unique(trueNeighbors.ToArray());
            Assert.AreEqual(ArrayUtils.Print1DArray(tneighbors), ArrayUtils.Print1DArray(neighbors));
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
            objMatrix.Set(0, new double[] { 0.0, 0.11, 0.095, 0.092, 0.01 });
            objMatrix.Set(1, new double[] { 0.12, 0.15, 0.02, 0.12, 0.09 });
            objMatrix.Set(2, new double[] { 0.51, 0.081, 0.025, 0.089, 0.31 });
            objMatrix.Set(3, new double[] { 0.18, 0.0601, 0.11, 0.011, 0.03 });
            objMatrix.Set(4, new double[] { 0.011, 0.011, 0.011, 0.011, 0.011 });
            mem.SetPermanences(objMatrix);

            //    	mem.SetConnectedSynapses(new SparseObjectMatrix<int[]>(new int[] { 5, 5 }));
            //    	SparseObjectMatrix<int[]> syns = mem.GetConnectedSynapses();
            //    	syns.Set(0, new int[] { 0, 1, 0, 0, 0 });
            //    	syns.Set(1, new int[] { 1, 1, 0, 1, 0 });
            //    	syns.Set(2, new int[] { 1, 0, 0, 0, 1 });
            //    	syns.Set(3, new int[] { 1, 0, 1, 0, 0 });
            //    	syns.Set(4, new int[] { 0, 0, 0, 0, 0 });

            mem.SetConnectedCounts(new int[] { 1, 3, 2, 2, 0 });

            double[][] truePermanences = new double[][]
            {
                new double[] {0.01, 0.12, 0.105, 0.102, 0.02},      // incremented once
                new double[] {0.12, 0.15, 0.02, 0.12, 0.09},        // no change
                new double[] {0.53, 0.101, 0.045, 0.109, 0.33},     // increment twice
                new double[] {0.22, 0.1001, 0.15, 0.051, 0.07},     // increment four times
                new double[] {0.101, 0.101, 0.101, 0.101, 0.101}    // increment 9 times
            };

            //FORGOT TO SET PERMANENCES ABOVE - DON'T USE mem.SetPermanences() 
            int[] indices = mem.GetMemory().GetSparseIndices();
            for (int i = 0; i < mem.GetNumColumns(); i++)
            {
                double[] perm = mem.GetPotentialPools().Get(i).GetSparsePermanences(); // reversed?
                sp.RaisePermanenceToThreshold(mem, perm, indices);
                Array.Reverse(perm);
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

            Debug.WriteLine("Initial true counts: " + Arrays.ToString(mem.GetConnectedCounts().GetTrueCounts()));

            double[][] permanences = new double[][]
            {
                new double[] {-0.10, 0.500, 0.400, 0.010, 0.020},
                new double[] {0.300, 0.010, 0.020, 0.120, 0.090},
                new double[] {0.070, 0.050, 1.030, 0.190, 0.060},
                new double[] {0.180, 0.090, 0.110, 0.010, 0.030},
                new double[] {0.200, 0.101, 0.050, -0.09, 1.100}
            };

            int[][] trueConnectedSynapses = new int[][]
            {
                new int[] {0, 1, 1, 0, 0},
                new int[] {1, 0, 0, 1, 0},
                new int[] {0, 0, 1, 1, 0},
                new int[] {1, 0, 1, 0, 0},
                new int[] {1, 1, 0, 0, 1}
            };

            int[][] connectedDense = new int[][]
            {
                new int[] {1, 2},
                new int[] {0, 3},
                new int[] {2, 3},
                new int[] {0, 2},
                new int[] {0, 1, 4}
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
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            InitSp();

            int[] dimensions = new int[] { 5, 10 };
            int[][] connectedSynapses = new int[][] {
          new int[]  {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}};
            AbstractSparseBinaryMatrix sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            int[] inputVector = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            int[] overlaps = sp.CalculateOverlap(mem, inputVector);
            int[] trueOverlaps = new int[5];
            double[] overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            double[] trueOverlapsPct = new double[5];
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            /////////////////

            dimensions = new int[] { 5, 10 };
            connectedSynapses = new int[][] {
          new int[]  {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}};
            sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 10, 8, 6, 4, 2 };
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 1, 1, 1, 1, 1 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            ///////////////////
            // test stimulsThreshold = 3
            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 3.0);
            InitSp();

            dimensions = new int[] { 5, 10 };
            connectedSynapses = new int[][] {
          new int[]  {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}};
            sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            inputVector = new int[] { 1, 1, 1, 1, 1, 1, 1, 1, 1, 1 };
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 10, 8, 6, 4, 0 }; // last gets squelched by stimulus threshold of 3
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 1, 1, 1, 1, 0 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            /////////////////

            parameters.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 1.0);
            InitSp();
            dimensions = new int[] { 5, 10 };
            connectedSynapses = new int[][] {
          new int[]  {1, 1, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 1, 1, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 1, 1, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 1, 1, 1, 1},
          new int[]  {0, 0, 0, 0, 0, 0, 0, 0, 1, 1}};
            sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
                }
            }

            inputVector = new int[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 };
            overlaps = sp.CalculateOverlap(mem, inputVector);
            trueOverlaps = new int[] { 1, 1, 1, 1, 1 };
            overlapsPct = sp.CalculateOverlapPct(mem, overlaps);
            trueOverlapsPct = new double[] { 0.1, 0.125, 1.0 / 6, 0.25, 0.5 };
            Assert.IsTrue(Arrays.AreEqual(trueOverlaps, overlaps));
            Assert.IsTrue(Arrays.AreEqual(trueOverlapsPct, overlapsPct));

            /////////////////
            // Zig-zag
            dimensions = new int[] { 5, 10 };
            connectedSynapses = new int[][] {
          new int[]  {1, 0, 0, 0, 0, 1, 0, 0, 0, 0},
          new int[]  {0, 1, 0, 0, 0, 0, 1, 0, 0, 0},
          new int[]  {0, 0, 1, 0, 0, 0, 0, 1, 0, 0},
          new int[]  {0, 0, 0, 1, 0, 0, 0, 0, 1, 0},
          new int[]  {0, 0, 0, 0, 1, 0, 0, 0, 0, 1}};
            sm = new SparseBinaryMatrix(dimensions);
            for (int i = 0; i < sm.GetDimensions()[0]; i++)
            {
                for (int j = 0; j < sm.GetDimensions()[1]; j++)
                {
                    sm.Set(connectedSynapses[i][j], i, j);
                }
            }

            mem.SetConnectedMatrix(sm);

            for (int i = 0; i < 5; i++)
            {
                for (int j = 0; j < 10; j++)
                {
                    Assert.AreEqual(connectedSynapses[i][j], sm.GetIntValue(i, j));
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
        public void TestInitPermanence()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetSynPermTrimThreshold(0);
            InitSp();

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

            SetupParameters();
            parameters.SetInputDimensions(new int[] { 100 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetSynPermTrimThreshold(0);
            InitSp();
            mem.SetPotentialRadius(100);
            connectedPct = 0.5;
            mask = new int[100];
            for (int i = 0; i < 100; i++) mask[i] = i;
            double[] perma = sp.InitPermanence(mem, mask, 0, connectedPct);
            numcon = ArrayUtils.ValueGreaterCount(mem.GetSynPermConnected(), perma);
            Assert.IsTrue(numcon > 0);
            Assert.IsTrue(numcon < mem.GetNumInputs());

            double minThresh = mem.GetSynPermActiveInc() / 2.0d;
            double connThresh = mem.GetSynPermConnected();

            double[] results = ArrayUtils.RetainLogicalAnd(perma,
                d => d >= minThresh,
                d => d < connThresh
                );
            //            double[] results = ArrayUtils.RetainLogicalAnd(perma, new Condition[] {
            //                new Condition.Adapter<Object>() {
            //                    public bool eval(double d)
            //        {
            //            return d >= minThresh;
            //        }
            //    },
            //                new Condition.Adapter<Object>() {
            //                    public bool eval(double d)
            //    {
            //        return d < connThresh;
            //    }
            //}
            //        });
            //double[] results = perma.Where(d => d >= minThresh && d < connThresh).ToArray();
            Assert.IsTrue(results.Length > 0);
        }

        public class SpatialPoolerRaisePermanenceToThresholdSparse : SpatialPooler
        {
            public override void RaisePermanenceToThresholdSparse(Connections c, double[] perm)
            {
                //Mocked to do nothing as per Python version of test
            }
        }

        /**
         * Test initial permanence generation. ensure that permanence values
         * are only assigned to bits within a column's potential pool. 
         */
        [TestMethod]
        public void TestInitPermanence2()
        {
            SetupParameters();
            parameters.SetInputDimensions(new int[] { 10 });
            parameters.SetColumnDimensions(new int[] { 5 });
            parameters.SetSynPermTrimThreshold(0);
            InitSp();

            sp = new SpatialPoolerRaisePermanenceToThresholdSparse();
            //    {
            //    public void raisePermanenceToThresholdSparse(Connections c, double[] perm)
            //{
            //    //Mocked to do nothing as per Python version of test
            //}
            //        };

            double connectedPct = 1;
            int[] mask = new int[] { 0, 1 };
            double[] perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            int[] trueConnected = new int[] { 0, 1 };
            //        Condition <?> cond = new Condition.Adapter<Object>()
            //        {
            //                        public bool eval(double d)
            //    {
            //        return d >= mem.GetSynPermConnected();
            //    }
            //};
            Func<double, bool> cond = d => d >= mem.GetSynPermConnected();

            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, cond)));// ArrayUtils.Where(perm, cond)));

            connectedPct = 1;
            mask = new int[] { 4, 5, 6 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 4, 5, 6 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, cond)));

            connectedPct = 1;
            mask = new int[] { 8, 9 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 8, 9 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, cond)));

            connectedPct = 1;
            mask = new int[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 };
            perm = sp.InitPermanence(mem, mask, 0, connectedPct);
            trueConnected = new int[] { 0, 1, 2, 3, 4, 5, 6, 8, 9 };
            Assert.IsTrue(Arrays.AreEqual(trueConnected, ArrayUtils.Where(perm, cond)));
        }
    }
}