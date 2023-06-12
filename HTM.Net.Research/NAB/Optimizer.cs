using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Research.NAB;

public class Optimizer
{
    public static Dictionary<string, double> OptimizeThreshold(
        string detectorName,
        Dictionary<string, double> costMatrix,
        ICorpus resultsCorpus,
        ICorpusLabel corpusLabel,
        double probationaryPercent)
    {
        var sweeper = new Sweeper(probationPercent: probationaryPercent, costMatrix: costMatrix);

        // First, get the sweep-scores for each row in each data set
        List<AnomalyPoint> allAnomalyScores = new List<AnomalyPoint>();
        foreach (var x in resultsCorpus.DataFiles)
        {
            string relativePath = x.Key;
            var dataSet = x.Value;
            if (relativePath == "_scores.csv")
            {
                continue;
            }

            // relativePath: raw dataset file,
            // e.g. 'artificialNoAnomaly/art_noisy.csv'
            relativePath = Utils.convertResultsPathToDataPath(relativePath);

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

            var timestamps = labels["timestamp"].Select(r => (DateTime)r).ToList();
            var anomalyScores = labels["anomaly_score"].Select(r => (double)r).ToList();

            var curAnomalyRows = sweeper.CalcSweepScore(timestamps, anomalyScores, windows, relativePath);
            allAnomalyScores.AddRange(curAnomalyRows);
        }

        // Get score by threshold for the entire corpus
        var scoresByThreshold = sweeper.CalcScoreByThreshold(allAnomalyScores);
        scoresByThreshold = scoresByThreshold.OrderByDescending(x => x.Score).ToList();
        var bestParams = scoresByThreshold.First();

        Console.WriteLine(
            $"Optimizer found a max score of {bestParams.Score} with anomaly threshold {bestParams.Threshold}");

        return new Dictionary<string, double>
        {
            { "threshold", bestParams.Threshold },
            { "score", bestParams.Score }
        };
    }
}