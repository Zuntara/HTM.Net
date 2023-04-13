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
using HTM.Net.Encoders;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Tests.Properties;
using HTM.Net.Util;
using MathNet.Numerics.Statistics;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Tests.Examples.Random
{
    public class RandomGuessNetworkApi
    {
        private readonly Network.Network _network;
        private readonly FileInfo _outputFile;
        private readonly StreamWriter _pw;
        private Map<int, double[]> _predictedValues; // step, values
        private List<RandomGuess> _predictions;

        private readonly int _nrOfRecordsToEvaluateAfterTraining;
        private readonly int _numberOfRecordsToTrain;
        private readonly int _iterationsToRepeat;
        private readonly int _offsetFromEnd;
        private readonly bool _writeToFile;

        private List<int[]> _selectedData = new List<int[]>();

        public RandomGuessNetworkApi(int nrOfRecordsToTrain, int nrOfRecordsToEvaluateAfterTraining, int iterationsToRepeat = 0,
            bool writeToFile = true, int offsetFromEnd = 0)
        {
            _numberOfRecordsToTrain = nrOfRecordsToTrain;
            _nrOfRecordsToEvaluateAfterTraining = nrOfRecordsToEvaluateAfterTraining;
            _iterationsToRepeat = iterationsToRepeat;
            _writeToFile = writeToFile;
            _offsetFromEnd = offsetFromEnd;

            _network = CreateBasicNetworkCla();
            _network.Observe().Subscribe(GetSubscriber());

            if (!_writeToFile) return;
            try
            {
                _outputFile = new FileInfo("c:\\temp\\RandomData_output_Simulator.txt");
                if (_outputFile.Exists)
                {
                    _outputFile.Delete();
                }
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
            var sensor = PrepareRandomDataAndGetSensor();

            Parameters p = NetworkDemoHarness.GetParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, GetEncoderSettings().AsMap());
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap(
                ("Number 1", typeof(CLAClassifier)),
                ("Number 2", typeof(CLAClassifier)),
                ("Number 3", typeof(CLAClassifier)),
                ("Number 4", typeof(CLAClassifier)),
                ("Number 5", typeof(CLAClassifier)),
                ("Number 6", typeof(CLAClassifier)),
                ("Bonus", typeof(CLAClassifier))));

            return Network.Network.Create("RandomData Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                        .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                        .Add(Anomaly.Create())
                        .Add(new TemporalMemory())
                        .Add(new Algorithms.SpatialPooler())
                        .Add(sensor)));
        }

        private static Map<string, Type> GetInferredFieldsMap(
            params (string field, Type classifier)[] args)
        {
            Map<string, Type> inferredFieldsMap = new Map<string, Type>();
            foreach ((string field, Type classifier) tuple in args)
            {
                inferredFieldsMap.Add(tuple.field, tuple.classifier);
            }

            return inferredFieldsMap;
        }

        public void RunNetwork()
        {
            _predictions = new List<RandomGuess>();

            _network.Start();
            _network.GetTail().GetTail().GetLayerThread().Wait();
        }

        internal IObserver<IInference> GetSubscriber()
        {
            return Observer.Create<IInference>(output =>
            {
                string[] classifierFields = { "Number 1", "Number 2", "Number 3", "Number 4", "Number 5", "Number 6", "Bonus" };
                Map<int, double[]> newPredictions = new Map<int, double[]>();
                // Step 1 is certainly there
                if (classifierFields.Any(cf => null != output.GetClassification(cf).GetMostProbableValue(1)))
                {
                    foreach (int step in output.GetClassification(classifierFields[0]).StepSet())
                    {
                        newPredictions.Add(step, classifierFields
                            .Take(7)
                            .Select(cf => ((double?)output.GetClassification(cf).GetMostProbableValue(step)).GetValueOrDefault(-1))
                            .ToArray());
                    }
                }
                else
                {
                    newPredictions = _predictedValues;
                }

                RandomGuess gd = RandomGuess.From(_predictedValues, newPredictions, output, classifierFields);

                if (gd.RecordNumber > 0) // first prediction is bogus, ignore it
                    _predictions.Add(gd);

                // Statistical good numbers for chances
                List<int[]> goodChances = GetBestChances(_offsetFromEnd + 1);
                foreach (int[] chance in goodChances)
                {
                    gd.AddPrediction(chance.Select(c => (double)c).ToArray(), false);
                    gd.NextPredictions.Add(chance);
                }
                gd.NextPredictions = gd.NextPredictions.Take(10).ToList();

                _predictedValues = newPredictions;

                if (_writeToFile)
                {
                    WriteToFile(gd);
                }

            }, Console.WriteLine, () =>
            {
                if (_writeToFile)
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
                }
            });
        }

        private void ReverseRandomData()
        {
            List<string> fileLines = YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).ToList();
            List<string> fileLinesReversed = new List<string>();

            // Copy header
            for (int i = 0; i < 3; i++)
            {
                fileLinesReversed.Add(fileLines[i]);
            }

            int takeCount = _numberOfRecordsToTrain;
            int skipCount = fileLines.Count - 3 - takeCount - _nrOfRecordsToEvaluateAfterTraining; // take last 110 records
            // Take the rest and reverse it
            for (int i = 0; i < _iterationsToRepeat; i++)
            {
                fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount).Take(takeCount));
            }
            fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount)); // last 10 record given, not trained

            StreamWriter sw = new StreamWriter("RandomData_rev.csv");
            foreach (string line in fileLinesReversed)
            {
                sw.WriteLine(line);
            }
            sw.Flush();
            sw.Close();
        }

        private Sensor<ObservableSensor<string[]>> PrepareRandomDataAndGetSensor()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("Date,Number 1,Number 2,Number 3,Number 4,Number 5,Number 6,Bonus")
                .AddHeader("datetime,int,int,int,int,int,int,int")
                .AddHeader("T,,,,,,,")
                .Build();

            List<string> fileLines = YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).ToList();

            // Cleanup and reverse
            List<string> fileLinesReversed = fileLines.Skip(3).Reverse().ToList();
            List<string> fileLinesFinal = new List<string>();

            // Define number of records to train on
            int takeTrainCount = _numberOfRecordsToTrain;
            int takeTotalCount = _numberOfRecordsToTrain + _nrOfRecordsToEvaluateAfterTraining;
            // Define offset from start of file
            int skipCount = fileLinesReversed.Count - takeTotalCount - _offsetFromEnd;

            // Repeat the training set if needed
            for (int i = 0; i < _iterationsToRepeat; i++)
            {
                fileLinesFinal.AddRange(fileLinesReversed.Skip(skipCount).Take(takeTrainCount));
            }
            fileLinesFinal.AddRange(fileLinesReversed.Skip(skipCount).Take(takeTotalCount)); // last x record given, not trained before

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            //StreamWriter sw = new StreamWriter("RandomData_rev.csv");
            foreach (string line in fileLinesFinal)
            {
                //sw.WriteLine(line);
                manual.OnNext(line);
                _selectedData.Add(line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(int.Parse).ToArray());
            }
            manual.OnComplete();

            //sw.Flush();
            //sw.Close();
            return sensor;
        }

        private EncoderSettingsList GetEncoderSettings()
        {
            List<MinMax> minmaxMap = GetFieldStatistics();

            int outputN = 63;
            int outputW = 21;

            EncoderSettingsList fieldEncodings = NetworkDemoHarness.SetupMap(
                    null,
                    0, // n
                    0, // w
                    0, 0, 0, 0, null, null, null,
                    "Date", FieldMetaType.DateTime, EncoderTypes.DateEncoder);

            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[0].Min(), minmaxMap[0].Max(), 0, 0, null, null, true,
                    $"Number 1", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);
            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[1].Min(), minmaxMap[1].Max(), 0, 0, null, null, true,
                    $"Number 2", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);
            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[2].Min(), minmaxMap[2].Max(), 0, 0, null, null, true,
                    $"Number 3", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);
            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[3].Min(), minmaxMap[3].Max(), 0, 0, null, null, true,
                    $"Number 4", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);
            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[4].Min(), minmaxMap[4].Max(), 0, 0, null, null, true,
                    $"Number 5", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);
            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[5].Min(), minmaxMap[5].Max(), 0, 0, null, null, true,
                    $"Number 6", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);


            fieldEncodings = NetworkDemoHarness.SetupMap(
                    fieldEncodings,
                    outputN,
                    outputW,
                    minmaxMap[6].Min(), minmaxMap[6].Max(), 0, 0, null, null, true,
                    "Bonus", FieldMetaType.Integer, EncoderTypes.ScalarEncoder);

            fieldEncodings["Date"].dayOfWeek = new Tuple(1, 1.0); // Day of week
            //fieldEncodings["Date"].timeOfDay = new Tuple(5, 4.0); // Time of day
            fieldEncodings["Date"].formatPattern = "dd/MM/YY";

            return fieldEncodings;
        }

        private List<MinMax> GetFieldStatistics()
        {
            List<MinMax> result = new List<MinMax>();

            for (int i = 0; i < 7; i++)
            {
                MinMax mm = new MinMax(_selectedData.Select(n => n[i]).Min(),
                    _selectedData.Select(n => n[i]).Max());
                result.Add(mm);
            }
            return result;
        }

        private static List<int[]> _randomActuals;
        private static List<int[]> GetRandomData()
        {
            if (_randomActuals == null)
            {
                _randomActuals = new List<int[]>();
                foreach (string line in YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).Skip(3))
                {
                    int[] actuals = line.Split(new[] {','}, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(v => int.Parse(v)).ToArray();
                    _randomActuals.Add(actuals);
                }
            }
            List<int[]> data = new List<int[]>();
            int i = 0;
            foreach (int[] actuals in _randomActuals)
            {
                data.Add(actuals);
            }
            return data;
        }

        private static List<int[]> GetRandomDataReversed()
        {
            var data = GetRandomData();
            data.Reverse();
            return data;
        }

        public static int[] GetBestChance(
            int nrOfRecords,
            int offsetFromEnd,
            List<int[]> data = null)
        {
            var allData = data ?? GetRandomDataReversed();
            int takeTrainCount = nrOfRecords;
            int skipCount = allData.Count - takeTrainCount;

            var dataset = allData.Skip(skipCount).Take(takeTrainCount).ToList();

            // Create histograms of each digit position
            Map<int, Histogram> histograms = new Map<int, Histogram>();
            for (int i = 0; i < 7; i++)
            {
                Histogram hist = new Histogram(
                    dataset.Select(d => (double)d[i]), 45, 1, 45);
                histograms.Add(i, hist);
            }

            int[] bestNumbersFirst = new int[7];
            int[] bestNumbersLast = new int[7];
            for (int i = 0; i < 7; i++)
            {
                var numberHist = histograms[i];
                int nrBest = Enumerable.Range(0, 45)
                    .OrderByDescending(n => (int)numberHist[n].Count)
                    .Select(n => (int)Math.Round(numberHist[n].LowerBound))
                    .First();
                bestNumbersFirst[i] = nrBest;
                int nrBest2 = Enumerable.Range(0, 45)
                    .OrderByDescending(n => (int)numberHist[n].Count)
                    .Select(n => (int)Math.Round(numberHist[n].LowerBound))
                    .Skip(1)
                    .First();
                bestNumbersLast[i] = nrBest2;
                //bestNumbersFirst[i] = histogram.First(h => h.Value[i] == histogram.Select(p => p.Value[i]).Max()).Key;
                //bestNumbersLast[i] = histogram.Last(h => h.Value[i] == histogram.Select(p => p.Value[i]).Max()).Key;
            }

            if (bestNumbersFirst.Distinct().Count() == bestNumbersFirst.Length)
            {
                return bestNumbersFirst;
            }

            return bestNumbersLast;
        }

        public static List<int[]> GetBestChances(int offset)
        {
            List<int[]> randomActuals = new List<int[]>();
            
            randomActuals.Add(GetBestChance(50, offset));
            randomActuals.Add(GetBestChance(100, offset));
            //randomActuals.Add(GetBestChance(150,  offset));
            randomActuals.Add(GetBestChance(200, offset));
            //randomActuals.Add(GetBestChance(250,  offset));
            randomActuals.Add(GetBestChance(1000,  offset));

            return randomActuals;
        }

        private void WriteToFile(RandomGuess data)
        {
            try
            {
                // Start logging from item 1
                if (data.RecordNumber > 99)
                {
                    StringBuilder sb = new StringBuilder()
                            .Append(data.RecordNumber).Append(", ")
                            //.Append("classifier input=")
                            .Append(string.Format("{0}", Arrays.ToString(data.ActualNumbers))).Append(",")
                            //.Append("prediction= ")
                            .Append(string.Format("{0}", Arrays.ToString(data.GetPrimaryPrediction()))).Append(",")
                            //.Append("correctGuesses=")
                            .Append(string.Format("{0}", data.GetPredictionScores())).Append(",")
                            //.Append("anomaly score=")
                            .Append(data.AnomalyFactor.ToString(NumberFormatInfo.InvariantInfo));
                    _pw.WriteLine(sb.ToString());
                    _pw.Flush();
                }
                //_predictedValues = newPredictions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _pw.Flush();
            }

        }

        public List<RandomGuess> GetGuesses()
        {
            return _predictions;
        }

        public RandomGuess GetLastGuess()
        {
            return _predictions.Last();
        }
    }

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
    public class NetworkApiRandom
    {
        /** modes to choose from to demonstrate network usage */
        public enum Mode { BasicCla, BasicClaPick, BasicSdr, MultiLayer, MultiRegion };

        private readonly Network.Network _network;

        private readonly FileInfo _outputFile;
        private readonly StreamWriter _pw;

        private Map<int, double[]> _predictedValues; // step, values
        private List<RandomGuess> _predictions;
        public List<PickThreeData> _predictionsPick;

        public NetworkApiRandom(Mode mode)
        {
            switch (mode)
            {
                case Mode.BasicCla:
                    _network = CreateBasicNetworkCla();
                    _network.Observe().Subscribe(GetSubscriber());
                    break;
                case Mode.BasicClaPick:
                    {
                        _network = CreateBasicNetworkClaPickThree();
                        _network.Observe().Subscribe(GetPickSubscriber());
                        break;
                    }
                case Mode.BasicSdr:
                    _network = CreateBasicNetworkSdr();
                    _network.Observe().Subscribe(GetSubscriber());
                    break;
                case Mode.MultiLayer:
                    _network = CreateMultiLayerNetwork();
                    _network.Observe().Subscribe(GetSubscriber());
                    break;
                case Mode.MultiRegion:
                    _network = CreateMultiRegionNetwork();
                    _network.Observe().Subscribe(GetSubscriber());
                    break;
            }

            try
            {
                _outputFile = new FileInfo("c:\\temp\\RandomData_output_" + mode + ".txt");
                if (_outputFile.Exists)
                {
                    _outputFile.Delete();
                }
                Debug.WriteLine("Creating output file: " + _outputFile);
                _pw = new StreamWriter(_outputFile.OpenWrite());
                _pw.WriteLine("RecordNum,Actual,Predicted,CorrectGuesses,AnomalyScore");
            }
            catch (IOException e)
            {
                Console.WriteLine(e);
            }
        }

        #region Network Creation

        /// <summary>
        /// Creates a basic <see cref="Network"/> with 1 <see cref="Region"/> and 1 <see cref="ILayer"/>. 
        /// However this basic network contains all algorithmic components.
        /// </summary>
        internal Network.Network CreateBasicNetworkCla()
        {
            if (_network != null)
            {
                return _network;
            }

            // First reverse the darn file
            ReverseRandomData();

            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap(
                ("Number 1", typeof(CLAClassifier)),
                ("Number 2", typeof(CLAClassifier)),
                ("Number 3", typeof(CLAClassifier)),
                ("Number 4", typeof(CLAClassifier)),
                ("Number 5", typeof(CLAClassifier)),
                ("Number 6", typeof(CLAClassifier)),
                ("Bonus", typeof(CLAClassifier))));

            return Network.Network.Create("RandomData Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                .Add(Network.Network.CreateLayer("Layer 2/3", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(Anomaly.Create())
                .Add(new TemporalMemory())
                .Add(new Algorithms.SpatialPooler())
                .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", $"RandomData_rev.csv")))));
        }

        internal Network.Network CreateBasicNetworkClaPickThree()
        {
            if (_network != null)
            {
                return _network;
            }

            // First reverse the darn file
            ReversePickThreeData();

            Parameters p = NetworkDemoHarness.GetPickParameters();
            p = p.Union(NetworkDemoHarness.GetPickThreeFieldEncodingParams());
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap(
                ("Number 1", typeof(CLAClassifier)),
                ("Number 2", typeof(CLAClassifier)),
                ("Number 3", typeof(CLAClassifier))));

            var nw2 = Network.Network.Create("PickThree Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                    .Add(Network.Network.CreateLayer("Layer 2/3", p)
                    .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                    .Add(Anomaly.Create())
                    .Add(new TemporalMemory())
                    .Add(new Algorithms.SpatialPooler())
                    .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                        SensorParams.Create(SensorParams.Keys.Path, "", "Pick3GameData_rev.csv")))));
            
            return nw2;
        }

        private void ReverseRandomData()
        {
            List<string> fileLines = YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).ToList();
            List<string> fileLinesReversed = new List<string>();

            // Copy header
            for (int i = 0; i < 3; i++)
            {
                fileLinesReversed.Add(fileLines[i]);
            }

            int takeCount = 150;
            int skipCount = fileLines.Count - 3 - takeCount - 10; // take last 110 records
            // Take the rest and reverse it
            for (int i = 0; i < 0; i++)
            {
                fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount).Take(takeCount));
            }
            fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount)); // last 10 record given, not trained

            using (StreamWriter sw = new StreamWriter("RandomData_rev.csv"))
            {
                foreach (string line in fileLinesReversed)
                {
                    sw.WriteLine(line);
                }
                sw.Flush();
                sw.Close();
            }
        }

        private void ReversePickThreeData()
        {
            List<string> fileLines = YieldingFileReader.ReadAllLines("Pick3GameData.csv", Encoding.UTF8).ToList();
            List<string> fileLinesReversed = new List<string>();

            // Copy header
            for (int i = 0; i < 3; i++)
            {
                fileLinesReversed.Add(fileLines[i]);
            }

            int takeCount = 100;
            int skipCount = fileLines.Count - 3 - takeCount - 30; // take last 110 records
            // Take the rest and reverse it
            for (int i = 0; i < 10; i++)
            {
                fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount).Take(takeCount));
            }
            fileLinesReversed.AddRange(fileLines.Skip(3).Reverse().Skip(skipCount)); // last 10 record given, not trained

            StreamWriter sw = new StreamWriter("Pick3GameData_rev.csv");
            foreach (string line in fileLinesReversed)
            {
                sw.WriteLine(line);
            }
            sw.Flush();
            sw.Close();
        }

        internal Network.Network CreateBasicNetworkSdr()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());
            // p.SetParameterByKey(Parameters.KEY.AUTO_CLASSIFY_TYPE, typeof(SDRClassifier));
            p.SetParameterByKey(Parameters.KEY.INFERRED_FIELDS, GetInferredFieldsMap(
                ("Number 1", typeof(SDRClassifier)),
                ("Number 2", typeof(SDRClassifier)),
                ("Number 3", typeof(SDRClassifier)),
                ("Number 4", typeof(SDRClassifier)),
                ("Number 5", typeof(SDRClassifier)),
                ("Number 6", typeof(SDRClassifier)),
                ("Bonus", typeof(SDRClassifier))));

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
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());

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
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());

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

                string[] classifierFields = { "Number 1", "Number 2", "Number 3", "Number 4", "Number 5", "Number 6", "Bonus" };
                Map<int, double[]> newPredictions = new Map<int, double[]>();
                // Step 1 is certainly there
                if (classifierFields.Any(cf => null != output.GetClassification(cf).GetMostProbableValue(1)))
                {
                    foreach (int step in output.GetClassification(classifierFields[0]).StepSet())
                    {
                        newPredictions.Add(step, classifierFields.Take(7)
                            .Select(cf => ((double?)output.GetClassification(cf).GetMostProbableValue(step)).GetValueOrDefault(-1)).ToArray());
                    }
                }
                else
                {
                    newPredictions = _predictedValues;
                }

                RandomGuess gd = RandomGuess.From(_predictedValues, newPredictions, output, classifierFields);

                if (gd.RecordNumber > 0)
                    _predictions.Add(gd);

                _predictedValues = newPredictions;

                WriteToFile(gd);
                //RecordStep(gd);
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

        internal IObserver<IInference> GetPickSubscriber()
        {
            return Observer.Create<IInference>(output =>
            {
                string[] classifierFields = { "Number 1", "Number 2", "Number 3" };

                Map<int, double[]> newPredictions = new Map<int, double[]>();
                // Step 1 is certainly there
                if (classifierFields.Any(cf => null != output.GetClassification(cf).GetMostProbableValue(1)))
                {
                    foreach (int step in output.GetClassification(classifierFields[0]).StepSet())
                    {
                        newPredictions.Add(step, classifierFields
                            .Select(cf => ((double?)output.GetClassification(cf).GetMostProbableValue(step)).GetValueOrDefault(-1)).ToArray());
                    }
                }
                else
                {
                    newPredictions = _predictedValues;
                }

                var gd = PickThreeData.From(_predictedValues, output, classifierFields);

                _predictionsPick.Add(gd);

                _predictedValues = newPredictions;

                WriteToFile(gd);
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

        #endregion

        //**
        // * Primitive file appender for collecting output. This just demonstrates how to use
        // * {@link Subscriber#onNext(Object)} to accomplish some work.
        // *
        // * @param infer             The {@link Inference} object produced by the Network
        // * @param classifierField   The field we use in this demo for anomaly computing.
        // */
        private void WriteToFile(RandomGuess data)
        {
            try
            {
                // Start logging from item 1
                if (data.RecordNumber > 99)
                {
                    StringBuilder sb = new StringBuilder()
                            .Append(data.RecordNumber).Append(", ")
                            //.Append("classifier input=")
                            .Append(string.Format("{0}", Arrays.ToString(data.ActualNumbers))).Append(",")
                            //.Append("prediction= ")
                            .Append(string.Format("{0}", Arrays.ToString(data.GetPrimaryPrediction()))).Append(",")
                            //.Append("correctGuesses=")
                            .Append(string.Format("{0}", data.GetPredictionScores())).Append(",")
                            //.Append("anomaly score=")
                            .Append(data.AnomalyFactor.ToString(NumberFormatInfo.InvariantInfo));
                    _pw.WriteLine(sb.ToString());
                    _pw.Flush();
                }
                //_predictedValues = newPredictions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _pw.Flush();
            }

        }

        private void WriteToFile(PickThreeData data)
        {
            try
            {
                // Start logging from item 1
                if (data.RecordNumber > 0)
                {
                    StringBuilder sb = new StringBuilder()
                            .Append(data.RecordNumber).Append(", ")
                            //.Append("classifier input=")
                            .Append(string.Format("{0}", Arrays.ToString(data.ActualNumbers))).Append(",")
                            //.Append("prediction= ")
                            .Append(string.Format("{0}", Arrays.ToString(data.PredictedNumbers))).Append(",")
                            //.Append("correctGuesses=")
                            .Append(string.Format("{0}", data.AnalysisResult)).Append(",")
                            //.Append("anomaly score=")
                            .Append(data.AnomalyFactor.ToString(NumberFormatInfo.InvariantInfo));
                    _pw.WriteLine(sb.ToString());
                    _pw.Flush();
                }
                //_predictedValues = newPredictions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                _pw.Flush();
            }

        }

        //private void RecordStep(IInference infer, string[] classifierFields)
        //{
        //    double[] newPredictions;
        //    if (classifierFields.Any(cf => null != infer.GetClassification(cf).GetMostProbableValue(1)))
        //    {
        //        newPredictions = classifierFields.Select(cf => ((double?)infer.GetClassification(cf).GetMostProbableValue(1)).GetValueOrDefault(-1)).ToArray();
        //    }
        //    else
        //    {
        //        newPredictions = _predictedValues;
        //    }
        //    if (infer.GetRecordNum() > 0)
        //    {
        //        double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)infer.GetClassifierInput()[cf]).Get("inputValue")).ToArray();
        //        double[] errors = ArrayUtils.Abs(ArrayUtils.Subtract(_predictedValues, actuals));
        //        double[] randomSequence = GetRandomGuesses();
        //        int correctGuesses = GetGuessCount(actuals, _predictedValues);
        //        int correctRandomGuesses = GetGuessCount(actuals, randomSequence);

        //        PredictionValue value = new PredictionValue();
        //        value.RecordNum = infer.GetRecordNum();
        //        value.ActualValues = actuals;
        //        value.PredictionErrors = errors;
        //        value.CorrectRandomGuesses = correctRandomGuesses;
        //        value.PredictedValues = GetDeviates(_predictedValues);
        //        value.CorrectGuesses = correctGuesses;
        //        value.CorrectGuesses = value.PredictedValues.Select(pv => GetGuessCount(actuals, pv)).Max();
        //        value.AnomalyFactor = infer.GetAnomalyScore();
        //        _predictions.Add(value);
        //        if (value.CorrectGuesses == 7)
        //        {
        //            Console.WriteLine($"({value.RecordNum}) = Bingo! ({Arrays.ToString(actuals)}) vs ({Arrays.ToString(_predictedValues)})");
        //        }
        //    }
        //    _predictedValues = newPredictions;
        //}

        /**
         * Simple run hook
         */
        public void RunNetwork()
        {
            _predictions = new List<RandomGuess>();
            _predictionsPick = new List<PickThreeData>();

            _network.Start();
            _network.GetTail().GetTail().GetLayerThread().Wait();
        }

        /**
 * @return a Map that can be used as the value for a Parameter
 * object's KEY.INFERRED_FIELDS key, to classify the specified
 * field with the specified Classifier type.
*/
        public static Map<string, Type> GetInferredFieldsMap(
            params (string field, Type classifier)[] args)
        {
            Map<string, Type> inferredFieldsMap = new Map<string, Type>();
            foreach ((string field, Type classifier) tuple in args)
            {
                inferredFieldsMap.Add(tuple.field, tuple.classifier);
            }

            return inferredFieldsMap;
        }

        public int GetNumberOfRounds()
        {
            return _predictions.Count;
        }

        public int GetTotalNumberOfGuesses()
        {
            return _predictions.Sum(p => p.Count);
        }

        public int GetTotalNumberOfPickPredictions()
        {
            return _predictionsPick.Count - 1;
        }

        public List<RandomGuess> GetGuesses()
        {
            return _predictions;
        }

        //public int GetHighestCorrectGuesses(double rangePct, bool fromBehind)
        //{
        //    int totalLength = _predictions.Count - 1;
        //    int takeRange = (int)(totalLength * rangePct);
        //    if (fromBehind)
        //    {
        //        int offset = totalLength - takeRange;
        //        int totalActual = _predictions.Skip(offset).Max(p => p.CorrectPredictionsWithBonus.Item1);

        //        return totalActual;
        //    }
        //    else
        //    {
        //        int totalActual = _predictions.Take(takeRange).Max(p => p.CorrectPredictionsWithBonus.Item1);
        //        return totalActual;
        //    }
        //}



        //public double GetAverageCorrectGuesses(double rangePct, bool fromBehind)
        //{
        //    int totalLength = _predictions.Count - 1;
        //    int takeRange = (int)(totalLength * rangePct);
        //    if (fromBehind)
        //    {
        //        int offset = totalLength - takeRange;
        //        double totalActual = _predictions.Skip(1).Skip(offset).Average(p => p.CorrectPredictionsWithBonus.Item1);

        //        return totalActual;
        //    }
        //    else
        //    {
        //        double totalActual = _predictions.Skip(1).Take(takeRange).Average(p => p.CorrectPredictionsWithBonus.Item1);
        //        return totalActual;
        //    }
        //}

    }
}