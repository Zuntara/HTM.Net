using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Reactive;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Tests.Properties;
using HTM.Net.Research.Vision;
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
    public class NetworkApiRandom
    {
        /** modes to choose from to demonstrate network usage */
        public enum Mode { BasicCla, BasicSdr, MultiLayer, MultiRegion };

        private readonly Network.Network _network;

        private readonly FileInfo _outputFile;
        private readonly StreamWriter _pw;

        private Map<int, double[]> _predictedValues; // step, values
        private List<RandomGameData> _predictions;

        public NetworkApiRandom(Mode mode)
        {
            switch (mode)
            {
                case Mode.BasicCla: _network = CreateBasicNetworkCla(); break;
                case Mode.BasicSdr: _network = CreateBasicNetworkSdr(); break;
                case Mode.MultiLayer: _network = CreateMultiLayerNetwork(); break;
                case Mode.MultiRegion: _network = CreateMultiRegionNetwork(); break;
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

        #region Network Creation

        /// <summary>
        /// Creates a basic <see cref="Network"/> with 1 <see cref="Region"/> and 1 <see cref="ILayer"/>. 
        /// However this basic network contains all algorithmic components.
        /// </summary>
        internal Network.Network CreateBasicNetworkCla()
        {
            // First reverse the darn file
            ReverseRandomData();

            Parameters p = NetworkDemoHarness.GetParameters();
            p = p.Union(NetworkDemoHarness.GetRandomDataFieldEncodingParams());

            return Network.Network.Create("RandomData Demo", p)
                .Add(Network.Network.CreateRegion("Region 1")
                .Add(Network.Network.CreateLayer("Layer 2/3", p)
                .AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true)
                .Add(Anomaly.Create())
                .Add(new TemporalMemory())
                .Add(new Algorithms.SpatialPooler())
                .Add(Sensor<FileInfo>.Create(FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", "RandomData_rev.csv")))));
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

            // Take the rest and reverse it
            fileLinesReversed.AddRange(fileLines.Skip(3).Reverse());

            StreamWriter sw = new StreamWriter("RandomData_rev.csv");
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
                if (classifierFields.Take(6).Any(cf => null != output.GetClassification(cf).GetMostProbableValue(1)))
                {
                    foreach (int step in output.GetClassification(classifierFields[0]).StepSet())
                    {
                        newPredictions.Add(step, classifierFields.Take(6)
                            .Select(cf => ((double?)output.GetClassification(cf).GetMostProbableValue(step)).GetValueOrDefault(-1)).ToArray());
                    }
                }
                else
                {
                    newPredictions = _predictedValues;
                }

                var gd = RandomGameData.From(_predictedValues, output, classifierFields);
                _predictions.Add(gd);

                _predictedValues = newPredictions;

                //WriteToFile(gd);
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

        #endregion

        ///**
        // * Primitive file appender for collecting output. This just demonstrates how to use
        // * {@link Subscriber#onNext(Object)} to accomplish some work.
        // *
        // * @param infer             The {@link Inference} object produced by the Network
        // * @param classifierField   The field we use in this demo for anomaly computing.
        // */
        //private void WriteToFile(IInference infer, string[] classifierFields)
        //{
        //    try
        //    {
        //        //double[] newPredictions;
        //        //if (classifierFields.Any(cf => null != infer.GetClassification(cf).GetMostProbableValue(1)))
        //        //{
        //        //    newPredictions = classifierFields.Select(cf => ((double?)infer.GetClassification(cf).GetMostProbableValue(1)).GetValueOrDefault(-1)).ToArray();
        //        //}
        //        //else
        //        //{
        //        //    newPredictions = _predictedValues;
        //        //}
        //        // Start logging from item 1
        //        if (infer.GetRecordNum() > 0)
        //        {
        //            double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)infer.GetClassifierInput()[cf]).Get("inputValue")).ToArray();
        //            //double[] errors = ArrayUtils.Abs(ArrayUtils.Subtract(_predictedValues, actuals));
        //            int correctGuesses = GetGuessCount(actuals, _predictedValues);
        //            StringBuilder sb = new StringBuilder()
        //                    .Append(infer.GetRecordNum()).Append(", ")
        //                    //.Append("classifier input=")
        //                    .Append(string.Format("{0}", Arrays.ToString(actuals))).Append(",")
        //                    //.Append("prediction= ")
        //                    .Append(string.Format("{0}", Arrays.ToString(_predictedValues))).Append(",")
        //                    //.Append("correctGuesses=")
        //                    .Append(string.Format("{0}", correctGuesses)).Append(",")
        //                    //.Append("anomaly score=")
        //                    .Append(infer.GetAnomalyScore().ToString(NumberFormatInfo.InvariantInfo));
        //            _pw.WriteLine(sb.ToString());
        //            _pw.Flush();
        //            //if (infer.GetRecordNum() % 100 == 0)
        //            //{
        //            //    Console.WriteLine(sb.ToString());
        //            //}
        //        }
        //        //_predictedValues = newPredictions;
        //    }
        //    catch (Exception e)
        //    {
        //        Console.WriteLine(e);
        //        _pw.Flush();
        //    }

        //}

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
            _predictions = new List<RandomGameData>();

            _network.Start();
            _network.GetTail().GetTail().GetLayerThread().Wait();
        }

        public int GetNumberOfPredictions()
        {
            return _predictions.Count;
        }

        public int GetTotalNumberOfPredictions()
        {
            return _predictions.Skip(1).Sum(p => p.CorrectDeviationPredictionsWithBonus.Count + 1);
        }

        public List<RandomGameData> Data()
        {
            return _predictions;
        }

        public int GetHighestCorrectGuesses(double rangePct, bool fromBehind)
        {
            int totalLength = _predictions.Count - 1;
            int takeRange = (int)(totalLength * rangePct);
            if (fromBehind)
            {
                int offset = totalLength - takeRange;
                int totalActual = _predictions.Skip(1).Skip(offset).Max(p => p.CorrectPredictionsWithBonus.Item1);

                return totalActual;
            }
            else
            {
                int totalActual = _predictions.Skip(1).Take(takeRange).Max(p => p.CorrectPredictionsWithBonus.Item1);
                return totalActual;
            }
        }



        public double GetAverageCorrectGuesses(double rangePct, bool fromBehind)
        {
            int totalLength = _predictions.Count - 1;
            int takeRange = (int)(totalLength * rangePct);
            if (fromBehind)
            {
                int offset = totalLength - takeRange;
                double totalActual = _predictions.Skip(1).Skip(offset).Average(p => p.CorrectPredictionsWithBonus.Item1);

                return totalActual;
            }
            else
            {
                double totalActual = _predictions.Skip(1).Take(takeRange).Average(p => p.CorrectPredictionsWithBonus.Item1);
                return totalActual;
            }
        }

    }

    public class RandomGameData
    {
        private static readonly IRandom Random = new XorshiftRandom(42);

        public RandomGameData(double[] actuals, double[] predicted)
        {
            RandomNumbers = GetRandomGuesses();
            ActualNumbers = actuals.Select(a => (int)a).ToArray();
            if (predicted != null)
            {
                PredictedNumbers = predicted.Select(a => (int)a).ToArray();
                DeviatedNumbers = GetDeviates(predicted, 20);
                CalculateGuessCounts();
                CalculateDeviationGuessCounts();
            }
        }

        public int RecordNumber { get; set; }

        public int[] ActualNumbers { get; set; }

        public int[] PredictedNumbers { get; set; }

        public int[] RandomNumbers { get; set; }

        public List<int[]> DeviatedNumbers { get; set; }

        public Tuple<int, bool> CorrectPredictionsWithBonus { get; set; }
        public List<Tuple<int, bool>> CorrectDeviationPredictionsWithBonus { get; set; }
        public Tuple<int, bool> CorrectRandomPredictionsWithBonus { get; set; }

        public double AnomalyFactor { get; set; }

        // Helper methods

        private void CalculateGuessCounts()
        {
            CorrectPredictionsWithBonus = CalculateOneGuess(ActualNumbers, PredictedNumbers);
            CorrectRandomPredictionsWithBonus = CalculateOneGuess(ActualNumbers, RandomNumbers);
        }

        private void CalculateDeviationGuessCounts()
        {
            List<Tuple<int, bool>> allResults = new List<Tuple<int, bool>>();
            foreach (int[] numbers in DeviatedNumbers)
            {
                allResults.Add(CalculateOneGuess(ActualNumbers, numbers));
            }
            CorrectDeviationPredictionsWithBonus = allResults;


        }

        public static Tuple<int, bool> CalculateOneGuess(int[] actuals, int[] predicted)
        {
            bool bonusHit = false;
            int correctNumbersAct = 0;

            Stack<int> predStack = new Stack<int>(predicted.Take(6)); // limit to 6 numbers
            List<int> actualList = new List<int>(actuals);

            while (predStack.Count > 0)
            {
                var predValue = predStack.Pop();

                if (actualList.Contains(predValue))
                {
                    int index = actualList.IndexOf(predValue);
                    actualList.RemoveAt(index);
                    correctNumbersAct++;
                }
            }
            bool correctBonus = predicted.Contains(actuals.Last());
            return new Tuple<int, bool>(correctNumbersAct, correctBonus);
        }

        /// <summary>
        /// Sets numbers that are close to the predictions
        /// </summary>
        /// <param name="currentPredictions"></param>
        /// <param name="count">number of deviations to set</param>
        private List<int[]> GetDeviates(double[] currentPredictions, int count = 20)
        {
            CombinationParameters cp = new CombinationParameters();

            for (int i = 0; i < currentPredictions.Length; i++)
            {
                if (currentPredictions[i] % 1.0 > double.Epsilon)
                {
                    int low = (int)Math.Floor(currentPredictions[i]);
                    int high = (int)Math.Ceiling(currentPredictions[i]);
                    cp.Define($"n{i + 1}", new List<object> { low, high }.Distinct().ToList());
                }
                else
                {
                    int pred = (int)currentPredictions[i];
                    cp.Define($"n{i + 1}", new List<object> { pred });
                }
            }
            return cp.GetAllCombinations().Select(c => c.Select(TypeConverter.Convert<int>).ToArray())
                .Where(n => !Arrays.AreEqual(n, PredictedNumbers))
                .Take(count)
                .ToList();
        }

        public static int[] GetCountsOfCorrectRandomGuesses(IEnumerable<RandomGameData> collection)
        {
            return // skip first prediction, it's bogus
                collection.Skip(1)
                    .Select(gd => gd.CorrectRandomPredictionsWithBonus.Item1 + (gd.CorrectRandomPredictionsWithBonus.Item2 ? 1 : 0))
                    .ToArray();
        }

        public static Map<string, int> GetCountsOfCorrectRandomGuessesInStrings(IEnumerable<RandomGameData> collection)
        {
            // skip first prediction, it's bogus
            var results = collection.Skip(1)
                .GroupBy(g => g.CorrectRandomPredictionsWithBonus)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            Map<string, int> summary = new Map<string, int>();
            foreach (var item in results)
            {
                summary.Add(item.Id, item.Value);
            }

            string[] relevantStrings = { "2+", "3", "3+", "4", "4+", "5", "5+", "6", "6+" };

            Map<string, int> retVal = new Map<string, int>();
            retVal["--"] = 0;
            foreach (var pair in summary)
            {
                if (relevantStrings.Contains(pair.Key))
                {
                    retVal[pair.Key] = pair.Value;
                }
                else
                {
                    retVal["--"] += pair.Value;
                }
            }

            return retVal;
        }


        public static int[] GetCountsOfCorrectPredictedGuesses(IEnumerable<RandomGameData> collection)
        {
            return // skip first prediction, it's bogus
                collection.Skip(1)
                    .Select(gd => gd.CorrectPredictionsWithBonus.Item1 + (gd.CorrectPredictionsWithBonus.Item2 ? 1 : 0))
                    .ToArray();
        }

        public static Map<string, int> GetCountsOfCorrectPredictedGuessesInStrings(IEnumerable<RandomGameData> collection)
        {
            // skip first prediction, it's bogus
            var results = collection.Skip(1)
                .GroupBy(g => g.CorrectPredictionsWithBonus)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            Map<string, int> summary = new Map<string, int>();
            foreach (var item in results)
            {
                summary.Add(item.Id, item.Value);
            }

            results = collection.Skip(1)
                .SelectMany(d => d.CorrectDeviationPredictionsWithBonus)
                .GroupBy(g => g)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            foreach (var item in results)
            {
                if (summary.ContainsKey(item.Id))
                {
                    summary[item.Id] += item.Value;
                }
                else
                {
                    summary.Add(item.Id, item.Value);
                }
            }

            string[] relevantStrings = { "2+", "3", "3+", "4", "4+", "5", "5+", "6", "6+" };

            Map<string, int> retVal = new Map<string, int>();
            retVal["--"] = 0;
            foreach (var pair in summary)
            {
                if (relevantStrings.Contains(pair.Key))
                {
                    retVal[pair.Key] = pair.Value;
                }
                else
                {
                    retVal["--"] += pair.Value;
                }
            }

            return retVal;
        }

        public static Map<string, int> GetLastGuesses(IList<RandomGameData> collection, int lastCount)
        {
            return GetCountsOfCorrectPredictedGuessesInStrings(collection.Skip(collection.Count - lastCount));
        }

        private int[] GetRandomGuesses()
        {
            return ArrayUtils.Range(0, 6).Select(i => Random.NextInt(45) + 1).ToArray();
        }

        public static double GetCost(Map<string, int> results)
        {
            return results.Values.Sum(); // 1 eur per record
        }

        public static double GetApproxRevenue(Map<string, int> results)
        {
            double rev = 0;

            foreach (var result in results)
            {
                if (result.Key == "2+") rev += result.Value * 3.00;
                if (result.Key == "3") rev += result.Value * 5.00;
                if (result.Key == "3+") rev += result.Value * 11.50;
                if (result.Key == "4") rev += result.Value * 26.50;
                if (result.Key == "4+") rev += result.Value * 300.00;
                if (result.Key == "5") rev += result.Value * 1200.00;
                if (result.Key == "5+") rev += result.Value * 16500.00;
                if (result.Key == "6" || result.Key == "6+") rev += result.Value * 1000000.0; // avg
            }

            return rev;
        }

        public static RandomGameData From(Map<int, double[]> previousPredicted, IInference inference, string[] classifierFields)
        {
            double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)inference.GetClassifierInput()[cf]).Get("inputValue")).ToArray();

            RandomGameData gd = new RandomGameData(actuals, previousPredicted?[1]);

            List<double[]> dNumbers = new List<double[]>();
            foreach (int step in inference.GetClassification(classifierFields[0]).StepSet())
            {
                dNumbers.Add(previousPredicted?[step]);
            }

            dNumbers = dNumbers.Where(n => n != null).ToList();
            if (dNumbers.Any())
            {
                foreach (double[] numbers in dNumbers)
                {
                    var devs = gd.GetDeviates(numbers);
                    gd.DeviatedNumbers.AddRange(devs);
                }
                gd.DeviatedNumbers = gd.DeviatedNumbers.Where(n => n != null && !Arrays.AreEqual(n, gd.PredictedNumbers)).Take(10).ToList();
                gd.CalculateDeviationGuessCounts();
            }
            return gd;
        }
    }
}