using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using HTM.Net.Research.NAB;
using HTM.Net.Research.NAB.Detectors;
using HTM.Net.Research.NAB.Detectors.HtmCore;
using HTM.Net.Research.NAB.Detectors.Knncad;
using HTM.Net.Research.NAB.Detectors.Numenta;
using HTM.Net.Research.NAB.Detectors.Skyline;
using HTM.Net.Util;
using log4net;
using log4net.Appender;
using log4net.Core;
using MathNet.Numerics.LinearAlgebra.Double;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using ScottPlot;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class ScorerTest
{
    private Dictionary<string, double> CostMatrix { get; set; }

    private void _checkCounts(ThresholdScore scoreRow, int tn, int tp, int fp, int fn)
    {
        Assert.AreEqual(scoreRow.TN, tn, "Incorrect tn count");
        Assert.AreEqual(scoreRow.TP, tp, "Incorrect tp count");
        Assert.AreEqual(scoreRow.FP, fp, "Incorrect fp count");
        Assert.AreEqual(scoreRow.FN, fn, "Incorrect fn count");
    }

    [TestInitialize]
    public void TestInitialize()
    {
        CostMatrix = new Dictionary<string, double>()
        {
            { "tpWeight", 1.0 },
            { "fnWeight", 1.0 },
            { "fpWeight", 1.0 },
            { "tnWeight", 1.0 }
        };
    }

    [TestMethod]
    public void TestNullCase()
    {
        DateTime start = DateTime.Now;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        List<double> anomalyScores = new List<double>(new double[length]);
        var windows = new List<(DateTime start, DateTime end)>();

        Sweeper sweeper = new Sweeper(0, null);
        var matchingRow = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.AreEqual(0.0, matchingRow.thresholdScore.Score);
        _checkCounts(matchingRow.thresholdScore, 10, 0, 0, 0);
    }

    [TestMethod]
    public void TestFalsePositiveScaling()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 100;
        int numWindows = 1;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);

        // Scale for 10% = windowSize/length
        Dictionary<string, double> costMatrix = new Dictionary<string, double>()
        {
            { "tpWeight", 1.0 },
            { "fnWeight", 1.0 },
            { "fpWeight", 0.11 },
            { "tnWeight", 1.0 }
        };
        Sweeper sweeper = new Sweeper(0, costMatrix);

        List<double> scores = new List<double>();
        for (int i = 0; i < 20; i++)
        {
            List<double> anomalyScores = new List<double>(new double[length]);
            List<int> indices = new List<int>(new int[10]);
            Random random = new Random();
            for (int j = 0; j < 10; j++)
            {
                int index = random.Next(length);
                indices[j] = index;
                anomalyScores[index] = 1;
            }

            var matchingRow = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
            scores.Add(matchingRow.thresholdScore.Score);
        }

        double avgScore = scores.Average();
        Assert.IsTrue(-1.5 <= avgScore && avgScore <= 0.5, "The average score across 20 sets "
                                                           + $"of random detections is {avgScore}, which is not within the acceptable range -1.5 to 0.5.");
    }

    [TestMethod]
    public void TestRewardLowFalseNegatives()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 100;
        int numWindows = 1;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        Dictionary<string, double> costMatrixFN = new Dictionary<string, double>(CostMatrix);
        costMatrixFN["fnWeight"] = 2.0;
        costMatrixFN["fpWeight"] = 0.055;

        Sweeper sweeper1 = new Sweeper(0, CostMatrix);
        Sweeper sweeper2 = new Sweeper(0, costMatrixFN);

        var matchingRow1 = sweeper1.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
        var matchingRow2 = sweeper2.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.AreEqual(0.5 * matchingRow2.thresholdScore.Score, matchingRow1.thresholdScore.Score);
        _checkCounts(matchingRow1.thresholdScore, length - windowSize * numWindows, 0, 0, windowSize * numWindows);
        _checkCounts(matchingRow2.thresholdScore, length - windowSize * numWindows, 0, 0, windowSize * numWindows);
    }

    [TestMethod]
    public void TestRewardLowFalsePositives()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 100;
        int numWindows = 0;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        Dictionary<string, double> costMatrixFP = new Dictionary<string, double>(CostMatrix);
        costMatrixFP["fpWeight"] = 2.0;
        costMatrixFP["fnWeight"] = 0.5;

        Sweeper sweeper1 = new Sweeper(0, CostMatrix);
        Sweeper sweeper2 = new Sweeper(0, costMatrixFP);

        // FP
        anomalyScores[0] = 1;

        var matchingRow1 = sweeper1.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
        var matchingRow2 = sweeper2.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.AreEqual(0.5 * matchingRow2.thresholdScore.Score, matchingRow1.thresholdScore.Score);
        _checkCounts(matchingRow1.thresholdScore, length - windowSize * numWindows - 1, 0, 1, 0);
        _checkCounts(matchingRow2.thresholdScore, length - windowSize * numWindows - 1, 0, 1, 0);
    }

    [TestMethod]
    public void TestScoringAllMetrics()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 100;
        int numWindows = 2;
        int windowSize = 5;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        int index = timestamps.FindIndex(t => windows[0].start == t);

        // TP, add'l TP, and FP
        anomalyScores[index] = 1;
        anomalyScores[index + 1] = 1;
        anomalyScores[index + 7] = 1;

        Sweeper sweeper = new Sweeper(0, CostMatrix);
        var matchingRow = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.AreEqual(-0.9540, matchingRow.thresholdScore.Score, 4);
        _checkCounts(matchingRow.thresholdScore, length - windowSize * numWindows - 1, 2, 1, 8);
    }
}

