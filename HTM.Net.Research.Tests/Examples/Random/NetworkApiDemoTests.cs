using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Vision;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.Random
{
    [TestClass]
    public class NetworkApiDemoTests
    {
        #region Parameter Tests

        [TestMethod]
        public void TestGetParameters()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            Assert.AreEqual(71, p.Size());
        }

        [TestMethod]
        public void TestGetRandomDataFieldEncodingMap()
        {
            EncoderSettingsList fieldEncodings = NetworkDemoHarness.GetRandomDataFieldEncodingMap();
            Assert.AreEqual(8, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestSettingPermutableVariables()
        {
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, new PermuteFloat(0.001, 0.1));

            var pVars = p.GetPermutationVars();
            Assert.IsNotNull(pVars);
            Assert.AreEqual(1, pVars.Count);
            var alphaVar = pVars.Single();
            Assert.AreEqual(Parameters.KEY.CLASSIFIER_ALPHA, alphaVar.Item1);
            Assert.IsInstanceOfType(alphaVar.Item2, typeof(PermuteFloat));
        }

        [TestMethod]
        public void TestClaExperimentParameters()
        {
            // Should return a filled parameter list with default values
            var pars = ExperimentParameters.Default();
            Assert.IsNotNull(pars);
            Assert.AreEqual(26, pars.Size());
        }

        #endregion

        #region Pick Three

        private List<double[]> _pickActuals = new List<double[]>();
        private List<double[]> _randomActuals = new List<double[]>();

        private List<PickThreeData> GetPickData(int take = 25)
        {
            if (_pickActuals.Count == 0)
            {
                foreach (string line in YieldingFileReader.ReadAllLines("Pick3GameData.csv", Encoding.UTF8).Skip(3).Skip(4293).Take(take))
                {
                    double[] actuals = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(v => double.Parse(v)).ToArray();
                    _pickActuals.Add(actuals);
                }
            }

            List<PickThreeData> data = new List<PickThreeData>();
            int i = 0;
            foreach (double[] actuals in _pickActuals)
            {
                PickThreeData d = new PickThreeData(i++, actuals, null);
                data.Add(d);
            }

            Assert.IsTrue(data.Count > 0);
            return data;
        }

        [TestMethod]
        [DeploymentItem("Resources\\Pick3GameData.csv")]
        public void TestDistribution()
        {
            PickThreeData.Random = new XorshiftRandom(398731);
            List<PickThreeData> data = GetPickData(50);

            int countPositive = data.Count(d => d.NettoRandomResult > 0);

            Assert.IsTrue(countPositive > 0);

            Console.WriteLine($"Number of correct random guesses: {countPositive}/{data.Count}");

            var histo = data.GroupBy(d => d.RandomAnalysisResult).Select(g => new { g.Key, Count = g.Count() });
            Console.WriteLine("");
            foreach (var hLine in histo)
            {
                Console.WriteLine($"{hLine.Key} : {hLine.Count}");
            }

            Console.WriteLine($"Total Winnings: {data.Sum(d => d.NettoRandomResult)}");
            Console.WriteLine($"Total Cost    : {data.Count * 5}");
        }

        [TestMethod]
        [DeploymentItem("Resources\\Pick3GameData.csv")]
        public void TestDistributionGetBestSeed()
        {
            Map<int, int> results = new Map<int, int>();
            int maxCount = 0;
            for (int i = 398000; i < 400000; i++)
            {
                PickThreeData.Random = new XorshiftRandom(i);

                List<PickThreeData> data = GetPickData();
                int countPositive = data.AsParallel().WithDegreeOfParallelism(4).Count(d => d.NettoRandomResult > 0);
                if (countPositive > maxCount)
                {
                    results.Add(i, countPositive);
                    maxCount = countPositive;
                }
            }

            var bestCountPositive = results.Max(p => p.Value);
            Assert.IsTrue(bestCountPositive > 0);
            int seed = results.First(p => p.Value == bestCountPositive).Key;
            PickThreeData.Random = new XorshiftRandom(seed);
            Console.WriteLine($"Best seed was {seed} with {bestCountPositive} hits.");
            List<PickThreeData> bestData = GetPickData();
            Console.WriteLine($"Number of correct random guesses: {bestCountPositive}/{bestData.Count}");

            var histo = bestData.GroupBy(d => d.RandomAnalysisResult).Select(g => new { g.Key, Count = g.Count() });
            Console.WriteLine("");
            foreach (var hLine in histo)
            {
                Console.WriteLine($"{hLine.Key} : {hLine.Count}");
            }

            Console.WriteLine($"Total Winnings: {bestData.Sum(d => d.NettoRandomResult)}");
            Console.WriteLine($"Total Cost    : {bestData.Count * 5}");
        }

        [TestMethod]
        [DeploymentItem("Resources\\Pick3GameData.csv")]
        public void RunBasicPickThreeNetwork()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.BasicClaPick);
            demo.RunNetwork();


            double dRatio = 100.0 / 100;//demo.GetTotalNumberOfPickPredictions();

            var data = demo._predictionsPick;

            int skipCountCheck = data.Count - 100;
            int skipCountLast = data.Count - 30;

            Console.WriteLine("");
            Console.WriteLine("Predicted bucket list (step 1)");
            Console.WriteLine("");
            var allGuessesHisto = data.Skip(skipCountCheck).GroupBy(d => d.AnalysisResult[1]).Select(g => new { g.Key, Count = g.Count() });
            foreach (var guess in allGuessesHisto)
            {
                Console.WriteLine($"{guess.Count}\t= {guess.Key}\t({dRatio * guess.Count:0.00}%)");
            }

            Console.WriteLine("");
            Console.WriteLine("Predicted bucket list (step 5)");
            Console.WriteLine("");
            var allGuessesHisto2 = data.Skip(skipCountCheck).GroupBy(d => d.AnalysisResult[5]).Select(g => new { g.Key, Count = g.Count() });
            foreach (var guess in allGuessesHisto2)
            {
                Console.WriteLine($"{guess.Count}\t= {guess.Key}\t({dRatio * guess.Count:0.00}%)");
            }

            Console.WriteLine("");
            Console.WriteLine("Predicted bucket list (random)");
            Console.WriteLine("");
            var allRandomHisto = data.Skip(skipCountCheck).GroupBy(d => d.RandomAnalysisResult).Select(g => new { g.Key, Count = g.Count() });
            foreach (var guess in allRandomHisto)
            {
                Console.WriteLine($"{guess.Count}\t= {guess.Key}\t({dRatio * guess.Count:0.00}%)");
            }

            Console.WriteLine("");
            Console.WriteLine("Last 30 guesses bucket list (grouped) (step 1)");
            Console.WriteLine("");
            var lastGuesses = data.Skip(skipCountLast).GroupBy(d => d.AnalysisResult[1]).Select(g => new { g.Key, Count = g.Count() });
            foreach (var guess in lastGuesses)
            {
                Console.WriteLine($"{guess.Count}\t= {guess.Key}");
            }

            Console.WriteLine("");
            Console.WriteLine($"All time Cost: {data.Skip(skipCountCheck).Count()}, Revenue: {data.Skip(skipCountCheck).Sum(d => d.NettoResults[1])}, Profit: {data.Skip(skipCountCheck).Sum(d => d.NettoResults[1]) - data.Skip(skipCountCheck).Count()}");
            Console.WriteLine($"Last {data.Skip(skipCountLast).Count()} guesses Cost: {data.Skip(skipCountLast).Count()}, Revenue: {data.Skip(skipCountLast).Sum(d => d.NettoResults[1])}, Profit: {data.Skip(skipCountLast).Sum(d => d.NettoResults[1]) - data.Skip(skipCountLast).Count()}");
            Console.WriteLine("");
        }

        #endregion

        [TestMethod]
        public void TestGuesses()
        {
            // 6 digits + bonus
            int[] actuals = { 1, 2, 3, 4, 5, 6, 10 };
            int[] predicted = { 1, 2, 3, 4, 5, 6 };
            var guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            int correctGuesses = guess.Item1;
            bool correctBonus = guess.Item2;
            Assert.AreEqual(6, correctGuesses);
            Assert.AreEqual(false, correctBonus);

            actuals = new[] { 1, 2, 3, 4, 5, 6, 10 };
            predicted = new[] { 10, 2, 3, 4, 5, 6 };
            guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            correctGuesses = guess.Item1;
            correctBonus = guess.Item2;
            Assert.AreEqual(6, correctGuesses);
            Assert.AreEqual(true, correctBonus);

            predicted = new[] { 1, 2, 3, 5, 6, 11 };
            guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            correctGuesses = guess.Item1;
            correctBonus = guess.Item2;
            Assert.AreEqual(5, correctGuesses);
            Assert.AreEqual(false, correctBonus);

            predicted = new[] { 11, 2, 3, 4, 5, 6 };
            guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            correctGuesses = guess.Item1;
            correctBonus = guess.Item2;
            Assert.AreEqual(5, correctGuesses);
            Assert.AreEqual(false, correctBonus);

            actuals = new[] { 1, 2, 3, 4, 5, 6, 11 };
            predicted = new[] { 11, 2, 3, 4, 5, 12 };
            guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            correctGuesses = guess.Item1;
            correctBonus = guess.Item2;
            Assert.AreEqual(5, correctGuesses);
            Assert.AreEqual(true, correctBonus);

            actuals = new[] { 11, 2, 3, 4, 5, 6, 12 };
            predicted = new[] { 12, 2, 3, 4, 5, 6 };
            guess = RandomGameData.CalculateOneGuess(actuals, predicted);
            correctGuesses = guess.Item1;
            correctBonus = guess.Item2;
            Assert.AreEqual(6, correctGuesses);
            Assert.AreEqual(true, correctBonus);
        }

        private List<int[]> GetRandomData()
        {
            if (_randomActuals.Count == 0)
            {
                foreach (string line in YieldingFileReader.ReadAllLines("RandomData.csv", Encoding.UTF8).Skip(3))
                {
                    double[] actuals = line.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Skip(1).Select(v => double.Parse(v)).ToArray();
                    _randomActuals.Add(actuals);
                }
            }

            List<int[]> data = new List<int[]>();
            int i = 0;
            foreach (double[] actuals in _randomActuals)
            {
                data.Add(actuals.Select(d => (int)d).ToArray());
            }

            Assert.IsTrue(data.Count > 0);
            return data;
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void TestNumberDistributionMatrix()
        {
            var dataset = GetRandomData();

            Map<int, double[]> histo = new Map<int, double[]>();
            for (int i = 1; i <= 45; i++)
            {
                histo[i] = new double[7];
            }
            double ratio = 100.0 / dataset.Count;
            // no of digits
            for (int i = 0; i < 7; i++)
            {
                foreach (int[] numbers in dataset)
                {
                    histo[numbers[i]][i] += ratio;
                }
            }

            histo = new Map<int, double[]>(histo.OrderBy(p => p.Key).ToDictionary(k => k.Key, v => v.Value));
            Console.WriteLine("");
            Console.WriteLine("Chance histo matrix");
            Console.WriteLine("");
            Console.WriteLine(@"        1      2      3      4      5      6      7");
            Console.WriteLine("");
            foreach (var pair in histo)
            {
                Console.WriteLine($"{pair.Key:00} = {Arrays.ToString(pair.Value, "{0:00.00}")}");
            }
            Console.WriteLine("");

            var results = new
            {
                Min1 = dataset.Select(n => n[0]).Min(),
                Max1 = dataset.Select(n => n[0]).Max(),
                Min2 = dataset.Select(n => n[1]).Min(),
                Max2 = dataset.Select(n => n[1]).Max(),
                Min3 = dataset.Select(n => n[2]).Min(),
                Max3 = dataset.Select(n => n[2]).Max(),
                Min4 = dataset.Select(n => n[3]).Min(),
                Max4 = dataset.Select(n => n[3]).Max(),
                Min5 = dataset.Select(n => n[4]).Min(),
                Max5 = dataset.Select(n => n[4]).Max(),
                Min6 = dataset.Select(n => n[5]).Min(),
                Max6 = dataset.Select(n => n[5]).Max(),
                Min7 = dataset.Select(n => n[6]).Min(),
                Max7 = dataset.Select(n => n[6]).Max(),
            };

            for (int i = 0; i < 7; i++)
            {
                foreach (int[] numbers in dataset)
                {
                    histo[numbers[i]][i] += ratio;
                }
            }

            Console.WriteLine("Min Max of digits");
            Console.WriteLine("");
            Console.WriteLine($"  \t1\t2\t3\t4\t5\t6\t7");
            Console.WriteLine($"Min\t{results.Min1}\t{results.Min2}\t{results.Min3}\t{results.Min4}\t{results.Min5}\t{results.Min6}\t{results.Min7}");
            Console.WriteLine($"Max\t{results.Max1}\t{results.Max2}\t{results.Max3}\t{results.Max4}\t{results.Max5}\t{results.Max6}\t{results.Max7}");


        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void TestNumberMatrixMostPopular()
        {
            var dataset = GetRandomData().Skip(10).Take(150).ToList();

            Map<int, double[]> histo = new Map<int, double[]>();
            for (int i = 1; i <= 45; i++)
            {
                histo[i] = new double[7];
            }
            double ratio = 100.0 / dataset.Count;
            // no of digits
            for (int i = 0; i < 7; i++)
            {
                foreach (int[] numbers in dataset)
                {
                    histo[numbers[i]][i] += ratio;
                }
            }

            histo = new Map<int, double[]>(histo.OrderBy(p => p.Key).ToDictionary(k => k.Key, v => v.Value));

            int[] bestNumbersFirst = new int[7];
            int[] bestNumbersLast = new int[7];
            for (int i = 0; i < 7; i++)
            {
                bestNumbersFirst[i] = histo.First(h => h.Value[i] == histo.Select(p => p.Value[i]).Max()).Key;
                bestNumbersLast[i] = histo.Last(h => h.Value[i] == histo.Select(p => p.Value[i]).Max()).Key;
            }

            Console.WriteLine("");
            Console.WriteLine("Most popular numbers");
            Console.WriteLine("");
            Console.WriteLine($"{Arrays.ToString(bestNumbersFirst)}");
            Console.WriteLine($"{Arrays.ToString(bestNumbersLast)}");
            Console.WriteLine("");
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void TestNumberDistributionMatrixLast110()
        {
            var dataset = GetRandomData().Skip(10).Take(150).ToList(); // last 100 pulls

            Map<int, double[]> histo = new Map<int, double[]>();
            for (int i = 1; i <= 45; i++)
            {
                histo[i] = new double[7];
            }
            double ratio = 100.0 / dataset.Count;
            // no of digits
            for (int i = 0; i < 7; i++)
            {
                foreach (int[] numbers in dataset)
                {
                    histo[numbers[i]][i] += ratio;
                }
            }

            histo = new Map<int, double[]>(histo.OrderBy(p => p.Key).ToDictionary(k => k.Key, v => v.Value));
            Console.WriteLine("");
            Console.WriteLine("Chance histo matrix");
            Console.WriteLine("");
            Console.WriteLine(@"        1      2      3      4      5      6      7");
            Console.WriteLine("");
            foreach (var pair in histo)
            {
                Console.WriteLine($"{pair.Key:00} = {Arrays.ToString(pair.Value, "{0:00.00}")}");
            }
            Console.WriteLine("");

            var results = new
            {
                Min1 = dataset.Select(n => n[0]).Min(),
                Max1 = dataset.Select(n => n[0]).Max(),
                Min2 = dataset.Select(n => n[1]).Min(),
                Max2 = dataset.Select(n => n[1]).Max(),
                Min3 = dataset.Select(n => n[2]).Min(),
                Max3 = dataset.Select(n => n[2]).Max(),
                Min4 = dataset.Select(n => n[3]).Min(),
                Max4 = dataset.Select(n => n[3]).Max(),
                Min5 = dataset.Select(n => n[4]).Min(),
                Max5 = dataset.Select(n => n[4]).Max(),
                Min6 = dataset.Select(n => n[5]).Min(),
                Max6 = dataset.Select(n => n[5]).Max(),
                Min7 = dataset.Select(n => n[6]).Min(),
                Max7 = dataset.Select(n => n[6]).Max(),
            };

            for (int i = 0; i < 7; i++)
            {
                foreach (int[] numbers in dataset)
                {
                    histo[numbers[i]][i] += ratio;
                }
            }

            Console.WriteLine("Min Max of digits");
            Console.WriteLine("");
            Console.WriteLine($"  \t1\t2\t3\t4\t5\t6\t7");
            Console.WriteLine($"Min\t{results.Min1}\t{results.Min2}\t{results.Min3}\t{results.Min4}\t{results.Min5}\t{results.Min6}\t{results.Min7}");
            Console.WriteLine($"Max\t{results.Max1}\t{results.Max2}\t{results.Max3}\t{results.Max4}\t{results.Max5}\t{results.Max6}\t{results.Max7}");


        }

        [TestMethod]
        public void TestGuessesCounts()
        {
            double[] actuals = { 1, 2, 3, 4, 5, 6, 10 };
            double[] predicted = { 1, 2, 3, 4, 5, 6, 10 };
            RandomGameData gd = new RandomGameData(actuals, predicted);

            var results = RandomGameData.GetCountsOfCorrectPredictedGuessesInStrings(new List<RandomGameData> { gd, gd, gd });

            Assert.IsNotNull(results);
            Assert.AreEqual(1, results.Count);
            var item = results.First();
            Assert.AreEqual("6+", item.Key);
            Assert.AreEqual(2, item.Value);  // we skip the first prediction
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void TestCreateBasicNetwork()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.BasicCla);
            Network.Network n = demo.CreateBasicNetworkCla();
            Assert.AreEqual(1, n.GetRegions().Count);
            Assert.IsNotNull(n.GetRegions().First().Lookup("Layer 2/3"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 4"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 5"));
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void SimulateBasicNetworks()
        {
            List<RandomGuessNetworkApi> apis = new List<RandomGuessNetworkApi>();

            // Simulate 10 versions
            int iterations = 20;
            for (int i = (iterations - 1); i >= 0; i--)
            {
                RandomGuessNetworkApi network = new RandomGuessNetworkApi(100, 1, 0, false, i);
                apis.Add(network);
            }

            List<RandomGuess> guesses = new List<RandomGuess>();
            // Run all versions and take last guess for evaluation
            for (int index = 0; index < apis.Count; index++)
            {
                var api = apis[index];
                api.RunNetwork();

                guesses.Add(api.GetLastGuess());

                apis[index] = null;
                GC.Collect();
            }

            PrintHistogramAndProfits(guesses);

            var outputFile = new FileInfo("c:\\temp\\RandomData_output_Simulator.txt");
            if (outputFile.Exists)
            {
                outputFile.Delete();
            }
            Debug.WriteLine("Creating output file: " + outputFile);
            var pw = new StreamWriter(outputFile.OpenWrite());
            pw.WriteLine("RecordNum,Actual,Predicted,CorrectGuesses,AnomalyScore");
            foreach (RandomGuess guess in guesses)
            {
                WriteToFile(pw, guess);
            }
            pw.Close();
            pw.Dispose();

            Console.WriteLine("");
            Console.WriteLine("Next predictions:");
            int p = 1;
            foreach (var prediction in guesses.Last().NextPredictions)
            {
                Console.WriteLine($"{p++} - {Arrays.ToString(prediction)}");
            }
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void SimulateBasedOnStatistics()
        {
            int testCount = 40;
            var allData = GetRandomData();
            var statsData = allData.Skip(testCount).Reverse().ToList();
            var testData = allData.Take(testCount).Reverse().ToList();

            List<double[]> stats = new List<double[]>();

            stats.Add(RandomGuessNetworkApi.GetBestChance(50, 0, 0, statsData).Select(d => (double)d).ToArray());
            stats.Add(RandomGuessNetworkApi.GetBestChance(100, 0, 0, statsData).Select(d => (double)d).ToArray());
            //stats.Add(RandomGuessNetworkApi.GetBestChance(250, 0, 0, statsData).Select(d => (double)d).ToArray());
            //stats.Add(RandomGuessNetworkApi.GetBestChance(500, 0, 0, statsData).Select(d => (double)d).ToArray());
            //stats.Add(RandomGuessNetworkApi.GetBestChance(1000, 0, 0, statsData).Select(d => (double)d).ToArray());
            //stats.Add(RandomGuessNetworkApi.GetBestChance(2000, 0, 0, statsData).Select(d => (double)d).ToArray());
            stats.Add(RandomGuessNetworkApi.GetBestChance(statsData.Count, 0, 0, statsData).Select(d => (double)d).ToArray());

            foreach (double[] stat in stats)
            {
                Debug.WriteLine(Arrays.ToString(stat));
            }

            // Create guess collection
            List<RandomGuess> guesses = new List<RandomGuess>();
            for (int i = 0; i < testData.Count; i++)
            {
                RandomGuess guess = new RandomGuess(i + 1, testData[i].Select(d => (double)d).ToArray(), 0);

                foreach (double[] stat in stats)
                {
                    guess.AddPrediction(stat, false);
                }
                guesses.Add(guess);
            }

            double profit = 0;
            foreach (RandomGuess randomGuess in guesses)
            {
                Debug.WriteLine($"{randomGuess.RecordNumber} {randomGuess.GetPredictionScores()} {randomGuess.GetProfit()}");
                profit += randomGuess.GetProfit();
            }
            Debug.WriteLine($"Profit: {profit}");
        }

        private void WriteToFile(StreamWriter sw, RandomGuess data)
        {
            try
            {
                // Start logging from item 1
                if (data.RecordNumber >= 0)
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
                    sw.WriteLine(sb.ToString());
                    sw.Flush();
                }
                //_predictedValues = newPredictions;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                sw.Flush();
            }

        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void RunBasicNetwork()
        {
            RandomGuessNetworkApi demo = new RandomGuessNetworkApi(200, 20, 0, false);
            demo.RunNetwork();

            var allGuessesObjs = demo.GetGuesses().ToList();
            var lastGuessesObjs = RandomGuess.GetLastGuesses(allGuessesObjs, 10);
            var lastGuessObjs = RandomGuess.GetLastGuesses(allGuessesObjs, 1);

            PrintHistogramAndProfits(allGuessesObjs);
            PrintHistogramAndProfits(lastGuessesObjs);
            PrintHistogramAndProfits(lastGuessObjs);

            Console.WriteLine("Next predictions:");
            int p = 1;
            foreach (var prediction in lastGuessObjs.First().NextPredictions)
            {
                Console.WriteLine($"{p++} - {Arrays.ToString(prediction)}");
            }

            //Console.WriteLine("Random Guesses bucket list (grouped)");
            //var randomGuesses = RandomGameData.GetCountsOfCorrectRandomGuessesInStrings(demo.Data());
            //dRatio = 100.0 / (demo.GetNumberOfPredictions() - 1);
            //foreach (var guess in randomGuesses)
            //{
            //    Console.WriteLine($"{guess.Key}\t= {guess.Value}\t({dRatio * guess.Value:0.00}%)");
            //}

            //Console.WriteLine("");
            //Console.WriteLine($"Random Cost: {randomGuesses.Values.Sum()}, Revenue: {RandomGameData.GetApproxRevenue(randomGuesses)}, Profit: {RandomGameData.GetApproxRevenue(randomGuesses) - randomGuesses.Values.Sum()}");
            //Console.WriteLine("");
        }

        private void PrintHistogramAndProfits(List<RandomGuess> guessList)
        {
            double dRatio = 100.0 / guessList.Sum(g => g.Count);
            var lastGuessHisto = RandomGuess.GetHistogramMap(guessList);
            int winningRounds = guessList.Count(g => g.GetApproximateRevenue() > 0);
            int winningGuesses = guessList.Sum(g => g.Count(x => x.Revenue > 0));
            double dRationRounds = 100.0 / guessList.Count;
            Console.WriteLine("");
            Console.WriteLine("##################################################################");
            Console.WriteLine($"Histogram for last {guessList.Count} predictions");
            Console.WriteLine($"Winning rounds  pct = {dRationRounds * winningRounds:0.00}% ({winningRounds}/{guessList.Count})");
            Console.WriteLine($"Winning guesses pct = {dRatio * winningGuesses:0.00}% ({winningGuesses}/{guessList.Sum(g => g.Count)})");
            Console.WriteLine("##################################################################");
            Console.WriteLine("");
            foreach (var guess in lastGuessHisto)
            {
                Console.WriteLine($"> {guess.Key}\t= {guess.Value}\t({dRatio * guess.Value:0.00}%)");
            }

            Console.WriteLine("");
            Console.WriteLine($"Last {guessList.Count} guesses  Cost: {guessList.Sum(g => g.GetCost())}, Revenue: {guessList.Sum(g => g.GetApproximateRevenue())}, Profit: {guessList.Sum(g => g.GetProfit())}");
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunBasicNetworkSdr()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.BasicSdr);
            demo.RunNetwork();

            //Console.WriteLine("From last predictions to first predictions");
            //for (int i = 10; i > 0; i--)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            //}
            //Console.WriteLine("From first predictions to last predictions");
            //for (int i = 1; i <= 10; i++)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: {0}/7", demo.GetHighestCorrectGuesses(pct, false), pct);
            //}

            //Console.WriteLine("Guesses bucket list (grouped)");
            //var allGuesses = demo.GetGuesses();
            //for (int i = 0; i < 8; i++)
            //{
            //    Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            //}
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunMultiLayerNetwork()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.MultiLayer);
            demo.RunNetwork();

            //Console.WriteLine("From last predictions to first predictions");
            //for (int i = 10; i > 0; i--)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            //}
            //Console.WriteLine("From first predictions to last predictions");
            //for (int i = 1; i <= 10; i++)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: {0}/7", demo.GetHighestCorrectGuesses(pct, false), pct);
            //}

            //Console.WriteLine("Guesses bucket list (grouped)");
            //var allGuesses = demo.GetGuesses();
            //for (int i = 0; i < 8; i++)
            //{
            //    Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            //}
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunMultiRegionNetwork()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.MultiRegion);
            demo.RunNetwork();

            //Console.WriteLine("From last predictions to first predictions");
            //for (int i = 10; i > 0; i--)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            //}
            //Console.WriteLine("From first predictions to last predictions");
            //for (int i = 1; i <= 10; i++)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: {0}/7", demo.GetHighestCorrectGuesses(pct, false), pct);
            //}

            //Console.WriteLine("Guesses bucket list (grouped)");
            //var allGuesses = demo.GetGuesses();
            //for (int i = 0; i < 8; i++)
            //{
            //    Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            //}
        }

        [TestMethod]
        public void TestCombinationParameters()
        {
            CombiParameters combiParams = new CombiParameters();

            combiParams.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, new PermuteInt(9, 16));
            combiParams.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, new PermuteInt(11, 16));

            var numCombinations = combiParams.GetNumCombinations();
            Debug.WriteLine($"Combinations #: {numCombinations}");
        }
    }
}