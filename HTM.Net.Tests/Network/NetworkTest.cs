using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text.RegularExpressions;
using System.Threading;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class NetworkTest
    {
        private static int[][] dayMap = new int[][]
        {
            new int[] {1, 1, 0, 0, 0, 0, 0, 1}, // Sunday
            new int[] {1, 1, 1, 0, 0, 0, 0, 0}, // Monday
            new int[] {0, 1, 1, 1, 0, 0, 0, 0}, // Tuesday
            new int[] {0, 0, 1, 1, 1, 0, 0, 0}, // Wednesday
            new int[] {0, 0, 0, 1, 1, 1, 0, 0}, // Thursday
            new int[] {0, 0, 0, 0, 1, 1, 1, 0}, // Friday
            new int[] {0, 0, 0, 0, 0, 1, 1, 1}, // Saturday
        };

        private Func<IInference, int, int> dayOfWeekPrintout = CreateDayOfWeekInferencePrintout();

        [TestMethod]
        public void TestResetMethod()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Net.Network.Network network = new Net.Network.Network("ResetTestNetwork", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("l1", p).Add(new TemporalMemory())));
            try
            {
                network.Reset();
                Assert.IsTrue(network.Lookup("r1").Lookup("l1").HasTemporalMemory());
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            network = new Net.Network.Network("ResetMethodTestNetwork", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("l1", p).Add(new SpatialPooler())));
            try
            {
                network.Reset();
                Assert.IsFalse(network.Lookup("r1").Lookup("l1").HasTemporalMemory());
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
            Net.Network.Network network = new Net.Network.Network("ResetRecordNumNetwork", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("l1", p).Add(new TemporalMemory())));
            network.Observe().Subscribe(Observer.Create<IInference>(
                output =>
                {

                },
                e => Console.WriteLine(e),
                () => { }));
            //    network.Observe().Subscribe(new Observer.Create<Inference>() {
            //     public void onCompleted() { }
            //     public void onError(Throwable e) { Console.WriteLine(e); }
            //     public void onNext(Inference output)
            //    {
            //        //                System.out.Println("output = " + Arrays.toString(output.GetSDR()));
            //    }
            //});

            network.Compute(new[] { 2, 3, 4 });
            network.Compute(new[] { 2, 3, 4 });
            Assert.AreEqual(1, network.Lookup("r1").Lookup("l1").GetRecordNum());

            network.ResetRecordNum();
            Assert.AreEqual(0, network.Lookup("r1").Lookup("l1").GetRecordNum());
        }

        [TestMethod]
        public void TestAdd()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Net.Network.Network network = Net.Network.Network.Create("test", NetworkTestHarness.GetParameters());

            // Add Layers to regions but regions not yet added to Network
            Region r1 = Net.Network.Network.CreateRegion("r1").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r2 = Net.Network.Network.CreateRegion("r2").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r3 = Net.Network.Network.CreateRegion("r3").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r4 = Net.Network.Network.CreateRegion("r4").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r5 = Net.Network.Network.CreateRegion("r5").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));

            Region[] regions = { r1, r2, r3, r4, r5 };
            foreach (Region r in regions)
            {
                Assert.IsNull(network.Lookup(r.GetName()));
            }

            // Add the regions to the network
            foreach (Region r in regions)
            {
                network.Add(r);
            }

            string[] names = { "r1", "r2", "r3", "r4", "r5" };
            int i = 0;
            foreach (Region r in regions)
            {
                Assert.IsNotNull(network.Lookup(r.GetName()));
                Assert.AreEqual(names[i++], r.GetName());
            }
        }

        [TestMethod]
        public void TestConnect()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            Net.Network.Network network = Net.Network.Network.Create("test", NetworkTestHarness.GetParameters());

            Region r1 = Net.Network.Network.CreateRegion("r1").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r2 = Net.Network.Network.CreateRegion("r2").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r3 = Net.Network.Network.CreateRegion("r3").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r4 = Net.Network.Network.CreateRegion("r4").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));
            Region r5 = Net.Network.Network.CreateRegion("r5").Add(Net.Network.Network.CreateLayer("l", p).Add(new SpatialPooler()));

            try
            {
                network.Connect("r1", "r2");
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Region with name: r2 not added to Network.", e.Message);
            }

            Region[] regions = { r1, r2, r3, r4, r5 };
            foreach (Region r in regions)
            {
                network.Add(r);
            }

            for (int i = 1; i < regions.Length; i++)
            {
                try
                {
                    network.Connect(regions[i - 1].GetName(), regions[i].GetName());
                }
                catch (Exception e)
                {
                    Assert.Fail(e.ToString());
                }
            }

            Region upstream = r1;
            Region tail = r1;
            while ((tail = tail.GetUpstreamRegion()) != null)
            {
                upstream = tail;
            }

            // Assert that the connect method sets the upstream region on all regions
            Assert.AreEqual(regions[4], upstream);

            Region downstream = r5;
            Region head = r5;
            while ((head = head.GetDownstreamRegion()) != null)
            {
                downstream = head;
            }

            // Assert that the connect method sets the upstream region on all regions
            Assert.AreEqual(regions[0], downstream);
            Assert.AreEqual(network.GetHead(), downstream);
        }

        private string _onCompleteStr;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestBasicNetworkHaltGetsOnComplete()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            // Create a Network
            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))));

            List<string> lines = new List<string>();

            // Listen to network emissions<
            network.Observe().Subscribe(
                output =>
                {
                    //Console.WriteLine(Arrays.ToString(output.GetSdr()));
                    //Console.WriteLine(output.GetRecordNum() + "," +
                    //    output.GetClassifierInput()["consumption"].Get("inputValue") + "," + output.GetAnomalyScore());

                    lines.Add(output.GetRecordNum() + "," +
                              output.GetClassifierInput()["consumption"].GetAsString("inputValue") + "," +
                              output.GetAnomalyScore());

                    if (output.GetRecordNum() == 9)
                    {
                        network.Halt();
                    }
                }, Console.WriteLine, () =>
                {
                    _onCompleteStr = "On completed reached!";
                });


            // Start the network
            network.Start();

            // Test network output
            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Assert.AreEqual(10, lines.Count);
            int i = 0;
            foreach (string l in lines)
            {
                string[] sa = Regex.Split(l, "[\\s]*\\,[\\s]*");// l.Split('|');//"[\\s]*\\,[\\s]*"
                Assert.AreEqual(3, sa.Length);
                Assert.AreEqual(i++, int.Parse(sa[0]));
            }

            Assert.AreEqual("On completed reached!", _onCompleteStr);
        }

        private string _onCompleteStr2;
        //[TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestBasicNetworkHalt_ThenRestart()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            // Create a Network
            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly.Csv"))))));

            List<string> lines = new List<string>();

            Stopwatch sw = new Stopwatch();
            List<TimeSpan> timings = new List<TimeSpan>();

            // Listen to network emissions
            network.Observe().Subscribe(output =>
            {
                timings.Add(sw.Elapsed);
                string sToAdd = output.GetRecordNum() + "," +
                                output.GetClassifierInput()["consumption"].GetAsString("inputValue") + "," +
                                output.GetAnomalyScore();
                //Console.WriteLine(Arrays.ToString(output.GetSdr()));
                //Console.WriteLine("> " + sToAdd);
                lines.Add(sToAdd);


                if (output.GetRecordNum() == 9)
                {
                    network.Halt();
                }
            }, Console.WriteLine, () =>
            {
                _onCompleteStr2 = "On completed reached!";
                sw.Stop();
            });

            // Start the network
            sw.Start();
            network.Start();

            // Test network output
            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Assert.AreEqual(10, lines.Count);
            int i = 0;
            Console.WriteLine("Start printing...");
            foreach (string l in lines)
            {
                Console.WriteLine(l);
                string[] sa = Regex.Split(l, "[\\s]*\\,[\\s]*");// l.Split('|');//"[\\s]*\\,[\\s]*"
                Assert.AreEqual(3, sa.Length);
                Assert.AreEqual(i++, int.Parse(sa[0]));
            }

            Assert.AreEqual("On completed reached!", _onCompleteStr2);

            Debug.WriteLine("Fastest call: {0}", timings.Min());
            Debug.WriteLine("Slowest call: {0}", timings.Max());
            Debug.WriteLine("Average call: {0}", timings.Average(t => t.TotalSeconds));

            ///////////////////////
            //     Now Restart   //
            ///////////////////////
            _onCompleteStr2 = null;

            // Listen to network emissions
            network.Observe().Subscribe(
                output =>
                {
                    //Console.WriteLine(Arrays.ToString(output.GetSdr()));

                    string sToAdd = output.GetRecordNum() + "," +
                                    output.GetClassifierInput().Get("consumption").GetAsString("inputValue") + "," +
                                    output.GetAnomalyScore();
                    //Console.WriteLine("2 > " + sToAdd);
                    lines.Add(sToAdd);

                    if (output.GetRecordNum() == 19)
                    {
                        network.Halt();
                    }
                },
                Console.WriteLine,
                () =>
                {
                    _onCompleteStr2 = "On completed reached!";
                });


            network.Restart();

            // Test network output
            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }


            Assert.AreEqual(20, lines.Count);

            i = 0;
            Console.WriteLine("Start printing...");
            foreach (String l in lines)
            {
                Console.WriteLine(l);
                String[] sa = Regex.Split(l, "[\\s]*\\,[\\s]*");
                Assert.AreEqual(3, sa.Length);
                Assert.AreEqual(i++, int.Parse(sa[0]));
            }


            Assert.AreEqual("On completed reached!", _onCompleteStr2);
        }

        bool expectedDataFlag = true;
        String failMessage;
        //[TestMethod]
        public void TestBasicNetworkHalt_ThenRestart_TighterExpectation()
        {
            const int NUM_CYCLES = 600;
            const int INPUT_GROUP_COUNT = 7; // Days of Week

            ///////////////////////////////////////
            //   Run until CYCLE 284, then halt  //
            ///////////////////////////////////////
            Net.Network.Network network = GetLoadedDayOfWeekNetwork();
            int cellsPerCol = (int)network.GetParameters().GetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN);

            network.Observe().Subscribe(
                inf =>
                {
                    /** see {@link #createDayOfWeekInferencePrintout()} */
                    int cycle = dayOfWeekPrintout(inf, cellsPerCol);
                    if (cycle == 284)
                    {
                        Console.WriteLine("halting publisher = " + network.GetPublisher());
                        network.Halt();
                    }
                },
                Console.WriteLine,
                () => { });

            Publisher pub = network.GetPublisher();

            network.Start();

            int cycleCount = 0;
            for (; cycleCount < NUM_CYCLES; cycleCount++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    pub.OnNext("" + j);
                }

                network.Reset();

                if (cycleCount == 284)
                {
                    break;
                }
            }

            // Test network output
            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait(2000);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            // Announce new start
            Console.WriteLine("\n\n\n Network Restart \n\n\n");


            ///////////////////////
            //     Now Restart   //
            ///////////////////////

            // 1. Re-Attach Observer
            // 2. Restart Network

            network.Observe().Subscribe(
                inf =>
                {
                    /** see {@link #createDayOfWeekInferencePrintout()} */

                    dayOfWeekPrintout(inf, cellsPerCol);

                    ////////////////////////////////////////////////////////////
                    // Ensure the records pick up precisely where we left off //
                    ////////////////////////////////////////////////////////////
                    if (inf.GetRecordNum() == 1975)
                    {
                        expectedDataFlag = true;
                    }
                },
                Console.WriteLine,
                () => { });


            network.Halt();
            try
            {
                network.Lookup("r1").Lookup("1").GetLayerThread().Wait(3000);
                // Add a little more wait time
                Thread.Sleep(3000);
            }
            catch (Exception e) { Console.WriteLine(e); }
            network.Restart();

            Publisher newPub = network.GetPublisher();

            // Assert that we have a new Publisher being created in the background upon restart()
            Assert.IsFalse(pub == newPub, "publisher is not recreated");

            for (; cycleCount < NUM_CYCLES; cycleCount++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    newPub.OnNext("" + j);
                }
                network.Reset();
            }

            newPub.OnComplete();

            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            if (!expectedDataFlag)
            {
                Assert.Fail(failMessage);
            }
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestBasicNetworkRunAWhileThenHalt()
        {
            _onCompleteStr = null;

            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            // Create a Network
            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path("rec-center-hourly.Csv"))))));

            List<string> lines = new List<string>();

            Stopwatch sw = new Stopwatch();
            List<TimeSpan> timings = new List<TimeSpan>();

            // Listen to network emissions
            network.Observe().Subscribe(output =>
            {
                timings.Add(sw.Elapsed);
                //                System.out.Println(Arrays.toString(i.GetSDR()));
                //                System.out.Println(i.GetRecordNum() + "," + 
                //                    i.GetClassifierInput().Get("consumption").Get("inputValue") + "," + i.GetAnomalyScore());
                lines.Add(output.GetRecordNum() + "|" +
                  output.GetClassifierInput()["consumption"].GetAsString("inputValue") + "|" + output.GetAnomalyScore());


                if (output.GetRecordNum() == 1000)
                {
                    network.Halt();
                }
            }, Console.WriteLine, () =>
            {
                _onCompleteStr = "On completed reached!";
                sw.Stop();
            });

            // Start the network
            sw.Start();
            network.Start();

            // Test network output
            try
            {
                Region r1 = network.Lookup("r1");
                r1.Lookup("1").GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            Assert.AreEqual(1001, lines.Count);
            int i = 0;
            foreach (string l in lines)
            {
                string[] sa = l.Split('|');//"[\\s]*\\,[\\s]*"
                Assert.AreEqual(3, sa.Length);
                Assert.AreEqual(i++, int.Parse(sa[0]));
            }

            Assert.AreEqual("On completed reached!", _onCompleteStr);

            Debug.WriteLine("Fastest call: {0}", timings.Min());
            Debug.WriteLine("Slowest call: {0}", timings.Max());
            Debug.WriteLine("Average call: {0}", timings.Average(t => t.TotalSeconds));
        }


        ManualInput _netInference = null;
        ManualInput _topInference;
        ManualInput _bottomInference;
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestRegionHierarchies()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p.SetPotentialRadius(16);
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("2", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())))
                .Add(Net.Network.Network.CreateRegion("r2")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))))
                .Connect("r1", "r2");

            Region r1 = network.Lookup("r1");
            Region r2 = network.Lookup("r2");

            int r1Hits = 0, r2Hits = 0;
            network.Observe().Subscribe(output =>
            {
                //Debug.WriteLine("Hit network observe() subscription");
                _netInference = (ManualInput)output;
                if (r1.GetHead().GetInference().GetPredictiveCells().Count > 0 &&
                    r2.GetHead().GetInference().GetPredictiveCells().Count > 0)
                {
                    Console.WriteLine("network observe() -> Halt network");
                    network.Halt();
                }
            }, Console.WriteLine, () => { });

            r1.Observe().Subscribe(output =>
            {
                //Debug.WriteLine("Hit region1 observe() subscription");
                _topInference = (ManualInput)output;
                r1Hits++;
            }, Console.WriteLine, () => { });

            r2.Observe().Subscribe(output =>
            {
                //Debug.WriteLine("Hit region2 observe() subscription");
                _bottomInference = (ManualInput)output;
                r2Hits++;
            }, Console.WriteLine, () => { });

            network.Start();

            // Let run for 5 secs.
            try
            {
                r2.Lookup("1").GetLayerThread().Wait();//5000);

                Assert.IsTrue(r1Hits > 0);
                Assert.AreEqual(r1Hits, r2Hits);

                Console.WriteLine("Hits R1 = {0}, R2 = {1}", r1Hits, r2Hits);
                Console.WriteLine("top ff = " + Arrays.ToString(_topInference.GetFeedForwardSparseActives()));
                Console.WriteLine("bot ff = " + Arrays.ToString(_bottomInference.GetFeedForwardSparseActives()));
                Console.WriteLine("top pred = " + Arrays.ToString(_topInference.GetPredictiveCells()));
                Console.WriteLine("bot pred = " + Arrays.ToString(_bottomInference.GetPredictiveCells()));
                Console.WriteLine("top active = " + Arrays.ToString(_topInference.GetActiveCells()));
                Console.WriteLine("bot active = " + Arrays.ToString(_bottomInference.GetActiveCells()));
                Assert.IsTrue(!_topInference.GetPredictiveCells().SequenceEqual(_bottomInference.GetPredictiveCells()), "PredictiveCells are equal");
                Assert.IsTrue(_topInference.GetPredictiveCells().Count > 0, "No predictive cells found (top)");
                Assert.IsTrue(_bottomInference.GetPredictiveCells().Count > 0, "No predictive cells found (bottom)");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }

        /**
         * Test that a null {@link Assembly.Mode} results in exception
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestFluentBuildSemantics()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);

            try
            {
                // Idea: Build up ResourceLocator paths in fluent style such as:
                // Layer.Using(
                //     ResourceLocator.AddPath("...") // Adds a search path for later mentioning terminal resources (i.e. files)
                //         .AddPath("...")
                //         .AddPath("..."))
                //     .Add(new SpatialPooler())
                //     ...
                Net.Network.Network.Create("test network", p)   // Add Network.Add() method for chaining region adds
                    .Add(Net.Network.Network.CreateRegion("r1")             // Add version of createRegion(String name) for later connecting by name
                        .Add(Net.Network.Network.CreateLayer<IInference>("2/3", p)      // so that regions can be added and connecting in one long chain.
                            .Using(new Connections())           // Test adding connections before elements which use them
                                .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))
                                .Add(new SpatialPooler())
                                .Add(new TemporalMemory())
                                .Add(Anomaly.Create(p))
                        )
                            .Add(Net.Network.Network.CreateLayer<IInference>("1", p)            // Add another Layer, and the Region internally connects it to the 
                                .Add(new SpatialPooler())               // previously added Layer
                                .Using(new Connections())               // Test adding connections after one element and before another
                                .Add(new TemporalMemory())
                                .Add(Anomaly.Create(p))
                        ))
                        .Add(Net.Network.Network.CreateRegion("r2")
                            .Add(Net.Network.Network.CreateLayer<IInference>("2/3", p)
                                .Add(new SpatialPooler())
                                .Using(new Connections()) // Test adding connections after one element and before another
                                .Add(new TemporalMemory())
                                .Add(Anomaly.Create(p))
                        ))
                        .Add(Net.Network.Network.CreateRegion("r3")
                            .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                                .Add(new SpatialPooler())
                                .Add(new TemporalMemory())
                                .Add(Anomaly.Create(p))
                                    .Using(new Connections()) // Test adding connections after elements which use them.
                        ))

                        .Connect("r1", "r2")
                        .Connect("r2", "r3");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail("Something went wrong here");
            }

        }

        [TestMethod]
        public void TestNetworkComputeWithNoSensor()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 30 });
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.4);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 7);
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

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

            //r1.Observe().Subscribe(new Subscriber<Inference>() {
            //    @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { Console.WriteLine(e); }
            //    @Override public void onNext(Inference i)
            //    {
            //        // UNCOMMENT TO VIEW STABILIZATION OF PREDICTED FIELDS
            //        //                System.out.Println("Day: " + r1.GetInput() + " - predictions: " + Arrays.toString(i.GetPreviousPrediction()) +
            //        //                    "   -   " + Arrays.toString(i.GetSparseActives()) + " - " + 
            //        //                    ((int)Math.rint(((Number)i.GetClassification("dayOfWeek").GetMostProbableValue(1)).doubleValue())));
            //    }
            //});

            int NUM_CYCLES = 400;
            int INPUT_GROUP_COUNT = 7; // Days of Week
            Map<string, object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    r1.Compute(multiInput);
                }
                n.Reset();
            }

            // Test that we get proper output after prediction stabilization
            //r1.Observe().Subscribe(new Subscriber<Inference>() {
            //    @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { Console.WriteLine(e); }
            //    @Override public void onNext(Inference i)
            //    {
            //        int nextDay = ((int)Math.rint(((Number)i.GetClassification("dayOfWeek").GetMostProbableValue(1)).doubleValue()));
            //        Assert.AreEqual(6, nextDay);
            //    }
            //});

            multiInput.Add("dayOfWeek", 5.0);
            n.Compute(multiInput);
        }

        [TestMethod]
        public void TestSynchronousBlockingComputeCall()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 30 });
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.4);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 7);
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("dayOfWeek", typeof(CLAClassifier)));

            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build())));

            bool gotResult = false;
            int NUM_CYCLES = 400;
            int INPUT_GROUP_COUNT = 7; // Days of Week
            Map<string, object> multiInput = new Map<string, object>();
            for (int i = 0; i < NUM_CYCLES; i++)
            {
                for (double j = 0; j < INPUT_GROUP_COUNT; j++)
                {
                    multiInput.Add("dayOfWeek", j);
                    IInference inf = n.ComputeImmediate(multiInput);
                    if (inf.GetPredictiveCells().Count > 6)
                    {
                        Assert.IsTrue(inf.GetPredictiveCells() != null);
                        // Make sure we've gotten all the responses
                        Assert.AreEqual((i * 7) + (int)j, inf.GetRecordNum());
                        gotResult = true;
                        break;
                    }
                }
                if (gotResult)
                {
                    break;
                }
            }

            Assert.IsTrue(gotResult);
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestThreadedStartFlagging()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 30 });
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.1);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_TRIM_THRESHOLD, 0.05);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.4);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 10.0);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 7);
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            Parameters pars = Parameters.Empty();
            pars.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

            Net.Network.Network n = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create(pars)))
                    .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                        .Add(new TemporalMemory()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                        .Add(new SpatialPooler())
                        .Add((MultiEncoder)MultiEncoder.GetBuilder().Name("").Build()))
                    .Connect("1", "2")
                    .Connect("2", "3")
                    .Connect("3", "4"));

            Assert.IsFalse(n.IsThreadedOperation());
            n.Start();
            Assert.IsFalse(n.IsThreadedOperation());

            //////////////////////////////////////////////////////
            // Add a Sensor which should allow Network to start //
            //////////////////////////////////////////////////////
            p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));
            n = Net.Network.Network.Create("test network", p)
                   .Add(Net.Network.Network.CreateRegion("r1")
                           .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                                   .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true))
                           .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                                   .Add(Anomaly.Create(p)))
                           .Add(Net.Network.Network.CreateLayer<IInference>("3", p)
                                   .Add(new TemporalMemory()))
                           .Add(Net.Network.Network.CreateLayer<IInference>("4", p)
                                   .Add(new SpatialPooler())
                                   .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                                       SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv")))))
                           .Connect("1", "2")
                           .Connect("2", "3")
                           .Connect("3", "4"));
            Assert.IsFalse(n.IsThreadedOperation());
            n.Start();
            Assert.IsTrue(n.IsThreadedOperation());

            try
            {
                p = NetworkTestHarness.GetParameters();
                p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
                p.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
                p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));
                n = Net.Network.Network.Create("test network", p)
                   .Add(Net.Network.Network.CreateRegion("r1")
                           .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                                   .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                                   .Add(new TemporalMemory())
                                   .Add(new SpatialPooler())
                                   .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                                       SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))));

                n.Start();

                n.ComputeImmediate(new Map<string, object>());

                // SHOULD FAIL HERE WITH EXPECTED EXCEPTION
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Cannot call computeImmediate() when Network has been started.", e.Message);
            }
        }

        double _anomaly = 1;
        bool _completed;
        [TestMethod]
        public void TestObservableWithCoordinateEncoder()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("timestamp,consumption,location")
                .AddHeader("datetime,float,geo")
                .AddHeader("T,,")
                .Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, "", manual));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetGeospatialTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer("1", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(htmSensor)));

            network.Start();

            network.Observe().Subscribe(output =>
            {
                //System.out.Println(output.GetRecordNum() + ":  input = " + Arrays.toString(output.GetEncoding()));//output = " + Arrays.toString(output.GetSDR()) + ", " + output.GetAnomalyScore());
                Debug.WriteLine("anomaly = " + _anomaly);
                if (output.GetAnomalyScore() < _anomaly)
                {
                    _anomaly = output.GetAnomalyScore();
                }
            }, Console.WriteLine, 
            () =>
            {
                Assert.AreEqual(0, _anomaly, 0);
                _completed = true;
            });


            int x = 0;
            for (int i = 0; i < 100; i++)
            {
                x = i % 10;
                manual.OnNext("7/12/10 13:10,35.3,40.6457;-73.7" + x + "692;" + x); //5 = meters per second
            }



            manual.OnComplete();

            ILayer l = network.Lookup("r1").Lookup("1");
            try
            {
                l.GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }

            Thread.Sleep(1000);

            Assert.IsTrue(_completed);

        }

        string _errorMessage = null;
        [TestMethod]
        public void TestObservableWithCoordinateEncoder_NEGATIVE()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("timestamp,consumption,location")
                .AddHeader("datetime,float,geo")
                .AddHeader("T,,").Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, "", manual));

            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetGeospatialTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            HTMSensor<ObservableSensor<string[]>> htmSensor = (HTMSensor<ObservableSensor<string[]>>)sensor;

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(htmSensor)));

            network.Observe().Subscribe(
                output => { }, 
                e =>
                {
                    _errorMessage = e.Message;
                    network.Halt();
                }, 
                () =>
                {
                    //Should never happen here.
                    Assert.AreEqual(0, _anomaly, 0);
                    _completed = true;
                });



            network.Start();

            int x = 0;
            for (int i = 0; i < 100; i++)
            {
                x = i % 10;
                manual.OnNext("7/12/10 13:10,35.3,40.6457;-73.7" + x + "692;" + x); //1st "x" is attempt to vary coords, 2nd "x" = meters per second
            }

            manual.OnComplete();

            ILayer l = network.Lookup("r1").Lookup("1");
            try
            {
                l.GetLayerThread().Wait();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(ThreadInterruptedException));
            }

            // Assert onNext condition never gets set
            Assert.IsFalse(_completed);
            Assert.AreEqual("Cannot autoclassify with raw array input or  " +
                "Coordinate based encoders... Remove auto classify setting.", _errorMessage);
        }

        ///////////////////////////////////////////////////////////////////////////////////
        //    Tests of Calculate Input Width for inter-regional and inter-layer calcs    //
        ///////////////////////////////////////////////////////////////////////////////////
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestCalculateInputWidth_NoPrevLayer_UpstreamRegion_with_TM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())))
                .Add(Net.Network.Network.CreateRegion("r2")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))))
                .Connect("r1", "r2");

            Region r1 = network.Lookup("r1");
            ILayer layer2 = r1.Lookup("2");

            int width = layer2.CalculateInputWidth();
            Assert.AreEqual(65536, width);
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestCalculateInputWidth_NoPrevLayer_UpstreamRegion_without_TM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap("consumption", typeof(CLAClassifier)));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler())))
                .Add(Net.Network.Network.CreateRegion("r2")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(new SpatialPooler())
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv"))))))
                .Connect("r1", "r2");

            Region r1 = network.Lookup("r1");
            ILayer layer2 = r1.Lookup("2");

            int width = layer2.CalculateInputWidth();
            Assert.AreEqual(2048, width);

        }

        [TestMethod]
        public void TestCalculateInputWidth_NoPrevLayer_NoPrevRegion_andTM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                        .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                                .Add(Anomaly.Create())
                                .Add(new TemporalMemory())
                                //.Add(new SpatialPooler())
                                .Close()));

            Region r1 = network.Lookup("r1");
            ILayer layer2 = r1.Lookup("2");

            int width = layer2.CalculateInputWidth();
            Assert.AreEqual(65536, width);
        }

        [TestMethod]
        public void TestCalculateInputWidth_NoPrevLayer_NoPrevRegion_andSPTM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                    .Add(Net.Network.Network.CreateRegion("r1")
                            .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                                    .Add(Anomaly.Create())
                                    .Add(new TemporalMemory())
                                            .Add(new SpatialPooler())
                                    .Close()));

            Region r1 = network.Lookup("r1");
            ILayer layer2 = r1.Lookup("2");

            int width = layer2.CalculateInputWidth();
            Assert.AreEqual(8, width);
        }

        [TestMethod]
        public void TestCalculateInputWidth_NoPrevLayer_NoPrevRegion_andNoTM()
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

            Region r1 = network.Lookup("r1");
            ILayer layer2 = r1.Lookup("2");

            int width = layer2.CalculateInputWidth();
            Assert.AreEqual(8, width);
        }

        [TestMethod]
        public void TestCalculateInputWidth_WithPrevLayer_WithTM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .Add(Anomaly.Create())
                        .Add(new SpatialPooler()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new SpatialPooler()))
                    .Connect("1", "2"));

            Region r1 = network.Lookup("r1");
            ILayer layer1 = r1.Lookup("1");

            int width = layer1.CalculateInputWidth();
            Assert.AreEqual(65536, width);
        }

        [TestMethod]
        public void TestCalculateInputWidth_WithPrevLayer_NoTM()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Net.Network.Network network = Net.Network.Network.Create("test network", p)
                .Add(Net.Network.Network.CreateRegion("r1")
                    .Add(Net.Network.Network.CreateLayer<IInference>("1", p)
                        .Add(Anomaly.Create())
                        .Add(new SpatialPooler()))
                    .Add(Net.Network.Network.CreateLayer<IInference>("2", p)
                        .Add(new SpatialPooler()))
                    .Connect("1", "2"));

            Region r1 = network.Lookup("r1");
            ILayer layer1 = r1.Lookup("1");

            int width = layer1.CalculateInputWidth();
            Assert.AreEqual(2048, width);
        }

        [TestMethod]
        public void CloseTest()
        {
            Parameters p = NetworkTestHarness.GetParameters();
            p = p.Union(NetworkTestHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new MersenneTwister(42));

            Region region1 = Net.Network.Network.CreateRegion("region1");
            ILayer layer1 = Net.Network.Network.CreateLayer("layer1", p);
            region1.Add(layer1);

            Region region2 = Net.Network.Network.CreateRegion("region2");
            ILayer layer2 = Net.Network.Network.CreateLayer("layer2", p);
            region2.Add(layer2);

            Net.Network.Network network = Net.Network.Network.Create("test network", p);

            // Calling close on an empty Network should not throw any Exceptions
            network.Close();

            // Calling close on a Network with a single unclosed Region
            network.Add(region1);
            network.Close();

            Assert.IsTrue(region1.IsClosed(), "Region 1 did not close, after closing Network");
            Assert.IsTrue(layer1.IsClosed(), "Layer 1 did not close, after closing Network");

            // Calling close on a Network with two regions, one of which is closed
            network.Add(region2);
            network.Close();

            Assert.IsTrue(region1.IsClosed(), "Region 1 did not close, after closing Network with 2 Regions");
            Assert.IsTrue(layer1.IsClosed(), "Layer 1 did not close, after closing Network with 2 Regions");
            Assert.IsTrue(region2.IsClosed(), "Region 2 did not close, after closing Network with 2 Regions");
            Assert.IsTrue(layer2.IsClosed(), "Layer 2 did not close, after closing Network with 2 Regions");
        }

        ////////////////////////////////////////
        //         Utility Methods            //
        ////////////////////////////////////////

        //Publisher pub = null;
        private Net.Network.Network GetLoadedDayOfWeekNetwork()
        {
            Parameters p = NetworkTestHarness.GetParameters().Copy();
            p = p.Union(NetworkTestHarness.GetDayDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(42));

            Sensor<ObservableSensor<String[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, 
                    SensorParams.Create(SensorParams.Keys.Obs, new Object[] {"name",
                Publisher.GetBuilder()
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

        private static Func<IInference, int, int> CreateDayOfWeekInferencePrintout()
        {
            int cycles = 1;
            return (inf, cellsPerColumn) =>
            {

                //                Classification<Object> result = inf.getClassification("dayOfWeek");
                double day = MapToInputData((int[])inf.GetLayerInput());
                if (day == 1.0)
                {
                    //                    System.out.println("\n=========================");
                    //                    System.out.println("CYCLE: " + cycles);
                    cycles++;
                }

                //                System.out.println("RECORD_NUM: " + inf.getRecordNum());
                //                System.out.println("ScalarEncoder Input = " + day);
                //                System.out.println("ScalarEncoder Output = " + Arrays.toString(inf.getEncoding()));
                //                System.out.println("SpatialPooler Output = " + Arrays.toString(inf.getFeedForwardActiveColumns()));
                //                
                //                if(inf.getPreviousPredictiveCells() != null)
                //                    System.out.println("TemporalMemory Previous Prediction = " + 
                //                        Arrays.toString(SDR.cellsAsColumnIndices(inf.getPreviousPredictiveCells(), cellsPerColumn)));
                //                
                //                System.out.println("TemporalMemory Actives = " + Arrays.toString(SDR.asColumnIndices(inf.getSDR(), cellsPerColumn)));
                //                
                //                System.out.print("CLAClassifier prediction = " + 
                //                    stringValue((Double)result.getMostProbableValue(1)) + " --> " + ((Double)result.getMostProbableValue(1)));
                //                
                //                System.out.println("  |  CLAClassifier 1 step prob = " + Arrays.toString(result.getStats(1)) + "\n");

                return cycles;
            };
        }

        private static double MapToInputData(int[] encoding)
        {
            for (int i = 0; i < dayMap.Length; i++)
            {
                if (Arrays.AreEqual(encoding, dayMap[i]))
                {
                    return i + 1;
                }
            }
            return -1;
        }

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