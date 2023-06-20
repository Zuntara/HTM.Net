using HTM.Net.Research.Data;
using MathNet.Numerics;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tweetinvi.Core.Events;

namespace HTM.Net.Research.NAB;

public interface ICorpusLabel
{
    string Path { get; }
    ICorpus Corpus { get; }
    Dictionary<string, DataFrame> Labels { get; }
    Dictionary<string, List<(DateTime start, DateTime end)>> Windows { get; }

    void GetWindows();
    void GetLabels();
    void ValidateLabels();
}

/// <summary>
/// Class to store and manipulate a single set of labels for the whole
///   benchmark corpus.
/// </summary>
public class CorpusLabel: ICorpusLabel
{
    public string Path { get; }
    public ICorpus Corpus { get; }
    public Dictionary<string, DataFrame> Labels { get; private set; }
    public Dictionary<string, List<(DateTime start, DateTime end)>> Windows { get; private set; }

    public CorpusLabel(string path, ICorpus corpus)
    {
        this.Path = path.Replace("/", "\\");
        this.Windows = null;
        this.Labels = null;
        this.Corpus = corpus;
        this.GetWindows();
        if (!this.Path.Contains("raw"))
        {
            // Do not get labels from files in the Path nab/labels/raw
            this.GetLabels();
        }
    }

    // 
    //     Read JSON label file. Get timestamps as dictionaries with key:value pairs of
    //     a relative Path and its corresponding list of windows.
    //     
    public void GetWindows()
    {
        List<string> timestamps;

        Func<string, DataFrame, bool> found = (t, data) =>
        {
            var f = data.Where("timestamp", item => (string)item == t).GetColumnIndices("timestamp");
            //var f = data["timestamp"][data["timestamp"] == t];
            var exists = f.Count == 1;
            return exists;
        };

        Dictionary<string, object> windows;
        using (StreamReader windowFile = File.OpenText(this.Path))
        {
            string json = windowFile.ReadToEnd();
            windows = JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
            windows = windows.ToDictionary(k => k.Key.Replace("/", "\\"), v => v.Value);
        }

        this.Windows = new Dictionary<string, List<(DateTime start, DateTime end)>>();
        foreach (var relativePathRaw in windows.Keys.ToList())
        {
            var relativePath = relativePathRaw.Replace("/", "\\");
            this.Windows[relativePath] = Utils.TimeMap(DateTime.Parse, windows[relativePath]);
            if (this.Windows[relativePath].Count == 0)
            {
                continue;
            }
            //artificialWithAnomaly\art_daily_flatmiddle.csv
            //artificialWithAnomaly/art_daily_flatmiddle.csv
            var data = this.Corpus.DataFiles[relativePath].Data;
       
            if (this.Path.Contains("raw"))
            {
                timestamps = ((JArray)windows[relativePath]).Select(i => i.Value<DateTime>().ToString("s")).ToList();
            }
            else
            {
                timestamps = ((JArray)windows[relativePath]).SelectMany(list => (JArray)list).Select(i => i.Value<DateTime>().ToString("s")).ToList();
            }

            // Check that timestamps are present in dataset
            if (!timestamps.All(t => found(t, data)))
            {
                throw new InvalidOperationException(
                    $"In the label file {this.Path}, one of the timestamps used for the datafile {this.Path} doesn't match; " +
                    $"it does not exist in the file. Timestamps in json label files have to exactly match timestamps in corresponding datafiles.");
            }
        }
    }

    // 
    //     This is run at the end of the label combining process (see
    //     scripts/combine_labels.py) to validate the resulting ground truth windows,
    //     specifically that they are distinct (unique, non-overlapping).
    //     
    public virtual void ValidateLabels()
    {
        Dictionary<string, List<List<string>>> windows;
        using (StreamReader windowFile = File.OpenText(this.Path))
        {
            windows = JsonConvert.DeserializeObject<Dictionary<string, List<List<string>>>>(windowFile.ReadToEnd());
        }

        this.Windows = new Dictionary<string, List<(DateTime start, DateTime end)>>();
        foreach (var relativePath in windows.Keys.ToList())
        {
            this.Windows[relativePath] = Utils.TimeMap(DateTime.Parse, windows[relativePath]);
            if (this.Windows[relativePath].Count == 0)
            {
                continue;
            }

            var num_windows = this.Windows[relativePath].Count;
            if (num_windows > 1)
            {
                if (!Enumerable.Range(0, num_windows - 1)
                        .All(i => ((this.Windows[relativePath][i + 1].start - this.Windows[relativePath][i].end)
                            .TotalSeconds >= 0)))
                {
                    throw new InvalidOperationException($"In the label file {this.Path}, windows overlap.");
                }
            }
        }
    }