[TestClass]
public class NYCTaxiTest
{
    private static string _root;
    private static string _corpusSource;

    [ClassInitialize]
    public static void SetUpClass(TestContext testContext)
    {
        int depth = 2;
        _root = Utils.Recur(
            p => Path.GetDirectoryName(p),
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testContext.DeploymentDirectory), testContext.TestDir)),
            depth);
        _corpusSource = Path.Combine(_root, "HTM.Net.Research.Tests", "NAB");
    }

    [TestMethod]
    public void TestDetectorHtmCore()
    {
        bool fast = true;

        string srcPath = "data/realKnownCause/nyc_taxi.csv";
        IDataFile dataSet = new DataFile(Path.Combine(_corpusSource, srcPath));
        if (fast)
            dataSet.Data = dataSet.Data.Take(1000);

        double[] val = dataSet.Data.ColumnData["value"].Select(x => Convert.ToDouble(x.value)).ToArray();
        double[] ts = dataSet.Data.ColumnData["timestamp"].Select(x => DateTime.ParseExact(x.value as string, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture).ToOADate()).ToArray();

        // Ground truth anomaly windows
        List<(DateTime start, DateTime end)> anomalyWindows = new List<(DateTime start, DateTime end)>
        {
            (DateTime.ParseExact("2014-10-30 15:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-03 22:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-11-25 12:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-29 19:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-23 11:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-12-27 18:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-29 21:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-03 04:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2015-01-24 20:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-29 03:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        HtmcoreDetector model = new HtmcoreDetector(dataSet: dataSet, probationaryPercent: 0.15);
        model.Initialize();
        var results = model.Run();
        double[] raw = results["raw_score"].Select(d=>(double)d).ToArray();
        double[] anom = results["anomaly_score"].Select(d => (double)d).ToArray(); ;

        Console.WriteLine();
        Console.WriteLine("Encoder: " + model.EncInfo);
        Console.WriteLine("Spatial Pooler: " + model.SpInfo);
        Console.WriteLine("Temporal Memory: " + model.TmInfo);

        // Plot the results.
        PlotResults(1, ts, val, raw, anom, anomalyWindows);
    }

    [TestMethod]
    public void TestDetectorNumenta()
    {
        bool fast = true;

        string srcPath = "data/realKnownCause/nyc_taxi.csv";
        IDataFile dataSet = new DataFile(Path.Combine(_corpusSource, srcPath));
        dataSet.UpdateColumnType("timestamp", typeof(DateTime), x => DateTime.ParseExact(x, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        dataSet.UpdateColumnType("value", typeof(double), x => double.Parse(x, NumberFormatInfo.InvariantInfo));

        if (fast)
            dataSet.Data = dataSet.Data.Take(1000);

        double[] val = dataSet.Data.ColumnData["value"].Select(x => Convert.ToDouble(x.value)).ToArray();
        double[] ts = dataSet.Data.ColumnData["timestamp"].Select(x => ((DateTime)x.value).ToOADate()).ToArray();

        // Ground truth anomaly windows
        List<(DateTime start, DateTime end)> anomalyWindows = new List<(DateTime start, DateTime end)>
        {
            (DateTime.ParseExact("2014-10-30 15:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-03 22:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-11-25 12:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-29 19:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-23 11:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-12-27 18:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-29 21:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-03 04:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2015-01-24 20:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-29 03:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        var model = new NumentaDetector(dataSet: dataSet, probationaryPercent: 0.15);
        model.AllRecordsProcessed.Subscribe(results =>
        {
            double[] raw = results["raw_score"].Select(d => (double)d).ToArray();
            double[] anom = results["anomaly_score"].Select(d => (double)d).ToArray(); ;

            Console.WriteLine();
            Console.WriteLine("Done");

            // Plot the results.
            PlotResults(2, ts, val, raw, anom, anomalyWindows);
        });
        model.Initialize();
        model.Run();

        model.Wait();
    }

    [TestMethod]
    public void TestDetectorSkyline()
    {
        bool fast = true;

        string srcPath = "data/realKnownCause/nyc_taxi.csv";
        IDataFile dataSet = new DataFile(Path.Combine(_corpusSource, srcPath));
        if (fast == true)
            dataSet.Data = dataSet.Data.Take(1000);

        dataSet.UpdateColumnType("timestamp", typeof(DateTime), x => DateTime.ParseExact(x, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        dataSet.UpdateColumnType("value", typeof(double), x => double.Parse(x, NumberFormatInfo.InvariantInfo));

        double[] val = dataSet.Data.ColumnData["value"].Select(x => Convert.ToDouble(x.value)).ToArray();
        double[] ts = dataSet.Data.ColumnData["timestamp"].Select(x => ((DateTime)x.value).ToOADate()).ToArray();

        // Ground truth anomaly windows
        List<(DateTime start, DateTime end)> anomalyWindows = new List<(DateTime start, DateTime end)>
        {
            (DateTime.ParseExact("2014-10-30 15:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-03 22:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-11-25 12:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-29 19:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-23 11:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-12-27 18:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-29 21:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-03 04:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2015-01-24 20:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-29 03:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        SkylineDetector model = new SkylineDetector(dataSet: dataSet, probationaryPercent: 0.15);
        model.Initialize();
        DataFrame results = model.Run();
        //double[] raw = results["raw_score"].Select(d => (double)d).ToArray();
        double[] anom = results["anomaly_score"].Select(d => (double)d).ToArray(); ;

        Console.WriteLine();

        // Plot the results.
        PlotResults(3, ts, val, null, anom, anomalyWindows);
    }

    [TestMethod]
    public void TestDetectorKnnCad()
    {
        bool fast = true;

        string srcPath = "data/realKnownCause/nyc_taxi.csv";
        IDataFile dataSet = new DataFile(Path.Combine(_corpusSource, srcPath));
        if (fast == true)
            dataSet.Data = dataSet.Data.Take(1000);

        dataSet.UpdateColumnType("timestamp", typeof(DateTime), x => DateTime.ParseExact(x, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        dataSet.UpdateColumnType("value", typeof(double), x => double.Parse(x, NumberFormatInfo.InvariantInfo));

        double[] val = dataSet.Data.ColumnData["value"].Select(x => Convert.ToDouble(x.value)).ToArray();
        double[] ts = dataSet.Data.ColumnData["timestamp"].Select(x => ((DateTime)x.value).ToOADate()).ToArray();

        // Ground truth anomaly windows
        List<(DateTime start, DateTime end)> anomalyWindows = new List<(DateTime start, DateTime end)>
        {
            (DateTime.ParseExact("2014-10-30 15:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-03 22:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-11-25 12:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-29 19:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-23 11:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-12-27 18:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-29 21:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-03 04:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2015-01-24 20:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-29 03:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        KnncadDetector model = new KnncadDetector(dataSet: dataSet, probationaryPercent: 0.10);
        model.Initialize();
        DataFrame results = model.Run();
        //double[] raw = results["raw_score"].Select(d => (double)d).ToArray();
        double[] anom = results["anomaly_score"].Select(d => (double)d).ToArray(); ;

        Console.WriteLine();

        // Plot the results.
        PlotResults(4, ts, val, null, anom, anomalyWindows);
    }

    [TestMethod]
    public void TestDetectorHtmNet()
    {
        bool fast = true;

        var consoleAppender = new ConsoleAppender();
        var hierarchy = (log4net.Repository.Hierarchy.Hierarchy)LogManager.GetRepository();
        hierarchy.Root.AddAppender(consoleAppender);
        hierarchy.Root.Level = Level.Info;
        log4net.Config.BasicConfigurator.Configure(hierarchy);

        string srcPath = "data/realKnownCause/nyc_taxi.csv";
        IDataFile dataSet = new DataFile(Path.Combine(_corpusSource, srcPath));
        dataSet.UpdateColumnType("timestamp", typeof(DateTime), x => DateTime.ParseExact(x, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        dataSet.UpdateColumnType("value", typeof(double), x => double.Parse(x, NumberFormatInfo.InvariantInfo));

        if (fast)
            dataSet.Data = dataSet.Data.Take(1500);

        double[] val = dataSet.Data.ColumnData["value"].Select(x => Convert.ToDouble(x.value)).ToArray();
        double[] ts = dataSet.Data.ColumnData["timestamp"].Select(x => ((DateTime)x.value).ToOADate()).ToArray();

        // Ground truth anomaly windows
        List<(DateTime start, DateTime end)> anomalyWindows = new List<(DateTime start, DateTime end)>
        {
            (DateTime.ParseExact("2014-10-30 15:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-03 22:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-11-25 12:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-11-29 19:00:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-23 11:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2014-12-27 18:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2014-12-29 21:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-03 04:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)),

            (DateTime.ParseExact("2015-01-24 20:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
                DateTime.ParseExact("2015-01-29 03:30:00", "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture))
        };

        var model = new HtmNetDetector(dataSet: dataSet, probationaryPercent: 0.15);
        model.AllRecordsProcessed.Subscribe(results =>
        {
            double[] raw = results["raw_score"].Select(d => (double)d).ToArray();
            double[] anom = results["anomaly_score"].Select(d => (double)d).ToArray(); ;

            Console.WriteLine();
            Console.WriteLine("Done");

            // Plot the results.
            PlotResults(5, ts, val, raw, anom, anomalyWindows);
        });
        model.Initialize();
        model.Run();

        model.Wait();
    }

    private static void PlotResults(int id, double[] ts, double[] val, double[] raw, double[] anom, List<(DateTime start, DateTime end)> anomalyWindows)
    {
        // Create a new ScottPlot plot
        var plt = new Plot(800, 600);

        double max = val.Max();
        val = val.Select(x => x / max).ToArray();

        // Plot the data
        plt.AddSignalXY(ts, val, label: "NYC Taxi (scaled)", color: System.Drawing.Color.Black);
        if (raw != null)
        {
            plt.AddSignalXY(ts, raw, label: "Raw Anomaly", color: System.Drawing.Color.Blue);
        }
        plt.AddSignalXY(ts, anom, label: "Anomaly Likelihood", color: System.Drawing.Color.Red);

        foreach (var window in anomalyWindows)
        {
            // Convert DateTime to double for plotting
            double startX = window.start.ToOADate();
            double endX = window.end.ToOADate();

            if (endX > ts.Max())
            {
                continue;
            }

            // Plot the anomaly windows
            plt.AddFill(new[] { startX }, new[] { endX }, color: System.Drawing.Color.Yellow);
            //plt.PlotFill(startX, endX, fillColor: System.Drawing.Color.Yellow, fillAlpha: 0.5);
        }

        plt.Style(figureBackground: System.Drawing.Color.White);
        plt.XAxis.DateTimeFormat(true);
        plt.Title("NYC Taxi Anomaly Detection with HTM.Core");
        plt.XLabel("Time");
        plt.YLabel("Value");
        plt.Legend();

        // Save the plot as either PNG or JPEG based on the file extension
        string filePath = @$"C:\temp\plot_tm_ny_{id}.png";
        if (filePath.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase))
        {
            plt.SaveFig(filePath);
        }
        else
        {
            plt.SaveFig(filePath);
        }
    }
}