using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

namespace HTM.Net.Research.NAB.Detectors;

public static class DetectorBase
{
    public static void DetectDataSet(RunArguments args)
    {
        var (i, detectorInstance, detectorName, labels, outputDir, relativePath) = args;
        string relativeDir = Path.GetDirectoryName(relativePath);
        string filename = Path.GetFileName(relativePath);
        filename = detectorName + "_" + filename;
        string outputPath = Path.Combine(outputDir, detectorName.ToString(), relativeDir, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        Console.WriteLine($"{i}: Beginning detection with {detectorName} for {relativePath}");

        detectorInstance.Initialize();

        DataFrame results = null;
        if (detectorInstance is IRxDetector rxDetector)
        {
            ManualResetEvent waitHandle = new ManualResetEvent(false);
            rxDetector.AllRecordsProcessed.Subscribe(df =>
                {
                    HandleProcessedRecords(i, outputPath, df, labels);
                },
                e => waitHandle.Set(),
                () => waitHandle.Set());

            rxDetector.Run();
            waitHandle.WaitOne();
        }
        else if (detectorInstance is IDirectDetector directDetector)
        {
            results = directDetector.Run();
            HandleProcessedRecords(i, outputPath, results, labels);
        }
    }

    private static void HandleProcessedRecords(int i, string outputPath, DataFrame dataFrame, List<object> labels)
    {
        // label =1 for relaxed windows, 0 otherwise
        dataFrame["labels"] = labels;

        // Write results to file
        dataFrame.ToCsv(outputPath, true);

        Console.WriteLine($"{i}: Completed processing {dataFrame.GetShape0()} at {DateTime.Now}");
        Console.WriteLine($"{i}: Results written to {outputPath}");
    }
}