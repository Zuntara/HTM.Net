using HTM.Net.Util;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;

namespace HTM.Net.Research.NAB;

public class Optimizer
{
    /// <summary>
    /// Optimize the threshold for a given combination of detector and profile
    /// </summary>
    /// <param name="detectorName">Name of detector.</param>
    /// <param name="costMatrix">Cost matrix to weight the
    /// true positives, false negatives,
    /// and false positives during
    /// scoring.</param>
    /// <param name="resultsCorpus">Corpus object that holds the per
    /// record anomaly scores for a
    /// given detector.</param>
    /// <param name="corpusLabel">Ground truth anomaly labels for the NAB corpus.</param>
    /// <param name="probationaryPercent">Percent of each data file not to be considered during scoring.</param>
    /// <returns>
    /// (dict) Contains:
    ///  "threshold" (double) Threshold that returns the largest score from the
    ///  Objective function.
    ///  
    ///  "score"     (double) The score from the objective function given the
    ///  threshold.
    /// </returns>
    public static Map<string, double> OptimizeThreshold(
        Detector detectorName,
        CostMatrix costMatrix,
        ICorpus resultsCorpus,
        ICorpusLabel corpusLabel,
        double probationaryPercent)
    {
        var sweeper = new Sweeper(probationPercent: probationaryPercent, costMatrix: costMatrix);

        // First, get the sweep-scores for each row in each data set
        List<AnomalyPoint> allAnomalyScores = new List<AnomalyPoint>();
        foreach (var x in resultsCorpus.DataFiles)
        {
            string root = Utils.Recur(Path.GetDirectoryName, x.Value.SrcPath, 2);
            string relativePath =  Path.GetRelativePath(root, x.Value.SrcPath);
            var dataSet = x.Value;
            if (relativePath.EndsWith("_scores.csv"))
            {
                continue;
            }

            // relativePath: raw dataset file,
            // e.g. 'artificialNoAnomaly/art_noisy.csv'
            relativePath = Utils.convertResultsPathToDataPath(Path.Join(detectorName.ToString(), relativePath));

            List<(DateTime start, DateTime end)> windows = null;
            DataFrame labels = null;
            try
            {
                windows = corpusLabel.Windows[relativePath];
                labels = corpusLabel.Labels[relativePath];
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }

            var timestamps = labels["timestamp"].Select(r => DateTime.Parse((string)r)).ToList();
            var anomalyScores = dataSet.Data["anomaly_score"].Select(r => double.Parse((string)r, NumberFormatInfo.InvariantInfo)).ToList();
            timestamps = timestamps.GetRange(0, anomalyScores.Count);

            var curAnomalyRows = sweeper.CalcSweepScore(timestamps, anomalyScores, windows, relativePath);
            allAnomalyScores.AddRange(curAnomalyRows);
        }

        // Get score by threshold for the entire corpus
        var scoresByThreshold = sweeper.CalcScoreByThreshold(allAnomalyScores);
        scoresByThreshold = scoresByThreshold.OrderByDescending(x => x.Score).ToList();
        var bestParams = scoresByThreshold.First();

        Console.WriteLine(
            $"({detectorName}) Optimizer found a max score of {bestParams.Score} with anomaly threshold {bestParams.Threshold}");

        return new Map<string, double>
        {
            { "threshold", bestParams.Threshold },
            { "score", bestParams.Score }
        };
    }
}