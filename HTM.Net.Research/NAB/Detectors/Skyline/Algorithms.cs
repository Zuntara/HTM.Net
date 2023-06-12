using MathNet.Numerics;
using System.Collections.Generic;
using System;
using System.Linq;
using MathNet.Numerics.Statistics;
using System.Diagnostics.Metrics;
using MathNet.Numerics.LinearRegression;

namespace HTM.Net.Research.NAB.Detectors.Skyline;

public static class Algorithms
{
    public static double TailAvg(List<(DateTime timestamp, double value)> timeseries)
    {
        try
        {
            double t = (timeseries[^1].value + timeseries[^2].value + timeseries[^3].value) / 3;
            return t;
        }
        catch (IndexOutOfRangeException)
        {
            return timeseries[^1].value;
        }
    }

    public static bool MedianAbsoluteDeviation(List<(DateTime timestamp, double value)> timeseries)
    {
        var series = timeseries.Select(x => x.value).ToList();
        var median = series.Median();
        var demedianed = series.Select(x => Math.Abs(x - median)).ToList();
        var medianDeviation = demedianed.Median();

        if (medianDeviation == 0)
            return false;

        var testStatistic = demedianed.Last() / medianDeviation;

        return testStatistic > 6;
    }

    public static bool FirstHourAverage(List<(DateTime timestamp, double value)> timeseries)
    {
        var day = TimeSpan.FromDays(1);
        var hour = TimeSpan.FromHours(1);
        var lastHourThreshold = timeseries.Last().timestamp - (day - hour);
        var startTime = lastHourThreshold - hour;
        var series = timeseries
            .Where(x => x.timestamp >= startTime && x.timestamp < lastHourThreshold)
            .Select(x => x.value)
            .ToList();
        var mean = series.Average();
        var stdDev = series.StandardDeviation();
        var t = TailAvg(timeseries);

        return Math.Abs(t - mean) > 3 * stdDev;
    }

    public static bool StdDevFromAverage(List<(DateTime timestamp, double value)> timeseries)
    {
        var series = timeseries.Select(x => x.value).ToList();
        var mean = series.Average();
        var stdDev = series.StandardDeviation();
        var t = TailAvg(timeseries);

        return Math.Abs(t - mean) > 3 * stdDev;
    }

    public static bool StdDevFromMovingAverage(List<(DateTime timestamp, double value)> timeseries)
    {
        var series = timeseries.Select(x => x.value).ToList();
        var expAverage = series.ExponentialMovingAverage(50);
        var stdDev = series.ExponentialMovingStdDev(50);

        return Math.Abs(series.Last() - expAverage.Last()) > 3 * stdDev.Last();
    }

    public static bool MeanSubtractionCumulation(List<(DateTime timestamp, double value)> timeseries)
    {
        var series = timeseries.Select(x => x.value).ToList();
        series = series.Select((x, i) => x - series.Take(i).Average()).ToList();
        var stdDev = series.Take(series.Count - 1).StandardDeviation();

        return Math.Abs(series.Last()) > 3 * stdDev;
    }

    public static bool LeastSquares2(List<Tuple<DateTime, double>> timeseries)
    {
        double[] x = timeseries.Select(t => (t.Item1 - new DateTime(1970, 1, 1)).TotalSeconds).ToArray();
        double[] y = timeseries.Select(t => t.Item2).ToArray();
        double[][] a = new double[x.Length][];
        for (int i = 0; i < x.Length; i++)
        {
            a[i] = new double[] { x[i], 1 };
        }

        var results = MultipleRegression.NormalEquations(a, y);
        double[] coefficients = results;
        double[] errors = new double[y.Length];
        for (int i = 0; i < y.Length; i++)
        {
            double projected = coefficients[0] * x[i] + coefficients[1];
            errors[i] = y[i] - projected;
        }

        if (errors.Length < 3)
            return false;

        double stdDev = Statistics.StandardDeviation(errors);
        double t = (errors[^1] + errors[^2] + errors[^3]) / 3;

        return Math.Abs(t) > stdDev * 3 && Math.Round(stdDev) != 0 && Math.Round(t) != 0;
    }

