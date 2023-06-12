using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace HTM.Net.Research.NAB;

/// <summary>
/// Class used to iterate over all anomaly scores in a data set, generating
/// threshold-score pairs for use in threshold optimization or dataset scoring.
/// </summary>
public class Sweeper
{
    public double ProbationPercent { get; }
    public double TpWeight { get; private set; }
    public double FpWeight { get; private set; }
    public double FnWeight { get; internal set; }

    public Sweeper(double probationPercent = 0.15, Dictionary<string, double> costMatrix = null)
    {
        this.ProbationPercent = probationPercent;
        TpWeight = 0;
        FpWeight = 0;
        FnWeight = 0;

        if (costMatrix != null)
        {
            SetCostMatrix(costMatrix);
        }
    }

    public void SetCostMatrix(Dictionary<string, double> costMatrix)
    {
        TpWeight = costMatrix["tpWeight"];
        FpWeight = costMatrix["fpWeight"];
        FnWeight = costMatrix["fnWeight"];
    }

    internal double GetProbationaryLength(int numRows)
    {
        return Math.Min(Math.Floor(ProbationPercent * numRows), ProbationPercent * 5000.0);
    }

    /// <summary>
    /// Sort by anomaly score and filter all rows with 'probationary' window name
    /// </summary>
    /// <param name="anomalyList"></param>
    /// <returns></returns>
    /// <exception cref="InvalidOperationException"></exception>
    internal List<AnomalyPoint> PrepAnomalyListForScoring(List<AnomalyPoint> anomalyList)
    {
        return anomalyList
            .Where(x => x.WindowName != "probationary")
            .OrderByDescending(x => x.AnomalyScore)
            .ToList();
    }

    internal Dictionary<string, double> PrepareScoreByThresholdParts(List<AnomalyPoint> inputAnomalyList)
    {
        Dictionary<string, double> scoreParts = new Dictionary<string, double>
        {
            { "fp", 0 }
        };
        foreach (var row in inputAnomalyList)
        {
            if (row.WindowName != "probationary" && row.WindowName != null)
            {
                scoreParts[row.WindowName] = -FnWeight;
            }
        }

        return scoreParts;
    }

    private double Sigmoid(double x)
    {
        return 1.0 / (1.0 + Math.Exp(-x));
    }

    private double ScaledSigmoid(double relativePositionInWindow)
    {
        if (relativePositionInWindow > 3.0)
        {
            return -1.0;
        }

        return 2 * Sigmoid(-5 * relativePositionInWindow) - 1.0;
    }

    public List<AnomalyPoint> CalcSweepScore(List<DateTime> timestamps, List<double> anomalyScores, List<(DateTime start, DateTime end)> windowLimits, string dataSetName)
    {
        Debug.Assert(timestamps.Count == anomalyScores.Count, "timestamps and anomalyScores should not be different lengths!");

        // Copy because we mutate this list
        timestamps = new List<DateTime>(timestamps);
        windowLimits = new List<(DateTime start, DateTime end)>(windowLimits);

        // The final list of anomaly points returned from this function.
        // Used for threshold optimization and scoring in other functions.
        List<AnomalyPoint> anomalyList = new List<AnomalyPoint>();

        // One-time config variables
        var maxTP = ScaledSigmoid(-1.0);
        var probationaryLength = GetProbationaryLength(timestamps.Count);

        // iteration variables
        (DateTime start, DateTime end)? curWindowLimits = null;
        string curWindowName = null;
        double? curWindowWidth = null;
        int? curWindowRightIndex = null;
        double? prevWindowWidth = null;
        int? prevWindowRightIndex = null;

        foreach (var (curTime, curAnomaly, i) in timestamps.Zip(anomalyScores).Select((tuple, index) => (tuple.First, tuple.Second, index)))
        {
            double? unweightedScore = null;
            double? weightedScore = null;

            // If not in a window, check if we've just entered one.
            if (windowLimits != null && windowLimits.Any() && curTime == windowLimits[0].start)
            {
                curWindowLimits = windowLimits[0];
                windowLimits.RemoveAt(0);
                curWindowName = $"{dataSetName}|{curWindowLimits.Value.start}";
                curWindowRightIndex = timestamps.IndexOf(curWindowLimits.Value.end);
                curWindowWidth = curWindowRightIndex - timestamps.IndexOf(curWindowLimits.Value.start) + 1.0;

                Console.WriteLine($"Entering window {curWindowName} ({curWindowLimits}");
            }

            // if in a window, score as if true positive
            if (curWindowLimits != null)
            {
                double? positionInWindow = -(curWindowRightIndex - i + 1) / curWindowWidth;
                unweightedScore = ScaledSigmoid(positionInWindow.Value);
                weightedScore = unweightedScore * TpWeight / maxTP;
            }
            else
            {
                // If outside a window, score as if false positive
                if (prevWindowRightIndex == null)
                {
                    // No preceding window, so return score as is we were just really
                    // far away from the nearest window.
                    unweightedScore = -1.0;
                }
                else
                {
                    var numerator = Math.Abs(prevWindowRightIndex.Value - i);
                    var denominator = prevWindowWidth - 1.0;
                    var positionPastWindow = numerator / denominator;
                    unweightedScore = ScaledSigmoid(positionPastWindow.Value);
                }

                weightedScore = unweightedScore * FpWeight;
            }

            string pointInWindowName;
            if (i >= probationaryLength)
            {
                pointInWindowName = curWindowName;
            }
            else
            {
                pointInWindowName = "probationary";
            }

            var point = new AnomalyPoint(curTime, curAnomaly, weightedScore, pointInWindowName);
            anomalyList.Add(point);

            // if at right-edge of window, exit window
            // this happens after processing the current point and appending it to the list
            if (curWindowLimits != null && curTime == curWindowLimits.Value.end)
            {
                Console.WriteLine($"Exiting window {curWindowName} ({curWindowLimits}");
                prevWindowRightIndex = i;
                prevWindowWidth = curWindowWidth;
                curWindowLimits = null;
                curWindowName = null;
                curWindowWidth = null;
                curWindowRightIndex = null;
            }
        }

        return anomalyList;
    }

