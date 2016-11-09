using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Model
{
    /**
     * Contains a snapshot of the state attained during one computational
     * call to the {@link TemporalMemory}. The {@code TemporalMemory} uses
     * data from previous compute cycles to derive new data for the current cycle
     * through a comparison between states of those different cycles, therefore
     * this state container is necessary.
     * 
     */
    [Serializable]
    public class ComputeCycle : Persistable
    {
        private const long serialVersionUID = 1L;

        public HashSet<Cell> activeCells = new HashSet<Cell>();
        public HashSet<Cell> winnerCells = new HashSet<Cell>();
        public List<DistalDendrite> activeSegments = new List<DistalDendrite>();
        public List<DistalDendrite> matchingSegments = new List<DistalDendrite>();
        public HashSet<Cell> predictiveCells = new HashSet<Cell>();


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
            this.activeCells = new HashSet<Cell>(c.GetActiveCells());
            this.winnerCells = new HashSet<Cell>(c.GetWinnerCells());
            this.predictiveCells = new HashSet<Cell>(c.GetPredictiveCells());
            this.activeSegments = new List<DistalDendrite>(c.GetActiveSegments());
            this.matchingSegments = new List<DistalDendrite>(c.GetMatchingSegments());
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
         * Returns the {@link List} of sorted predictive cells.
         * @return
         */
        public HashSet<Cell> PredictiveCells()
        {
            if (!predictiveCells.Any())
            {
                Cell previousCell = null;
                Cell currCell = null;

                foreach (DistalDendrite activeSegment in activeSegments)
                {
                    if ((currCell = activeSegment.GetParentCell()) != previousCell)
                    {
                        predictiveCells.Add(previousCell = currCell);
                    }
                }
            }
            return predictiveCells;
        }

        /* (non-Javadoc)
         * @see java.lang.Object#hashCode()
         */
        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((activeCells == null) ? 0 : activeCells.GetHashCode());
            result = prime * result + ((predictiveCells == null) ? 0 : predictiveCells.GetHashCode());
            result = prime * result + ((winnerCells == null) ? 0 : winnerCells.GetHashCode());
            result = prime * result + ((activeSegments == null) ? 0 : activeSegments.GetHashCode());
            result = prime * result + ((matchingSegments == null) ? 0 : matchingSegments.GetHashCode());
            return result;
        }

        /* (non-Javadoc)
         * @see java.lang.Object#equals(java.lang.Object)
         */
        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            ComputeCycle other = (ComputeCycle)obj;
            if (activeCells == null)
            {
                if (other.activeCells != null)
                    return false;
            }
            else if (!activeCells.Equals(other.activeCells))
                return false;
            if (predictiveCells == null)
            {
                if (other.predictiveCells != null)
                    return false;
            }
            else if (!predictiveCells.Equals(other.predictiveCells))
                return false;
            if (winnerCells == null)
            {
                if (other.winnerCells != null)
                    return false;
            }
            else if (!winnerCells.Equals(other.winnerCells))
                return false;
            if (activeSegments == null)
            {
                if (other.activeSegments != null)
                    return false;
            }
            else if (!activeSegments.Equals(other.activeSegments))
                return false;
            if (matchingSegments == null)
            {
                if (other.matchingSegments != null)
                    return false;
            }
            else if (!matchingSegments.Equals(other.matchingSegments))
                return false;
            return true;
        }
    }
}