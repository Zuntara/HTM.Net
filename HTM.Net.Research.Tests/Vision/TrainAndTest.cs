using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using HTM.Net.Network;
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
            p.SetParameterByKey(Parameters.KEY.MIN_PCT_OVERLAP_DUTY_CYCLES, 0.001);
            p.SetParameterByKey(Parameters.KEY.MIN_PCT_ACTIVE_DUTY_CYCLES, 0.001);
            p.SetParameterByKey(Parameters.KEY.DUTY_CYCLE_PERIOD, 1000);
            p.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);
            p.SetParameterByKey(Parameters.KEY.SEED, 1956); // The seed that Grok uses
            p.SetParameterByKey(Parameters.KEY.RANDOM, new XorshiftRandom(1956)); // The seed that Grok uses
            p.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 1);
            p.SetParameterByKey(Parameters.KEY.SP_PARALLELMODE, true);

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

        //[TestMethod]
        //public void TestPrintMethodOfCombinationParameters()
        //{
        //    CombinationParameters parameters = new CombinationParameters();
        //    parameters.Define("synPermConn", new List<object> { 0.5 });
        //    parameters.Define("synPermDecFrac", new List<object> { 1.0, 0.5, 0.1 });
        //    parameters.Define("synPermIncFrac", new List<object> { 1.0, 0.5, 0.1 });

        //    // Pick a combination of parameter values
        //    parameters.NextCombination();

        //    // Add results to the list
        //    parameters.AppendResults(new List<object> { 67.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 68.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 57.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 47.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 37.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 27.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 17.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();
        //    parameters.AppendResults(new List<object> { 16.8, 3 }); // accurancy , cycles
        //    parameters.NextCombination();

        //    parameters.PrintResults(new[] { "Percent Accuracy", "Training Cycles" }, new[] { "\t{0}", "\t{0}" });
        //}

        [TestMethod]
        public void TestParameterCombinations()
        {
            CombinationParameters parameters = new CombinationParameters();

            parameters.Define("synPermConn", new List<object> { 0.5 });
            parameters.Define("synPermDecFrac", new List<object> { 1.0, 0.5, 0.1 });
            parameters.Define("synPermIncFrac", 1.0, 0.5, 0.1);

            // Pick a combination of parameter values
            IDictionary<string, object> combis;
            int combiNr = parameters.NextCombination(out combis);

            Assert.IsNotNull(combis);
            Assert.AreEqual(3, combis.Count);
            Assert.AreEqual(9, parameters.GetNumCombinations());
            Assert.AreEqual(0, combiNr);

            // Add results to the list
            parameters.AppendResults(combiNr, new List<object> { 67.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 68.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 57.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 47.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 37.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 27.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 17.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 16.8, 3 }); // accurancy , cycles
            combiNr = parameters.NextCombination(out combis);
            parameters.AppendResults(combiNr, new List<object> { 14.8, 3 }); // accurancy , cycles

            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);
            parameters.NextCombination(out combis);

            parameters.PrintResults(new[] { "Percent Accuracy", "Training Cycles" }, new[] { "\t{0}", "\t{0}" });
        }

        //// https://github.com/numenta/nupic.vision/blob/master/nupic/vision/tests/integration/nupicvision/regions/image_knn_test.py
        ///// <summary>
        ///// This test is a simple end to end test. It creates a simple network with an
        ///// ImageSensor and a KNNClassifier region.It creates a 'dataset' with two random
        ///// images, trains the network and then runs inference to ensures we can correctly
        ///// classify them.This tests that the plumbing is working well.
        ///// </summary>
        //[TestMethod]
        //public void TestSimpleImageNetwork()
        //{
        //    Parameters pars = Parameters.GetAllDefaultParameters();

        //    pars.SetParameterByKey(Parameters.KEY.DISTANCE_THRESHOLD, 0.01);

        //    EncoderSetting catInnerSettings = new EncoderSetting();
        //    catInnerSettings.fieldName = "category";
        //    catInnerSettings.name = "category";
        //    catInnerSettings.n = 8;
        //    catInnerSettings.w = 3;
        //    catInnerSettings.forced = true;
        //    catInnerSettings.resolution = 1;
        //    catInnerSettings.radius = 0;
        //    catInnerSettings.minVal = 0;
        //    catInnerSettings.maxVal = 10;
        //    catInnerSettings.fieldType = FieldMetaType.Integer;
        //    catInnerSettings.encoderType = "ScalarEncoder";

        //    EncoderSetting imgInnerSettings = new EncoderSetting();
        //    imgInnerSettings.fieldName = "imageIn";
        //    imgInnerSettings.n = 1024; // width
        //    imgInnerSettings.name = "imageIn";
        //    imgInnerSettings.fieldType = FieldMetaType.DenseArray;
        //    imgInnerSettings.encoderType = "SDRPassThroughEncoder";

        //    EncoderSettingsList settings = new EncoderSettingsList();
        //    settings.Add("imageIn", imgInnerSettings);
        //    settings.Add("category", catInnerSettings);

        //    pars.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);
        //    //pars.SetParameterByKey(Parameters.KEY.MAX_CATEGORYCOUNT, 2);

        //    var network = Network.Network.Create("ImageNetwork", pars);
        //    network
        //        .Add(Network.Network.CreateRegion("Region 1")
        //        .Add(new KnnLayer("KNN Layer", network, pars))
        //        .Add(Network.Network.CreateLayer("Layer 1", pars)
        //        .Add(Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
        //                    SensorParams.Keys.Image, new ImageSensorConfig
        //                    {
        //                        Width = 32,
        //                        Height = 32,
        //                        ExplorerConfig = new ExplorerConfig
        //                        {
        //                            ExplorerName = "ImageSweep"
        //                        }
        //                    }))))
        //            .Connect("KNN Layer", "Layer 1"));

        //    network.Close();

        //    // Test the sensor data retrieval
        //    HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)network.GetSensor();
        //    ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

        //    KnnLayer classifierLayer = ((KnnLayer)network.Lookup("Region 1").Lookup("KNN Layer"));

        //    // Make sure learning is on
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("learningMode"));

        //    var outStream = htmSensor.GetOutputStream();

        //    Bitmap b1s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b1s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
        //    }
        //    KalikoImage b1 = new KalikoImage(b1s);

        //    Bitmap b2s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b2s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 15, 15, 25, 25);
        //    }
        //    KalikoImage b2 = new KalikoImage(b2s);

        //    Assert.IsTrue(!b1.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.SequenceEqual(b1.ByteArray));

        //    sensor.LoadSpecificImages(new[] { b1, b2 }, new[] { "1", "2" });

        //    ImageDefinition inputObject1 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject1.InputVector));

        //    Layer<IInference> tailLayer = (Layer<IInference>)network.GetTail().GetTail();

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject1);

        //    ImageDefinition inputObject2 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject2.InputVector));

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject2);

        //    // turn off learning and turn on inference mode
        //    classifierLayer.SetParameter("inferenceMode", true);
        //    classifierLayer.SetParameter("learningMode", false);

        //    // Check parameters
        //    Assert.IsFalse((bool)classifierLayer.GetParameter("learningMode"));
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("inferenceMode"));
        //    Assert.AreEqual(2, classifierLayer.GetParameter("categoryCount"), "Incorrect category count");
        //    Assert.AreEqual(2, classifierLayer.GetParameter("patternCount"), "Incorrect pattern count");

        //    // Now test the network to make sure it categories the images correctly
        //    int numCorrect = 0;
        //    tailLayer.Compute(inputObject1);

        //    IInference inference = tailLayer.GetInference();

        //    int inferredCategory = inference.GetInferredCategory();
        //    if (inputObject1.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }
        //    tailLayer.Compute(inputObject2);
        //    inferredCategory = inference.GetInferredCategory();
        //    if (inputObject2.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }

        //    Assert.AreEqual(2, numCorrect, "Classification error");
        //}

        //[TestMethod]
        //public void TestSimpleImageNetwork_WithFilter()
        //{
        //    Parameters pars = Parameters.GetAllDefaultParameters();

        //    pars.SetParameterByKey(Parameters.KEY.DISTANCE_THRESHOLD, 0.01);

        //    EncoderSetting catInnerSettings = new EncoderSetting();
        //    catInnerSettings.fieldName = "category";
        //    catInnerSettings.name = "category";
        //    catInnerSettings.n = 8;
        //    catInnerSettings.w = 3;
        //    catInnerSettings.forced = true;
        //    catInnerSettings.resolution = 1;
        //    catInnerSettings.radius = 0;
        //    catInnerSettings.minVal = 0;
        //    catInnerSettings.maxVal = 10;
        //    catInnerSettings.fieldType = FieldMetaType.Integer;
        //    catInnerSettings.encoderType = "ScalarEncoder";

        //    EncoderSetting imgInnerSettings = new EncoderSetting();
        //    imgInnerSettings.fieldName = "imageIn";
        //    imgInnerSettings.n = 1024; // width
        //    imgInnerSettings.name = "imageIn";
        //    imgInnerSettings.fieldType = FieldMetaType.DenseArray;
        //    imgInnerSettings.encoderType = "SDRPassThroughEncoder";

        //    EncoderSettingsList settings = new EncoderSettingsList();
        //    settings.Add("imageIn", imgInnerSettings);
        //    settings.Add("category", catInnerSettings);

        //    pars.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);
        //    //pars.SetParameterByKey(Parameters.KEY.MAX_CATEGORYCOUNT, 2);

        //    var network = Network.Network.Create("ImageNetwork", pars);
        //    network
        //        .Add(Network.Network.CreateRegion("Region 1")
        //            .Add(new KnnLayer("KNN Layer", network, pars))
        //            .Add(Network.Network.CreateLayer("Layer 1", pars)
        //                .Add(Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
        //                    SensorParams.Keys.Image, new ImageSensorConfig
        //                    {
        //                        Width = 32,
        //                        Height = 32,
        //                        ExplorerConfig = new ExplorerConfig
        //                        {
        //                            ExplorerName = "ImageSweep"
        //                        },
        //                        FilterConfigs = new[]
        //                        {
        //                            new FilterConfig
        //                            {
        //                                FilterName = "AddNoise",
        //                                FilterArgs = new Map<string, object> {{"noiseLevel", 0.2}}
        //                            }
        //                        }
        //                    }))))
        //            .Connect("KNN Layer", "Layer 1"));

        //    network.Close();

        //    // Test the sensor data retrieval
        //    HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)network.GetSensor();
        //    ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

        //    KnnLayer classifierLayer = ((KnnLayer)network.Lookup("Region 1").Lookup("KNN Layer"));

        //    // Make sure learning is on
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("learningMode"));

        //    var outStream = htmSensor.GetOutputStream();

        //    Bitmap b1s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b1s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
        //    }
        //    KalikoImage b1 = new KalikoImage(b1s);

        //    Bitmap b2s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b2s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 15, 15, 25, 25);
        //    }
        //    KalikoImage b2 = new KalikoImage(b2s);

        //    Assert.IsTrue(!b1.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.SequenceEqual(b1.ByteArray));

        //    sensor.LoadSpecificImages(new[] { b1, b2 }, new[] { "1", "2" });

        //    ImageDefinition inputObject1 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject1.InputVector));

        //    Layer<IInference> tailLayer = (Layer<IInference>)network.GetTail().GetTail();

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject1);

        //    ImageDefinition inputObject2 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject2.InputVector));

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject2);

        //    // turn off learning and turn on inference mode
        //    classifierLayer.SetParameter("inferenceMode", true);
        //    classifierLayer.SetParameter("learningMode", false);

        //    // Check parameters
        //    Assert.IsFalse((bool)classifierLayer.GetParameter("learningMode"));
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("inferenceMode"));
        //    Assert.AreEqual(2, classifierLayer.GetParameter("categoryCount"), "Incorrect category count");
        //    Assert.AreEqual(2, classifierLayer.GetParameter("patternCount"), "Incorrect pattern count");

        //    // Now test the network to make sure it categories the images correctly
        //    int numCorrect = 0;
        //    tailLayer.Compute(inputObject1);

        //    IInference inference = tailLayer.GetInference();

        //    int inferredCategory = inference.GetInferredCategory();
        //    if (inputObject1.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }
        //    tailLayer.Compute(inputObject2);
        //    inferredCategory = inference.GetInferredCategory();
        //    if (inputObject2.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }

        //    Assert.AreEqual(2, numCorrect, "Classification error");
        //}

        //[TestMethod]
        //public void TestSimpleImageNetwork_WithEyeMovementExplorer()
        //{
        //    Parameters pars = Parameters.GetAllDefaultParameters();

        //    pars.SetParameterByKey(Parameters.KEY.DISTANCE_THRESHOLD, 0.01);

        //    EncoderSetting catInnerSettings = new EncoderSetting();
        //    catInnerSettings.fieldName = "category";
        //    catInnerSettings.name = "category";
        //    catInnerSettings.n = 8;
        //    catInnerSettings.w = 3;
        //    catInnerSettings.forced = true;
        //    catInnerSettings.resolution = 1;
        //    catInnerSettings.radius = 0;
        //    catInnerSettings.minVal = 0;
        //    catInnerSettings.maxVal = 10;
        //    catInnerSettings.fieldType = FieldMetaType.Integer;
        //    catInnerSettings.encoderType = "ScalarEncoder";

        //    EncoderSetting imgInnerSettings = new EncoderSetting();
        //    imgInnerSettings.fieldName = "imageIn";
        //    imgInnerSettings.n = 1024; // width
        //    imgInnerSettings.name = "imageIn";
        //    imgInnerSettings.fieldType = FieldMetaType.DenseArray;
        //    imgInnerSettings.encoderType = "SDRPassThroughEncoder";

        //    EncoderSettingsList settings = new EncoderSettingsList();
        //    settings.Add("imageIn", imgInnerSettings);
        //    settings.Add("category", catInnerSettings);

        //    pars.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, settings);
        //    //pars.SetParameterByKey(Parameters.KEY.MAX_CATEGORYCOUNT, 2);

        //    var network = Network.Network.Create("ImageNetwork", pars);
        //    network
        //        .Add(Network.Network.CreateRegion("Region 1")
        //            .Add(new KnnLayer("KNN Layer", network, pars))
        //            .Add(Network.Network.CreateLayer("Layer 1", pars)
        //                .Add(Sensor<ImageDefinition>.Create(ImageSensor.Create, SensorParams.Create(
        //                    SensorParams.Keys.Image, new ImageSensorConfig
        //                    {
        //                        Width = 32,
        //                        Height = 32,
        //                        ExplorerConfig = new ExplorerConfig
        //                        {
        //                            ExplorerName = "EyeMovements"
        //                        },
        //                        //FilterConfigs = new[]
        //                        //{
        //                        //    new FilterConfig
        //                        //    {
        //                        //        FilterName = "AddNoise",
        //                        //        FilterArgs = new Map<string, object> {{"noiseLevel", 0.2}}
        //                        //    }
        //                        //}
        //                    }))))
        //            .Connect("KNN Layer", "Layer 1"));

        //    network.Close();

        //    // Test the sensor data retrieval
        //    HTMSensor<ImageDefinition> htmSensor = (HTMSensor<ImageDefinition>)network.GetSensor();
        //    ImageSensor sensor = (ImageSensor)htmSensor.GetDelegateSensor();

        //    KnnLayer classifierLayer = ((KnnLayer)network.Lookup("Region 1").Lookup("KNN Layer"));

        //    // Make sure learning is on
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("learningMode"));

        //    var outStream = htmSensor.GetOutputStream();

        //    Bitmap b1s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b1s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 10, 10, 20, 20);
        //    }
        //    KalikoImage b1 = new KalikoImage(b1s);

        //    Bitmap b2s = new Bitmap(32, 32);
        //    using (Graphics g = Graphics.FromImage(b2s))
        //    {
        //        g.FillRectangle(Brushes.White, 0, 0, 32, 32);
        //        g.DrawRectangle(new Pen(Color.Black, 1), 15, 15, 25, 25);
        //    }
        //    KalikoImage b2 = new KalikoImage(b2s);

        //    Assert.IsTrue(!b1.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.All(i => i == 255));
        //    Assert.IsTrue(!b2.ByteArray.SequenceEqual(b1.ByteArray));

        //    sensor.LoadSpecificImages(new[] { b1, b2 }, new[] { "1", "2" });

        //    ImageDefinition inputObject1 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject1.InputVector));

        //    Layer<IInference> tailLayer = (Layer<IInference>)network.GetTail().GetTail();

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject1);
        //    for (int i = 0; i < 8; i++)
        //    {
        //        // Still 8 iterations for eyemovements to go
        //        tailLayer.Compute(outStream.ReadUntyped());
        //    }

        //    ImageDefinition inputObject2 = (ImageDefinition)outStream.ReadUntyped();
        //    //Debug.WriteLine(Arrays.ToString(inputObject2.InputVector));

        //    // Same logic as in start but executed manually
        //    tailLayer.Compute(inputObject2);
        //    for (int i = 0; i < 8; i++)
        //    {
        //        // Still 8 iterations for eyemovements to go
        //        tailLayer.Compute(outStream.ReadUntyped());
        //    }
        //    // turn off learning and turn on inference mode
        //    classifierLayer.SetParameter("inferenceMode", true);
        //    classifierLayer.SetParameter("learningMode", false);

        //    // Check parameters
        //    Assert.IsFalse((bool)classifierLayer.GetParameter("learningMode"));
        //    Assert.IsTrue((bool)classifierLayer.GetParameter("inferenceMode"));
        //    Assert.AreEqual(8, classifierLayer.GetParameter("categoryCount"), "Incorrect category count");
        //    Assert.AreEqual(8, classifierLayer.GetParameter("patternCount"), "Incorrect pattern count");

        //    // Now test the network to make sure it categories the images correctly
        //    int numCorrect = 0;
        //    tailLayer.Compute(inputObject1);


        //    IInference inference = tailLayer.GetInference();

        //    int inferredCategory = inference.GetInferredCategory();
        //    if (inputObject1.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }
        //    tailLayer.Compute(inputObject2);
        //    inferredCategory = inference.GetInferredCategory();
        //    if (inputObject2.CategoryIndices[0] == inferredCategory)
        //    {
        //        numCorrect += 1;
        //    }

        //    Assert.AreEqual(2, numCorrect, "Classification error");
        //}
    }
}