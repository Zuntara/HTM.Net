using System.Collections.Generic;
using System;
using System.Diagnostics;
using System.Linq;
using MathNet.Numerics.Statistics;
using MathNet.Numerics.LinearRegression;

namespace HTM.Net.Research.NAB.Detectors.Skyline;

public static class Algorithms
{
    private static Stopwatch sw = new Stopwatch();

    private static T RecordTiming<T>(Func<T> method, bool enable, bool indent = false)
    {
        if (!enable)
        {
            return method();
        }

        sw.Restart();
        T result = method();
        sw.Stop();
        string methodName = new StackFrame(1).GetMethod()?.Name;
        Console.WriteLine($"{(indent ? "\t" : "")}[{methodName}] Time elapsed: {sw.Elapsed.TotalMilliseconds} ms");
        return result;
    }

    private static double TailAvg(List<(DateTime timestamp, double value)> timeseries, bool enable, bool indent)
    {
        return RecordTiming(() =>
        {
            try
            {
                double t = (timeseries[^1].value + timeseries[^2].value + timeseries[^3].value) / 3;
                return t;
            }
            catch (ArgumentOutOfRangeException)
            {
                if (timeseries.Count == 0)
                {
                    return 0;
                }

                return timeseries[^1].value;
            }
        }, enable, indent);
    }

    public static bool MedianAbsoluteDeviation(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            double median = 0;
            double medianDeviation = 0;
            double testStatistic = 0;
            int count = 0;

            foreach (var (timestamp, value) in timeseries)
            {
                count++;
                double delta = value - median;
                median += delta / count;
                medianDeviation += Math.Abs(delta) * (count - 1) / count;
                testStatistic = Math.Abs(value - median) / medianDeviation;

                if (count == timeseries.Count)
                    break;
            }

            if (medianDeviation == 0)
                return false;

            return testStatistic > 6;
        }, enable);
    }

    public static bool MedianAbsoluteDeviationOld(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var series = timeseries.Select(x => x.value).ToList();
            var median = series.Median();
            var demedianed = series.Select(x => Math.Abs(x - median)).ToList();
            var medianDeviation = demedianed.Median();

            if (medianDeviation == 0)
                return false;

            var testStatistic = demedianed.Last() / medianDeviation;

            return testStatistic > 6;
        }, enable);
    }

    public static bool FirstHourAverage(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var day = TimeSpan.FromDays(1);
            var hour = TimeSpan.FromHours(1);
            var lastHourThreshold = timeseries.Last().timestamp - (day - hour);
            var startTime = lastHourThreshold - hour;
            var series = timeseries
                .Where(x => x.timestamp >= startTime && x.timestamp < lastHourThreshold)
                .Select(x => x.value)
                .ToList();
            var mean = series.Any() ? series.Average() : 0.0;
            var stdDev = series.StandardDeviation();
            var t = TailAvg(timeseries, enable, true);

            return Math.Abs(t - mean) > 3 * stdDev;
        }, enable);
    }

    public static bool StdDevFromAverage(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var series = timeseries.Select(x => x.value).ToList();
            var mean = series.Average();
            var stdDev = series.StandardDeviation();
            var t = TailAvg(timeseries, enable, true);

            return Math.Abs(t - mean) > 3 * stdDev;
        }, enable);
    }

    public static bool StdDevFromMovingAverage(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var series = timeseries.Select(x => x.value).ToList();
            var expAverage = series.ExponentialMovingAverage(50, enable, true);
            var stdDev = series.ExponentialMovingStdDev(50, enable, true);

            return Math.Abs(series.Last() - expAverage.Last()) > 3 * stdDev.Last();
        }, enable);
    }

    public static bool MeanSubtractionCumulation(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            int count = timeseries.Count;
            if (count <= 1)
            {
                return false;
            }

            timeseries = timeseries.ToList(); // make a clone

            double mean = 0.0;
            double squaredSum = 0.0;

            for (int i = 0; i < count; i++)
            {
                double value = timeseries[i].value;
                mean += value;
                squaredSum += value * value;

                if (i > 0)
                {
                    double average = mean / i;
                    value -= average;
                }

                timeseries[i] = (timeseries[i].timestamp, value);
            }

            mean /= count - 1;
            double variance = squaredSum / count - mean * mean;
            double stdDev = Math.Sqrt(variance);

            double lastValue = timeseries[count - 1].value;
            return Math.Abs(lastValue) > 3 * stdDev;
        }, enable);
    }

    public static bool MeanSubtractionCumulationOld(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var series = timeseries.Select(x => x.value).ToList();
            series = series.Select((x, i) =>
            {
                if (series.Count >= i && i > 0)
                {
                    return x - series.Take(i).Average();
                }

                return x;
            }).ToList();
            var stdDev = series.Take(series.Count - 1).StandardDeviation();

            return Math.Abs(series.Last()) > 3 * stdDev;
        }, enable);
    }

    public static bool LeastSquares2(List<Tuple<DateTime, double>> timeseries, bool enable)
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

    public static bool LeastSquares(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            if (timeseries.Count < 2)
            {
                return false;
            }

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
        }, enable);
    }

    public static bool HistogramBins(List<(DateTime timestamp, double value)> timeseries, bool enable)
    {
        return RecordTiming(() =>
        {
            var series = timeseries.Select(x => x.value).ToArray();
            var t = TailAvg(timeseries, enable, true);
            var h = new MathNet.Numerics.Statistics.Histogram(series, 15);
            //var h = Histogram(series, 15);// Util.Histogram(series, 15);
            var bins = Enumerable.Range(0, h.BucketCount).Select(x => h[x]).ToArray(); // h.binEdges;

            for (int i = 0; i < h.BucketCount; i++)
            {
                int binSize = (int)h[i].Count; //h.histogram[i];
                if (binSize <= 20)
                {
                    if (i == 0)
                    {
                        if (t <= bins[0].UpperBound)
                            return true;
                    }
                    else if (t >= bins[i].LowerBound && t < bins[i].UpperBound)
                    {
                        return true;
                    }
                }
            }

            return false;
        }, enable);
    }

    private static (int[] histogram, double[] binEdges) Histogram(double[] data, int bins, bool enable, bool indent)
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

    private static List<double> ExponentialMovingAverage(this IEnumerable<double> source, int window, bool enable, bool indent)
    {
        return RecordTiming(() =>
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
        }, enable, indent);
    }

    private static List<double> ExponentialMovingStdDev(this IEnumerable<double> source, int window, bool enable, bool indent)
    {
        return RecordTiming(() =>
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
        }, enable, indent);
    }
}