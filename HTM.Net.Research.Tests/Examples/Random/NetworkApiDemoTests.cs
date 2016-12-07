using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Encoders;
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
        public void TestGetNetworkDemoTestEncoderParams()
        {
            Parameters p = NetworkDemoHarness.GetNetworkDemoTestEncoderParams();
            Assert.AreEqual(28, p.Size());
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
        public void RunBasicNetwork()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.BasicCla);
            demo.RunNetwork();

            Console.WriteLine("From last predictions to first predictions");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}",
                    demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            }
            //Console.WriteLine("From first predictions to last predictions");
            //for (int i = 1; i <= 10; i++)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: {0}/7", demo.GetHighestCorrectGuesses(pct, false), pct);
            //}

            Console.WriteLine("Predicted bucket list (grouped)");
            double dRatio = 100.0 / demo.GetTotalNumberOfPredictions();

            var allGuesses = RandomGameData.GetCountsOfCorrectPredictedGuessesInStrings(demo.Data());

            foreach (var guess in allGuesses)
            {
                Console.WriteLine($"{guess.Key}\t= {guess.Value}\t({dRatio * guess.Value:0.00}%)");
            }

            Console.WriteLine("Last 10 guesses bucket list (grouped)");
            var lastGuesses = RandomGameData.GetLastGuesses(demo.Data(), 10);
            foreach (var guess in lastGuesses)
            {
                Console.WriteLine($"> {guess.Key}\t= {guess.Value}");
            }

            Console.WriteLine("");
            Console.WriteLine($"All time Cost: {RandomGameData.GetCost(allGuesses)}, Revenue: {RandomGameData.GetApproxRevenue(allGuesses)}, Profit: {RandomGameData.GetApproxRevenue(allGuesses) - RandomGameData.GetCost(allGuesses)}");
            Console.WriteLine($"Last 10 guesses Cost: {RandomGameData.GetCost(lastGuesses)}, Revenue: {RandomGameData.GetApproxRevenue(lastGuesses)}, Profit: {RandomGameData.GetApproxRevenue(lastGuesses) - RandomGameData.GetCost(lastGuesses)}");
            Console.WriteLine("");

            Console.WriteLine("Random Guesses bucket list (grouped)");
            var randomGuesses = RandomGameData.GetCountsOfCorrectRandomGuessesInStrings(demo.Data());
            dRatio = 100.0 / (demo.GetNumberOfPredictions()-1);
            foreach (var guess in randomGuesses)
            {
                Console.WriteLine($"{guess.Key}\t= {guess.Value}\t({dRatio * guess.Value:0.00}%)");
            }

            Console.WriteLine("");
            Console.WriteLine($"Random Cost: {RandomGameData.GetCost(randomGuesses)}, Revenue: {RandomGameData.GetApproxRevenue(randomGuesses)}, Profit: {RandomGameData.GetApproxRevenue(randomGuesses) - RandomGameData.GetCost(randomGuesses)}");
            Console.WriteLine("");
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunBasicNetworkSdr()
        {
            NetworkApiRandom demo = new NetworkApiRandom(NetworkApiRandom.Mode.BasicSdr);
            demo.RunNetwork();

            Console.WriteLine("From last predictions to first predictions");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            }
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

            Console.WriteLine("From last predictions to first predictions");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            }
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

            Console.WriteLine("From last predictions to first predictions");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            }
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