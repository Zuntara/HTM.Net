using System;
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
            double[] actuals = {1, 2, 3, 4, 5, 6, 10};
            double[] predicted = { 1, 2, 3, 4, 5, 6, 10 };
            int correctGuesses = NetworkAPIDemo.GetGuessCount(actuals, predicted);
            Assert.AreEqual(7, correctGuesses);

            predicted =new double[] { 1, 2, 3, 4, 5, 6, 11 };
            correctGuesses = NetworkAPIDemo.GetGuessCount(actuals, predicted);
            Assert.AreEqual(6, correctGuesses);

            predicted = new double[] { 11, 2, 3, 4, 5, 6, 11 };
            correctGuesses = NetworkAPIDemo.GetGuessCount(actuals, predicted);
            Assert.AreEqual(5, correctGuesses);

            actuals = new double[] { 1, 2, 3, 4, 5, 6, 11 };
            predicted = new double[] { 11, 2, 3, 4, 5, 6, 11 };
            correctGuesses = NetworkAPIDemo.GetGuessCount(actuals, predicted);
            Assert.AreEqual(6, correctGuesses);

            actuals = new double[] { 11, 2, 3, 4, 5, 6, 11 };
            predicted = new double[] { 12, 2, 3, 4, 5, 6, 11 };
            correctGuesses = NetworkAPIDemo.GetGuessCount(actuals, predicted);
            Assert.AreEqual(6, correctGuesses);
        }

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        public void TestCreateBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_CLA);
            Network.Network n = demo.CreateBasicNetworkCla();
            Assert.AreEqual(1, n.GetRegions().Count);
            Assert.IsNotNull(n.GetRegions().First().Lookup("Layer 2/3"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 4"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 5"));
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_CLA);
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

            Console.WriteLine("Guesses bucket list (grouped)");
            var allGuesses = demo.GetGuesses();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            }
            allGuesses = demo.GetRandomGuessesCounts();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Random correct: {i} = {allGuesses.Count(g => g == i)}");
            }
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunBasicNetworkSdr()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_SDR);
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

            Console.WriteLine("Guesses bucket list (grouped)");
            var allGuesses = demo.GetGuesses();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            }
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunMultiLayerNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTILAYER);
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

            Console.WriteLine("Guesses bucket list (grouped)");
            var allGuesses = demo.GetGuesses();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            }
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\RandomData.csv")]
        public void RunMultiRegionNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTIREGION);
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

            Console.WriteLine("Guesses bucket list (grouped)");
            var allGuesses = demo.GetGuesses();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
            }
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