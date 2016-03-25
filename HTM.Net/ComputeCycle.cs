using System.Collections.Generic;
using HTM.Net.Model;

namespace HTM.Net
{
    /**
     * Contains a snapshot of the state attained during one computational
     * call to the {@link TemporalMemory}. The {@code TemporalMemory} uses
     * data from previous compute cycles to derive new data for the current cycle
     * through a comparison between states of those different cycles, therefore
     * this state container is necessary.
     * 
     */
    public class ComputeCycle
    {
        internal HashSet<Cell> activeCells = new HashSet<Cell>();
        protected HashSet<Cell> winnerCells = new HashSet<Cell>();
        protected HashSet<Cell> predictiveCells = new HashSet<Cell>();
        protected HashSet<Cell> predictedInactiveCells = new HashSet<Cell>();
        protected HashSet<Cell> matchingCells = new HashSet<Cell>();
        protected HashSet<Column> successfullyPredictedColumns = new HashSet<Column>();
        protected HashSet<DistalDendrite> activeSegments = new HashSet<DistalDendrite>();
        protected HashSet<DistalDendrite> learningSegments = new HashSet<DistalDendrite>();
        protected HashSet<DistalDendrite> matchingSegments = new HashSet<DistalDendrite>();


        /**
         * Constructs a new {@code ComputeCycle}
         */
        public ComputeCycle() { }

        /**
         * Constructs a new {@code ComputeCycle} initialized with
         * the connections relevant to the current calling {@link Thread} for
         * the specified {@link TemporalMemory}
         * 
         * @param   c       the current connections state of the TemporalMemory
         */
        public ComputeCycle(Connections c)
        {
            activeCells = new HashSet<Cell>(c.GetActiveCells());
            winnerCells = new HashSet<Cell>(c.GetWinnerCells());
            predictiveCells = new HashSet<Cell>(c.GetPredictiveCells());
            successfullyPredictedColumns = new HashSet<Column>(c.GetSuccessfullyPredictedColumns());
            activeSegments = new HashSet<DistalDendrite>(c.GetActiveSegments());
            learningSegments = new HashSet<DistalDendrite>(c.GetLearningSegments());
        }

        /**
         * Returns the current {@link Set} of active cells
         * 
         * @return  the current {@link Set} of active cells
         */
        public HashSet<Cell> ActiveCells()
        {
            return activeCells;
        }

        /**
         * Returns the current {@link Set} of winner cells
         * 
         * @return  the current {@link Set} of winner cells
         */
        public HashSet<Cell> WinnerCells()
        {
            return winnerCells;
        }

        /**
         * Returns the {@link Set} of predictive cells.
         * @return
         */
        public HashSet<Cell> PredictiveCells()
        {
            return predictiveCells;
        }

        /**
         * Returns the {@link Set} of columns successfully predicted from t - 1.
         * 
         * @return  the current {@link Set} of predicted columns
         */
        public HashSet<Column> SuccessfullyPredictedColumns()
        {
            return successfullyPredictedColumns;
        }

        /**
         * Returns the Set of learning {@link DistalDendrite}s
         * @return
         */
        public HashSet<DistalDendrite> LearningSegments()
        {
            return learningSegments;
        }

        /**
         * Returns the Set of active {@link DistalDendrite}s
         * @return
         */
        public HashSet<DistalDendrite> ActiveSegments()
        {
            return activeSegments;
        }

        /**
         * Returns a Set of predicted inactive cells.
         * @return
         */
        public HashSet<Cell> PredictedInactiveCells()
        {
            return predictedInactiveCells;
        }

        /**
         * Returns the Set of matching {@link DistalDendrite}s from 
         * {@link TemporalMemory#computePredictiveCells(Connections, ComputeCycle, Map)}
         * @return
         */
        public HashSet<DistalDendrite> MatchingSegments()
        {
            return matchingSegments;
        }

        /**
         * Returns the Set of matching <see cref="Cell"/>s from
         * {@link TemporalMemory#computePredictiveCells(Connections, ComputeCycle, Map)}
         * @return
         */
        public HashSet<Cell> MatchingCells()
        {
            return matchingCells;
        }
    }
}