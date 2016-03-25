using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Monitor;
using HTM.Net.Util;

namespace HTM.Net.Algorithms
{
    public class TemporalMemory : IComputeDecorator
    {
        /**
         * Uses the specified {@link Connections} object to Build the structural 
         * anatomy needed by this {@code TemporalMemory} to implement its algorithms.
         * 
         * The connections object holds the <see cref="Column"/> and <see cref="Cell"/> infrastructure,
         * and is used by both the {@link SpatialPooler} and {@link TemporalMemory}. Either of
         * these can be used separately, and therefore this Connections object may have its
         * Columns and Cells initialized by either the init method of the SpatialPooler or the
         * init method of the TemporalMemory. We check for this so that complete initialization
         * of both Columns and Cells occurs, without either being redundant (initialized more than
         * once). However, <see cref="Cell"/>s only get created when initializing a TemporalMemory, because
         * they are not used by the SpatialPooler.
         * 
         * @param   c       {@link Connections} object
         */
        public void Init(Connections c)
        {
            SparseObjectMatrix<Column> matrix = c.GetMemory() ?? new SparseObjectMatrix<Column>(c.GetColumnDimensions());
            c.SetMemory(matrix);

            int numColumns = matrix.GetMaxIndex() + 1;
            c.SetNumColumns(numColumns);
            int cellsPerColumn = c.GetCellsPerColumn();
            Cell[] cells = new Cell[numColumns * cellsPerColumn];

            //Used as flag to determine if Column objects have been created.
            Column colZero = matrix.GetObject(0);
            for (int i = 0; i < numColumns; i++)
            {
                Column column = colZero == null ? new Column(cellsPerColumn, i) : matrix.GetObject(i);
                for (int j = 0; j < cellsPerColumn; j++)
                {
                    cells[i * cellsPerColumn + j] = column.GetCell(j);
                }
                //If columns have not been previously configured
                if (colZero == null) matrix.Set(i, column);
            }
            //Only the TemporalMemory initializes cells so no need to test for redundancy
            c.SetCells(cells);
        }

        /////////////////////////// CORE FUNCTIONS /////////////////////////////
        /**
         * Feeds input record through TM, performing inferencing and learning
         * 
         * @param connections       the connection memory
         * @param activeColumns     direct proximal dendrite input
         * @param learn             learning mode flag
         * @return                  {@link ComputeCycle} container for one cycle of inference values.
         */
        public ComputeCycle Compute(Connections connections, int[] activeColumns, bool learn)
        {
            ComputeCycle result = ComputeFn(connections, connections.GetColumnSet(activeColumns), connections.GetPredictiveCells(),
                connections.GetActiveSegments(), connections.GetActiveCells(), connections.GetWinnerCells(), connections.GetMatchingSegments(),
                    connections.GetMatchingCells(), learn);

            connections.SetActiveCells(result.ActiveCells());
            connections.SetWinnerCells(result.WinnerCells());
            connections.SetPredictiveCells(result.PredictiveCells());
            connections.SetSuccessfullyPredictedColumns(result.SuccessfullyPredictedColumns());
            connections.SetActiveSegments(result.ActiveSegments());
            connections.SetLearningSegments(result.LearningSegments());
            connections.SetMatchingSegments(result.MatchingSegments());
            connections.SetMatchingCells(result.MatchingCells());

            return result;
        }

        /**
         * Functional version of {@link #compute(int[], boolean)}. 
         * This method is stateless and concurrency safe.
         * 
         * @param c                             {@link Connections} object containing state of memory members
         * @param activeColumns                 active <see cref="Column"/>s in t
         * @param prevPredictiveCells           cells predicting in t-1
         * @param prevActiveSegments            active {@link Segment}s in t-1
         * @param prevActiveCells               active <see cref="Cell"/>s in t-1
         * @param prevWinnerCells               winner <see cref="Cell"/>s in t-1
         * @param prevMatchingSegments          matching {@link Segment}s in t-1
         * @param prevMatchingCells             matching cells in t-1 
         * @param learn                         whether mode is "learning" mode
         * @return
         */
        public ComputeCycle ComputeFn(Connections c, HashSet<Column> activeColumns, HashSet<Cell> prevPredictiveCells, HashSet<DistalDendrite> prevActiveSegments,
            HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells, HashSet<DistalDendrite> prevMatchingSegments, HashSet<Cell> prevMatchingCells, bool learn)
        {
            ComputeCycle cycle = new ComputeCycle();

            ActivateCorrectlyPredictiveCells(c, cycle, prevPredictiveCells, prevMatchingCells, activeColumns);

            BurstColumns(cycle, c, activeColumns, cycle.SuccessfullyPredictedColumns(), prevActiveCells, prevWinnerCells);

            if (learn)
            {
                LearnOnSegments(c, prevActiveSegments, cycle.LearningSegments(), prevActiveCells,
                    cycle.WinnerCells(), prevWinnerCells, cycle.PredictedInactiveCells(), prevMatchingSegments);
            }

            ComputePredictiveCells(c, cycle, cycle.ActiveCells());

            return cycle;
        }

