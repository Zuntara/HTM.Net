using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Reactive;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Tests.Properties;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.PaHotGym
{
    /**
 * Demonstrates the Java version of the NuPIC Network API (NAPI) Demo.
 *
 * This demo demonstrates many powerful features of the HTM.java
 * NAPI. Looking at the {@link NetworkAPIDemo#createBasicNetwork()} method demonstrates
 * the conciseness of setting up a basic network. As you can see, the network is
 * constructed in fluent style proceeding from the top-level {@link Network} container,
 * to a single {@link Region}; then to a single {@link PALayer}.
 *
 * Layers contain most of the operation logic and are the only constructs that contain
 * algorithm components (i.e. {@link CLAClassifier}, {@link Anomaly} (the anomaly computer),
 * {@link TemporalMemory}, {@link PASpatialPooler}, and {@link Encoder} (actually, a {@link MultiEncoder}
 * which can be the parent of many child encoders).
 *
 *
 * @author cogmission
 *
 */
    public class NetworkAPIDemo
    {
        /** 3 modes to choose from to demonstrate network usage */
        public enum Mode { BASIC, MULTILAYER, MULTIREGION };

        private Network.Network network;

        private FileInfo outputFile;
        private StreamWriter pw;

        private double predictedValue = 0.0;

        public NetworkAPIDemo(Mode mode)
        {
            switch (mode)
            {
                case Mode.BASIC: network = CreateBasicNetwork(); break;
                case Mode.MULTILAYER: network = CreateMultiLayerNetwork(); break;
                case Mode.MULTIREGION: network = CreateMultiRegionNetwork(); break;
            }

            network.Observe().Subscribe(GetSubscriber());
            try
            {
                outputFile = new FileInfo("c:\\temp\\pa_hotgym_15_output_"+ mode + ".txt");
                Debug.WriteLine("Creating output file: " + outputFile);
                pw = new StreamWriter(outputFile.OpenWrite());
                pw.WriteLine("RecordNum,Actual,Predicted,Error,AnomalyScore");
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        /**
         * Creates a basic {@link Network} with 1 {@link Region} and 1 {@link PALayer}. However
         * this basic network contains all algorithmic components.
         *
         * @return  a basic Network
         */
        internal Network.Network CreateBasicNetwork()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 22); // 22 18
            p.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 16); // 18
            p.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0); // 0.0
            p.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 1); // 1

            Region r = Network.Network.CreateRegion("Region 1");
            PALayer<IInference> l = new PALayer<IInference>("Layer 2/3", null, p);
            l.SetPADepolarize(0.0); // 0.25
            l.SetVerbosity(0);
            PASpatialPooler sp = new PASpatialPooler();
            string infile = "rec-center-15m.csv";

            // This is how easy it is to create a full running Network!

            return Network.Network.Create("Network API Demo", p)
                .Add(r.Add(l.AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(Anomaly.Create())
                .Add(new TemporalMemory())
                .Add(sp)
                .Add(Sensor<FileInfo>.Create(FileSensor.Create, 
                    SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), infile))))));
        }

        /**
         * Creates a {@link Network} containing one {@link Region} with multiple
         * {@link PALayer}s. This demonstrates the method by which multiple layers
         * are added and connected; and the flexibility of the fluent style api.
         *
         * @return  a multi-layer Network
         */
        internal Network.Network CreateMultiLayerNetwork()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetNetworkDemoTestEncoderParams());

            return Network.Network.Create("Network API Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory()))
                    .Add(Network.Network.CreateLayer("Layer 4", p)
                        .Add(new PASpatialPooler()))
                    .Add(Network.Network.CreateLayer("Layer 5", p)
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-15m.csv")))))
                    .Connect("Layer 2/3", "Layer 4")
                    .Connect("Layer 4", "Layer 5"));
        }

        /**
         * Creates a {@link Network} containing 2 {@link Region}s with multiple
         * {@link PALayer}s in each.
         *
         * @return a multi-region Network
         */
        internal Network.Network CreateMultiRegionNetwork()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetNetworkDemoTestEncoderParams());

            return Network.Network.Create("Network API Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory()))
                    .Add(Network.Network.CreateLayer("Layer 4", p)
                        .Add(new PASpatialPooler()))
                    .Connect("Layer 2/3", "Layer 4"))
               .Add(Network.Network.CreateRegion("Region 2")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new PASpatialPooler()))
                    .Add(Network.Network.CreateLayer("Layer 4", p)
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-15m.csv")))))
                    .Connect("Layer 2/3", "Layer 4"))
               .Connect("Region 1", "Region 2");

        }

        /**
         * Demonstrates the composition of a {@link Subscriber} (may also use
         * {@link Observer}). There are 3 methods one must be concerned with:
         * </p>
         * <p>
         * <pre>
         * 1. onCompleted(). Called when the stream is exhausted and will be closed.
         * 2. onError(). Called when there is an underlying exception or error in the processing.
         * 3. onNext(). Called for each processing cycle of the network. This is the method
         * that is overridden to do downstream work in your application.
         *
         * @return
         */
        internal IObserver<IInference> GetSubscriber()
        {
            return Observer.Create<IInference>(output =>
            {
                Debug.WriteLine("Writing to file");
                WriteToFile(output, "consumption");
            }, Console.WriteLine, () =>
            {
                Console.WriteLine("Stream completed. see output: " + outputFile.FullName);
                try
                {
                    pw.Flush();
                    pw.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            });
        }

        /**
         * Primitive file appender for collecting output. This just demonstrates how to use
         * {@link Subscriber#onNext(Object)} to accomplish some work.
         *
         * @param infer             The {@link Inference} object produced by the Network
         * @param classifierField   The field we use in this demo for anomaly computing.
         */
        private void WriteToFile(IInference infer, string classifierField)
        {
            try
            {
                double newPrediction;
                if (null != infer.GetClassification(classifierField).GetMostProbableValue(1))
                {
                    newPrediction = (double)infer.GetClassification(classifierField).GetMostProbableValue(1);
                }
                else {
                    newPrediction = predictedValue;
                }
                if (infer.GetRecordNum() > 0)
                {
                    double actual = (double)((NamedTuple)infer.GetClassifierInput()[classifierField]).Get("inputValue");
                    double error = Math.Abs(predictedValue - actual);
                    StringBuilder sb = new StringBuilder()
                            .Append(infer.GetRecordNum()).Append(", ")
                            //.Append("classifier input=")
                            .Append(string.Format("{0}", actual.ToString(NumberFormatInfo.InvariantInfo))).Append(",")
                            //.Append("prediction= ")
                            .Append(string.Format("{0}", predictedValue.ToString(NumberFormatInfo.InvariantInfo))).Append(",")
                            .Append(string.Format("{0}", error.ToString(NumberFormatInfo.InvariantInfo))).Append(",")
                            //.Append("anomaly score=")
                            .Append(infer.GetAnomalyScore().ToString(NumberFormatInfo.InvariantInfo));
                    pw.WriteLine(sb.ToString());
                    pw.Flush();
                    if (infer.GetRecordNum() % 100 == 0)
                    {
                        Console.WriteLine(sb.ToString());
                    }
                }
                predictedValue = newPrediction;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                pw.Flush();
            }

        }

        /**
         * Simple run hook
         */
        public void RunNetwork()
        {
            network.Start();
            network.GetHead().GetHead().GetLayerThread().Wait();
        }

        

        /**
         * Main entry point of the demo
         * @param args
         */
        //public static void Main(String[] args)
        //{
        //    // Substitute the other modes here to see alternate examples of Network construction
        //    // in operation.
        //    NetworkAPIDemo demo = new NetworkAPIDemo(Mode.BASIC);
        //    demo.RunNetwork();
        //}
    }

}