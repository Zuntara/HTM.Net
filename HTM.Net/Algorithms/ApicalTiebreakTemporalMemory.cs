using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Monitor;
using HTM.Net.Util;

namespace HTM.Net.Algorithms;

// https://github.com/numenta/htmresearch/blob/ea7f86eb6c575e5a749ce4411d1cd10b18da19a1/htmresearch/algorithms/apical_tiebreak_temporal_memory.py 
/// <summary>
/// A generalized Temporal Memory with apical dendrites that add a "tiebreak".
/// Basal connections are used to implement traditional Temporal Memory.
///     The apical connections are used for further disambiguation.If multiple cells
/// 
///     in a minicolumn have active basal segments, each of those cells is predicted,
/// unless one of them also has an active apical segment, in which case only the
/// 
/// cells with active basal and apical segments are predicted.
/// In other words, the apical connections have no effect unless the basal input
/// is a union of SDRs (e.g.from bursting minicolumns).
/// This class is generalized in two ways:
/// - This class does not specify when a 'timestep' begins and ends.It exposes
/// two main methods: 'depolarizeCells' and 'activateCells', and callers or
///     subclasses can introduce the notion of a timestep.
/// - This class is unaware of whether its 'basalInput' or 'apicalInput' are from
/// internal or external cells.They are just cell numbers.The caller knows
///     what these cell numbers mean, but the TemporalMemory doesn't.
/// </summary>
/*[Serializable]
public abstract class ApicalTiebreakTemporalMemory : Persistable, IComputeDecorator
{
    public int ColumnCount { get; private set; }
    public int BasalInputSize { get; private set; }
    public int ApicalInputSize { get; private set; }
    public int CellsPerColumn { get; private set; }
    public int ActivationThreshold { get; private set; }
    public int ReducedBasalThreshold { get; private set; }
    public double InitialPermanence { get; private set; }
    public double ConnectedPermanence { get; private set; }
    public int MinThreshold { get; private set; }
    public int SampleSize { get; private set; }
    public double PermanenceIncrement { get; private set; }
    public double PermanenceDecrement { get; private set; }
    public double BasalPredictedSegmentDecrement { get; private set; }
    public double ApicalPredictedSegmentDecrement { get; private set; }
    public int MaxSynapsesPerSegment { get; private set; }
    public int Seed { get; private set; }
    private bool UseApicalTiebreak = true;
    private bool UseApicalModulationBasalThreshold = true;

    public ApicalTiebreakTemporalMemory(
        int columnCount = 2048,
        int basalInputSize = 0,
        int apicalInputSize = 0,
        int cellsPerColumn = 32,
        int activationThreshold = 13,
        int reducedBasalThreshold = 13,
        double initialPermanence = 0.21,
        double connectedPermanence = 0.50,
        int minThreshold = 10,
        int sampleSize = 20,
        double permanenceIncrement = 0.1,
        double permanenceDecrement = 0.1,
        double basalPredictedSegmentDecrement = 0.0,
        double apicalPredictedSegmentDecrement = 0.0,
        int maxSynapsesPerSegment = -1,
        int seed = 42)
    {
        ColumnCount = columnCount;
        BasalInputSize = basalInputSize;
        ApicalInputSize = apicalInputSize;
        CellsPerColumn = cellsPerColumn;
        ActivationThreshold = activationThreshold;
        ReducedBasalThreshold = reducedBasalThreshold;
        InitialPermanence = initialPermanence;
        ConnectedPermanence = connectedPermanence;
        MinThreshold = minThreshold;
        SampleSize = sampleSize;
        PermanenceIncrement = permanenceIncrement;
        PermanenceDecrement = permanenceDecrement;
        BasalPredictedSegmentDecrement = basalPredictedSegmentDecrement;
        ApicalPredictedSegmentDecrement = apicalPredictedSegmentDecrement;
        MaxSynapsesPerSegment = maxSynapsesPerSegment;
        Seed = seed;
        
        this.BasalConnections = new SparseObjectMatrix<Column>(new[] { columnCount * cellsPerColumn ,
            basalInputSize});
        this.ApicalConnections = new SparseObjectMatrix<Column>(new[] { columnCount * cellsPerColumn ,
            apicalInputSize});
        this.Rng = new XorshiftRandom(seed);
        this.ActiveCells = Array.Empty<int>();
        this.WinnerCells = Array.Empty<int>();
        this.PredictedCells = Array.Empty<int>();
        this.PredictedActiveCells = Array.Empty<int>();
        this.ActiveBasalSegments = Array.Empty<int>();
        this.ActiveApicalSegments = Array.Empty<int>();
        this.MatchingBasalSegments = Array.Empty<int>();
        this.MatchingApicalSegments = Array.Empty<int>();
        this.BasalPotentialOverlaps = Array.Empty<int>();
        this.ApicalPotentialOverlaps = Array.Empty<int>();

        this.UseApicalTiebreak = true;
        this.UseApicalModulationBasalThreshold = true;
    }

    public int[] ApicalPotentialOverlaps { get; set; }

    public int[] BasalPotentialOverlaps { get; set; }

    public DistalDendrite[] MatchingApicalSegments { get; set; }

    public DistalDendrite[] MatchingBasalSegments { get; set; }

    public DistalDendrite[] ActiveApicalSegments { get; set; }

    public DistalDendrite[] ActiveBasalSegments { get; set; }

    public Cell[] PredictedActiveCells { get; set; }

    public Cell[] PredictedCells { get; set; }

    public Cell[] WinnerCells { get; set; }

    public Cell[] ActiveCells { get; set; }

    public XorshiftRandom Rng { get; set; }

    public SparseObjectMatrix<Column> ApicalConnections { get; set; }

    public SparseObjectMatrix<Column> BasalConnections { get; set; }

    /// <summary>
    /// @param columnCount (int)
    ///     The number of minicolumns
    /// @param basalInputSize(sequence)
    ///     The number of bits in the basal input
    /// @param apicalInputSize(int)
    ///     The number of bits in the apical input
    /// @param cellsPerColumn(int)
    ///     Number of cells per column
    /// @param activationThreshold(int)
    ///     If the number of active connected synapses on a segment is at least this
    ///     threshold, the segment is said to be active.
    /// @param reducedBasalThreshold (int)
    ///     The activation threshold of basal (lateral) segments for cells that have
    ///     active apical segments. If equal to activationThreshold (default),
    ///     this parameter has no effect.
    /// @param initialPermanence (float)
    ///     Initial permanence of a new synapse
    /// @param connectedPermanence(float)
    ///     If the permanence value for a synapse is greater than this value, it is said
    ///     to be connected.
    /// @param minThreshold (int)
    ///     If the number of potential synapses active on a segment is at least this
    ///     threshold, it is said to be "matching" and is eligible for learning.
    /// @param sampleSize (int)
    ///     How much of the active SDR to sample with synapses.
    /// @param permanenceIncrement (float)
    ///     Amount by which permanences of synapses are incremented during learning.
    /// @param permanenceDecrement (float)
    ///     Amount by which permanences of synapses are decremented during learning.
    /// @param basalPredictedSegmentDecrement (float)
    ///     Amount by which segments are punished for incorrect predictions.
    /// @param apicalPredictedSegmentDecrement (float)
    ///     Amount by which segments are punished for incorrect predictions.
    /// @param maxSynapsesPerSegment
    ///     The maximum number of synapses per segment.
    /// @param seed (int)
    ///     Seed for the random number generator.
    /// </summary>
    /// <param name="c">Connections object</param>
    public static void Init(Connections c)
    {
        if (c.GetNumColumns() == 1)
        {
            c.SetNumColumns(2048);
        }

        SparseObjectMatrix<Column> basalConnections = new SparseObjectMatrix<Column>(
            new[] { c.GetNumColumns() * c.GetCellsPerColumn(), c.GetBasalInputSize() });
        c.SetBasalConnections(basalConnections);

        SparseObjectMatrix<Column> apicalConnections = new SparseObjectMatrix<Column>(
            new[] { c.GetNumColumns() * c.GetCellsPerColumn(), c.GetApicalInputSize() });
        c.SetApicalConnections(apicalConnections);
    }

    /// <summary>
    /// Clear all cell and segment activity.
    /// </summary>
    /// <param name="connections"></param>
    public void Reset(Connections connections)
    {
        this.ActiveCells = Array.Empty<Cell>();
        this.WinnerCells = Array.Empty<Cell>();
        this.PredictedCells = Array.Empty<Cell>();
        this.PredictedActiveCells = Array.Empty<Cell>();
        this.ActiveBasalSegments = Array.Empty<DistalDendrite>();
        this.ActiveApicalSegments = Array.Empty<DistalDendrite>();
        this.MatchingBasalSegments = Array.Empty<DistalDendrite>();
        this.MatchingApicalSegments = Array.Empty<DistalDendrite>();
        this.BasalPotentialOverlaps = Array.Empty<int>();
        this.ApicalPotentialOverlaps = Array.Empty<int>();
    }

    public abstract ComputeCycle Compute(Connections c, int[] activeColumns, bool learn);

    /// <summary>
    /// 
    /// </summary>
    /// <param name="basalInput">List of active input bits for the basal dendrite segments</param>
    /// <param name="apicalInput">List of active input bits for the apical dendrite segments</param>
    /// <param name="learn"> Whether learning is enabled. Some TM implementations may depolarize cells
    /// differently or do segment activity bookkeeping when learning is enabled.</param>
    public void DepolarizeCells(Connections c, int[] basalInput, int[] apicalInput, bool learn)
    {
        var (activeApicalSegments, matchingApicalSegments, apicalPotentialOverlaps)
            = CalculateApicalSegmentActivity(c.GetApicalConnections(), apicalInput,
                c.GetConnectedPermanence(), c.GetActivationThreshold(), c.GetMinThreshold());

        HashSet<Cell> reducedBasalThresholdCells;
        if (learn || UseApicalModulationBasalThreshold == false)
        {
            reducedBasalThresholdCells = new HashSet<Cell>();
        }
        else
        {
            reducedBasalThresholdCells = c.GetApicalConnections()
                .MapSegmentsToCells(activeApicalSegments);
        }

        var (activeBasalSegments, matchingBasalSegments, basalPotentialOverlaps)
            = CalculateBasalSegmentActivity(c.GetBasalConnections(), basalInput,
                c.GetConnectedPermanence(), c.GetActivationThreshold(), c.GetMinThreshold(),
                c.GetReducedBasalThreshold());

        var predictedCells = CalculatePredictedCells(activeBasalSegments,
            activeApicalSegments);

        this.PredictedCells = predictedCells;
        this.ActiveBasalSegments = activeBasalSegments;
        this.ActiveApicalSegments = activeApicalSegments;
        this.MatchingBasalSegments = matchingBasalSegments;
        this.MatchingApicalSegments = matchingApicalSegments;
        this.BasalPotentialOverlaps = basalPotentialOverlaps;
        this.ApicalPotentialOverlaps = apicalPotentialOverlaps;
    }

    /// <summary>
    ///  Activate cells in the specified columns, using the result of the previous
    /// 'depolarizeCells' as predictions. Then learn.
    /// </summary>
    /// <param name="activeColumns">List of active columns</param>
    /// <param name="basalReinforceCandidates"> List of bits that the active cells may reinforce basal synapses to.</param>
    /// <param name="apicalReinforceCandidates">List of bits that the active cells may reinforce apical synapses to.</param>
    /// <param name="basalGrowthCandidates">List of bits that the active cells may grow new basal synapses to.</param>
    /// <param name="apicalGrowthCandidates"> List of bits that the active cells may grow new apical synapses to</param>
    /// <param name="learn">Whether to grow / reinforce / punish synapses</param>
    public void ActivateCells(ICollection<Cell> activeColumns, int[] basalReinforceCandidates,
        int[] apicalReinforceCandidates, int[] basalGrowthCandidates,
        int[] apicalGrowthCandidates, bool learn = true)
    {
        // Calculate the active cells
        var (correctPredictedCells, burstingColumns, _) = Arrays.SetCompare(PredictedCells, activeColumns,
            ArrayUtils.Divide(PredictedCells , CellsPerColumn), rightMinusLeft: true);
        var newActiveCells = 
            correctPredictedCells.Concat(Arrays.GetAllCellsInColumns(burstingColumns, CellsPerColumn));

        // Calculate learning
        var (learningActiveBasalSegments, learningMatchingBasalSegments, basalSegmentsToPunish, newBasalSegments,
                learningCells)
            = CalculateBasalLearning(activeColumns, burstingColumns, correctPredictedCells,
                ActiveBasalSegments, MatchingBasalSegments, BasalPotentialOverlaps);

        var (learningActiveApicalSegments,
            learningMatchingApicalSegments,
            apicalSegmentsToPunish,
            newApicalSegmentCells) = CalculateApicalLearning(
            learningCells, activeColumns, ActiveApicalSegments,
            MatchingApicalSegments, ApicalPotentialOverlaps);

        // Learn
        if (learn)
        {
            // Learn on existing segments
            foreach (var learningSegments in learningActiveBasalSegments.Concat(learningMatchingBasalSegments))
            {
                Learn(BasalConnections, Rng, learningSegments,
                    basalReinforceCandidates, basalGrowthCandidates,
                    BasalPotentialOverlaps,
                    InitialPermanence, SampleSize,
                    PermanenceIncrement, PermanenceDecrement,
                    MaxSynapsesPerSegment);
            }

            foreach (var learningSegments in learningActiveApicalSegments.Concat(learningMatchingApicalSegments))
            {
                Learn(ApicalConnections, Rng, learningSegments,
                    apicalReinforceCandidates, apicalGrowthCandidates,
                    ApicalPotentialOverlaps,
                    InitialPermanence, SampleSize,
                    PermanenceIncrement, PermanenceDecrement,
                    MaxSynapsesPerSegment);
            }

            // Punish incorrect predictions
            if (BasalPredictedSegmentDecrement != 0.0)
            {
                BasalConnections.AdjustActiveSynapses(basalSegmentsToPunish, basalReinforceCandidates,
                    -BasalPredictedSegmentDecrement);
            }

            if (ApicalPredictedSegmentDecrement != 0.0)
            {
                ApicalConnections.AdjustActiveSynapses(apicalSegmentsToPunish, apicalReinforceCandidates,
                    -ApicalPredictedSegmentDecrement);
            }

            // Grow new segments
            if (basalGrowthCandidates.Length > 0)
            {
                LearnOnNewSegments(BasalConnections, Rng,
                    newBasalSegmentCells, basalGrowthCandidates,
                    InitialPermanence, SampleSize,
                    MaxSynapsesPerSegment);
            }
        }

        // Save the results
        newActiveCells.sort();
        learningCells.sort();
        ActiveCells = newActiveCells;
        WinnerCells = learningCells;
        PredictedActiveCells = correctPredictedCells;
    }

    private (int[] learningActiveBasalSegments, int[] learningMatchingBasalSegments, int[] basalSegmentsToPunish, int[] newBasalSegments,
        int[] learningCells) CalculateBasalLearning(ICollection<Cell> activeColumns, int[] burstingColumns,
        int[] correctPredictedCells, int[] activeBasalSegments, int[] matchingBasalSegments,
        int[] basalPotentialOverlaps)
    {
        // Correctly predicted columns
        var learningActiveBasalSegments = BasalConnections.FilterSegmentsByCell(
            activeBasalSegments, correctPredictedCells);

        var cellsForMatchingBasal = BasalConnections.MapSegmentsToCells(
            matchingBasalSegments);
        var matchingCells = cellsForMatchingBasal.Distinct().ToArray(); //np.unique(cellsForMatchingBasal);

        var (matchingCellsInBurstingColumns,
            burstingColumnsWithNoMatch, _) = Arrays.SetCompare(
            matchingCells, burstingColumns, ArrayUtils.Divide(matchingCells, CellsPerColumn),
            rightMinusLeft: true);

        var learningMatchingBasalSegments = ChooseBestSegmentPerColumn(
            BasalConnections, matchingCellsInBurstingColumns,
            matchingBasalSegments, basalPotentialOverlaps, CellsPerColumn);
        var newBasalSegmentCells = GetCellsWithFewestSegments(
            BasalConnections, Rng, burstingColumnsWithNoMatch,
            CellsPerColumn);

        var learningCells = correctPredictedCells.Concat(
                BasalConnections.MapSegmentsToCells(learningMatchingBasalSegments))
            .Concat(newBasalSegmentCells);

        // Incorrectly predicted columns
        var correctMatchingBasalMask = Arrays.In1D(
            ArrayUtils.Divide(cellsForMatchingBasal, CellsPerColumn), activeColumns);

        var basalSegmentsToPunish = matchingBasalSegments.Filter(correctMatchingBasalMask, true);

        return (learningActiveBasalSegments,
            learningMatchingBasalSegments,
            basalSegmentsToPunish,
            newBasalSegmentCells,
            learningCells);
    }

    private (int[] learningActiveApicalSegments, int[] learningMatchingApicalSegments, int[] apicalSegmentsToPunish,
        int[] newApicalSegmentCells) CalculateApicalLearning(int[] learningCells, ICollection<Cell> activeColumns,
        int[] activeApicalSegments, int[] matchingApicalSegments, int[] apicalPotentialOverlaps)
    {
// Cells with active apical segments
        var learningActiveApicalSegments = ApicalConnections.FilterSegmentsByCell(
            activeApicalSegments, learningCells);

// Cells with matching apical segments
        var learningCellsWithoutActiveApical = np.setdiff1d(
            learningCells,
            ApicalConnections.MapSegmentsToCells(learningActiveApicalSegments));
        var cellsForMatchingApical = ApicalConnections.MapSegmentsToCells(
            matchingApicalSegments);
        var learningCellsWithMatchingApical = np.intersect1d(
            learningCellsWithoutActiveApical, cellsForMatchingApical);
        var learningMatchingApicalSegments = ChooseBestSegmentPerCell(
            ApicalConnections, learningCellsWithMatchingApical,
            matchingApicalSegments, apicalPotentialOverlaps);

// Cells that need to grow an apical segment
        var newApicalSegmentCells = np.setdiff1d(learningCellsWithoutActiveApical,
            learningCellsWithMatchingApical);

// Incorrectly predicted columns
        var correctMatchingApicalMask =
            Arrays.In1D(ArrayUtils.Divide(cellsForMatchingApical, CellsPerColumn), activeColumns);

        var apicalSegmentsToPunish = matchingApicalSegments.Filter(correctMatchingApicalMask, true);

        return (learningActiveApicalSegments,
            learningMatchingApicalSegments,
            apicalSegmentsToPunish,
            newApicalSegmentCells);
    }

    private (int[] activeSegments, int[] matchingSegments, int[] potentialOverlaps) 
        CalculateApicalSegmentActivity(Connections connections, ICollection<Cell> activeInput, double connectedPermanence, double activationThreshold, double minThreshold)
    {
        // Active
        var overlaps = connections.ComputeActivity(activeInput, connectedPermanence);
        var activeSegments = ArrayUtils.Where(overlaps.numActiveConnected, o => o > activationThreshold);

        // Matching
        var potentialOverlaps = connections.ComputeActivity(activeInput, 0);
        var matchingSegments = ArrayUtils.Where(potentialOverlaps.numActivePotential, o => o > minThreshold);

        return (activeSegments, matchingSegments, potentialOverlaps.numActivePotential);
    }

    private (int[] activeSegments, int[] matchingSegments, int[] potentialOverlaps)
        CalculateBasalSegmentActivity(SparseObjectMatrix<Column> connections, int[] activeInput,
            int[] reducedBasalThresholdCells, double connectedPermanence, double activationThreshold,
            double minThreshold, int reducedBasalThreshold)
    {
        // Active apical segments lower the activation threshold for basal (lateral) segments
        var overlaps = connections.ComputeActivity(activeInput, connectedPermanence);
        var outrightActiveSegments = ArrayUtils.Where(overlaps, o => o > activationThreshold);
        int[] activeSegments;
        if ((reducedBasalThreshold != activationThreshold) && (reducedBasalThresholdCells.Length > 0))
        {
            var potentiallyActiveSegments = ArrayUtils.Where(overlaps, o => o < activationThreshold && o >= reducedBasalThreshold);
            var cellsOfCASegments = connections.MapSegmentsToCells(potentiallyActiveSegments);
            // apically active segments are condit. active segments from apically active cells
            var conditionallyActiveSegments = potentiallyActiveSegments.Filter(Arrays.In1D(cellsOfCASegments, reducedBasalThresholdCells));
            activeSegments = outrightActiveSegments.Concat(conditionallyActiveSegments).ToArray();
        }
        else
        {
            activeSegments = outrightActiveSegments;
        }

        // Matching
        var potentialOverlaps = connections.ComputeActivity(activeInput);
        var matchingSegments = ArrayUtils.Where(potentialOverlaps, o => o > minThreshold);

        return (activeSegments,
            matchingSegments,
            potentialOverlaps);
    }

    private int[] CalculatePredictedCells(int[] activeBasalSegments, int[] activeApicalSegments)
    {
        var cellsForBasalSegments = BasalConnections.MapSegmentsToCells(
            activeBasalSegments);
        var cellsForApicalSegments = ApicalConnections.MapSegmentsToCells(
            activeApicalSegments);

        var fullyDepolarizedCells = np.intersect1d(cellsForBasalSegments,
            cellsForApicalSegments);
        var partlyDepolarizedCells = np.setdiff1d(cellsForBasalSegments,
            fullyDepolarizedCells);

        var inhibitedMask = np.in1d(
            partlyDepolarizedCells / self.cellsPerColumn,
            fullyDepolarizedCells / self.cellsPerColumn);
        var predictedCells = np.append(fullyDepolarizedCells,
            partlyDepolarizedCells[~inhibitedMask]);

        if (UseApicalTiebreak == false)
        {
            predictedCells = cellsForBasalSegments;
        }

        return predictedCells;
    }

    private void Learn(SparseObjectMatrix<Column> connections, IRandom rng,
        int[] learningSegments, ICollection<Cell> activeInput, int[] growthCandidates,
        int[] potentialOverlaps, double initialPermanence, int sampleSize,
        double permanenceIncrement, double permanenceDecrement, int maxSynapsesPerSegment)
    {
        // Learn on existing segments
        connections.AdjustSynapses(learningSegments, activeInput,
            permanenceIncrement, -permanenceDecrement);

        // Grow new synapses. Calculate "maxNew", the maximum number of synapses to
        // grow per segment. "maxNew" might be a number or it might be a list of
        // numbers.
        int maxNew = 0;
        int[] maxNewList = null;
        if (sampleSize == -1)
            maxNew = growthCandidates.Length;
        else
            maxNewList = sampleSize - potentialOverlaps[learningSegments];

        if (maxSynapsesPerSegment != -1)
        {
            int[] synapseCounts = connections.MapSegmentsToSynapseCounts(learningSegments);
            int[] numSynapsesToReachMax = ArrayUtils.Sub(maxSynapsesPerSegment , synapseCounts);
            maxNewList = ArrayUtils.Where(maxNewList.Concat(numSynapsesToReachMax), maxNew => maxNew <= numSynapsesToReachMax);
        }

        connections.GrowSynapsesToSample(learningSegments, growthCandidates,
            maxNewList, initialPermanence, rng);
    }

    private void LearnOnNewSegments(SparseObjectMatrix<Column> connections, IRandom rng, int[] newSegmentCells,
        int[] growthCandidates, double initialPermanence, int sampleSize, int maxSynapsesPerSegment)
    {
        var numNewSynapses = growthCandidates.Length;

        if (sampleSize != -1)
        {
            numNewSynapses = Math.Min(numNewSynapses, sampleSize);
        }

        if (maxSynapsesPerSegment != -1)
        {
            numNewSynapses = Math.Min(numNewSynapses, maxSynapsesPerSegment);
        }

        var newSegments = connections.CreateSegments(newSegmentCells);
        connections.GrowSynapsesToSample(newSegments, growthCandidates,
            numNewSynapses, initialPermanence,
            rng);
    }

    private int[] ChooseBestSegmentPerCell(SparseObjectMatrix<Column> connections, int[] cells, int[] allMatchingSegments, int[] potentialOverlaps)
    {
        var candidateSegments = connections.FilterSegmentsByCell(allMatchingSegments, cells);

        // Narrow it down to one pair per cell.
        var onePerCellFilter = np2.argmaxMulti(potentialOverlaps[candidateSegments],
            connections.mapSegmentsToCells(
                candidateSegments));
        var learningSegments = candidateSegments[onePerCellFilter];

        return learningSegments;
    }

    private int[] ChooseBestSegmentPerColumn(SparseObjectMatrix<Column> connections, 
        int[] matchingCells, int[] allMatchingSegments, int[] potentialOverlaps, int cellsPerColumn)
    {
        var candidateSegments = connections.FilterSegmentsByCell(allMatchingSegments,
            matchingCells)

        // Narrow it down to one segment per column.
        var cellScores = potentialOverlaps[candidateSegments];
        var columnsForCandidates = (connections.mapSegmentsToCells(candidateSegments) /
                                cellsPerColumn);
        var onePerColumnFilter = np2.argmaxMulti(cellScores, columnsForCandidates);

        var learningSegments = candidateSegments[onePerColumnFilter];

        return learningSegments;
    }

    private int[] GetCellsWithFewestSegments(SparseObjectMatrix<Column> connections, IRandom rng, int[] columns, int cellsPerColumn)
    {
        var candidateCells = np2.getAllCellsInColumns(columns, cellsPerColumn);

// Arrange the segment counts into one row per minicolumn.
        var segmentCounts = np.reshape(connections.getSegmentCounts(candidateCells),
            newshape = (len(columns), cellsPerColumn));

// Filter to just the cells that are tied for fewest in their minicolumn.
        var minSegmentCounts = np.amin(segmentCounts, axis = 1, keepdims = True);
        candidateCells = candidateCells[np.flatnonzero(segmentCounts ==
                                                       minSegmentCounts)];

// Filter to one cell per column, choosing randomly from the minimums.
// To do the random choice, add a random offset to each index in-place, using
// casting to floor the result.
        (_,
            onePerColumnFilter,
            numCandidatesInColumns) = np.unique(candidateCells / cellsPerColumn,
            return_index = True, return_counts = True);

        offsetPercents = np.empty(len(columns), dtype = "float32");
        rng.initializeReal32Array(offsetPercents);

        np.add(onePerColumnFilter,
            offsetPercents * numCandidatesInColumns,
            out= onePerColumnFilter,
        casting = "unsafe");

        return candidateCells[onePerColumnFilter];
    }
}

public class ApicalTiebreakPairMemory : ApicalTiebreakTemporalMemory
{
    public override ComputeCycle Compute(Connections c, int[] activeColumns, bool learn)
    {
        throw new NotImplementedException();
    }
}*/