    public static bool LeastSquares(List<(DateTime timestamp, double value)> timeseries)
    {
        var x = timeseries.Select(t => (t.timestamp - new DateTime(1970, 1, 1)).TotalSeconds).ToArray();
        var y = timeseries.Select(t => t.value).ToArray();
        var A = x.Select((xi, i) => new[] { xi, 1.0 }).ToArray();
        var results = MathNet.Numerics.LinearRegression.SimpleRegression.Fit(x, y);
        //var residual = results.value;
        var m = results.A;
        var c = results.B;
        var errors = y.Select((value, i) =>
        {
            var projected = m * x[i] + c;
            return value - projected;
        }).ToArray();

        if (errors.Length < 3)
            return false;

        var stdDev = errors.StandardDeviation();
        var t = (errors[^1] + errors[^2] + errors[^3]) / 3;

        return Math.Abs(t) > stdDev * 3 && Math.Round(stdDev) != 0 && Math.Round(t) != 0;
    }

    public static bool HistogramBins(List<(DateTime timestamp, double value)> timeseries)
    {
        var series = timeseries.Select(x => x.value).ToArray();
        var t = TailAvg(timeseries);
        var h = Histogram(series, 15);// Util.Histogram(series, 15);
        var bins = h.binEdges;

        for (int i = 0; i < h.histogram.Length; i++)
        {
            int binSize = h.histogram[i];
            if (binSize <= 20)
            {
                if (i == 0)
                {
                    if (t <= bins[0])
                        return true;
                }
                else if (t >= bins[i] && t < bins[i + 1])
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static (int[] histogram, double[] binEdges) Histogram(double[] data, int bins)
    {
        var histogram = new int[bins];
        var binEdges = new double[bins + 1];

        double minValue = data.Min();
        double maxValue = data.Max();
        double binWidth = (maxValue - minValue) / bins;

        for (int i = 0; i <= bins; i++)
        {
            binEdges[i] = minValue + i * binWidth;
        }

        foreach (var value in data)
        {
            int binIndex = bins;
            for (int i = 0; i < bins; i++)
            {
                if (value < binEdges[i + 1])
                {
                    binIndex = i;
                    break;
                }
            }
            histogram[binIndex]++;
        }

        return (histogram, binEdges);
    }

    private static List<double> ExponentialMovingAverage(this IEnumerable<double> source, int window)
    {
        var result = new List<double>();
        var alpha = 2.0 / (window + 1);
        var enumerator = source.GetEnumerator();
        bool hasMoreData = enumerator.MoveNext();
        double currentAvg = enumerator.Current;

        while (hasMoreData)
        {
            result.Add(currentAvg);
            hasMoreData = enumerator.MoveNext();
            if (hasMoreData)
                currentAvg = alpha * enumerator.Current + (1 - alpha) * currentAvg;
        }

        return result;
    }

    private static List<double> ExponentialMovingStdDev(this IEnumerable<double> source, int window)
    {
        var result = new List<double>();
        var alpha = 2.0 / (window + 1);
        var enumerator = source.GetEnumerator();
        bool hasMoreData = enumerator.MoveNext();
        double currentAvg = enumerator.Current;
        double currentSquaredAvg = enumerator.Current * enumerator.Current;

        while (hasMoreData)
        {
            var avgSquared = currentAvg * currentAvg;
            var variance = currentSquaredAvg - avgSquared;
            var stdDev = Math.Sqrt(Math.Max(variance, 0));
            result.Add(stdDev);
            hasMoreData = enumerator.MoveNext();
            if (hasMoreData)
            {
                var currentValue = enumerator.Current;
                currentAvg = alpha * currentValue + (1 - alpha) * currentAvg;
                currentSquaredAvg = alpha * (currentValue * currentValue) + (1 - alpha) * currentSquaredAvg;
            }
        }

        return result;
    }
}