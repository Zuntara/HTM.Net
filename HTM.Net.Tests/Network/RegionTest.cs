using System;
using System.Threading;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Encoders;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class RegionTest
    {

        [TestMethod]
        public void TestClose()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build()))
                        .Close());

            Assert.IsTrue(n.Lookup("r1").IsClosed());

            try
            {
                n.Lookup("r1").Add(Net.Network.Network.CreateLayer<IInference>("5", p));
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.GetType().IsAssignableFrom(typeof(InvalidOperationException)));
                Assert.AreEqual("Cannot add Layers when Region has already been closed.", e.Message);
            }
        }

        [TestMethod]
        public void TestResetMethod()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Region r1 = Net.Network.Network.CreateRegion("r1");
            r1.Add(Net.Network.Network.CreateLayer<IInference>("l1", p).Add(new TemporalMemory()));
            try
            {
                r1.Reset();
                Assert.IsTrue(r1.Lookup("l1").HasTemporalMemory());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            r1 = Net.Network.Network.CreateRegion("r1");
            r1.Add(Net.Network.Network.CreateLayer<IInference>("l1", p).Add(new SpatialPooler()));
            try
            {
                r1.Reset();
                Assert.IsFalse(r1.Lookup("l1").HasTemporalMemory());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        [TestMethod]
        public void TestResetRecordNum()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Region r1 = Net.Network.Network.CreateRegion("r1");
            r1.Add(Net.Network.Network.CreateLayer<IInference>("l1", p).Add(new TemporalMemory()));
            //r1.Observe().Subscribe(new Observer<Inference>() {
            //    public void onCompleted() { }
            //     public void onError(Throwable e) { e.printStackTrace(); }
            //     public void onNext(Inference output)
            //    {
            //        System.Out.println("output = " + Arrays.toString(output.GetSDR()));
            //    }
            //});

            r1.Compute(new int[] { 2, 3, 4 });
            r1.Compute(new int[] { 2, 3, 4 });
            Assert.AreEqual(1, r1.Lookup("l1").GetRecordNum());

            r1.ResetRecordNum();
            Assert.AreEqual(0, r1.Lookup("l1").GetRecordNum());
        }

        [TestMethod]
        public void TestAutomaticClose()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build())));
            //.Close(); // Not necessary due to implicit call during start() or compute()

            Region r1 = n.Lookup("r1");
            r1.Start();

            Assert.IsTrue(r1.IsClosed());

            try
            {
                r1.Add(Net.Network.Network.CreateLayer<IInference>("5", p));
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsTrue(e.GetType().IsAssignableFrom(typeof(InvalidOperationException)));
                Assert.AreEqual("Cannot add Layers when Region has already been closed.", e.Message);
            }
        }

        [TestMethod]
        public void TestAdd()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build())));

            Region r1 = n.Lookup("r1");
            ILayer layer4 = r1.Lookup("4");
            Assert.IsNotNull(layer4);
            Assert.AreEqual("r1:4", layer4.GetName());

            try
            {
                r1.Add(Net.Network.Network.CreateLayer<IInference>("4", p));
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(InvalidOperationException));
                Assert.AreEqual("A Layer with the name: 4 has already been added to this Region.", e.Message);
            }
        }

        bool isHalted;
        [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
        public void TestHalt()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create(p)))
                    .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(Sensor<FileSensor>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.csv"))))
                        .Add(new SpatialPooler()))
                .Connect("1", "2")
                .Connect("2", "3")
                .Connect("3", "4"));

            Region r1 = n.Lookup("r1");

            int seq = 0;
            r1.Observe().Subscribe(
                // next
                i =>
                {
                    if (seq == 2)
                    {
                        isHalted = true;
                    }
                    seq++;
                },
                //error
                e => { Console.WriteLine(e); },
                //completed
                () => { Console.WriteLine("onCompleted() called"); }
           );

            //r1.Observe().Subscribe(new Subscriber<Inference>() {
            //    int seq = 0;
            //    public void onCompleted()
            //    {
            //        //                System.Out.println("onCompleted() called");
            //    }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        if (seq == 2)
            //        {
            //            isHalted = true;
            //        }
            //        seq++;
            //        //                System.Out.println("output: " + i.GetSDR());
            //    }
            //});

            new Thread(() =>
            {
                while (!isHalted)
                {
                    try { Thread.Sleep(1); } catch (Exception e) { Console.WriteLine(e); }
                }
                r1.Halt();
            }).Start();

            //        (new Thread()
            //        {
            //        public void run()
            //    {
            //        while (!isHalted)
            //        {
            //            try { Thread.Sleep(1); } catch (Exception e) { e.printStackTrace(); }
            //        }
            //        r1.Halt();
            //    }
            //}).Start();

            r1.Start();

            try
            {
                r1.Lookup("4").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Test that we automatically calculate the input dimensions despite
         * there being an improper Parameter setting.
         */
        [TestMethod]
        public void TestInputDimensionsAutomaticallyInferredFromEncoderWidth()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            // Purposefully set this to be wrong
            p.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 40, 40 });

            Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build()))
                        .Close());

            // Should correct the above ( {40,40} ) to have only one dimension whose width is 8 ( {8} )
            Assert.IsTrue(Arrays.AreEqual(new int[] { 8 }, (int[])p.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS)));
        }

        /**
         * Test encoder bubbles up to L1
         */
        [TestMethod]
        public void TestEncoderPassesUpToTopLayer()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create(p)))
                    .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(new SpatialPooler())
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build())));

            Region r1 = n.Lookup("r1");
            r1.Connect("1", "2").Connect("2", "3").Connect("3", "4");

            Assert.IsNotNull(r1.Lookup("1").GetEncoder());
        }

        /**
         * Test that we can assemble a multi-layer Region and manually feed in
         * input and have the processing pass through each Layer.
         */
        [TestMethod]
        public void TestMultiLayerAssemblyNoSensor()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 30 });
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.4);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 7);
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create(p)))
                    .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(new SpatialPooler())
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build()))
                    .Connect("1", "2")
                    .Connect("2", "3")
                    .Connect("3", "4"));

            Region r1 = n.Lookup("r1");
            r1.Lookup("3").Using(r1.Lookup("4").GetConnections()); // How to share Connections object between Layers

            //r1.Observe().Subscribe(new Subscriber<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        // UNCOMMENT TO VIEW STABILIZATION OF PREDICTED FIELDS
            //        System.Out.println("Day: " + r1.GetInput() + " - predictive cells: " + i.GetPreviousPredictiveCells() +
            //            "   -   " + Arrays.toString(i.GetFeedForwardSparseActives()) + " - " +
            //            ((int)Math.Rint(((Number)i.GetClassification("dayOfWeek").GetMostProbableValue(1)).doubleValue())));
            //    }
            //});

            const int NUM_CYCLES = 400;
            const int INPUT_GROUP_COUNT = 7; // Days of Week
            Map<String, Object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    r1.Compute(multiInput);
                }
                r1.Reset();
            }

            r1.SetLearn(false);
            r1.Reset();

            // Test that we get proper output after prediction stabilization
            //r1.Observe().Subscribe(new Subscriber<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        int nextDay = ((int)Math.Rint(((Number)i.GetClassification("dayOfWeek").GetMostProbableValue(1)).doubleValue()));
            //        Assert.AreEqual(6, nextDay);
            //    }
            //});
            multiInput.Add("dayOfWeek", 5.0);
            r1.Compute(multiInput);

        }

        [TestMethod]
        public void TestIsLearn()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 30 });
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.4);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 7);
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create(p)))
                    .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(new SpatialPooler())
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build()))
                    .Connect("1", "2")
                    .Connect("2", "3")
                    .Connect("3", "4"));

            n.Lookup("r1").Close();

            n.SetLearn(false);

            Assert.IsFalse(n.IsLearn());

            Region r1 = n.Lookup("r1");
            Assert.IsFalse(n.IsLearn());
            ILayer layer = r1.GetTail();
            Assert.IsFalse(layer.SetIsLearn());
            while (layer.GetNext() != null)
            {
                layer = layer.GetNext();
                Assert.IsFalse(layer.SetIsLearn());
            }
        }

        int idx0 = 0;
        int idx1 = 0;
        int idx2 = 0;
        /**
         * For this test, see that we can subscribe to each layer and also to the
         * Region itself and that emissions for each sequence occur for all 
         * subscribers.
         */
        [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
        public void Test2LayerAssemblyWithSensor()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(Sensor<FileSensor>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.Csv"))))
                        .Add(new SpatialPooler()))
                .Connect("2/3", "4"));

            int[][] inputs = new int[7][];
            inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
            inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
            inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
            inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
            inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
            inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
            inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

            Region r1 = n.Lookup("r1");
            // Observe the top layer
            r1.Lookup("4").Observe().Subscribe(
                // next
                i =>
                {
                    Console.WriteLine("onNext() called (4)");
                    Assert.IsTrue(Arrays.AreEqual(inputs[idx0++], i.GetEncoding()));
                },
                //error
                e => { Console.WriteLine(e); },
                //completed
                () => { Console.WriteLine("onCompleted() called (4)"); }
           );
            //r1.Lookup("4").Observe().Subscribe(new Subscriber<Inference>()
            //{
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        Assert.IsTrue(Arrays.equals(inputs[idx0++], i.GetEncoding()));
            //    }
            //});

            // Observe the bottom layer
            r1.Lookup("2/3").Observe().Subscribe(
                // next
                i =>
                {
                    Console.WriteLine("onNext() called (2/3)");
                    Assert.IsTrue(Arrays.AreEqual(inputs[idx1++], i.GetEncoding()));
                },
                //error
                e => { Console.WriteLine(e); },
                //completed
                () => { Console.WriteLine("onCompleted() called (2/3)"); }
           );
            //r1.Lookup("2/3").Observe().Subscribe(new Subscriber<Inference>() {
            //    public void onCompleted() { }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        Assert.IsTrue(Arrays.equals(inputs[idx1++], i.GetEncoding()));
            //    }
            //});

            // Observe the Region output
            r1.Observe().Subscribe(
                // next
                i =>
                {
                    Console.WriteLine("onNext() called (out)");
                    Assert.IsTrue(Arrays.AreEqual(inputs[idx2++], i.GetEncoding()));
                },
                //error
                e => { Console.WriteLine(e); },
                //completed
                () => { Console.WriteLine("onCompleted() called (out)"); }
           );
            //r1.Observe().Subscribe(new Subscriber<Inference>() {
            //     public void onCompleted() { }
            //    public void onError(Throwable e) { e.printStackTrace(); }
            //    public void onNext(Inference i)
            //    {
            //        Assert.IsTrue(Arrays.equals(inputs[idx2++], i.GetEncoding()));
            //    }
            //});

            r1.Start();

            try
            {
                r1.Lookup("4").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Assert.AreEqual(7, idx0);
            Assert.AreEqual(7, idx1);
            Assert.AreEqual(7, idx2);
        }

        /**
         * Tests that we can detect if the occurrence of algorithms within a region's layers
         * is repeated or not. If they are repeated, then we don't allow the Inference object's
         * values to be passed from one layer to another because it is assumed that values 
         * such as "activeColumns" or "previousPrediction" should not be overwritten in the case
         * where algorithms are not repeated, and should be overwritten when algorithms are repeated.
         * 
         * The SDR is <em>always</em> passed between layers however.
         */
        [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
        public void TestAlgorithmRepetitionDetection()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            // -- No overlap
            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(Sensor<FileSensor>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.Csv"))))
                        .Add(new SpatialPooler()))
                .Connect("2/3", "4"));

            Region r = n.Lookup("r1");
            Assert.IsTrue(r.layersDistinct);
            LayerMask flags = r.flagAccumulator;
            flags ^= LayerMask.SpatialPooler;
            flags ^= LayerMask.TemporalMemory;
            flags ^= LayerMask.ClaClassifier;
            Assert.AreEqual(LayerMask.None, flags);
            Assert.AreEqual(r.Lookup("2/3").GetMask(), (LayerMask.TemporalMemory | LayerMask.ClaClassifier));
            Assert.AreEqual(r.Lookup("4").GetMask(), LayerMask.SpatialPooler);

            // -- Test overlap detection
            n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(Sensor<FileSensor>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.Csv"))))
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler()))
                .Connect("2/3", "4"));

            r = n.Lookup("r1");
            Assert.IsFalse(r.layersDistinct);
            Assert.AreEqual(r.Lookup("2/3").GetMask(), (LayerMask.TemporalMemory | LayerMask.ClaClassifier));
            Assert.AreEqual(r.Lookup("4").GetMask(), (LayerMask.SpatialPooler | LayerMask.TemporalMemory));

        }
    }
}