using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    /// <summary>
    /// This script trains and tests the spatial pooler (SP) over and over while varying
    /// some SP parameters.For each set of SP parameter values it trains the spatial
    ///  pooler(SP) on a set of training images and tests its image recognition
    /// abilities on a set of test images.
    /// During training the SP is trained until it achieves either a minimum specified
    /// image recognition accuracy on the training data set or until a maximum number of
    /// training cycles is reached.
    /// After training and testing is completed for all combinations of the parameter
    /// values, a summary of the results is displayed.
    /// trainingDataset - name of XML file that lists the training images
    /// testingDataset - name of XML file that lists the testing images
    /// minAccuracy - minimum accuracy requred to stop training before
    ///               maxTrainingCycles is reached
    /// maxTrainingCycles - maximum number of training cycles to perform
    /// </summary>
    public class ParameterSearch
    {
        private const string TrainingDataSet = "DataSets/OCR/characters/cmr_hex.xml";
        private const string TestingDataSet = "DataSets/OCR/characters/cmr_hex.xml";
        private double minAccuracy = 100.0;
        private int maxTrainingCycles = 5;

        public void Execute(string dataSet = TrainingDataSet, double minAccuracy = 100.0, int maxTrainingCycles = 5)
        {
            var tupleTraining = DatasetReader.GetImagesAndTags(dataSet);
            // Get training images and convert them to vectors.
            var trainingImages = (List<Bitmap>)tupleTraining.Get(0);
            var trainingTags = tupleTraining.Get(1) as List<string>;
            var trainingVectors = trainingImages.Select((i, index) => new { index, vector = i.ToVector() })
                .ToDictionary(k => k.index, v => v.vector);

            // Specify parameter values to search
            CombinationParameters parameters = new CombinationParameters();
            parameters.Define("synPermConn", new List<object> { 0.5 });
            parameters.Define("synPermDecFrac", new List<object> { 1.0,0.5,0.1 });
            parameters.Define("synPermIncFrac", new List<object> { 1.0,0.5,0.1 });

            // Run the model until all combinations have been tried
            while (parameters.GetNumResults() < parameters.GetNumCombinations())
            {
                // Pick a combination of parameter values
                parameters.NextCombination();

                double synPermConnected = (double) parameters.GetValue("synPermConn");
                var synPermDec = synPermConnected * (double)parameters.GetValue("synPermDecFrac");
                var synPermInc = synPermConnected * (double)parameters.GetValue("synPermIncFrac");

                // Instantiate our spatial pooler
                Parameters p = Parameters.GetAllDefaultParameters();
                p.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 32, 32 }); // Size of image patch
                p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 32, 32 });
                p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 10000); // Ensures 100% potential pool
                p.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.8);
                p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
                p.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0); // Using numActiveColumnsPerInhArea
                p.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 64.0);
                // All input activity can contribute to feature output
                p.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
                p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, synPermDec);
                p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, synPermInc);
                p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, synPermConnected);
                p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 1000);
                p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);
                p.SetParameterByKey(Parameters.KEY.SEED, 1956); // The seed that Grok uses
                p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(1956)); // The seed that Grok uses
                p.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 1);

                Connections cn = new Connections();
                p.Apply(cn);

                SpatialPooler sp = new SpatialPooler();
                sp.Init(cn);

                // Instantiate the spatial pooler test bench.
                VisionTestBench tb = new VisionTestBench(cn, sp);

                // Instantiate the classifier
                KNNClassifier clf = KNNClassifier.GetBuilder().Apply(p);

                int numCycles = tb.Train(trainingVectors, trainingTags, clf, maxTrainingCycles, minAccuracy);

                // Get testing images and convert them to vectors.
                var tupleTesting = DatasetReader.GetImagesAndTags(dataSet);
                var testingImages = (List<System.Drawing.Bitmap>)tupleTesting.Get(0);
                var testingTags = tupleTesting.Get(1) as List<string>;
                var testingVectors = testingImages.Select((i, index) => new { index, vector = i.ToVector() })
                    .ToDictionary(k => k.index, v => v.vector);

                // Reverse the order of the vectors and tags for testing
                testingTags.Reverse();
                testingVectors.Reverse();

                // Test the spatial pooler on testingVectors.
                var accurancy = tb.Test(testingVectors, testingTags, clf, learn: true);

                // Add results to the list
                parameters.AppendResults(new List<object> {accurancy, numCycles});
            }
            parameters.PrintResults(new[] { "Percent Accuracy", "Training Cycles" }, new[] {"\t{0}","\t{0}"});
            Console.WriteLine("The maximum number of training cycles is set to: {0}", maxTrainingCycles);
        }
    }
}