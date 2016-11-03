using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Threading;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using log4net;
using log4net.Config;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network
{
    /// <summary>
    /// Tests the "heart and soul" of the Network API
    /// </summary>
    [TestClass]
    public class LayerTest
    {
        /// <summary>
        /// Total used for spatial pooler priming tests
        /// </summary>
        private int TOTAL = 0;

        [TestInitialize]
        public void Initialize()
        {
            // Initialize log4net.
            XmlConfigurator.Configure();
        }

        [TestMethod]
        public void TestMasking()
        {
            LayerMask algo_content_mask = 0;

            // -- Build up mask
            algo_content_mask |= LayerMask.ClaClassifier;
            Assert.AreEqual(4, (int)algo_content_mask);

            algo_content_mask |= LayerMask.SpatialPooler;
            Assert.AreEqual(5, (int)algo_content_mask);

            algo_content_mask |= LayerMask.TemporalMemory;
            Assert.AreEqual(7, (int)algo_content_mask);

            algo_content_mask |= LayerMask.AnomalyComputer;
            Assert.AreEqual(15, (int)algo_content_mask);

            // -- Now Peel Off
            algo_content_mask ^= LayerMask.AnomalyComputer;
            Assert.AreEqual(7, (int)algo_content_mask);

            Assert.AreEqual(0, (int)(algo_content_mask & LayerMask.AnomalyComputer));
            Assert.AreEqual(2, (int)(algo_content_mask & LayerMask.TemporalMemory));

            algo_content_mask ^= LayerMask.TemporalMemory;
            Assert.AreEqual(5, (int)algo_content_mask);

            algo_content_mask ^= LayerMask.SpatialPooler;
            Assert.AreEqual(4, (int)algo_content_mask);

            algo_content_mask ^= LayerMask.ClaClassifier;
            Assert.AreEqual(0, (int)algo_content_mask);
        }

        [TestMethod]
        public void CallsOnClosedLayer()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            Net.Network.Network n = new Net.Network.Network("AlreadyClosed", p)
                .Add(Net.Network.Network.CreateRegion("AlreadyClosed")
                    .Add(Net.Network.Network.CreateLayer("AlreadyClosed", p)));

            ILayer l = n.Lookup("AlreadyClosed").Lookup("AlreadyClosed");
            l.Using(new Connections());
            l.Using(p);

            l.Close();

            try
            {
                l.Using(new Connections());

                Assert.Fail(); // Should fail here, disallowing "using" call on closed layer
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType());
                Assert.AreEqual("Layer already \"closed\"", e.Message);
            }

            try
            {
                l.Using(p);

                Assert.Fail(); // Should fail here, disallowing "using" call on closed layer
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType());
                Assert.AreEqual("Layer already \"closed\"", e.Message);
            }
        }


        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestNoName()
        {
            Parameters p = Parameters.GetAllDefaultParameters();

            try
            {
                new Net.Network.Network("", p)
                    .Add(Net.Network.Network.CreateRegion("")
                        .Add(Net.Network.Network.CreateLayer("", p)
                            .Add(Sensor<FileInfo>.Create(
                                Net.Network.Sensor.FileSensor.Create,
                                SensorParams.Create(
                                    SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly-small.csv"))))));

                Assert.Fail(); // Fails due to no name...
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType());
                Assert.AreEqual("All Networks must have a name. Increases digestion, and overall happiness!",
                e.Message);
            }

            try
            {
                new Net.Network.Network("Name", p)
                    .Add(Net.Network.Network.CreateRegion("")
                        .Add(Net.Network.Network.CreateLayer("", p)
                            .Add(Sensor<FileInfo>.Create(
                                Net.Network.Sensor.FileSensor.Create,
                                SensorParams.Create(
                                    SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly-small.csv"))))));


                Assert.Fail(); // Fails due to no name on Region...
            }
            catch (Exception e)
            {
                Assert.AreEqual(typeof(InvalidOperationException), e.GetType());
                Assert.AreEqual("Name may not be null or empty. ...not that anyone here advocates name calling!",
                e.Message);
            }
        }

        [TestMethod]
        public void TestAddSensor()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            try
            {
                Publisher supplier = Publisher.GetBuilder()
                .AddHeader("dayOfWeek")
                .AddHeader("int")
                .AddHeader("B")
                .Build();

                Net.Network.Network n = new Net.Network.Network("Name", p)
                    .Add(Net.Network.Network.CreateRegion("Name")
                        .Add(Net.Network.Network.CreateLayer("Name", p)));

                ILayer l = n.Lookup("Name").Lookup("Name");
                l.Add(Sensor<ObservableSensor<string[]>>.Create(
                    ObservableSensor<string[]>.Create,
                    SensorParams.Create(SensorParams.Keys.Obs, "", supplier)));

                Assert.AreEqual(n, l.GetNetwork());
                Assert.IsTrue(l.GetRegion() != null);

            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [TestMethod]
        public void TestGetAllValues()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            // Test that we get the expected exception if there hasn't been any processing.
            try
            {
                l.GetAllValues<object>("dayOfWeek", 1);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Predictions not available. Either classifiers unspecified or inferencing has not yet begun.", e.Message);
            }

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    Assert.IsNotNull(output);
                    Assert.AreEqual(48, output.GetSdr().Length);
                },
                // OnError
                e =>
                {
                    Console.WriteLine("Error: {0}", e.Message); Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                }));

            //l.Subscribe(new Observer<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        Assert.AreEqual(36, i.GetSdr().Length);
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            Map<string, object> multiInput = new Map<string, object>();
            multiInput.Add("dayOfWeek", 0.0);
            l.Compute(multiInput);

            double[] values = l.GetAllValues<double>("dayOfWeek", 1);
            Assert.IsNotNull(values);
            Assert.IsTrue(values.Length == 1);
            Assert.AreEqual(0.0D, values[0]);
        }

        [TestMethod]
        public void TestResetMethod()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            var l = Net.Network.Network.CreateLayer<int[]>("l1", p).Add(new TemporalMemory());
            try
            {
                l.Reset();
                Assert.IsTrue(l.HasTemporalMemory());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            var l2 = Net.Network.Network.CreateLayer<int[]>("l2", p).Add(new SpatialPooler());
            try
            {
                l2.Reset();
                Assert.IsFalse(l2.HasTemporalMemory());
            }
            catch (Exception e)
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestResetRecordNum()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();

            Layer<int[]> l = (Layer<int[]>)Net.Network.Network.CreateLayer<int[]>("l1", p).Add(new TemporalMemory());

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {

                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                }));
            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        //                System.out.println("output = " + Arrays.toString(output.GetSdr()));
            //    }
            //});

            l.Compute(new int[] { 2, 3, 4 });
            l.Compute(new int[] { 2, 3, 4 });
            Assert.AreEqual(1, l.GetRecordNum());

            l.ResetRecordNum();
            Assert.AreEqual(0, l.GetRecordNum());
        }

        bool isHalted = false;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestHalt()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                            FileSensor.Create,
                            SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv")));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            l.Start();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {

                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {
                    Assert.IsTrue(l.IsHalted());
                    isHalted = true;
                }));

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted()
            //    {
            //        Assert.IsTrue(l.IsHalted());
            //        isHalted = true;
            //    }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output) { }
            //});

            try
            {
                l.Halt();
                l.GetLayerThread().Wait();
                Assert.IsTrue(isHalted);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        int trueCount = 0;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4reset.csv")]
        public void TestReset()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(
                        SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4reset.csv")));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            l.Start();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    Debug.WriteLine("Called onNext()");
                    if (l.GetSensor().GetMetaInfo().IsReset())
                    {
                        trueCount++;
                    }
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {
                    Debug.WriteLine("onCompleted called");
                }));

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        if (l.GetSensor().GetMetaInfo().IsReset())
            //        {
            //            trueCount++;
            //        }
            //    }
            //});

            try
            {
                l.GetLayerThread().Wait();
                Assert.AreEqual(3, trueCount);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        int seqResetCount = 0;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4seqReset.csv")]
        public void TestSequenceChangeReset()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(
                        SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4seqReset.csv")));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            l.Start();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    if (l.GetSensor().GetMetaInfo().IsReset())
                    {
                        seqResetCount++;
                    }
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                }));

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        if (l.GetSensor().GetMetaInfo().IsReset())
            //        {
            //            seqResetCount++;
            //        }
            //    }
            //});

            try
            {
                l.GetLayerThread().Wait();
                Assert.AreEqual(3, seqResetCount);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestLayerWithObservableInput()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("timestamp,consumption")
                .AddHeader("datetime,float")
                .AddHeader("B")
                .Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create,
                SensorParams.Create(SensorParams.Keys.Obs, "name", manual));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            l.Start();

            int idx = 0;
            l.Subscribe(Observer.Create<IInference>(
                        // OnNext
                        output =>
                        {
                            Debug.WriteLine("Called OnNext");
                            switch (idx)
                            {
                                case 0:
                                    Assert.AreEqual("[0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1]", Arrays.ToString(output.GetSdr()));
                                    break;
                                case 1:
                                    Assert.AreEqual("[0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]", Arrays.ToString(output.GetSdr()));
                                    break;
                                case 2:
                                    Assert.AreEqual("[0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]", Arrays.ToString(output.GetSdr()));
                                    break;
                            }
                            ++idx;
                        },
                        // OnError
                        e =>
                        {
                            Console.WriteLine(e);
                        },
                        // OnCompleted
                        () =>
                        {
                            Debug.WriteLine("Called OnComplete");
                        }));

            try
            {
                string[] entries =
                {
                    "7/2/10 0:00,21.2",
                    "7/2/10 1:00,34.0",
                    "7/2/10 2:00,40.4",
                };

                // Send inputs through the observable
                foreach (string s in entries)
                {
                    manual.OnNext(s);
                }
                manual.OnComplete();
                Thread.Sleep(100);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestLayerWithObservableInputIntegerArray()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("sdr_in")
                .AddHeader("darr")
                .AddHeader("B")
                .Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create,
                    SensorParams.Create(
                        SensorParams.Keys.Obs, new object[] { "name", manual }));

            Parameters p = Parameters.GetAllDefaultParameters();
            p = p.Union(GetArrayTestParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            l.Start();

            string input = "[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
                            + "1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, "
                            + "0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, "
                            + "1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, "
                            + "1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                            + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0]";

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        Assert.AreEqual(input, Arrays.toString((int[])output.GetLayerInput()));
            //    }
            //});
            bool calledSubscription = false;

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    calledSubscription = true;
                    Assert.AreEqual(input, Arrays.ToString((int[])output.GetLayerInput()));
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                }));
            try
            {
                string[] entries =
                {
                    input
                };

                // Send inputs through the observable
                foreach (string s in entries)
                {
                    manual.OnNext(s);
                    manual.OnComplete();
                }

                l.GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
            Assert.IsTrue(calledSubscription, "calledSubscription");
        }

        [TestMethod]
        public void TestLayerWithGenericObservable()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            int[] expected0 = new int[] { 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            int[] expected1 = new int[] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1 };

            Func<ManualInput, ManualInput> addedFunc = mi =>
            {
                return mi.SetCustomObject("Interposed: " + Arrays.ToString(mi.GetSdr()));
            };

            Net.Network.Network n = Net.Network.Network.Create("Generic Test", p)
                .Add(Net.Network.Network.CreateRegion("R1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("L1", p)
                        .Add(addedFunc)
                        .Add(new SpatialPooler())));

            Layer<IInference> l = (Layer<IInference>)n.Lookup("R1").Lookup("L1");

            int test = 0;
            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {

                    if (test == 0)
                    {
                        Debug.WriteLine(Arrays.ToString(expected0));
                        Debug.WriteLine(Arrays.ToString(output.GetSdr()));
                        Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()));
                        Assert.AreEqual("Interposed: [1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0]", output.GetCustomObject());
                    }
                    if (test == 1)
                    {
                        Debug.WriteLine(Arrays.ToString(expected1));
                        Debug.WriteLine(Arrays.ToString(output.GetSdr()));
                        Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
                        Assert.AreEqual("Interposed: [0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1]", output.GetCustomObject());
                    }
                    ++test;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                }));
            // SHOULD RECEIVE BOTH
            // Now push some fake data through so that "onNext" is called above
            l.Compute(inputs[0]);
            l.Compute(inputs[1]);
        }

        [TestMethod]
        public void testBasicSetupEncoder_UsingSubscribe()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, null, null, null, null);

            int[][] expected = new int[7][];
            expected[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            expected[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            expected[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            expected[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            expected[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            expected[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            expected[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            //l.Subscribe(new Observer<Inference>() {
            //    int seq = 0;
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        Assert.IsTrue(Arrays.equals(expected[seq++], output.GetSdr()));
            //    }
            //});
            int seq = 0;
            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    Assert.IsTrue(Arrays.AreEqual(expected[seq++], output.GetSdr()));
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );
            Map<string, object> inputs = new Map<string, object>();
            for (double i = 0; i < 7; i++)
            {
                inputs.Add("dayOfWeek", i);
                l.Compute(inputs);
            }
        }

        [TestMethod]
        public void TestBasicSetupEncoder_UsingObserve()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, null, null, null, null);

            int[][] expected = new int[7][];
            expected[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            expected[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            expected[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            expected[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            expected[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            expected[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            expected[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            IObservable<IInference> o = l.Observe();
            //o.Subscribe(new Observer<Inference>() {
            //    int seq = 0;
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        Assert.IsTrue(Arrays.equals(expected[seq++], output.GetSdr()));
            //    }
            //});
            int seq = 0;
            o.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    Assert.IsTrue(Arrays.AreEqual(expected[seq++], output.GetSdr()));
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );
            Map<string, object> inputs = new Map<string, object>();
            for (double i = 0; i < 7; i++)
            {
                inputs.Add("dayOfWeek", i);
                l.Compute(inputs);
            }
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestBasicSetupEncoder_AUTO_MODE()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                SensorParams.Create(
                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv")));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<int[]> l = new Layer<int[]>(n);
            l.Add(htmSensor);

            int[][] expected = {
                new[]    { 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1},
                new[]    { 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1},
                new[]    { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 1, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0},
                new[]    { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 }
            };

            ///////////////////////////////////////////////////////
            //              Test with 2 subscribers              //
            ///////////////////////////////////////////////////////
            int seq = 0;
            l.Observe().Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    //Debug.WriteLine("Called OnNext()");
                    //Debug.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.ToString(expected[seq]));
                    //Debug.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.ToString(output.GetSdr()));
                    Assert.IsTrue(Arrays.AreEqual(expected[seq], output.GetSdr()));
                    seq++;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );
            //l.Observe().Subscribe(new Observer<Inference>() {
            //        int seq = 0;
            //        public void onCompleted() { }
            //        public void onError(Throwable e) { Console.WriteLine(e); }
            //        public void onNext(Inference output)
            //        {
            //            //                System.out.println("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.toString(expected[seq]));
            //            //                System.out.println("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.toString(output.GetSdr()));
            //            Assert.IsTrue(Arrays.equals(expected[seq], output.GetSdr()));
            //            seq++;
            //        }
            //    });
            int seq2 = 0;
            l.Observe().Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    //                System.out.println("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.toString(expected[seq]));
                    //                System.out.println("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.toString(output.GetSdr()));
                    Assert.IsTrue(Arrays.AreEqual(expected[seq2], output.GetSdr()));
                    seq2++;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );
            //l.observe().Subscribe(new Observer<Inference>() {
            //    int seq2 = 0;
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }
            //    public void onNext(Inference output)
            //    {
            //        //                System.out.println("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.toString(expected[seq2]));
            //        //                System.out.println("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.toString(output.GetSdr()));
            //        Assert.IsTrue(Arrays.equals(expected[seq2], output.GetSdr()));
            //        seq2++;
            //    }
            //});

            l.Start();

            try
            {
                l.GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Temporary test to test basic sequence mechanisms
         */
        [TestMethod]
        public void TestBasicSetup_SpatialPooler_MANUAL_MODE()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            int[] expected0 = new int[] { 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            int[] expected1 = new int[] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1 };

            Layer<int[]> l = new Layer<int[]>(p, null, new SpatialPooler(), null, null, null);

            int test = 0;
            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                spatialPoolerOutput =>
                {
                    Debug.WriteLine("E " + Arrays.ToString(expected0));
                    Debug.WriteLine("A " + Arrays.ToString(spatialPoolerOutput.GetSdr()));
                    if (test == 0)
                    {
                        Assert.IsTrue(Arrays.AreEqual(expected0, spatialPoolerOutput.GetSdr()));
                    }
                    if (test == 1)
                    {
                        Assert.IsTrue(Arrays.AreEqual(expected1, spatialPoolerOutput.GetSdr()));
                    }
                    ++test;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );

            //l.Subscribe(new Observer<Inference>() {
            //    int test = 0;

            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }

            //    public void onNext(Inference spatialPoolerOutput)
            //    {
            //        if (test == 0)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected0, spatialPoolerOutput.GetSdr()));
            //        }
            //        if (test == 1)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected1, spatialPoolerOutput.GetSdr()));
            //        }
            //        ++test;
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            l.Compute(inputs[0]);
            l.Compute(inputs[1]);
        }

        /**
         * Temporary test to test basic sequence mechanisms
         */
        [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
        public void TestBasicSetup_SpatialPooler_AUTO_MODE()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.csv")));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            Net.Network.Network n = Net.Network.Network.Create("test network", p);
            Layer<IInference> l = new Layer<IInference>(n);
            l.Add(htmSensor).Add(new SpatialPooler());

            int[] expected0 = new int[] { 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };
            int[] expected1 = new int[] { 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };

            int test = 0;
            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                spatialPoolerOutput =>
                {
                    if (test == 0)
                    {
                        Debug.WriteLine(Arrays.ToString(expected0));
                        Debug.WriteLine(Arrays.ToString(spatialPoolerOutput.GetSdr()));
                        Assert.IsTrue(Arrays.AreEqual(expected0, spatialPoolerOutput.GetSdr()));
                    }
                    if (test == 1)
                    {
                        Debug.WriteLine(Arrays.ToString(expected1));
                        Debug.WriteLine(Arrays.ToString(spatialPoolerOutput.GetSdr()));
                        Assert.IsTrue(Arrays.AreEqual(expected1, spatialPoolerOutput.GetSdr()));
                    }
                    ++test;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );

            //l.Subscribe(new Observer<Inference>() {
            //    int test = 0;

            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }

            //    public void onNext(Inference spatialPoolerOutput)
            //    {
            //        if (test == 0)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected0, spatialPoolerOutput.GetSdr()));
            //        }
            //        if (test == 1)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected1, spatialPoolerOutput.GetSdr()));
            //        }
            //        ++test;
            //    }
            //});

            l.Start();

            try
            {
                l.GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Temporary test to test basic sequence mechanisms
         */
        int seq = 0;
        [TestMethod]
        public void TestBasicSetup_TemporalMemory_MANUAL_MODE()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

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

            int test = 0;
            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    if (seq / 7 >= timeUntilStable)
                    {
                        //                    System.out.println("seq: " + (seq) + "  --> " + (test) + "  output = " + Arrays.toString(output.GetSdr()) +
                        //                        ", \t\t\t\t cols = " + Arrays.toString(SDR.asColumnIndices(output.GetSdr(), l.GetConnections().GetCellsPerColumn())));
                        Assert.IsTrue(output.GetSdr().Length >= 8);
                    }

                    if (test == 6) test = 0;
                    else test++;
                },
                // OnError
                e =>
                {
                    Console.WriteLine(e);
                },
                // OnCompleted
                () =>
                {

                })
            );
            //l.Subscribe(new Observer<Inference>() {
            //    int test = 0;

            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }

            //    public void onNext(Inference output)
            //    {
            //        if (seq / 7 >= timeUntilStable)
            //        {
            //            //                    System.out.println("seq: " + (seq) + "  --> " + (test) + "  output = " + Arrays.toString(output.GetSdr()) +
            //            //                        ", \t\t\t\t cols = " + Arrays.toString(SDR.asColumnIndices(output.GetSdr(), l.GetConnections().GetCellsPerColumn())));
            //            Assert.IsTrue(output.GetSdr().Length >= 8);
            //        }

            //        if (test == 6) test = 0;
            //        else test++;
            //    }
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
        }

        [TestMethod]
        public void TestBasicSetup_SPandTM()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            Layer<int[]> l = new Layer<int[]>(p, null, new SpatialPooler(), new TemporalMemory(), null, null);

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   Assert.IsTrue(output.GetSdr().Length > 0);
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        Assert.AreEqual(0, i.GetSdr().Length);
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            l.Compute(inputs[0]);
            l.Compute(inputs[1]);
        }

        [TestMethod]
        public void TestSpatialPoolerPrimerDelay()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            int[] expected0 = new int[] { 1, 0, 0, 1, 1, 1, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 };
            int[] expected1 = new int[] { 0, 0, 0, 0, 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1 };

            // First test without prime directive :-P
            Layer<int[]> l = new Layer<int[]>(p, null, new SpatialPooler(), null, null, null);

            int test = 0;
            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Debug.WriteLine(Arrays.ToString(expected0));
                   Debug.WriteLine(Arrays.ToString(output.GetSdr()));
                   if (test == 0)
                   {

                       Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()));
                   }
                   if (test == 1)
                   {
                       Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
                   }
                   ++test;
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>() {
            //    int test = 0;

            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        if (test == 0)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected0, i.GetSdr()));
            //        }
            //        if (test == 1)
            //        {
            //            Assert.IsTrue(Arrays.equals(expected1, i.GetSdr()));
            //        }
            //        ++test;
            //    }
            //});

            // SHOULD RECEIVE BOTH
            // Now push some fake data through so that "onNext" is called above
            l.Compute(inputs[0]);
            l.Compute(inputs[1]);

            // --------------------------------------------------------------------------------------------

            // NOW TEST WITH prime directive
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42)); // due to static RNG we have to reset the sequence
            p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, 1);

            Layer<int[]> l2 = new Layer<int[]>(p, null, new SpatialPooler(), null, null, null);

            l2.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   // should be one and only onNext() called 
                   Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l2.Subscribe(new Observer<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        // should be one and only onNext() called 
            //        Assert.IsTrue(Arrays.equals(expected1, i.GetSdr()));
            //    }
            //});

            // SHOULD RECEIVE BOTH
            // Now push some fake data through so that "onNext" is called above
            l2.Compute(inputs[0]);
            l2.Compute(inputs[1]);
        }

        /**
         * Simple test to verify data gets passed through the {@link CLAClassifier}
         * configured within the chain of components.
         */
        [TestMethod]
        public void TestBasicClassifierSetup()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   Assert.AreEqual(48, output.GetSdr().Length);
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        Assert.AreEqual(36, i.GetSdr().Length);
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            Map<string, object> multiInput = new Map<string, object>();
            multiInput.Add("dayOfWeek", 0.0);
            l.Compute(multiInput);
        }

        /**
         * The {@link SpatialPooler} sometimes needs to have data run through it 
         * prior to passing the data on to subsequent algorithmic components. This
         * tests the ability to specify exactly the number of input records for 
         * the SpatialPooler to consume before passing records on.
         */
        [TestMethod]
        public void TestMoreComplexSpatialPoolerPriming()
        {
            int PRIME_COUNT = 35;
            int NUM_CYCLES = 20;
            int INPUT_GROUP_COUNT = 7; // Days of Week
            TOTAL = 0;

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   TOTAL++;
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        TOTAL++;
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            Map<string, object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    l.Compute(multiInput);
                }
            }

            // Assert we can accurately specify how many inputs to "prime" the spatial pooler
            // and subtract that from the total input to get the total entries sent through
            // the event chain from bottom to top.
            Assert.AreEqual((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT, TOTAL);
        }

        /**
         * Tests the ability for multiple subscribers to receive copies of
         * a given {@link Layer}'s computed values.
         */
        [TestMethod]
        public void Test2NdAndSubsequentSubscribersPossible()
        {
            int PRIME_COUNT = 35;
            int NUM_CYCLES = 50;
            int INPUT_GROUP_COUNT = 7; // Days of Week
            TOTAL = 0;

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            int[][] inputs = new int[7][];
            inputs[0] = new int[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new int[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new int[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new int[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new int[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new int[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new int[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   TOTAL++;
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        TOTAL++;
            //    }
            //});

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   TOTAL++;
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        TOTAL++;
            //    }
            //});

            l.Subscribe(Observer.Create<IInference>(
               // OnNext
               output =>
               {
                   Assert.IsNotNull(output);
                   TOTAL++;
               },
               // OnError
               e =>
               {
                   Console.WriteLine(e);
               },
               // OnCompleted
               () =>
               {

               })
           );
            //l.Subscribe(new Observer<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        Assert.IsNotNull(i);
            //        TOTAL++;
            //    }
            //});

            // Now push some fake data through so that "onNext" is called above
            Map<string, object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    l.Compute(multiInput);
                }
            }

            int NUM_SUBSCRIBERS = 3;
            // Assert we can accurately specify how many inputs to "prime" the spatial pooler
            // and subtract that from the total input to get the total entries sent through
            // the event chain from bottom to top.
            Assert.AreEqual(((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT) * NUM_SUBSCRIBERS, TOTAL);
        }

        [TestMethod]
        public void TestGetAllPredictions()
        {
            ILog logger = LogManager.GetLogger(typeof(LayerTest));

            logger.Debug("Starting test...");

            int PRIME_COUNT = 35;
            int NUM_CYCLES = 600;
            int INPUT_GROUP_COUNT = 7; // Days of Week
            TOTAL = 0;

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(43));
            p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

            int cellsPerColumn = (int)p.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN);
            Assert.IsTrue(cellsPerColumn > 0);

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    Assert.IsNotNull(output);
                    TOTAL++;
                    if (l.GetPreviousPredictiveCells() != null)
                    {
                        //UNCOMMENT TO VIEW STABILIZATION OF PREDICTED FIELDS
                        Console.WriteLine("recNum: {0} + Day: {1} - FFWActCols: {2} - PrevPred: {3}",
                            output.GetRecordNum(), ((Dictionary<String, Object>)output.GetLayerInput()).Get("dayOfWeek"),
                            Arrays.ToString(ArrayUtils.Where(output.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1)),
                            Arrays.ToString(SDR.CellsAsColumnIndices(output.GetPreviousPredictiveCells(), cellsPerColumn)));
                        //                    System.out.println("recordNum: " + i.GetRecordNum() + "  Day: " + ((Dictionary<String, Object>)i.GetLayerInput()).Get("dayOfWeek") + "  -  " + 
                        //                       Arrays.toString(ArrayUtils.where(l.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1)) +
                        //                         "   -   " + Arrays.toString(SDR.cellsAsColumnIndices(l.GetPreviousPredictiveCells(), cellsPerColumn)));
                    }
                },
                // OnError
                e => { Console.WriteLine(e); Assert.Fail(e.ToString()); },
                // OnCompleted
                () => { })
            );

            // Now push some fake data through so that "onNext" is called above
            Map<string, object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    l.Compute(multiInput);
                }
                l.Reset();
            }

            // Assert we can accurately specify how many inputs to "prime" the spatial pooler
            // and subtract that from the total input to get the total entries sent through
            // the event chain from bottom to top.
            Assert.AreEqual((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT, TOTAL);

            double[] all = l.GetAllPredictions("dayOfWeek", 1);
            double highestVal = double.NegativeInfinity;
            int highestIdx = -1;
            int cnt = 0;
            foreach (double d in all)
            {
                if (d > highestVal)
                {
                    highestIdx = cnt;
                    highestVal = d;
                }
                cnt++;
            }

            Assert.AreEqual(highestIdx, l.GetMostProbableBucketIndex("dayOfWeek", 1));
            Assert.AreEqual(7, l.GetAllPredictions("dayOfWeek", 1).Length);

            //var activeColumnsFfwd = ArrayUtils.Where(l.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1);
            //var prevPredictedCells = SDR.CellsAsColumnIndices(l.GetPreviousPredictiveCells(), cellsPerColumn);
            //Debug.WriteLine("A: " + Arrays.ToString(activeColumnsFfwd));
            //Debug.WriteLine("P: " + Arrays.ToString(prevPredictedCells));

            //Assert.IsTrue(Arrays.AreEqual(activeColumnsFfwd, prevPredictedCells));
        }

        /**
         * Test that a given layer can return an {@link Observable} capable of 
         * service multiple subscribers.
         */
        [TestMethod]
        public void TestObservableRetrieval()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, me, new SpatialPooler(), new TemporalMemory(), true, null);

            List<int[]> emissions = new List<int[]>();
            IObservable<IInference> o = l.Observe();

            o.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    emissions.Add(l.GetFeedForwardActiveColumns());
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );
            o.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    emissions.Add(l.GetFeedForwardActiveColumns());
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );
            o.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    emissions.Add(l.GetFeedForwardActiveColumns());
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );
            //o.Subscribe(new Subscriber<Inference>()
            //{
            //        public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }
            //    public void onNext(Inference i)
            //    {
            //        emissions.Add(l.GetFeedForwardActiveColumns());
            //    }
            //});
            //o.Subscribe(new Subscriber<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }
            //    public void onNext(Inference i)
            //    {
            //        emissions.Add(l.GetFeedForwardActiveColumns());
            //    }
            //});
            //o.Subscribe(new Subscriber<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }
            //    public void onNext(Inference i)
            //    {
            //        emissions.Add(l.GetFeedForwardActiveColumns());
            //    }
            //});

            Map<string, object> multiInput = new Map<string, object>();
            multiInput.Add("dayOfWeek", 0.0);
            l.Compute(multiInput);

            Assert.AreEqual(3, emissions.Count);
            int[] e1 = emissions[0];
            foreach (int[] ia in emissions)
            {
                Assert.IsTrue(ia == e1);//test same object propagated
            }
        }

        [TestMethod]
        public void TestInferInputDimensions()
        {
            Parameters p = Parameters.GetAllDefaultParameters();
            Layer<Map<string, object>> l = new Layer<Map<string, object>>(p, null, new SpatialPooler(), new TemporalMemory(), true, null);

            int[] dims = l.InferInputDimensions(16384, 2);
            Assert.IsTrue(new[] { 128, 128 }.SequenceEqual(dims));

            dims = l.InferInputDimensions(8000, 3);
            Assert.IsTrue(new[] { 20, 20, 20 }.SequenceEqual(dims));

            dims = l.InferInputDimensions(450, 2);
            Assert.IsTrue(new[] { 1, 450 }.SequenceEqual(dims));
        }

        [TestMethod]
        public void TestExplicitCloseFailure()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create())
                        .Add(new SpatialPooler())
                        .Close()));

            // Set up a log filter to grab the next message.

            //LoggerContext lc = (LoggerContext)LoggerFactory.getILoggerFactory();
            //StatusPrinter.print(lc);
            //lc.addTurboFilter(new TurboFilter() {
            //    public FilterReply decide(Marker arg0, Logger arg1, Level arg2, String arg3, Object[] arg4, Throwable arg5)
            //{
            //    filterMessage = arg3;
            //                    return FilterReply.ACCEPT;
            //                }
            //});

            network.Lookup("r1").Lookup("2").Close();

            // Test that the close() method exited after logging the correct message
            //Assert.AreEqual("Close called on Layer r1:2 which is already closed.", filterMessage);
            // Make sure not to slow the entire test phase down by removing the filter
            //lc.resetTurboFilterList();
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        [ExpectedException(typeof(InvalidOperationException))]
        public void IsClosedAddSensorTest()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            ILayer l = Net.Network.Network.CreateLayer("l", p);
            l.Close();

            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                    FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly-small.csv")));
            l.Add(sensor);
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void IsClosedAddMultiEncoderTest()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            ILayer l = Net.Network.Network.CreateLayer("l", p);
            l.Close();

            l.Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build());
        }

        [TestMethod]
        [ExpectedException(typeof(InvalidOperationException))]
        public void IsClosedAddSpatialPoolerTest()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            ILayer l = Net.Network.Network.CreateLayer("l", p);
            l.Close();

            l.Add(new SpatialPooler());
        }

        /**
         * Simple test to verify data gets passed through the {@link CLAClassifier}
         * configured within the chain of components.
         */
        bool flowReceived = false;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestFullLayerFluentAssembly()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
            p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 200);
            p.SetParameterByKey(Parameters.KEY.INHIBITION_RADIUS, 50);
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);

            //        System.out.println(p);

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);

            Anomaly anomalyComputer = Anomaly.Create(p);

            Layer<object> l = (Layer<object>)Net.Network.Network.CreateLayer<object>("TestLayer", p)
                                            .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                                            .Add(anomalyComputer)
                                            .Add(new TemporalMemory())
                                            .Add(new SpatialPooler())
                                            .Add(Sensor<FileInfo>.Create(
                                                FileSensor.Create,
                                                SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv"))));

            l.GetConnections().PrintParameters();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    if (flowReceived) return; // No need to set this value multiple times

                    flowReceived = output.GetClassifiers().Count == 4 &&
                                   output.GetClassifiers().Get("timestamp") != null &&
                                   output.GetClassifiers().Get("consumption") != null;
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        if (flowReceived) return; // No need to set this value multiple times

            //        flowReceived = i.GetClassifiers().size() == 4 &&
            //            i.GetClassifiers().Get("timestamp") != null &&
            //                i.GetClassifiers().Get("consumption") != null;
            //    }
            //});

            l.Start();

            try
            {
                l.GetLayerThread().Wait();
                Assert.IsTrue(flowReceived);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
        public void TestMissingEncoderMap()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            //p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
            p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 200);
            p.SetParameterByKey(Parameters.KEY.INHIBITION_RADIUS, 50);
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);

            //        System.out.println(p);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);

            Anomaly anomalyComputer = Anomaly.Create(p);

            Layer<object> l = (Layer<object>)Net.Network.Network.CreateLayer<object>("TestLayer", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(anomalyComputer)
                .Add(new TemporalMemory())
                .Add(new SpatialPooler())
                .Add(Sensor<FileInfo>.Create(
                    FileSensor.Create,
                    SensorParams.Create(
                        SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv"))));

            l.GetConnections().PrintParameters();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    if (flowReceived) return; // No need to set this value multiple times

                    flowReceived = output.GetClassifiers().Count == 4 &&
                                   output.GetClassifiers().Get("timestamp") != null &&
                                   output.GetClassifiers().Get("consumption") != null;
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        if (flowReceived) return; // No need to set this value multiple times

            //        flowReceived = i.GetClassifiers().size() == 4 &&
            //            i.GetClassifiers().Get("timestamp") != null &&
            //                i.GetClassifiers().Get("consumption") != null;
            //    }
            //});

            try
            {
                l.Close();
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Cannot initialize this Sensor's MultiEncoder with a null settings", e.Message);
            }

            try
            {
                Assert.IsFalse(flowReceived);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            ////////////////// Test catch with no Sensor ////////////////////

            p = NetworkTestHarness.GetParameters().Copy();
            //p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
            p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 200);
            p.SetParameterByKey(Parameters.KEY.INHIBITION_RADIUS, 50);
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);

            anomalyComputer = Anomaly.Create(p);

            l = (Layer<object>)Net.Network.Network.CreateLayer<object>("TestLayer", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(anomalyComputer)
                .Add(new TemporalMemory())
                .Add(new SpatialPooler())
                .Add(anomalyComputer)
                .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build());

            l.GetConnections().PrintParameters();

            l.Subscribe(Observer.Create<IInference>(
                // OnNext
                output =>
                {
                    if (flowReceived) return; // No need to set this value multiple times

                    flowReceived = output.GetClassifiers().Count == 4 &&
                                   output.GetClassifiers().Get("timestamp") != null &&
                                   output.GetClassifiers().Get("consumption") != null;
                },
                // OnError
                e => { Console.WriteLine(e); },
                // OnCompleted
                () => { })
            );

            //l.Subscribe(new Observer<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { System.out.println("error: " + e.Message); Console.WriteLine(e); }

            //    public void onNext(Inference i)
            //    {
            //        if (flowReceived) return; // No need to set this value multiple times

            //        flowReceived = i.GetClassifiers().size() == 4 &&
            //            i.GetClassifiers().Get("timestamp") != null &&
            //                i.GetClassifiers().Get("consumption") != null;
            //    }
            //});

            try
            {
                l.Close();
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("No field encoding map found for specified MultiEncoder", e.Message);
            }

            try
            {
                Assert.IsFalse(flowReceived);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private Parameters GetArrayTestParams()
        {
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                null,
                884, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "sdr_in", "darr", "SDRPassThroughEncoder");
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            return p;
        }

        private Map<string, Map<string, object>> SetupMap(
            Map<string, Map<string, object>> map,
            int n, int w, double min, double max, double radius, double resolution, bool? periodic,
            bool? clip, bool? forced, string fieldName, string fieldType, string encoderType)
        {

            if (map == null)
            {
                map = new Map<string, Map<string, object>>();
            }
            Map<string, object> inner;
            if (!map.TryGetValue(fieldName, out inner))
            {
                map.Add(fieldName, inner = new Map<string, object>());
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

        [TestMethod]
        public void TestEquality()
        {
            Parameters p = Parameters.GetAllDefaultParameters();
            Layer<IDictionary<string, object>> l = new Layer<IDictionary<string, object>>(p, null, new SpatialPooler(), new TemporalMemory(), true, null);
            Layer<IDictionary<string, object>> l2 = new Layer<IDictionary<string, object>>(p, null, new SpatialPooler(), new TemporalMemory(), true, null);

            Assert.IsTrue(l.Equals(l));
            Assert.IsFalse(l.Equals(null));
            Assert.IsTrue(l.Equals(l2));

            l2.SetName("I'm different");
            Assert.IsFalse(l.Equals(l2));

            l2.SetName(null);
            Assert.IsTrue(l.Equals(l2));

            Net.Network.Network n = new Net.Network.Network("TestNetwork", p);
            Region r = new Region("r1", n);
            l.SetRegion(r);
            Assert.IsFalse(l.Equals(l2));

            l2.SetRegion(r);
            Assert.IsTrue(l.Equals(l2));

            Region r2 = new Region("r2", n);
            l2.SetRegion(r2);
            Assert.IsFalse(l.Equals(l2));
        }
    }
}