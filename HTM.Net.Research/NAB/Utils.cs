using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HTM.Net.Util;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HTM.Net.Research.NAB;

public class Utils
{
    //   Buckets (groups) timestamps that are within the amount of time specified by
    //   buffer.
    //   
    public static List<List<T>> Bucket<T>(List<T> rawTimes, TimeSpan buffer)
    {
        List<T> bucket = new List<T>();
        List<List<T>> rawBuckets = new List<List<T>>();

        T current = default(T);
        foreach (T t in rawTimes)
        {
            if (current == null || current.Equals(default(T)))
            {
                current = t;
                bucket = new List<T> { current };
                continue;
            }
            if (((dynamic)t - (dynamic)current) <= buffer)
            {
                bucket.Add(t);
            }
            else
            {
                rawBuckets.Add(bucket);
                current = t;
                bucket = new List<T> { current };
            }
        }
        if (bucket.Count > 0)
        {
            rawBuckets.Add(bucket);
        }

        return rawBuckets;
    }


    // 
    //   Merges bucketed timestamps into one timestamp (most frequent, or earliest).
    //   
    public static (List<T> truths, List<List<T>> passed) Merge<T>(List<List<T>> rawBuckets, double threshold)
    {
        List<T> truths = new List<T>();
        List<List<T>> passed = new List<List<T>>();

        foreach (List<T> bucket in rawBuckets)
        {
            if (bucket.Count >= threshold)
            {
                truths.Add(bucket.GroupBy(x => x).OrderByDescending(x => x.Count()).First().Key);
            }
            else
            {
                passed.Add(bucket);
            }
        }

        return (truths, passed);
    }

    // 
    //   Raise a ValueError if the difference between any consecutive labels is smaller
    //   than the buffer.
    //   
    public static void CheckForOverlap(List<(DateTime start, DateTime end)> labels, TimeSpan buffer, string labelsFileName, string dataFileName)
    {
        for (int i = 0; i < labels.Count; i++)
        {
            if (labels[i].end - labels[i].start <= buffer)
            {
                throw new InvalidOperationException($"The labels {labels[i].start} and {labels[i].end} in '{labelsFileName}' labels for data file '{dataFileName}' are too close to each other to be considered distinct anomalies. Please relabel.");
            }
        }
    }

    public static int getProbationPeriod(double probationPercent, int fileLength)
    {
        return (int)Math.Min(Math.Floor(fileLength * (double)probationPercent), probationPercent * 5000.0);
    }

    public static string convertResultsPathToDataPath(string path)
    {
        var parts = path.Split(Path.DirectorySeparatorChar);
        string detector = parts[0];
        var pth = parts[Range.StartAt(1)];
        var fileName = pth[^1];
        var toRemove = detector + "_";

        var i = fileName.IndexOf(toRemove, StringComparison.InvariantCultureIgnoreCase);
        fileName = fileName.Substring(0, i) + fileName.Substring(i + toRemove.Length);

        pth[pth.Length - 1] = fileName;

        return string.Join(Path.DirectorySeparatorChar, pth);
    }

    /// <summary>
    /// Update results file with new results
    /// </summary>
    /// <param name="newResults">[detector][profile][score/threshold]</param>
    /// <param name="resultsFilePath">Path</param>
    /// <returns></returns>
    public static Dictionary<Detector, Map<string, Map<string, double>>> UpdateFinalResults(Dictionary<Detector, Map<string, Map<string, double>>> newResults, string resultsFilePath)
    {
        var results = GetOldDict(resultsFilePath);

        foreach (var pair in newResults)
        {
            Detector detector = pair.Key;
            var score = pair.Value;

            results[detector] = score;
        }

        WriteJSON(resultsFilePath, results);

        return results;
    }

