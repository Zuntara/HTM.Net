using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Monitor;
using HTM.Net.Util;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Algorithms
{
    [Serializable]
    public class TemporalMemory : Persistable, IComputeDecorator
    {
        private const double Epsilon = 0.00001;
        private const int ActiveColumnsConst = 1;

        /// <summary>
        /// Uses the specified <see cref="Connections"/> object to Build the structural 
        /// anatomy needed by this <see cref="TemporalMemory"/> to implement its algorithms.
        /// 
        /// The connections object holds the <see cref="Column"/> and <see cref="Cell"/> infrastructure,
        /// and is used by both the <see cref="SpatialPooler"/> and <see cref="TemporalMemory"/>. Either of
        /// these can be used separately, and therefore this Connections object may have its
        /// Columns and Cells initialized by either the init method of the SpatialPooler or the
        /// init method of the TemporalMemory. We check for this so that complete initialization
        /// of both Columns and Cells occurs, without either being redundant (initialized more than
        /// once). However, <see cref="Cell"/>s only get created when initializing a TemporalMemory, because
        /// they are not used by the SpatialPooler.
        /// </summary>
        /// <param name="c"><see cref="Connections"/> object</param>
        public static void Init(Connections c)
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

        #region Core Functions

        /////////////////////////// CORE FUNCTIONS /////////////////////////////
        ///

        /// <summary>
        /// Feeds input record through TM, performing inferencing and learning
        /// </summary>
        /// <param name="connections">the connection memory</param>
        /// <param name="activeColumns">direct proximal dendrite input</param>
        /// <param name="learn">learning mode flag</param>
        /// <returns><see cref="ComputeCycle"/> container for one cycle of inference values.</returns>
        public ComputeCycle Compute(Connections connections, int[] activeColumns, bool learn)
        {
            ComputeCycle cycle = new ComputeCycle();
            ActivateCells(connections, cycle, activeColumns, learn);
            ActivateDendrites(connections, cycle, learn);

            return cycle;
        }

        /// <summary>
        /// Calculate the active cells, using the current active columns and dendrite
        /// segments. Grow and reinforce synapses.
        /// 
        /// <pre>
        /// Pseudocode:
        ///   for each column
        ///     if column is active and has active distal dendrite segments
        ///       call activatePredictedColumn
        ///     if column is active and doesn't have active distal dendrite segments
        ///       call burstColumn
        ///     if column is inactive and has matching distal dendrite segments
        ///       call punishPredictedColumn
        ///      
        /// </pre>
        /// </summary>
        /// <param name="conn"></param>
        /// <param name="cycle"></param>
        /// <param name="activeColumnIndices"></param>
        /// <param name="learn"></param>
        public void ActivateCells(Connections conn, ComputeCycle cycle, int[] activeColumnIndices, bool learn)
        {
            ColumnData columnData = new ColumnData();

            HashSet<Cell> prevActiveCells = conn.GetActiveCells();
            HashSet<Cell> prevWinnerCells = conn.GetWinnerCells();

            List<Column> activeColumns = activeColumnIndices
                .OrderBy(i => i)
                .Select(conn.GetColumn)
                .ToList();

            Func<Column, Column> identity = c => c;
            Func<DistalDendrite, Column> segToCol = segment => segment.GetParentCell().GetColumn();

            //@SuppressWarnings({ "rawtypes" })
            GroupBy2<Column> grouper = GroupBy2<Column>.Of(
                new Tuple<List<object>, Func<object, Column>>(activeColumns.Cast<object>().ToList(), x => identity((Column)x)),
                new Tuple<List<object>, Func<object, Column>>(new List<DistalDendrite>(conn.GetActiveSegments()).Cast<object>().ToList(), x => segToCol((DistalDendrite)x)),
                new Tuple<List<object>, Func<object, Column>>(new List<DistalDendrite>(conn.GetMatchingSegments()).Cast<object>().ToList(), x => segToCol((DistalDendrite)x)));

            double permanenceIncrement = conn.GetPermanenceIncrement();
            double permanenceDecrement = conn.GetPermanenceDecrement();

            foreach (Tuple t in grouper)
            {
                columnData = columnData.Set(t);

                if (columnData.IsNotNone(ActiveColumnsConst))
                {
                    if (columnData.ActiveSegments().Any())
                    {
                        List<Cell> cellsToAdd = ActivatePredictedColumn(conn, columnData.ActiveSegments(),
                            columnData.MatchingSegments(), prevActiveCells, prevWinnerCells,
                                permanenceIncrement, permanenceDecrement, learn);

                        cycle.ActiveCells().UnionWith(cellsToAdd);
                        cycle.WinnerCells().UnionWith(cellsToAdd);
                    }
                    else
                    {
                        Tuple cellsXwinnerCell = BurstColumn(conn, columnData.Column(), columnData.MatchingSegments(),
                            prevActiveCells, prevWinnerCells, permanenceIncrement, permanenceDecrement, conn.GetRandomForTemporalMemory(),
                               learn);

                        cycle.ActiveCells().UnionWith((IEnumerable<Cell>)cellsXwinnerCell.Get(0));
                        cycle.WinnerCells().Add((Cell)cellsXwinnerCell.Get(1));
                    }
                }
                else
                {
                    if (learn)
                    {
                        PunishPredictedColumn(conn, columnData.ActiveSegments(), columnData.MatchingSegments(),
                            prevActiveCells, prevWinnerCells, conn.GetPredictedSegmentDecrement());
                    }
                }
            }
        }

        /// <summary>
        /// Calculate dendrite segment activity, using the current active cells.
        /// 
        /// <pre>
        /// Pseudocode:
        ///   for each distal dendrite segment with activity >= activationThreshold
        ///     mark the segment as active
        ///   for each distal dendrite segment with unconnected activity >= minThreshold
        ///     mark the segment as matching
        /// </pre>
        /// </summary>
        /// <param name="conn">the Connectivity</param>
        /// <param name="cycle">Stores current compute cycle results</param>
        /// <param name="learn">If true, segment activations will be recorded. This information is used during segment cleanup.</param>
        public void ActivateDendrites(Connections conn, ComputeCycle cycle, bool learn)
        {
            Connections.Activity activity = conn.ComputeActivity(cycle.activeCells, conn.GetConnectedPermanence());

            List<DistalDendrite> activeSegments = ArrayUtils.Range(0, activity.numActiveConnected.Length)
                .Where(i => activity.numActiveConnected[i] >= conn.GetActivationThreshold())
                .Select(conn.GetSegmentForFlatIdx)
                .ToList();

            List<DistalDendrite> matchingSegments = ArrayUtils.Range(0, activity.numActiveConnected.Length)
                .Where(i => activity.numActivePotential[i] >= conn.GetMinThreshold())
                .Select(conn.GetSegmentForFlatIdx)
                .ToList();

            activeSegments.Sort(conn.segmentPositionSortKey);
            matchingSegments.Sort(conn.segmentPositionSortKey);

            //Collections.sort(activeSegments, conn.segmentPositionSortKey);
            //Collections.sort(matchingSegments, conn.segmentPositionSortKey);

            cycle.activeSegments = activeSegments;
            cycle.matchingSegments = matchingSegments;

            conn.lastActivity = activity;
            conn.SetActiveCells(new HashSet<Cell>(cycle.activeCells));
            conn.SetWinnerCells(new HashSet<Cell>(cycle.winnerCells));
            conn.SetActiveSegments(activeSegments);
            conn.setMatchingSegments(matchingSegments);
            // Forces generation of the predictive cells from the above active segments
            conn.ClearPredictiveCells();
            conn.GetPredictiveCells();

            if (learn)
            {
                activeSegments.ForEach(conn.RecordSegmentActivity);
                conn.StartNewIteration();
            }
        }

        /// <summary>
        /// Indicates the start of a new sequence. Clears any predictions and makes sure
        /// synapses don't grow to the currently active cells in the next time step.
        /// </summary>
        /// <param name="connections"></param>
        public void Reset(Connections connections)
        {
            connections.GetActiveCells().Clear();
            connections.GetWinnerCells().Clear();
            connections.GetActiveSegments().Clear();
            connections.GetMatchingSegments().Clear();
        }

        /// <summary>
        /// Determines which cells in a predicted column should be added to winner cells
        /// list, and learns on the segments that correctly predicted this column.
        /// <pre>
        /// Pseudocode:
        ///   for each cell in the column that has an active distal dendrite segment
        ///     mark the cell as active
        ///     mark the cell as a winner cell
        ///     (learning) for each active distal dendrite segment
        ///       strengthen active synapses
        ///       weaken inactive synapses
        ///       grow synapses to previous winner cells
        /// </pre>
        /// </summary>
        /// <param name="conn">the connections</param>
        /// <param name="activeSegments">Active segments in the specified column</param>
        /// <param name="matchingSegments">Matching segments in the specified column</param>
        /// <param name="prevActiveCells">Active cells in `t-1`</param>
        /// <param name="prevWinnerCells">Winner cells in `t-1`</param>
        /// <param name="permanenceIncrement"></param>
        /// <param name="permanenceDecrement"></param>
        /// <param name="learn">If true, grow and reinforce synapses</param>
        /// <returns>A list of predicted cells that will be added to active cells and winner cells.</returns>
        public List<Cell> ActivatePredictedColumn(Connections conn, List<DistalDendrite> activeSegments,
            List<DistalDendrite> matchingSegments, HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells,
                double permanenceIncrement, double permanenceDecrement, bool learn)
        {

            List<Cell> cellsToAdd = new List<Cell>();
            Cell previousCell = null;
            Cell currCell;
            foreach (DistalDendrite segment in activeSegments)
            {
                if (!Equals(currCell = segment.GetParentCell(), previousCell))
                {
                    cellsToAdd.Add(currCell);
                    previousCell = currCell;
                }

                if (learn)
                {
                    AdaptSegment(conn, segment, prevActiveCells, permanenceIncrement, permanenceDecrement);

                    int numActive = conn.GetLastActivity().numActivePotential[segment.GetIndex()];
                    int nGrowDesired = conn.GetMaxNewSynapseCount() - numActive;

                    if (nGrowDesired > 0)
                    {
                        GrowSynapses(conn, prevWinnerCells, segment, conn.GetInitialPermanence(),
                            nGrowDesired, conn.GetRandomForTemporalMemory());
                    }
                }
            }

            return cellsToAdd;
        }
        
        /// <summary>
        /// <p>
        /// Activates all of the cells in an unpredicted active column,
        /// chooses a winner cell, and, if learning is turned on, either adapts or
        /// creates a segment. growSynapses is invoked on this segment.
        /// </p>
        /// <p>
        /// <b>Pseudocode:</b>
        /// </p><p>
        /// <pre>
        ///  mark all cells as active
        ///  if there are any matching distal dendrite segments
        ///      find the most active matching segment
        ///      mark its cell as a winner cell
        ///      (learning)
        ///      grow and reinforce synapses to previous winner cells
        ///  else
        ///      find the cell with the least segments, mark it as a winner cell
        ///      (learning)
        ///      (optimization) if there are previous winner cells
        ///          add a segment to this winner cell
        ///          grow synapses to previous winner cells
        /// </pre>
        /// </p>
        /// </summary>
        /// <param name="conn">Connections instance for the TM</param>
        /// <param name="column">Bursting <see cref="Column"/></param>
        /// <param name="matchingSegments">List of matching <see cref="DistalDendrite"/>s</param>
        /// <param name="prevActiveCells">Active cells in `t-1`</param>
        /// <param name="prevWinnerCells">Winner cells in `t-1`</param>
        /// <param name="permanenceIncrement">Amount by which permanences of synapses are decremented during learning</param>
        /// <param name="permanenceDecrement">Amount by which permanences of synapses are incremented during learning</param>
        /// <param name="random">Random number generator</param>
        /// <param name="learn">Whether or not learning is enabled</param>
        /// <returns>Tuple containing:
        ///                  cells       list of the processed column's cells
        ///                  bestCell    the best cell
        /// </returns>
        public Tuple BurstColumn(Connections conn, Column column, List<DistalDendrite> matchingSegments,
            HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells, double permanenceIncrement, double permanenceDecrement,
                IRandom random, bool learn)
        {

            IList<Cell> cells = column.GetCells();
            Cell bestCell;

            if (matchingSegments.Any())
            {
                int[] numPoten = conn.GetLastActivity().numActivePotential;
                Comparison<DistalDendrite> cmp = (dd1, dd2) => numPoten[dd1.GetIndex()] - numPoten[dd2.GetIndex()];

                var sortedSegments = new List<DistalDendrite>(matchingSegments);
                sortedSegments.Sort(cmp);

                DistalDendrite bestSegment = sortedSegments.Last();
                bestCell = bestSegment.GetParentCell();

                if (learn)
                {
                    AdaptSegment(conn, bestSegment, prevActiveCells, permanenceIncrement, permanenceDecrement);

                    int nGrowDesired = conn.GetMaxNewSynapseCount() - numPoten[bestSegment.GetIndex()];

                    if (nGrowDesired > 0)
                    {
                        GrowSynapses(conn, prevWinnerCells, bestSegment, conn.GetInitialPermanence(),
                            nGrowDesired, random);
                    }
                }
            }
            else
            {
                bestCell = LeastUsedCell(conn, cells, random);
                if (learn)
                {
                    int nGrowExact = Math.Min(conn.GetMaxNewSynapseCount(), prevWinnerCells.Count);
                    if (nGrowExact > 0)
                    {
                        DistalDendrite bestSegment = conn.CreateSegment(bestCell);
                        GrowSynapses(conn, prevWinnerCells, bestSegment, conn.GetInitialPermanence(),
                            nGrowExact, random);
                    }
                }
            }

            return new Tuple(cells, bestCell);
        }
        
        /// <summary>
        /// Punishes the Segments that incorrectly predicted a column to be active.
        /// 
        /// <p>
        /// <pre>
        /// Pseudocode:
        ///  for each matching segment in the column
        ///    weaken active synapses
        /// </pre>
        /// </p>
        /// </summary>
        /// <param name="conn">Connections instance for the tm</param>
        /// <param name="activeSegments">An iterable of <see cref="DistalDendrite"/> actives</param>
        /// <param name="matchingSegments">An iterable of <see cref="DistalDendrite"/> matching for the column compute is operating on that are matching; None if empty</param>
        /// <param name="prevActiveCells">Active cells in `t-1`</param>
        /// <param name="prevWinnerCells">Winner cells in `t-1`</param>
        /// <param name="predictedSegmentDecrement">Amount by which segments are punished for incorrect predictions</param>
        public void PunishPredictedColumn(Connections conn, List<DistalDendrite> activeSegments,
            List<DistalDendrite> matchingSegments, HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells,
               double predictedSegmentDecrement)
        {

            if (predictedSegmentDecrement > 0)
            {
                foreach (DistalDendrite segment in matchingSegments)
                {
                    AdaptSegment(conn, segment, prevActiveCells, -conn.GetPredictedSegmentDecrement(), 0);
                }
            }
        }

        #endregion

        #region Helper Methods

        ////////////////////////////
        //     Helper Methods     //
        ////////////////////////////

        /// <summary>
        /// Gets the cell with the smallest number of segments.
        /// Break ties randomly.
        /// </summary>
        /// <param name="conn">Connections instance for the tm</param>
        /// <param name="cells">List of <see cref="Cell"/>s</param>
        /// <param name="random">Random Number Generator</param>
        /// <returns>the least used <see cref="Cell"/></returns>
        internal Cell LeastUsedCell(Connections conn, IEnumerable<Cell> cells, IRandom random)
        {
            List<Cell> leastUsedCells = new List<Cell>();
            int minNumSegments = int.MaxValue;
            foreach (Cell cell in cells)
            {
                int numSegments = conn.GetNumSegments(cell);

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

            int i = random.NextInt(leastUsedCells.Count);
            return leastUsedCells[i];
        }

        /// <summary>
        /// Creates nDesiredNewSynapes synapses on the segment passed in if
        /// possible, choosing random cells from the previous winner cells that are
        /// not already on the segment.
        /// <p>
        /// <b>Notes:</b> The process of writing the last value into the index in the array
        /// that was most recently changed is to ensure the same results that we get
        /// in the c++ implementation using iter_swap with vectors.
        /// </p>
        /// </summary>
        /// <param name="conn">Connections instance for the tm</param>
        /// <param name="prevWinnerCells">Winner cells in `t-1`</param>
        /// <param name="segment">Segment to grow synapses on.   </param>
        /// <param name="initialPermanence">Initial permanence of a new synapse.</param>
        /// <param name="nDesiredNewSynapses">Desired number of synapses to grow</param>
        /// <param name="random">Random Number Generator</param>
        internal void GrowSynapses(Connections conn, HashSet<Cell> prevWinnerCells, DistalDendrite segment,
            double initialPermanence, int nDesiredNewSynapses, IRandom random)
        {

            List<Cell> candidates = new List<Cell>(prevWinnerCells);
            candidates.Sort();
            //Collections.sort(candidates);

            foreach (Synapse synapse in conn.GetSynapses(segment))
            {
                Cell presynapticCell = synapse.GetPresynapticCell();
                int index = candidates.IndexOf(presynapticCell);
                if (index != -1)
                {
                    candidates.RemoveAt(index);
                }
            }

            int candidatesLength = candidates.Count;
            int nActual = nDesiredNewSynapses < candidatesLength ? nDesiredNewSynapses : candidatesLength;

            for (int i = 0; i < nActual; i++)
            {
                int rand = random.NextInt(candidates.Count);
                conn.CreateSynapse(segment, candidates[rand], initialPermanence);
                candidates.RemoveAt(rand);
            }
        }

        /// <summary>
        /// Updates synapses on segment.
        /// Strengthens active synapses; weakens inactive synapses.
        /// </summary>
        /// <param name="conn">Connections instance for the tm</param>
        /// <param name="segment"><see cref="DistalDendrite"/> to adapt</param>
        /// <param name="prevActiveCells">Active <see cref="Cell"/>s in `t-1`</param>
        /// <param name="permanenceIncrement">Amount to increment active synapses </param>
        /// <param name="permanenceDecrement">Amount to decrement inactive synapses</param>
        internal void AdaptSegment(Connections conn, DistalDendrite segment, HashSet<Cell> prevActiveCells,
            double permanenceIncrement, double permanenceDecrement)
        {

            // Destroying a synapse modifies the set that we're iterating through.
            List<Synapse> synapsesToDestroy = new List<Synapse>();

            foreach (Synapse synapse in conn.GetSynapses(segment))
            {
                double permanence = synapse.GetPermanence();

                if (prevActiveCells.Contains(synapse.GetPresynapticCell()))
                {
                    permanence += permanenceIncrement;
                }
                else
                {
                    permanence -= permanenceDecrement;
                }

                // Keep permanence within min/max bounds
                permanence = permanence < 0 ? 0 : permanence > 1.0 ? 1.0 : permanence;

                // Use this to examine issues caused by subtle floating point differences
                // be careful to set the scale (1 below) to the max significant digits right of the decimal point
                // between the permanenceIncrement and initialPermanence
                //
                // permanence = new BigDecimal(permanence).setScale(1, RoundingMode.HALF_UP).doubleValue(); 

                if (permanence < Epsilon)
                {
                    synapsesToDestroy.Add(synapse);
                }
                else
                {
                    synapse.SetPermanence(conn, permanence);
                }
            }

            foreach (Synapse s in synapsesToDestroy)
            {
                conn.DestroySynapse(s);
            }

            if (conn.GetNumSynapses(segment) == 0)
            {
                conn.DestroySegment(segment);
            }
        }

        #endregion

        /// <summary>
        /// Used in the <see cref="Compute"/> method
        /// to make pulling values out of the <see cref="GroupBy2{R}"/> more readable and named.
        /// </summary>
        [Serializable]
        public class ColumnData // implements Serializable
        {
            private Tuple _t;

            public ColumnData() { }

            public ColumnData(Tuple t)
            {
                _t = t;
            }

            public Column Column() { return (Column)_t.Get(0); }

            public List<Column> ActiveColumns() { return (List<Column>)_t.Get(1); }

            public List<DistalDendrite> ActiveSegments()
            {
                var list = (IList) _t.Get(2);
                return list[0].Equals(GroupBy2<Column>.Slot<Tuple<object,Column>>.Empty()) ?
                     new List<DistalDendrite>() :
                         list.Cast<DistalDendrite>().ToList();
            }

            public List<DistalDendrite> MatchingSegments()
            {
                var list = (IList) _t.Get(3);
                return list[0].Equals(GroupBy2<Column>.Slot<Tuple<object, Column>>.Empty()) ?
                     new List<DistalDendrite>() :
                         list.Cast<DistalDendrite>().ToList();
            }

            public ColumnData Set(Tuple t) { _t = t; return this; }

            /// <summary>
            /// Returns a boolean flag indicating whether the slot contained by the
            /// tuple at the specified index is filled with the special empty
            /// indicator.
            /// </summary>
            /// <param name="memberIndex">the index of the tuple to assess.</param>
            /// <returns>true if <em><b>not</b></em> none, false if it <em><b>is none</b></em>.</returns>
            public bool IsNotNone(int memberIndex)
            {
                // return !((List<?>)t.get(memberIndex)).get(0).equals(NONE);
                var list = (IList)_t.Get(memberIndex);
                var element = list[0];

                return !(element.Equals(GroupBy2<Column>.Slot<Tuple<object, Column>>.NONE));
            }
        }
    }
}