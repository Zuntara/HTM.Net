using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using DeepEqual.Syntax;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Serialize;
using HTM.Net.Tests.Algorithms;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class PersistanceApiTest
    {
        /** Printer to visualize DayOfWeek printouts - SET TO TRUE FOR PRINTOUT */
        private Func<IInference, int, int> dayOfWeekPrintout = CreateDayOfWeekInferencePrintout(false);

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
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            SerialConfig config = new SerialConfig("testSerializeParameters", SerialConfig.SERIAL_TEST_DIR);

            IPersistenceAPI api = Persistence.Get(config);

            // 1. serialize
            byte[] data = api.Write(p, "testSerializeParameters");

            // 2. deserialize
            Parameters serialized = api.ReadContent<Parameters>(data);

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
            Connections serialized = api.ReadContent<Connections>(data);
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
            Connections fromFile = api.ReadContent<Connections>(data);
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
            Connections serializedConnections = api.ReadContent<Connections>(bytes);

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
            Connections serializedConnections = api.ReadContent<Connections>(bytes);

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
            HTMSensor<FileInfo> serializedSensor = api.ReadContent<HTMSensor<FileInfo>>(bytes);

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
            HTMSensor<FileInfo> serializedSensor = api.ReadContent<HTMSensor<FileInfo>>(bytes);

            bool b = serializedSensor.IsDeepEqual(sensor);
            DeepCompare(serializedSensor, sensor);
            Assert.IsTrue(b);
        }

        [TestMethod]
        public void TestSerializeObservableSensor()
        {
            PublisherSupplier supplier = PublisherSupplier.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B").Build();

            ObservableSensor<string[]> oSensor = new ObservableSensor<string[]>(
                SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", supplier }));

            SerialConfig config = new SerialConfig("testSerializeObservableSensor", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(oSensor);
            ObservableSensor<string[]> serializedOSensor = api.ReadContent<ObservableSensor<string[]>>(bytes);

            bool b = serializedOSensor.IsDeepEqual(oSensor);
            DeepCompare(serializedOSensor, oSensor);
            Assert.IsTrue(b);
        }

        [TestMethod]
        public void TestSerializeObservableSensor_Other_Init()
        {
            PublisherSupplier supplier = PublisherSupplier.GetBuilder()
                                                          .AddHeader("dayOfWeek")
                                                          .AddHeader("float")
                                                          .AddHeader("B").Build();

            SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, "name", supplier);
            Sensor<ObservableSensor<string[]>> oSensor = Sensor<ObservableSensor<string[]>>
                .Create(ObservableSensor<string[]>.Create, parms);

            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new UniversalRandomSource(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("dayOfWeek", typeof(CLAClassifier)));

            oSensor.InitEncoder(p);

            SerialConfig config = new SerialConfig("testSerializeObservableSensor", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            byte[] bytes = api.Write(oSensor);
            Sensor<ObservableSensor<string[]>> serializedOSensor = api.ReadContent<Sensor<ObservableSensor<string[]>>>(bytes);

            bool b = serializedOSensor.IsDeepEqual(oSensor);
            DeepCompare(serializedOSensor, oSensor);
            Assert.IsTrue(b);
        }

        [TestMethod]
        public void TestSerializeObservableSensor2()
        {
            PublisherSupplier supplier = PublisherSupplier.GetBuilder()
                                                          .AddHeader("consumption")
                                                          .AddHeader("float")
                                                          .AddHeader("B").Build();

            SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, "", supplier);
            Sensor<ObservableSensor<string[]>> oSensor = Sensor<ObservableSensor<string[]>>
                .Create(ObservableSensor<string[]>.Create, parms);

            SerialConfig config = new SerialConfig("testSerializeObservableSensor2", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new UniversalRandomSource(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            p.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 4 });
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 4 });
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 2);

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                                             .Add(Net.Network.Network.CreateRegion("r1")
                                                     .Add(Net.Network.Network.CreateLayer("2", p)
                                                             .Add(Anomaly.Create())
                                                             .Add(new TemporalMemory()))
                                                     .Add(Net.Network.Network.CreateLayer("3", p)
                                                             .Add(new SpatialPooler()))
                                                     .Connect("2", "3"))
                                             .Add(Net.Network.Network.CreateRegion("r2")
                                                     .Add(Net.Network.Network.CreateLayer("1", p)
                                                             .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                                                             .Add(new TemporalMemory())
                                                             .Add(new SpatialPooler())
                                                             .Add(oSensor)))
                                             .Connect("r1", "r2");

            api.Store(network);

            var loaded = api.Load("testSerializeObservableSensor2");
            
            Sensor<ObservableSensor<string[]>> serializedOSensor = (Sensor<ObservableSensor<string[]>>)loaded.GetSensor();

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
            Anomaly serializedAnomalyComputer = api.ReadContent<Anomaly>(bytes);
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
            Anomaly serializedAnomalyComputer = api.ReadContent<Anomaly>(bytes);
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


        [TestMethod]
        public void TestSerializeAnomalyLikelihood()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);

            AnomalyLikelihood an = (AnomalyLikelihood)Anomaly.Create(@params);

            // Serialize the Anomaly Computer without errors
            SerialConfig config = new SerialConfig("testSerializeAnomalyLikelihood", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            byte[] bytes = api.Write(an);

            // Deserialize the Anomaly Computer and make sure its usable (same tests as AnomalyTest.java)
            Anomaly serializedAn = api.ReadContent<Anomaly>(bytes);
            Assert.IsNotNull(serializedAn);
        }

        [TestMethod]
        public void TestSerializeAnomalyLikelihoodForUpdates()
        {
            Parameters @params = Parameters.Empty();
            @params.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);

            AnomalyLikelihood an = (AnomalyLikelihood)Anomaly.Create(@params);

            // Serialize the Anomaly Computer without errors
            SerialConfig config = new SerialConfig("testSerializeAnomalyLikelihood", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            byte[] bytes = api.Write(an);

            // Deserialize the Anomaly Computer and make sure its usable (same tests as AnomalyTest.java)
            AnomalyLikelihood serializedAn = api.ReadContent<AnomalyLikelihood>(bytes);
            Assert.IsNotNull(serializedAn);

            //----------------------------------------
            // Step 1. Generate an initial estimate using fake distribution of anomaly scores.
            List<Sample> data1 = AnomalyLikelihoodTest.GenerateSampleData(0.2, 0.2, 0.2, 0.2).Take(1000).ToList();
            AnomalyLikelihoodMetrics metrics1 = serializedAn.EstimateAnomalyLikelihoods(data1, 5, 0);

            //----------------------------------------
            // Step 2. Generate some new data with a higher average anomaly
            // score. Using the estimator from step 1, to compute likelihoods. Now we
            // should see a lot more anomalies.
            List<Sample> data2 = AnomalyLikelihoodTest.GenerateSampleData(0.6, 0.2, 0.2, 0.2).Take(300).ToList();
            AnomalyLikelihoodMetrics metrics2 = serializedAn.UpdateAnomalyLikelihoods(data2, metrics1.GetParams());

            // Serialize the Metrics too just to be sure everything can be serialized
            SerialConfig metricsConfig = new SerialConfig("testSerializeMetrics", SerialConfig.SERIAL_TEST_DIR);
            api = Persistence.Get(metricsConfig);
            api.Write(metrics2);

            // Deserialize the Metrics
            AnomalyLikelihoodMetrics serializedMetrics = api.Read<AnomalyLikelihoodMetrics>();
            Assert.IsNotNull(serializedMetrics);

            Assert.AreEqual(serializedMetrics.GetLikelihoods().Length, data2.Count);
            Assert.AreEqual(serializedMetrics.GetAvgRecordList().Count, data2.Count);
            Assert.IsTrue(serializedAn.IsValidEstimatorParams(serializedMetrics.GetParams()));

            // The new running total should be different
            Assert.IsFalse(metrics1.GetAvgRecordList().Total == serializedMetrics.GetAvgRecordList().Total);

            // We should have many more samples where likelihood is < 0.01, but not all
            int conditionCount = ArrayUtils.Where(serializedMetrics.GetLikelihoods(), d => d < 0.1).Length;
            Assert.IsTrue(conditionCount >= 25);
            Assert.IsTrue(conditionCount <= 250);
        }
        ///////////////////////   End Serialize Anomaly //////////////////////////

        ///////////////////////////
        //      CLAClassifier    //
        ///////////////////////////
        // Test Serialize CLAClassifier
        [TestMethod]
        public void TestSerializeCLAClassifier()
        {
            CLAClassifier classifier = new CLAClassifier(new int[] { 1 }, 0.1, 0.1, 0);
            int recordNum = 0;
            Map<String, Object> classification = new Map<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            IClassification<double> result = classifier.Compute<double>(recordNum, classification, new int[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 41.7);
            result = classifier.Compute<double>(recordNum, classification, new int[] { 0, 6, 9, 11 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 44.9);
            result = classifier.Compute<double>(recordNum, classification, new int[] { 6, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 42.9);
            result = classifier.Compute<double>(recordNum, classification, new int[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            // Serialize the Metrics too just to be sure everything can be serialized
            SerialConfig config = new SerialConfig("testSerializeCLAClassifier", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            api.Write(classifier);

            // Deserialize the Metrics
            CLAClassifier serializedClassifier = api.Read<CLAClassifier>();
            Assert.IsNotNull(serializedClassifier);

            //Using the deserialized classifier, continue test
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            result = serializedClassifier.Compute<double>(recordNum, classification, new int[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            Assert.IsTrue(Arrays.AreEqual(new int[] { 1 }, result.StepSet()));
            Assert.AreEqual(35.520000457763672, result.GetActualValue(4), 0.00001);
            Assert.AreEqual(42.020000457763672, result.GetActualValue(5), 0.00001);
            Assert.AreEqual(6, result.GetStatCount(1));
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 3), 0.00001);
            Assert.AreEqual(0.12300123, result.GetStat(1, 4), 0.00001);
            Assert.AreEqual(0.87699877, result.GetStat(1, 5), 0.00001);
        }
        ////////////////////////  End CLAClassifier ///////////////////////

        ///////////////////////////
        //         Layers        //
        ///////////////////////////
        // Serialize a Layer
        [TestMethod]
        public void TestSerializeLayer()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("dayOfWeek", typeof(CLAClassifier)));

            Map<String, Map<String, Object>> settings = NetworkTestHarness.SetupMap(
                null, // map
                8,    // n
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

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] {"name",
                PublisherSupplier.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("darr")
                .AddHeader("B").Build() }));

            ILayer layer = Net.Network.Network.CreateLayer("1", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(new SpatialPooler())
                .Add(sensor);

            //        Observer obs = new Observer<IInference>() {
            //        @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override
            //        public void onNext(Inference spatialPoolerOutput)
            //    {
            //        System.out.println("in onNext()");
            //    }
            //};

            var obs = Observer.Create<IInference>(
                spatialPoolerOutput => { Console.WriteLine("in onNext()"); },
                e => Console.WriteLine(e),
                () => { });

            layer.Subscribe(obs);
            layer.Close();

            SerialConfig config = new SerialConfig("testSerializeLayer", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            api.Write(layer);

            //Serialize above Connections for comparison with same run but unserialized below...
            ILayer serializedLayer = api.Read<ILayer>();
            Assert.AreEqual(serializedLayer, layer);
            DeepCompare(layer, serializedLayer);

            // Now change one attribute and see that they are not equal
            serializedLayer.ResetRecordNum();
            Assert.AreNotEqual(serializedLayer, layer);
        }
        //////////////////////  End Layers  ///////////////////////

        ///////////////////////////
        //      Full Network     //
        ///////////////////////////
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.csv")]
        public void TestHierarchicalNetwork()
        {
            Net.Network.Network network = GetLoadedHotGymHierarchy();
            try
            {
                SerialConfig config = new SerialConfig("testSerializeHierarchy", SerialConfig.SERIAL_TEST_DIR);
                IPersistenceAPI api = Persistence.Get(config);
                api.Store(network);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail(e.ToString());
            }
        }

        /**
     * Test that a serialized/de-serialized {@link Network} can be run...
     */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.csv")]
        public void TestSerializedUnStartedNetworkRuns()
        {
            const int NUM_CYCLES = 600;
            const int INPUT_GROUP_COUNT = 7; // Days of Week

            Net.Network.Network network = GetLoadedDayOfWeekNetwork();

            SerialConfig config = new SerialConfig("testSerializedUnStartedNetworkRuns", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);
            api.Store(network);

            //Serialize above Connections for comparison with same run but unserialized below...
            Net.Network.Network serializedNetwork = api.Load();
            Assert.AreEqual(serializedNetwork, network);
            DeepCompare(network, serializedNetwork);

            int cellsPerCol = (int)serializedNetwork.GetParameters().GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN);

            serializedNetwork.Observe().Subscribe(
                inf => { dayOfWeekPrintout(inf, cellsPerCol); },
                e => { Console.WriteLine(e); },
                () => { });
            //            new Observer<Inference>() {
            //        @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override
            //        public void onNext(Inference inf)
            //    {
            //        /** see {@link #createDayOfWeekInferencePrintout()} */
            //        dayOfWeekPrintout.apply(inf, cellsPerCol);
            //    }
            //});

            Publisher pub = serializedNetwork.GetPublisher();

            serializedNetwork.Start();

            int cycleCount = 0;
            for (; cycleCount < NUM_CYCLES; cycleCount++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    pub.OnNext("" + j);
                }

                serializedNetwork.Reset();

                if (cycleCount == 284)
                {
                    break;
                }
            }

            pub.OnComplete();

            try
            {
                Region r1 = serializedNetwork.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        private Net.Network.Network GetLoadedDayOfWeekNetwork()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("dayOfWeek", typeof(CLAClassifier)));

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new object[] {"name",
                PublisherSupplier.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("number")
                .AddHeader("B").Build() }));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                .Add(Net.Network.Network.CreateLayer("1", p)
                    .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                    .Add(Anomaly.Create())
                    .Add(new TemporalMemory())
                    .Add(new SpatialPooler())
                    .Add(sensor)));

            return network;
        }

        private Net.Network.Network GetLoadedHotGymHierarchy()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("2", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer("3", p)
                        .Add(new SpatialPooler()))
                    .Connect("2", "3"))
                .Add(Net.Network.Network.CreateRegion("r2")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(FileSensor.Create(Net.Network.Sensor.FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly.csv"))))))
                .Connect("r1", "r2");

            return network;
        }

        private Parameters GetTestEncoderParams()
        {
            Map<String, Map<String, Object>> fieldEncodings = SetupMap(
                null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", EncoderTypes.DateEncoder);

            fieldEncodings = SetupMap(
                fieldEncodings,
                25,
                3,
                0, 0, 0, 0.1, null, null, null,
                "consumption", "float", EncoderTypes.RandomDistributedScalarEncoder);

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
            bool? clip, bool? forced, String fieldName, String fieldType, EncoderTypes encoderType)
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
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

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
                EncoderTypes.SDRPassThroughEncoder); // encoderType

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

            int[] expected0 = new int[] { 0, 1, 1, 1, 0, 0, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0 };
            int[] expected1 = new int[] { 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 1 };
            int[] expected2 = new int[] { 1, 1, 1, 0, 0, 0, 0, 1, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1 };
            int[] expected3 = new int[] { 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0 };
            int[] expected4 = new int[] { 1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            int[] expected5 = new int[] { 1, 1, 1, 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            int[] expected6 = new int[] { 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1 };
            int[][] expecteds = new int[][] { expected0, expected1, expected2, expected3, expected4, expected5, expected6 };

            //TestObserver<Inference> tester;
            int test = 0;
            network.Observe().Subscribe(
                spatialPoolerOutput =>
                {
                    Console.WriteLine(test + " E: " + Arrays.ToString(expecteds[test]) + "  --  "
                        + "A: " + Arrays.ToString(spatialPoolerOutput.GetSdr()));
                    Assert.IsTrue(Arrays.AreEqual(expecteds[test], spatialPoolerOutput.GetSdr()));
                    test++;
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
                EncoderTypes.SDRPassThroughEncoder); // encoderType

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

        private static Func<IInference, int, int> CreateDayOfWeekInferencePrintout(bool on)
        {
            int cycles = 1;
            return (IInference inf, int cellsPerColumn) =>
                {

                    IClassification<Object> result = inf.GetClassification("dayOfWeek");
                    double day = MapToInputData((int[])inf.GetLayerInput());
                    if (day == 1.0)
                    {
                        if (on)
                        {
                            Console.WriteLine("\n=========================");
                            Console.WriteLine("CYCLE: " + cycles);
                        }
                        cycles++;
                    }

                    if (on)
                    {
                        Console.WriteLine("RECORD_NUM: " + inf.GetRecordNum());
                        Console.WriteLine("ScalarEncoder Input = " + day);
                        Console.WriteLine("ScalarEncoder Output = " + Arrays.ToString(inf.GetEncoding()));
                        Console.WriteLine("SpatialPooler Output = " + Arrays.ToString(inf.GetFeedForwardActiveColumns()));

                        if (inf.GetPreviousPredictiveCells() != null)
                            Console.WriteLine("TemporalMemory Previous Prediction = " +
                            Arrays.ToString(SDR.CellsAsColumnIndices(inf.GetPreviousPredictiveCells(), cellsPerColumn)));

                        Console.WriteLine("TemporalMemory Actives = " + Arrays.ToString(SDR.AsColumnIndices(inf.GetSdr(), cellsPerColumn)));

                        Console.Write("CLAClassifier prediction = " +
                        result.GetMostProbableValue(1) + " --> " + result.GetMostProbableValue(1));

                        Console.WriteLine("  |  CLAClassifier 1 step prob = " + Arrays.ToString(result.GetStats(1)) + "\n");
                    }
                    return cycles;
                };
        }

        private static double MapToInputData(int[] encoding)
        {
            for (int i = 0; i < DayMap.Length; i++)
            {
                if (Arrays.AreEqual(encoding, DayMap[i]))
                {
                    return i + 1;
                }
            }
            return -1;
        }

        private static int[][] DayMap = new int[][]
        {
            new int[] {1, 1, 0, 0, 0, 0, 0, 1},
            new int[] {1, 1, 1, 0, 0, 0, 0, 0},
            new int[] {0, 1, 1, 1, 0, 0, 0, 0},
            new int[] {0, 0, 1, 1, 1, 0, 0, 0},
            new int[] {0, 0, 0, 1, 1, 1, 0, 0},
            new int[] {0, 0, 0, 0, 1, 1, 1, 0},
            new int[] {0, 0, 0, 0, 0, 1, 1, 1},
        };


        /**
         * @return a Map that can be used as the value for a Parameter
         * object's KEY.INFERRED_FIELDS key, to classify the specified
         * field with the specified Classifier type.
         */
        public static Map<String, Type> GetInferredFieldsMap(
            String field, Type classifier)
        {
            Map<String, Type> inferredFieldsMap = new Map<string, Type>();
            inferredFieldsMap.Add(field, classifier);
            return inferredFieldsMap;
        }
    }
}