    public static Dictionary<Detector, Map<string, Map<string, double>>> UpdateThresholds(Dictionary<Detector, Map<string, Map<string, double>>> newThresholds, string thresholdsFilePath)
    {
        // newThresholds [detector][profile][score/threshold]
        var oldThresholds = GetOldDict(thresholdsFilePath);

        foreach (var detector in newThresholds.Keys)
        {
            if (!oldThresholds.ContainsKey(detector))
            {
                // add an entry for a new detector
                oldThresholds[detector] = newThresholds[detector];
                continue;
            }

            foreach (var profileName in newThresholds[detector].Keys)
            {
                if (!oldThresholds[detector].ContainsKey(profileName))
                {
                    // add an entry for a new scoring profile under this detector
                    oldThresholds[detector][profileName] = newThresholds[detector][profileName];
                    continue;
                }
                oldThresholds[detector][profileName] = newThresholds[detector][profileName];
            }
        }

        WriteJSON(thresholdsFilePath, oldThresholds);

        return oldThresholds;
    }

    public static Dictionary<Detector, Map<string, Map<string, double>>> GetOldDict(string filePath)
    {
        if (File.Exists(filePath))
        {
            var json = File.ReadAllText(filePath);
            var dataDict = JsonConvert.DeserializeObject<Dictionary<Detector, Map<string, Map<string, double>>>>(json);
            return dataDict;
        }
        else
        {
            return new Dictionary<Detector, Map<string, Map<string, double>>>();
        }
    }

    public static void WriteJSON(string filePath, object data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    public static List<(T start, T end)> TimeMap<T>(Func<string, T> convert, List<List<string>> root)
    {
        // "realAWSCloudwatch/ec2_cpu_utilization_5f5533.csv": [
        //      "2014-02-19 00:22:00",
        //      "2014-02-24 18:37:00"
        //  ],
        List<(T start, T end)> result = new List<(T start, T end)>();
        for (int i = 0; i < root.Count; i++)
        {
            var subElement = root[i];
            for (int j = 0; j < subElement.Count - 1; j++)
            {
                var start = convert(subElement[j]);
                var end = convert(subElement[j+1]);
                result.Add((start, end));
            }
        }

        return result;
    }

    public static List<(T start, T end)> TimeMap<T>(Func<string, T> convert, object root)
    {
        // "realAWSCloudwatch/ec2_cpu_utilization_5f5533.csv": [
        //      "2014-02-19 00:22:00",
        //      "2014-02-24 18:37:00"
        //  ],
        List<(T start, T end)> result = new List<(T start, T end)>();
        if (root is JArray list)
        {
            if (list.Count > 0 && list[0] is JArray)
            {
                // nested array
                for (int i = 0; i < list.Count; i++)
                {
                    var innerList = (JArray)list[i];
                    var results = TimeMap(convert, innerList);
                    result.AddRange(results);
                }
            }
            else
            {
                // single array
                for (int i = 0; i < list.Count - 1; i++)
                {
                    var item = (JToken)list[i];
                    var item2 = (JToken)list[i+1];
                    var start = convert(item.Value<DateTime>().ToString("s"));
                    var end = convert(item2.Value<DateTime>().ToString("s"));
                    result.Add((start, end));
                }
            }
        }
        else if (root is IList list2)
        {
            if (list2.Count > 0 && list2[0] is IList)
            {
                // nested array
                for (int i = 0; i < list2.Count; i++)
                {
                    var innerList = (IList)list2[i];
                    var results = TimeMap(convert, innerList);
                    result.AddRange(results);
                }
            }
            else
            {
                // single array
                for (int i = 0; i < list2.Count - 1; i++)
                {
                    var item = list2[i] is DateTime dt ? dt.ToString("s"): (string)list2[i];
                    var item2 = list2[i + 1] is DateTime dt2 ? dt2.ToString("s") : (string)list2[i + 1];
                    var start = convert(item);
                    var end = convert(item2);
                    result.Add((start, end));
                }
            }
        }

        return result;
    }

    public static T Recur<T>(Func<T, T> function, T value, int n)
    {
        if (n < 0 || n != Convert.ToInt32(n))
        {
            Console.WriteLine("incorrect input");
            throw new ArgumentException("incorrect input");
        }
        else if (n == 0)
        {
            return value;
        }
        else if (n == 1)
        {
            return function(value);
        }
        else
        {
            return Recur(function, function(value), n - 1);
        }
    }
}