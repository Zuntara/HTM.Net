using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Tests.Properties;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.Random
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
        public enum Mode { BASIC_CLA, BASIC_SDR, MULTILAYER, MULTIREGION };

        private readonly Network.Network _network;

        private readonly FileInfo _outputFile;
        private readonly StreamWriter _pw;

        private double[] _predictedValues = null;
        private List<PredictionValue> _predictions;

        public NetworkAPIDemo(Mode mode)
        {
            switch (mode)
            {
                case Mode.BASIC_CLA: _network = CreateBasicNetworkCla(); break;
                case Mode.BASIC_SDR: _network = CreateBasicNetworkSdr(); break;
                case Mode.MULTILAYER: _network = CreateMultiLayerNetwork(); break;
                case Mode.MULTIREGION: _network = CreateMultiRegionNetwork(); break;
            }

            _network.Observe().Subscribe(GetSubscriber());
            try
            {
                _outputFile = new FileInfo("c:\\temp\\RandomData_output_" + mode + ".txt");
                Debug.WriteLine("Creating output file: " + _outputFile);
                _pw = new StreamWriter(_outputFile.OpenWrite());
                _pw.WriteLine("RecordNum,Actual,Predicted,CorrectGuesses,AnomalyScore");
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        /// <summary>
        /// Creates a basic <see cref="Network"/> with 1 <see cref="Region"/> and 1 <see cref="ILayer"/>. 
        /// However this basic network contains all algorithmic components.
        /// </summary>
        internal Network.Network CreateBasicNetworkCla()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());

            // This is how easy it is to create a full running Network!

            return Network.Network.Create("RandomData Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                .Add(Network.Network.CreateLayer("Layer 2/3", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(Anomaly.Create())
                .Add(new TemporalMemory())
                .Add(new Algorithms.SpatialPooler())
                .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", "RandomData.csv")))));
        }

        internal Network.Network CreateBasicNetworkSdr()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetNetworkDemoTestEncoderParams());
            p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY_TYPE, typeof(SDRClassifier));

            // This is how easy it is to create a full running Network!

            return Network.Network.Create("Network API Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                .Add(Network.Network.CreateLayer("Layer 2/3", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(Anomaly.Create())
                .Add(new TemporalMemory())
                .Add(new Algorithms.SpatialPooler())
                .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", "RandomData.csv")))));
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
                        .Add(new Algorithms.SpatialPooler()))
                    .Add(Network.Network.CreateLayer("Layer 5", p)
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "RandomData.csv")))))
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
                        .Add(new Algorithms.SpatialPooler()))
                    .Connect("Layer 2/3", "Layer 4"))
               .Add(Network.Network.CreateRegion("Region 2")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new Algorithms.SpatialPooler()))
                    .Add(Network.Network.CreateLayer("Layer 4", p)
                        .Add(Sensor<FileInfo>.Create(FileSensor.Create, SensorParams.Create(
                            SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "RandomData.csv")))))
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
                //Debug.WriteLine("Writing to file");
                WriteToFile(output, new[] {"Number 1", "Number 2" , "Number 3" , "Number 4" , "Number 5" , "Number 6" , "Bonus" });
                RecordStep(output, new[] { "Number 1", "Number 2", "Number 3", "Number 4", "Number 5", "Number 6", "Bonus" });
            }, Console.WriteLine, () =>
            {
                Console.WriteLine("Stream completed. see output: " + _outputFile.FullName);
                try
                {
                    _pw.Flush();
                    _pw.Close();
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
        private void WriteToFile(IInference infer, string[] classifierFields)
        {
            try
            {
                double[] newPredictions;
                if (classifierFields.Any(cf => null != infer.GetClassification(cf).GetMostProbableValue(1)))
                {
                    newPredictions = classifierFields.Select(cf => ((double?)infer.GetClassification(cf).GetMostProbableValue(1)).GetValueOrDefault(-1)).ToArray();
                }
                else {
                    newPredictions = _predictedValues;
                }
                // Start logging from item 1
                if (infer.GetRecordNum() > 0)
                {
                    double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)infer.GetClassifierInput()[cf]).Get("inputValue")).ToArray();
                    //double[] errors = ArrayUtils.Abs(ArrayUtils.Subtract(_predictedValues, actuals));
                    int correctGuesses = actuals.Where(a => _predictedValues.Contains(a)).Count();
                    StringBuilder sb = new StringBuilder()
                            .Append(infer.GetRecordNum()).Append(", ")
                            //.Append("classifier input=")
                            .Append(string.Format("{0}", Arrays.ToString(actuals))).Append(",")
                            //.Append("prediction= ")
                            .Append(string.Format("{0}", Arrays.ToString(_predictedValues))).Append(",")
                            //.Append("correctGuesses=")
                            .Append(string.Format("{0}", correctGuesses)).Append(",")
                            //.Append("anomaly score=")
                            .Append(infer.GetAnomalyScore().ToString(NumberFormatInfo.InvariantInfo));
                    _pw.WriteLine(sb.ToString());
                    _pw.Flush();
                    if (infer.GetRecordNum() % 100 == 0)
                    {
                        Console.WriteLine(sb.ToString());
                    }
                }
                _predictedValues = newPredictions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _pw.Flush();
            }

        }

        private void RecordStep(IInference infer, string[] classifierFields)
        {
            double[] newPredictions;
            if (classifierFields.Any(cf => null != infer.GetClassification(cf).GetMostProbableValue(1)))
            {
                newPredictions = classifierFields.Select(cf => ((double?)infer.GetClassification(cf).GetMostProbableValue(1)).GetValueOrDefault(-1)).ToArray();
            }
            else
            {
                newPredictions = _predictedValues;
            }
            if (infer.GetRecordNum() > 0)
            {
                double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)infer.GetClassifierInput()[cf]).Get("inputValue")).ToArray();
                double[] errors = ArrayUtils.Abs(ArrayUtils.Subtract(_predictedValues, actuals));
                int correctGuesses = actuals.Where(a => _predictedValues.Contains(a)).Count();

                PredictionValue value = new PredictionValue();
                value.RecordNum = infer.GetRecordNum();
                value.ActualValues = actuals;
                value.PredictionErrors = errors;
                value.CorrectGuesses = correctGuesses;
                value.PredictedValues = newPredictions;
                value.AnomalyFactor = infer.GetAnomalyScore();
                _predictions.Add(value);
            }
            _predictedValues = newPredictions;
        }

        /**
         * Simple run hook
         */
        public void RunNetwork()
        {
            _predictions = new List<PredictionValue>();

            _network.Start();
            _network.GetHead().GetHead().GetLayerThread().Wait();
        }

        public int GetHighestCorrectGuesses(double rangePct, bool fromBehind)
        {
            int totalLength = _predictions.Count-1;
            int takeRange = (int)(totalLength * rangePct);
            if (fromBehind)
            {
                int offset = totalLength - takeRange;
                int totalActual = _predictions.Skip(1).Skip(offset).Max(p => p.CorrectGuesses);

                return  totalActual;
            }
            else
            {
                int totalActual = _predictions.Skip(1).Take(takeRange).Max(p => p.CorrectGuesses);
                return  totalActual;
            }
        }

        public double GetAverageCorrectGuesses(double rangePct, bool fromBehind)
        {
            int totalLength = _predictions.Count-1;
            int takeRange = (int)(totalLength * rangePct);
            if (fromBehind)
            {
                int offset = totalLength - takeRange;
                double totalActual = _predictions.Skip(1).Skip(offset).Average(p => p.CorrectGuesses);

                return totalActual;
            }
            else
            {
                double totalActual = _predictions.Skip(1).Take(takeRange).Average(p => p.CorrectGuesses);
                return totalActual;
            }
        }

        public int[] GetGuesses()
        {
            return _predictions.Skip(1).Select(p => p.CorrectGuesses).ToArray();
        }

        //public double GetTotalAccurancy(double rangePct, bool fromBehind)
        //{
        //    int totalLength = _predictions.Count;
        //    int takeRange = (int)(totalLength * rangePct);
        //    if (fromBehind)
        //    {
        //        int offset = totalLength - takeRange;
        //        double totalActual = _predictions.Skip(offset).Sum(p => p.ActualValue);
        //        double totalPredicted = _predictions.Skip(offset).Sum(p => p.PredictedValue);

        //        return totalPredicted / totalActual;
        //    }
        //    else
        //    {
        //        double totalActual = _predictions.Take(takeRange).Sum(p => p.ActualValue);
        //        double totalPredicted = _predictions.Take(takeRange).Sum(p => p.PredictedValue);

        //        return totalPredicted / totalActual;
        //    }
        //}

        public class PredictionValue
        {
            public int RecordNum { get; set; }
            public double[] ActualValues { get; set; }
            public double[] PredictedValues { get; set; }
            public double[] PredictionErrors { get; set; }
            public int CorrectGuesses { get; set; }
            public double AnomalyFactor { get; set; }
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