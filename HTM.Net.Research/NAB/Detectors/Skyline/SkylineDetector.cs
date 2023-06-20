using System.Collections.Generic;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

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
    private readonly List<(DateTime timestamp, double value)> _timeseries;
    private readonly List<Func<List<(DateTime timestamp, double value)>, bool, bool>> _algorithms;
    private static object _syncRoot = new object();

    public bool EnableTimings { get; set; } = false;

    public SkylineDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        _timeseries = new List<(DateTime timestamp, double value)>();
        _algorithms = new List<Func<List<(DateTime timestamp, double value)>, bool, bool>>()
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
        int score = 0;
        var inputRow = (timestamp: DateTime.Parse((string)inputData["timestamp"]), value: (double)inputData["value"]);
        _timeseries.Add(inputRow);

        Parallel.ForEach(_algorithms, algo =>
        {
            if (algo(_timeseries, EnableTimings))
            {
                Interlocked.Increment(ref score);
            }
        });

        double averageScore = (double)score / (_algorithms.Count + 1);
        return new List<object> { averageScore };
    }
}