using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.NAB;
using MathNet.Numerics;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static System.Formats.Asn1.AsnWriter;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class FalseNegativeTests
{
    private void CheckCounts(ThresholdScore scoreRow, int tn, int tp, int fp, int fn)
    {
        Assert.AreEqual(tn, scoreRow.TN, "Incorrect tn count");
        Assert.AreEqual(tp, scoreRow.TP, "Incorrect tp count");
        Assert.AreEqual(fp, scoreRow.FP, "Incorrect fp count");
        Assert.AreEqual(fn, scoreRow.FN, "Incorrect fn count");
    }

    private Dictionary<string, double> costMatrix;

    [TestInitialize]
    public void SetUp()
    {
        costMatrix = new Dictionary<string, double>
        {
            { "tpWeight", 1.0 },
            { "fnWeight", 1.0 },
            { "fpWeight", 1.0 },
            { "tnWeight", 1.0 }
        };
    }

    [TestMethod]
    public void TestFalseNegativeCausesNegativeScore()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 1000;
        int numWindows = 1;
        int windowSize = 10;

        List<DateTime> timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        List<List<DateTime>> windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);

        List<double> anomalyScores = Enumerable.Repeat(0.0, length).ToList();
        double threshold = 1.0;

        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        (List<double> scores, ThresholdScore matchingRow) = sweeper.ScoreDataSet(
            timestamps,
            anomalyScores,
            windows,
            "testData",
            threshold
        );

        Assert.AreEqual(-costMatrix["fnWeight"], matchingRow.Score);
        CheckCounts(matchingRow, length - windowSize * numWindows, 0, 0, windowSize * numWindows);
    }

    [TestMethod]
    public void TestFourFalseNegatives()
    {
        DateTime start = DateTime.Now.Date;
        TimeSpan increment = TimeSpan.FromMinutes(5);
        int length = 2000;
        int numWindows = 4;
        int windowSize = 10;

        List<DateTime> timestamps = TestUtils.GenerateTimestamps(start, increment, length);
        List<List<DateTime>> windowsRaw = TestUtils.GenerateWindows(timestamps, numWindows, windowSize);
        var windows = Utils.TimeMap(DateTime.Parse, windowsRaw);

        List<double> anomalyScores = Enumerable.Repeat(0.0, length).ToList();
        double threshold = 1.0;

        Sweeper sweeper = new Sweeper(probationPercent: 0, costMatrix: costMatrix);
        (List<double> scores, ThresholdScore matchingRow) = sweeper.ScoreDataSet(
            timestamps,
            anomalyScores,
            windows,
            "testData",
            threshold
        );

        Assert.AreEqual(4 * -costMatrix["fnWeight"], matchingRow.Score);
        CheckCounts(matchingRow, length - windowSize * numWindows, 0, 0, windowSize * numWindows);
    }
}