        /**
         * Phase 1: Activate the correctly predictive cells
         * 
         * Pseudocode:
         *
         * - for each previous predictive cell
         *   - if in active column
         *     - mark it as active
         *     - mark it as winner cell
         *     - mark column as predicted
         *   - if not in active column
         *     - mark it as a predicted but inactive cell
         *     
         * @param cnx                   Connectivity of layer
         * @param c                     ComputeCycle interim values container
         * @param prevPredictiveCells   predictive <see cref="Cell"/>s predictive cells in t-1
         * @param activeColumns         active columns in t
         */
        public void ActivateCorrectlyPredictiveCells(Connections cnx, ComputeCycle c,
            HashSet<Cell> prevPredictiveCells, HashSet<Cell> prevMatchingCells, HashSet<Column> activeColumns)
        {

            foreach (Cell cell in prevPredictiveCells)
            {
                Column column = cell.GetColumn();
                if (activeColumns.Contains(column))
                {
                    c.ActiveCells().Add(cell);
                    c.WinnerCells().Add(cell);
                    c.SuccessfullyPredictedColumns().Add(column);
                }
            }

            if (cnx.GetPredictedSegmentDecrement() > 0)
            {
                foreach (Cell cell in prevMatchingCells)
                {
                    Column column = cell.GetColumn();

                    if (!activeColumns.Contains(column))
                    {
                        c.PredictedInactiveCells().Add(cell);
                    }
                }
            }
        }

        /**
         * Phase 2: Burst unpredicted columns.
         * 
         * Pseudocode:
         *
         * - for each unpredicted active column
         *   - mark all cells as active
         *   - mark the best matching cell as winner cell
         *     - (learning)
         *       - if it has no matching segment
         *         - (optimization) if there are previous winner cells
         *           - add a segment to it
         *       - mark the segment as learning
         * 
         * @param cycle                         ComputeCycle interim values container
         * @param c                             Connections temporal memory state
         * @param activeColumns                 active columns in t
         * @param predictedColumns              predicted columns in t
         * @param prevActiveCells               active <see cref="Cell"/>s in t-1
         * @param prevWinnerCells               winner <see cref="Cell"/>s in t-1
         */
        public void BurstColumns(ComputeCycle cycle, Connections c, 
            HashSet<Column> activeColumns, HashSet<Column> predictedActiveColumns, HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells)
        {
            var activeCells = new HashSet<Cell>();
            var winnerCells = new HashSet<Cell>();
            var learnSegments = new HashSet<DistalDendrite>();

            var unpredictedActiveColumns = activeColumns.Except(predictedActiveColumns).ToList();
            unpredictedActiveColumns.Sort();

            foreach (Column column in unpredictedActiveColumns)
            {
                var cells = column.GetCells();
                activeCells.UnionWith(cells);

                CellSearch bmcs = GetBestMatchingCell(c, cells, prevActiveCells);
                winnerCells.Add(bmcs.BestCell);

                if (bmcs.BestSegment == null && prevWinnerCells.Count > 0)
                {
                    bmcs.BestSegment = bmcs.BestCell.CreateSegment(c);
                }
                if (bmcs.BestSegment != null)
                {
                    learnSegments.Add(bmcs.BestSegment);
                }
            }

            cycle.ActiveCells().UnionWith(activeCells);
            cycle.WinnerCells().UnionWith(winnerCells);
            cycle.LearningSegments().UnionWith(learnSegments);
        }

