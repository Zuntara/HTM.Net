using System;
using System.Collections.Generic;
using HTM.Net.Research.NAB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class TruePositiveTests
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
    public void TestFirstTruePositiveWithinWindow()
    {
        /*
        First record within window has a score approximately equal to 
        self.costMatrix["tpWeight"]; within 4 decimal places is more than enough
        precision.
        */
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        int numWindows = 1;
        int windowSize = 2;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);

        double threshold = 0.5;

        // Set a single true positive
        int index = timestamps.IndexOf(windows[0].start);
        anomalyScores[index] = 1.0;

        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        var result = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
        var matchingRow = result.thresholdScore;

        Assert.AreEqual(matchingRow.Score, costMatrix["tpWeight"]);
        CheckCounts(matchingRow, length - windowSize * numWindows, 1, 0, windowSize * numWindows - 1);
    }

    [TestMethod]
    public void TestEarlierTruePositiveIsBetter()
    {
        /*
        If two algorithms both get a true positive within a window, the algorithm
        with the earlier true positive (in the window) should get a higher score.
        */
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        int numWindows = 1;
        int windowSize = 2;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores1 = new List<double>(new double[length]);
        List<double> anomalyScores2 = new List<double>(new double[length]);
        
        double threshold = 0.5;
        DateTime t1 = windows[0].start;
        DateTime t2 = windows[0].end;

        int index1 = timestamps.IndexOf(t1);
        anomalyScores1[index1] = 1;
        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        var result1 = sweeper.ScoreDataSet(timestamps, anomalyScores1, windows, "testData", threshold);
        var matchingRow1 = result1.thresholdScore;

        int index2 = timestamps.FindIndex(t => t == t2);
        anomalyScores2[index2] = 1;
        var result2 = sweeper.ScoreDataSet(timestamps, anomalyScores2, windows, "testData", threshold);
        var matchingRow2 = result2.thresholdScore;

        double score1 = matchingRow1.Score;
        double score2 = matchingRow2.Score;

        Assert.IsTrue(score1 > score2, string.Format("The earlier TP score is not greater than the later TP. They are {0} and {1}, respectively.", score1, score2));
        CheckCounts(matchingRow1, length - windowSize * numWindows, 1, 0, windowSize * numWindows - 1);
        CheckCounts(matchingRow2, length - windowSize * numWindows, 1, 0, windowSize * numWindows - 1);
    }

    [TestMethod]
    public void TestOnlyScoreFirstTruePositiveWithinWindow()
    {
        /*
        An algorithm making multiple detections within a window (i.e. true positive)
        should only be scored for the earliest true positive.
        */
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        int numWindows = 1;
        int windowSize = 2;

        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        var windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);
        List<double> anomalyScores = new List<double>(new double[length]);
        double threshold = 0.5;
        var window = windows[0];
        DateTime t1 = window.start;
        DateTime t2 = window.end;

        // Score with a single true positive at start of window
        int index1 = timestamps.FindIndex(t => t == t1);
        anomalyScores[index1] = 1;
        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        var result1 = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
        var matchingRow1 = result1.thresholdScore;

        // Add a second true positive to end of window
        int index2 = timestamps.FindIndex(t => t == t2);
        anomalyScores[index2] = 1;
        var result2 = sweeper.ScoreDataSet(timestamps, anomalyScores, windows, "testData", threshold);
        var matchingRow2 = result2.thresholdScore;

        Assert.AreEqual(matchingRow1.Score, matchingRow2.Score);
        CheckCounts(matchingRow1, length - windowSize * numWindows, 1, 0, windowSize * numWindows - 1);
        CheckCounts(matchingRow2, length - windowSize * numWindows, 2, 0, windowSize * numWindows - 2);
    }

    [TestMethod]
    public void TestTruePositivesWithDifferentWindowSizes()
    {
        /*
        True positives  at the left edge of windows should have the same score
        regardless of width of window.
        */
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 10;
        int numWindows = 1;
        var timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        double threshold = 0.5;

        int windowSize1 = 2;
        var windowsRaw1 = TestUtils.GenerateWindows(timestamps, numWindows, windowSize1);
        var windows1 = Utils.TimeMap(DateTime.Parse, windowsRaw1);
        int index = timestamps.FindIndex(t => t == windows1[0].Item1);
        List<double> anomalyScores1 = new List<double>();
        for (int i = 0; i < length; i++)
        {
            anomalyScores1.Add(0);
        }
        anomalyScores1[index] = 1;

        int windowSize2 = 3;
        var windowsRaw2 = TestUtils.GenerateWindows(timestamps, numWindows, windowSize2);
        var windows2 = Utils.TimeMap(DateTime.Parse, windowsRaw2);
        index = timestamps.FindIndex(t => t == windows2[0].Item1);
        List<double> anomalyScores2 = new List<double>();
        for (int i = 0; i < length; i++)
        {
            anomalyScores2.Add(0);
        }
        anomalyScores2[index] = 1;

        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        var result1 = sweeper.ScoreDataSet(timestamps, anomalyScores1, windows1, "testData", threshold);
        var matchingRow1 = result1.thresholdScore;

        var result2 = sweeper.ScoreDataSet(timestamps, anomalyScores2, windows2, "testData", threshold);
        var matchingRow2 = result2.thresholdScore;

        Assert.AreEqual(matchingRow1.Score, matchingRow2.Score);
        CheckCounts(matchingRow1, length - windowSize1 * numWindows, 1, 0, windowSize1 * numWindows - 1);
        CheckCounts(matchingRow2, length - windowSize2 * numWindows, 1, 0, windowSize2 * numWindows - 1);
    }

    private void CheckCounts(ThresholdScore scoreRow, int tn, int tp, int fp, int fn)
    {
        Assert.AreEqual(tn, scoreRow.TN, "Incorrect tn count");
        Assert.AreEqual(tp, scoreRow.TP, "Incorrect tp count");
        Assert.AreEqual(fp, scoreRow.FP, "Incorrect fp count");
        Assert.AreEqual(fn, scoreRow.FN, "Incorrect fn count");
    }
}