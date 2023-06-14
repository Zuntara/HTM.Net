using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HTM.Net.Research.NAB.Detectors;

public static class DetectorBase
{
    public static void DetectDataSet(RunArguments args)
    {
        var (i, detectorInstance, detectorName, labels, outputDir, relativePath) = args;
        string relativeDir = Path.GetDirectoryName(relativePath);
        string filename = Path.GetFileName(relativePath);
        filename = detectorName + "_" + filename;
        string outputPath = Path.Combine(outputDir, detectorName, relativeDir, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath));

        Console.WriteLine($"{i}: Beginning detection with {detectorName} for {relativePath}");

        detectorInstance.Initialize();

        DataFrame results = null;
        if (detectorInstance is IRxDetector rxDetector)
        {
            rxDetector.AllRecordsProcessed.Subscribe(df =>
            {
                HandleProcessedRecords(i, outputPath, results, labels);
            });

            rxDetector.Run();
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
        File.WriteAllText(outputPath, JsonConvert.SerializeObject(dataFrame));

        Console.WriteLine($"{i}: Completed processing {dataFrame.GetShape0()} at {DateTime.Now}");
        Console.WriteLine($"{i}: Results written to {outputPath}");
    }
}