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
        /** simple serial version id */
        private const long serialVersionUID = 1L;

        private const double EPSILON = 0.00001;

        private const int ACTIVE_COLUMNS = 1;

        /**
         * Uses the specified {@link Connections} object to Build the structural 
         * anatomy needed by this {@code TemporalMemory} to implement its algorithms.
         * 
         * The connections object holds the {@link Column} and {@link Cell} infrastructure,
         * and is used by both the {@link SpatialPooler} and {@link TemporalMemory}. Either of
         * these can be used separately, and therefore this Connections object may have its
         * Columns and Cells initialized by either the init method of the SpatialPooler or the
         * init method of the TemporalMemory. We check for this so that complete initialization
         * of both Columns and Cells occurs, without either being redundant (initialized more than
         * once). However, {@link Cell}s only get created when initializing a TemporalMemory, because
         * they are not used by the SpatialPooler.
         * 
         * @param   c       {@link Connections} object
         */
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
            ComputeCycle cycle = new ComputeCycle();
            ActivateCells(connections, cycle, activeColumns, learn);
            ActivateDendrites(connections, cycle, learn);

            return cycle;
        }
        /**
	     * Calculate the active cells, using the current active columns and dendrite
         * segments. Grow and reinforce synapses.
         * 
         * <pre>
         * Pseudocode:
         *   for each column
         *     if column is active and has active distal dendrite segments
         *       call activatePredictedColumn
         *     if column is active and doesn't have active distal dendrite segments
         *       call burstColumn
         *     if column is inactive and has matching distal dendrite segments
         *       call punishPredictedColumn
         *      
         * </pre>
         * 
	     * @param conn                     
	     * @param activeColumnIndices
	     * @param learn
	     */
        public void ActivateCells(Connections conn, ComputeCycle cycle, int[] activeColumnIndices, bool learn)
        {
            ColumnData columnData = new ColumnData();

            HashSet<Cell> prevActiveCells = conn.GetActiveCells();
            HashSet<Cell> prevWinnerCells = conn.GetWinnerCells();

            List<Column> activeColumns = activeColumnIndices
                .OrderBy(i => i)
                .Select(i => conn.GetColumn(i))
                .ToList();

            Func<Column, Column> identity = c => c;
            Func<DistalDendrite, Column> segToCol = segment => segment.GetParentCell().GetColumn();

            //@SuppressWarnings({ "rawtypes" })
            GroupBy2<Column> grouper = GroupBy2<Column>.Of(
                new Tuple<List<object>, Func<object, Column>>(activeColumns.Cast<object>().ToList(), x=> identity((Column) x)),
                new Tuple<List<object>, Func<object, Column>>(new List<DistalDendrite>(conn.GetActiveSegments()).Cast<object>().ToList(), x => segToCol((DistalDendrite) x)),
                new Tuple<List<object>, Func<object, Column>>(new List<DistalDendrite>(conn.GetMatchingSegments()).Cast<object>().ToList(), x => segToCol((DistalDendrite) x)));

            double permanenceIncrement = conn.GetPermanenceIncrement();
            double permanenceDecrement = conn.GetPermanenceDecrement();

            foreach (Tuple t in grouper)
            {
                columnData = columnData.Set(t);

                if (columnData.IsNotNone(ACTIVE_COLUMNS))
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

        /**
         * Calculate dendrite segment activity, using the current active cells.
         * 
         * <pre>
         * Pseudocode:
         *   for each distal dendrite segment with activity >= activationThreshold
         *     mark the segment as active
         *   for each distal dendrite segment with unconnected activity >= minThreshold
         *     mark the segment as matching
         * </pre>
         * 
         * @param conn     the Connectivity
         * @param cycle    Stores current compute cycle results
         * @param learn    If true, segment activations will be recorded. This information is used
         *                 during segment cleanup.
         */
        public void ActivateDendrites(Connections conn, ComputeCycle cycle, bool learn)
        {
            Connections.Activity activity = conn.ComputeActivity(cycle.activeCells, conn.GetConnectedPermanence());

            List<DistalDendrite> activeSegments = ArrayUtils.Range(0, activity.numActiveConnected.Length)
                .Where(i => activity.numActiveConnected[i] >= conn.GetActivationThreshold())
                .Select(i => conn.GetSegmentForFlatIdx(i))
                .ToList();

            List<DistalDendrite> matchingSegments = ArrayUtils.Range(0, activity.numActiveConnected.Length)
                .Where(i => activity.numActivePotential[i] >= conn.GetMinThreshold())
                .Select(i => conn.GetSegmentForFlatIdx(i))
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
                activeSegments.ForEach(s => conn.RecordSegmentActivity(s));
                conn.StartNewIteration();
            }
        }

        /**
         * Indicates the start of a new sequence. Clears any predictions and makes sure
         * synapses don't grow to the currently active cells in the next time step.
         */
        public void Reset(Connections connections)
        {
            connections.GetActiveCells().Clear();
            connections.GetWinnerCells().Clear();
            connections.GetActiveSegments().Clear();
            connections.GetMatchingSegments().Clear();
        }

        /**
         * Determines which cells in a predicted column should be added to winner cells
         * list, and learns on the segments that correctly predicted this column.
         * 
         * @param conn                 the connections
         * @param activeSegments       Active segments in the specified column
         * @param matchingSegments     Matching segments in the specified column
         * @param prevActiveCells      Active cells in `t-1`
         * @param prevWinnerCells      Winner cells in `t-1`
         * @param learn                If true, grow and reinforce synapses
         * 
         * <pre>
         * Pseudocode:
         *   for each cell in the column that has an active distal dendrite segment
         *     mark the cell as active
         *     mark the cell as a winner cell
         *     (learning) for each active distal dendrite segment
         *       strengthen active synapses
         *       weaken inactive synapses
         *       grow synapses to previous winner cells
         * </pre>
         * 
         * @return A list of predicted cells that will be added to active cells and winner
         *         cells.
         */
        public List<Cell> ActivatePredictedColumn(Connections conn, List<DistalDendrite> activeSegments,
            List<DistalDendrite> matchingSegments, HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells,
                double permanenceIncrement, double permanenceDecrement, bool learn)
        {

            List<Cell> cellsToAdd = new List<Cell>();
            Cell previousCell = null;
            Cell currCell;
            foreach (DistalDendrite segment in activeSegments)
            {
                if ((currCell = segment.GetParentCell()) != previousCell)
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

        /**
         * Activates all of the cells in an unpredicted active column,
         * chooses a winner cell, and, if learning is turned on, either adapts or
         * creates a segment. growSynapses is invoked on this segment.
         * </p><p>
         * <b>Pseudocode:</b>
         * </p><p>
         * <pre>
         *  mark all cells as active
         *  if there are any matching distal dendrite segments
         *      find the most active matching segment
         *      mark its cell as a winner cell
         *      (learning)
         *      grow and reinforce synapses to previous winner cells
         *  else
         *      find the cell with the least segments, mark it as a winner cell
         *      (learning)
         *      (optimization) if there are previous winner cells
         *          add a segment to this winner cell
         *          grow synapses to previous winner cells
         * </pre>
         * </p>
         * 
         * @param conn                      Connections instance for the TM
         * @param column                    Bursting {@link Column}
         * @param matchingSegments          List of matching {@link DistalDendrite}s
         * @param prevActiveCells           Active cells in `t-1`
         * @param prevWinnerCells           Winner cells in `t-1`
         * @param permanenceIncrement       Amount by which permanences of synapses
         *                                  are decremented during learning
         * @param permanenceDecrement       Amount by which permanences of synapses
         *                                  are incremented during learning
         * @param random                    Random number generator
         * @param learn                     Whether or not learning is enabled
         * 
         * @return  Tuple containing:
         *                  cells       list of the processed column's cells
         *                  bestCell    the best cell
         */
        public Tuple BurstColumn(Connections conn, Column column, List<DistalDendrite> matchingSegments,
            HashSet<Cell> prevActiveCells, HashSet<Cell> prevWinnerCells, double permanenceIncrement, double permanenceDecrement,
                IRandom random, bool learn)
        {

            IList<Cell> cells = column.GetCells();
            Cell bestCell = null;

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

        /**
         * Punishes the Segments that incorrectly predicted a column to be active.
         * 
         * <p>
         * <pre>
         * Pseudocode:
         *  for each matching segment in the column
         *    weaken active synapses
         * </pre>
         * </p>
         *   
         * @param conn                              Connections instance for the tm
         * @param activeSegments                    An iterable of {@link DistalDendrite} actives
         * @param matchingSegments                  An iterable of {@link DistalDendrite} matching
         *                                          for the column compute is operating on
         *                                          that are matching; None if empty
         * @param prevActiveCells                   Active cells in `t-1`
         * @param prevWinnerCells                   Winner cells in `t-1`
         *                                          are decremented during learning.
         * @param predictedSegmentDecrement         Amount by which segments are punished for incorrect predictions
         */
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


        ////////////////////////////
        //     Helper Methods     //
        ////////////////////////////

        /**
         * Gets the cell with the smallest number of segments.
         * Break ties randomly.
         * 
         * @param conn      Connections instance for the tm
         * @param cells     List of {@link Cell}s
         * @param random    Random Number Generator
         * 
         * @return  the least used {@code Cell}
         */
        public Cell LeastUsedCell(Connections conn, IEnumerable<Cell> cells, IRandom random)
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

        /**
         * Creates nDesiredNewSynapes synapses on the segment passed in if
         * possible, choosing random cells from the previous winner cells that are
         * not already on the segment.
         * <p>
         * <b>Notes:</b> The process of writing the last value into the index in the array
         * that was most recently changed is to ensure the same results that we get
         * in the c++ implementation using iter_swap with vectors.
         * </p>
         * 
         * @param conn                      Connections instance for the tm
         * @param prevWinnerCells           Winner cells in `t-1`
         * @param segment                   Segment to grow synapses on.     
         * @param initialPermanence         Initial permanence of a new synapse.
         * @param nDesiredNewSynapses       Desired number of synapses to grow
         * @param random                    Tm object used to generate random
         *                                  numbers
         */
        public void GrowSynapses(Connections conn, HashSet<Cell> prevWinnerCells, DistalDendrite segment,
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

        /**
         * Updates synapses on segment.
         * Strengthens active synapses; weakens inactive synapses.
         *  
         * @param conn                      {@link Connections} instance for the tm
         * @param segment                   {@link DistalDendrite} to adapt
         * @param prevActiveCells           Active {@link Cell}s in `t-1`
         * @param permanenceIncrement       Amount to increment active synapses    
         * @param permanenceDecrement       Amount to decrement inactive synapses
         */
        public void AdaptSegment(Connections conn, DistalDendrite segment, HashSet<Cell> prevActiveCells,
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

                if (permanence < EPSILON)
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

        /**
         * Used in the {@link TemporalMemory#compute(Connections, int[], boolean)} method
         * to make pulling values out of the {@link GroupBy2} more readable and named.
         */
        [Serializable]
        public class ColumnData // implements Serializable
        {
            /** Default Serial */
            private const long serialVersionUID = 1L;
            Tuple t;

            public ColumnData() { }

            public ColumnData(Tuple t)
            {
                this.t = t;
            }

            public Column Column() { return (Column)t.Get(0); }
            public List<Column> ActiveColumns() { return (List<Column>)t.Get(1); }

            public List<DistalDendrite> ActiveSegments()
            {
                var list = (IList) t.Get(2);
                return list[0].Equals(GroupBy2<Column>.Slot<Tuple<object,Column>>.Empty()) ?
                     new List<DistalDendrite>() :
                         list.Cast<DistalDendrite>().ToList();
            }

            public List<DistalDendrite> MatchingSegments()
            {
                var list = (IList) t.Get(3);
                return list[0].Equals(GroupBy2<Column>.Slot<Tuple<object, Column>>.Empty()) ?
                     new List<DistalDendrite>() :
                         list.Cast<DistalDendrite>().ToList();
            }

            public ColumnData Set(Tuple t) { this.t = t; return this; }

            /**
             * Returns a boolean flag indicating whether the slot contained by the
             * tuple at the specified index is filled with the special empty
             * indicator.
             * 
             * @param memberIndex   the index of the tuple to assess.
             * @return  true if <em><b>not</b></em> none, false if it <em><b>is none</b></em>.
             */
            public bool IsNotNone(int memberIndex)
            {
                // return !((List<?>)t.get(memberIndex)).get(0).equals(NONE);
                var list = (IList)t.Get(memberIndex);
                var element = list[0];

                return !((IList)t.Get(memberIndex))[0].Equals(GroupBy2<Column>.Slot<Tuple<object, Column>>.NONE);
            }
        }
    }
}