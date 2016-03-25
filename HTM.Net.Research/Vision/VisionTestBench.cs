using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    // https://github.com/numenta/nupic.vision/blob/master/vision_testbench.py
    /// <summary>
    /// This class provides methods for characterizing nupic's image recognition
    /// capabilities.The goal is to put most of the details in here so the top
    /// level can be as clear and concise as possible.
    /// </summary>
    public class VisionTestBench
    {
        private SpatialPooler sp;
        private List<int[]> SDRs;
        private List<string> tags;
        private int inputHeight, inputWidth;
        private int columnHeight, columnWidth;
        private object permanencesImage, connectionsImage;
        private Connections _connections;

        /// <summary>
        /// The test bench has just a few things to keep track off:
        /// -   A list of the output SDRs that is shared between the training and testing routines
        /// -   Height and width of the spatial pooler's inputs and columns which are used for 
        ///     producing images of permanences and connected synapses
        /// -   Images of permanences and connected synapses so these images do not have to be generated more than necessary
        /// </summary>
        public VisionTestBench(Connections c, SpatialPooler spatialPooler)
        {
            _connections = c;
            sp = spatialPooler;
            SDRs = new List<int[]>();
            tags = new List<string>();

            // These images are produced together so these properties are used to allow
            // them to be saved separately without having to generate the images twice.
            permanencesImage = null;
            connectionsImage = null;

            // Limit inputs and columns to 1D and 2D layouts for now
            var inputDimensions = c.GetInputDimensions();
            try
            {
                Debug.Assert(inputDimensions.Length < 3);
                inputHeight = inputDimensions[0];
                inputWidth = inputDimensions[1];
            }
            catch (IndexOutOfRangeException)
            {
                inputHeight = (int)Math.Sqrt(inputDimensions[0]);
                inputWidth = (int)Math.Sqrt(inputDimensions[0]);
            }

            var columnDimensions = c.GetColumnDimensions();
            try
            {
                Debug.Assert(inputDimensions.Length < 3);
                columnHeight = columnDimensions[0];
                columnWidth = columnDimensions[1];
            }
            catch (IndexOutOfRangeException)
            {
                columnHeight = (int)Math.Sqrt(columnDimensions[0]);
                columnWidth = (int)Math.Sqrt(columnDimensions[0]);
            }
        }

        /// <summary>
        /// This routine trains the spatial pooler using the bit vectors produced from
        /// the training images by using these vectors as input to the SP.It continues
        /// training until either the minimum specified accuracy is met or the maximum
        /// number of training cycles is reached.It records each output SDR as the
        /// index of that SDR in a list of all SDRs seen during training.  This list of
        /// indexes is used to generate the SDRs for evaluating recognition accuracy
        /// after each training cycle.It also creates a list of all tags (ground
        /// truth) seen during training.This list is used to establish the integer
        /// categories for the classifier so they can be used again during testing to
        /// establish the correct categories even if the order of the input vectors is
        /// changed.
        /// </summary>
        public int Train(IDictionary<int, int[]> trainingVectors, List<string> trainingTags, KNNClassifier classifier,
            int maxCycles = 10, double minAccurancy = 100.0)
        {
            // Get rid of permanence and connection images from previous training
            permanencesImage = null;
            connectionsImage = null;

            // print starting stats
            int cyclesCompleted = 0;
            double accuracy = 0;
            PrintTrainingStats(cyclesCompleted, accuracy);

            // keep training until minAccuracy or maxCycles is reached
            while ((minAccurancy - accuracy) < 1.0 / trainingTags.Count && cyclesCompleted < maxCycles)
            {
                // increment cycle number
                cyclesCompleted += 1;

                // Feed each training vector into the spatial pooler and then teach the
                // classifier to associate the tag and the SDR
                var SDRIs = new List<int>();
                classifier.Clear();
                var activeArray = new int[_connections.GetNumColumns()];
                foreach (var trainingPair in trainingVectors)
                {
                    int j = trainingPair.Key;
                    var trainingVector = trainingPair.Value;

                    sp.Compute(_connections, trainingVector, activeArray, true, true);
                    // Build a list of indexes corresponding to each SDR
                    var activeList = activeArray.ToArray();
                    if (!SDRs.Any(ip => ip.SequenceEqual(activeList)))
                    {
                        SDRs.Add(activeList);
                    }
                    var SDRI = SDRs.FindIndex(ip => ip.SequenceEqual(activeList));
                    SDRIs.Add(SDRI);
                    // tell classifier to associate SDR and training Tag
                    // if there are repeat tags give the index of the first occurrence
                    int category;
                    if (tags.Contains(trainingTags[j]))
                    {
                        category = tags.IndexOf(trainingTags[j]);
                    }
                    else
                    {
                        tags.Add(trainingTags[j]);
                        category = tags.Count - 1;
                    }
                    classifier.Learn(activeArray.Select(i => (double)i).ToArray(), category, 0, 0);
                }
                // Check the accuracy of the SP, classifier combination
                accuracy = 0.0;
                foreach (int j in ArrayUtils.Range(0, SDRIs.Count))
                {
                    var SDRI = SDRIs[j];
                    activeArray = SDRs[SDRI].ToArray();
                    // if there are repeat tags give the index of the first occurrence
                    int category = tags.IndexOf(trainingTags[j]);
                    int inferred_category = (int) classifier.Infer(activeArray.Select(i=>(double)i).ToArray()).Get(0);
                    if (inferred_category == category)
                    {
                        accuracy += 100.0 / trainingTags.Count;
                    }
                }
                // print updated stats
                PrintTrainingStats(cyclesCompleted, accuracy);
            }
            return cyclesCompleted;
        }

        /// <summary>
        /// This routine tests the spatial pooler on the bit vectors produced from the
        /// testing images.
        /// </summary>
        public double Test(IDictionary<int, int[]> testVectors, List<string> testingTags, KNNClassifier classifier,
            bool verbose = false, bool learn = true)
        {
            Console.WriteLine("Testing:");
            //  Get rid of old permanence and connection images
            permanencesImage = null;
            connectionsImage = null;

            // Feed testing vectors into the spatial pooler and build a list of SDRs.
            var SDRIs = new List<int>();
            var activeArray = new int[_connections.GetNumColumns()];
            int category;
            foreach (var testPair in testVectors)
            {
                int j = testPair.Key;
                var testVector = testPair.Value;
                sp.Compute(_connections, testVector, activeArray, learn, learn);
                // Build a list of indexes corresponding to each SDR
                var activeList = activeArray.ToArray();
                if (!SDRs.Any(ip => ip.SequenceEqual(activeList)))
                {
                    SDRs.Add(activeList);
                }
                var SDRI = SDRs.FindIndex(ip => ip.SequenceEqual(activeList));
                SDRIs.Add(SDRI);
                if (learn)
                {
                    // tell classifier to associate SDR and testing Tag
                    category = tags.IndexOf(testingTags[j]);
                    classifier.Learn(activeArray.Select(i => (double)i).ToArray(), category, 0, 0);
                }
            }
            // Check the accuracy of the SP, classifier combination
            double accuracy = 0.0;
            bool recognitionMistake = false;

            foreach (int j in ArrayUtils.Range(0, SDRIs.Count))
            {
                activeArray = SDRs[SDRIs[j]].ToArray();
                category = tags.IndexOf(testingTags[j]);
                int inferred_category = (int) classifier.Infer(activeArray.Select(i => (double)i).ToArray()).Get(0);
                if (inferred_category == category)
                {
                    accuracy += 100.0 / testingTags.Count;
                    if (verbose) Console.WriteLine("{0}-{1}", testingTags[j], testingTags[inferred_category]);
                }
                else
                {
                    if (!recognitionMistake)
                    {
                        recognitionMistake = true;
                        Console.WriteLine("Recognition mistakes:");
                        //print "%5s" % "Input", "Output"
                    }
                    Console.WriteLine("{0}-{1}", testingTags[j], testingTags[inferred_category]);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Accuracy: {0:0.0} %", accuracy);
            Console.WriteLine();
            return accuracy;
        }

        private void PrintTrainingStats(int trainingCyclesCompleted, double accuracy)
        {
            // Print header if this is the first training cycle
            if (trainingCyclesCompleted == 0)
            {
                Console.WriteLine("\nTraining:\n");
                Console.Write("{0}", "".PadRight(5));
                Console.Write("{0}", "Connected".PadRight(16));
                Console.Write("{0}", "Unconnected".PadRight(19));
                Console.Write("{0}", "Recognition".PadRight(16));
                Console.Write("{0}", "Cycle".PadRight(5));
                Console.Write("{0}", "Percent".PadRight(10));
                Console.Write("{0}", "Mean".PadRight(8));
                Console.Write("{0}", "Percent".PadRight(10));
                Console.Write("{0}", "Mean".PadRight(8));
                Console.Write("{0}", "Accuracy".PadRight(13));
                Console.WriteLine();
            }
            // Calculate permanence stats
            double pctConnected = 0;
            double pctUnconnected = 0;
            double connectedMean = 0;
            double unconnectedMean = 0;
            double[] perms = new double[_connections.GetNumInputs()];
            var numCols = _connections.GetNumColumns();
            //foreach (int i in ArrayUtils.Range(0, numCols))
            //{
            //    perms = _connections.GetPotentialPools().Get(i).GetDensePermanences(_connections);
            //    int numPerms = perms.Length;
            //    var connectedPerms = perms.Where(p => p >= _connections.GetConnectedPermanence());
            //    double numConnected = connectedPerms.Sum();
            //    pctConnected += 100.0/numCols*numConnected/numPerms;
            //    double sumConnected = ArrayUtils.Multiply(perms,connectedPerms).Sum();
            //    connectedMean += sumConnected/(numConnected*numCols);
            //    var unconnectedPerms = perms.Where(p => p < _connections.GetConnectedPermanence());
            //    var numUnconnected = unconnectedPerms.Sum();
            //    pctUnconnected += 100.0/numCols*numUnconnected/numPerms;
            //    var sumUnconnected = (perms*unconnectedPerms).Sum();
            //    unconnectedMean += sumUnconnected/(numUnconnected*numCols);
            //}
            /*
            print "%5s" % trainingCyclesCompleted,
            print "%10s" % ("%.4f" % pctConnected),
            print "%8s" % ("%.3f" % connectedMean),
            print "%10s" % ("%.4f" % pctUnconnected),
            print "%8s" % ("%.3f" % unconnectedMean),
            print "%13s" % ("%.5f" % accuracy)
            */
        }
    }
}