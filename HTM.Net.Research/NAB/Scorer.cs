using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Tweetinvi.Core.Events;

namespace HTM.Net.Research.NAB;

public class Scorer
{
    public static async Task<ResultsList> ScoreCorpusAsync(double threshold, Detector detectorName, string profileName,
        CostMatrix costMatrix,
        string resultsDetectorDir, ICorpus resultsCorpus, ICorpusLabel corpusLabel, double probationaryPercent, bool scoreFlag)
    {
        List<Arguments> args = new List<Arguments>();

        foreach (KeyValuePair<string, IDataFile> pair in resultsCorpus.DataFiles)
        {
            string relativePath = pair.Key;
            IDataFile dataSet = pair.Value;
            if (relativePath.EndsWith("_scores.csv"))
            {
                continue;
            }

            relativePath = Utils.convertResultsPathToDataPath(Path.Combine(detectorName.ToString(), relativePath));
            string relativeDir = Path.GetDirectoryName(relativePath);
            string fileName = detectorName + "_" + Path.GetFileName(relativePath);
            string outputPath = Path.Combine(resultsDetectorDir, relativeDir, fileName);

            List<(DateTime start, DateTime end)> windows;
            DataFrame labels;
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

            args.Add(new Arguments(detectorName,
                profileName,
                relativePath,
                outputPath,
                threshold,
                timestamps,
                anomalyScores,
                windows,
                costMatrix,
                probationaryPercent,
                scoreFlag));
        }

        var tasks = args.AsParallel().Select(ScoreDataSet).ToArray();
        var results = new ResultsList(tasks);

        double totalScore = 0;
        double totalTP = 0;
        double totalTN = 0;
        double totalFP = 0;
        double totalFN = 0;
        double totalTotalCount = 0;

        foreach (var result in results)
        {
            totalScore += result.Score;
            totalTP += result.TP;
            totalTN += result.TN;
            totalFP += result.FP;
            totalFN += result.FN;
            totalTotalCount += result.TotalCount;
        }

        Result total = new Result(Detector.Totals, "", "", null, totalScore, totalTP, totalTN, totalFP, totalFN, totalTotalCount);

        results.Add(total);

        return results;
    }

    private static Result ScoreDataSet(Arguments args)
    {
        var (detectorName, profileName, relativePath, outputPath, threshold, timestamps, anomalyScores, windows, costMatrix, probationaryPercent, scoreFlag) = args;
        var scorer = new Sweeper(probationPercent: probationaryPercent, costMatrix: costMatrix);

        var (scores, bestRow) = scorer.ScoreDataSet(timestamps, anomalyScores, windows, relativePath, threshold);
        //var (scores2, bestRow2) = scorer.ScoreDataSetOld(timestamps, anomalyScores, windows, relativePath, threshold);

        if (scoreFlag)
        {
            // Append scoring function values to the respective results file
            var dfCsv = DataFrame.LoadCsv(outputPath);
            dfCsv["S(t)_" + profileName] = scores.Select(s => (object)s).ToList();
            dfCsv.ToCsv(outputPath, true);
            // var dfCSV = pandas.read_csv(outputPath, header = 0, parse_dates =[0])
            // dfCSV["S(t)_%s" % profileName] = scores
            // dfCSV.to_csv(outputPath, index = False)
        }

        return new Result(detectorName, profileName, relativePath, threshold, bestRow.Score,
            bestRow.TP, bestRow.TN, bestRow.FP, bestRow.FN, bestRow.TotalCount);
    }

    public record Arguments(Detector detectorName, string profileName, string relativePath, string outputPath,
        double threshold, List<DateTime> timestamps, List<double> anomalyScores,
        List<(DateTime start, DateTime end)> windows, CostMatrix costMatrix, double probationaryPercent,
        bool scoreFlag);

    public record Result(Detector detectorName, string profileName, string relativePath, double? threshold, double Score,
        double TP, double TN, double FP, double FN, double TotalCount)
    {
        public static Result FromFields(Dictionary<string, object> row)
        {
            Detector detectorName = row.ContainsKey("detector") ? Enum.Parse<Detector>(row["detector"]?.ToString() ?? "None", true) : Detector.None;
            string profileName = row.ContainsKey("profile") ? row["profile"]?.ToString() : "unknown";
            string relativePath = row.ContainsKey("relative_path") ? row["relative_path"].ToString() : "unknown";
            double threshold = row.ContainsKey("threshold") && row["threshold"].ToString() != string.Empty ? double.Parse(row["threshold"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double score = row.ContainsKey("score") ? double.Parse(row["score"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double tp = row.ContainsKey("tp") ? double.Parse(row["tp"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double tn = row.ContainsKey("tn") ? double.Parse(row["tn"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double fp = row.ContainsKey("fp") ? double.Parse(row["fp"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double fn = row.ContainsKey("fn") ? double.Parse(row["fn"].ToString(), NumberFormatInfo.InvariantInfo) : 0;
            double totalCount = row.ContainsKey("total_count") ? double.Parse(row["total_count"].ToString(), NumberFormatInfo.InvariantInfo) : 0;

            Result result = new Result(detectorName, profileName, relativePath, threshold, score, tp, tn, fp, fn,
                totalCount);
            return result;
        }
    }

    public class ResultsList : List<Result>
    {
        public ResultsList(IEnumerable<Result> results)
            : base(results)
        {
        }

        public ResultsList(DataFrame dataFrame)
        {
            foreach (Dictionary<string, object> row in dataFrame.IterateRows())
            {
                Add(Result.FromFields(row));
            }
        }

        public void ToCsv(string path, bool includeHeaders)
        {
            using StreamWriter writer = new StreamWriter(path, Encoding.UTF8,
                new FileStreamOptions { Access = FileAccess.Write, Mode = FileMode.Create });
            if (includeHeaders)
            {
                writer.WriteLine("detector,profile,relative_path,threshold,score,tp,tn,fp,fn,total_count");
            }

            foreach (var result in this)
            {
                writer.WriteLine($"{result.detectorName},{result.profileName},{result.relativePath}," +
                                 $"{result.threshold?.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.Score.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.TP.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.TN.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.FP.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.FN.ToString("0.0", NumberFormatInfo.InvariantInfo)}," +
                                 $"{result.TotalCount.ToString("0.0", NumberFormatInfo.InvariantInfo)}");
            }

            writer.Flush();
        }

        public static ResultsList ReadCsv(StreamReader reader)
        {
            var results = new List<Result>();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (line.StartsWith("detector,profile,relative_path,"))
                {
                    continue;
                }

                var parts = line.Split(',');
                results.Add(new Result(
                    Enum.Parse<Detector>(parts[0]),
                    parts[1],
                    parts[2],
                    double.Parse(parts[3]),
                    double.Parse(parts[4]),
                    double.Parse(parts[5]),
                    double.Parse(parts[6]),
                    double.Parse(parts[7]),
                    double.Parse(parts[8]),
                    double.Parse(parts[9])));
            }

            return new ResultsList(results);
        }

        public List<T> GetColumn<T>(string columnName)
        {
            var property = typeof(Result).GetProperty(columnName);
            if (property == null)
            {
                throw new ArgumentException($"Column name {columnName} not found");
            }

            return this.Select(r => (T)property.GetValue(r)).ToList();
        }
    }

}