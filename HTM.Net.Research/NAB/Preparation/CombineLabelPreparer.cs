using System.Collections.Generic;
using System.IO;
using System;

namespace HTM.Net.Research.NAB.Preparation;

public class CombineLabelPreparer<TCorpus, TCorpusLabel>
    where TCorpus : ICorpus
    where TCorpusLabel : ICorpusLabel
{
    public static void Prepare(PrepareCombineLabelsArguments args)
    {
        string dataDir;
        string labelDir;

        if (!args.absolutePaths)
        {
            string root = Utils.Recur(Path.GetDirectoryName, System.Reflection.Assembly.GetEntryAssembly().Location, 2);
            dataDir = Path.Combine(root, args.DataDir);
            labelDir = Path.Combine(root, args.LabelDir);
        }
        else
        {
            dataDir = args.DataDir;
            labelDir = args.LabelDir;
        }

        double windowSize = 0.10;
        double probationaryPercent = 0.15;

        Console.WriteLine("Getting corpus.");
        TCorpus corpus = (TCorpus)Activator.CreateInstance(typeof(TCorpus), new object[] { dataDir });

        Console.WriteLine("Creating LabelCombiner.");
        LabelCombiner labelCombiner = new LabelCombiner(labelDir, corpus, args.threshold, windowSize, probationaryPercent, args.verbosity);

        Console.WriteLine("Combining labels.");
        labelCombiner.Combine();

        Console.WriteLine("Writing combined labels files.");
        labelCombiner.Write(args.combinedLabelsPath, args.combinedWindowsPath);

        Console.WriteLine("Attempting to load objects as a test.");
        TCorpusLabel corpusLabel = (TCorpusLabel)Activator.CreateInstance(typeof(TCorpusLabel), new object[] { args.combinedWindowsPath, corpus }); 
        corpusLabel.ValidateLabels();

        Console.WriteLine("Successfully combined labels!");
        Console.WriteLine("Resulting windows stored in: " + args.combinedWindowsPath);
    }
}

public record PrepareCombineLabelsArguments(
    string LabelDir= "labels/raw", string DataDir ="data", string combinedLabelsPath= "labels/combined_windows.json",
    string combinedWindowsPath = "labels/combined_windows.json",
    bool absolutePaths = false, double threshold = 0.5,
    int verbosity = 1, bool skipConfirmation = false);