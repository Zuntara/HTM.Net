using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.NAB;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static log4net.Appender.RollingFileAppender;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class TestSweeper
{
    [TestMethod]
    public void TestOptimizerInit()
    {
        Sweeper o = new Sweeper();
        Assert.IsNotNull(o.ProbationPercent);

        o = new Sweeper(probationPercent: 0.30);
        Assert.AreEqual(0.30, o.ProbationPercent, "ProbationPercent wrong");

        o = new Sweeper(costMatrix: new Dictionary<string, double> { { "tpWeight", 0 }, { "fpWeight", 1 }, { "fnWeight", 2 } });
        Assert.AreEqual(0f, o.TpWeight);
        Assert.AreEqual(1f, o.FpWeight);
        Assert.AreEqual(2f, o.FnWeight);
    }

    [DataTestMethod]
    [DataRow(100, 0.0, 0)]  // 0% probationary --> length 0
    [DataRow(100, 1.0, 100)]  // 100% --> length 100
    [DataRow(100, 0.1, 10)]
    [DataRow(100, 0.15, 15)]
    [DataRow(5000, 0.1, 500)]
    [DataRow(6000, 0.1, 500)]  // Cap at 5000 works as expected
    public void TestGetProbationaryLength(int numRows, double probationaryPercent, int expectedLength)
    {
        Sweeper o = new Sweeper(probationPercent: probationaryPercent);
        double actualLength = (int) o.GetProbationaryLength(numRows);
        Assert.AreEqual(expectedLength, actualLength);
    }

    [TestMethod]
    public void TestSetCostMatrix()
    {
        Sweeper o = new Sweeper();
        Assert.AreEqual(0, o.TpWeight);
        Assert.AreEqual(0, o.FpWeight);
        Assert.AreEqual(0, o.FnWeight);

        // These are all arbitrary.
        float expectedTP = 2.0f;
        float expectedFN = 3.0f;
        float expectedFP = 4.0f;

        Dictionary<string, double> costMatrix = new Dictionary<string, double>
        {
            { "tpWeight", expectedTP },
            { "fnWeight", expectedFN },
            { "fpWeight", expectedFP }
        };

        o.SetCostMatrix(costMatrix);

        Assert.AreEqual(expectedTP, o.TpWeight);
        Assert.AreEqual(expectedFN, o.FnWeight);
        Assert.AreEqual(expectedFP, o.FpWeight);
    }

    [TestMethod]
    public void TestCalcSweepScoreWindowScoreInteraction()
    {
        int numRows = 100;
        var fakeAnomalyScores = Enumerable.Repeat(1.0, numRows).ToList();
        var fakeTimestamps = Enumerable.Range(0, numRows).Select(n => DateTime.Now.Date.AddDays(n)).ToList();  // We'll use numbers, even though real data uses dates
        string fakeName = "TestDataSet";

        var windowA = (DateTime.Now.Date.AddDays(30), DateTime.Now.Date.AddDays(39));
        var windowB = (DateTime.Now.Date.AddDays(75), DateTime.Now.Date.AddDays(95));
        var windowLimits = new List<(DateTime, DateTime)> { windowA, windowB };
        var expectedInWindowCount = ((windowA.Item2 - windowA.Item1).Add(TimeSpan.FromDays(1) + (windowB.Item2 - windowB.Item1).Add(TimeSpan.FromDays(1)))).TotalDays;

        // Standard profile
        Dictionary<string, double> costMatrix = new Dictionary<string, double>
        {
            { "tpWeight", 1.0 },
            { "fnWeight", 1.0 },
            { "fpWeight", 0.11 }
        };
        float probationPercent = 0.1f;
        Sweeper o = new Sweeper(probationPercent: probationPercent, costMatrix: costMatrix);
        List<AnomalyPoint> scoredAnomalies = o.CalcSweepScore(fakeTimestamps, fakeAnomalyScores, windowLimits, fakeName);

        // Check that correct number of AnomalyPoints returned
        Assert.AreEqual(numRows, scoredAnomalies.Count);
        Assert.IsTrue(scoredAnomalies.All(x => x is AnomalyPoint));

        // Expected number of points marked 'probationary'
        var probationary = scoredAnomalies.Where(x => x.WindowName == "probationary").ToList();
        Assert.AreEqual(o.GetProbationaryLength(numRows), probationary.Count);

        // Expected number of points marked 'in window'
        var inWindow = scoredAnomalies.Where(x => x.WindowName != "probationary" && x.WindowName != null).ToList();
        Assert.AreEqual((int)expectedInWindowCount, inWindow.Count);

        // Points in window have positive score; others have negative score
        foreach (var point in scoredAnomalies)
        {
            if (point.WindowName != "probationary" && point.WindowName != null)
            {
                Assert.IsTrue(point.SweepScore > 0);
            }
            else
            {
                Assert.IsTrue(point.SweepScore < 0);
            }
        }
    }

    [TestMethod]
    public void TestPrepAnomalyListForScoring()
    {
        List<AnomalyPoint> fakeInput = new List<AnomalyPoint>
        {
            new AnomalyPoint(DateTime.Now.Date.AddDays(0), 0.5, 0, "probationary"),  // filter because 'probationary'
            new AnomalyPoint(DateTime.Now.Date.AddDays(1), 0.5, 0, "probationary"),  // filter because 'probationary'
            new AnomalyPoint(DateTime.Now.Date.AddDays(2), 0.0, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(3), 0.1, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(4), 0.2, 0, "windowA"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(5), 0.5, 0, "windowB"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(6), 0.5, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(7), 0.0, 0, null),
        };

        // Expected: sorted by anomaly score descending, with probationary rows filtered out.
        List<AnomalyPoint> expectedList = new List<AnomalyPoint>
        {
            new AnomalyPoint(DateTime.Now.Date.AddDays(5), 0.5, 0, "windowB"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(6), 0.5, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(4), 0.2, 0, "windowA"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(3), 0.1, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(2), 0.0, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(7), 0.0, 0, null),
        };

        Sweeper o = new Sweeper();
        List<AnomalyPoint> sortedList = o.PrepAnomalyListForScoring(fakeInput);
        CollectionAssert.AreEqual(expectedList, sortedList);
    }

    [TestMethod]
    public void TestPrepareScoreParts()
    {
        List<AnomalyPoint> fakeInput = new List<AnomalyPoint>
        {
            new AnomalyPoint(DateTime.Now.Date.AddDays(0), 0.5, 0, "probationary"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(1), 0.5, 0, "probationary"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(2), 0.0, 0, null),
            new AnomalyPoint(DateTime.Now.Date.AddDays(4), 0.2, 0, "windowA"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(5), 0.2, 0, "windowA"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(6), 0.5, 0, "windowB"),
            new AnomalyPoint(DateTime.Now.Date.AddDays(7), 0.5, 0, null),
        };

        double fakeFNWeight = 33.0;
        Sweeper o = new Sweeper();
        o.FnWeight = fakeFNWeight;

        // Expect one entry for all false positives and one entry per unique window name,
        // initialized to a starting score of `-self.fnWeight`
        Dictionary<string, double> expectedOutput = new Dictionary<string, double>
        {
            { "fp", 0 },
            { "windowA", -fakeFNWeight },
            { "windowB", -fakeFNWeight },
        };

        Dictionary<string, double> actualScoreParts = o.PrepareScoreByThresholdParts(fakeInput);
        CollectionAssert.AreEqual(expectedOutput, actualScoreParts);
    }

    [TestMethod]
    public void TestCalcScoreByThresholdReturnsExpectedScores()
    {
        double fnWeight = 5.0;
        Sweeper o = new Sweeper();
        o.FnWeight = fnWeight;

        List<AnomalyPoint> fakeInput = new List<AnomalyPoint>
        {
            new AnomalyPoint(DateTime.Now.Date.AddDays(0), 0.5, -1000, "probationary"),  // Should never contribute to score (probationary)
            new AnomalyPoint(DateTime.Now.Date.AddDays(1), 0.5, -1000, "probationary"),  // Should never contribute to score (probationary)
            new AnomalyPoint(DateTime.Now.Date.AddDays(2), 0.0, -3, null),  // Should never contribute to score (anomaly == 0.0)
            new AnomalyPoint(DateTime.Now.Date.AddDays(4), 0.2, 20, "windowA"),  // Should be used instead of next row when threshold <= 0.2
            new AnomalyPoint(DateTime.Now.Date.AddDays(5), 0.3, 10, "windowA"),  // Should be used for winowA _until_ threshold <= 0.2
            new AnomalyPoint(DateTime.Now.Date.AddDays(6), 0.5, 5, "windowB"),  // Only score for windowB, but won't be used until threshold <= 0.5
            new AnomalyPoint(DateTime.Now.Date.AddDays(7), 0.5, -3, null),
        };

        List<ThresholdScore> expectedScoresByThreshold = new List<ThresholdScore>
        {
            new ThresholdScore(1.1, -2 * fnWeight, 0, 2, 0, 3, 5),  // two windows, both false negatives at this threshold
            new ThresholdScore(0.5, 5 - 3 - fnWeight, 1, 1, 1, 2, 5),  // Both 'anomalyScore == 0.5' score, windowA is still FN
            new ThresholdScore(0.3, 5 - 3 + 10, 2, 1, 1, 1, 5),  // Both windows now have a TP
            new ThresholdScore(0.2, 5 - 3 + 20, 3, 1, 1, 0, 5),  // windowA gets a new max value due to row 4 becoming active
            new ThresholdScore(0.0, 5 - 3 + 20 - 3, 3, 0, 2, 0, 5),  // Both windows now FN, windowA isn't counted
        };

        Console.WriteLine("expected");
        foreach (var score in expectedScoresByThreshold)
        {
            Console.WriteLine(score);
        }

        List<ThresholdScore> actualScoresByThreshold = o.CalcScoreByThreshold(fakeInput);
        Console.WriteLine("actuals");
        foreach (var score in actualScoresByThreshold)
        {
            Console.WriteLine(score);
        }

        CollectionAssert.AreEqual(expectedScoresByThreshold, actualScoresByThreshold);
    }
}