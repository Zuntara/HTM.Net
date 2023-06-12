using System;
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

        var results = detectorInstance.Run();

        // label =1 for relaxed windows, 0 otherwise
        results["labels"] = labels;

        // Write results to file
        File.WriteAllText(outputPath, JsonConvert.SerializeObject(results));
        Console.WriteLine($"{i}: Completed processing {results.GetShape0()} at {DateTime.Now}");
        Console.WriteLine($"{i}: Results written to {outputPath}");
    }
}