        /**
         * Phase 3: Perform learning by adapting segments.
         * <pre>
         * Pseudocode:
         *
         * - (learning) for each previously active or learning segment
         *   - if learning segment or from winner cell
         *     - strengthen active synapses
         *     - weaken inactive synapses
         *   - if learning segment
         *     - add some synapses to the segment
         *       - sub sample from previous winner cells
         *   
         *   - if predictedSegmentDecrement > 0
         *     - for each previously matching segment
         *       - weaken active synapses but don't touch inactive synapses
         * </pre>    
         *     
         * @param c                             the Connections state of the temporal memory
         * @param prevActiveSegments            the Set of segments active in the previous cycle. "t-1"
         * @param learningSegments              the Set of segments marked as learning segments in "t"
         * @param prevActiveCells               the Set of active cells in "t-1"
         * @param winnerCells                   the Set of winner cells in "t"
         * @param prevWinnerCells               the Set of winner cells in "t-1"
         * @param predictedInactiveCells        the Set of predicted inactive cells
         * @param prevMatchingSegments          the Set of segments with
         * 
         */
        public void LearnOnSegments(Connections c, HashSet<DistalDendrite> prevActiveSegments,
            HashSet<DistalDendrite> learningSegments, HashSet<Cell> prevActiveCells, HashSet<Cell> winnerCells, HashSet<Cell> prevWinnerCells,
                HashSet<Cell> predictedInactiveCells, HashSet<DistalDendrite> prevMatchingSegments)
        {

            double permanenceIncrement = c.GetPermanenceIncrement();
            double permanenceDecrement = c.GetPermanenceDecrement();

            HashSet<DistalDendrite> prevAndLearning = new HashSet<DistalDendrite>(prevActiveSegments);

            foreach (DistalDendrite dendrite in learningSegments)
            {
                prevAndLearning.Add(dendrite);
            }

            //prevAndLearning.addAll(learningSegments);

            foreach (DistalDendrite dd in prevAndLearning)
            {

                bool isLearningSegment = learningSegments.Contains(dd);
                bool isFromWinnerCell = winnerCells.Contains(dd.GetParentCell());

                HashSet<Synapse> activeSynapses = dd.GetActiveSynapses(c, prevActiveCells);

                if (isLearningSegment || isFromWinnerCell)
                {
                    dd.AdaptSegment(c, activeSynapses, permanenceIncrement, permanenceDecrement);
                }

                int n = c.GetMaxNewSynapseCount() - activeSynapses.Count;
                if (isLearningSegment && n > 0)
                {
                    HashSet<Cell> learnCells = dd.PickCellsToLearnOn(c, n, prevWinnerCells, c.GetRandom());
                    foreach (Cell sourceCell in learnCells)
                    {
                        dd.CreateSynapse(c, sourceCell, c.GetInitialPermanence());
                    }
                }
            }

            if (c.GetPredictedSegmentDecrement() > 0)
            {
                foreach (DistalDendrite segment in prevMatchingSegments)
                {
                    bool isPredictedInactiveCell = predictedInactiveCells.Contains(segment.GetParentCell());

                    if (isPredictedInactiveCell)
                    {
                        HashSet<Synapse> activeSynapses = segment.GetActiveSynapses(c, prevActiveCells);
                        segment.AdaptSegment(c, activeSynapses, -c.GetPredictedSegmentDecrement(), 0.0);
                    }
                }
            }
        }

        /**
         * Phase 4: Compute predictive cells due to lateral input on distal dendrites.
         *
         * Pseudocode:
         *
         * - for each distal dendrite segment with activity >= activationThreshold
         *   - mark the segment as active
         *   - mark the cell as predictive
         *   
         * - if predictedSegmentDecrement > 0
         *   - for each distal dendrite segment with unconnected activity > = minThreshold
         *     - mark the segment as matching
         *     - mark the cell as matching
         * 
         * @param c                 the Connections state of the temporal memory
         * @param cycle             the state during the current compute cycle
         * @param activeCells       the active <see cref="Cell"/>s in t
         */
        public void ComputePredictiveCells(Connections c, ComputeCycle cycle, HashSet<Cell> activeCells)
        {
            Map<DistalDendrite, int> numActiveConnectedSynapsesForSegment = new Map<DistalDendrite, int>();
            Map<DistalDendrite, int> numActiveSynapsesForSegment = new Map<DistalDendrite, int>();
            double connectedPermanence = c.GetConnectedPermanence();

            foreach (Cell cell in activeCells)
            {
                foreach (Synapse syn in c.GetReceptorSynapses(cell))
                {
                    DistalDendrite segment = syn.GetSegment<DistalDendrite>();
                    double permanence = syn.GetPermanence();

                    if (permanence >= connectedPermanence)
                    {
                        numActiveConnectedSynapsesForSegment.AdjustOrPutValue(segment, 1, 1);

                        if (numActiveConnectedSynapsesForSegment[segment] >= c.GetActivationThreshold())
                        {
                            cycle.ActiveSegments().Add(segment);
                            cycle.PredictiveCells().Add(segment.GetParentCell());
                        }
                    }

                    if (permanence > 0 && c.GetPredictedSegmentDecrement() > 0)
                    {
                        numActiveSynapsesForSegment.AdjustOrPutValue(segment, 1, 1);

                        if (numActiveSynapsesForSegment[segment] >= c.GetMinThreshold())
                        {
                            cycle.MatchingSegments().Add(segment);
                            cycle.MatchingCells().Add(segment.GetParentCell());
                        }
                    }
                }
            }
        }

