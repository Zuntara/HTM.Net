using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using DeepEqual.Syntax;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Serialize;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class PersistanceApiTest
    {
        [ClassCleanup]
        public static void CleanUp()
        {
            Console.WriteLine("Cleaning Up...");
            try
            {
                DirectoryInfo serialDir = new DirectoryInfo(Environment.CurrentDirectory + "\\" + SerialConfig.SERIAL_TEST_DIR);
                if (serialDir.Exists)
                {
                    serialDir.Delete(true);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        [TestMethod]
        public void TestEnsurePathExists()
        {
            SerialConfig config = new SerialConfig("testEnsurePathExists", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI persist = Persistence.Get();
            persist.SetConfig(config);

            try
            {
                ((Persistence.PersistenceAccess)persist).EnsurePathExists(config);
            }
            catch (Exception e) { Assert.Fail(); }

            FileInfo f1 = new FileInfo(Environment.CurrentDirectory + "\\" + config.GetFileDir() + "\\" + "testEnsurePathExists");
            Assert.IsTrue(f1.Exists);
        }

        [TestMethod]
        public void TestSearchAndListPreviousCheckPoint()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())));

            IPersistenceAPI pa = Persistence.Get(new SerialConfig(null, SerialConfig.SERIAL_TEST_DIR));
            ArrayUtils.Range(0, 5).ToList().ForEach(i =>
                ((Persistence.PersistenceAccess)pa).GetCheckPointFunction<Net.Network.Network>(network)(network));

            Dictionary<string, DateTime> checkPointFiles = pa.ListCheckPointFiles();
            Assert.IsTrue(checkPointFiles.Count > 4);

            Assert.AreEqual(checkPointFiles.ElementAt(checkPointFiles.Count - 2).Key,
                pa.GetPreviousCheckPoint(checkPointFiles.ElementAt(checkPointFiles.Count - 1)));
        }

        [TestMethod]
        public void TestNetworkSerialisation()
        {
            Net.Network.Network n = new Net.Network.Network("network", Parameters.Empty());
            IPersistenceAPI pa = Persistence.Get(new SerialConfig(null, SerialConfig.SERIAL_TEST_DIR));
            byte[] bytes = pa.Serializer().Serialize(n);
            Net.Network.Network n2 = pa.Serializer().Deserialize<Net.Network.Network>(bytes);

            Assert.AreEqual(n, n2);

            // With parameters
            Parameters p = NetworkTestHarness.GetParameters();
            n = new Net.Network.Network("network", p);
            bytes = pa.Serializer().Serialize(n);
            n2 = pa.Serializer().Deserialize<Net.Network.Network>(bytes);

            Assert.AreEqual(n, n2);

            // With region
            n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())));

            bytes = pa.Serializer().Serialize(n);
            n2 = pa.Serializer().Deserialize<Net.Network.Network>(bytes);

            Assert.AreEqual(n, n2);
        }

        [TestMethod]
        public void TestLayerSerialisation()
        {
            Net.Network.Network n = new Net.Network.Network("network", Parameters.Empty());
            BaseLayer l = new Layer<int>("layer", n, Parameters.Empty());

            IPersistenceAPI pa = Persistence.Get(new SerialConfig(null, SerialConfig.SERIAL_TEST_DIR));
            byte[] bytes = pa.Serializer().Serialize(l);
            ILayer l2 = pa.Serializer().Deserialize<BaseLayer>(bytes);

            Assert.AreEqual(l, l2);
        }

        [TestMethod]
        public void TestRegionSerialisation()
        {
            Net.Network.Network n = new Net.Network.Network("network", Parameters.Empty());
            Region r = new Region("myRegion", n);

            IPersistenceAPI pa = Persistence.Get(new SerialConfig(null, SerialConfig.SERIAL_TEST_DIR));
            byte[] bytes = pa.Serializer().Serialize(r);
            Region r2 = pa.Serializer().Deserialize<Region>(bytes);

            Assert.AreEqual(r, r2);
        }

        ////////////////////////////////////////////////////////////////////////////
        //     First, Test Serialization of Each (Critical) Object Individually    //
        /////////////////////////////////////////////////////////////////////////////

        /////////////////////
        //    Parameters   //
        /////////////////////
        [TestMethod]
        public void TestSerializeParameters()
        {
            Parameters p = GetParameters();

            SerialConfig config = new SerialConfig("testSerializeParameters", SerialConfig.SERIAL_TEST_DIR);

            IPersistenceAPI api = Persistence.Get(config);

            // 1. serialize
            byte[] data = api.Write(p, "testSerializeParameters");

            // 2. deserialize
            Parameters serialized = api.Read<Parameters>(data);

            Assert.IsTrue(p.Keys().Count == serialized.Keys().Count);
            Assert.IsTrue(p.IsDeepEqual(serialized));
            foreach (Parameters.KEY k in p.Keys())
            {
                DeepCompare(serialized.GetParameterByKey(k), p.GetParameterByKey(k));
            }

            // 3. reify from file
            /////////////////////////////////////
            //  SHOW RETRIEVAL USING FILENAME  //
            /////////////////////////////////////
            Parameters fromFile = api.Read<Parameters>("testSerializeParameters");
            Assert.IsTrue(p.Keys().Count == fromFile.Keys().Count);
            Assert.IsTrue(p.IsDeepEqual(fromFile));
            foreach (Parameters.KEY k in p.Keys())
            {
                DeepCompare(fromFile.GetParameterByKey(k), p.GetParameterByKey(k));
            }
        }

        /////////////////////
        //   Connections   //
        /////////////////////
        [TestMethod]
        public void TestSerializeConnections()
        {
            Parameters p = GetParameters();
            Connections con = new Connections();
            p.Apply(con);

            TemporalMemory.Init(con);

            SerialConfig config = new SerialConfig("testSerializeConnections", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            // 1. serialize
            byte[] data = api.Write(con);

            // 2. deserialize
            Connections serialized = api.Read<Connections>(data);
            Assert.IsTrue(con.IsDeepEqual(serialized));

            serialized.PrintParameters();
            int cellCount = con.GetCellsPerColumn();
            for (int i = 0; i < con.GetNumColumns(); i++)
            {
                DeepCompare(con.GetColumn(i), serialized.GetColumn(i));
                for (int j = 0; j < cellCount; j++)
                {
                    Cell cell = serialized.GetCell(i * cellCount + j);
                    DeepCompare(con.GetCell(i * cellCount + j), cell);
                }
            }

            // 3. reify from file
            Connections fromFile = api.Read<Connections>(data);
            Assert.IsTrue(con.IsDeepEqual(fromFile));
            for (int i = 0; i < con.GetNumColumns(); i++)
            {
                DeepCompare(con.GetColumn(i), fromFile.GetColumn(i));
                for (int j = 0; j < cellCount; j++)
                {
                    Cell cell = fromFile.GetCell(i * cellCount + j);
                    DeepCompare(con.GetCell(i * cellCount + j), cell);
                }
            }
        }

        // Test Connections Serialization after running through TemporalMemory
        [TestMethod]
        public void TestThreadedPublisher_TemporalMemoryNetwork()
        {
            Net.Network.Network network = CreateAndRunTestTemporalMemoryNetwork();
            ILayer l = network.Lookup("r1").Lookup("1");
            Connections cn = l.GetConnections();

            SerialConfig config = new SerialConfig("testThreadedPublisher_TemporalMemoryNetwork", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(cn);
            Connections serializedConnections = api.Read<Connections>(bytes);

            Net.Network.Network network2 = CreateAndRunTestTemporalMemoryNetwork();
            ILayer l2 = network2.Lookup("r1").Lookup("1");
            Connections newCons = l2.GetConnections();

            newCons.ShouldDeepEqual(serializedConnections);

            bool b = newCons.IsDeepEqual(serializedConnections);
            DeepCompare(newCons, serializedConnections);
            Assert.IsTrue(b);
        }

        // Test Connections Serialization after running through SpatialPooler
        [TestMethod]
        public void TestThreadedPublisher_SpatialPoolerNetwork()
        {
            Net.Network.Network network = CreateAndRunTestSpatialPoolerNetwork(0, 6);
            ILayer l = network.Lookup("r1").Lookup("1");
            Connections cn = l.GetConnections();

            SerialConfig config = new SerialConfig("testThreadedPublisher_SpatialPoolerNetwork", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(cn);
            //Serialize above Connections for comparison with same run but unserialized below...
            Connections serializedConnections = api.Read<Connections>(bytes);

            Net.Network.Network network2 = CreateAndRunTestSpatialPoolerNetwork(0, 6);
            ILayer l2 = network2.Lookup("r1").Lookup("1");
            Connections newCons = l2.GetConnections();

            //Compare the two Connections (both serialized and regular runs) - should be equal
            bool b = newCons.IsDeepEqual(serializedConnections);
            DeepCompare(newCons, serializedConnections);
            Assert.IsTrue(b);
        }
        /////////////////////////// End Connections Serialization Testing //////////////////////////////////

        /////////////////////
        //    HTMSensor    //
        /////////////////////
        // Serialize HTMSensors though they'll probably be reconstituted rather than serialized
        [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
        public void TestHTMSensor_DaysOfWeek()
        {
            Object[] n = { "some name", ResourceLocator.Path("days-of-week.csv") };
            HTMSensor<FileInfo> sensor = (HTMSensor<FileInfo>)Sensor<FileInfo>.Create(
                FileSensor.Create, SensorParams.Create(SensorParams.Keys.Path, n));

            Parameters p = GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            sensor.InitEncoder(p);

            SerialConfig config = new SerialConfig("testHTMSensor_DaysOfWeek", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(sensor);
            HTMSensor<FileInfo> serializedSensor = api.Read<HTMSensor<FileInfo>>(bytes);

            bool b = serializedSensor.IsDeepEqual(sensor);
            DeepCompare(serializedSensor, sensor);
            Assert.IsTrue(b);
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestHTMSensor_HotGym()
        {
            Object[] n = { "some name", ResourceLocator.Path("rec-center-hourly-small.csv") };
            HTMSensor<FileInfo> sensor = (HTMSensor<FileInfo>)Sensor<FileInfo>.Create(
                FileSensor.Create, SensorParams.Create(SensorParams.Keys.Path, n));

            sensor.InitEncoder(GetTestEncoderParams());

            SerialConfig config = new SerialConfig("testHTMSensor_HotGym");
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(sensor);
            Assert.IsNotNull(bytes);
            Assert.IsTrue(bytes.Length > 0);
            HTMSensor<FileInfo> serializedSensor = api.Read<HTMSensor<FileInfo>>(bytes);

            bool b = serializedSensor.IsDeepEqual(sensor);
            DeepCompare(serializedSensor, sensor);
            Assert.IsTrue(b);
        }

        [TestMethod]
        public void TestSerializeObservableSensor()
        {
            Publisher supplier = Publisher.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B").Build();

            ObservableSensor<string[]> oSensor = new ObservableSensor<string[]>(
                SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", supplier }));

            SerialConfig config = new SerialConfig("testSerializeObservableSensor", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(oSensor);
            ObservableSensor<string[]> serializedOSensor = api.Read<ObservableSensor<string[]>>(bytes);

            bool b = serializedOSensor.IsDeepEqual(oSensor);
            DeepCompare(serializedOSensor, oSensor);
            Assert.IsTrue(b);
        }
        //////////////////////////////////End HTMSensors ////////////////////////////////////

        /////////////////////
        //    Anomaly      //
        /////////////////////
        // Serialize Anomaly, AnomalyLikelihood and its support classes
        [TestMethod]
        public void TestSerializeAnomaly()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Anomaly anomalyComputer = Anomaly.Create(@params);

            // Serialize the Anomaly Computer without errors
            SerialConfig config = new SerialConfig("testSerializeAnomaly1", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            byte[] bytes = api.Write(anomalyComputer);

            double score = anomalyComputer.Compute(new int[0], new int[0], 0, 0);

            score = anomalyComputer.Compute(new int[0], new int[0], 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = anomalyComputer.Compute(new int[0], new int[] { 3, 5 }, 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = anomalyComputer.Compute(new int[] { 3, 5, 7 }, new int[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = anomalyComputer.Compute(new int[] { 2, 3, 6 }, new int[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(2.0 / 3.0, score, 0);

            // Deserialize the Anomaly Computer and make sure its usable (same tests as AnomalyTest.java)
            Anomaly serializedAnomalyComputer = api.Read<Anomaly>(bytes);
            score = serializedAnomalyComputer.Compute(new int[0], new int[0], 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = serializedAnomalyComputer.Compute(new int[0], new int[] { 3, 5 }, 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = serializedAnomalyComputer.Compute(new int[] { 3, 5, 7 }, new int[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(0.0, score, 0);

            score = serializedAnomalyComputer.Compute(new int[] { 2, 3, 6 }, new int[] { 3, 5, 7 }, 0, 0);
            Assert.AreEqual(2.0 / 3.0, score, 0);
        }


        [TestMethod]
        public void TestSerializeCumulativeAnomaly()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);

            Anomaly anomalyComputer = Anomaly.Create(@params);

            // Serialize the Anomaly Computer without errors
            SerialConfig config = new SerialConfig("testSerializeCumulativeAnomaly", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            byte[] bytes = api.Write(anomalyComputer);

            // Deserialize the Anomaly Computer and make sure its usable (same tests as AnomalyTest.java)
            Anomaly serializedAnomalyComputer = api.Read<Anomaly>(bytes);
            Assert.IsNotNull(serializedAnomalyComputer);

            Object[] predicted =
            {
                new int[] {1, 2, 6}, new int[] {1, 2, 6}, new int[] {1, 2, 6},
                new int[] {1, 2, 6}, new int[] {1, 2, 6}, new int[] {1, 2, 6},
                new int[] {1, 2, 6}, new int[] {1, 2, 6}, new int[] {1, 2, 6}
            };
            Object[] actual =
            {
                new int[] {1, 2, 6}, new int[] {1, 2, 6}, new int[] {1, 4, 6},
                new int[] {10, 11, 6}, new int[] {10, 11, 12}, new int[] {10, 11, 12},
                new int[] {10, 11, 12}, new int[] {1, 2, 6}, new int[] {1, 2, 6}
            };

            double[] anomalyExpected = { 0.0, 0.0, 1.0 / 9.0, 3.0 / 9.0, 2.0 / 3.0, 8.0 / 9.0, 1.0, 2.0 / 3.0, 1.0 / 3.0 };
            for (int i = 0; i < 9; i++)
            {
                double score = serializedAnomalyComputer.Compute((int[])actual[i], (int[])predicted[i], 0, 0);
                Assert.AreEqual(anomalyExpected[i], score, 0.01);
            }
        }

        private Parameters GetTestEncoderParams()
        {
            Map<String, Map<String, Object>> fieldEncodings = SetupMap(
                null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");

            fieldEncodings = SetupMap(
                fieldEncodings,
                25,
                3,
                0, 0, 0, 0.1, null, null, null,
                "consumption", "float", "RandomDistributedScalarEncoder");

            fieldEncodings.Get("timestamp").Add(Parameters.KEY.DATEFIELD_DOFW.GetFieldName(), new Tuple(1, 1.0)); // Day of week
            fieldEncodings.Get("timestamp").Add(Parameters.KEY.DATEFIELD_TOFD.GetFieldName(), new Tuple(5, 4.0)); // Time of day
            fieldEncodings.Get("timestamp").Add(Parameters.KEY.DATEFIELD_PATTERN.GetFieldName(), "MM/dd/YY HH:mm");

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        private Map<String, Map<String, Object>> SetupMap(
            Map<String, Map<String, Object>> map,
            int n, int w, double min, double max, double radius, double resolution, bool? periodic,
            bool? clip, bool? forced, String fieldName, String fieldType, String encoderType)
        {

            if (map == null)
            {
                map = new Map<String, Map<String, Object>>();
            }
            Map<String, Object> inner = null;
            if ((inner = map.Get(fieldName)) == null)
            {
                map.Add(fieldName, inner = new Map<String, Object>());
            }

            inner.Add("n", n);
            inner.Add("w", w);
            inner.Add("minVal", min);
            inner.Add("maxVal", max);
            inner.Add("radius", radius);
            inner.Add("resolution", resolution);

            if (periodic != null) inner.Add("periodic", periodic);
            if (clip != null) inner.Add("clip", clip);
            if (forced != null) inner.Add("forced", forced);
            if (fieldName != null) inner.Add("fieldName", fieldName);
            if (fieldType != null) inner.Add("fieldType", fieldType);
            if (encoderType != null) inner.Add("encoderType", encoderType);

            return map;
        }

        private Net.Network.Network CreateAndRunTestSpatialPoolerNetwork(int start, int runTo)
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B")
                .Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<String[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Map<String, Map<String, Object>> settings = NetworkTestHarness.SetupMap(
                null, // map
                8,   // n
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
                "SDRPassThroughEncoder"); // encoderType

            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .Add(new SpatialPooler())
                        .Add(sensor)));

            network.Start();

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            int[] expected0 = new int[] { 1, 1, 1, 0, 1, 1, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0 };
            int[] expected1 = new int[] { 1, 1, 1, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0 };
            int[] expected2 = new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1 };
            int[] expected3 = new int[] { 0, 0, 1, 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 1, 0 };
            int[] expected4 = new int[] { 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0 };
            int[] expected5 = new int[] { 0, 0, 1, 1, 1, 1, 0, 0, 1, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0 };
            int[] expected6 = new int[] { 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
            int[][] expecteds = new int[][] { expected0, expected1, expected2, expected3, expected4, expected5, expected6 };

            //TestObserver<Inference> tester;
            int test = 0;
            network.Observe().Subscribe(
                spatialPoolerOutput =>
                {
                    Console.WriteLine("expected: " + Arrays.ToString(expecteds[test]) + "  --  "
                        + "actual: " + Arrays.ToString(spatialPoolerOutput.GetSdr()));
                    Assert.IsTrue(Arrays.AreEqual(expecteds[test++], spatialPoolerOutput.GetSdr()));
                },
                e =>
                {
                    Console.WriteLine(e);
                },
                () =>
                {

                });


            // Now push some fake data through so that "onNext" is called above
            for (int i = start; i <= runTo; i++)
            {
                manual.OnNext(Arrays.ToString(inputs[i]));
            }

            manual.OnComplete();

            try
            {
                network.Lookup("r1").Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e) { Console.WriteLine(e); }


            //checkObserver(tester);

            return network;
        }

        private Net.Network.Network CreateAndRunTestTemporalMemoryNetwork()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B").Build();

            Sensor<ObservableSensor<String[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            Parameters p = GetParameters();

            Map<String, Map<String, Object>> settings = NetworkTestHarness.SetupMap(
                null, // map
                20,   // n
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
                "SDRPassThroughEncoder"); // encoderType

            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .Add(new TemporalMemory())
                        .Add(sensor)));

            network.Start();

            network.Observe().Subscribe(i => { }, e => Console.WriteLine(e));
            //            new Subscriber<Inference>() {
            //        @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override public void onNext(Inference i) { }
            //});

            int[] input1 = new int[] { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
            int[] input2 = new int[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
            int[] input3 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] input4 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input5 = new int[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
            int[] input6 = new int[] { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
            int[] input7 = new int[] { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
            int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };

            // Run until TemporalMemory is "warmed up".
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

                Console.WriteLine(Arrays.ToString(SDR.AsCellIndices(l.GetConnections().GetActiveCells())));

            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(ThreadInterruptedException), e.GetType());
            }

            return network;
        }

        private Parameters GetParameters()
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

            //Temporal Memory specific
            parameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.2);
            parameters.SetParameterByKey(Parameters.KEY.CONNECTED_PERMANENCE, 0.8);
            parameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 5);
            parameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 6);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.05);
            parameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 4);
            parameters.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            return parameters;
        }

        private void DeepCompare(object getParameterByKey, object p1)
        {
            getParameterByKey.ShouldDeepEqual(p1);
        }
    }
}