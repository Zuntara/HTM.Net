using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HTM.Net.Research.NAB;

public class Scorer
{
    public static async Task<ResultsList> ScoreCorpusAsync(double threshold, string detectorName, string profileName,
        Dictionary<string, double> costMatrix,
        string resultsDetectorDir, ICorpus resultsCorpus, ICorpusLabel corpusLabel, double probationaryPercent, bool scoreFlag)
    {
        List<Arguments> args = new List<Arguments>();

        foreach (KeyValuePair<string, IDataFile> pair in resultsCorpus.DataFiles)
        {
            string relativePath = pair.Key;
            IDataFile dataSet = pair.Value;
            if (relativePath == "_scores.csv")
            {
                continue;
            }

            relativePath = Utils.convertResultsPathToDataPath(Path.Combine(detectorName, relativePath));
            string relativeDir = Path.GetDirectoryName(relativePath);
            string fileName = Path.GetFileName(relativePath);
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

            var timestamps = labels["timestamp"].Select(v => (DateTime)v).ToList();
            var anomalyScores = dataSet.Data["anomaly_score"].Select(v => (double)v).ToList();

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

        var tasks = args.Select(a => new Task<Result>((a) => ScoreDataSet((Arguments)a), a)).ToArray();
        var results = new ResultsList((await Task.WhenAll(tasks)).ToList());

        foreach (var result in results)
        {
            result.Totals =  result.Score + result.TP + result.TN + result.FP + result.FN + result.TotalCount;
        }

        return results;
    }

    private static Result ScoreDataSet(Arguments args)
    {
        var (detectorName, profileName, relativePath, outputPath, threshold, timestamps, anomalyScores, windows, costMatrix, probationaryPercent, scoreFlag) = args;
        var scorer = new Sweeper(probationPercent: probationaryPercent, costMatrix: costMatrix);

        var (scores, bestRow) = scorer.ScoreDataSet(timestamps, anomalyScores, windows, relativePath, threshold);

        if (scoreFlag)
        {

        }

        return new Result(detectorName, profileName, relativePath, threshold, bestRow.Score,
            bestRow.TP, bestRow.TN, bestRow.FP, bestRow.FN, bestRow.TotalCount);
    }

    public record Arguments(string detectorName, string profileName, string relativePath, string outputPath,
        double threshold, List<DateTime> timestamps, List<double> anomalyScores,
        List<(DateTime start, DateTime end)> windows, Dictionary<string, double> costMatrix, double probationaryPercent,
        bool scoreFlag);

    public record Result(string detectorName, string profileName, string relativePath, double threshold, double Score,
        double TP, double TN, double FP, double FN, double TotalCount)
    {
        public double Totals { get; set; }
    }

    public class ResultsList : List<Result>
    {
        public ResultsList(IEnumerable<Result> results)
            : base(results)
        {
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
                writer.WriteLine($"{result.detectorName},{result.profileName},{result.relativePath},{result.threshold},{result.Score},{result.TP},{result.TN},{result.FP},{result.FN},{result.TotalCount}");
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
                results.Add(new Result(parts[0], parts[1], parts[2], double.Parse(parts[3]), double.Parse(parts[4]),
                                       double.Parse(parts[5]), double.Parse(parts[6]), double.Parse(parts[7]), double.Parse(parts[8]),
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