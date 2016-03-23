using System;
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
        private PCAKNNData knnData = new PCAKNNData();

        private const int TRAIN = 0;
        private const int TEST = 1;

        [TestMethod]
        [DeploymentItem("Resources\\test_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\test_pcaknnshort_data.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_data.txt")]
        public void TestPCAKNNShort()
        {
            RunTestPCAKNN(0);
        }

        private void RunTestPCAKNN(int _short)
        {
            Console.WriteLine("\nTesting PCA/k-NN classifier");
            Console.WriteLine("Mode=" + _short);

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
                .NumSVDDims((int)KnnMode.ADAPTIVE)
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
                var winner = (int)result.Get(0);
                var inference = (double[])result.Get(1);
                var dist = (double[])result.Get(2);
                var categoryDist = result.Get(3);

                var pcaResult = pca_knn.Infer(testData[i]);
                var pcawinner = (int)pcaResult.Get(0);
                var pcainference = (double[])pcaResult.Get(1);
                var pcadist = (double[])pcaResult.Get(2);
                var pcacategoryDist = pcaResult.Get(3);

                if (winner != testClass[i])
                {
                    numAbsErrors += 1;
                }
                if (pcawinner != winner)
                {
                    numWinnerFailures += 1;
                }
                if(ArrayUtils.Abs(ArrayUtils.Sub(pcainference, inference)).Any(n => n > 1e-4))
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
        }

    }
}