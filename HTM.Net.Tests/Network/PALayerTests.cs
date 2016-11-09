using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reactive;
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
    //[TestClass]
    //public class PaLayerTest
    //{

    //    /** Total used for spatial pooler priming tests */
    //    private int _total;

    //    [TestMethod]
    //    public void TestMasking()
    //    {
    //        LayerMask algoContentMask = 0;

    //        // -- Build up mask
    //        algoContentMask |= LayerMask.ClaClassifier;
    //        Assert.AreEqual(4, (int)algoContentMask);

    //        algoContentMask |= LayerMask.SpatialPooler;
    //        Assert.AreEqual(5, (int)algoContentMask);

    //        algoContentMask |= LayerMask.TemporalMemory;
    //        Assert.AreEqual(7, (int)algoContentMask);

    //        algoContentMask |= LayerMask.AnomalyComputer;
    //        Assert.AreEqual(15, (int)algoContentMask);

    //        // -- Now Peel Off
    //        algoContentMask ^= LayerMask.AnomalyComputer;
    //        Assert.AreEqual(7, (int)algoContentMask);

    //        Assert.AreEqual(0, (int)(algoContentMask & LayerMask.AnomalyComputer));
    //        Assert.AreEqual(2, (int)(algoContentMask & LayerMask.TemporalMemory));

    //        algoContentMask ^= LayerMask.TemporalMemory;
    //        Assert.AreEqual(5, (int)algoContentMask);

    //        algoContentMask ^= LayerMask.SpatialPooler;
    //        Assert.AreEqual(4, (int)algoContentMask);

    //        algoContentMask ^= LayerMask.ClaClassifier;
    //        Assert.AreEqual(0, (int)algoContentMask);
    //    }

    //    [TestMethod]
    //    public void TestGetAllValues()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);

    //        // Test that we get the expected exception if there hasn't been any processing.
    //        try
    //        {
    //            l.GetAllValues<double>("dayOfWeek", 1);
    //            Assert.Fail();
    //        }
    //        catch (Exception e)
    //        {
    //            Assert.AreEqual("Predictions not available. Either classifiers unspecified or inferencing has not yet begun.", e.Message);
    //        }

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                Assert.AreEqual(48, output.GetSdr().Length);
    //            },
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },
    //            () => { }
    //        ));

    //        //l.Subscribe(new IObserver<IInference>() {
    //        //    public void onCompleted() { }
    //        //     public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }

    //        //        public void onNext(Inference i)
    //        //    {
    //        //        Assert.IsNotNull(i);
    //        //        Assert.AreEqual(36, i.GetSDR().Length);
    //        //    }
    //        //});

    //        // Now push some fake data through so that "onNext" is called above
    //        Dictionary<string, object> multiInput = new Dictionary<string, object>();
    //        multiInput.Add("dayOfWeek", 0.0);
    //        l.Compute(multiInput);

    //        double[] values = l.GetAllValues<double>("dayOfWeek", 1);
    //        Assert.IsNotNull(values);
    //        Assert.IsTrue(values.Length == 1);
    //        Assert.AreEqual(0.0D, values[0]);
    //    }

    //    [TestMethod]
    //    public void TestConstructors()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        Net.Network.Network n = new Net.Network.Network("test", p);
    //        PALayer<object> l = new PALayer<object>(n, p);
    //        Assert.IsTrue(n == l.GetParentNetwork());
    //    }

    //    [TestMethod]
    //    public void TestPolariseAndVerbosity()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        Net.Network.Network n = new Net.Network.Network("test", p);
    //        PALayer<object> l = new PALayer<object>(n, p);
    //        l.SetPADepolarize(1.0);
    //        Assert.IsTrue(1.0 == l.GetPADepolarize());

    //        l.SetVerbosity(1);
    //        Assert.IsTrue(1 == l.GetVerbosity());
    //    }


    //    [TestMethod]
    //    public void TestSpatialInput()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 3 });
    //        Net.Network.Network n = new Net.Network.Network("test", p);
    //        PALayer<object> l = new PALayer<object>(n, p);

    //        l.SetVerbosity(2);
    //        int[] result;
    //        try
    //        {
    //            result = l.SpatialInput(null);
    //        }
    //        catch (Exception e)
    //        {
    //            Assert.IsTrue(e is NullReferenceException);
    //        }

    //        result = l.SpatialInput(new int[] { });
    //        Assert.IsTrue(result.Length == 0);

    //        try
    //        {
    //            result = l.SpatialInput(new[] { 1, 0, 1, 0, 0 });
    //        }
    //        catch (Exception e)
    //        {
    //            Assert.IsTrue(e is ArgumentException);
    //        }
    //    }

    //    [TestMethod]
    //    public void TestResetMethod()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        var l = Net.Network.Network.CreatePALayer<object>("l1", p).Add(new TemporalMemory());
    //        try
    //        {
    //            l.Reset();
    //            Assert.IsTrue(l.HasTemporalMemory());
    //        }
    //        catch (Exception e)
    //        {
    //            Assert.Fail(e.ToString());
    //        }

    //        var l2 = Net.Network.Network.CreatePALayer<object>("l2", p).Add(new PASpatialPooler());
    //        try
    //        {
    //            l2.Reset();
    //            Assert.IsFalse(l2.HasTemporalMemory());
    //        }
    //        catch (Exception e)
    //        {
    //            Assert.Fail(e.ToString());
    //        }
    //    }

    //    [TestMethod]
    //    public void TestResetRecordNum()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        Layer<int[]> l = (PALayer<int[]>)Net.Network.Network.CreatePALayer<int[]>("l1", p).Add(new TemporalMemory());

    //        l.Subscribe(Observer.Create<IInference>(
    //            output => { Console.WriteLine("output = " + Arrays.ToString(output.GetSdr())); },
    //            Console.WriteLine,
    //            () => { }
    //        ));

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Console.WriteLine("output = " + Arrays.ToString(output.GetSdr()));
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () => { } // completed
    //        ));

    //        //        l.Subscribe(new Observer<Inference>() {
    //        //        public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        //                Console.WriteLine("output = " + Arrays.toString(output.GetSDR()));
    //        //    }
    //        //});

    //        l.Compute(new[] { 2, 3, 4 });
    //        l.Compute(new[] { 2, 3, 4 });
    //        Assert.AreEqual(1, l.GetRecordNum());

    //        l.ResetRecordNum();
    //        Assert.AreEqual(0, l.GetRecordNum());
    //    }

    //    bool _isHalted = false;

    //    [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
    //    public void TestHalt()
    //    {
    //        Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
    //                        FileSensor.Create,
    //                        SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv")));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<IInference> l = new PALayer<IInference>(n);
    //        l.Add(htmSensor);

    //        l.Start();

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {
    //                Assert.IsTrue(l.IsHalted());
    //                _isHalted = true;
    //            } // completed
    //        ));
    //        //    l.Subscribe(new Observer<Inference>() {
    //        //            public void onCompleted()
    //        //{
    //        //    Assert.IsTrue(l.isHalted());
    //        //    isHalted = true;
    //        //}
    //        //public void onError(Throwable e) { Console.WriteLine(e); }
    //        //public void onNext(Inference output) { }
    //        //        });

    //        try
    //        {
    //            l.Halt();
    //            l.GetLayerThread().Wait();
    //            Assert.IsTrue(_isHalted);
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //            Assert.Fail();
    //        }
    //    }

    //    int _trueCount = 0;
    //    [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4reset.csv")]
    //    public void TestReset()
    //    {
    //        Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
    //              FileSensor.Create
    //            , SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4reset.csv")));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<IInference> l = new PALayer<IInference>(n);
    //        l.Add(htmSensor);

    //        l.Start();

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine(">> Called OnNext");
    //                if (l.GetSensor().GetMetaInfo().IsReset())
    //                {
    //                    _trueCount++;
    //                }
    //            }, // next
    //            e => { Console.WriteLine("Error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //        l.Subscribe(new Observer<Inference>() {
    //        //        public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        if (l.GetSensor().GetMetaInfo().isReset())
    //        //        {
    //        //            trueCount++;
    //        //        }
    //        //    }
    //        //});

    //        try
    //        {
    //            l.GetLayerThread().Wait();
    //            Assert.AreEqual(3, _trueCount);
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //            Assert.Fail();
    //        }
    //    }

    //    int _seqResetCount;
    //    [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4seqReset.csv")]
    //    public void TestSequenceChangeReset()
    //    {
    //        Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
    //            FileSensor.Create,
    //                SensorParams.Create(
    //                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4seqReset.csv")));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<int[]> l = new PALayer<int[]>(n);
    //        l.Add(htmSensor);

    //        l.Start();

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine("Called OnNext()");
    //                if (l.GetSensor().GetMetaInfo().IsReset())
    //                {
    //                    _seqResetCount++;
    //                }
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>()
    //        //{
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        if (l.GetSensor().GetMetaInfo().isReset())
    //        //        {
    //        //            seqResetCount++;
    //        //        }
    //        //    }
    //        //});

    //        try
    //        {
    //            l.GetLayerThread().Wait();
    //            Assert.AreEqual(3, _seqResetCount);
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //            Assert.Fail();
    //        }
    //    }

    //    [TestMethod]
    //    public void TestLayerWithObservableInput()
    //    {
    //        Publisher manual = Publisher.GetBuilder()
    //            .AddHeader("timestamp,consumption")
    //            .AddHeader("datetime,float")
    //            .AddHeader("B")
    //            .Build();

    //        Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
    //            ObservableSensor<string[]>.Create,
    //            SensorParams.Create(SensorParams.Keys.Obs, "name", manual));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<int[]> l = new PALayer<int[]>(n);
    //        l.Add(htmSensor);

            

    //        bool hit = false;
    //        int idx = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                hit = true;
    //                switch (idx)
    //                {
    //                    case 0:
    //                        Assert.AreEqual("[0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1]", Arrays.ToString(output.GetSdr()));
    //                        break;
    //                    case 1:
    //                        Assert.AreEqual("[0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]", Arrays.ToString(output.GetSdr()));
    //                        break;
    //                    case 2:
    //                        Assert.AreEqual("[0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]", Arrays.ToString(output.GetSdr()));
    //                        break;
    //                }
    //                ++idx;

    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>() {
    //        //    int idx = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        switch (idx)
    //        //        {
    //        //            case 0:
    //        //                Assert.AreEqual("[0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1]", Arrays.toString(output.GetSDR()));
    //        //                break;
    //        //            case 1:
    //        //                Assert.AreEqual("[0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1]", Arrays.toString(output.GetSDR()));
    //        //                break;
    //        //            case 2:
    //        //                Assert.AreEqual("[1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0]", Arrays.toString(output.GetSDR()));
    //        //                break;
    //        //        }
    //        //        ++idx;
    //        //    }
    //        //});

    //        try
    //        {

    //            string[] entries =
    //            {
    //                "7/2/10 0:00,21.2",
    //                "7/2/10 1:00,34.0",
    //                "7/2/10 2:00,40.4",
    //            };

    //            // Send inputs through the observable
    //            foreach (string s in entries)
    //            {
    //                manual.OnNext(s);
    //            }


    //            l.Start();

    //            Thread.Sleep(1000);

    //            manual.OnComplete();

    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //            Assert.Fail();
    //        }
    //        l.GetLayerThread().Wait();
    //        Assert.IsTrue(hit, "Subscriptions are not hit");
    //    }

    //    [TestMethod]
    //    public void TestLayerWithObservableInputIntegerArray()
    //    {
    //        Publisher manual = Publisher.GetBuilder()
    //            .AddHeader("sdr_in")
    //            .AddHeader("darr")
    //            .AddHeader("B")
    //            .Build();

    //        ISensor sensor = Sensor<ObservableSensor<string[]>>.Create(
    //            ObservableSensor<string[]>.Create,
    //                SensorParams.Create(SensorParams.Keys.Obs, new object[] { "name", manual }));

    //        //Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
    //        //    ObservableSensor<string[]>.Create,
    //        //        SensorParams.Create(SensorParams.Keys.Obs, new object[] { "name", manual }));

    //        Parameters p = Parameters.GetAllDefaultParameters();
    //        p = p.Union(GetArrayTestParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<int[]> l = new PALayer<int[]>(n);
    //        l.Add(htmSensor);

    //        l.Start();

    //        string input = "[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
    //                        + "1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, "
    //                        + "0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, "
    //                        + "1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, "
    //                        + "1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
    //                        + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0]";

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.AreEqual(input, Arrays.ToString((int[])output.GetLayerInput()));
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>()
    //        //{
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        Assert.AreEqual(input, Arrays.toString((int[])output.GetLayerInput()));
    //        //    }
    //        //});

    //        try
    //        {
    //            string[] entries = {
    //              input
    //        };

    //            // Send inputs through the observable
    //            foreach (string s in entries)
    //            {
    //                manual.OnNext(s);
    //            }
    //            manual.OnComplete();
    //            l.GetLayerThread().Wait();
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //            Assert.Fail();
    //        }
    //    }

    //    [TestMethod]
    //    public void TestLayerWithGenericObservable()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        List<int[]> inputs = new List<int[]>();
    //        for (int x = 0; x < 7; x++) inputs.Add(null);
    //        inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        int[] expected0 = new[] { 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };
    //        int[] expected1 = new[] { 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };

    //        Func<ManualInput, ManualInput> addedFunc = mi => mi.SetCustomObject("Interposed: " + Arrays.ToString(mi.GetSdr()));

    //        Net.Network.Network n = Net.Network.Network.Create("Generic Test", p)
    //            .Add(Net.Network.Network.CreateRegion("R1")
    //                .Add(Net.Network.Network.CreatePALayer<IInference>("L1", p)
    //                    .Add(addedFunc)
    //                    .Add(new PASpatialPooler())));

    //        Layer<IInference> l = (PALayer<IInference>)n.Lookup("R1").Lookup("L1");

    //        int test = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Console.WriteLine("sdr = " + Arrays.ToString(output.GetSdr()));
    //                if (test == 0)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()));
    //                    Assert.AreEqual("Interposed: [1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1]", output.GetCustomObject());
    //                }
    //                if (test == 1)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
    //                    Assert.AreEqual("Interposed: [0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1]", output.GetCustomObject());
    //                }
    //                ++test;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<IInference>() {
    //        //    int test = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference i)
    //        //    {
    //        //        Console.WriteLine("sdr = " + Arrays.toString(i.GetSDR()));
    //        //        if (test == 0)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected0, i.GetSDR()));
    //        //            Assert.AreEqual("Interposed: [1, 1, 0, 0, 0, 0, 0, 1]", i.GetCustomObject());
    //        //        }
    //        //        if (test == 1)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected1, i.GetSDR()));
    //        //            Assert.AreEqual("Interposed: [1, 1, 1, 0, 0, 0, 0, 0]", i.GetCustomObject());
    //        //        }
    //        //        ++test;
    //        //    }
    //        //});

    //        // SHOULD RECEIVE BOTH
    //        // Now push some fake data through so that "onNext" is called above
    //        l.Compute(inputs[0]);
    //        l.Compute(inputs[1]);
    //    }

    //    [TestMethod]
    //    public void TestBasicSetupEncoder_UsingSubscribe()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, null, null, null, null);

    //        List<int[]> expected = new List<int[]>();
    //        for (int x = 0; x < 7; x++) expected.Add(null);
    //        //List<int[]> expected = new int[7,8];
    //        expected[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        expected[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        expected[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        expected[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        expected[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        expected[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        expected[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        int seq = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsTrue(Arrays.AreEqual(expected[seq++], output.GetSdr()));
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>() {
    //        //    int seq = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        Assert.IsTrue(Arrays.equals(expected[seq++], output.GetSDR()));
    //        //    }
    //        //});

    //        Map<string, object> inputs = new Map<string, object>();
    //        for (double i = 0; i < 7; i++)
    //        {
    //            inputs.Add("dayOfWeek", i);
    //            l.Compute(inputs);
    //        }
    //    }

    //    [TestMethod]
    //    public void TestBasicSetupEncoder_UsingObserve()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, null, null, null, null);

    //        List<int[]> expected = new List<int[]>();
    //        for (int x = 0; x < 7; x++) expected.Add(null);
    //        expected[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        expected[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        expected[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        expected[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        expected[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        expected[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        expected[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        IObservable<IInference> o = l.Observe();
    //        int seq = 0;
    //        o.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsTrue(Arrays.AreEqual(expected[seq++], output.GetSdr()));
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));
    //        //o.Subscribe(new Observer<Inference>() {
    //        //    int seq = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        Assert.IsTrue(Arrays.equals(expected[seq++], output.GetSDR()));
    //        //    }
    //        //});

    //        Map<string, object> inputs = new Map<string, object>();
    //        for (double i = 0; i < 7; i++)
    //        {
    //            inputs.Add("dayOfWeek", i);
    //            l.Compute(inputs);
    //        }
    //    }

    //    [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
    //    public void TestBasicSetupEncoder_AUTO_MODE()
    //    {
    //        Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
    //            FileSensor.Create,
    //            SensorParams.Create(
    //                SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv")));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<int[]> l = new PALayer<int[]>(n);
    //        l.Add(htmSensor);

    //        List<int[]> expected = new List<int[]>();
    //        for (int x = 0; x < 7; x++) expected.Add(null);

    //        //int[][] expected = new int[][] {
    //        expected.Add(new[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 });
    //        expected.Add(new[] { 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
    //        expected.Add(new[] { 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 });


    //        ///////////////////////////////////////////////////////
    //        //              Test with 2 subscribers              //
    //        ///////////////////////////////////////////////////////

    //        int seq = 0;
    //        l.Observe().Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Console.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.ToString(expected[seq]));
    //                Console.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.ToString(output.GetSdr()));
    //                Assert.IsTrue(Arrays.AreEqual(expected[seq], output.GetSdr()));
    //                seq++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Observe().Subscribe(new IObserver<Inference>() {
    //        //        int seq = 0;
    //        //        public void onCompleted() { }
    //        //        public void onError(Throwable e) { Console.WriteLine(e); }
    //        //        public void onNext(Inference output)
    //        //        {
    //        //            //                Console.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.toString(expected[seq]));
    //        //            //                Console.WriteLine("  seq = " + seq + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.toString(output.GetSDR()));
    //        //            Assert.IsTrue(Arrays.equals(expected[seq], output.GetSDR()));
    //        //            seq++;
    //        //        }
    //        //    });

    //        int seq2 = 0;
    //        l.Observe().Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Console.WriteLine("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.ToString(expected[seq2]));
    //                Console.WriteLine("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.ToString(output.GetSdr()));
    //                Assert.IsTrue(Arrays.AreEqual(expected[seq2], output.GetSdr()));
    //                seq2++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.observe().Subscribe(new Observer<Inference>() {
    //        //    int seq2 = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference output)
    //        //    {
    //        //        //                Console.WriteLine("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",  expected = " + Arrays.toString(expected[seq2]));
    //        //        //                Console.WriteLine("  seq = " + seq2 + ",    recNum = " + output.GetRecordNum() + ",    output = " + Arrays.toString(output.GetSDR()));
    //        //        Assert.IsTrue(Arrays.equals(expected[seq2], output.GetSDR()));
    //        //        seq2++;
    //        //    }
    //        //});

    //        l.Start();

    //        try
    //        {
    //            l.GetLayerThread().Wait();
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //        }
    //    }

    //    /**
    //     * Temporary test to test basic sequence mechanisms
    //     */
    //    [TestMethod]
    //    public void TestBasicSetup_SpatialPooler_MANUAL_MODE()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        List<int[]> inputs = new List<int[]>();
    //        for (int x = 0; x < 7; x++) inputs.Add(null);
    //        inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        int[] expected0 = new int[] { 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };
    //        int[] expected1 = new int[] { 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };

    //        Layer<int[]> l = new PALayer<int[]>(p, null, new PASpatialPooler(), null, null, null);

    //        int test = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                if (test == 0)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()));
    //                }
    //                if (test == 1)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
    //                }
    //                ++test;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));
    //        //l.Subscribe(new Observer<Inference>() {
    //        //    int test = 0;
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine(e); }
    //        //    public void onNext(Inference spatialPoolerOutput)
    //        //    {
    //        //        if (test == 0)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected0, spatialPoolerOutput.GetSDR()));
    //        //        }
    //        //        if (test == 1)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected1, spatialPoolerOutput.GetSDR()));
    //        //        }
    //        //        ++test;
    //        //    }
    //        //});

    //        // Now push some fake data through so that "onNext" is called above
    //        l.Compute(inputs[0]);
    //        l.Compute(inputs[1]);
    //    }

    //    /**
    //     * Temporary test to test basic sequence mechanisms
    //     */
    //    [TestMethod, DeploymentItem("Resources\\days-of-week.csv")]
    //    public void TestBasicSetup_SpatialPooler_AUTO_MODE()
    //    {
    //        Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
    //            FileSensor.Create,
    //            SensorParams.Create(
    //                SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "days-of-week.csv")));

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, true);

    //        HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

    //        Net.Network.Network n = Net.Network.Network.Create("test network", p);
    //        Layer<int[]> l = new PALayer<int[]>(n);
    //        l.Add(htmSensor).Add(new PASpatialPooler());

    //        int[] expected0 = new int[] { 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };
    //        int[] expected1 = new int[] { 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };

    //        int test = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                if (test == 0)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()));
    //                }
    //                if (test == 1)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
    //                }
    //                ++test;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));


    //        //    l.Subscribe(new Observer<Inference>() {
    //        //            int test = 0;

    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine(e); }
    //        //            public void onNext(Inference spatialPoolerOutput)
    //        //{
    //        //    if (test == 0)
    //        //    {
    //        //        Assert.IsTrue(Arrays.equals(expected0, spatialPoolerOutput.GetSDR()));
    //        //    }
    //        //    if (test == 1)
    //        //    {
    //        //        Assert.IsTrue(Arrays.equals(expected1, spatialPoolerOutput.GetSDR()));
    //        //    }
    //        //    ++test;
    //        //}
    //        //        });

    //        l.Start();

    //        try
    //        {
    //            l.GetLayerThread().Wait();
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //        }
    //    }

    //    /**
    //     * Temporary test to test basic sequence mechanisms
    //     */
    //    [TestMethod]
    //    public void TestBasicSetup_TemporalMemory_MANUAL_MODE()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        int[] input1 = new[] { 0, 1, 0, 0, 0, 1, 0, 1, 0, 0, 0, 1, 1, 0, 0, 0, 1, 0, 0, 0 };
    //        int[] input2 = new[] { 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 };
    //        int[] input3 = new[] { 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
    //        int[] input4 = new[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 1, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
    //        int[] input5 = new[] { 0, 0, 1, 1, 0, 0, 0, 0, 1, 0, 0, 0, 1, 0, 0, 0, 0, 1, 1, 0 };
    //        int[] input6 = new[] { 0, 0, 1, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };
    //        int[] input7 = new[] { 0, 1, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0 };
    //        int[][] inputs = { input1, input2, input3, input4, input5, input6, input7 };


    //        Layer<int[]> l = new PALayer<int[]>(p, null, null, new TemporalMemory(), null, null);

    //        int timeUntilStable = 600;

    //        int test = 0;
    //        int seq = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                if (seq / 7 >= timeUntilStable)
    //                {
    //                    Console.WriteLine("seq: " + (seq) + "  --> " + (test) + "  output = " + Arrays.ToString(output.GetSdr()) +
    //                        ", \t\t\t\t cols = " + Arrays.ToString(SDR.AsColumnIndices(output.GetSdr(), l.GetConnections().GetCellsPerColumn())));
    //                    Assert.IsTrue(output.GetSdr().Length >= 8);
    //                }

    //                if (test == 6) test = 0;
    //                else test++;
    //                seq++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //    l.Subscribe(new Observer<Inference>() {
    //        //            int test = 0;
    //        //            int seq = 0;

    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine(e); }
    //        //            public void onNext(Inference output)
    //        //{
    //        //    if (seq / 7 >= timeUntilStable)
    //        //    {
    //        //        //                    Console.WriteLine("seq: " + (seq) + "  --> " + (test) + "  output = " + Arrays.toString(output.GetSDR()) +
    //        //        //                        ", \t\t\t\t cols = " + Arrays.toString(SDR.asColumnIndices(output.GetSDR(), l.GetConnections().GetCellsPerColumn())));
    //        //        Assert.IsTrue(output.GetSDR().Length >= 8);
    //        //    }

    //        //    if (test == 6) test = 0;
    //        //    else test++;
    //        //    seq++;
    //        //}
    //        //        });

    //        // Now push some warm up data through so that "onNext" is called above
    //        for (int j = 0; j < timeUntilStable; j++)
    //        {
    //            for (int i = 0; i < inputs.Length; i++)
    //            {
    //                l.Compute(inputs[i]);
    //            }
    //        }

    //        for (int j = 0; j < 2; j++)
    //        {
    //            for (int i = 0; i < inputs.Length; i++)
    //            {
    //                l.Compute(inputs[i]);
    //            }
    //        }
    //    }

    //    [TestMethod]
    //    public void TestBasicSetup_SPandTM()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        List<int[]> inputs = new List<int[]>();
    //        for (int x = 0; x < 7; x++) inputs.Add(null);
    //        inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        Layer<int[]> l = new PALayer<int[]>(p, null, new PASpatialPooler(), new TemporalMemory(), null, null);

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                Assert.AreEqual(48, output.GetSdr().Length);
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>()
    //        //{
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { }
    //        //    public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    Assert.AreEqual(0, i.GetSDR().Length);
    //        //}
    //        //});

    //        // Now push some fake data through so that "onNext" is called above
    //        l.Compute(inputs[0]);
    //        l.Compute(inputs[1]);
    //    }

    //    [TestMethod]
    //    public void TestSpatialPoolerPrimerDelay()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        List<int[]> inputs = new List<int[]>();
    //        for (int x = 0; x < 7; x++) inputs.Add(null);
    //        inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        int[] expected0 = new int[] { 1, 1, 1, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };
    //        int[] expected1 = new int[] { 0, 1, 1, 0, 1, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 1, 1 };

    //        // First test without prime directive :-P
    //        Layer<int[]> l = new PALayer<int[]>(p, null, new PASpatialPooler(), null, null, null);

    //        int test = 0;
    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                if (test == 0)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected0, output.GetSdr()), "Arrays are not equal test0");
    //                }
    //                if (test == 1)
    //                {
    //                    Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()), "Arrays are not equal test1");
    //                }
    //                ++test;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>() {
    //        //        int test = 0;

    //        //        public void onCompleted() { }
    //        //        public void onError(Throwable e) { Console.WriteLine(e); }
    //        //        public void onNext(Inference i)
    //        //    {
    //        //        if (test == 0)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected0, i.GetSDR()));
    //        //        }
    //        //        if (test == 1)
    //        //        {
    //        //            Assert.IsTrue(Arrays.equals(expected1, i.GetSDR()));
    //        //        }
    //        //        ++test;
    //        //    }
    //        //});

    //        // SHOULD RECEIVE BOTH
    //        // Now push some fake data through so that "onNext" is called above
    //        l.Compute(inputs[0]);
    //        l.Compute(inputs[1]);

    //        // --------------------------------------------------------------------------------------------

    //        // NOW TEST WITH prime directive
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42)); // due to static RNG we have to reset the sequence
    //        p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, 1);

    //        Layer<int[]> l2 = new PALayer<int[]>(p, null, new PASpatialPooler(), null, null, null);

    //        l2.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                // should be one and only onNext() called
    //                Assert.IsTrue(Arrays.AreEqual(expected1, output.GetSdr()));
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l2.Subscribe(new Observer<Inference>() {
    //        //        public void onCompleted() { }
    //        //        public void onError(Throwable e) { Console.WriteLine(e); }
    //        //        public void onNext(Inference i)
    //        //{
    //        //    // should be one and only onNext() called
    //        //    Assert.IsTrue(Arrays.equals(expected1, i.GetSDR()));
    //        //}
    //        //});

    //        // SHOULD RECEIVE BOTH
    //        // Now push some fake data through so that "onNext" is called above
    //        l2.Compute(inputs[0]);
    //        l2.Compute(inputs[1]);
    //    }

    //    /**
    //     * Simple test to verify data gets passed through the {@link CLAClassifier}
    //     * configured within the chain of components.
    //     */
    //    [TestMethod]
    //    public void TestBasicClassifierSetup()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                Assert.AreEqual(48, output.GetSdr().Length);
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //    l.Subscribe(new Observer<Inference>()
    //        //    {
    //        //        public void onCompleted() { }
    //        //        public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //        public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    Assert.AreEqual(36, i.GetSDR().Length);
    //        //}
    //        //    });

    //        // Now push some fake data through so that "onNext" is called above
    //        Map<string, object> multiInput = new Map<string, object>();
    //        multiInput.Add("dayOfWeek", 0.0);
    //        l.Compute(multiInput);
    //    }

    //    /**
    //     * The {@link PASpatialPooler} sometimes needs to have data run through it
    //     * prior to passing the data on to subsequent algorithmic components. This
    //     * tests the ability to specify exactly the number of input records for
    //     * the PASpatialPooler to consume before passing records on.
    //     */
    //    [TestMethod]
    //    public void TestMoreComplexSpatialPoolerPriming()
    //    {
    //        int PRIME_COUNT = 35;
    //        int NUM_CYCLES = 20;
    //        int INPUT_GROUP_COUNT = 7; // Days of Week
    //        _total = 0;

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                _total++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //    l.Subscribe(new Observer<Inference>()
    //        //    {
    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //            public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    TOTAL++;
    //        //}
    //        //        });

    //        // Now push some fake data through so that "onNext" is called above
    //        Map<string, object> multiInput = new Map<string, object>();
    //        for (int i = 0; i < NUM_CYCLES; i++)
    //        {
    //            for (double j = 0; j < INPUT_GROUP_COUNT; j++)
    //            {
    //                multiInput.Add("dayOfWeek", j);
    //                l.Compute(multiInput);
    //            }
    //        }

    //        // Assert we can accurately specify how many inputs to "prime" the spatial pooler
    //        // and subtract that from the total input to get the total entries sent through
    //        // the event chain from bottom to top.
    //        Assert.AreEqual((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT, _total);
    //    }

    //    /**
    //     * Tests the ability for multiple subscribers to receive copies of
    //     * a given {@link PALayer}'s computed values.
    //     */
    //    [TestMethod]
    //    public void Test2NdAndSubsequentSubscribersPossible()
    //    {
    //        int PRIME_COUNT = 35;
    //        int NUM_CYCLES = 50;
    //        int INPUT_GROUP_COUNT = 7; // Days of Week
    //        _total = 0;

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);

    //        List<int[]> inputs = new List<int[]>();
    //        for (int x = 0; x < 7; x++) inputs.Add(null);
    //        inputs[0] = new[] { 1, 1, 0, 0, 0, 0, 0, 1 };
    //        inputs[1] = new[] { 1, 1, 1, 0, 0, 0, 0, 0 };
    //        inputs[2] = new[] { 0, 1, 1, 1, 0, 0, 0, 0 };
    //        inputs[3] = new[] { 0, 0, 1, 1, 1, 0, 0, 0 };
    //        inputs[4] = new[] { 0, 0, 0, 1, 1, 1, 0, 0 };
    //        inputs[5] = new[] { 0, 0, 0, 0, 1, 1, 1, 0 };
    //        inputs[6] = new[] { 0, 0, 0, 0, 0, 1, 1, 1 };

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                _total++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //    l.Subscribe(new Observer<Inference>()
    //        //    {
    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //            public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    TOTAL++;
    //        //}
    //        //        });

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                _total++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //        l.Subscribe(new Observer<Inference>() {
    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //            public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    TOTAL++;
    //        //}
    //        //        });

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                _total++;
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //        l.Subscribe(new Observer<Inference>() {
    //        //            public void onCompleted() { }
    //        //            public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //            public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    TOTAL++;
    //        //}
    //        //        });

    //        // Now push some fake data through so that "onNext" is called above
    //        Map<string, object> multiInput = new Map<string, object>();
    //        for (int i = 0; i < NUM_CYCLES; i++)
    //        {
    //            for (double j = 0; j < INPUT_GROUP_COUNT; j++)
    //            {
    //                multiInput.Add("dayOfWeek", j);
    //                l.Compute(multiInput);
    //            }
    //        }

    //        int NUM_SUBSCRIBERS = 3;
    //        // Assert we can accurately specify how many inputs to "prime" the spatial pooler
    //        // and subtract that from the total input to get the total entries sent through
    //        // the event chain from bottom to top.
    //        Assert.AreEqual(((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT) * NUM_SUBSCRIBERS, _total);
    //    }

    //    [TestMethod]
    //    public void TestGetAllPredictions()
    //    {
    //        int PRIME_COUNT = 35;
    //        int NUM_CYCLES = 120;
    //        int INPUT_GROUP_COUNT = 7; // Days of Week
    //        _total = 0;

    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(43));

    //        p.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, PRIME_COUNT);

    //        int cellsPerColumn = (int)p.GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN);
    //        Assert.IsTrue(cellsPerColumn > 0);

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        PALayer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);
    //        l.SetPADepolarize(0.0);

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Assert.IsNotNull(output);
    //                _total++;
    //                if (l.GetPreviousPredictiveCells() != null)
    //                {
    //                    //UNCOMMENT TO VIEW STABILIZATION OF PREDICTED FIELDS
    //                    //                    Console.WriteLine("recordNum: " + i.GetRecordNum() + "  Day: " + ((Dictionary<String, Object>)i.GetLayerInput()).Get("dayOfWeek") + "  -  " +
    //                    //                       Arrays.toString(ArrayUtils.where(l.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1)) +
    //                    //                         "   -   " + Arrays.toString(SDR.cellsAsColumnIndices(l.GetPreviousPredictiveCells(), cellsPerColumn)));
    //                }
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //    l.Subscribe(new Observer<Inference>()
    //        //    {
    //        //        public void onCompleted() { }
    //        //        public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //        public void onNext(Inference i)
    //        //{
    //        //    Assert.IsNotNull(i);
    //        //    TOTAL++;

    //        //    //                if(l.GetPreviousPredictiveCells() != null) {
    //        //    //UNCOMMENT TO VIEW STABILIZATION OF PREDICTED FIELDS
    //        //    //                    Console.WriteLine("recordNum: " + i.GetRecordNum() + "  Day: " + ((Dictionary<String, Object>)i.GetLayerInput()).Get("dayOfWeek") + "  -  " +
    //        //    //                       Arrays.toString(ArrayUtils.where(l.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1)) +
    //        //    //                         "   -   " + Arrays.toString(SDR.cellsAsColumnIndices(l.GetPreviousPredictiveCells(), cellsPerColumn)));
    //        //    //                }
    //        //}
    //        //    });

    //        // Now push some fake data through so that "onNext" is called above
    //        Map<string, object> multiInput = new Map<string, object>();
    //        for (int c = 0; c < NUM_CYCLES; c++)
    //        {
    //            for (double j = 0; j < INPUT_GROUP_COUNT; j++)
    //            {
    //                multiInput.Add("dayOfWeek", j);
    //                l.Compute(multiInput);
    //            }
    //            l.Reset();
    //        }

    //        // Assert we can accurately specify how many inputs to "prime" the spatial pooler
    //        // and subtract that from the total input to get the total entries sent through
    //        // the event chain from bottom to top.
    //        Assert.AreEqual((NUM_CYCLES * INPUT_GROUP_COUNT) - PRIME_COUNT, _total);

    //        double[] all = l.GetAllPredictions("dayOfWeek", 1);
    //        double highestVal = double.NegativeInfinity;
    //        int highestIdx = -1;
    //        int i = 0;
    //        foreach (double d in all)
    //        {
    //            if (d > highestVal)
    //            {
    //                highestIdx = i;
    //                highestVal = d;
    //            }
    //            i++;
    //        }

    //        Assert.AreEqual(highestIdx, l.GetMostProbableBucketIndex("dayOfWeek", 1));
    //        Assert.AreEqual(7, l.GetAllPredictions("dayOfWeek", 1).Length);

    //        var activeColumnsFfwd = ArrayUtils.Where(l.GetFeedForwardActiveColumns(), ArrayUtils.WHERE_1);
    //        var prevPredictedCells = SDR.CellsAsColumnIndices(l.GetPreviousPredictiveCells(), cellsPerColumn);
    //        Debug.WriteLine("A: " + Arrays.ToString(activeColumnsFfwd));
    //        Debug.WriteLine("P: " + Arrays.ToString(prevPredictedCells));

    //        Assert.IsTrue(Arrays.AreEqual(activeColumnsFfwd, prevPredictedCells));
    //    }

    //    /**
    //     * Test that a given layer can return an {@link Observable} capable of
    //     * service multiple subscribers.
    //     */
    //    [TestMethod]
    //    public void TestObservableRetrieval()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

    //        MultiEncoder me = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build();
    //        Layer<IDictionary<string, object>> l = new PALayer<IDictionary<string, object>>(p, me, new PASpatialPooler(), new TemporalMemory(), true, null);

    //        List<int[]> emissions = new List<int[]>();
    //        IObservable<IInference> o = l.Observe();

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine("Observing layer subscription");
    //            }, // next
    //            e =>
    //            {
    //                Console.WriteLine("error: " + e.Message);
    //                Console.WriteLine(e);
    //            }, // error
    //            () =>
    //            {

    //            } // completed
    //            ));

    //        o.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine("Observing layer.observe() subscription");
    //                emissions.Add(l.GetFeedForwardActiveColumns());
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));
    //        o.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine("Observing layer.observe() subscription");
    //                emissions.Add(l.GetFeedForwardActiveColumns());
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));
    //        o.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                Debug.WriteLine("Observing layer.observe() subscription");
    //                emissions.Add(l.GetFeedForwardActiveColumns());
    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //o.Subscribe(new Subscriber<Inference>()
    //        //{
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //    public void onNext(Inference i)
    //        //    {
    //        //        emissions.Add(l.GetFeedForwardActiveColumns());
    //        //    }
    //        //});
    //        //o.Subscribe(new Subscriber<Inference>() {
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //    public void onNext(Inference i)
    //        //    {
    //        //        emissions.Add(l.GetFeedForwardActiveColumns());
    //        //    }
    //        //});
    //        //o.Subscribe(new Subscriber<Inference>() {
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //    public void onNext(Inference i)
    //        //    {
    //        //        emissions.Add(l.GetFeedForwardActiveColumns());
    //        //    }
    //        //});

    //        Dictionary<string, object> multiInput = new Dictionary<string, object>();
    //        multiInput.Add("dayOfWeek", 0.0);
    //        l.Compute(multiInput);

    //        Assert.AreEqual(3, emissions.Count);
    //        int[] e1 = emissions[0];
    //        foreach (int[] ia in emissions)
    //        {
    //            Assert.IsTrue(ia == e1);//test same object propagated
    //        }
    //    }

    //    /**
    //     * Simple test to verify data gets passed through the {@link CLAClassifier}
    //     * configured within the chain of components.
    //     */
    //    bool flowReceived = false;
    //    [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.csv")]
    //    public void TestFullLayerFluentAssembly()
    //    {
    //        Parameters p = NetworkTestHarness.GetParameters().Copy();
    //        p = p.Union(NetworkTestHarness.GetHotGymTestEncoderParams());
    //        p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
    //        p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 2048 });
    //        p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 200);
    //        p.SetParameterByKey(Parameters.KEY.INHIBITION_RADIUS, 50);
    //        p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);

    //        //        Console.WriteLine(p);

    //        p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
    //        p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, 3);
    //        p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_USE_MOVING_AVG, true);
            
    //        Anomaly anomalyComputer = Anomaly.Create(p);

    //        Layer<object> l = (Layer<object>)Net.Network.Network.CreatePALayer<object>("TestLayer", p)
    //            .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
    //            .Add(anomalyComputer)
    //            .Add(new TemporalMemory())
    //            .Add(new PASpatialPooler())
    //            .Add(Sensor<FileInfo>.Create(
    //                FileSensor.Create,
    //                SensorParams.Create(
    //                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.csv"))));

    //        l.GetConnections().PrintParameters();

    //        l.Subscribe(Observer.Create<IInference>(
    //            output =>
    //            {
    //                if (flowReceived) return; // No need to set this value multiple times

    //                flowReceived = output.GetClassifiers().Count == 4 &&
    //                    output.GetClassifiers().Get("timestamp") != null &&
    //                        output.GetClassifiers().Get("consumption") != null;

    //            }, // next
    //            e => { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); },  // error
    //            () =>
    //            {

    //            } // completed
    //        ));

    //        //l.Subscribe(new Observer<Inference>()
    //        //{
    //        //    public void onCompleted() { }
    //        //    public void onError(Throwable e) { Console.WriteLine("error: " + e.Message); Console.WriteLine(e); }
    //        //    public void onNext(Inference i)
    //        //{
    //        //    if (flowReceived) return; // No need to set this value multiple times

    //        //    flowReceived = i.GetClassifiers().size() == 4 &&
    //        //        i.GetClassifiers().Get("timestamp") != null &&
    //        //            i.GetClassifiers().Get("consumption") != null;
    //        //}
    //        //});

    //        l.Start();

    //        try
    //        {
    //            l.GetLayerThread().Wait();
    //            Assert.IsTrue(flowReceived);
    //        }
    //        catch (Exception e)
    //        {
    //            Console.WriteLine(e);
    //        }
    //    }

    //    private Parameters GetArrayTestParams()
    //    {
    //        Map<string, Map<string, object>> fieldEncodings = SetupMap(
    //            null,
    //            884, // n
    //            0, // w
    //            0, 0, 0, 0, null, null, null,
    //            "sdr_in", "darr", "SDRPassThroughEncoder");
    //        Parameters p = Parameters.Empty();
    //        p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
    //        return p;
    //    }

    //    private Map<string, Map<string, object>> SetupMap(
    //        Map<string, Map<string, object>> map,
    //        int n, int w, double min, double max, double radius, double resolution, bool? periodic,
    //        bool? clip, bool? forced, string fieldName, string fieldType, string encoderType)
    //    {

    //        if (map == null)
    //        {
    //            map = new Map<string, Map<string, object>>();
    //        }
    //        Map<string, object> inner = null;
    //        if (!map.TryGetValue(fieldName, out inner))
    //        {
    //            map.Add(fieldName, inner = new Map<string, object>());
    //        }

    //        inner.Add("n", n);
    //        inner.Add("w", w);
    //        inner.Add("minVal", min);
    //        inner.Add("maxVal", max);
    //        inner.Add("radius", radius);
    //        inner.Add("resolution", resolution);

    //        if (periodic != null) inner.Add("periodic", periodic);
    //        if (clip != null) inner.Add("clip", clip);
    //        if (forced != null) inner.Add("forced", forced);
    //        if (fieldName != null) inner.Add("fieldName", fieldName);
    //        if (fieldType != null) inner.Add("fieldType", fieldType);
    //        if (encoderType != null) inner.Add("encoderType", encoderType);

    //        return map;
    //    }
    //}
}