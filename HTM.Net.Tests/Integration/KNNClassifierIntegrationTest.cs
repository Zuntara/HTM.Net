using System;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Datagen;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Integration
{
    // https://github.com/numenta/nupic/blob/1410fcc4e31dc0907130460bcf6085a4f3badaeb/tests/integration/nupic/algorithms/knn_classifier_test/classifier_test.py
    [TestClass]
    public class KNNClassifierIntegrationTest
    {
        private PCAKNNData knnData;

        private const int TRAIN = 0;
        private const int TEST = 1;

        [TestMethod]
        [DeploymentItem("Resources\\test_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\test_pcaknnshort_data.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_data.txt")]
        public void TestPCAKNNShort()
        {
            knnData = new PCAKNNData();
            RunTestPCAKNN(0);
        }

        private void RunTestPCAKNN(int _short)
        {
            Console.WriteLine("\nTesting PCA/k-NN classifier");
            Console.WriteLine("Mode = " + _short);

            int numClasses = 10;
            int k = 10;
            int numPatternsPerClass = 100;
            int numPatterns = (int)Math.Round(0.9 * numClasses * numPatternsPerClass);
            int numTests = numClasses * numPatternsPerClass - numPatterns;
            int numSVDSamples = (int)Math.Round(0.1 * numPatterns);
            int keep = 1;

            KNNClassifier pca_knn = KNNClassifier.GetBuilder()
                .K(k)
                .NumSVDSamples(numSVDSamples)
                .NumSVDDims(1)
                .Build();

            KNNClassifier knn = KNNClassifier.GetBuilder()
                .K(k)
                .Build();

            Console.WriteLine("Training PCA k-NN");

            double[][] trainData = knnData.GetPcaKNNShortData()[TRAIN].GetDataArray();
            int[] trainClass = knnData.GetPcaKNNShortData()[TRAIN].GetClassArray();
            for (int i = 0; i < numPatterns; i++)
            {
                knn.Learn(trainData[i], trainClass[i]);
                pca_knn.Learn(trainData[i], trainClass[i]);
            }

            Console.WriteLine("Testing PCA k-NN");
            int numWinnerFailures = 0;
            int numInferenceFailures = 0;
            int numDistFailures = 0;
            int numAbsErrors = 0;

            double[][] testData = knnData.GetPcaKNNShortData()[TEST].GetDataArray();
            int[] testClass = knnData.GetPcaKNNShortData()[TEST].GetClassArray();

            for (int i = 0; i < numTests; i++)
            {
                var result = knn.Infer(testData[i]);
                var winner = result.GetWinner();
                var inference = result.GetInference();
                var dist = result.GetProtoDistance();
                var categoryDist = result.GetCategoryDistances();

                var pcaResult = pca_knn.Infer(testData[i]);
                var pcawinner = pcaResult.GetWinner();
                var pcainference = pcaResult.GetInference();
                var pcadist = pcaResult.GetProtoDistance();
                var pcacategoryDist = pcaResult.GetCategoryDistances();

                if (winner != testClass[i])
                {
                    numAbsErrors += 1;
                }
                if (pcawinner != winner)
                {
                    numWinnerFailures += 1;
                }
                if (ArrayUtils.Abs(ArrayUtils.Sub(pcainference, inference)).Any(n => n > 1e-4))
                {
                    numInferenceFailures += 1;
                }
                if (ArrayUtils.Abs(ArrayUtils.Sub(pcadist, dist)).Any(n => n > 1e-4))
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

            //Assert.AreEqual(100, s1, "PCA/k-NN test failed");
        }

        /// <summary>
        /// A small test with k=3
        /// </summary>
        [TestMethod]
        public void TestKMoreThanOnce()
        {
            string failures = "";
            Console.WriteLine("Testing the sparse KNN Classifier with k=3");

            KNNClassifier knn = KNNClassifier.GetBuilder()
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
            knn.Learn(v[3], 1);
            knn.Learn(v[4], 1);
            knn.Learn(v[5], 1);

            var result = knn.Infer(v[0]);
            var winner = result.GetWinner();
            if (winner != 0)
                failures += "Inference failed with k=3\r\n";

            result = knn.Infer(v[2]);
            winner = result.GetWinner();
            if (winner != 0)
                failures += "Inference failed with k=3\r\n";

            result = knn.Infer(v[3]);
            winner = result.GetWinner();
            if (winner != 0)
                failures += "Inference failed with k=3\r\n";

            result = knn.Infer(v[5]);
            winner = result.GetWinner();
            if (winner != 1)
                failures += "Inference failed with k=3\r\n";

            if (!string.IsNullOrWhiteSpace(failures))
            {
                Assert.Fail(failures);
            }
        }

        // https://github.com/numenta/nupic/blob/a5a7f52e39e30c5356c561547fc6ac3ffd99588c/tests/integration/nupic/algorithms/knn_classifier_test/classifier_test.py
        [TestMethod]
        public void TestKnnClassifierShort()
        {
            Console.WriteLine("Testing KNN Classifier on dense patterns");

            int runningMode = 0; // 0 = short, 1 = medium, 2 = long
            IRandom random = new XorshiftRandom(42);

            var testTuple = GetNumTestPatterns(random, runningMode);
            int numPatterns = testTuple.Item1;
            int numClasses = testTuple.Item2;
            int patternSize = 100;
            double[][] patterns = random.GetMatrix(numPatterns, patternSize);

            Map<int, Map<string, object>> patternDict = new Map<int, Map<string, object>>();
            Map<int, Map<string, object>> testDict = new Map<int, Map<string, object>>();

            // Assume there are no repeated patterns -- if there are, then
            // random would be completely broken.
            // Patterns in testDict are identical to those in patternDict but for the
            // first 2% of items.
            for (int i = 0; i < numPatterns; i++)
            {
                patternDict[i] = new Map<string, object>();
                patternDict[i]["pattern"] = patterns[i];
                patternDict[i]["category"] = random.NextInt(numClasses-1);

                testDict[i]= new Map<string, object>(patternDict[i]);
                for (int p = 0; p < patternSize*0.02; p++)
                {
                    ((double[]) testDict[i]["pattern"])[p] = random.NextDouble();
                }
                testDict[i]["category"] = null;
            }

            Console.WriteLine("Testing KNN Classifier with L2 Norm");

            var knn = KNNClassifier.GetBuilder().K(1).Build();
            string failures = SimulateClassifier(knn, patternDict, "KNN Classifier with L2 norm test");

            Console.WriteLine("Testing KNN Classifier with L1 Norm");
            var knnL1 = KNNClassifier.GetBuilder().K(1).DistanceNorm(1.0).Build();
            failures += SimulateClassifier(knnL1, patternDict, "KNN Classifier with L1 norm test");

            // Test with exact matching classifications.
            Console.WriteLine("\nTesting KNN Classifier with exact matching. For testing we slightly alter the training data and expect None to be returned for the classifications.");
            var knnExact = KNNClassifier.GetBuilder().K(1).Exact(true).Build();
            failures += SimulateClassifier(knnExact, patternDict, "KNN Classifier with exact matching test", testDict);

            Console.WriteLine("Testing KNN on sparse patterns");

            testTuple = GetNumTestPatterns(random, runningMode);
            numPatterns = testTuple.Item1;
            numClasses = testTuple.Item2;
            patterns = random.GetMatrix(numPatterns, patternSize, 0.7);

            var patternDictSparse = new Map<string, Map<string, object>>();
            patternDict = new Map<int, Map<string, object>>();
            foreach(var i in patterns)
            {
                string iString = Arrays.ToString(i);
                //if (!patternDictSparse.ContainsKey(iString))
                {
                    patternDictSparse[iString] = new Map<string, object>();
                    patternDictSparse[iString]["pattern"] = i;
                    patternDictSparse[iString]["category"] = random.NextInt(numClasses - 1);
                }
            }
            // overload to dict we can use
            for(int i = 0 ; i < patternDictSparse.Count; i++)
            {
                patternDict[i] = new Map<string, object>();
                var entry = patternDictSparse.ElementAt(i).Value;
                patternDict[i] = entry;
            }

            var knnDense = KNNClassifier.GetBuilder().K(1).DoBinarization(true).Build();
            failures += SimulateClassifier(knnDense, patternDict, "KNN Classifier on sparse pattern test");

            Assert.IsTrue(string.IsNullOrWhiteSpace(failures), "Tests failed: " + failures);
        }

        /// <summary>
        /// Train this classifier instance with the given patterns.
        /// </summary>
        /// <param name="knn"></param>
        /// <param name="patternDict"></param>
        /// <param name="testName"></param>
        /// <param name="testDict"></param>
        /// <returns></returns>
        private string SimulateClassifier(KNNClassifier knn, Map<int, Map<string, object>> patternDict, string testName, Map<int, Map<string, object>> testDict = null)
        {
            string failures = "";
            int numPatterns = patternDict.Count;

            Console.WriteLine("Training the classifier");
            Stopwatch watch = Stopwatch.StartNew();
            foreach(int i in patternDict.Keys)
            {
                knn.Learn((double[]) patternDict[i]["pattern"], (int) patternDict[i]["category"]);
            }
            Console.WriteLine("Time elapsed: {0} s", watch.Elapsed.TotalSeconds);

            watch = Stopwatch.StartNew();
            int errorCount = 0;
            if (testDict != null)
            {
                Console.WriteLine("Testing the classifier on the test set");
                foreach (int i in testDict.Keys)
                {
                    var infer = knn.Infer((double[])testDict[i]["pattern"]);
                    if (infer.GetWinner() != (int?)testDict[i]["category"])
                    {
                        errorCount += 1;
                    }
                }
            }
            else
            {
                Console.WriteLine("Testing with training set");
                Console.WriteLine("Numer of patterns: {0}", numPatterns);
                foreach (int i in patternDict.Keys)
                {
                    var infer = knn.Infer((double[])patternDict[i]["pattern"]);
                    if (infer.GetWinner() != (int?) patternDict[i]["category"])
                    {
                        errorCount += 1;
                    }
                }
            }
            Console.WriteLine("Time elapsed: {0} s", watch.Elapsed.TotalSeconds);

            double errorRate = (double)errorCount/numPatterns;
            Console.WriteLine("Error rate: {0}", errorRate);

            if (errorRate == 0.0)
            {
                Console.WriteLine("{0} passed\r\n", testName);
            }
            else
            {
                Console.WriteLine("{0} failed\r\n", testName);
                failures += testName + " failed\r\n";
            }
           return failures;
        }

        private Tuple<int,int> GetNumTestPatterns(IRandom random, int mode)
        {
            if (mode == 0)
            {
                int numPatterns = random.NextInt(300) + 300;
                int numClasses = random.NextInt(100) + 50;
                return new Tuple<int, int>(numPatterns, numClasses);
            }
            if (mode == 1)
            {
                int numPatterns = random.NextInt(1000) + 500;
                int numClasses = random.NextInt(970) + 30;
                return new Tuple<int, int>(numPatterns, numClasses);
            }
            if (mode == 2)
            {
                int numPatterns = random.NextInt(2500) + 500;
                int numClasses = random.NextInt(970) + 30;
                return new Tuple<int, int>(numPatterns, numClasses);
            }
            throw new ArgumentOutOfRangeException("mode");
        }
    }
}