    public void GetLabels()
    {
        // Get Labels as a dictionary of key-value pairs of a relative Path and its
        // corresponding binary vector of anomaly labels. Labels are simply a more
        // verbose version of the windows.
        this.Labels = new Dictionary<string, DataFrame>();

        foreach (KeyValuePair<string, IDataFile> dataFile in this.Corpus.DataFiles)
        {
            string relativePath = dataFile.Key;
            IDataFile dataSet = dataFile.Value;

            if (this.Windows.ContainsKey(relativePath))
            {
                List<(DateTime start, DateTime end)> windows = this.Windows[relativePath];

                DataFrame labels = new DataFrame();
                
                labels.Add("timestamp", dataSet.Data["timestamp"]);
                labels.Add("label", 0);

                foreach ((DateTime start, DateTime end) window in windows)
                {
                    DataFrame moreThanT1 = labels.Where("timestamp", item => DateTime.Parse((string)item) >= window.start);
                    //DataFrame moreThanT1 = labels[labels["timestamp"] >= window.start];
                    //DataFrame betweenT1AndT2 = moreThanT1[moreThanT1["timestamp"] <= window.end];
                    DataFrame betweenT1AndT2 = moreThanT1.Where("timestamp", item => DateTime.Parse((string)item) <= window.end);
                    List<int> indices = betweenT1AndT2.GetColumnIndices("label");
                    foreach (int index in indices)
                    {
                        labels["label",index] = 1;
                    }
                }

                this.Labels[relativePath] = labels;
            }
            else
            {
                Console.WriteLine("Warning: no label for datafile " + relativePath);
            }
        }
    }
}

// 
//   This class is used to combine labels from multiple human labelers, and the set
//   of manual labels (known anomalies).
//   The output is a single ground truth label file containing anomalies where
//   there is enough human agreement. The class also computes the window around
//   each anomaly.  The exact logic is described elsewhere in the NAB
//   documentation.
//   
public class LabelCombiner
{
    private string labelDir;
    private ICorpus corpus;
    private int? nLabelers;
    private double threshold;
    private double windowSize;
    private double probationaryPercent;
    private int verbosity;
    private List<ICorpusLabel> userLabels;
    private List<ICorpusLabel> knownLabels;
    private Dictionary<string, List<int>> labelIndices;
    private Dictionary<string, List<(DateTime start, DateTime end)>> combinedWindows;
    private Dictionary<string, List<string>> labelTimestamps;

    public LabelCombiner(
        string labelDir,
        ICorpus corpus,
        double threshold,
        double windowSize,
        double probationaryPercent,
        int verbosity)
    {
        this.labelDir = labelDir;
        this.corpus = corpus;
        this.threshold = threshold;
        this.windowSize = windowSize;
        this.probationaryPercent = probationaryPercent;
        this.verbosity = verbosity;
        this.userLabels = null;
        this.nLabelers = null;
        this.knownLabels = null;
        this.combinedWindows = null;
    }

    public override string ToString()
    {
        var ans = "";
        ans += $"labelDir:            {this.labelDir}\n";
        ans += $"corpus:              {this.corpus}\n";
        ans += $"number of labelers:  {this.nLabelers}\n";
        ans += $"agreement threshold: {this.threshold}\n";
        return ans;
    }

    /// <summary>
    /// Write the combined labels and windows to destination directories.
    /// </summary>
    /// <param name="labelsPath"></param>
    /// <param name="windowsPath"></param>
    public virtual void Write(string labelsPath, string windowsPath)
    {
        if (!Directory.Exists(labelsPath))
        {
            Directory.CreateDirectory(labelsPath);
        }

        if (!Directory.Exists(windowsPath))
        {
            Directory.CreateDirectory(windowsPath);
        }

        WriteJSON(labelsPath, this.labelTimestamps);
        WriteJSON(windowsPath, this.combinedWindows);
    }

    private void WriteJSON(string path, object data)
    {
        using (StreamWriter writer = new StreamWriter(path, Encoding.UTF8, new FileStreamOptions{Access = FileAccess.Write}))
        {
            string json = JsonConvert.SerializeObject(data, Formatting.Indented);
            writer.Write(json);
            writer.Flush();
        }
    }

    // Combine raw and known labels in anomaly windows.
    public virtual void Combine()
    {
        this.GetRawLabels();
        this.CombineLabels();
        this.editPoorLabels();
        this.applyWindows();
        this.checkWindows();
    }