    /// <summary>
    /// Find NAB scores for each threshold in `anomalyList`.
    /// </summary>
    /// <param name="anomalyList"></param>
    /// <returns></returns>
    public List<ThresholdScore> CalcScoreByThreshold(List<AnomalyPoint> anomalyList)
    {
        var scorableList = PrepAnomalyListForScoring(anomalyList);
        var scoreParts = PrepareScoreByThresholdParts(scorableList);
        var scoresByThreshold = new List<ThresholdScore>();

        // The current threshold above which an anomaly score is considered
        // an anomaly prediction. This starts above 1.0 so that all points
        // are skipped, which gives us a full false-negative score.
        double curThreshold = 1.1;

        // Initialize counts:
        // * every point in a window is a false negative
        // * every point outside a window is a true negative
        var tn = scorableList.Sum(x => string.IsNullOrWhiteSpace(x.WindowName) ? 1.0 : 0);
        var fn = scorableList.Sum(x => !string.IsNullOrWhiteSpace(x.WindowName) ? 1.0 : 0);
        double tp = 0;
        double fp = 0;

        // Iterate through every data point, starting with highest anomaly scores
        // and working down. Whenever we reach a new anomaly score, we save the
        // current score and begin calculating the score for the new, lower
        // threshold. Every data point we iterate over is 'active' for the current
        // threshold level, so the point is either:
        //   * a true positive (has a `windowName`)
        //   * a false positive (`windowName is None`).
        double curScore;
        foreach(var dataPoint in scorableList)
        {
            // If we've reached a new anomaly threshold, store the current
            // threshold+score pair.
            if (dataPoint.AnomalyScore != curThreshold)
            {
                curScore = scoreParts.Sum(kvp => kvp.Value);
                double totalCount = tp + tn + fp + fn;
                var s = new ThresholdScore(curThreshold, curScore, tp, tn, fp, fn, totalCount);
                scoresByThreshold.Add(s);
                curThreshold = dataPoint.AnomalyScore;
            }

            // Adjust counts
            if (dataPoint.WindowName != null)
            {
                tp += 1;
                fn -= 1;
            }
            else
            {
                fp += 1;
                tn -= 1;
            }

            if (dataPoint.WindowName == null)
            {
                scoreParts["fp"] += dataPoint.SweepScore.GetValueOrDefault();
            }
            else
            {
                scoreParts[dataPoint.WindowName] = Math.Max(scoreParts[dataPoint.WindowName], dataPoint.SweepScore.GetValueOrDefault());
            }
        }

        //  Make sure to save the score for the last threshold
        curScore = scoreParts.Sum(kvp => kvp.Value);
        var totalCnt = tp + tn + fp + fn;
        var s1 = new ThresholdScore(curThreshold, curScore, tp, tn, fp, fn, totalCnt);
        scoresByThreshold.Add(s1);

        return scoresByThreshold;
    }

    public (List<double> rowScores, ThresholdScore thresholdScore) ScoreDataSet(
        List<DateTime> timestamps, List<double> anomalyScores, List<(DateTime start, DateTime end)> windowLimits, string dataSetName, double threshold)
    {
        var anomalyList = CalcSweepScore(timestamps, anomalyScores, windowLimits, dataSetName);
        var scoresByThreshold = CalcScoreByThreshold(anomalyList);

        ThresholdScore matchingRow = null;
        ThresholdScore prevRow = null;
        foreach (var thresholdScore in scoresByThreshold)
        {
            if (thresholdScore.Threshold == threshold)
            {
                matchingRow = thresholdScore;
                break;
            }
            else if (thresholdScore.Threshold < threshold)
            {
                matchingRow = prevRow;
                break;
            }

            prevRow = thresholdScore;
        }

        // Return sweepScore for each row, to be added to score file
        return (anomalyList.Select(x => x.SweepScore.Value).ToList(), matchingRow);
    }
}

public record AnomalyPoint(DateTime Timestamp, double AnomalyScore, double? SweepScore, string WindowName);

public record ThresholdScore(double Threshold, double Score, double TP, double TN, double FP, double FN, double TotalCount);