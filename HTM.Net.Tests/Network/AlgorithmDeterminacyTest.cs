using System;
using System.Diagnostics;
using System.Reactive;
using System.Threading;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class AlgorithmDeterminacyTest
    {
        private static readonly int[][] TEST_AGGREGATION = new int[3][];

        private const int TM_EXPL = 0;
        private const int TM_LYR = 1;
        private const int TM_NAPI = 2;

        public static Parameters GetParameters()
        {
            Parameters parameters = Parameters.GetAllDefaultParameters();
            parameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 8 });
            parameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 20 });
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
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.1);
            parameters.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.1);
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

        [TestMethod]
        public void DoTest()
        {
            // Run the three tests again, after each other
            Debug.WriteLine("TestTemporalMemoryExplicit");
            TestTemporalMemoryExplicit();
            Debug.WriteLine("TestTemporalMemoryThroughLayer");
            TestTemporalMemoryThroughLayer();
            Debug.WriteLine("TestThreadedPublisher");
            TestThreadedPublisher();

            Console.WriteLine(Arrays.ToString(TEST_AGGREGATION[TM_EXPL]));
            Console.WriteLine(Arrays.ToString(TEST_AGGREGATION[TM_LYR]));
            Console.WriteLine(Arrays.ToString(TEST_AGGREGATION[TM_NAPI]));
            Assert.IsTrue(Arrays.AreEqual(TEST_AGGREGATION[TM_EXPL], TEST_AGGREGATION[TM_LYR]));
            Assert.IsTrue(Arrays.AreEqual(TEST_AGGREGATION[TM_EXPL], TEST_AGGREGATION[TM_NAPI]));
        }

        [TestMethod]
        public void TestTemporalMemoryExplicit()
        {
            int[] input1 = new int[] { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
            int[] input2 = new int[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
            int[] input3 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] input4 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input5 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input6 = new int[] { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
            int[] input7 = new int[] { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };

            Parameters p = GetParameters();
            Connections con = new Connections();
            p.Apply(con);
            TemporalMemory tm = new TemporalMemory();
            TemporalMemory.Init(con);

            ComputeCycle cc = null;
            for (int x = 0; x < 602; x++)
            {
                foreach (int[] i in inputs)
                {
                    cc = tm.Compute(con, ArrayUtils.Where(i, ArrayUtils.WHERE_1), true);
                }
            }

            TEST_AGGREGATION[TM_EXPL] = SDR.AsCellIndices(cc.activeCells);
        }

        [TestMethod]
        public void TestTemporalMemoryThroughLayer()
        {
            Parameters p = GetParameters();

            int[] input1 = new int[] { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
            int[] input2 = new int[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
            int[] input3 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] input4 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input5 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input6 = new int[] { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
            int[] input7 = new int[] { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };

            Layer<int[]> l = new Layer<int[]>(p, null, null, new TemporalMemory(), null, null);

            int timeUntilStable = 600;

            l.Subscribe(Observer.Create<IInference>(
                output => { }, Console.WriteLine, () => { }
            ));
            //    @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override public void onNext(Inference output) { }
            //});

            // Now push some warm up data through so that "onNext" is called above
            for (int j = 0; j < timeUntilStable; j++)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    l.Compute(inputs[i]);
                }
            }

            for (int j = 0; j < 2; j++)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    l.Compute(inputs[i]);
                }
            }

            ComputeCycle cc = l.GetInference().GetComputeCycle();
            TEST_AGGREGATION[TM_LYR] = SDR.AsCellIndices(cc.activeCells);
        }

        [TestMethod]
        public void TestThreadedPublisher()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B").Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            Parameters p = GetParameters();

            Map<String, Map<String, Object>> settings = NetworkTestHarness.SetupMap(
                            null, // map
                            20,    // n
                            0,    // w
                            0,    // min
                            0,    // max
                            0,    // radius
                            0,    // resolution
                            null, // periodic
                            null,                 // clip
                            true,         // forced
                            "dayOfWeek",          // fieldName
                            "darr",               // fieldType (dense array as opposed to sparse array or "sarr")
                            EncoderTypes.SDRPassThroughEncoder); // encoderType

            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .Add(new TemporalMemory())
                        .Add(sensor)));

            network.Start();

            network.Observe().Subscribe(Observer.Create<IInference>(
                output =>
                {
                },
                Console.WriteLine,
                () => { }
            ));

            int[] input1 = new int[] { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
            int[] input2 = new int[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
            int[] input3 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] input4 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input5 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input6 = new int[] { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
            int[] input7 = new int[] { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };

            // Now push some warm up data through so that "onNext" is called above
            int timeUntilStable = 602;
            for (int j = 0; j < timeUntilStable; j++)
            {
                for (int i = 0; i < inputs.Length; i++)
                {
                    manual.OnNext(Arrays.ToString(inputs[i]));
                }
            }

            manual.OnComplete();

            ILayer l = network.Lookup("r1").Lookup("1");
            try
            {
                l.GetLayerThread().Wait();
                ComputeCycle cc = l.GetInference().GetComputeCycle();
                TEST_AGGREGATION[TM_NAPI] = SDR.AsCellIndices(cc.activeCells);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.ToString());
                Assert.IsInstanceOfType(e, typeof(ThreadAbortException));
            }
        }

        [TestMethod]
        public void TestModelClasses()
        {
            //Test Segment equality
            Column column1 = new Column(2, 0);
            Cell cell1 = new Cell(column1, 0);
            Segment s1 = new DistalDendrite(cell1, 0, 1, 0);
            Assert.IsTrue(s1.Equals(s1)); // test ==
            Assert.IsFalse(s1.Equals(null));

            Segment s2 = new DistalDendrite(cell1, 0, 1, 0);
            Assert.IsTrue(s1.Equals(s2));

            Cell cell2 = new Cell(column1, 0);
            Segment s3 = new DistalDendrite(cell2, 0, 1, 0);
            Assert.IsTrue(s1.Equals(s3));

            //Segment's Cell has different index
            Cell cell3 = new Cell(column1, 1);
            Segment s4 = new DistalDendrite(cell3, 0, 1, 0);
            Assert.IsFalse(s1.Equals(s4));

            //Segment has different index
            Segment s5 = new DistalDendrite(cell3, 1, 1, 0);
            Assert.IsFalse(s4.Equals(s5));
            Assert.IsTrue(s5.ToString().Equals("1"));
            Assert.AreEqual(-1, s4.CompareTo(s5));
            Assert.AreEqual(1, s5.CompareTo(s4));

            //Different type of segment
            Segment s6 = new ProximalDendrite(0);
            Assert.IsFalse(s5.Equals(s6));

            Console.WriteLine(s4.CompareTo(s5));
        }
    }
}