    // Collect the raw user labels from specified directory.
    public virtual void GetRawLabels()
    {
        var labelPaths = Directory.GetFiles(this.labelDir);
        this.userLabels = new List<ICorpusLabel>();
        this.knownLabels = new List<ICorpusLabel>();
        foreach (var path in labelPaths)
        {
            if (path.Contains("known"))
            {
                this.knownLabels.Add(new CorpusLabel(path, this.corpus));
            }
            else
            {
                this.userLabels.Add(new CorpusLabel(path, this.corpus));
            }
        }
        this.nLabelers = this.userLabels.Count;
        if (this.nLabelers == 0)
        {
            throw new InvalidOperationException("No users labels found");
        }
    }

    /// <summary>
    ///     Combines raw user labels to create set of true anomaly labels.
    ///     A buffer is used to bucket labels that identify the same anomaly. The buffer
    ///     is half the estimated window size of an anomaly -- approximates an average
    ///     of two anomalies per dataset, and no window can have > 1 anomaly.
    ///     After bucketing, a label becomes a true anomaly if it was labeled by a
    ///     proportion of the users greater than the defined threshold. Then the bucket
    ///     is merged into one timestamp -- the ground truth label.
    ///     The set of known anomaly labels are added as well. These have been manually
    ///     labeled because we know the direct causes of the anomalies. They are added
    ///     as if they are the result of the bucket-merge process.
    /// 
    ///     If verbosity > 0, the dictionary passedLabels -- the raw labels that did not
    ///     pass the threshold qualification -- is printed to the console.
    ///     
    /// </summary>
    public virtual (Dictionary<string, List<string>> labelTimestamps, Dictionary<string, List<int>> labelIndices) CombineLabels()
    {
        Func<IDataFile, List<(DateTime start, DateTime end)>, List<int>> setTruthLabels = (dataSet, trueAnomalies) =>
        {
            var timestamps = dataSet.Data["timestamp"].Select(r => DateTime.Parse(r as string)).ToList();
            var labelTruth = timestamps.Select((t, index) => trueAnomalies.Any(ta => ta.start == t || ta.end == t) ? index  : 0).ToList();
            /*var labels = numpy.array(timestamps.isin(trueAnomalies), dtype: @int);
            return (from i in Enumerable.Range(0, labels.Count)
                    where labels[i] == 1
                    select i).ToList();*/
            return labelTruth.Where(t => t > 0).ToList();
        };

        this.labelTimestamps = new Dictionary<string, List<string>>();
        this.labelIndices = new Dictionary<string, List<int>>();

        foreach (var dataFilePair in this.corpus.DataFiles)
        {
            var relativePath = dataFilePair.Key;
            var dataSet = dataFilePair.Value;
            if (relativePath.Contains("Known") || relativePath.Contains("artificial"))
            {
                var knownAnomalies = this.knownLabels[0].Windows[relativePath]
                    .Select(t => (start:t.start, end:t.end)).ToList();
                this.labelTimestamps[relativePath] = knownAnomalies
                    .SelectMany(t => new[] { t.start.ToString("s"), t.end.ToString("s") })
                    .ToList();
                this.labelIndices[relativePath] = setTruthLabels(dataSet, knownAnomalies);
                continue;
            }

            // Calculate the window buffer -- used for bucketing labels identifying
            // the same anomaly.
            var granularity = DateTime.Parse(dataSet.Data["timestamp"][1]  as string) - DateTime.Parse(dataSet.Data["timestamp"][0] as string);
            var buffer = TimeSpan.FromMinutes(granularity.TotalMinutes * dataSet.Data.GetShape0() * this.windowSize / 10);
            var rawTimesLists = new List<List<(DateTime start, DateTime end)>>();
            var userCount = 0;
            foreach (var user in this.userLabels)
            {
                if (user.Windows.ContainsKey(relativePath))
                {
                    // the user has labels for this file
                    Utils.CheckForOverlap(user.Windows[relativePath], buffer, user.Path, relativePath);
                    rawTimesLists.Add(user.Windows[relativePath]);
                    userCount += 1;
                }
            }

            List<DateTime> rawTimes = null;
            if (!rawTimesLists.Any())
            {
                // no labeled anomalies for this data file
                this.labelTimestamps[relativePath] = new List<string>();
                this.labelIndices[relativePath] = setTruthLabels(dataSet, new List<(DateTime, DateTime)>());
                continue;
            }
            else
            {
                //var rawTimes = itertools.chain.from_iterable(rawTimesLists).ToList();
                rawTimes = rawTimesLists.SelectMany(t => t).SelectMany(t => new[] { t.start, t.end }).ToList();
                rawTimes.Sort();
            }

            // Bucket and merge the anomaly timestamps.
            var threshold = userCount * this.threshold;
            var mergedTuple = Utils.Merge(Utils.Bucket(rawTimes, buffer), threshold);
            var trueAnomalies = mergedTuple.truths.Select(t => t.ToString("s")).ToList();
            var passedAnomalies = mergedTuple.passed;

            this.labelTimestamps[relativePath] = trueAnomalies.ToList();
            this.labelIndices[relativePath] = setTruthLabels(dataSet, Utils.TimeMap(DateTime.Parse, trueAnomalies));
            
            if (this.verbosity > 0)
            {
                Console.WriteLine("----");
                Console.WriteLine(String.Format("For %s the passed raw labels and qualified true labels are, respectively:", relativePath));
                Console.WriteLine(passedAnomalies);
                Console.WriteLine(trueAnomalies);
            }
        }

        return (this.labelTimestamps, this.labelIndices);
    }

