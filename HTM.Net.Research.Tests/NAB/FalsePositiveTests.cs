using System;
using System.Collections.Generic;
using HTM.Net.Research.NAB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class FalsePositiveTests
{
    private Dictionary<string, double> costMatrix;

    [TestInitialize]
    public void SetUp()
    {
        costMatrix = new Dictionary<string, double>()
        {
            { "tpWeight", 1.0 },
            { "fnWeight", 1.0 },
            { "fpWeight", 1.0 },
            { "tnWeight", 1.0 }
        };
    }

    [TestMethod]
    public void TestFalsePositiveMeansNegativeScore()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 1000;
        int numWindows = 1;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        anomalyScores[0] = 1;
        Sweeper sweeper = new Sweeper(0, costMatrix);
        var matchingRow = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.IsTrue(matchingRow.thresholdScore.Score < 0);
        _checkCounts(matchingRow.thresholdScore, length - windowSize * numWindows - 1, 0, 1, windowSize * numWindows);
    }

    [TestMethod]
    public void TestTwoFalsePositivesIsWorseThanOne()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 1000;
        int numWindows = 1;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        anomalyScores[0] = 1;
        Sweeper sweeper = new Sweeper(0, costMatrix);
        var matchingRow1 = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        anomalyScores[1] = 1;
        sweeper = new Sweeper(0, costMatrix);
        var matchingRow2 = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.IsTrue(matchingRow2.thresholdScore.Score < matchingRow1.thresholdScore.Score);
        _checkCounts(matchingRow1.thresholdScore, length - windowSize * numWindows - 1, 0, 1, windowSize * numWindows);
        _checkCounts(matchingRow2.thresholdScore, length - windowSize * numWindows - 2, 0, 2, windowSize * numWindows);
    }

    [TestMethod]
    public void TestOneFalsePositiveNoWindow()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 1000;
        int numWindows = 0;
        int windowSize = 10;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        anomalyScores[0] = 1;
        Sweeper sweeper = new Sweeper(0, costMatrix);
        var matchingRow = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);

        Assert.AreEqual(-costMatrix["fpWeight"], matchingRow.thresholdScore.Score);
        _checkCounts(matchingRow.thresholdScore, length - windowSize * numWindows - 1, 0, 1, windowSize * numWindows);
    }

    /// <summary>
    /// For two false positives A and B, where A occurs earlier than B, the
    /// score change due to A will be less than the score change due to B.
    /// </summary>
    [TestMethod]
    public void TestEarlierFalsePositiveAfterWindowIsBetter()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        int numWindows = 1;
        int windowSize = 2;
        double threshold = 0.5;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);

        List<double> anomalyScores1 = new List<double>(new double[length]);
        List<double> anomalyScores2 = new List<double>(new double[length]);

        DateTime t1 = windows[0].start;
        DateTime t2 = windows[0].end;

        int index1 = timestamps.IndexOf(t2) + 1;
        anomalyScores1[index1] = 1;
        Sweeper sweeper = new Sweeper(0, costMatrix);
        var matchingRow1 = sweeper.ScoreDataSet(timestamps, anomalyScores1, windows, "testData", threshold);

        anomalyScores2[index1 + 1] = 1;
        sweeper = new Sweeper(0, costMatrix);
        var matchingRow2 = sweeper.ScoreDataSet(timestamps, anomalyScores2, windows, "testData", threshold);

        Assert.IsTrue(matchingRow1.thresholdScore.Score > matchingRow2.thresholdScore.Score);
        _checkCounts(matchingRow1.thresholdScore, length - windowSize * numWindows - 1, 0, 1, windowSize * numWindows);
        _checkCounts(matchingRow2.thresholdScore, length - windowSize * numWindows - 1, 0, 1, windowSize * numWindows);
    }

    private void _checkCounts(ThresholdScore scoreRow, int tn, int tp, int fp, int fn)
    {
        Assert.AreEqual(tn, scoreRow.TN, "Incorrect tn count");
        Assert.AreEqual(tp, scoreRow.TP, "Incorrect tp count");
        Assert.AreEqual(fp, scoreRow.FP, "Incorrect fp count");
        Assert.AreEqual(fn, scoreRow.FN, "Incorrect fn count");
    }
}