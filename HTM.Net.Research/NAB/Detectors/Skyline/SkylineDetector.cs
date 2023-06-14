using System.Collections.Generic;
using System;

namespace HTM.Net.Research.NAB.Detectors.Skyline;

/// <summary>
/// Detects anomalies using Etsy Skyline's ensemble of algorithms from
/// https://github.com/etsy/skyline/blob/master/src/analyzer/algorithms.py.
/// Each algorithm in the ensemble gives a binary vote for the data record.
/// The original implementation used a majority voting scheme to classify a record
///     as anomalous.Here we improve the detector's performance by using the average
/// of the algorithms' votes as an anomaly score.
/// </summary>
public class SkylineDetector : AnomalyDetector
{
    private List<(DateTime timestamp, double value)> timeseries;
    private List<Func<List<(DateTime timestamp, double value)>, bool>> algorithms;

    public SkylineDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        timeseries = new List<(DateTime timestamp, double value)>();
        algorithms = new List<Func<List<(DateTime timestamp, double value)>, bool>>()
        {
            Algorithms.MedianAbsoluteDeviation,
            Algorithms.FirstHourAverage,
            Algorithms.StdDevFromAverage,
            Algorithms.StdDevFromMovingAverage,
            Algorithms.MeanSubtractionCumulation,
            Algorithms.LeastSquares,
            Algorithms.HistogramBins
        };
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        double score = 0.0;
        var inputRow = (timestamp: (DateTime)inputData["timestamp"], value: (double)inputData["value"]);
        timeseries.Add(inputRow);

        foreach (var algo in algorithms)
        {
            if (algo(timeseries))
                score += 1.0;
        }

        double averageScore = score / (algorithms.Count + 1);
        return new List<object> { averageScore };
    }
}