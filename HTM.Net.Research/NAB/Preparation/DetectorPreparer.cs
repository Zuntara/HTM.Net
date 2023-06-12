using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HTM.Net.Research.NAB.Preparation;

public static class DetectorPreparer
{
    public static void Prepare(CreateDetectorArguments args)
    {
        if (string.IsNullOrWhiteSpace(args.Detector))
        {
            throw new ArgumentException("Must specify detector name (--detector).");
        }

        string root = Utils.Recur(Path.GetDirectoryName, System.Reflection.Assembly.GetEntryAssembly().Location, 2);
        string thresholdFile = Path.Combine(root, args.ThresholdFile);
        string resultsDir = Path.Combine(root, args.ResultsDir);

        List<string> categorySubDirs = GetCategoryNames(args.DataDir, root);

        CreateThresholds(args.Detector, thresholdFile);
        CreateResultsDir(args.Detector, resultsDir, categorySubDirs);
    }

    private static Dictionary<string, object> ReadJSON(string filename)
    {
        string directoryName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        string filePath = Path.Combine(directoryName, filename);

        using (StreamReader file = File.OpenText(filePath))
        {
            JsonSerializer serializer = new JsonSerializer();
            Dictionary<string, object> data = (Dictionary<string, object>)serializer.Deserialize(file, typeof(Dictionary<string, object>));
            return data;
        }
    }

    private static void WriteJSON(string filename, object data)
    {
        string directoryName = Path.GetDirectoryName(System.Reflection.Assembly.GetEntryAssembly().Location);
        string filePath = Path.Combine(directoryName, filename);

        using (StreamWriter file = File.CreateText(filePath))
        {
            JsonSerializer serializer = new JsonSerializer();
            serializer.Serialize(file, data);
        }
    }

    private static Dictionary<string, object> GetOldDict(string filename)
    {
        if (File.Exists(filename))
        {
            return ReadJSON(filename);
        }
        else
        {
            return new Dictionary<string, object>();
        }
    }

    private static void CreateThresholds(string detectorName, string thresholdFile)
    {
        Dictionary<string, object> oldThresholds = GetOldDict(thresholdFile);

        if (!oldThresholds.ContainsKey(detectorName))
        {
            oldThresholds[detectorName] = new Dictionary<string, object>();
        }

        WriteJSON(thresholdFile, oldThresholds);
    }

    private static void CreateResultsDir(string detectorName, string resultsDir, IEnumerable<string> categorySubDirs)
    {
        string directory = Path.Combine(resultsDir, detectorName);

        if (!Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        foreach (string category in categorySubDirs)
        {
            string subdir = Path.Combine(directory, category);
            if (!Directory.Exists(subdir))
            {
                Directory.CreateDirectory(subdir);
            }
        }
    }

    private static List<string> GetCategoryNames(string dataDir, string root)
    {
        string[] subdirs = Directory.GetDirectories(dataDir);
        List<string> categoryNames = new List<string>();

        foreach (string subdir in subdirs)
        {
            string categoryName = Path.GetFileName(subdir);
            categoryNames.Add(categoryName);
        }

        return categoryNames;
    }
}

public record CreateDetectorArguments(string Detector, string ResultsDir = "results", string DataDir = "data", string ThresholdFile = "config/thresholds.json");