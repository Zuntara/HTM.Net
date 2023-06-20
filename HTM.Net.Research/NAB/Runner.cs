using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using HTM.Net.Research.NAB.Detectors;
using HTM.Net.Research.NAB.Detectors.Expose;
using HTM.Net.Research.NAB.Detectors.HtmCore;
using HTM.Net.Research.NAB.Detectors.Knncad;
using HTM.Net.Research.NAB.Detectors.Null;
using HTM.Net.Research.NAB.Detectors.Numenta;
using HTM.Net.Research.NAB.Detectors.RelativeEntropy;
using HTM.Net.Research.NAB.Detectors.Skyline;
using HTM.Net.Util;
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
    internal List<string> ResultsFiles { get; set; }
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
    internal Dictionary<string, Dictionary<string, CostMatrix>> Profiles { get; set; }

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

        Profiles = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, CostMatrix>>>(File.ReadAllText(ProfilesPath));
    }

    /// <summary>
    /// Generate results file given a dictionary of detector classes
    ///
    /// Function that takes a set of detectors and a corpus of data and creates a
    ///    set of files storing the alerts and anomaly scores given by the detectors
    /// </summary>
    /// <param name="detectors">Dictionary with key value pairs of a detector name and its corresponding class constructor.</param>
    public void Detect(Dictionary<Detector, Type> detectors)
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
            }
        }

        // TODO: chain the RX detectors to run after each other, the others can run in parallel
        var parallelArgs = args.Where(a => !(a.detector is IRxDetector)).ToList();
        var parallelTasks = parallelArgs.Select(a => new Task(() => DetectorBase.DetectDataSet(a))).ToArray();
        var serialArgs = args.Where(a => (a.detector is IRxDetector)).ToList();
        var serialTasks = serialArgs.Select(a => new Task(() => DetectorBase.DetectDataSet(a))).ToArray();

        foreach (var task in parallelTasks)
        {
            task.Start();
            task.Wait();
        }

        foreach (var task in serialTasks)
        {
            task.Start();
            task.Wait();
        }

        /*Task.WhenAll(parallelTasks).ContinueWith(t =>
        {
            // Run the serial tasks after the parallel tasks are done
            foreach (var task in serialTasks)
            {
                task.Start();
                task.Wait();
            }
        }).Wait();*/

        //var tasks = args.Select(a => new TaskFactory().StartNew(() => DetectorBase.DetectDataSet(a))).ToArray();
        //Task.WaitAll(tasks);
    }

    public Dictionary<Detector, Map<string, Map<string, double>>> Optimize(List<Detector> detectorNames)
    {
        Console.WriteLine("\nRunning optimize step");

        bool scoreFlag = false;
        var thresholds = new Dictionary<Detector, Map<string, Map<string, double>>>();

        foreach (var detectorName in detectorNames)
        {
            var resultsDetectorDir = Path.Combine(this.ResultsDir, detectorName.ToString());
            var resultsCorpus = (TCorpus)Activator.CreateInstance(typeof(TCorpus), new object[] { resultsDetectorDir });

            thresholds[detectorName] = new Map<string, Map<string, double>>();

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

    public void Score(List<Detector> detectorNames, Dictionary<Detector, Dictionary<string, Dictionary<string, double>>> thresholds)
    {
        Console.WriteLine("\nRunning scoring step");

        bool scoreFlag = true;
        var baselines = new Dictionary<string, object>();

        this.ResultsFiles = new List<string>();
        foreach (var detectorName in detectorNames)
        {
            var resultsDetectorDir = Path.Combine(this.ResultsDir, detectorName.ToString());
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

                resultsDF.ToCsv(scorePath, true);
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
            var dataFrame = DataFrame.LoadCsv(fileName);
            var results = new Scorer.ResultsList(dataFrame);
            baselines[profileName] = results.GetColumn<double>("Score").Last();
            //using (var reader = new StreamReader(fileName))
            //{
            //    var results = Scorer.ResultsList.ReadCsv(reader);
            //    baselines[profileName] = results.GetColumn<double>("Score").Last();
            //}
        }

        var tpCount = 0;
        using (var reader = new StreamReader(this.LabelPath))
        {
            var labelsDict = JsonConvert.DeserializeObject<Dictionary<string, List<object>>>(reader.ReadToEnd());
            tpCount = labelsDict.Values.Select(labels => labels.Count).Sum();
        }

        var finalResults = new Dictionary<Detector, Map<string, Map<string, double>>>();
        foreach (var resultsFile in this.ResultsFiles)
        {
            var profileName = baselines.Keys.FirstOrDefault(k => resultsFile.Contains(k));
            var baseValue = (double)baselines[profileName];

            var dataFrame = DataFrame.LoadCsv(resultsFile);
            var results = new Scorer.ResultsList(dataFrame);

            var detector = Enum.Parse<Detector>(resultsFile.Split(Path.DirectorySeparatorChar).Last().Split('_')[0], true);
            var profile = resultsFile.Split(Path.DirectorySeparatorChar).Last().Split('.')[0]
                .Replace(detector + "_", "", StringComparison.InvariantCultureIgnoreCase).Replace("_scores", "");

            var perfect = tpCount * (double)this.Profiles[profileName]["CostMatrix"].TpWeight;
            var score = 100 * (results.GetColumn<double>("Score").Last() - baseValue) / (perfect - baseValue);

            if (!finalResults.ContainsKey(detector))
            {
                finalResults[detector] = new Map<string, Map<string, double>>();
            }

            if (!finalResults[detector].ContainsKey(profile))
            {
                finalResults[detector][profile] = new Map<string, double>();
            }

            finalResults[detector][profile]["score"] = score;

            Console.WriteLine($"Final score for '{detector}' detector on '{profile}' profile = {score:F2}");
        }

        var resultsPath = Path.Combine(this.ResultsDir, "final_results.json");

        Utils.UpdateFinalResults(finalResults, resultsPath);

        Console.WriteLine($"Final scores have been written to {resultsPath}.");
    }
}

/// <summary>
/// 
/// </summary>
/// <param name="Root">Root folder to look in</param>
/// <param name="DataDir">This holds all the label windows for the corpus.</param>
/// <param name="WindowsFile">JSON file containing ground truth labels for the corpus</param>
/// <param name="ResultsDir">This will hold the results after running detectors on the data</param>
/// <param name="ProfilesFile">The configuration file to use while running the benchmark</param>
/// <param name="ThresholdsFile"></param>
/// <param name="detectors"></param>
public record FileRunnerArguments(string Root, string DataDir = "data",
    string WindowsFile = "labels/combined_windows.json",
    string ResultsDir = "results", string ProfilesFile = "config/profiles.json",
    string ThresholdsFile = "config/thresholds.json")
{
    public List<Detector> Detectors = DefaultDetectors.ToList();

    public static Detector[] DefaultDetectors => new Detector[]
    {
        Detector.Numenta, Detector.NumentaTM, Detector.HtmCore, Detector.HtmNet, Detector.Null, Detector.Random,
        Detector.BayesChangePt, Detector.WindowedGaussian, Detector.Expose,
        Detector.RelativeEntropy, Detector.EarthgeckoSkyline
    };
}

public enum Detector
{
    None,
    Numenta,
    NumentaTM,
    HtmCore,
    HtmNet,
    HtmJava,
    Null,
    Random,
    BayesChangePt,
    WindowedGaussian,
    Expose,
    RelativeEntropy,
    Skyline,
    EarthgeckoSkyline,
    KnnCad,
    ContextOSE,
    RandomCutForest,
    Threshold,
    /// <summary>
    /// Spacial case for the final results
    /// </summary>
    Totals,
    // For unit testing
    BadDetector,
    LuckyDetector,
    DeepThought
}

public class FileRunner
{
    private string _root;
    private int? _numCpUs;
    private readonly string _dataDir;
    private readonly string _windowsFile;
    private readonly string _resultsDir;
    private readonly string _profilesFile;
    private readonly string _thresholdsFile;

    private Runner<Corpus, CorpusLabel> _runner;

    /// <summary>
    /// Creates a filebased runner for the benchmark
    /// </summary>
    /// <param name="arguments"></param>
    /// <param name="detect">Generate detector results but do not analyze results</param>
    /// <param name="optimize">Optimize the thresholds for each detector and user</param>
    /// <param name="score">Analyze results in the results directory</param>
    /// <param name="normalize">Normalize the final scores</param>
    public FileRunner(FileRunnerArguments arguments, bool detect = false, bool optimize = false, bool score = false, bool normalize = false)
    {
        (this._root, this._dataDir, this._windowsFile, this._resultsDir, this._profilesFile, this._thresholdsFile) =
            (arguments.Root, Path.Combine(arguments.Root, arguments.DataDir),
                Path.Combine(arguments.Root, arguments.WindowsFile), Path.Combine(arguments.Root, arguments.ResultsDir),
                Path.Combine(arguments.Root, arguments.ProfilesFile), Path.Combine(arguments.Root, arguments.ThresholdsFile));

        if (!detect && !optimize && !score && !normalize)
        {
            detect = true;
            optimize = true;
            score = true;
            normalize = true;
        }

        _runner = new Runner<Corpus, CorpusLabel>(this._dataDir, this._resultsDir, this._windowsFile, this._profilesFile, this._thresholdsFile);

        Stopwatch stopwatch = Stopwatch.StartNew();
        _runner.Initialize();
        Console.WriteLine($">>>> Initialize: {stopwatch.ElapsedMilliseconds} ms");
        stopwatch.Restart();

        if (detect)
        {
            var detectorConstructors = GetDetectorClassConstructors(arguments.Detectors);
            _runner.Detect(detectorConstructors);
            Console.WriteLine($">>>> Detect: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();
        }

        if (optimize)
        {
            _runner.Optimize(arguments.Detectors);
            Console.WriteLine($">>>> Optimize: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();
        }

        if (score)
        {
            var detectorThresholds = JsonConvert.DeserializeObject<Dictionary<Detector, Dictionary<string, Dictionary<string, double>>>>(File.ReadAllText(this._thresholdsFile));
            _runner.Score(arguments.Detectors, detectorThresholds);
            Console.WriteLine($">>>> Score: {stopwatch.ElapsedMilliseconds} ms");
            stopwatch.Restart();

            if (normalize)
            {
                _runner.Normalize();
                Console.WriteLine($">>>> Normalize: {stopwatch.ElapsedMilliseconds} ms");
            }
        }
        stopwatch.Stop();
    }

    public static Dictionary<Detector, Type> GetDetectorClassConstructors(List<Detector> detectors)
    {
        var detectorConstructors = new Dictionary<Detector, Type>();

        if (detectors.Contains(Detector.BayesChangePt))
        {
            //detectorConstructors["bayesChangePt"] = typeof(BayesChangePtDetector);
        }

        if (detectors.Contains(Detector.Null))
        {
            detectorConstructors[Detector.Null] = typeof(NullDetector);
        }

        if (detectors.Contains(Detector.Random))
        {
            //detectorConstructors["random"] = typeof(RandomDetector);
        }

        if (detectors.Contains(Detector.Skyline))
        {
            detectorConstructors[Detector.Skyline] = typeof(SkylineDetector);
        }

        if (detectors.Contains(Detector.WindowedGaussian))
        {
            //detectorConstructors["windowedGaussian"] = typeof(WindowedGaussianDetector);
        }

        if (detectors.Contains(Detector.KnnCad))
        {
            detectorConstructors[Detector.KnnCad] = typeof(KnncadDetector);
        }

        if (detectors.Contains(Detector.RelativeEntropy))
        {
            detectorConstructors[Detector.RelativeEntropy] = typeof(RelativeEntropyDetector);
        }

        if (detectors.Contains(Detector.Expose))
        {
            detectorConstructors[Detector.Expose] = typeof(ExposeDetector);
        }

        if (detectors.Contains(Detector.ContextOSE))
        {
            ///detectorConstructors["contextOSE"] = typeof(ContextOSEDetector);
        }

        if (detectors.Contains(Detector.EarthgeckoSkyline))
        {
            //detectorConstructors["earthgeckoSkyline"] = typeof(EarthgeckoSkylineDetector);
        }

        if (detectors.Contains(Detector.HtmCore))
        {
            detectorConstructors[Detector.HtmCore] = typeof(HtmcoreDetector);
        }

        if (detectors.Contains(Detector.HtmNet))
        {
            detectorConstructors[Detector.HtmNet] = typeof(HtmNetDetector);
        }

        if (detectors.Contains(Detector.Numenta))
        {
            detectorConstructors[Detector.Numenta] = typeof(NumentaDetector);
        }

        if (detectors.Contains(Detector.NumentaTM))
        {
            detectorConstructors[Detector.NumentaTM] = typeof(NumentaTMDetector);
        }

        if (detectors.Contains(Detector.Threshold))
        {
            //detectorConstructors["threshold"] = typeof(ThresholdDetector);
        }

        return detectorConstructors;
    }
}