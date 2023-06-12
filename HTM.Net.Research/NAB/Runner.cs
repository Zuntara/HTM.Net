﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HTM.Net.Research.NAB.Detectors;
using Newtonsoft.Json;

namespace HTM.Net.Research.NAB;

/// <summary>
/// Class to run an endpoint (detect, optimize, or score) on the NAB
/// benchmark using the specified set of profiles, thresholds, and/or detectors.
/// </summary>
public class Runner<TCorpus, TCorpusLabel>
    where TCorpus : ICorpus
    where TCorpusLabel : ICorpusLabel
{
    private List<string> ResultsFiles { get; set; }
    private string DataDir { get; }
    private string ResultsDir { get; }
    private string LabelPath { get; }
    private string ProfilesPath { get; }
    private string ThresholdsPath { get; }
    private int NumCpus { get; }
    private double ProbationaryPercent { get; }
    private double WindowSize { get; }
    private ICorpus Corpus { get; set; }
    private ICorpusLabel CorpusLabel { get; set; }
    private Dictionary<string, Dictionary<string, Dictionary<string, double>>> Profiles { get; set; }

    public Runner(string dataDir, string resultsDir, string labelPath, string profilesPath, string thresholdsPath, int numCpus = 0)
    {
        DataDir = dataDir;
        ResultsDir = resultsDir;
        LabelPath = labelPath;
        ProfilesPath = profilesPath;
        ThresholdsPath = thresholdsPath;
        NumCpus = numCpus;

        this.ProbationaryPercent = 0.15f;
        this.WindowSize = 0.10f;

        this.Corpus = null;
        this.CorpusLabel = null;
        this.Profiles = null;
    }

    /// <summary>
    /// Initialize all the relevant objects for the run.
    /// </summary>
    public void Initialize()
    {
        Corpus = (TCorpus)Activator.CreateInstance(typeof(TCorpus), new object[]{DataDir});
        CorpusLabel = (TCorpusLabel)Activator.CreateInstance(typeof(TCorpusLabel), new object[] { LabelPath, Corpus });

        Profiles = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, double>>>>(File.ReadAllText(ProfilesPath));
    }

    /// <summary>
    /// Generate results file given a dictionary of detector classes
    ///
    /// Function that takes a set of detectors and a corpus of data and creates a
    ///    set of files storing the alerts and anomaly scores given by the detectors
    /// </summary>
    /// <param name="detectors">Dictionary with key value pairs of a detector name and its corresponding class constructor.</param>
    public void Detect(Dictionary<string, Type> detectors)
    {
        Console.WriteLine("Running detector step");

        int count = 0;
        List<RunArguments> args = new();
        foreach (var detectorPair in detectors)
        {
            var detectorName = detectorPair.Key;
            var detectorClass = detectorPair.Value;

            foreach (var dataFilePair in Corpus.DataFiles)
            {
                var relativePath = dataFilePair.Key;
                var dataSet = dataFilePair.Value;

                if (CorpusLabel.Labels.ContainsKey(relativePath))
                {
                    var detector = Activator.CreateInstance(detectorClass, dataSet, ProbationaryPercent) as IDetector;

                    args.Add(new RunArguments(count, detector, detectorName, CorpusLabel.Labels[relativePath]["label"],
                        ResultsDir, relativePath));

                    count++;
                }

                var tasks = args.Select(a => new Task(() => DetectorBase.DetectDataSet(a))).ToArray();
                Task.WaitAll(tasks);
            }
        }
    }

    public Dictionary<string, Dictionary<string, Dictionary<string, double>>> Optimize(List<string> detectorNames)
    {
        Console.WriteLine("\nRunning optimize step");

        bool scoreFlag = false;
        var thresholds = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();

        foreach (var detectorName in detectorNames)
        {
            var resultsDetectorDir = Path.Combine(this.ResultsDir, detectorName);
            var resultsCorpus = (TCorpus)Activator.CreateInstance(typeof(TCorpus), new object[] { resultsDetectorDir });

            thresholds[detectorName] = new Dictionary<string, Dictionary<string, double>>();

            foreach (var profileName in this.Profiles.Keys)
            {
                var profile = this.Profiles[profileName];
                var threshold = Optimizer.OptimizeThreshold(
                    detectorName,
                    profile["CostMatrix"],
                    resultsCorpus,
                    this.CorpusLabel,
                    this.ProbationaryPercent);

                thresholds[detectorName][profileName] = threshold;
            }
        }

        Utils.UpdateThresholds(thresholds, this.ThresholdsPath);

        return thresholds;
    }

    public void Score(List<string> detectorNames, Dictionary<string, Dictionary<string, Dictionary<string, double>>> thresholds)
    {
        Console.WriteLine("\nRunning scoring step");

        bool scoreFlag = true;
        var baselines = new Dictionary<string, object>();

        this.ResultsFiles = new List<string>();
        foreach (var detectorName in detectorNames)
        {
            var resultsDetectorDir = Path.Combine(this.ResultsDir, detectorName);
            var resultsCorpus = (TCorpus)Activator.CreateInstance(typeof(TCorpus), new object[] { resultsDetectorDir });

            foreach (var profileName in this.Profiles.Keys)
            {
                var profile = this.Profiles[profileName];
                var threshold = (double)thresholds[detectorName][profileName]["threshold"];
                var resultsDF = Scorer.ScoreCorpusAsync(threshold,
                    detectorName,
                    profileName,
                    profile["CostMatrix"],
                    resultsDetectorDir,
                    resultsCorpus,
                    this.CorpusLabel,
                    this.ProbationaryPercent,
                    scoreFlag).Result;

                var scorePath = Path.Combine(resultsDetectorDir, $"{detectorName}_{profileName}_scores.csv");

                resultsDF.ToCsv(scorePath, false);
                Console.WriteLine($"{detectorName} detector benchmark scores written to {scorePath}");
                this.ResultsFiles.Add(scorePath);
            }
        }
    }

    public void Normalize()
    {
        Console.WriteLine("\nRunning score normalization step");

        var nullDir = Path.Combine(this.ResultsDir, "null");
        if (!Directory.Exists(nullDir))
        {
            throw new IOException("No results directory for null detector. You must run the null detector before normalizing scores.");
        }

        var baselines = new Dictionary<string, double>();
        foreach (var profileName in this.Profiles.Keys)
        {
            var fileName = Path.Combine(nullDir, $"null_{profileName}_scores.csv");
            using (var reader = new StreamReader(fileName))
            {
                var results = Scorer.ResultsList.ReadCsv(reader);
                baselines[profileName] = results.GetColumn<double>("Score").Last();
            }
        }

        var tpCount = 0;
        using (var reader = new StreamReader(this.LabelPath))
        {
            var labelsDict = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(reader.ReadToEnd());
            tpCount = labelsDict.Values.Select(labels => labels.Count).Sum();
        }

        var finalResults = new Dictionary<string, Dictionary<string, Dictionary<string, double>>>();
        foreach (var resultsFile in this.ResultsFiles)
        {
            var profileName = baselines.Keys.FirstOrDefault(k => resultsFile.Contains(k));
            var baseValue = (double)baselines[profileName];

            using (var reader = new StreamReader(resultsFile))
            {
                var results = Scorer.ResultsList.ReadCsv(reader);

                var profile = resultsFile.Split(Path.DirectorySeparatorChar).Last().Split('.')[0].Replace(profileName + "_scores", "").Replace("_", "");
                var detector = resultsFile.Split(Path.DirectorySeparatorChar).Last().Split('_')[0];

                var perfect = tpCount * (double)this.Profiles[profileName]["CostMatrix"]["tpWeight"];
                var score = 100 * (results.GetColumn<double>("Score").Last() - baseValue) / (perfect - baseValue);

                if (!finalResults.ContainsKey(detector))
                {
                    finalResults[detector] = new Dictionary<string, Dictionary<string, double>>();
                }

                finalResults[detector][profile]["score"] = score;

                Console.WriteLine($"Final score for '{detector}' detector on '{profile}' profile = {score:F2}");
            }
        }

        var resultsPath = Path.Combine(this.ResultsDir, "final_results.json");

        Utils.UpdateFinalResults(finalResults, resultsPath);

        Console.WriteLine($"Final scores have been written to {resultsPath}.");
    }
}