        /**
         * Called to start the input of a new sequence, and
         * reset the sequence state of the TM.
         * 
         * @param   connections   the Connections state of the temporal memory
         */
        public void Reset(Connections connections)
        {
            connections.GetActiveCells().Clear();
            connections.GetPredictiveCells().Clear();
            connections.GetActiveSegments().Clear();
            connections.GetWinnerCells().Clear();
            connections.GetMatchingCells().Clear();
            connections.GetMatchingSegments().Clear();
        }

        /////////////////////////////////////////////////////////////
        //                    Helper functions                     //
        /////////////////////////////////////////////////////////////
        /**
         * Gets the cell with the best matching segment
         * (see `TM.bestMatchingSegment`) that has the largest number of active
         * synapses of all best matching segments.
         *
         * If none were found, pick the least used cell (see `TM.leastUsedCell`).
         *  
         * @param c                 Connections temporal memory state
         * @param columnCells             
         * @param activeCells
         * @return a CellSearch (bestCell, BestSegment)
         */
        public CellSearch GetBestMatchingCell(Connections c, IList<Cell> columnCells, HashSet<Cell> activeCells)
        {
            int maxSynapses = 0;
            Cell bestCell = null;
            DistalDendrite bestSegment = null;

            foreach (Cell cell in columnCells)
            {
                SegmentSearch bestMatchResult = GetBestMatchingSegment(c, cell, activeCells);

                if (bestMatchResult.BestSegment != null && bestMatchResult.NumActiveSynapses > maxSynapses)
                {
                    maxSynapses = bestMatchResult.NumActiveSynapses;
                    bestCell = cell;
                    bestSegment = bestMatchResult.BestSegment;
                }
            }

            if (bestCell == null)
            {
                bestCell = GetLeastUsedCell(c, columnCells);
            }

            return new CellSearch(bestCell, bestSegment);
        }

        /**
         * Gets the segment on a cell with the largest number of activate synapses,
         * including all synapses with non-zero permanences.
         * 
         * @param c
         * @param columnCell
         * @param activeCells
         * @return
         */
        public SegmentSearch GetBestMatchingSegment(Connections c, Cell columnCell, HashSet<Cell> activeCells)
        {
            int maxSynapses = c.GetMinThreshold();
            DistalDendrite bestSegment = null;
            int bestNumActiveSynapses = 0;
            int numActiveSynapses = 0;

            foreach (DistalDendrite segment in c.GetSegments(columnCell))
            {
                numActiveSynapses = 0;
                foreach (Synapse synapse in c.GetSynapses(segment))
                {
                    if (activeCells.Contains(synapse.GetPresynapticCell()) && synapse.GetPermanence() > 0)
                    {
                        ++numActiveSynapses;
                    }
                }

                if (numActiveSynapses >= maxSynapses)
                {
                    maxSynapses = numActiveSynapses;
                    bestSegment = segment;
                    bestNumActiveSynapses = numActiveSynapses;
                }
            }

            return new SegmentSearch(bestSegment, bestNumActiveSynapses);
        }

        /**
         * Gets the cell with the smallest number of segments.
         * Break ties randomly.
         * 
         * @param c
         * @param columnCells
         * @return
         */
        public Cell GetLeastUsedCell(Connections c, IList<Cell> columnCells)
        {
            HashSet<Cell> leastUsedCells = new HashSet<Cell>();
            int minNumSegments = int.MaxValue;

            foreach (Cell cell in columnCells)
            {
                int numSegments = c.GetSegments(cell).Count;

                if (numSegments < minNumSegments)
                {
                    minNumSegments = numSegments;
                    leastUsedCells.Clear();
                }

                if (numSegments == minNumSegments)
                {
                    leastUsedCells.Add(cell);
                }
            }

            int randomIdx = c.GetRandom().NextInt(leastUsedCells.Count);
            List<Cell> l = new List<Cell>(leastUsedCells);
            l.Sort();

            return l[randomIdx];
        }

        /// <summary>
        /// Used locally to return best cell/segment pair
        /// </summary>
        public class CellSearch
        {
            internal Cell BestCell { get; set; }
            internal DistalDendrite BestSegment { get; set; }

            public CellSearch() { }
            public CellSearch(Cell bestCell, DistalDendrite bestSegment)
            {
                BestCell = bestCell;
                BestSegment = bestSegment;
            }
        }

        /// <summary>
        /// Used locally to return best segment matching results
        /// </summary>
        public class SegmentSearch
        {
            internal DistalDendrite BestSegment { get; set; }
            internal int NumActiveSynapses { get; set; }

            public SegmentSearch(DistalDendrite bestSegment, int numActiveSynapses)
            {
                BestSegment = bestSegment;
                NumActiveSynapses = numActiveSynapses;
            }
        }
    }
}