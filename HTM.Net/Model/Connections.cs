using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace HTM.Net.Model
{
    /// <summary>
    /// Contains the definition of the interconnected structural state of the <see cref="SpatialPooler"/> and
    /// <see cref="TemporalMemory"/> as well as the state of all support structures
    /// (i.e. Cells, Columns, Segments, Synapses etc.).
    /// 
    /// In the separation of data from logic, this class represents the data/state.
    /// </summary>
    public class Connections
    {
        /** keep it simple */
        private const long serialVersionUID = 1L;

        private const double EPSILON = 0.00001;

        /////////////////////////////////////// Spatial Pooler Vars ///////////////////////////////////////////
        /** <b>WARNING:</b> potentialRadius **must** be set to 
         * the inputWidth if using "globalInhibition" and if not 
         * using the Network API (which sets this automatically) 
         */
        private int potentialRadius = 16;
        private double potentialPct = 0.5;
        private bool globalInhibition = false;
        private double localAreaDensity = -1.0;
        private double numActiveColumnsPerInhArea;
        private double stimulusThreshold = 0;
        private double synPermInactiveDec = 0.008;
        private double synPermActiveInc = 0.05;
        private double synPermConnected = 0.10;
        private double synPermBelowStimulusInc = 0.10/10.0; //synPermConnected / 10.0;
        private double minPctOverlapDutyCycles = 0.001;
        private double minPctActiveDutyCycles = 0.001;
        private double predictedSegmentDecrement = 0.0;
        private int dutyCyclePeriod = 1000;
        private double maxBoost = 10.0;
        private bool wrapAround = true;

        private int numInputs = 1;  //product of input dimensions
        private int numColumns = 1; //product of column dimensions

        //Extra parameter settings
        private double synPermMin = 0.0;
        private double synPermMax = 1.0;
        private double synPermTrimThreshold = 0.05/2.0;//synPermActiveInc / 2.0;
        private int updatePeriod = 50;
        private double initConnectedPct = 0.5;

        //Internal state
        private double version = 1.0;
        public int spIterationNum = 0;
        public int spIterationLearnNum = 0;
        public long tmIteration = 0;

        public double[] boostedOverlaps;
        public int[] overlaps;

        /** Manages input neighborhood transformations */
        private Topology inputTopology;
        /** Manages column neighborhood transformations */
        private Topology columnTopology;
        /** A matrix representing the shape of the input. */
        protected ISparseMatrix inputMatrix;
        /**
         * Store the set of all inputs that are within each column's potential pool.
         * 'potentialPools' is a matrix, whose rows represent cortical columns, and
         * whose columns represent the input bits. if potentialPools[i][j] == 1,
         * then input bit 'j' is in column 'i's potential pool. A column can only be
         * connected to inputs in its potential pool. The indices refer to a
         * flattened version of both the inputs and columns. Namely, irrespective
         * of the topology of the inputs and columns, they are treated as being a
         * one dimensional array. Since a column is typically connected to only a
         * subset of the inputs, many of the entries in the matrix are 0. Therefore
         * the potentialPool matrix is stored using the SparseObjectMatrix
         * class, to reduce memory footprint and computation time of algorithms that
         * require iterating over the data structure.
         */
        private IFlatMatrix<Pool> potentialPools;
        /**
         * Initialize a tiny random tie breaker. This is used to determine winning
         * columns where the overlaps are identical.
         */
        private double[] tieBreaker;
        /**
         * Stores the number of connected synapses for each column. This is simply
         * a sum of each row of 'connectedSynapses'. again, while this
         * information is readily available from 'connectedSynapses', it is
         * stored separately for efficiency purposes.
         */
        private Matrix<double> connectedCounts;
        /**
         * The inhibition radius determines the size of a column's local
         * neighborhood. of a column. A cortical column must overcome the overlap
         * score of columns in its neighborhood in order to become actives. This
         * radius is updated every learning round. It grows and shrinks with the
         * average number of connected synapses per column.
         */
        private int inhibitionRadius = 0;

        private double[] overlapDutyCycles;
        private double[] activeDutyCycles;
        private volatile double[] minOverlapDutyCycles;
        private volatile double[] minActiveDutyCycles;
        private double[] boostFactors;

        /////////////////////////////////////// Temporal Memory Vars ///////////////////////////////////////////

        protected HashSet<Cell> activeCells = new HashSet<Cell>();
        protected HashSet<Cell> winnerCells = new HashSet<Cell>();
        protected HashSet<Cell> predictiveCells = new HashSet<Cell>();
        protected List<DistalDendrite> activeSegments = new List<DistalDendrite>();
        protected List<DistalDendrite> matchingSegments = new List<DistalDendrite>();

        /** Total number of columns */
        protected int[] columnDimensions = new int[] { 2048 };
        /** Total number of cells per column */
        protected int cellsPerColumn = 32;
        /** What will comprise the Layer input. Input (i.e. from encoder) */
        protected int[] inputDimensions = new int[] { 32, 32 };
        /**
         * If the number of active connected synapses on a segment
         * is at least this threshold, the segment is said to be active.
         */
        private int activationThreshold = 13;
        /**
         * Radius around cell from which it can
         * sample to form distal {@link DistalDendrite} connections.
         */
        private int learningRadius = 2048;
        /**
         * If the number of synapses active on a segment is at least this
         * threshold, it is selected as the best matching
         * cell in a bursting column.
         */
        private int minThreshold = 10;
        /** The maximum number of synapses added to a segment during learning. */
        private int maxNewSynapseCount = 20;
        /** The maximum number of segments (distal dendrites) allowed on a cell */
        private int maxSegmentsPerCell = 255;
        /** The maximum number of synapses allowed on a given segment (distal dendrite) */
        private int maxSynapsesPerSegment = 255;
        /** Initial permanence of a new synapse */
        private double initialPermanence = 0.21;
        /**
         * If the permanence value for a synapse
         * is greater than this value, it is said
         * to be connected.
         */
        private double connectedPermanence = 0.50;
        /**
         * Amount by which permanences of synapses
         * are incremented during learning.
         */
        private double permanenceIncrement = 0.10;
        /**
         * Amount by which permanences of synapses
         * are decremented during learning.
         */
        private double permanenceDecrement = 0.10;

        /** The main data structure containing columns, cells, and synapses */
        private SparseObjectMatrix<Column> memory;

        private Cell[] cells;

        ///////////////////////   Structural Elements /////////////////////////
        /** Reverse mapping from source cell to {@link Synapse} */
        public Map<Cell, HashSet<Synapse>> receptorSynapses;

        protected Map<Cell, List<DistalDendrite>> segments;
        public Map<Segment, List<Synapse>> distalSynapses;
        protected Map<Segment, List<Synapse>> proximalSynapses;

        /** Helps index each new proximal Synapse */
        protected int proximalSynapseCounter = -1;
        /** Global tracker of the next available segment index */
        protected int nextFlatIdx;
        /** Global counter incremented for each DD segment creation*/
        protected int nextSegmentOrdinal;
        /** Global counter incremented for each DD synapse creation*/
        protected int nextSynapseOrdinal;
        /** Total number of synapses */
        protected long numSynapses;
        /** Used for recycling {@link DistalDendrite} indexes */
        protected List<int> freeFlatIdxs = new List<int>();
        /** Indexed segments by their global index (can contain nulls) */
        protected List<DistalDendrite> segmentForFlatIdx = new List<DistalDendrite>();
        /** Stores each cycle's most recent activity */
        public Activity lastActivity;
        /** The default random number seed */
        protected int seed = 42;
        /** The random number generator */
        public IRandom random = new MersenneTwister(42);

        /** Sorting Lambda used for sorting active and matching segments */
        public Comparison<DistalDendrite> segmentPositionSortKey;

        /** Sorting Lambda used for SpatialPooler inhibition */
        public Comparison<Tuple<int, Double>> inhibitionComparator;

        ////////////////////////////////////////
        //       Connections Constructor      //
        ////////////////////////////////////////
        /**
         * Constructs a new {@code OldConnections} object. This object
         * is usually configured via the {@link Parameters#apply(Object)}
         * method.
         */

        public Connections()
        {
            segmentPositionSortKey = (s1, s2) =>
            {
                double c1 = s1.GetParentCell().GetIndex() + ((double)(s1.GetOrdinal() / (double)nextSegmentOrdinal));
                double c2 = s2.GetParentCell().GetIndex() + ((double)(s2.GetOrdinal() / (double)nextSegmentOrdinal));
                return c1 == c2 ? 0 : c1 > c2 ? 1 : -1;
            };

            inhibitionComparator = (p1, p2) =>
            {
                int p1key = p1.Item1;
                int p2key = p2.Item1;
                double p1val = p1.Item2;
                double p2val = p2.Item2;
                if (Math.Abs(p2val - p1val) < 0.000000001)
                {
                    return Math.Abs(p2key - p1key) < 0.000000001 ? 0 : p2key > p1key ? -1 : 1;
                }
                else
                {
                    return p2val > p1val ? -1 : 1;
                }
            };
        }

        /**
         * Returns a deep copy of this {@code Connections} object.
         * @return a deep copy of this {@code Connections}
         */
        public Connections Copy()
        {
            throw new NotImplementedException();
            //PersistenceAPI api = Persistence.Get(new SerialConfig());
            //byte[] myBytes = api.serializer().serialize(this);
            //return api.serializer().deSerialize(myBytes);
        }

        /**
         * Sets the derived values of the {@link SpatialPooler}'s initialization.
         */
        public void DoSpatialPoolerPostInit()
        {
            synPermBelowStimulusInc = synPermConnected / 10.0;
            synPermTrimThreshold = synPermActiveInc / 2.0;
            if (potentialRadius == -1)
            {
                potentialRadius = ArrayUtils.Product(inputDimensions);
            }
        }

        /////////////////////////////////////////
        //         General Methods             //
        /////////////////////////////////////////
        /**
         * Sets the seed used for the internal random number generator.
         * If the generator has been instantiated, this method will initialize
         * a new random generator with the specified seed.
         *
         * @param seed
         */
        public void SetSeed(int seed)
        {
            this.seed = seed;
        }

        /**
         * Returns the configured random number seed
         * @return
         */
        public int GetSeed()
        {
            return seed;
        }

        /**
         * Returns the thread specific {@link Random} number generator.
         * @return
         */
        public IRandom GetRandom()
        {
            return random;
        }

        /**
         * Sets the random number generator.
         * @param random
         */
        public void SetRandom(IRandom random)
        {
            this.random = random;
        }

        /**
         * Returns the {@link Cell} specified by the index passed in.
         * @param index     of the specified cell to return.
         * @return
         */
        public Cell GetCell(int index)
        {
            return cells[index];
        }

        /**
         * Returns an array containing all of the {@link Cell}s.
         * @return
         */
        public Cell[] GetCells()
        {
            return cells;
        }

        /**
         * Sets the flat array of cells
         * @param cells
         */
        public void SetCells(Cell[] cells)
        {
            this.cells = cells;
        }

        /**
         * Returns an array containing the {@link Cell}s specified
         * by the passed in indexes.
         *
         * @param cellIndexes   indexes of the Cells to return
         * @return
         */
        public Cell[] GetCells(params int[] cellIndexes)
        {
            Cell[] retVal = new Cell[cellIndexes.Length];
            for (int i = 0; i < cellIndexes.Length; i++)
            {
                retVal[i] = cells[cellIndexes[i]];
            }
            return retVal;
        }

        /**
         * Returns a {@link LinkedHashSet} containing the {@link Cell}s specified
         * by the passed in indexes.
         *
         * @param cellIndexes   indexes of the Cells to return
         * @return
         */
        public HashSet<Cell> GetCellSet(params int[] cellIndexes)
        {
            HashSet<Cell> retVal = new HashSet<Cell>(/*cellIndexes.Length*/);

            for (int i = 0; i < cellIndexes.Length; i++)
            {
                retVal.Add(cells[cellIndexes[i]]);
            }
            return retVal;
        }

        /**
         * Sets the matrix containing the {@link Column}s
         * @param mem
         */
        public void SetMemory(SparseObjectMatrix<Column> mem)
        {
            this.memory = mem;
        }

        /**
         * Returns the matrix containing the {@link Column}s
         * @return
         */
        public SparseObjectMatrix<Column> GetMemory()
        {
            return memory;
        }

        /**
         * Returns the {@link Topology} overseeing input 
         * neighborhoods.
         * @return 
         */
        public Topology GetInputTopology()
        {
            return inputTopology;
        }

        /**
         * Sets the {@link Topology} overseeing input 
         * neighborhoods.
         * 
         * @param topology  the input Topology
         */
        public void SetInputTopology(Topology topology)
        {
            this.inputTopology = topology;
        }

        /**
         * Returns the {@link Topology} overseeing {@link Column} 
         * neighborhoods.
         * @return
         */
        public Topology GetColumnTopology()
        {
            return columnTopology;
        }

        /**
         * Sets the {@link Topology} overseeing {@link Column} 
         * neighborhoods.
         * 
         * @param topology  the column Topology
         */
        public void SetColumnTopology(Topology topology)
        {
            this.columnTopology = topology;
        }

        /**
         * Returns the input column mapping
         */
        public ISparseMatrix GetInputMatrix()
        {
            return inputMatrix;
        }

        /**
         * Sets the input column mapping matrix
         * @param matrix
         */
        public void SetInputMatrix(ISparseMatrix matrix)
        {
            this.inputMatrix = matrix;
        }

        ////////////////////////////////////////
        //       SpatialPooler Methods        //
        ////////////////////////////////////////
        /**
         * Returns the configured initial connected percent.
         * @return
         */
        public double GetInitConnectedPct()
        {
            return this.initConnectedPct;
        }

        /**
         * Returns the cycle count.
         * @return
         */
        public int GetIterationNum()
        {
            return spIterationNum;
        }

        /**
         * Sets the iteration count.
         * @param num
         */
        public void SetIterationNum(int num)
        {
            this.spIterationNum = num;
        }

        /**
         * Returns the period count which is the number of cycles
         * between meta information updates.
         * @return
         */
        public int GetUpdatePeriod()
        {
            return updatePeriod;
        }

        /**
         * Sets the update period
         * @param period
         */
        public void SetUpdatePeriod(int period)
        {
            this.updatePeriod = period;
        }

        /**
         * Returns the inhibition radius
         * @return
         */
        public int GetInhibitionRadius()
        {
            return inhibitionRadius;
        }

        /**
         * Sets the inhibition radius
         * @param radius
         */
        public void SetInhibitionRadius(int radius)
        {
            this.inhibitionRadius = radius;
        }

        /**
         * Returns the product of the input dimensions
         * @return  the product of the input dimensions
         */
        public int GetNumInputs()
        {
            return numInputs;
        }

        /**
         * Sets the product of the input dimensions to
         * establish a flat count of bits in the input field.
         * @param n
         */
        public void SetNumInputs(int n)
        {
            this.numInputs = n;
        }

        /**
         * Returns the product of the column dimensions
         * @return  the product of the column dimensions
         */
        public int GetNumColumns()
        {
            return numColumns;
        }

        /**
         * Sets the product of the column dimensions to be
         * the column count.
         * @param n
         */
        public void SetNumColumns(int n)
        {
            this.numColumns = n;
        }

        /**
         * This parameter determines the extent of the input
         * that each column can potentially be connected to.
         * This can be thought of as the input bits that
         * are visible to each column, or a 'receptiveField' of
         * the field of vision. A large enough value will result
         * in 'global coverage', meaning that each column
         * can potentially be connected to every input bit. This
         * parameter defines a square (or hyper square) area: a
         * column will have a max square potential pool with
         * sides of length 2 * potentialRadius + 1.
         * 
         * <b>WARNING:</b> potentialRadius **must** be set to 
         * the inputWidth if using "globalInhibition" and if not 
         * using the Network API (which sets this automatically) 
         *
         *
         * @param potentialRadius
         */
        public void SetPotentialRadius(int potentialRadius)
        {
            this.potentialRadius = potentialRadius;
        }

        /**
         * Returns the configured potential radius
         * 
         * @return  the configured potential radius
         * @see setPotentialRadius
         */
        public int GetPotentialRadius()
        {
            return potentialRadius;
        }

        /**
         * The percent of the inputs, within a column's
         * potential radius, that a column can be connected to.
         * If set to 1, the column will be connected to every
         * input within its potential radius. This parameter is
         * used to give each column a unique potential pool when
         * a large potentialRadius causes overlap between the
         * columns. At initialization time we choose
         * ((2*potentialRadius + 1)^(# inputDimensions) *
         * potentialPct) input bits to comprise the column's
         * potential pool.
         *
         * @param potentialPct
         */
        public void SetPotentialPct(double potentialPct)
        {
            this.potentialPct = potentialPct;
        }

        /**
         * Returns the configured potential pct
         *
         * @return the configured potential pct
         * @see setPotentialPct
         */
        public double GetPotentialPct()
        {
            return potentialPct;
        }

        /**
         * Sets the {@link SparseObjectMatrix} which represents the
         * proximal dendrite permanence values.
         *
         * @param s the {@link SparseObjectMatrix}
         */
        public void SetProximalPermanences(SparseObjectMatrix<double[]> s)
        {
            foreach (int idx in s.GetSparseIndices())
            {
                memory.GetObject(idx).SetProximalPermanences(this, s.GetObject(idx));
            }
        }

        /**
         * Returns the count of {@link Synapse}s on
         * {@link ProximalDendrite}s
         * @return
         */
        public int GetProximalSynapseCount()
        {
            return proximalSynapseCounter + 1;
        }

        /**
         * Sets the count of {@link Synapse}s on
         * {@link ProximalDendrite}s
         * @param i
         */
        public void SetProximalSynapseCount(int i)
        {
            this.proximalSynapseCounter = i;
        }

        /**
         * Increments and returns the incremented
         * proximal {@link Synapse} count.
         *
         * @return
         */
        public int IncrementProximalSynapses()
        {
            return ++proximalSynapseCounter;
        }

        /**
         * Decrements and returns the decremented
         * proximal {link Synapse} count
         * @return
         */
        public int DecrementProximalSynapses()
        {
            return --proximalSynapseCounter;
        }

        /**
         * Returns the indexed count of connected synapses per column.
         * @return
         */
        public Matrix<double> GetConnectedCounts()
        {
            return connectedCounts;
        }

        /**
         * Returns the connected count for the specified column.
         * @param columnIndex
         * @return
         */
        public int GetConnectedCount(int columnIndex)
        {
            return (int)connectedCounts.Row(columnIndex).Sum();
            //return connectedCounts.GetTrueCount(columnIndex);
        }

        /**
         * Sets the indexed count of synapses connected at the columns in each index.
         * @param counts
         */
        //public void SetConnectedCounts(int[] counts)
        //{
        //    for (int i = 0; i < counts.Length; i++)
        //    {
        //        connectedCounts.SetTrueCount(i, counts[i]);
        //    }
        //}

        /**
         * Sets the connected count {@link AbstractSparseBinaryMatrix}
         * @param columnIndex
         * @param count
         */
        public void SetConnectedMatrix(Matrix<double> matrix)
        {
            this.connectedCounts = matrix;
        }

        /**
         * Sets the array holding the random noise added to proximal dendrite overlaps.
         *
         * @param tieBreaker	random values to help break ties
         */
        public void SetTieBreaker(double[] tieBreaker)
        {
            this.tieBreaker = tieBreaker;
        }

        /**
         * Returns the array holding random values used to add to overlap scores
         * to break ties.
         *
         * @return
         */
        public double[] GetTieBreaker()
        {
            return tieBreaker;
        }

        /**
         * If true, then during inhibition phase the winning
         * columns are selected as the most active columns from
         * the region as a whole. Otherwise, the winning columns
         * are selected with respect to their local
         * neighborhoods. Using global inhibition boosts
         * performance x60.
         *
         * @param globalInhibition
         */
        public void SetGlobalInhibition(bool globalInhibition)
        {
            this.globalInhibition = globalInhibition;
        }

        /**
         * Returns the configured global inhibition flag
         * @return  the configured global inhibition flag
         *
         * @see setGlobalInhibition
         */
        public bool GetGlobalInhibition()
        {
            return globalInhibition;
        }

        /**
         * The desired density of active columns within a local
         * inhibition area (the size of which is set by the
         * internally calculated inhibitionRadius, which is in
         * turn determined from the average size of the
         * connected potential pools of all columns). The
         * inhibition logic will insure that at most N columns
         * remain ON within a local inhibition area, where N =
         * localAreaDensity * (total number of columns in
         * inhibition area).
         *
         * @param localAreaDensity
         */
        public void SetLocalAreaDensity(double localAreaDensity)
        {
            this.localAreaDensity = localAreaDensity;
        }

        /**
         * Returns the configured local area density
         * @return  the configured local area density
         * @see setLocalAreaDensity
         */
        public double GetLocalAreaDensity()
        {
            return localAreaDensity;
        }

        /**
         * An alternate way to control the density of the active
         * columns. If numActivePerInhArea is specified then
         * localAreaDensity must be less than 0, and vice versa.
         * When using numActivePerInhArea, the inhibition logic
         * will insure that at most 'numActivePerInhArea'
         * columns remain ON within a local inhibition area (the
         * size of which is set by the internally calculated
         * inhibitionRadius, which is in turn determined from
         * the average size of the connected receptive fields of
         * all columns). When using this method, as columns
         * learn and grow their effective receptive fields, the
         * inhibitionRadius will grow, and hence the net density
         * of the active columns will *decrease*. This is in
         * contrast to the localAreaDensity method, which keeps
         * the density of active columns the same regardless of
         * the size of their receptive fields.
         *
         * @param numActiveColumnsPerInhArea
         */
        public void SetNumActiveColumnsPerInhArea(double numActiveColumnsPerInhArea)
        {
            this.numActiveColumnsPerInhArea = numActiveColumnsPerInhArea;
        }

        /**
         * Returns the configured number of active columns per
         * inhibition area.
         * @return  the configured number of active columns per
         * inhibition area.
         * @see setNumActiveColumnsPerInhArea
         */
        public double GetNumActiveColumnsPerInhArea()
        {
            return numActiveColumnsPerInhArea;
        }

        /**
         * This is a number specifying the minimum number of
         * synapses that must be on in order for a columns to
         * turn ON. The purpose of this is to prevent noise
         * input from activating columns. Specified as a percent
         * of a fully grown synapse.
         *
         * @param stimulusThreshold
         */
        public void SetStimulusThreshold(double stimulusThreshold)
        {
            this.stimulusThreshold = stimulusThreshold;
        }

        /**
         * Returns the stimulus threshold
         * @return  the stimulus threshold
         * @see setStimulusThreshold
         */
        public double GetStimulusThreshold()
        {
            return stimulusThreshold;
        }

        /**
         * The amount by which an inactive synapse is
         * decremented in each round. Specified as a percent of
         * a fully grown synapse.
         *
         * @param synPermInactiveDec
         */
        public void SetSynPermInactiveDec(double synPermInactiveDec)
        {
            this.synPermInactiveDec = synPermInactiveDec;
        }

        /**
         * Returns the synaptic permanence inactive decrement.
         * @return  the synaptic permanence inactive decrement.
         * @see setSynPermInactiveDec
         */
        public double GetSynPermInactiveDec()
        {
            return synPermInactiveDec;
        }

        /**
         * The amount by which an active synapse is incremented
         * in each round. Specified as a percent of a
         * fully grown synapse.
         *
         * @param synPermActiveInc
         */
        public void SetSynPermActiveInc(double synPermActiveInc)
        {
            this.synPermActiveInc = synPermActiveInc;
        }

        /**
         * Returns the configured active permanence increment
         * @return the configured active permanence increment
         * @see setSynPermActiveInc
         */
        public double GetSynPermActiveInc()
        {
            return synPermActiveInc;
        }

        /**
         * The default connected threshold. Any synapse whose
         * permanence value is above the connected threshold is
         * a "connected synapse", meaning it can contribute to
         * the cell's firing.
         *
         * @param synPermConnected
         */
        public void SetSynPermConnected(double synPermConnected)
        {
            this.synPermConnected = synPermConnected;
        }

        /**
         * Returns the synapse permanence connected threshold
         * @return the synapse permanence connected threshold
         * @see setSynPermConnected
         */
        public double GetSynPermConnected()
        {
            return synPermConnected;
        }

        /**
         * Sets the stimulus increment for synapse permanences below
         * the measured threshold.
         * @param stim
         */
        public void SetSynPermBelowStimulusInc(double stim)
        {
            this.synPermBelowStimulusInc = stim;
        }

        /**
         * Returns the stimulus increment for synapse permanences below
         * the measured threshold.
         *
         * @return
         */
        public double GetSynPermBelowStimulusInc()
        {
            return synPermBelowStimulusInc;
        }

        /**
         * A number between 0 and 1.0, used to set a floor on
         * how often a column should have at least
         * stimulusThreshold active inputs. Periodically, each
         * column looks at the overlap duty cycle of
         * all other columns within its inhibition radius and
         * sets its own internal minimal acceptable duty cycle
         * to: minPctDutyCycleBeforeInh * max(other columns'
         * duty cycles).
         * On each iteration, any column whose overlap duty
         * cycle falls below this computed value will  get
         * all of its permanence values boosted up by
         * synPermActiveInc. Raising all permanences in response
         * to a sub-par duty cycle before  inhibition allows a
         * cell to search for new inputs when either its
         * previously learned inputs are no longer ever active,
         * or when the vast majority of them have been
         * "hijacked" by other columns.
         *
         * @param minPctOverlapDutyCycle
         */
        public void SetMinPctOverlapDutyCycles(double minPctOverlapDutyCycle)
        {
            this.minPctOverlapDutyCycles = minPctOverlapDutyCycle;
        }

        /**
         * see {@link #setMinPctOverlapDutyCycles(double)}
         * @return
         */
        public double GetMinPctOverlapDutyCycles()
        {
            return minPctOverlapDutyCycles;
        }

        /**
         * A number between 0 and 1.0, used to set a floor on
         * how often a column should be activate.
         * Periodically, each column looks at the activity duty
         * cycle of all other columns within its inhibition
         * radius and sets its own internal minimal acceptable
         * duty cycle to:
         *   minPctDutyCycleAfterInh *
         *   max(other columns' duty cycles).
         * On each iteration, any column whose duty cycle after
         * inhibition falls below this computed value will get
         * its internal boost factor increased.
         *
         * @param minPctActiveDutyCycle
         */
        public void SetMinPctActiveDutyCycles(double minPctActiveDutyCycle)
        {
            minPctActiveDutyCycles = minPctActiveDutyCycle;
        }

        /**
         * Returns the minPctActiveDutyCycle
         * see {@link #setMinPctActiveDutyCycles(double)}
         * @return  the minPctActiveDutyCycle
         */
        public double GetMinPctActiveDutyCycles()
        {
            return minPctActiveDutyCycles;
        }

        /**
         * The period used to calculate duty cycles. Higher
         * values make it take longer to respond to changes in
         * boost or synPerConnectedCell. Shorter values make it
         * more unstable and likely to oscillate.
         *
         * @param dutyCyclePeriod
         */
        public void SetDutyCyclePeriod(int dutyCyclePeriod)
        {
            this.dutyCyclePeriod = dutyCyclePeriod;
        }

        /**
         * Returns the configured duty cycle period
         * see {@link #setDutyCyclePeriod(double)}
         * @return  the configured duty cycle period
         */
        public int GetDutyCyclePeriod()
        {
            return dutyCyclePeriod;
        }

        /**
         * The maximum overlap boost factor. Each column's
         * overlap gets multiplied by a boost factor
         * before it gets considered for inhibition.
         * The actual boost factor for a column is number
         * between 1.0 and maxBoost. A boost factor of 1.0 is
         * used if the duty cycle is &gt;= minOverlapDutyCycle,
         * maxBoost is used if the duty cycle is 0, and any duty
         * cycle in between is linearly extrapolated from these
         * 2 end points.
         *
         * @param maxBoost
         */
        public void SetMaxBoost(double maxBoost)
        {
            this.maxBoost = maxBoost;
        }

        /**
         * Returns the max boost
         * see {@link #setMaxBoost(double)}
         * @return  the max boost
         */
        public double GetMaxBoost()
        {
            return maxBoost;
        }

        /**
         * Specifies whether neighborhoods wider than the 
         * borders wrap around to the other side.
         * @param b
         */
        public void SetWrapAround(bool b)
        {
            wrapAround = b;
        }

        /**
         * Returns a flag indicating whether neighborhoods
         * wider than the borders, wrap around to the other
         * side.
         * @return
         */
        public bool IsWrapAround()
        {
            return wrapAround;
        }

        /**
         * Sets and Returns the boosted overlap score for each column
         * @param boostedOverlaps
         * @return
         */
        public double[] SetBoostedOverlaps(double[] boostedOverlaps)
        {
            return this.boostedOverlaps = boostedOverlaps;
        }

        /**
         * Returns the boosted overlap score for each column
         * @return the boosted overlaps
         */
        public double[] GetBoostedOverlaps()
        {
            return boostedOverlaps;
        }

        /**
         * Sets and Returns the overlap score for each column
         * @param overlaps
         * @return
         */
        public int[] SetOverlaps(int[] overlaps)
        {
            return this.overlaps = overlaps;
        }

        /**
         * Returns the overlap score for each column
         * @return the overlaps
         */
        public int[] GetOverlaps()
        {
            return overlaps;
        }

        /**
         * Sets the synPermTrimThreshold
         * @param threshold
         */
        public void SetSynPermTrimThreshold(double threshold)
        {
            synPermTrimThreshold = threshold;
        }

        /**
         * Returns the synPermTrimThreshold
         * @return
         */
        public double GetSynPermTrimThreshold()
        {
            return synPermTrimThreshold;
        }

        /**
         * Sets the {@link FlatMatrix} which holds the mapping
         * of column indexes to their lists of potential inputs.
         *
         * @param pools		{@link FlatMatrix} which holds the pools.
         */
        public void SetPotentialPools(IFlatMatrix<Pool> pools)
        {
            potentialPools = pools;
        }

        /**
         * Returns the {@link FlatMatrix} which holds the mapping
         * of column indexes to their lists of potential inputs.
         * @return	the potential pools
         */
        public IFlatMatrix<Pool> GetPotentialPools()
        {
            return potentialPools;
        }

        /**
         * Returns the minimum {@link Synapse} permanence.
         * @return
         */
        public double GetSynPermMin()
        {
            return synPermMin;
        }

        /**
         * Returns the maximum {@link Synapse} permanence.
         * @return
         */
        public double GetSynPermMax()
        {
            return synPermMax;
        }

        /**
         * Returns the version number
         * @return
         */
        public double GetVersion()
        {
            return version;
        }

        /**
         * Returns the overlap duty cycles.
         * @return
         */
        public double[] GetOverlapDutyCycles()
        {
            return overlapDutyCycles;
        }

        /**
         * Sets the overlap duty cycles
         * @param overlapDutyCycles
         */
        public void SetOverlapDutyCycles(double[] overlapDutyCycles)
        {
            this.overlapDutyCycles = overlapDutyCycles;
        }

        /**
         * Returns the dense (size=numColumns) array of duty cycle stats.
         * @return	the dense array of active duty cycle values.
         */
        public double[] GetActiveDutyCycles()
        {
            return activeDutyCycles;
        }

        /**
         * Sets the dense (size=numColumns) array of duty cycle stats.
         * @param activeDutyCycles
         */
        public void SetActiveDutyCycles(double[] activeDutyCycles)
        {
            this.activeDutyCycles = activeDutyCycles;
        }

        /**
         * Applies the dense array values which aren't -1 to the array containing
         * the active duty cycles of the column corresponding to the index specified.
         * The length of the specified array must be as long as the configured number
         * of columns of this {@code OldConnections}' column configuration.
         *
         * @param	denseActiveDutyCycles	a dense array containing values to set.
         */
        public void UpdateActiveDutyCycles(double[] denseActiveDutyCycles)
        {
            for (int i = 0; i < denseActiveDutyCycles.Length; i++)
            {
                if (denseActiveDutyCycles[i] != -1)
                {
                    activeDutyCycles[i] = denseActiveDutyCycles[i];
                }
            }
        }

        /**
         * Returns the minOverlapDutyCycles.
         * @return	the minOverlapDutyCycles.
         */
        public double[] GetMinOverlapDutyCycles()
        {
            return minOverlapDutyCycles;
        }

        /**
         * Sets the minOverlapDutyCycles
         * @param minOverlapDutyCycles	the minOverlapDutyCycles
         */
        public void SetMinOverlapDutyCycles(double[] minOverlapDutyCycles)
        {
            this.minOverlapDutyCycles = minOverlapDutyCycles;
        }

        /**
         * Returns the minActiveDutyCycles
         * @return	the minActiveDutyCycles
         */
        public double[] GetMinActiveDutyCycles()
        {
            return minActiveDutyCycles;
        }

        /**
         * Sets the minActiveDutyCycles
         * @param minActiveDutyCycles	the minActiveDutyCycles
         */
        public void SetMinActiveDutyCycles(double[] minActiveDutyCycles)
        {
            this.minActiveDutyCycles = minActiveDutyCycles;
        }

        /**
         * Returns the array of boost factors
         * @return	the array of boost factors
         */
        public double[] GetBoostFactors()
        {
            return boostFactors;
        }

        /**
         * Sets the array of boost factors
         * @param boostFactors	the array of boost factors
         */
        public void SetBoostFactors(double[] boostFactors)
        {
            this.boostFactors = boostFactors;
        }


        ////////////////////////////////////////
        //       TemporalMemory Methods       //
        ////////////////////////////////////////

        /**
         * Return type from {@link Connections#computeActivity(Set, double, int, double, int, boolean)}
         */
        [Serializable]
        public class Activity //implements Serializable
        {
            /** default serial */
            private const long serialVersionUID = 1L;

            public int[] numActiveConnected;
            public int[] numActivePotential;

            public Activity(int[] numConnected, int[] numPotential)
            {
                numActiveConnected = numConnected;
                numActivePotential = numPotential;
            }
        }

        /**
         * Compute each segment's number of active synapses for a given input.
         * In the returned lists, a segment's active synapse count is stored at index
         * `segment.flatIdx`.
         * 
         * @param activePresynapticCells
         * @param connectedPermanence
         * @return
         */
        public Activity ComputeActivity(ICollection<Cell> activePresynapticCells, double connectedPermanence)
        {
            int[] numActiveConnectedSynapsesForSegment = new int[nextFlatIdx];
            int[] numActivePotentialSynapsesForSegment = new int[nextFlatIdx];

            double threshold = connectedPermanence - EPSILON;

            foreach (Cell cell in activePresynapticCells)
            {
                foreach (Synapse synapse in GetReceptorSynapses(cell))
                {
                    int flatIdx = synapse.GetSegment<Segment>().GetIndex();
                    ++numActivePotentialSynapsesForSegment[flatIdx];
                    if (synapse.GetPermanence() > threshold)
                    {
                        ++numActiveConnectedSynapsesForSegment[flatIdx];
                    }
                }
            }

            return lastActivity = new Activity(
                numActiveConnectedSynapsesForSegment,
                    numActivePotentialSynapsesForSegment);
        }

        /**
         * Returns the last {@link Activity} computed during the most
         * recently executed cycle.
         * 
         * @return  the last activity to be computed.
         */
        public Activity GetLastActivity()
        {
            return lastActivity;
        }

        /**
         * Record the fact that a segment had some activity. This information is
         * used during segment cleanup.
         * 
         * @param segment		the segment for which to record activity
         */
        public void RecordSegmentActivity(DistalDendrite segment)
        {
            segment.SetLastUsedIteration(tmIteration);
        }

        /**
         * Mark the passage of time. This information is used during segment
         * cleanup.
         */
        public void StartNewIteration()
        {
            ++tmIteration;
        }


        /////////////////////////////////////////////////////////////////
        //     Segment (Specifically, Distal Dendrite) Operations      //
        /////////////////////////////////////////////////////////////////

        /**
         * Adds a new {@link DistalDendrite} segment on the specified {@link Cell},
         * or reuses an existing one.
         * 
         * @param cell  the Cell to which a segment is added.
         * @return  the newly created segment or a reused segment
         */
        public DistalDendrite CreateSegment(Cell cell)
        {
            while (GetNumSegments(cell) >= maxSegmentsPerCell)
            {
                DestroySegment(LeastRecentlyUsedSegment(cell));
            }

            int flatIdx;
            int len;
            if ((len = freeFlatIdxs.Count) > 0)
            {
                flatIdx = freeFlatIdxs[len - 1];
                freeFlatIdxs.RemoveRange(len - 1, 1);
            }
            else
            {
                flatIdx = nextFlatIdx;
                segmentForFlatIdx.Add(null);
                ++nextFlatIdx;
            }

            int ordinal = nextSegmentOrdinal;
            ++nextSegmentOrdinal;

            DistalDendrite segment = new DistalDendrite(cell, flatIdx, tmIteration, ordinal);
            GetSegments(cell, true).Add(segment);
            segmentForFlatIdx[flatIdx] = segment;

            return segment;
        }

        /**
         * Destroys a segment ({@link DistalDendrite})
         * @param segment   the segment to destroy
         */
        public void DestroySegment(DistalDendrite segment)
        {
            // Remove the synapses from all data structures outside this Segment.
            List<Synapse> synapses = GetSynapses(segment);
            int len = synapses.Count;
            GetSynapses(segment).ForEach(s => RemoveSynapseFromPresynapticMap(s));
            numSynapses -= len;

            // Remove the segment from the cell's list.
            GetSegments(segment.GetParentCell()).Remove(segment);

            // Remove the segment from the map
            distalSynapses.Remove(segment);

            // Free the flatIdx and remove the final reference so the Segment can be
            // garbage-collected.
            freeFlatIdxs.Add(segment.GetIndex());
            segmentForFlatIdx[segment.GetIndex()] = null;
        }

        /**
         * Used internally to return the least recently activated segment on 
         * the specified cell
         * 
         * @param cell  cell to search for segments on
         * @return  the least recently activated segment on 
         *          the specified cell
         */
        private DistalDendrite LeastRecentlyUsedSegment(Cell cell)
        {
            List<DistalDendrite> segments = GetSegments(cell, false);
            DistalDendrite minSegment = null;
            long minIteration = long.MaxValue;

            foreach (DistalDendrite dd in segments)
            {
                if (dd.GetLastUsedIteration() < minIteration)
                {
                    minSegment = dd;
                    minIteration = dd.GetLastUsedIteration();
                }
            }

            return minSegment;
        }

        /**
         * Returns the total number of {@link DistalDendrite}s
         * 
         * @return  the total number of segments
         */
        public int GetNumSegments()
        {
            return GetNumSegments(null);
        }

        /**
         * Returns the number of {@link DistalDendrite}s on a given {@link Cell}
         * if specified, or the total number if the "optionalCellArg" is null.
         * 
         * @param optionalCellArg   an optional Cell to specify the context of the segment count.
         * @return  either the total number of segments or the number on a specified cell.
         */
        public int GetNumSegments(Cell optionalCellArg)
        {
            if (optionalCellArg != null)
            {
                return GetSegments(optionalCellArg).Count;
            }

            return nextFlatIdx - freeFlatIdxs.Count;
        }

        /**
         * Returns the mapping of {@link Cell}s to their {@link DistalDendrite}s.
         *
         * @param cell      the {@link Cell} used as a key.
         * @return          the mapping of {@link Cell}s to their {@link DistalDendrite}s.
         */
        public List<DistalDendrite> GetSegments(Cell cell)
        {
            return GetSegments(cell, false);
        }

        /**
         * Returns the mapping of {@link Cell}s to their {@link DistalDendrite}s.
         *
         * @param cell              the {@link Cell} used as a key.
         * @param doLazyCreate      create a container for future use if true, if false
         *                          return an orphaned empty set.
         * @return          the mapping of {@link Cell}s to their {@link DistalDendrite}s.
         */
        public List<DistalDendrite> GetSegments(Cell cell, bool doLazyCreate)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell), "Cell was null");
            }

            if (segments == null)
            {
                segments = new Map<Cell, List<DistalDendrite>>();
            }

            List<DistalDendrite> retVal = null;
            if ((retVal = segments.Get(cell)) == null)
            {
                if (!doLazyCreate) return new List<DistalDendrite>();
                segments[cell] = retVal = new List<DistalDendrite>();
            }
            return retVal;
        }

        /**
         * Get the segment with the specified flatIdx.
         * @param index		The segment's flattened list index.
         * @return	the {@link DistalDendrite} who's index matches.
         */
        public DistalDendrite GetSegmentForFlatIdx(int index)
        {
            return segmentForFlatIdx[index];
        }

        /**
         * Returns the index of the {@link Column} owning the cell which owns 
         * the specified segment.
         * @param segment   the {@link DistalDendrite} of the cell whose column index is desired.
         * @return  the owning column's index
         */
        public int GetColumnIndexForSegment(DistalDendrite segment)
        {
            return segment.GetParentCell().GetIndex() / cellsPerColumn;
        }

        /**
         * <b>FOR TEST USE ONLY</b>
         * @return
         */
        public Map<Cell, List<DistalDendrite>> GetSegmentMapping()
        {
            return new Map<Cell, List<DistalDendrite>>(segments);
        }

        /**
         * Set by the {@link TemporalMemory} following a compute cycle.
         * @param l
         */
        public void SetActiveSegments(List<DistalDendrite> l)
        {
            this.activeSegments = l;
        }

        /**
         * Retrieved by the {@link TemporalMemorty} prior to a compute cycle.
         * @return
         */
        public List<DistalDendrite> GetActiveSegments()
        {
            return activeSegments;
        }

        /**
         * Set by the {@link TemporalMemory} following a compute cycle.
         * @param l
         */
        public void setMatchingSegments(List<DistalDendrite> l)
        {
            this.matchingSegments = l;
        }

        /**
         * Retrieved by the {@link TemporalMemorty} prior to a compute cycle.
         * @return
         */
        public List<DistalDendrite> GetMatchingSegments()
        {
            return matchingSegments;
        }


        /////////////////////////////////////////////////////////////////
        //                    Synapse Operations                       //
        /////////////////////////////////////////////////////////////////

        /**
         * Creates a new synapse on a segment.
         * 
         * @param segment               the {@link DistalDendrite} segment to which a {@link Synapse} is 
         *                              being created
         * @param presynapticCell       the source {@link Cell}
         * @param permanence            the initial permanence
         * @return  the created {@link Synapse}
         */
        public Synapse CreateSynapse(DistalDendrite segment, Cell presynapticCell, double permanence)
        {
            while (GetNumSynapses(segment) >= maxSynapsesPerSegment)
            {
                DestroySynapse(minPermanenceSynapse(segment));
            }

            Synapse synapse;
            GetSynapses(segment).Add(
                synapse = new Synapse(presynapticCell, segment, nextSynapseOrdinal, permanence));

            GetReceptorSynapses(presynapticCell, true).Add(synapse);

            ++nextSynapseOrdinal;

            ++numSynapses;

            return synapse;
        }

        /**
         * Destroys the specified {@link Synapse}
         * @param synapse   the Synapse to destroy
         */
        public void DestroySynapse(Synapse synapse)
        {
            --numSynapses;

            RemoveSynapseFromPresynapticMap(synapse);

            GetSynapses((DistalDendrite)synapse.GetSegment<DistalDendrite>()).Remove(synapse);
        }

        /**
         * Removes the specified {@link Synapse} from its
         * pre-synaptic {@link Cell}'s map of synapses it 
         * activates.
         * 
         * @param synapse   the synapse to remove
         */
        public void RemoveSynapseFromPresynapticMap(Synapse synapse)
        {
            HashSet<Synapse> presynapticSynapses;
            Cell cell = synapse.GetPresynapticCell();
            (presynapticSynapses = GetReceptorSynapses(cell, false)).Remove(synapse);

            if (!presynapticSynapses.Any())
            {
                receptorSynapses.Remove(cell);
            }
        }

        /**
         * Used internally to find the synapse with the smallest permanence
         * on the given segment.
         * 
         * @param dd    Segment object to search for synapses on
         * @return  Synapse object on the segment with the minimal permanence
         */
        private Synapse minPermanenceSynapse(DistalDendrite dd)
        {
            //List<Synapse> synapses = getSynapses(dd).stream().sorted().collect(Collectors.toList());
            List<Synapse> synapses = GetSynapses(dd).OrderBy(s => s).ToList();
            Synapse min = null;
            double minPermanence = Double.MaxValue;

            foreach (Synapse synapse in synapses)
            {
                if (!synapse.IsDestroyed() && synapse.GetPermanence() < minPermanence - EPSILON)
                {
                    min = synapse;
                    minPermanence = synapse.GetPermanence();
                }
            }

            return min;
        }

        /**
         * Returns the total number of {@link Synapse}s
         * 
         * @return  either the total number of synapses
         */
        public long GetNumSynapses()
        {
            return GetNumSynapses(null);
        }

        /**
         * Returns the number of {@link Synapse}s on a given {@link DistalDendrite}
         * if specified, or the total number if the "optionalSegmentArg" is null.
         * 
         * @param optionalSegmentArg    an optional Segment to specify the context of the synapse count.
         * @return  either the total number of synapses or the number on a specified segment.
         */
        public long GetNumSynapses(DistalDendrite optionalSegmentArg)
        {
            if (optionalSegmentArg != null)
            {
                return GetSynapses(optionalSegmentArg).Count;
            }

            return numSynapses;
        }

        /**
         * Returns the mapping of {@link Cell}s to their reverse mapped
         * {@link Synapse}s.
         *
         * @param cell      the {@link Cell} used as a key.
         * @return          the mapping of {@link Cell}s to their reverse mapped
         *                  {@link Synapse}s.
         */
        public HashSet<Synapse> GetReceptorSynapses(Cell cell)
        {
            return GetReceptorSynapses(cell, false);
        }

        /**
         * Returns the mapping of {@link Cell}s to their reverse mapped
         * {@link Synapse}s.
         *
         * @param cell              the {@link Cell} used as a key.
         * @param doLazyCreate      create a container for future use if true, if false
         *                          return an orphaned empty set.
         * @return          the mapping of {@link Cell}s to their reverse mapped
         *                  {@link Synapse}s.
         */
        public HashSet<Synapse> GetReceptorSynapses(Cell cell, bool doLazyCreate)
        {
            if (cell == null)
            {
                throw new ArgumentNullException(nameof(cell), "Cell was null");
            }

            if (receptorSynapses == null)
            {
                receptorSynapses = new Map<Cell, HashSet<Synapse>>();
            }

            HashSet<Synapse> retVal = null;
            if ((retVal = receptorSynapses.Get(cell)) == null)
            {
                if (!doLazyCreate) return new HashSet<Synapse>();
                receptorSynapses[cell] = retVal = new HashSet<Synapse>();
            }

            return retVal;
        }

        /**
         * Returns the mapping of {@link DistalDendrite}s to their {@link Synapse}s.
         *
         * @param segment   the {@link DistalDendrite} used as a key.
         * @return          the mapping of {@link DistalDendrite}s to their {@link Synapse}s.
         */
        public List<Synapse> GetSynapses(DistalDendrite segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment), "Segment was null");
            }

            if (distalSynapses == null)
            {
                distalSynapses = new Map<Segment, List<Synapse>>();
            }

            List<Synapse> retVal = null;
            if ((retVal = distalSynapses.Get(segment)) == null)
            {
                distalSynapses[segment] = retVal = new List<Synapse>();
            }

            return retVal;
        }

        /**
         * Returns the mapping of {@link ProximalDendrite}s to their {@link Synapse}s.
         *
         * @param segment   the {@link ProximalDendrite} used as a key.
         * @return          the mapping of {@link ProximalDendrite}s to their {@link Synapse}s.
         */
        public List<Synapse> GetSynapses(ProximalDendrite segment)
        {
            if (segment == null)
            {
                throw new ArgumentNullException(nameof(segment), "Segment was null");
            }

            if (proximalSynapses == null)
            {
                proximalSynapses = new Map<Segment, List<Synapse>>();
            }

            List<Synapse> retVal = null;
            if ((retVal = proximalSynapses.Get(segment)) == null)
            {
                proximalSynapses[segment] = retVal = new List<Synapse>();
            }

            return retVal;
        }

        /**
         * <b>FOR TEST USE ONLY<b>
         * @return
         */
        public Map<Cell, HashSet<Synapse>> GetReceptorSynapseMapping()
        {
            return new Map<Cell, HashSet<Synapse>>(receptorSynapses);
        }

        /**
         * Clears all {@link TemporalMemory} state.
         */
        public void Clear()
        {
            activeCells.Clear();
            winnerCells.Clear();
            predictiveCells.Clear();
        }

        /**
         * Returns the current {@link Set} of active {@link Cell}s
         *
         * @return  the current {@link Set} of active {@link Cell}s
         */
        public HashSet<Cell> GetActiveCells()
        {
            return activeCells;
        }

        /**
         * Sets the current {@link Set} of active {@link Cell}s
         * @param cells
         */
        public void SetActiveCells(HashSet<Cell> cells)
        {
            this.activeCells = cells;
        }

        /**
         * Returns the current {@link Set} of winner cells
         *
         * @return  the current {@link Set} of winner cells
         */
        public HashSet<Cell> GetWinnerCells()
        {
            return winnerCells;
        }

        /**
         * Sets the current {@link Set} of winner {@link Cell}s
         * @param cells
         */
        public void SetWinnerCells(HashSet<Cell> cells)
        {
            this.winnerCells = cells;
        }

        /**
         * Returns the {@link Set} of predictive cells.
         * @return
         */
        public HashSet<Cell> GetPredictiveCells()
        {
            if (!predictiveCells.Any())
            {
                Cell previousCell = null;
                Cell currCell = null;

                List<DistalDendrite> temp = new List<DistalDendrite>(activeSegments);
                foreach (DistalDendrite activeSegment in temp)
                {
                    if ((currCell = activeSegment.GetParentCell()) != previousCell)
                    {
                        predictiveCells.Add(previousCell = currCell);
                    }
                }
            }
            return predictiveCells;
        }

        /**
         * Clears the previous predictive cells from the list.
         */
        public void ClearPredictiveCells()
        {
            this.predictiveCells.Clear();
        }

        /**
         * Returns the column at the specified index.
         * @param index
         * @return
         */
        public Column GetColumn(int index)
        {
            return memory.GetObject(index);
        }

        /**
         * Sets the number of {@link Column}.
         *
         * @param columnDimensions
         */
        public void SetColumnDimensions(int[] columnDimensions)
        {
            this.columnDimensions = columnDimensions;
        }

        /**
         * Gets the number of {@link Column}.
         *
         * @return columnDimensions
         */
        public int[] GetColumnDimensions()
        {
            return this.columnDimensions;
        }

        /**
         * A list representing the dimensions of the input
         * vector. Format is [height, width, depth, ...], where
         * each value represents the size of the dimension. For a
         * topology of one dimension with 100 inputs use 100, or
         * [100]. For a two dimensional topology of 10x5 use
         * [10,5].
         *
         * @param inputDimensions
         */
        public void SetInputDimensions(int[] inputDimensions)
        {
            this.inputDimensions = inputDimensions;
        }

        /**
         * Returns the configured input dimensions
         * see {@link #setInputDimensions(int[])}
         * @return the configured input dimensions
         */
        public int[] GetInputDimensions()
        {
            return inputDimensions;
        }

        /**
         * Sets the number of {@link Cell}s per {@link Column}
         * @param cellsPerColumn
         */
        public void SetCellsPerColumn(int cellsPerColumn)
        {
            this.cellsPerColumn = cellsPerColumn;
        }

        /**
         * Gets the number of {@link Cell}s per {@link Column}.
         *
         * @return cellsPerColumn
         */
        public int GetCellsPerColumn()
        {
            return this.cellsPerColumn;
        }

        /**
         * Sets the activation threshold.
         *
         * If the number of active connected synapses on a segment
         * is at least this threshold, the segment is said to be active.
         *
         * @param activationThreshold
         */
        public void SetActivationThreshold(int activationThreshold)
        {
            this.activationThreshold = activationThreshold;
        }

        /**
         * Returns the activation threshold.
         * @return
         */
        public int GetActivationThreshold()
        {
            return activationThreshold;
        }

        /**
         * Radius around cell from which it can
         * sample to form distal dendrite connections.
         *
         * @param   learningRadius
         */
        public void SetLearningRadius(int learningRadius)
        {
            this.learningRadius = learningRadius;
        }

        /**
         * Returns the learning radius.
         * @return
         */
        public int GetLearningRadius()
        {
            return learningRadius;
        }

        /**
         * If the number of synapses active on a segment is at least this
         * threshold, it is selected as the best matching
         * cell in a bursting column.
         *
         * @param   minThreshold
         */
        public void SetMinThreshold(int minThreshold)
        {
            this.minThreshold = minThreshold;
        }

        /**
         * Returns the minimum threshold of active synapses to be picked as best.
         * @return
         */
        public int GetMinThreshold()
        {
            return minThreshold;
        }

        /**
         * The maximum number of synapses added to a segment during learning.
         *
         * @param   maxNewSynapseCount
         */
        public void SetMaxNewSynapseCount(int maxNewSynapseCount)
        {
            this.maxNewSynapseCount = maxNewSynapseCount;
        }

        /**
         * Returns the maximum number of synapses added to a segment during
         * learning.
         *
         * @return
         */
        public int GetMaxNewSynapseCount()
        {
            return maxNewSynapseCount;
        }

        /**
         * The maximum number of segments allowed on a given cell
         * @param maxSegmentsPerCell
         */
        public void SetMaxSegmentsPerCell(int maxSegmentsPerCell)
        {
            this.maxSegmentsPerCell = maxSegmentsPerCell;
        }

        /**
         * Returns the maximum number of segments allowed on a given cell
         * @return
         */
        public int GetMaxSegmentsPerCell()
        {
            return maxSegmentsPerCell;
        }

        /**
         * The maximum number of synapses allowed on a given segment
         * @param maxSynapsesPerSegment
         */
        public void SetMaxSynapsesPerSegment(int maxSynapsesPerSegment)
        {
            this.maxSynapsesPerSegment = maxSynapsesPerSegment;
        }

        /**
         * Returns the maximum number of synapses allowed per segment
         * @return
         */
        public int GetMaxSynapsesPerSegment()
        {
            return maxSynapsesPerSegment;
        }

        /**
         * Initial permanence of a new synapse
         *
         * @param   initialPermanence
         */
        public void SetInitialPermanence(double initialPermanence)
        {
            this.initialPermanence = initialPermanence;
        }

        /**
         * Returns the initial permanence setting.
         * @return
         */
        public double GetInitialPermanence()
        {
            return initialPermanence;
        }

        /**
         * If the permanence value for a synapse
         * is greater than this value, it is said
         * to be connected.
         *
         * @param connectedPermanence
         */
        public void SetConnectedPermanence(double connectedPermanence)
        {
            this.connectedPermanence = connectedPermanence;
        }

        /**
         * If the permanence value for a synapse
         * is greater than this value, it is said
         * to be connected.
         *
         * @return
         */
        public double GetConnectedPermanence()
        {
            return connectedPermanence;
        }

        /**
         * Amount by which permanences of synapses
         * are incremented during learning.
         *
         * @param   permanenceIncrement
         */
        public void SetPermanenceIncrement(double permanenceIncrement)
        {
            this.permanenceIncrement = permanenceIncrement;
        }

        /**
         * Amount by which permanences of synapses
         * are incremented during learning.
         */
        public double GetPermanenceIncrement()
        {
            return permanenceIncrement;
        }

        /**
         * Amount by which permanences of synapses
         * are decremented during learning.
         *
         * @param   permanenceDecrement
         */
        public void SetPermanenceDecrement(double permanenceDecrement)
        {
            this.permanenceDecrement = permanenceDecrement;
        }

        /**
         * Amount by which permanences of synapses
         * are decremented during learning.
         */
        public double GetPermanenceDecrement()
        {
            return permanenceDecrement;
        }

        /**
         * Amount by which active permanences of synapses of previously predicted but inactive segments are decremented.
         * @param predictedSegmentDecrement
         */
        public void SetPredictedSegmentDecrement(double predictedSegmentDecrement)
        {
            this.predictedSegmentDecrement = predictedSegmentDecrement;
        }

        /**
         * Returns the predictedSegmentDecrement amount.
         * @return
         */
        public double GetPredictedSegmentDecrement()
        {
            return predictedSegmentDecrement;
        }

        /**
         * Converts a {@link Collection} of {@link Cell}s to a list
         * of cell indexes.
         *
         * @param cells
         * @return
         */
        public static List<int> AsCellIndexes(ICollection<Cell> cells)
        {
            List<int> ints = new List<int>();
            foreach (Cell cell in cells)
            {
                ints.Add(cell.GetIndex());
            }

            return ints;
        }

        /**
         * Converts a {@link Collection} of {@link Column}s to a list
         * of column indexes.
         *
         * @param columns
         * @return
         */
        public static List<int> AsColumnIndexes(ICollection<Column> columns)
        {
            List<int> ints = new List<int>();
            foreach (Column col in columns)
            {
                ints.Add(col.GetIndex());
            }

            return ints;
        }

        /**
         * Returns a list of the {@link Cell}s specified.
         * @param cells		the indexes of the {@link Cell}s to return
         * @return	the specified list of cells
         */
        public List<Cell> AsCellObjects(ICollection<int> cells)
        {
            List<Cell> objs = new List<Cell>();
            foreach (int i in cells)
            {
                objs.Add(this.cells[i]);
            }
            return objs;
        }

        /**
         * Returns a list of the {@link Column}s specified.
         * @param cols		the indexes of the {@link Column}s to return
         * @return		the specified list of columns
         */
        public List<Column> AsColumnObjects(ICollection<int> cols)
        {
            List<Column> objs = new List<Column>();
            foreach (int i in cols)
            {
                objs.Add(memory.GetObject(i));
            }
            return objs;
        }

        /**
         * Returns a {@link Set} view of the {@link Column}s specified by
         * the indexes passed in.
         *
         * @param indexes		the indexes of the Columns to return
         * @return				a set view of the specified columns
         */
        public HashSet<Column> GetColumnSet(int[] indexes)
        {
            HashSet<Column> retVal = new HashSet<Column>();
            for (int i = 0; i < indexes.Length; i++)
            {
                retVal.Add(memory.GetObject(indexes[i]));
            }
            return retVal;
        }

        /**
         * Returns a {@link List} view of the {@link Column}s specified by
         * the indexes passed in.
         *
         * @param indexes		the indexes of the Columns to return
         * @return				a List view of the specified columns
         */
        public List<Column> GetColumnList(int[] indexes)
        {
            List<Column> retVal = new List<Column>();
            for (int i = 0; i < indexes.Length; i++)
            {
                retVal.Add(memory.GetObject(indexes[i]));
            }
            return retVal;
        }

        /**
         * High verbose output useful for debugging
         */
        public void PrintParameters()
        {
            Console.WriteLine("------------ SpatialPooler Parameters ------------------");
            Console.WriteLine("numInputs                  = " + GetNumInputs());
            Console.WriteLine("numColumns                 = " + GetNumColumns());
            Console.WriteLine("cellsPerColumn             = " + GetCellsPerColumn());
            Console.WriteLine("columnDimensions           = " + Arrays.ToString(GetColumnDimensions()));
            Console.WriteLine("numActiveColumnsPerInhArea = " + GetNumActiveColumnsPerInhArea());
            Console.WriteLine("potentialPct               = " + GetPotentialPct());
            Console.WriteLine("potentialRadius            = " + GetPotentialRadius());
            Console.WriteLine("globalInhibition           = " + GetGlobalInhibition());
            Console.WriteLine("localAreaDensity           = " + GetLocalAreaDensity());
            Console.WriteLine("inhibitionRadius           = " + GetInhibitionRadius());
            Console.WriteLine("stimulusThreshold          = " + GetStimulusThreshold());
            Console.WriteLine("synPermActiveInc           = " + GetSynPermActiveInc());
            Console.WriteLine("synPermInactiveDec         = " + GetSynPermInactiveDec());
            Console.WriteLine("synPermConnected           = " + GetSynPermConnected());
            Console.WriteLine("minPctOverlapDutyCycle     = " + GetMinPctOverlapDutyCycles());
            Console.WriteLine("minPctActiveDutyCycle      = " + GetMinPctActiveDutyCycles());
            Console.WriteLine("dutyCyclePeriod            = " + GetDutyCyclePeriod());
            Console.WriteLine("maxBoost                   = " + GetMaxBoost());
            Console.WriteLine("version                    = " + GetVersion());

            Console.WriteLine("\n------------ TemporalMemory Parameters ------------------");
            Console.WriteLine("activationThreshold        = " + GetActivationThreshold());
            Console.WriteLine("learningRadius             = " + GetLearningRadius());
            Console.WriteLine("minThreshold               = " + GetMinThreshold());
            Console.WriteLine("maxNewSynapseCount         = " + GetMaxNewSynapseCount());
            Console.WriteLine("maxSynapsesPerSegment      = " + GetMaxSynapsesPerSegment());
            Console.WriteLine("maxSegmentsPerCell         = " + GetMaxSegmentsPerCell());
            Console.WriteLine("initialPermanence          = " + GetInitialPermanence());
            Console.WriteLine("connectedPermanence        = " + GetConnectedPermanence());
            Console.WriteLine("permanenceIncrement        = " + GetPermanenceIncrement());
            Console.WriteLine("permanenceDecrement        = " + GetPermanenceDecrement());
            Console.WriteLine("predictedSegmentDecrement  = " + GetPredictedSegmentDecrement());
        }

        /**
         * High verbose output useful for debugging
         */
        public String getPrintString()
        {
            StringWriter pw = new StringWriter();

            pw.WriteLine("---------------------- General -------------------------");
            pw.WriteLine("columnDimensions           = " + Arrays.ToString(GetColumnDimensions()));
            pw.WriteLine("inputDimensions            = " + Arrays.ToString(GetInputDimensions()));
            pw.WriteLine("cellsPerColumn             = " + GetCellsPerColumn());

            pw.WriteLine("random                     = " + GetRandom());
            pw.WriteLine("seed                       = " + GetSeed());

            pw.WriteLine("\n------------ SpatialPooler Parameters ------------------");
            pw.WriteLine("numInputs                  = " + GetNumInputs());
            pw.WriteLine("numColumns                 = " + GetNumColumns());
            pw.WriteLine("numActiveColumnsPerInhArea = " + GetNumActiveColumnsPerInhArea());
            pw.WriteLine("potentialPct               = " + GetPotentialPct());
            pw.WriteLine("potentialRadius            = " + GetPotentialRadius());
            pw.WriteLine("globalInhibition           = " + GetGlobalInhibition());
            pw.WriteLine("localAreaDensity           = " + GetLocalAreaDensity());
            pw.WriteLine("inhibitionRadius           = " + GetInhibitionRadius());
            pw.WriteLine("stimulusThreshold          = " + GetStimulusThreshold());
            pw.WriteLine("synPermActiveInc           = " + GetSynPermActiveInc());
            pw.WriteLine("synPermInactiveDec         = " + GetSynPermInactiveDec());
            pw.WriteLine("synPermConnected           = " + GetSynPermConnected());
            pw.WriteLine("synPermBelowStimulusInc    = " + GetSynPermBelowStimulusInc());
            pw.WriteLine("synPermTrimThreshold       = " + GetSynPermTrimThreshold());
            pw.WriteLine("minPctOverlapDutyCycles    = " + GetMinPctOverlapDutyCycles());
            pw.WriteLine("minPctActiveDutyCycles     = " + GetMinPctActiveDutyCycles());
            pw.WriteLine("dutyCyclePeriod            = " + GetDutyCyclePeriod());
            pw.WriteLine("wrapAround                 = " + IsWrapAround());
            pw.WriteLine("maxBoost                   = " + GetMaxBoost());
            pw.WriteLine("version                    = " + GetVersion());

            pw.WriteLine("\n------------ TemporalMemory Parameters ------------------");
            pw.WriteLine("activationThreshold        = " + GetActivationThreshold());
            pw.WriteLine("learningRadius             = " + GetLearningRadius());
            pw.WriteLine("minThreshold               = " + GetMinThreshold());
            pw.WriteLine("maxNewSynapseCount         = " + GetMaxNewSynapseCount());
            pw.WriteLine("maxSynapsesPerSegment      = " + GetMaxSynapsesPerSegment());
            pw.WriteLine("maxSegmentsPerCell         = " + GetMaxSegmentsPerCell());
            pw.WriteLine("initialPermanence          = " + GetInitialPermanence());
            pw.WriteLine("connectedPermanence        = " + GetConnectedPermanence());
            pw.WriteLine("permanenceIncrement        = " + GetPermanenceIncrement());
            pw.WriteLine("permanenceDecrement        = " + GetPermanenceDecrement());
            pw.WriteLine("predictedSegmentDecrement  = " + GetPredictedSegmentDecrement());

            return pw.ToString();
        }

        /**
         * Returns a 2 Dimensional array of 1's and 0's indicating
         * which of the column's pool members are above the connected
         * threshold, and therefore considered "connected"
         * @return
         */
        public int[][] GetConnecteds()
        {
            int[][] retVal = new int[GetNumColumns()][];
            for (int i = 0; i < GetNumColumns(); i++)
            {
                Pool pool = GetPotentialPools().Get(i);
                int[] indexes = pool.GetDenseConnected(this);
                retVal[i] = indexes;
            }

            return retVal;
        }

        /**
         * Returns a 2 Dimensional array of 1's and 0's indicating
         * which input bits belong to which column's pool.
         * @return
         */
        public int[][] GetPotentials()
        {
            int[][] retVal = new int[GetNumColumns()][];
            for (int i = 0; i < GetNumColumns(); i++)
            {
                Pool pool = GetPotentialPools().Get(i);
                int[] indexes = pool.GetDensePotential(this);
                retVal[i] = indexes;
            }

            return retVal;
        }

        /**
         * Returns a 2 Dimensional array of the permanences for SP
         * proximal dendrite column pooled connections.
         * @return
         */
        public double[][] GetPermanences()
        {
            double[][] retVal = new double[GetNumColumns()][];
            for (int i = 0; i < GetNumColumns(); i++)
            {
                Pool pool = GetPotentialPools().Get(i);
                double[] perm = pool.GetDensePermanences(this);
                retVal[i] = perm;
            }

            return retVal;
        }

        /**
         * {@inheritDoc}
         */

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + activationThreshold;
            result = prime * result + ((activeCells == null) ? 0 : activeCells.GetHashCode());
            result = prime * result + Arrays.GetHashCode(activeDutyCycles);
            result = prime * result + Arrays.GetHashCode(boostFactors);
            result = prime * result + Arrays.GetHashCode(cells);
            result = prime * result + cellsPerColumn;
            result = prime * result + Arrays.GetHashCode(columnDimensions);
            result = prime * result + ((connectedCounts == null) ? 0 : connectedCounts.GetHashCode());
            long temp;
            temp = BitConverter.DoubleToInt64Bits(connectedPermanence);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + dutyCyclePeriod;
            result = prime * result + (globalInhibition ? 1231 : 1237);
            result = prime * result + inhibitionRadius;
            temp = BitConverter.DoubleToInt64Bits(initConnectedPct);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(initialPermanence);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + Arrays.GetHashCode(inputDimensions);
            result = prime * result + ((inputMatrix == null) ? 0 : inputMatrix.GetHashCode());
            result = prime * result + spIterationLearnNum;
            result = prime * result + spIterationNum;
            result = prime * result + (int)tmIteration;
            result = prime * result + learningRadius;
            temp = BitConverter.DoubleToInt64Bits(localAreaDensity);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(maxBoost);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + maxNewSynapseCount;
            result = prime * result + ((memory == null) ? 0 : memory.GetHashCode());
            result = prime * result + Arrays.GetHashCode(minActiveDutyCycles);
            result = prime * result + Arrays.GetHashCode(minOverlapDutyCycles);
            temp = BitConverter.DoubleToInt64Bits(minPctActiveDutyCycles);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(minPctOverlapDutyCycles);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + minThreshold;
            temp = BitConverter.DoubleToInt64Bits(numActiveColumnsPerInhArea);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + numColumns;
            result = prime * result + numInputs;
            temp = numSynapses;
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + Arrays.GetHashCode(overlapDutyCycles);
            temp = BitConverter.DoubleToInt64Bits(permanenceDecrement);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(permanenceIncrement);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(potentialPct);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + ((potentialPools == null) ? 0 : potentialPools.GetHashCode());
            result = prime * result + potentialRadius;
            temp = BitConverter.DoubleToInt64Bits(predictedSegmentDecrement);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + ((predictiveCells == null) ? 0 : predictiveCells.GetHashCode());
            result = prime * result + ((random == null) ? 0 : random.GetHashCode());
            result = prime * result + ((receptorSynapses == null) ? 0 : receptorSynapses.GetHashCode());
            result = prime * result + seed;
            result = prime * result + ((segments == null) ? 0 : segments.GetHashCode());
            temp = BitConverter.DoubleToInt64Bits(stimulusThreshold);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermActiveInc);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermBelowStimulusInc);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermConnected);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermInactiveDec);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermMax);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermMin);
            result = prime * result + (int)(temp ^ (temp >> 32));
            temp = BitConverter.DoubleToInt64Bits(synPermTrimThreshold);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + proximalSynapseCounter;
            result = prime * result + ((proximalSynapses == null) ? 0 : proximalSynapses.GetHashCode());
            result = prime * result + ((distalSynapses == null) ? 0 : distalSynapses.GetHashCode());
            result = prime * result + Arrays.GetHashCode(tieBreaker);
            result = prime * result + updatePeriod;
            temp = BitConverter.DoubleToInt64Bits(version);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + ((winnerCells == null) ? 0 : winnerCells.GetHashCode());
            return result;
        }

        /**
         * {@inheritDoc}
         */
        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (this.GetType() != obj.GetType())
                return false;
            Connections other = (Connections)obj;
            if (activationThreshold != other.activationThreshold)
                return false;
            if (activeCells == null)
            {
                if (other.activeCells != null)
                    return false;
            }
            else if (!activeCells.Equals(other.activeCells))
                return false;
            if (!Arrays.AreEqual(activeDutyCycles, other.activeDutyCycles))
                return false;
            if (!Arrays.AreEqual(boostFactors, other.boostFactors))
                return false;
            if (!Arrays.AreEqual(cells, other.cells))
                return false;
            if (cellsPerColumn != other.cellsPerColumn)
                return false;
            if (!Arrays.AreEqual(columnDimensions, other.columnDimensions))
                return false;
            if (connectedCounts == null)
            {
                if (other.connectedCounts != null)
                    return false;
            }
            else if (!connectedCounts.Equals(other.connectedCounts))
                return false;
            if (BitConverter.DoubleToInt64Bits(connectedPermanence) != BitConverter.DoubleToInt64Bits(other.connectedPermanence))
                return false;
            if (dutyCyclePeriod != other.dutyCyclePeriod)
                return false;
            if (globalInhibition != other.globalInhibition)
                return false;
            if (inhibitionRadius != other.inhibitionRadius)
                return false;
            if (BitConverter.DoubleToInt64Bits(initConnectedPct) != BitConverter.DoubleToInt64Bits(other.initConnectedPct))
                return false;
            if (BitConverter.DoubleToInt64Bits(initialPermanence) != BitConverter.DoubleToInt64Bits(other.initialPermanence))
                return false;
            if (!Arrays.AreEqual(inputDimensions, other.inputDimensions))
                return false;
            if (inputMatrix == null)
            {
                if (other.inputMatrix != null)
                    return false;
            }
            else if (!inputMatrix.Equals(other.inputMatrix))
                return false;
            if (spIterationLearnNum != other.spIterationLearnNum)
                return false;
            if (spIterationNum != other.spIterationNum)
                return false;
            if (tmIteration != other.tmIteration)
                return false;
            if (learningRadius != other.learningRadius)
                return false;
            if (BitConverter.DoubleToInt64Bits(localAreaDensity) != BitConverter.DoubleToInt64Bits(other.localAreaDensity))
                return false;
            if (BitConverter.DoubleToInt64Bits(maxBoost) != BitConverter.DoubleToInt64Bits(other.maxBoost))
                return false;
            if (maxNewSynapseCount != other.maxNewSynapseCount)
                return false;
            if (memory == null)
            {
                if (other.memory != null)
                    return false;
            }
            else if (!memory.Equals(other.memory))
                return false;
            if (!Arrays.AreEqual(minActiveDutyCycles, other.minActiveDutyCycles))
                return false;
            if (!Arrays.AreEqual(minOverlapDutyCycles, other.minOverlapDutyCycles))
                return false;
            if (BitConverter.DoubleToInt64Bits(minPctActiveDutyCycles) != BitConverter.DoubleToInt64Bits(other.minPctActiveDutyCycles))
                return false;
            if (BitConverter.DoubleToInt64Bits(minPctOverlapDutyCycles) != BitConverter.DoubleToInt64Bits(other.minPctOverlapDutyCycles))
                return false;
            if (minThreshold != other.minThreshold)
                return false;
            if (BitConverter.DoubleToInt64Bits(numActiveColumnsPerInhArea) != BitConverter.DoubleToInt64Bits(other.numActiveColumnsPerInhArea))
                return false;
            if (numColumns != other.numColumns)
                return false;
            if (numInputs != other.numInputs)
                return false;
            if (numSynapses != other.numSynapses)
                return false;
            if (!Arrays.AreEqual(overlapDutyCycles, other.overlapDutyCycles))
                return false;
            if (BitConverter.DoubleToInt64Bits(permanenceDecrement) != BitConverter.DoubleToInt64Bits(other.permanenceDecrement))
                return false;
            if (BitConverter.DoubleToInt64Bits(permanenceIncrement) != BitConverter.DoubleToInt64Bits(other.permanenceIncrement))
                return false;
            if (BitConverter.DoubleToInt64Bits(potentialPct) != BitConverter.DoubleToInt64Bits(other.potentialPct))
                return false;
            if (potentialPools == null)
            {
                if (other.potentialPools != null)
                    return false;
            }
            else if (!potentialPools.Equals(other.potentialPools))
                return false;
            if (potentialRadius != other.potentialRadius)
                return false;
            if (BitConverter.DoubleToInt64Bits(predictedSegmentDecrement) != BitConverter.DoubleToInt64Bits(other.predictedSegmentDecrement))
                return false;
            if (predictiveCells == null)
            {
                if (other.predictiveCells != null)
                    return false;
            }
            else if (!GetPredictiveCells().Equals(other.GetPredictiveCells()))
                return false;
            if (receptorSynapses == null)
            {
                if (other.receptorSynapses != null)
                    return false;
            }
            else if (!receptorSynapses.ToString().Equals(other.receptorSynapses.ToString()))
                return false;
            if (seed != other.seed)
                return false;
            if (segments == null)
            {
                if (other.segments != null)
                    return false;
            }
            else if (!segments.Equals(other.segments))
                return false;
            if (BitConverter.DoubleToInt64Bits(stimulusThreshold) != BitConverter.DoubleToInt64Bits(other.stimulusThreshold))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermActiveInc) != BitConverter.DoubleToInt64Bits(other.synPermActiveInc))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermBelowStimulusInc) != BitConverter.DoubleToInt64Bits(other.synPermBelowStimulusInc))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermConnected) != BitConverter.DoubleToInt64Bits(other.synPermConnected))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermInactiveDec) != BitConverter.DoubleToInt64Bits(other.synPermInactiveDec))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermMax) != BitConverter.DoubleToInt64Bits(other.synPermMax))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermMin) != BitConverter.DoubleToInt64Bits(other.synPermMin))
                return false;
            if (BitConverter.DoubleToInt64Bits(synPermTrimThreshold) != BitConverter.DoubleToInt64Bits(other.synPermTrimThreshold))
                return false;
            if (proximalSynapseCounter != other.proximalSynapseCounter)
                return false;
            if (proximalSynapses == null)
            {
                if (other.proximalSynapses != null)
                    return false;
            }
            else if (!proximalSynapses.Equals(other.proximalSynapses))
                return false;
            if (distalSynapses == null)
            {
                if (other.distalSynapses != null)
                    return false;
            }
            else if (!distalSynapses.Equals(other.distalSynapses))
                return false;
            if (!Arrays.AreEqual(tieBreaker, other.tieBreaker))
                return false;
            if (updatePeriod != other.updatePeriod)
                return false;
            if (BitConverter.DoubleToInt64Bits(version) != BitConverter.DoubleToInt64Bits(other.version))
                return false;
            if (winnerCells == null)
            {
                if (other.winnerCells != null)
                    return false;
            }
            else if (!winnerCells.Equals(other.winnerCells))
                return false;
            return true;
        }
    }
}