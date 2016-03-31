using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Research.Vision;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Vision
{
    [TestClass]
    public class TrainAndTest
    {
        //[TestMethod, DeploymentItem("Resources/DataSets/OCR/characters/cmr_all.xml")]
        public void TestVisionBench()
        {
            // Set some training paths
            string trainingDataset = "cmr_all.xml";
            string testingDataset = "Resources/DataSets/OCR/characters/cmr_all.xml";
            double minAccuracy = 100.0; // force max training cycles
            int maxTrainingCycles = 5;

            // Create spatial parameters
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new[] { 32, 32 }); // Size of image patch
            p.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new[] { 32, 32 });
            p.SetParameterByKey(Parameters.KEY.POTENTIAL_RADIUS, 10000); // Ensures 100% potential pool
            p.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.8);
            p.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
            p.SetParameterByKey(Parameters.KEY.LOCAL_AREA_DENSITY, -1.0); // Using numActiveColumnsPerInhArea
            p.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 64.0);
            // All input activity can contribute to feature output
            p.SetParameterByKey(Parameters.KEY.STIMULUS_THRESHOLD, 0.0);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.001);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.001);
            p.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.3);
            p.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLE, 0.001);
            p.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLE, 0.001);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 1000);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);
            p.SetParameterByKey(Parameters.KEY.SEED, 1956); // The seed that Grok uses
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(1956)); // The seed that Grok uses
            p.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 1);

            Connections cn = new Connections();
            p.Apply(cn);

            // Instantiate our spatial pooler
            SpatialPooler sp = new SpatialPooler();
            sp.Init(cn);

            // Instantiate the spatial pooler test bench.
            VisionTestBench tb = new VisionTestBench(cn, sp);

            // Instantiate the classifier
            KNNClassifier clf = KNNClassifier.GetBuilder().Apply(p);

            // Get testing images and convert them to vectors.
            var tupleTraining = DatasetReader.GetImagesAndTags(trainingDataset);
            var trainingImages = (List<Bitmap>)tupleTraining.Get(0);
            var trainingTags = tupleTraining.Get(1) as List<string>;
            var trainingVectors = trainingImages.Select((i, index) => new {index, vector = i.ToVector()})
                .ToDictionary(k => k.index, v => v.vector);

            // Train the spatial pooler on trainingVectors.
            int numcycles = tb.Train(trainingVectors, trainingTags, clf, maxTrainingCycles, minAccuracy);

            // Get testing images and convert them to vectors.
            var tupleTesting = DatasetReader.GetImagesAndTags(trainingDataset);
            var testingImages = (List<System.Drawing.Bitmap>)tupleTesting.Get(0);
            var testingTags = tupleTesting.Get(1) as List<string>;
            var testingVectors = testingImages.Select((i, index) => new { index, vector = i.ToVector() })
                .ToDictionary(k => k.index, v => v.vector);

            // Reverse the order of the vectors and tags for testing
            testingTags.Reverse();
            testingVectors.Reverse();

            // Test the spatial pooler on testingVectors.
            var accurancy = tb.Test(testingVectors, testingTags, clf, learn: true);

            Debug.WriteLine("Number of training cycles : " + numcycles);
            Debug.WriteLine("Accurancy : " + accurancy);

            tb.SavePermsAndConns("C:\\temp\\permsAndConns.jpg");
        }

        //[TestMethod, DeploymentItem("Resources/DataSets/OCR/characters/cmr_hex.xml")]
        public void ExecuteParameterSearch()
        {
            ParameterSearch search = new ParameterSearch();

            search.Execute(dataSet: "cmr_hex.xml");
        }

        [TestMethod]
        public void TestPrintMethodOfCombinationParameters()
        {
            CombinationParameters parameters = new CombinationParameters();
            parameters.Define("synPermConn", new List<object> { 0.5 });
            parameters.Define("synPermDecFrac", new List<object> { 1.0, 0.5, 0.1 });
            parameters.Define("synPermIncFrac", new List<object> { 1.0, 0.5, 0.1 });

            // Pick a combination of parameter values
            parameters.NextCombination();

            // Add results to the list
            parameters.AppendResults(new List<object> { 67.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 68.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 57.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 47.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 37.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 27.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 17.8, 3 }); // accurancy , cycles
            parameters.NextCombination();
            parameters.AppendResults(new List<object> { 16.8, 3 }); // accurancy , cycles
            parameters.NextCombination();

            parameters.PrintResults(new[] { "Percent Accuracy", "Training Cycles" }, new[] { "\t{0}", "\t{0}" });
        }
    }
}