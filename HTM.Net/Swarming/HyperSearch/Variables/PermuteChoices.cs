using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Util;

namespace HTM.Net.Swarming.HyperSearch.Variables;

/// <summary>
/// Define a permutation variable which can take on discrete choices.
/// </summary>
[Serializable]
public class PermuteChoices : PermuteVariable
{
    public int PositionIdx { get; private set; }
    public int BestPositionIdx { get; private set; }
    public bool FixEarly { get; private set; }
    public double FixEarlyFactor { get; private set; }
    public object[] Choices { get; private set; }
    public double? BestResult { get; private set; }
    public List<List<double>> ResultsPerChoice { get; private set; }

    [Obsolete("Don't use")]
    public PermuteChoices()
    {

    }

    public PermuteChoices(object[] choices, bool fixEarly = false)
    {
        this.Choices = choices;
        PositionIdx = 0;

        // Keep track of the results obtained for each choice
        //this._resultsPerChoice = [[]] *len(this.choices);
        ResultsPerChoice = new List<List<double>>();
        for (int i = 0; i < choices.Length; i++)
            ResultsPerChoice.Add(new List<double>());

        // The particle's local best position and the best global position
        BestPositionIdx = PositionIdx;
        BestResult = null;

        // If this is true then we only return the best position for this encoder
        // after all choices have been seen.
        FixEarly = fixEarly;

        // Factor that affects how quickly we assymptote to simply choosing the
        // choice with the best error value
        FixEarlyFactor = 0.7;
    }

    #region Overrides of PermuteVariable

    public override VarState GetState()
    {
        return new VarState
        {
            _position = GetPosition(),
            position = GetPosition(),
            velocity = null,
            bestPosition = Choices[BestPositionIdx],
            bestResult = BestResult
        };
    }

    public override void SetState(VarState varState)
    {
        PositionIdx = Array.IndexOf(Choices, varState._position);
        BestPositionIdx = Array.IndexOf(Choices, varState.bestPosition);
        BestResult = varState.bestResult;
    }

    public override object GetPosition()
    {
        return Choices[PositionIdx];
    }

    public override void Agitate()
    {
        // Not sure what to do for choice variables....
        // TODO: figure this out
    }

    public override object NewPosition(object globalBestPosition, IRandom rng)
    {
        // Compute the mean score per choice.
        int numChoices = Choices.Length;
        List<double?> meanScorePerChoice = new List<double?>();
        double overallSum = 0;
        int numResults = 0;

        foreach (var i in ArrayUtils.Range(0, numChoices))
        {
            if (ResultsPerChoice[i].Count > 0)
            {
                var data = ResultsPerChoice[i].ToArray();
                meanScorePerChoice.Add(data.Average());
                overallSum += data.Sum();
                numResults += data.Length;
            }
            else
            {
                meanScorePerChoice.Add(null);
            }
        }

        if (Math.Abs(numResults) < double.Epsilon)
        {
            overallSum = 1.0;
            numResults = 1;
        }

        // For any choices we don't have a result for yet, set to the overall mean.
        foreach (var i in ArrayUtils.Range(0, numChoices))
        {
            if (meanScorePerChoice[i] == null)
            {
                meanScorePerChoice[i] = overallSum / numResults;
            }
        }

        // Now, pick a new choice based on the above probabilities. Note that the
        //  best result is the lowest result. We want to make it more likely to
        //  pick the choice that produced the lowest results. So, we need to invert
        //  the scores (someLargeNumber - score).
        meanScorePerChoice = meanScorePerChoice.ToList();

        // Invert meaning.
        //meanScorePerChoice = (1.1 * meanScorePerChoice.Max().GetValueOrDefault()) - meanScorePerChoice;
        meanScorePerChoice = meanScorePerChoice.Select(d => 1.1 * meanScorePerChoice.Max().GetValueOrDefault() - d).ToList();
        // If you want the scores to quickly converge to the best choice, raise the
        // results to a power. This will cause lower scores to become lower
        // probability as you see more results, until it eventually should
        // assymptote to only choosing the best choice.
        if (FixEarly)
        {
            meanScorePerChoice = meanScorePerChoice.Select(d =>
            {
                if (d.HasValue)
                {
                    return Math.Pow(d.Value, numResults * FixEarlyFactor / numChoices);
                }
                return (double?)null;
            }).ToList();
            //meanScorePerChoice **= (numResults * this._fixEarlyFactor / numChoices);
        }
        // Normalize.
        double total = meanScorePerChoice.Sum().GetValueOrDefault();
        if (total == 0)
        {
            total = 1.0;
        }
        //meanScorePerChoice /= total;
        meanScorePerChoice = meanScorePerChoice.Select(m => m / total).ToList();
        // Get distribution and choose one based on those probabilities.
        var distribution = meanScorePerChoice.CumulativeSum().ToList();
        var r = rng.NextDouble() * distribution.Last();
        int choiceIdx = ArrayUtils.Where(distribution, d => r < d).First();
        // int choiceIdx = numpy.where(r <= distribution)[0][0];

        PositionIdx = choiceIdx;
        return GetPosition();
    }


    public override void PushAwayFrom(List<object> otherPositions, IRandom rng)
    {
        // Get the count of how many in each position
        //positions = [this.choices.index(x) for x in otherPositions];
        var positions = otherPositions.Select(x => Array.IndexOf(Choices, x)).ToList();
        var positionCounts = new int[Choices.Length];  // [0] * this.choices.Length;
        foreach (var pos in positions)
        {
            positionCounts[pos] += 1;
        }

        PositionIdx = ArrayUtils.Argmin(positionCounts);
        BestPositionIdx = PositionIdx;
    }

    public override void ResetVelocity(IRandom rng)
    {

    }

    #endregion

    #region Overrides of Object

    public override string ToString()
    {
        return string.Format("PermuteChoices(choices={0}) [position={1}]", Choices,
            Choices[PositionIdx]);
    }

    #endregion

    /// <summary>
    /// Setup our resultsPerChoice history based on the passed in
    /// resultsPerChoice.
    /// 
    /// For example, if this variable has the following choices:
    /// ['a', 'b', 'c']
    /// 
    /// resultsPerChoice will have up to 3 elements, each element is a tuple
    /// containing(choiceValue, errors) where errors is the list of errors
    /// received from models that used the specific choice:
    /// retval:
    /// [('a', [0.1, 0.2, 0.3]), ('b', [0.5, 0.1, 0.6]), ('c', [0.2])]
    /// </summary>
    /// <param name="resultsPerChoice"></param>
    public void SetResultsPerChoice(List<Tuple<object, List<double>>> resultsPerChoice)
    {
        // Keep track of the results obtained for each choice.

        //this._resultsPerChoice = [[]] *len(this.choices);
        ResultsPerChoice = new List<List<double>>();
        for (int i = 0; i < Choices.Length; i++)
            ResultsPerChoice.Add(new List<double>());

        //for (choiceValue, values) in resultsPerChoice
        foreach (var pair in resultsPerChoice)
        {
            object choiceValue = pair.Item1;
            List<double> values = pair.Item2;

            int choiceIndex = Array.IndexOf(Choices, choiceValue);
            ResultsPerChoice[choiceIndex] = values.ToList();
        }
    }
}