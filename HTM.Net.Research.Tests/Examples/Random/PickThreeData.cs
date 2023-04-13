using System.Collections.Generic;
using System.Linq;
using HTM.Net.Network;
using HTM.Net.Util;

namespace HTM.Net.Research.Tests.Examples.Random
{
    public class PickThreeData
    {
        public static IRandom Random = new XorshiftRandom(42);

        public PickThreeData(int recordNumber, double[] actuals, Map<int, double[]> predicted)
        {
            RecordNumber = recordNumber;
            RandomNumbers = GetRandomGuesses();
            ActualNumbers = actuals.Select(a => (int)a).ToArray();
            AnalysisResult = new Map<int, PickThreeHit>();
            if (predicted != null)
            {
                PredictedNumbers = new Map<int, int[]>(predicted.ToDictionary(k => k.Key, v => v.Value.Select(x => (int)x).ToArray()));

                AnalysisResult = CalculateHitResult(ActualNumbers, PredictedNumbers);
                NettoResults = CalculateNettoResults(AnalysisResult);
            }
            RandomAnalysisResult = CalculateHitResult(ActualNumbers, RandomNumbers);
            NettoRandomResult = CalculateNettoResult(RandomAnalysisResult);
        }

        private int[] GetRandomGuesses()
        {
            // 3 numbers between 0 and 9
            return ArrayUtils.Range(0, 3).Select(i => Random.NextInt(9)).ToArray();
        }

        internal Map<int, PickThreeHit> CalculateHitResult(int[] actuals, Map<int, int[]> guesses)
        {
            Map<int, PickThreeHit> netResults = new Map<int, PickThreeHit>();
            foreach (int key in guesses.Keys)
            {
                netResults[key] = CalculateHitResult(actuals, guesses[key]);
            }
            return netResults;
        }

        internal PickThreeHit CalculateHitResult(int[] actuals, int[] guess)
        {
            PickThreeHit hit = PickThreeHit.None;

            // First two digits correct
            hit |= Arrays.AreEqual(actuals.Take(2), guess.Take(2)) ? PickThreeHit.CorrectFirstTwo : PickThreeHit.None;
            // Last two digits correct
            hit |= Arrays.AreEqual(actuals.Skip(1).Take(2), guess.Skip(1).Take(2)) ? PickThreeHit.CorrectLastTwo : PickThreeHit.None;
            // Digits correct but not in order
            hit |= ArrayContained(actuals, guess) && guess.Distinct().Count() == 3 ? PickThreeHit.CorrectNumbers : PickThreeHit.None;

            if (ArrayContained(actuals, guess) && guess.Distinct().Count() < 3)
            {
                // Digits correct but not in order with doubles
                hit |= PickThreeHit.CorrectNumbersWithDoubles;
            }
            if (Arrays.AreEqual(actuals, guess))
            {
                // All digits correct and correct order
                hit |= PickThreeHit.CorrectNumbers | PickThreeHit.CorrectOrder;
            }
            return hit;
        }

        private bool ArrayContained(int[] actuals, int[] guess)
        {
            Stack<int> actStack = new Stack<int>(actuals);
            List<int> guessStack = new List<int>(guess);
            bool contained = true;
            while (actStack.Count > 0)
            {
                int check = actStack.Pop();
                if (guessStack.Contains(check))
                {
                    int index = guessStack.IndexOf(check);
                    guessStack.RemoveAt(index);
                }
                else
                {
                    contained = false;
                }
            }
            return contained;
        }

        private int CalculateNettoResult(PickThreeHit hitResult)
        {
            int nettoResult = 0;
            if (hitResult.HasFlag(PickThreeHit.CorrectNumbers) && hitResult.HasFlag(PickThreeHit.CorrectOrder))
            {
                nettoResult += 500;
            }
            if (hitResult.HasFlag(PickThreeHit.CorrectNumbersWithDoubles))
            {
                nettoResult += 160;
            }
            if (hitResult.HasFlag(PickThreeHit.CorrectNumbers))
            {
                nettoResult += 80;
            }
            if (hitResult.HasFlag(PickThreeHit.CorrectFirstTwo) || hitResult.HasFlag(PickThreeHit.CorrectLastTwo))
            {
                nettoResult += 50;
            }
            return nettoResult;
        }

        private Map<int, int> CalculateNettoResults(Map<int, PickThreeHit> hitResult)
        {
            Map<int, int> netResults = new Map<int, int>();
            foreach (int key in hitResult.Keys)
            {
                netResults[key] = CalculateNettoResult(hitResult[key]);
            }
            return netResults;
        }

        public static PickThreeData From(Map<int, double[]> previousPredicted, IInference inference, string[] classifierFields)
        {
            double[] actuals = classifierFields.Select(cf => (double)(inference.GetClassifierInput()[cf]).Get("inputValue")).ToArray();
            PickThreeData data = new PickThreeData(inference.GetRecordNum(), actuals, previousPredicted);

            data.AnomalyFactor = inference.GetAnomalyScore();

            return data;
        }

        public Map<int, PickThreeHit> AnalysisResult { get; private set; }
        public PickThreeHit RandomAnalysisResult { get; private set; }

        public int RecordNumber { get; set; }
        public int[] ActualNumbers { get; set; }
        public Map<int, int[]> PredictedNumbers { get; set; }
        public int[] RandomNumbers { get; set; }

        public Map<int, int> NettoResults { get; private set; }
        public int NettoRandomResult { get; private set; }

        public double AnomalyFactor { get; set; }

    }
}