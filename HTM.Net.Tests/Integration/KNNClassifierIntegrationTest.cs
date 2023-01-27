using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Util;
using log4net.Repository.Hierarchy;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Complex;
using MathNet.Numerics.Random;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Integration
{
    // https://github.com/numenta/nupic/blob/1410fcc4e31dc0907130460bcf6085a4f3badaeb/tests/integration/nupic/algorithms/knn_classifier_test/classifier_test.py
    [TestClass]
    public class KNNClassifierIntegrationTest
    {
        private PCAKNNData knnData = new PCAKNNData();

        private const int TRAIN = 0;
        private const int TEST = 1;

        private Random _random = new Random(1942);

        [TestMethod]
        public void TestPCAKNNShort()
        {
            RunTestPCAKNN(0);
        }

        [TestMethod]
        [DeploymentItem("Resources\\test_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\test_pcaknnshort_data.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_data.txt")]
        public void TestPCAKNNMedium()
        {
            PCAKNNData.Generate();

            RunTestPCAKNN(1);
        }

        private void RunTestPCAKNN(int @short)
        {
            Console.WriteLine("\nTesting PCA/k-NN classifier");
            Console.WriteLine($"Mode = {@short}");

            int numDims = 10;
            int numClasses = 10;
            int k = 11;
            int numPatternsPerClass = 100;
            int numPatterns = (int)Math.Round(0.9 * numClasses * numPatternsPerClass);
            int numTests = numClasses * numPatternsPerClass - numPatterns;
            int numSVDSamples = (int)Math.Round(0.1 * numPatterns);
            int keep = 1;

            KNNClassifier pca_knn = KNNClassifier.GetBuilder()
                .K(k)
                .NumSVDSamples(numSVDSamples)
                .NumSVDDims(keep)
                .Build();

            KNNClassifier knn = KNNClassifier.GetBuilder()
                .K(k)
                .Build();

            Console.WriteLine("Training PCA k-NN");

            var dataTuple = knnData.Generate(
                numDims, numClasses,
                k, numPatternsPerClass,
                numPatterns, numTests,
                numSVDSamples, keep);

            for (int i = 0; i < numPatterns; i++)
            {
                knn.Learn(dataTuple.trainData[i], dataTuple.trainClass[i]);
                pca_knn.Learn(dataTuple.trainData[i], dataTuple.trainClass[i]);
            }

            Console.WriteLine("Testing PCA k-NN");
            int numWinnerFailures = 0;
            int numInferenceFailures = 0;
            int numDistFailures = 0;
            int numAbsErrors = 0;

            for (int i = 0; i < numTests; i++)
            {
                var result = knn.Infer(dataTuple.testData[i]);
                var winner = (int?)result.GetWinner();
                var inference = result.GetInference();
                var dist = (Vector<double>)result.GetProtoDistance();
                var categoryDist = result.GetCategoryDistances();

                var pcaResult = pca_knn.Infer(dataTuple.testData[i]);
                var pcawinner = (int?)pcaResult.GetWinner();
                var pcainference = pcaResult.GetInference();
                var pcadist = (Vector<double>)pcaResult.GetProtoDistance();
                var pcaCategoryDist = pcaResult.GetCategoryDistances();

                if (winner != dataTuple.testClass[i])
                {
                    numAbsErrors += 1;
                    Console.WriteLine($"> Failed winner to testclass @{i} - {winner} vs {dataTuple.testClass[i]}");
                }
                if (pcawinner != winner)
                {
                    numWinnerFailures += 1;
                    Console.WriteLine($"> Failed winner @{i} - {pcawinner} vs {winner}");
                }
                if (Vector<double>.Abs(pcainference - inference).Any(n => n > 1e-4))
                {
                    numInferenceFailures += 1;
                }
                if (Vector<double>.Abs(pcadist - dist).Any(n => n > 1e-4))
                {
                    numDistFailures += 1;
                }
            }

            double s0 = 100.0 * (numTests - numAbsErrors) / numTests;
            double s1 = 100.0 * (numTests - numWinnerFailures) / numTests;
            double s2 = 100.0 * (numTests - numInferenceFailures) / numTests;
            double s3 = 100.0 * (numTests - numDistFailures) / numTests;

            Console.WriteLine("PCA/k-NN success rate = {0}%", s0);
            Console.WriteLine("Winner success = {0}%", s1);
            Console.WriteLine("Inference success = {0}%", s2);
            Console.WriteLine("Distance success = {0}%", s3);

            Assert.AreEqual(100.0, s0, "PCA/k-NN test failed. (abs errors)");
            Assert.AreEqual(100.0, s1, "PCA/k-NN test failed. (winners)");
            Assert.AreEqual(100.0, s2, "PCA/k-NN test failed. (inference)");
        }

        [TestMethod]
        public void TestKNNClassifierShort()
        {
            RunTestKNNClassifier(0);
        }

        [TestMethod]
        public void TestKNNClassifierMedium()
        {
            RunTestKNNClassifier(1);
        }

        [TestMethod]
        public void TestCategories()
        {
            Random random = new Random(42);
            (string failures, KNNClassifier knn) = SimulateCategories(random);

            Assert.AreEqual(0, failures.Length, $"Tests failed: {failures}");
        }

        /// <summary>
        /// Simulate running KNN classifier on many disjoint categories
        /// </summary>
        /// <returns></returns>
        /// <exception cref="NotImplementedException"></exception>
        private (string failures, KNNClassifier knn) SimulateCategories(Random random, int numSamples = 100, int numDimensions = 500)
        {
            string failures = string.Empty;
            Console.WriteLine("Testing the sparse KNN Classifier on many disjoint categories");

            var knn = KNNClassifier.GetBuilder()
                .K(1)
                .DistanceNorm(1.0)
                .UseSparseMemory(true)
                .Build();
            for (int i = 0; i < numSamples; i++)
            {
                // select category randomly and generate vector
                var c = 2 * random.Next(0, 50) + 50;
                var v = CreatePattern(random, c, numDimensions);
                knn.Learn(v, c);
            }

            // Go through each category and ensure we have at least one from each!
            for (int i = 0; i < 50; i++)
            {
                var c = 2 * i + 50;
                var v = CreatePattern(random, c, numDimensions);
                knn.Learn(v, c);
            }

            int errors = 0;
            for (int i = 0; i < numSamples; i++)
            {
                // Select category randomly and generate vector
                var c = 2 * random.Next(0, 50) + 50;
                Vector<double> v = CreatePattern(random, c, numDimensions);

                var infered = knn.Infer(v.ToArray());
                if (infered.GetWinner() != c)
                {
                    Console.WriteLine($"Mistake with {Arrays.ToString(v.Storage.EnumerateNonZero())} mapped to category {infered.GetWinner()} instead of category {c}");
                    errors += 1;
                }
            }

            if (errors != 0)
            {
                failures += "Failure in handling non-consecutive category indices";
            }
            else
            {
                Console.WriteLine("Passed Test non-consecutive category indices");
            }

            errors = 0;
            // Test closest methods
            for (int i = 0; i < 10; i++)
            {
                var c = 2 * random.Next(0, 50) + 50;
                Vector<double> v = CreatePattern(random, c, numDimensions);

                Vector<double> p = knn.ClosestTrainingPattern(v, c);
                if (!p.Storage.EnumerateNonZeroIndexed().Any(t => t.Item1 == c))
                {
                    Console.WriteLine($"Mistake {Arrays.ToString(p.Storage.EnumerateNonZero())} {Arrays.ToString(v.Storage.EnumerateNonZero())}");
                    errors += 1;
                }
            }

            if (errors != 0)
            {
                failures += "Failure in closestTrainingPatternMethod";
            }
            else
            {
                Console.WriteLine("Passed Test closestTrainingPatternMethod");
            }

            return (failures, knn);
        }

        private Vector<double> CreatePattern(Random random, int c, int numDimensions)
        {
            var v = Vector<double>.Build.Sparse(numDimensions);
            v[c] = 5 * random.NextDouble() + 10;
            v[c + 1] = random.NextDouble();
            if (c > 0)
            {
                v[c - 1] = random.NextDouble();
            }

            return v;
        }

        /// <summary>
        /// Test the KNN classifier in this module. short can be:
        /// 0 (short), 1 (medium), or 2 (long)
        /// </summary>
        /// <param name=""></param>
        /// <param name=""></param>
        private void RunTestKNNClassifier(int @short = 0)
        {
            string failures = string.Empty;
            if (@short != 2)
            {
                _random = new Random(42);
            }
            else
            {
                var seedValue = new Random(RandomSeed.Time());
                Console.WriteLine($"Seed used: {seedValue}");
            }

            failures += SimulateKMoreThanOne();

            Console.WriteLine("\nTesting KNN Classifier on dense patterns");
            (int numPatterns, int numClasses) = GetNumTestPatterns(@short);
            int patternSize = 100;
            List<Vector<double>> patterns = new List<Vector<double>>();
            for (int i = 0; i < numPatterns; i++)
            {
                var p = Vector<double>.Build.Random(patternSize, new Normal(_random));
                patterns.Add(p);
            }

            var patternDict = new Map<object, Map<string, object>>();
            var testDict = new Map<int, Map<string, object>>();

            // Assume there are no repeated patterns -- if there are, then
            // numpy.random would be completely broken.
            // Patterns in testDict are identical to those in patternDict but for the
            // first 2% of items.
            foreach (int i in ArrayUtils.XRange(0, numPatterns, 1))
            {
                patternDict[i] = new Map<string, object>();
                patternDict[i]["pattern"] = patterns[i];
                patternDict[i]["category"] = _random.Next(0, numClasses - 1);

                testDict[i] = new Map<string, object>(patternDict[i]);
                testDict[i]["pattern"] = patterns[i].Clone();
                var testPat = (Vector<double>)testDict[i]["pattern"];
                for (int r = 0; r < 0.06 * patternSize; r++)
                {
                    testPat[r] = _random.NextDouble();
                }
                testDict[i]["category"] = null;
            }

            // Lets check that the first test dict sequence does not occur anywhere else
            var firstTestSet = (Vector<double>)testDict[0]["pattern"];
            bool ok = patternDict.All(d => !((Vector<double>)d.Value["pattern"]).Equals(firstTestSet));
            Assert.IsTrue(ok, "first test dict entry is repeated somewhere else! Randomness issue");

            Console.WriteLine("\nTesting KNN Classifier with L2 norm");

            var knn = KNNClassifier.GetBuilder().K(1).DistanceNorm(2).Build();

            failures += SimulateClassifier(knn, patternDict, "KNN Classifier with L2 norm test");

            Console.WriteLine("\nTesting KNN Classifier with L1 norm");

            var knnL1 = KNNClassifier.GetBuilder().K(1).DistanceNorm(1).Build();
            failures += SimulateClassifier(knnL1, patternDict, "KNN Classifier with L1 norm test");

            // Test with exact matching classifications.
            Console.WriteLine("\nTesting KNN Classifier with exact matching. For testing we " +
                              "slightly alter the training data and expect None to be returned for the " +
                              "classifications.");

            var knnExact = KNNClassifier.GetBuilder().K(3).Exact(true).Build();
            failures += SimulateClassifier(knnExact,
                patternDict,
                "KNN Classifier with exact matching test",
                testDict);

            (numPatterns, numClasses) = GetNumTestPatterns(@short);
            patterns = new List<Vector<double>>();
            for (int i = 0; i < numPatterns; i++)
            {
                var p = Vector<double>.Build.SparseOfArray(
                    _random.NextDoubleSequence().Where(d => d > 0.7).Take(25).ToArray());
                patterns.Add(p);
            }
            //patterns = (numpy.random.rand(numPatterns, 25) > 0.7).astype(RealNumpyDType);
            patternDict = new Map<object, Map<string, object>>();

            foreach (Vector<double> i in patterns)
            {
                string iString = ArrayUtils.DoubleArrayToString(i, "{0:0.0000}, ");
                if (!patternDict.ContainsKey(iString))
                {
                    int randCategory = _random.Next(0, numClasses - 1);
                    patternDict[iString] = new Map<string, object>();
                    patternDict[iString]["pattern"] = i;
                    patternDict[iString]["category"] = randCategory;
                }
            }

            Console.WriteLine("\nTesting KNN Classifier on sparse patterns");
            var knnDense = KNNClassifier.GetBuilder().K(1).Build();
            failures += SimulateClassifier(knnDense,
                patternDict,
                "KNN Classifier on sparse pattern test");

            Assert.AreEqual(0, failures.Length, $"Tests failed: \r\n{failures}");
        }

        private string SimulateClassifier(
            KNNClassifier knn,
            Map<object, Map<string, object>> patternDict,
            string label,
            Map<int, Map<string, object>> testDict = null)
        {
            string failures = "";
            int numPatterns = patternDict.Count;

            Console.WriteLine("Training the classifier");
            var stopwatch = Stopwatch.StartNew();
            foreach (var idx in patternDict.Keys)
            {
                knn.Learn((Vector<double>)patternDict[idx]["pattern"], (int)patternDict[idx]["category"]);
            }

            Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds:0.000000} ms");
            stopwatch.Restart();

            int errorCount = 0;
            if (testDict != null)
            {
                Console.WriteLine("Testing the classifier on the test set");
                foreach (var i in testDict.Keys)
                {
                    var result = knn.Infer((Vector<double>)testDict[i]["pattern"]);
                    if (result.GetWinner() != (int?)testDict[i]["category"])
                    {
                        errorCount += 1;
                        Console.WriteLine($"{i} - Missed category winner {result.GetWinner()} vs {(int?)testDict[i]["category"]}");
                    }
                }
            }
            else
            {
                Console.WriteLine("Testing the classifier on the training set");
                Console.WriteLine($"Number of patterns: {numPatterns}");
                foreach (var key in patternDict.Keys)
                {
                    //Console.WriteLine($"Testing idx: {key} - cat: {patternDict[key]["category"]} " +
                    //                  $"patLen: {((Vector<double>)patternDict[key]["pattern"]).Count}");
                    var result = knn.Infer((Vector<double>)patternDict[key]["pattern"]);
                    if (result.GetWinner() != (int?)patternDict[key]["category"])
                    {
                        errorCount += 1;
                    }
                }
            }

            Console.WriteLine($"Time elapsed: {stopwatch.ElapsedMilliseconds:0.0000000} ms");

            var errorRate = (double)errorCount / numPatterns;
            Console.WriteLine($"Error rate: {errorRate:0.000}");
            if (errorRate == 0)
            {
                Console.WriteLine($"Passed");
            }
            else
            {
                Console.WriteLine($"Failed");
                failures += $" failed ({label})\r\n";
            }

            return failures;
        }

        private (int numPatterns, int numClasses) GetNumTestPatterns(int s)
        {
            (int numPatterns, int numClasses) x = (0, 0);
            if (s == 0)
            {
                Console.WriteLine("Running short tests");
                x = (_random.Next(300, 600), _random.Next(50, 150));
            }
            else if (s == 1)
            {
                Console.WriteLine("Running medium tests");
                x = (_random.Next(500, 1500), _random.Next(50, 150));
            }
            else
            {
                Console.WriteLine("Running long tests");
                x = (_random.Next(500, 3000), _random.Next(30, 1000));
            }

            Console.WriteLine($"number of patterns is {x.numPatterns}");
            Console.WriteLine($"number of classes is {x.numClasses}");

            return x;
        }

        private string SimulateKMoreThanOne()
        {
            string failures = string.Empty;
            Console.WriteLine("Testing the sparse KNN Classifier with k=3");
            var knn = KNNClassifier.GetBuilder()
                .K(3)
                .Build();

            double[][] v = new double[6][];
            v[0] = new[] { 1.0, 0.0 };
            v[1] = new[] { 1.0, 0.2 };
            v[2] = new[] { 1.0, 0.2 };
            v[3] = new[] { 1.0, 2.0 };
            v[4] = new[] { 1.0, 4.0 };
            v[5] = new[] { 1.0, 4.5 };
            knn.Learn(v[0], 0);
            knn.Learn(v[1], 0);
            knn.Learn(v[2], 0);
            knn.Learn(v[3], 0);
            knn.Learn(v[4], 0);
            knn.Learn(v[5], 0);

            var result = knn.Infer(v[0]);
            if (result.GetWinner() != 0)
            {
                failures += "Inference failed with k=3\r\n";
            }

            result = knn.Infer(v[2]);
            if (result.GetWinner() != 0)
            {
                failures += "Inference failed with k=3\r\n";
            }

            result = knn.Infer(v[3]);
            if (result.GetWinner() != 0)
            {
                failures += "Inference failed with k=3\r\n";
            }

            result = knn.Infer(v[5]);
            if (result.GetWinner() != 0)
            {
                failures += "Inference failed with k=3\r\n";
            }

            if (failures.Length == 0)
            {
                Console.WriteLine("Tests passed.");
            }

            return failures;
        }
    }
}