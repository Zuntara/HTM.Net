using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Network;
using HTM.Net.Research.Vision;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.Random
{

    public class RandomGuess : List<RandomGuessData>
    {
        private RandomGuess(int recordNumber, double[] actualNumbers, double anomalyFactor)
        {
            RecordNumber = recordNumber;
            AnomalyFactor = anomalyFactor;
            ActualNumbers = actualNumbers.Select(a => (int)a).ToArray();
        }

        public void AddPrediction(double[] prediction, bool addDeviatesAlso)
        {
            int[] predictedNumbers = prediction.Select(p => (int)p).ToArray();
            Add(new RandomGuessData(ActualNumbers, predictedNumbers));

            if (addDeviatesAlso)
            {
                var deviates = GetDeviates(predictedNumbers, prediction);
                foreach (int[] deviate in deviates)
                {
                    Add(new RandomGuessData(ActualNumbers, deviate));
                }
            }
            Cleanup();
        }

        public double GetProfit()
        {
            return this.Sum(d => d.Revenue - d.Cost);
        }

        public double GetCost()
        {
            return this.Sum(d => d.Cost);
        }

        public double GetApproximateRevenue()
        {
            return this.Sum(d => d.Revenue);
        }

        public int[] GetPrimaryPrediction()
        {
            return this.First(d => d.IsValid).Numbers;
        }

        public string GetPredictionScores()
        {
            return Arrays.ToString(this.Select(d => d.Score).Where(d => !string.IsNullOrWhiteSpace(d)).Distinct());
        }

        public static List<RandomGuess> GetLastGuesses(List<RandomGuess> list, int cnt)
        {
            return list.Skip(list.Count - cnt).ToList();
        }

        public static Map<string, int> GetHistogramMap(List<RandomGuess> list)
        {
            Map<string, int> histo = new Map<string, int>();

            foreach (RandomGuess guess in list)
            {
                foreach (RandomGuessData guessData in guess)
                {
                    string key = string.IsNullOrWhiteSpace(guessData.Score) ? "--" : guessData.Score;
                    if (!histo.ContainsKey(key))
                    {
                        histo.Add(key, 1);
                    }
                    else
                    {
                        histo[key] += 1;
                    }
                }
            }
            // sort the map
            histo = new Map<string, int>(histo.OrderBy(h => h.Key).ToDictionary(k => k.Key, v => v.Value));
            return histo;
        }

        public static RandomGuess From(Map<int, double[]> previousPredicted, Map<int, double[]> nextPredictions, IInference inference, string[] classifierFields)
        {
            double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)inference.GetClassifierInput()[cf]).Get("inputValue")).ToArray();

            RandomGuess guess = new RandomGuess(inference.GetRecordNum(), actuals, inference.GetAnomalyScore());

            // Get predictions
            List<double[]> dNumbers = new List<double[]>();
            foreach (int step in inference.GetClassification(classifierFields[0]).StepSet())
            {
                dNumbers.Add(previousPredicted?[step]);
            }

            dNumbers = dNumbers.Where(n => n != null).ToList();
            if (dNumbers.Any())
            {
                foreach (double[] numbers in dNumbers)
                {
                    guess.AddPrediction(numbers, false);
                }
            }

            // Add next predictions (for housekeeping)

            guess.NextPredictions = new List<int[]>();
            foreach (var nextPrediction in nextPredictions)
            {
                int[] predictedNextNumbers = nextPrediction.Value.Select(p => (int)p).ToArray();
                guess.NextPredictions.Add(predictedNextNumbers);

                //var deviates = GetDeviates(predictedNextNumbers, nextPrediction.Value);
                //foreach (int[] deviate in deviates)
                //{
                //    guess.NextPredictions.Add(deviate);
                //}
            }

            

            guess.NextPredictions = guess.NextPredictions.Take(10).ToList();

            return guess;
        }

        /// <summary>
        /// Gets numbers that are close to the predictions
        /// </summary>
        /// <param name="predictedNumbers">converted prediction numbers</param>
        /// <param name="currentPredictions">original prediction numbers</param>
        /// <param name="count">number of deviations to set</param>
        private static List<int[]> GetDeviates(int[] predictedNumbers, double[] currentPredictions, int count = 20)
        {
            CombinationParameters cp = new CombinationParameters();

            for (int i = 0; i < currentPredictions.Length; i++)
            {
                if (currentPredictions[i] % 1.0 > double.Epsilon)
                {
                    int low = (int)Math.Floor(currentPredictions[i]);
                    int high = (int)Math.Ceiling(currentPredictions[i]);
                    cp.Define($"n{i + 1}", new List<object> { low, high }.Distinct().ToList());
                }
                else
                {
                    int pred = (int)currentPredictions[i];
                    cp.Define($"n{i + 1}", new List<object> { pred });
                }
            }
            return cp.GetAllCombinations().Select(c => c.Select(TypeConverter.Convert<int>).ToArray())
                .Where(n => !Arrays.AreEqual(n, predictedNumbers))
                .Take(count)
                .ToList();
        }

        /// <summary>
        /// Make a distinct list of predictions
        /// </summary>
        private void Cleanup()
        {
            List<RandomGuessData> data = this.Distinct().ToList();
            this.Clear();
            foreach (RandomGuessData guessData in data.Take(10))
            {
                Add(guessData);
            }
        }

        public int RecordNumber { get; private set; }

        public int[] ActualNumbers { get; private set; }

        public double AnomalyFactor { get; private set; }

        public List<int[]> NextPredictions { get; set; }
    }

    /// <summary>
    /// Represents one single prediction
    /// </summary>
    public class RandomGuessData
    {
        public RandomGuessData(int[] actualNumbers, int[] prediction)
        {
            Numbers = prediction;
            Length = prediction.Length;
            Score = GetScore(actualNumbers);
            Cost = GetCost();
            Revenue = GetApproximateRevenue();
        }

        private Tuple<int, bool> CalculateGuess(int[] actuals)
        {
            bool bonusHit = false;
            int correctNumbersAct = 0;

            Stack<int> predStack = new Stack<int>(Numbers);
            List<int> actualList = new List<int>(actuals);

            while (predStack.Count > 0)
            {
                var predValue = predStack.Pop();

                if (actualList.Contains(predValue))
                {
                    int index = actualList.IndexOf(predValue);
                    actualList.RemoveAt(index);
                    correctNumbersAct++;
                }
            }
            bool correctBonus = Numbers.Contains(actuals.Last());
            return new Tuple<int, bool>(correctNumbersAct, correctBonus);
        }

        private string GetScore(int[] actualNumbers)
        {
            var guessScore = CalculateGuess(actualNumbers);
            if (guessScore.Item1 < 2 || (guessScore.Item1 == 2 && !guessScore.Item2))
                return "";
            return $"{guessScore.Item1}{(guessScore.Item2 ? "+" : "")}";
        }

        private double GetCost()
        {
            switch (Length)
            {
                case 6:
                    return 1;
                case 7:
                    return 7;
            }
            throw new NotSupportedException("Unsupported length");
        }

        private double GetApproximateRevenue()
        {
            if (Length == 6)
            {
                return GetApproxRevenue(Score);
            }
            if (Length == 7)
            {
                double subRev = 0;
                if (Score == "2+" || Score == "3")
                {
                    subRev += GetApproxRevenue(Score) * 4;
                }
                else if (Score == "3+")
                {
                    subRev += GetApproxRevenue("3+") * 3 + GetApproxRevenue("3") * 1 + GetApproxRevenue("2+") * 3;
                }
                else if (Score == "4")
                {
                    subRev += GetApproxRevenue("4") * 3 + GetApproxRevenue("3") * 4;
                }
                else if (Score == "4+")
                {
                    subRev += GetApproxRevenue("4+") * 2 + GetApproxRevenue("4") * 1 + GetApproxRevenue("3+") * 4;
                }
                else if (Score == "5")
                {
                    subRev += GetApproxRevenue("5") * 2 + GetApproxRevenue("4") * 5;
                }
                else if (Score == "5+")
                {
                    subRev += GetApproxRevenue("5+") * 1 + GetApproxRevenue("5") * 1 + GetApproxRevenue("4") * 5;
                }
                else if (Score == "6")
                {
                    subRev += GetApproxRevenue("6") * 1 + GetApproxRevenue("5") * 6;
                }
                else if (Score == "6+")
                {
                    subRev += GetApproxRevenue("6") * 1 + GetApproxRevenue("5+") * 6;
                }
                return subRev;
            }
            throw new NotSupportedException("Unsupported prediction length");
        }

        public static double GetApproxRevenue(string score)
        {
            double rev = 0;

            if (score == "2+") rev += 3.00;
            if (score == "3") rev += 5.00;
            if (score == "3+") rev += 11.50;
            if (score == "4") rev += 26.50;         // approx
            if (score == "4+") rev += 300.00;       // approx
            if (score == "5") rev += 1200.00;       // approx
            if (score == "5+") rev += 16500.00;     // approx
            if (score == "6" || score == "6+") rev += 1000000.0; // approx

            return rev;
        }

        #region Equality members

        protected bool Equals(RandomGuessData other)
        {
            return Length == other.Length && Arrays.AreEqual(Numbers, other.Numbers);
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj)) return false;
            if (ReferenceEquals(this, obj)) return true;
            if (obj.GetType() != this.GetType()) return false;
            return Equals((RandomGuessData)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Length * 397) ^ (Numbers != null ? Numbers.GetHashCode() : 0);
            }
        }

        #endregion

        public bool IsValid
        {
            get { return Numbers.Distinct().Count() == Length; } // no doubles allowed
        }

        public int Length { get; }

        public int[] Numbers { get; }

        public string Score { get; }

        public double Cost { get; }

        public double Revenue { get; }
    }

    public class RandomGameData
    {
        private static readonly IRandom Random = new XorshiftRandom(1956);

        public RandomGameData(double[] actuals, double[] predicted)
        {
            RandomNumbers = GetRandomGuesses();
            ActualNumbers = actuals.Select(a => (int)a).ToArray();
            if (predicted != null)
            {
                PredictedNumbers = predicted.Select(a => (int)a).ToArray();
                DeviatedNumbers = GetDeviates(predicted, 20);
                CalculateGuessCounts();
                CalculateDeviationGuessCounts();
            }
        }

        public int RecordNumber { get; set; }

        public int[] ActualNumbers { get; set; }

        public int[] PredictedNumbers { get; set; }

        public int[] RandomNumbers { get; set; }

        public List<int[]> DeviatedNumbers { get; set; }

        public Tuple<int, bool> CorrectPredictionsWithBonus { get; set; }
        public List<Tuple<int, bool>> CorrectDeviationPredictionsWithBonus { get; set; }
        public Tuple<int, bool> CorrectRandomPredictionsWithBonus { get; set; }

        public double AnomalyFactor { get; set; }

        // Helper methods

        private void CalculateGuessCounts()
        {
            CorrectPredictionsWithBonus = CalculateOneGuess(ActualNumbers, PredictedNumbers);
            CorrectRandomPredictionsWithBonus = CalculateOneGuess(ActualNumbers, RandomNumbers);
        }

        public void CalculateDeviationGuessCounts()
        {
            List<Tuple<int, bool>> allResults = new List<Tuple<int, bool>>();
            foreach (int[] numbers in DeviatedNumbers)
            {
                allResults.Add(CalculateOneGuess(ActualNumbers, numbers));
            }
            CorrectDeviationPredictionsWithBonus = allResults;
        }

        public static Tuple<int, bool> CalculateOneGuess(int[] actuals, int[] predicted)
        {
            bool bonusHit = false;
            int correctNumbersAct = 0;

            Stack<int> predStack = new Stack<int>(predicted); // limit to 6 numbers
            List<int> actualList = new List<int>(actuals);

            while (predStack.Count > 0)
            {
                var predValue = predStack.Pop();

                if (actualList.Contains(predValue))
                {
                    int index = actualList.IndexOf(predValue);
                    actualList.RemoveAt(index);
                    correctNumbersAct++;
                }
            }
            bool correctBonus = predicted.Contains(actuals.Last());
            return new Tuple<int, bool>(correctNumbersAct, correctBonus);
        }

        /// <summary>
        /// Gets numbers that are close to the predictions
        /// </summary>
        /// <param name="currentPredictions"></param>
        /// <param name="count">number of deviations to set</param>
        private List<int[]> GetDeviates(double[] currentPredictions, int count = 20)
        {
            CombinationParameters cp = new CombinationParameters();

            for (int i = 0; i < currentPredictions.Length; i++)
            {
                if (currentPredictions[i] % 1.0 > double.Epsilon)
                {
                    int low = (int)Math.Floor(currentPredictions[i]);
                    int high = (int)Math.Ceiling(currentPredictions[i]);
                    cp.Define($"n{i + 1}", new List<object> { low, high }.Distinct().ToList());
                }
                else
                {
                    int pred = (int)currentPredictions[i];
                    cp.Define($"n{i + 1}", new List<object> { pred });
                }
            }
            return cp.GetAllCombinations().Select(c => c.Select(TypeConverter.Convert<int>).ToArray())
                .Where(n => !Arrays.AreEqual(n, PredictedNumbers))
                .Take(count)
                .ToList();
        }

        public static int[] GetCountsOfCorrectRandomGuesses(IEnumerable<RandomGameData> collection)
        {
            return // skip first prediction, it's bogus
                collection.Skip(1)
                    .Select(gd => gd.CorrectRandomPredictionsWithBonus.Item1 + (gd.CorrectRandomPredictionsWithBonus.Item2 ? 1 : 0))
                    .ToArray();
        }

        public static Map<string, int> GetCountsOfCorrectRandomGuessesInStrings(IEnumerable<RandomGameData> collection)
        {
            // skip first prediction, it's bogus
            var results = collection.Skip(1)
                .GroupBy(g => g.CorrectRandomPredictionsWithBonus)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            Map<string, int> summary = new Map<string, int>();
            foreach (var item in results)
            {
                summary.Add(item.Id, item.Value);
            }

            string[] relevantStrings = { "2+", "3", "3+", "4", "4+", "5", "5+", "6", "6+" };

            Map<string, int> retVal = new Map<string, int>();
            retVal["--"] = 0;
            foreach (var pair in summary)
            {
                if (relevantStrings.Contains(pair.Key))
                {
                    retVal[pair.Key] = pair.Value;
                }
                else
                {
                    retVal["--"] += pair.Value;
                }
            }

            return retVal;
        }


        public static int[] GetCountsOfCorrectPredictedGuesses(IEnumerable<RandomGameData> collection)
        {
            return // skip first prediction, it's bogus
                collection.Skip(1)
                    .Select(gd => gd.CorrectPredictionsWithBonus.Item1 + (gd.CorrectPredictionsWithBonus.Item2 ? 1 : 0))
                    .ToArray();
        }

        public static Map<string, int> GetCountsOfCorrectPredictedGuessesInStrings(IEnumerable<RandomGameData> collection)
        {
            // skip first prediction, it's bogus
            var results = collection.Skip(1)
                .Where(g => g.CorrectPredictionsWithBonus != null)
                .GroupBy(g => g.CorrectPredictionsWithBonus)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            Map<string, int> summary = new Map<string, int>();
            foreach (var item in results)
            {
                summary.Add(item.Id, item.Value);
            }

            results = collection.Skip(1)
                .Where(d => d.CorrectDeviationPredictionsWithBonus != null)
                .SelectMany(d => d.CorrectDeviationPredictionsWithBonus)
                .GroupBy(g => g)
                .OrderBy(g => g.Key.Item1).ThenBy(g => g.Key.Item2)
                .Select(g => new { Id = $"{g.Key.Item1}{(g.Key.Item2 ? "+" : "")}", Value = g.Count() });

            foreach (var item in results)
            {
                if (summary.ContainsKey(item.Id))
                {
                    summary[item.Id] += item.Value;
                }
                else
                {
                    summary.Add(item.Id, item.Value);
                }
            }

            string[] relevantStrings = { "2+", "3", "3+", "4", "4+", "5", "5+", "6", "6+" };

            Map<string, int> retVal = new Map<string, int>();
            retVal["--"] = 0;
            foreach (var pair in summary)
            {
                if (relevantStrings.Contains(pair.Key))
                {
                    retVal[pair.Key] = pair.Value;
                }
                else
                {
                    retVal["--"] += pair.Value;
                }
            }

            return retVal;
        }

        public string GetHighestCorrectPredictionScore()
        {
            var strings = GetCountsOfCorrectPredictedGuessesInStrings(new List<RandomGameData> { this, this }).Keys.OrderByDescending(k => k).Where(k => k != "--").ToList();
            return strings.FirstOrDefault() ?? "0";
        }
        public Map<string, int> GetPredictionScores()
        {
            var strings = GetCountsOfCorrectPredictedGuessesInStrings(new List<RandomGameData> { this, this });
            return strings;
        }

        public static Map<string, int> GetLastGuesses(IList<RandomGameData> collection, int lastCount)
        {
            return GetCountsOfCorrectPredictedGuessesInStrings(collection.Skip(collection.Count - lastCount));
        }

        private int[] GetRandomGuesses()
        {
            int[] guesses = new int[7];
            guesses[0] = Random.NextInt(22) + 1;
            guesses[1] = Random.NextInt(35 - 1) + 2;
            guesses[2] = Random.NextInt(36 - 3) + 4;
            guesses[3] = Random.NextInt(42 - 7) + 8;
            guesses[4] = Random.NextInt(44 - 12) + 13;
            guesses[5] = Random.NextInt(45 - 15) + 16;
            guesses[6] = Random.NextInt(45) + 1;
            return guesses;//ArrayUtils.Range(0, 6).Select(i => Random.NextInt(45) + 1).ToArray();
        }

        public static double GetCost(IList<RandomGameData> results)
        {
            var prices = results.Where(gd => gd.PredictedNumbers != null).Select(gd => gd.PredictedNumbers.Length == 6 ? 1 : 7);
            var pricesDev = results.Where(gd => gd.DeviatedNumbers != null).SelectMany(gd => gd.DeviatedNumbers.Select(d => d.Length == 6 ? 1 : 7));

            return prices.Sum() + pricesDev.Sum(); // 1 eur per record
        }

        public static double GetApproxRevenue(IList<RandomGameData> results, Map<string, int> rangCounts)
        {
            double rev = 0;

            if (results.Last().PredictedNumbers.Length == 6)
            {
                foreach (var result in rangCounts)
                {
                    rev += GetApproxRevenue(result.Key, result.Value);
                }
            }
            if (results.Last().PredictedNumbers.Length == 7)
            {
                foreach (RandomGameData data in results)
                {
                    var scores = data.GetPredictionScores();
                    foreach (var score in scores)
                    {
                        double subRev = 0;
                        if (score.Key == "2+" || score.Key == "3")
                        {
                            subRev += GetApproxRevenue(score.Key, 4);
                        }
                        if (score.Key == "3+")
                        {
                            subRev += GetApproxRevenue("3+", 3) + GetApproxRevenue("3", 1) + GetApproxRevenue("2+", 3);
                        }
                        if (score.Key == "4")
                        {
                            subRev += GetApproxRevenue("4", 3) + GetApproxRevenue("3", 4);
                        }
                        if (score.Key == "4+")
                        {
                            subRev += GetApproxRevenue("4+", 2) + GetApproxRevenue("4", 1) + GetApproxRevenue("3+", 4);
                        }
                        if (score.Key == "5")
                        {
                            subRev += GetApproxRevenue("5", 2) + GetApproxRevenue("4", 5);
                        }
                        if (score.Key == "5+")
                        {
                            subRev += GetApproxRevenue("5+", 1) + GetApproxRevenue("5", 1) + GetApproxRevenue("4", 5);
                        }
                        if (score.Key == "6")
                        {
                            subRev += GetApproxRevenue("6", 1) + GetApproxRevenue("5", 6);
                        }
                        if (score.Key == "6+")
                        {
                            subRev += GetApproxRevenue("6", 1) + GetApproxRevenue("5+", 6);
                        }
                        rev += subRev * score.Value;
                    }
                }
            }

            return rev;
        }

        public static double GetApproxRevenue(string rangStr, double cnt)
        {
            double rev = 0;

            if (rangStr == "2+") rev += cnt * 3.00;
            if (rangStr == "3") rev += cnt * 5.00;
            if (rangStr == "3+") rev += cnt * 11.50;
            if (rangStr == "4") rev += cnt * 26.50;
            if (rangStr == "4+") rev += cnt * 300.00;
            if (rangStr == "5") rev += cnt * 1200.00;
            if (rangStr == "5+") rev += cnt * 16500.00;
            if (rangStr == "6" || rangStr == "6+") rev += cnt * 1000000.0; // avg

            return rev;
        }

        public static RandomGameData From(Map<int, double[]> previousPredicted, IInference inference, string[] classifierFields)
        {
            double[] actuals = classifierFields.Select(cf => (double)((NamedTuple)inference.GetClassifierInput()[cf]).Get("inputValue")).ToArray();

            RandomGameData gd = new RandomGameData(actuals, previousPredicted?[1]);
            gd.AnomalyFactor = inference.GetAnomalyScore();

            List<double[]> dNumbers = new List<double[]>();
            foreach (int step in inference.GetClassification(classifierFields[0]).StepSet())
            {
                dNumbers.Add(previousPredicted?[step]);
            }

            dNumbers = dNumbers.Where(n => n != null).ToList();
            if (dNumbers.Any())
            {
                foreach (double[] numbers in dNumbers)
                {
                    var devs = gd.GetDeviates(numbers);
                    gd.DeviatedNumbers.AddRange(devs);
                }
                gd.DeviatedNumbers = gd.DeviatedNumbers.Where(n => n != null && !Arrays.AreEqual(n, gd.PredictedNumbers)).Take(10).ToList();
                gd.CalculateDeviationGuessCounts();
            }
            return gd;
        }
    }
}