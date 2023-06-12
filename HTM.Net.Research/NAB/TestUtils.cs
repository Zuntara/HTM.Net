using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;

namespace HTM.Net.Research.NAB;

public class TestUtils
{
    public static void WriteCorpusLabel(string labelsPath, Dictionary<string, List<List<string>>> labelWindows)
    {
        CreatePath(Path.GetDirectoryName(labelsPath));
        string windows = JsonConvert.SerializeObject(labelWindows, Formatting.Indented);

        File.WriteAllText(labelsPath, windows);
    }

    public static void WriteCorpusLabel(string labelsPath, Dictionary<string, List<string>> labelWindows)
    {
        CreatePath(Path.GetDirectoryName(labelsPath));
        string windows = JsonConvert.SerializeObject(labelWindows, Formatting.Indented);

        File.WriteAllText(labelsPath, windows);
    }

    private static void CreatePath(string path)
    {
        if (!Directory.Exists(path))
        {
            Directory.CreateDirectory(path);
        }
    }

    public static void WriteCorpus(string corpusDir, Dictionary<string, DataFrame> corpusData)
    {
        CreatePath(corpusDir);

        foreach (var kvp in corpusData)
        {
            string relativePath = kvp.Key;
            DataFrame data = kvp.Value;
            string dataFilePath = Path.Combine(corpusDir, relativePath);
            //CreatePath(dataFilePath);
            data.ToCsv(dataFilePath, includeHeaders: true);
        }
    }

    public static List<DateTime> GenerateTimestamps(DateTime start, TimeSpan? increment, int length)
    {
        List<DateTime> timestamps = new List<DateTime> { start };
        for (int i = 0; i < length - 1; i++)
        {
            timestamps.Add(timestamps[i] + increment.GetValueOrDefault());
        }
        return timestamps;
    }

    public static List<List<DateTime>> GenerateWindows(List<DateTime> timestamps, int numWindows, int windowSize)
    {
        DateTime start = timestamps[0];
        TimeSpan delta = timestamps[1] - timestamps[0];
        int diff = (int)Math.Round((timestamps.Count - (numWindows * windowSize)) / (numWindows + 1.0));
        List<List<DateTime>> windows = new List<List<DateTime>>();
        for (int i = 0; i < numWindows; i++)
        {
            DateTime t1 = start + (delta * diff * (i + 1)) + (delta * windowSize * i);
            DateTime t2 = t1 + (delta * (windowSize - 1));
            if (!timestamps.Contains(t1) || !timestamps.Contains(t2))
            {
                throw new InvalidOperationException("You got the wrong times from the window generator");
            }
            windows.Add(new List<DateTime> { t1, t2 });
        }
        return windows;
    }
}