    // 
    //     This edits labels that have been flagged for manual revision. From
    //     inspecting the data and anomaly windows, we have determined some combined
    //     labels should be revised, or not included in the ground truth labels.
    //     
    public virtual void editPoorLabels()
    {
        var count = 0;
        foreach (var _tup_1 in this.labelIndices)
        {
            var relativePath = _tup_1.Key;
            var indices = _tup_1.Value;
            if (relativePath.Contains("iio_us-east-1_i-a2eb1cd9_NetworkIn"))
            {
                this.labelIndices[relativePath] = new List<int> {
                            249,
                            339
                        };
            }
            count += indices.Count;
        }
        if (this.verbosity > 0)
        {
            Console.WriteLine("=============================================================");
            Console.WriteLine($"Total ground truth anomalies in benchmark dataset = {count}");
        }
    }

    // 
    //     This takes all the true anomalies, as calculated by combineLabels(), and
    //     adds a standard window. The window length is the class variable windowSize,
    //     and the location is centered on the anomaly timestamp.
    // 
    //     If verbosity = 2, the window metrics are printed to the console.
    //     
    public virtual void applyWindows()
    {
        int windowLength;
        var allWindows = new Dictionary<string, List<(DateTime, DateTime)>>();
        foreach (var _tup_1 in this.labelIndices)
        {
            var relativePath = _tup_1.Key;
            var anomalies = _tup_1.Value;
            var data = this.corpus.DataFiles[relativePath].Data;
            int length = data.GetShape0();
            int num = anomalies.Count;
            if (num > 0)
            {
                windowLength = Convert.ToInt32(this.windowSize * length / anomalies.Count);
            }
            else
            {
                windowLength = Convert.ToInt32(this.windowSize * length);
            }
            if (this.verbosity == 2)
            {
                Console.WriteLine("----");
                Console.WriteLine($"Window metrics for file {relativePath}");
                Console.WriteLine($"file length = {length}; number of windows = {num}; window length = {windowLength}");
            }
            var windows = new List<(DateTime, DateTime)>();
            foreach (var a in anomalies)
            {
                var front = Math.Max(a - windowLength / 2, 0);
                var back = Math.Min(a + windowLength / 2, length - 1);
                var windowLimit = (
                    data["timestamp"][front] is DateTime ? (DateTime)data["timestamp"][front] : default, 
                    data["timestamp"][back] is DateTime ? (DateTime)data["timestamp"][back] : default);
            
                windows.Add(windowLimit);
            }
            allWindows[relativePath] = windows;
        }
        this.combinedWindows = allWindows;
    }

    // 
    //     This takes the anomaly windows and checks for overlap with both each other
    //     and with the probationary period. Overlapping windows are merged into a
    //     single window. Windows overlapping with the probationary period are deleted.
    //     
    public virtual void checkWindows()
    {
        foreach (var _tup_1 in this.combinedWindows)
        {
            var relativePath = _tup_1.Key;
            var windows = _tup_1.Value;
            var numWindows = windows.Count;
            if (numWindows > 0)
            {
                var fileLength = this.corpus.DataFiles[relativePath].Data.GetShape0();
                var probationIndex = Utils.getProbationPeriod(this.probationaryPercent, fileLength);
                var probationTimestamp = (DateTime)this.corpus.DataFiles[relativePath].Data["timestamp"][(int)probationIndex];
                
                if ( (windows[0].Item1 - probationTimestamp).TotalSeconds < 0)
                {
                    windows.RemoveAt(0);
                    Console.WriteLine("The first window in {0} overlaps with the probationary period , so we're deleting it.",relativePath);
                }
                var i = 0;
                while (windows.Count - 1 > i)
                {
                    if ((windows[i + 1].Item1 - windows[i].Item2).TotalSeconds <= 0)
                    {
                        // merge windows
                        windows[i] = (windows[i].Item1, windows[i + 1].Item2);
                        windows.RemoveAt(i + 1);
                    }
                    i += 1;
                }
            }
        }
    }
}