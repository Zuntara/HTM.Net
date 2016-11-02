//using System;
//using System.Collections.Generic;
//using System.Threading;
//using HTM.Net.Algorithms;
//using HTM.Net.Model;
//using HTM.Net.Util;
//using MathNet.Numerics.LinearAlgebra;
//using MathNet.Numerics.LinearAlgebra.Double;

//namespace HTM.Net
//{
//    public class Connections
//    {
//        /////////////////////////////////////// Spatial Pooler Vars ///////////////////////////////////////////
//        private int potentialRadius = 16;
//        private double potentialPct = 0.5;
//        private bool globalInhibition = false;
//        private double localAreaDensity = -1.0;
//        private double numActiveColumnsPerInhArea;
//        private double stimulusThreshold = 0;
//        private double synPermInactiveDec = 0.01;
//        private double synPermActiveInc = 0.10;
//        private double synPermConnected = 0.10;
//        private double synPermBelowStimulusInc;
//        private double minPctOverlapDutyCycles = 0.001;
//        private double minPctActiveDutyCycles = 0.001;
//        private double predictedSegmentDecrement = 0.0;
//        private int dutyCyclePeriod = 1000;
//        private double maxBoost = 10.0;
//        private int spVerbosity = 0;

//        private int numInputs = 1;  //product of input dimensions
//        private int numColumns = 1; //product of column dimensions

//        //Extra parameter settings
//        private double synPermMin = 0.0;
//        private double synPermMax = 1.0;
//        private double synPermTrimThreshold;
//        private int updatePeriod = 50;
//        private double initConnectedPct = 0.5;

//        //Internal state
//        private double version = 1.0;
//        public int iterationNum = 0;
//        public int iterationLearnNum = 0;

//        /** A matrix representing the shape of the input. */
//        protected ISparseMatrix inputMatrix;
//        /**
//         * Store the set of all inputs that are within each column's potential pool.
//         * 'potentialPools' is a matrix, whose rows represent cortical columns, and
//         * whose columns represent the input bits. if potentialPools[i][j] == 1,
//         * then input bit 'j' is in column 'i's potential pool. A column can only be
//         * connected to inputs in its potential pool. The indices refer to a
//         * flattened version of both the inputs and columns. Namely, irrespective
//         * of the topology of the inputs and columns, they are treated as being a
//         * one dimensional array. Since a column is typically connected to only a
//         * subset of the inputs, many of the entries in the matrix are 0. Therefore
//         * the potentialPool matrix is stored using the SparseObjectMatrix
//         * class, to reduce memory footprint and computation time of algorithms that
//         * require iterating over the data structure.
//         */
//        private IFlatMatrix<Pool> potentialPools;
//        /**
//         * Initialize a tiny random tie breaker. This is used to determine winning
//         * columns where the overlaps are identical.
//         */
//        private double[] tieBreaker;
//        /**
//         * Stores the number of connected synapses for each column. This is simply
//         * a sum of each row of 'connectedSynapses'. again, while this
//         * information is readily available from 'connectedSynapses', it is
//         * stored separately for efficiency purposes.
//         */
//        private Matrix<double> connectedCounts;
//        /**
//         * The inhibition radius determines the size of a column's local
//         * neighborhood. of a column. A cortical column must overcome the overlap
//         * score of columns in its neighborhood in order to become actives. This
//         * radius is updated every learning round. It grows and shrinks with the
//         * average number of connected synapses per column.
//         */
//        private int inhibitionRadius = 0;

//        private int proximalSynapseCounter = 0;

//        private double[] overlapDutyCycles;
//        private double[] activeDutyCycles;
//        private double[] minOverlapDutyCycles;
//        private double[] minActiveDutyCycles;
//        private double[] boostFactors;

//        /////////////////////////////////////// Temporal Memory Vars ///////////////////////////////////////////

//        protected HashSet<Cell> activeCells = new HashSet<Cell>();
//        protected HashSet<Cell> winnerCells = new HashSet<Cell>();
//        protected HashSet<Cell> predictiveCells = new HashSet<Cell>();
//        protected HashSet<Cell> matchingCells = new HashSet<Cell>();
//        protected HashSet<Column> successfullyPredictedColumns = new HashSet<Column>();
//        protected HashSet<DistalDendrite> activeSegments = new HashSet<DistalDendrite>();
//        protected HashSet<DistalDendrite> learningSegments = new HashSet<DistalDendrite>();
//        protected HashSet<DistalDendrite> matchingSegments = new HashSet<DistalDendrite>();

//        /** Total number of columns */
//        protected int[] columnDimensions = new int[] { 2048 };
//        /** Total number of cells per column */
//        protected int cellsPerColumn = 32;
//        /** What will comprise the Layer input. Input (i.e. from encoder) */
//        protected int[] inputDimensions = new int[] { 32, 32 };
//        /**
//         * If the number of active connected synapses on a segment
//         * is at least this threshold, the segment is said to be active.
//         */
//        private int activationThreshold = 13;
//        /**
//         * Radius around cell from which it can
//         * sample to form distal {@link DistalDendrite} connections.
//         */
//        private int learningRadius = 2048;
//        /**
//         * If the number of synapses active on a segment is at least this
//         * threshold, it is selected as the best matching
//         * cell in a bursting column.
//         */
//        private int minThreshold = 10;
//        /** The maximum number of synapses added to a segment during learning. */
//        private int maxNewSynapseCount = 20;
//        /** Initial permanence of a new synapse */
//        private double initialPermanence = 0.21;
//        /**
//         * If the permanence value for a synapse
//         * is greater than this value, it is said
//         * to be connected.
//         */
//        private double connectedPermanence = 0.50;
//        /**
//         * Amount by which permanences of synapses
//         * are incremented during learning.
//         */
//        private double permanenceIncrement = 0.10;
//        /**
//         * Amount by which permanences of synapses
//         * are decremented during learning.
//         */
//        private double permanenceDecrement = 0.10;

//        /** The main data structure containing columns, cells, and synapses */
//        private SparseObjectMatrix<Column> memory;

//        private Cell[] cells;

//        ///////////////////////   Structural Elements /////////////////////////
//        /** Reverse mapping from source cell to {@link Synapse} */
//        protected Map<Cell, List<Synapse>> receptorSynapses;

//        protected Map<Cell, List<DistalDendrite>> segments;
//        protected Map<Segment, List<Synapse>> synapses;

//        /** Helps index each new Segment */
//        protected long segmentCounter = 0; // AtomicInteger
//        /** Helps index each new Synapse */
//        protected long synapseCounter = 0; // AtomicInteger
//        /** The default random number seed */
//        protected int seed = 42;
//        /** The random number generator */
//        protected IRandom random = new MersenneTwister(42);
//        /// <summary>
//        /// If true this will initialize and run the spatial pooler multithreaded, this will 
//        /// make the network less deterministic because the random generator will return different values on different places,
//        /// even when a seed is used. (can be a problem for unit tests)
//        /// </summary>
//        protected bool spParallelMode = false;

//        public object SyncRoot = new object();

//        ///////// paCLA extensions

//        protected double[] paOverlaps;
//        /**
//         * Sets paOverlaps (predictive assist vector) for {@link PASpatialPooler}
//         *
//         * @param overlaps
//         */
//        public void SetPAOverlaps(double[] overlaps)
//        {
//            this.paOverlaps = overlaps;
//        }

//        /// <summary>
//        /// Returns paOverlaps (predictive assist vector) for <see cref="PASpatialPooler"/>
//        /// </summary>
//        public double[] GetPAOverlaps()
//        {
//            return this.paOverlaps;
//        }

//        /// <summary>
//        /// Constructs a new <see cref="Connections"/> object.
//        /// </summary>
//        public Connections()
//        {
//            synPermBelowStimulusInc = synPermConnected / 10.0;
//            synPermTrimThreshold = synPermActiveInc / 2.0;
//        }

//        /// <summary>
//        /// Returns the configured initial connected percent.
//        /// </summary>
//        public double GetInitConnectedPct()
//        {
//            return this.initConnectedPct;
//        }

//        /// <summary>
//        /// Clears all state.
//        /// </summary>
//        public void Clear()
//        {
//            activeCells.Clear();
//            winnerCells.Clear();
//            predictiveCells.Clear();
//            matchingCells.Clear();
//            matchingSegments.Clear();
//            successfullyPredictedColumns.Clear();
//            activeSegments.Clear();
//            learningSegments.Clear();
//        }

//        /// <summary>
//        /// Atomically returns the segment counter
//        /// </summary>
//        /// <returns></returns>
//        public int GetSegmentCount()
//        {
//            return (int)Interlocked.Read(ref segmentCounter);
//            //return segmentCounter.Get();
//        }

//        /// <summary>
//        /// Atomically increments and returns the incremented count.
//        /// </summary>
//        /// <returns></returns>
//        public int IncrementSegments()
//        {
//            return (int)Interlocked.Increment(ref segmentCounter) - 1;
//            //return segmentCounter.GetAndIncrement();
//        }

//        /// <summary>
//        /// Atomically decrements and returns the decremented count.
//        /// </summary>
//        /// <returns></returns>
//        public int DecrementSegments()
//        {
//            return (int)Interlocked.Decrement(ref segmentCounter) + 1;
//            //return segmentCounter.GetAndDecrement();
//        }

//        /// <summary>
//        /// Atomically sets the segment counter
//        /// </summary>
//        /// <param name="counter"></param>
//        public void SetSegmentCount(int counter)
//        {
//            Interlocked.Exchange(ref segmentCounter, counter);
//            //this.segmentCounter.Set(counter);
//        }

//        /// <summary>
//        /// Returns the iteration/cycle count.
//        /// </summary>
//        /// <returns></returns>
//        public int GetIterationNum()
//        {
//            return iterationNum;
//        }

//        /// <summary>
//        /// Sets the iteration/cycle count.
//        /// </summary>
//        /// <param name="num"></param>
//        public void SetIterationNum(int num)
//        {
//            this.iterationNum = num;
//        }

//        /// <summary>
//        /// Returns the period count which is the number of cycles between meta information updates.
//        /// </summary>
//        public int GetUpdatePeriod()
//        {
//            return updatePeriod;
//        }

//        /// <summary>
//        /// Sets the update period between meta information updates.
//        /// </summary>
//        /// <param name="period"></param>
//        public void SetUpdatePeriod(int period)
//        {
//            this.updatePeriod = period;
//        }

//        /**
//         * Returns the <see cref="Cell"/> specified by the index passed in.
//         * @param index		of the specified cell to return.
//         * @return
//         */
//        public Cell GetCell(int index)
//        {
//            return cells[index];
//        }

//        /// <summary>
//        /// Returns an array containing all of the <see cref="Cell"/>s.
//        /// </summary>
//        /// <returns></returns>
//        public Cell[] GetCells()
//        {
//            return cells;
//        }

//        /// <summary>
//        /// Sets the flat array of cells
//        /// </summary>
//        /// <param name="cells">Cells to set</param>
//        public void SetCells(Cell[] cells)
//        {
//            this.cells = cells;
//        }

//        /**
//         * Returns an array containing the <see cref="Cell"/>s specified
//         * by the passed in indexes.
//         *
//         * @param cellIndexes	indexes of the Cells to return
//         * @return
//         */
//        public Cell[] GetCells(int[] cellIndexes)
//        {
//            Cell[] retVal = new Cell[cellIndexes.Length];
//            for (int i = 0; i < cellIndexes.Length; i++)
//            {
//                retVal[i] = cells[cellIndexes[i]];
//            }
//            return retVal;
//        }

//        /**
//         * Returns a {@link LinkedHashSet} containing the <see cref="Cell"/>s specified
//         * by the passed in indexes.
//         *
//         * @param cellIndexes	indexes of the Cells to return
//         * @return
//         */
//        public HashSet<Cell> GetCellSet(int[] cellIndexes)
//        {
//            HashSet<Cell> retVal = new HashSet<Cell>(); // cellIndexes.Length
//            for (int i = 0; i < cellIndexes.Length; i++)
//            {
//                retVal.Add(cells[cellIndexes[i]]);
//            }
//            return retVal;
//        }

//        /**
//         * Sets the seed used for the internal random number generator.
//         * If the generator has been instantiated, this method will initialize
//         * a new random generator with the specified seed.
//         *
//         * @param seed
//         */
//        public void SetSeed(int seed)
//        {
//            this.seed = seed;
//            this.random = new MersenneTwister(seed);
//        }

//        /// <summary>
//        /// Returns the configured random number seed
//        /// </summary>
//        public int GetSeed()
//        {
//            return seed;
//        }

//        /**
//         * Returns the thread specific {@link Random} number generator.
//         * @return
//         */
//        public IRandom GetRandom()
//        {
//            return random;
//        }

//        /**
//         * Sets the random number generator.
//         * @param random
//         */
//        public void SetRandom(IRandom random)
//        {
//            this.random = random;
//        }

//        /**
//         * Sets the matrix containing the <see cref="Column"/>s
//         * @param mem
//         */
//        public void SetMemory(SparseObjectMatrix<Column> mem)
//        {
//            this.memory = mem;
//        }

//        /**
//         * Returns the matrix containing the <see cref="Column"/>s
//         * @return
//         */
//        public SparseObjectMatrix<Column> GetMemory()
//        {
//            return memory;
//        }

//        /**
//         * Returns the input column mapping
//         */
//        public ISparseMatrix GetInputMatrix()
//        {
//            return inputMatrix;
//        }

//        /**
//         * Sets the input column mapping matrix
//         * @param matrix
//         */
//        public void SetInputMatrix(ISparseMatrix matrix)
//        {
//            this.inputMatrix = matrix;
//        }

//        /**
//         * Returns the inhibition radius
//         * @return
//         */
//        public int GetInhibitionRadius()
//        {
//            return inhibitionRadius;
//        }

//        /**
//         * Sets the inhibition radius
//         * @param radius
//         */
//        public void SetInhibitionRadius(int radius)
//        {
//            this.inhibitionRadius = radius;
//        }

//        /**
//         * Returns the product of the input dimensions
//         * @return  the product of the input dimensions
//         */
//        public int GetNumInputs()
//        {
//            return numInputs;
//        }

//        /**
//         * Sets the product of the input dimensions to
//         * establish a flat count of bits in the input field.
//         * @param n
//         */
//        public void SetNumInputs(int n)
//        {
//            this.numInputs = n;
//        }

//        /**
//         * Returns the product of the column dimensions
//         * @return  the product of the column dimensions
//         */
//        public int GetNumColumns()
//        {
//            return numColumns;
//        }

//        /**
//         * Sets the product of the column dimensions to be
//         * the column count.
//         * @param n
//         */
//        public void SetNumColumns(int n)
//        {
//            this.numColumns = n;
//            this.paOverlaps = new double[n];
//        }

//        /**
//         * This parameter determines the extent of the input
//         * that each column can potentially be connected to.
//         * This can be thought of as the input bits that
//         * are visible to each column, or a 'receptiveField' of
//         * the field of vision. A large enough value will result
//         * in 'global coverage', meaning that each column
//         * can potentially be connected to every input bit. This
//         * parameter defines a square (or hyper square) area: a
//         * column will have a max square potential pool with
//         * sides of length 2 * potentialRadius + 1.
//         *
//         * @param potentialRadius
//         */
//        public void SetPotentialRadius(int potentialRadius)
//        {
//            this.potentialRadius = potentialRadius;
//        }

//        /**
//         * Returns the configured potential radius
//         * @return  the configured potential radius
//         * @see setPotentialRadius
//         */
//        public int GetPotentialRadius()
//        {
//            return Math.Min(numInputs, potentialRadius);
//        }

//        /**
//         * The percent of the inputs, within a column's
//         * potential radius, that a column can be connected to.
//         * If set to 1, the column will be connected to every
//         * input within its potential radius. This parameter is
//         * used to give each column a unique potential pool when
//         * a large potentialRadius causes overlap between the
//         * columns. At initialization time we choose
//         * ((2*potentialRadius + 1)^(# inputDimensions) *
//         * potentialPct) input bits to comprise the column's
//         * potential pool.
//         *
//         * @param potentialPct
//         */
//        public void SetPotentialPct(double potentialPct)
//        {
//            this.potentialPct = potentialPct;
//        }

//        /**
//         * Returns the configured potential pct
//         *
//         * @return the configured potential pct
//         * @see setPotentialPct
//         */
//        public double GetPotentialPct()
//        {
//            return potentialPct;
//        }

//        /**
//         * Sets the {@link SparseObjectMatrix} which represents the
//         * proximal dendrite permanence values.
//         *
//         * @param s the {@link SparseObjectMatrix}
//         */
//        public void SetPermanences(SparseObjectMatrix<double[]> s)
//        {
//            //for (int idx : s.getSparseIndices())
//            foreach(int idx in s.GetSparseIndices())
//            {
//                memory.GetObject(idx).SetProximalPermanences(this, s.GetObject(idx));
//            }
//        }

//        /**
//         * Atomically returns the count of {@link Synapse}s
//         * @return
//         */
//        public int GetSynapseCount()
//        {
//            return (int)Interlocked.Read(ref synapseCounter);
//        }

//        /**
//         * Atomically sets the count of {@link Synapse}s
//         * @param i
//         */
//        public void SetSynapseCount(int i)
//        {
//            Interlocked.Exchange(ref synapseCounter, i);
//            //this.synapseCounter.Set(i);
//        }

//        /**
//         * Atomically increments and returns the incremented
//         * {@link Synapse} count.
//         *
//         * @return
//         */
//        public int IncrementSynapses()
//        {
//            return (int)Interlocked.Increment(ref synapseCounter) - 1;
//            //return this.synapseCounter.GetAndIncrement();
//        }

//        /**
//         * Atomically decrements and returns the decremented
//         * {link Synapse} count
//         * @return
//         */
//        public int DecrementSynapses()
//        {
//            return (int)Interlocked.Decrement(ref synapseCounter) + 1;
//            //return this.synapseCounter.GetAndDecrement();
//        }

//        /**
//         * Returns the indexed count of connected synapses per column.
//         * @return
//         */
//        public Matrix<double> GetConnectedCounts()
//        {
//            return connectedCounts;
//        }

//        /**
//         * Returns the connected count for the specified column.
//         * @param columnIndex
//         * @return
//         */
//        public int GetConnectedCount(int columnIndex)
//        {
//            return (int)connectedCounts.Row(columnIndex).Sum();
//            //return connectedCounts.GetTrueCount(columnIndex);
//        }

//        ///**
//        // * Sets the indexed count of synapses connected at the columns in each index.
//        // * @param counts
//        // */
//        //public void SetConnectedCounts(int[] counts)
//        //{
//        //    for (int i = 0; i < counts.Length; i++)
//        //    {
//        //        //connectedCounts.SetTrueCount(i, counts[i]);
//        //        throw new InvalidOperationException();
//        //    }
//        //}

//        /**
//         * Sets the connected count {@link AbstractSparseBinaryMatrix}
//         * @param columnIndex
//         * @param count
//         */
//        public void SetConnectedMatrix(Matrix<double> matrix)
//        {
//            this.connectedCounts = matrix;
//        }

//        /**
//         * Sets the array holding the random noise added to proximal dendrite overlaps.
//         *
//         * @param tieBreaker	random values to help break ties
//         */
//        public void SetTieBreaker(double[] tieBreaker)
//        {
//            this.tieBreaker = tieBreaker;
//        }

//        /// <summary>
//        /// Returns the array holding random values used to add to overlap scores to break ties.
//        /// </summary>
//        public double[] GetTieBreaker()
//        {
//            return tieBreaker;
//        }

//        /**
//         * If true, then during inhibition phase the winning
//         * columns are selected as the most active columns from
//         * the region as a whole. Otherwise, the winning columns
//         * are selected with respect to their local
//         * neighborhoods. Using global inhibition boosts
//         * performance x60.
//         *
//         * @param globalInhibition
//         */
//        public void SetGlobalInhibition(bool globalInhibition)
//        {
//            this.globalInhibition = globalInhibition;
//        }

//        /**
//         * Returns the configured global inhibition flag
//         * @return  the configured global inhibition flag
//         *
//         * @see setGlobalInhibition
//         */
//        public bool GetGlobalInhibition()
//        {
//            return globalInhibition;
//        }

//        /**
//         * The desired density of active columns within a local
//         * inhibition area (the size of which is set by the
//         * internally calculated inhibitionRadius, which is in
//         * turn determined from the average size of the
//         * connected potential pools of all columns). The
//         * inhibition logic will insure that at most N columns
//         * remain ON within a local inhibition area, where N =
//         * localAreaDensity * (total number of columns in
//         * inhibition area).
//         *
//         * @param localAreaDensity
//         */
//        public void SetLocalAreaDensity(double localAreaDensity)
//        {
//            this.localAreaDensity = localAreaDensity;
//        }

//        /**
//         * Returns the configured local area density
//         * @return  the configured local area density
//         * @see setLocalAreaDensity
//         */
//        public double GetLocalAreaDensity()
//        {
//            return localAreaDensity;
//        }

//        /**
//         * An alternate way to control the density of the active
//         * columns. If numActivePerInhArea is specified then
//         * localAreaDensity must be less than 0, and vice versa.
//         * When using numActivePerInhArea, the inhibition logic
//         * will insure that at most 'numActivePerInhArea'
//         * columns remain ON within a local inhibition area (the
//         * size of which is set by the internally calculated
//         * inhibitionRadius, which is in turn determined from
//         * the average size of the connected receptive fields of
//         * all columns). When using this method, as columns
//         * learn and grow their effective receptive fields, the
//         * inhibitionRadius will grow, and hence the net density
//         * of the active columns will *decrease*. This is in
//         * contrast to the localAreaDensity method, which keeps
//         * the density of active columns the same regardless of
//         * the size of their receptive fields.
//         *
//         * @param numActiveColumnsPerInhArea
//         */
//        public void SetNumActiveColumnsPerInhArea(double numActiveColumnsPerInhArea)
//        {
//            this.numActiveColumnsPerInhArea = numActiveColumnsPerInhArea;
//        }

//        /**
//         * Returns the configured number of active columns per
//         * inhibition area.
//         * @return  the configured number of active columns per
//         * inhibition area.
//         * @see setNumActiveColumnsPerInhArea
//         */
//        public double GetNumActiveColumnsPerInhArea()
//        {
//            return numActiveColumnsPerInhArea;
//        }

//        /**
//         * This is a number specifying the minimum number of
//         * synapses that must be on in order for a columns to
//         * turn ON. The purpose of this is to prevent noise
//         * input from activating columns. Specified as a percent
//         * of a fully grown synapse.
//         *
//         * @param stimulusThreshold
//         */
//        public void SetStimulusThreshold(double stimulusThreshold)
//        {
//            this.stimulusThreshold = stimulusThreshold;
//        }

//        /**
//         * Returns the stimulus threshold
//         * @return  the stimulus threshold
//         * @see setStimulusThreshold
//         */
//        public double GetStimulusThreshold()
//        {
//            return stimulusThreshold;
//        }

//        /**
//         * The amount by which an inactive synapse is
//         * decremented in each round. Specified as a percent of
//         * a fully grown synapse.
//         *
//         * @param synPermInactiveDec
//         */
//        public void SetSynPermInactiveDec(double synPermInactiveDec)
//        {
//            this.synPermInactiveDec = synPermInactiveDec;
//        }

//        /**
//         * Returns the synaptic permanence inactive decrement.
//         * @return  the synaptic permanence inactive decrement.
//         * @see setSynPermInactiveDec
//         */
//        public double GetSynPermInactiveDec()
//        {
//            return synPermInactiveDec;
//        }

//        /**
//         * The amount by which an active synapse is incremented
//         * in each round. Specified as a percent of a
//         * fully grown synapse.
//         *
//         * @param synPermActiveInc
//         */
//        public void SetSynPermActiveInc(double synPermActiveInc)
//        {
//            this.synPermActiveInc = synPermActiveInc;
//        }

//        /**
//         * Returns the configured active permanence increment
//         * @return the configured active permanence increment
//         * @see setSynPermActiveInc
//         */
//        public double GetSynPermActiveInc()
//        {
//            return synPermActiveInc;
//        }

//        /**
//         * The default connected threshold. Any synapse whose
//         * permanence value is above the connected threshold is
//         * a "connected synapse", meaning it can contribute to
//         * the cell's firing.
//         *
//         * @param synPermConnected
//         */
//        public void SetSynPermConnected(double synPermConnected)
//        {
//            this.synPermConnected = synPermConnected;
//        }

//        /**
//         * Returns the synapse permanence connected threshold
//         * @return the synapse permanence connected threshold
//         * @see setSynPermConnected
//         */
//        public double GetSynPermConnected()
//        {
//            return synPermConnected;
//        }

//        /**
//         * Sets the stimulus increment for synapse permanences below
//         * the measured threshold.
//         * @param stim
//         */
//        public void SetSynPermBelowStimulusInc(double stim)
//        {
//            this.synPermBelowStimulusInc = stim;
//        }

//        /**
//         * Returns the stimulus increment for synapse permanences below
//         * the measured threshold.
//         *
//         * @return
//         */
//        public double GetSynPermBelowStimulusInc()
//        {
//            return synPermBelowStimulusInc;
//        }

//        /**
//         * A number between 0 and 1.0, used to set a floor on
//         * how often a column should have at least
//         * stimulusThreshold active inputs. Periodically, each
//         * column looks at the overlap duty cycle of
//         * all other columns within its inhibition radius and
//         * sets its own internal minimal acceptable duty cycle
//         * to: minPctDutyCycleBeforeInh * max(other columns'
//         * duty cycles).
//         * On each iteration, any column whose overlap duty
//         * cycle falls below this computed value will  get
//         * all of its permanence values boosted up by
//         * synPermActiveInc. Raising all permanences in response
//         * to a sub-par duty cycle before  inhibition allows a
//         * cell to search for new inputs when either its
//         * previously learned inputs are no longer ever active,
//         * or when the vast majority of them have been
//         * "hijacked" by other columns.
//         *
//         * @param minPctOverlapDutyCycle
//         */
//        public void SetMinPctOverlapDutyCycles(double minPctOverlapDutyCycle)
//        {
//            this.minPctOverlapDutyCycles = minPctOverlapDutyCycle;
//        }

//        /**
//         * see {@link #setMinPctOverlapDutyCycles(double)}
//         * @return
//         */
//        public double GetMinPctOverlapDutyCycles()
//        {
//            return minPctOverlapDutyCycles;
//        }

//        /**
//         * A number between 0 and 1.0, used to set a floor on
//         * how often a column should be activate.
//         * Periodically, each column looks at the activity duty
//         * cycle of all other columns within its inhibition
//         * radius and sets its own internal minimal acceptable
//         * duty cycle to:
//         *   minPctDutyCycleAfterInh *
//         *   max(other columns' duty cycles).
//         * On each iteration, any column whose duty cycle after
//         * inhibition falls below this computed value will get
//         * its internal boost factor increased.
//         *
//         * @param minPctActiveDutyCycle
//         */
//        public void SetMinPctActiveDutyCycles(double minPctActiveDutyCycle)
//        {
//            this.minPctActiveDutyCycles = minPctActiveDutyCycle;
//        }

//        /**
//         * Returns the minPctActiveDutyCycle
//         * see {@link #setMinPctActiveDutyCycles(double)}
//         * @return  the minPctActiveDutyCycle
//         */
//        public double GetMinPctActiveDutyCycles()
//        {
//            return minPctActiveDutyCycles;
//        }

//        /**
//         * The period used to calculate duty cycles. Higher
//         * values make it take longer to respond to changes in
//         * boost or synPerConnectedCell. Shorter values make it
//         * more unstable and likely to oscillate.
//         *
//         * @param dutyCyclePeriod
//         */
//        public void SetDutyCyclePeriod(int dutyCyclePeriod)
//        {
//            this.dutyCyclePeriod = dutyCyclePeriod;
//        }

//        /**
//         * Returns the configured duty cycle period
//         * see {@link #setDutyCyclePeriod(double)}
//         * @return  the configured duty cycle period
//         */
//        public int GetDutyCyclePeriod()
//        {
//            return dutyCyclePeriod;
//        }

//        /**
//         * The maximum overlap boost factor. Each column's
//         * overlap gets multiplied by a boost factor
//         * before it gets considered for inhibition.
//         * The actual boost factor for a column is number
//         * between 1.0 and maxBoost. A boost factor of 1.0 is
//         * used if the duty cycle is &gt;= minOverlapDutyCycle,
//         * maxBoost is used if the duty cycle is 0, and any duty
//         * cycle in between is linearly extrapolated from these
//         * 2 end points.
//         *
//         * @param maxBoost
//         */
//        public void SetMaxBoost(double maxBoost)
//        {
//            this.maxBoost = maxBoost;
//        }

//        /**
//         * Returns the max boost
//         * see {@link #setMaxBoost(double)}
//         * @return  the max boost
//         */
//        public double GetMaxBoost()
//        {
//            return maxBoost;
//        }

//        /**
//         * spVerbosity level: 0, 1, 2, or 3
//         *
//         * @param spVerbosity
//         */
//        public void SetSpVerbosity(int spVerbosity)
//        {
//            this.spVerbosity = spVerbosity;
//        }

//        /**
//         * Returns the verbosity setting.
//         * see {@link #setSpVerbosity(int)}
//         * @return  the verbosity setting.
//         */
//        public int GetSpVerbosity()
//        {
//            return spVerbosity;
//        }

//        /**
//         * Sets the synPermTrimThreshold
//         * @param threshold
//         */
//        public void SetSynPermTrimThreshold(double threshold)
//        {
//            this.synPermTrimThreshold = threshold;
//        }

//        /**
//         * Returns the synPermTrimThreshold
//         * @return
//         */
//        public double GetSynPermTrimThreshold()
//        {
//            return synPermTrimThreshold;
//        }

//        /**
//         * Sets the {@link FlatMatrix} which holds the mapping
//         * of column indexes to their lists of potential inputs.
//         *
//         * @param pools		{@link FlatMatrix} which holds the pools.
//         */
//        public void SetPotentialPools(IFlatMatrix<Pool> pools)
//        {
//            this.potentialPools = pools;
//        }

//        /// <summary>
//        /// Returns the <see cref="IFlatMatrix{Pool}"/> which holds the mapping
//        /// of column indexes to their lists of potential inputs.
//        /// </summary>
//        /// <returns>the potential pools</returns>
//        public IFlatMatrix<Pool> GetPotentialPools()
//        {
//            return this.potentialPools;
//        }

//        /**
//         * Returns the minimum {@link Synapse} permanence.
//         * @return
//         */
//        public double GetSynPermMin()
//        {
//            return synPermMin;
//        }

//        /**
//         * Returns the maximum {@link Synapse} permanence.
//         * @return
//         */
//        public double GetSynPermMax()
//        {
//            return synPermMax;
//        }

//        /**
//         * Returns the output setting for verbosity
//         * @return
//         */
//        public int GetVerbosity()
//        {
//            return spVerbosity;
//        }

//        /**
//         * Returns the version number
//         * @return
//         */
//        public double GetVersion()
//        {
//            return version;
//        }

//        /**
//         * Returns the overlap duty cycles.
//         * @return
//         */
//        public double[] GetOverlapDutyCycles()
//        {
//            return overlapDutyCycles;
//        }

//        public void SetOverlapDutyCycles(double[] overlapDutyCycles)
//        {
//            this.overlapDutyCycles = overlapDutyCycles;
//        }

//        /**
//         * Returns the dense (size=numColumns) array of duty cycle stats.
//         * @return	the dense array of active duty cycle values.
//         */
//        public double[] GetActiveDutyCycles()
//        {
//            return activeDutyCycles;
//        }

//        /**
//         * Sets the dense (size=numColumns) array of duty cycle stats.
//         * @param activeDutyCycles
//         */
//        public void SetActiveDutyCycles(double[] activeDutyCycles)
//        {
//            this.activeDutyCycles = activeDutyCycles;
//        }

//        /**
//         * Applies the dense array values which aren't -1 to the array containing
//         * the active duty cycles of the column corresponding to the index specified.
//         * The length of the specified array must be as long as the configured number
//         * of columns of this {@code Connections}' column configuration.
//         *
//         * @param	denseActiveDutyCycles	a dense array containing values to set.
//         */
//        public void UpdateActiveDutyCycles(double[] denseActiveDutyCycles)
//        {
//            for (int i = 0; i < denseActiveDutyCycles.Length; i++)
//            {
//                if (denseActiveDutyCycles[i] != -1)
//                {
//                    activeDutyCycles[i] = denseActiveDutyCycles[i];
//                }
//            }
//        }

//        public double[] GetMinOverlapDutyCycles()
//        {
//            return minOverlapDutyCycles;
//        }

//        public void SetMinOverlapDutyCycles(double[] minOverlapDutyCycles)
//        {
//            this.minOverlapDutyCycles = minOverlapDutyCycles;
//        }

//        public double[] GetMinActiveDutyCycles()
//        {
//            return minActiveDutyCycles;
//        }

//        public void SetMinActiveDutyCycles(double[] minActiveDutyCycles)
//        {
//            this.minActiveDutyCycles = minActiveDutyCycles;
//        }

//        public double[] GetBoostFactors()
//        {
//            return boostFactors;
//        }

//        public void SetBoostFactors(double[] boostFactors)
//        {
//            this.boostFactors = boostFactors;
//        }

//        /**
//         * Returns the current count of {@link Synapse}s for {@link ProximalDendrite}s.
//         * @return
//         */
//        public int GetProxSynCount()
//        {
//            return proximalSynapseCounter;
//        }

//        /**
//         * High verbose output useful for debugging
//         */
//        public void PrintParameters()
//        {
//            Console.WriteLine("------------ SpatialPooler Parameters ------------------");
//            Console.WriteLine("numInputs                  = " + GetNumInputs());
//            Console.WriteLine("numColumns                 = " + GetNumColumns());
//            Console.WriteLine("cellsPerColumn             = " + GetCellsPerColumn());
//            Console.WriteLine("columnDimensions           = " + Arrays.ToString(GetColumnDimensions()));
//            Console.WriteLine("numActiveColumnsPerInhArea = " + GetNumActiveColumnsPerInhArea());
//            Console.WriteLine("potentialPct               = " + GetPotentialPct());
//            Console.WriteLine("potentialRadius            = " + GetPotentialRadius());
//            Console.WriteLine("globalInhibition           = " + GetGlobalInhibition());
//            Console.WriteLine("localAreaDensity           = " + GetLocalAreaDensity());
//            Console.WriteLine("inhibitionRadius           = " + GetInhibitionRadius());
//            Console.WriteLine("stimulusThreshold          = " + GetStimulusThreshold());
//            Console.WriteLine("synPermActiveInc           = " + GetSynPermActiveInc());
//            Console.WriteLine("synPermInactiveDec         = " + GetSynPermInactiveDec());
//            Console.WriteLine("synPermConnected           = " + GetSynPermConnected());
//            Console.WriteLine("minPctOverlapDutyCycle     = " + GetMinPctOverlapDutyCycles());
//            Console.WriteLine("minPctActiveDutyCycle      = " + GetMinPctActiveDutyCycles());
//            Console.WriteLine("dutyCyclePeriod            = " + GetDutyCyclePeriod());
//            Console.WriteLine("maxBoost                   = " + GetMaxBoost());
//            Console.WriteLine("spVerbosity                = " + GetSpVerbosity());
//            Console.WriteLine("version                    = " + GetVersion());
            
//            Console.WriteLine("\n------------ TemporalMemory Parameters ------------------");
//            Console.WriteLine("activationThreshold        = " + GetActivationThreshold());
//            Console.WriteLine("learningRadius             = " + GetLearningRadius());
//            Console.WriteLine("minThreshold               = " + GetMinThreshold());
//            Console.WriteLine("maxNewSynapseCount         = " + GetMaxNewSynapseCount());
//            Console.WriteLine("initialPermanence          = " + GetInitialPermanence());
//            Console.WriteLine("connectedPermanence        = " + GetConnectedPermanence());
//            Console.WriteLine("permanenceIncrement        = " + GetPermanenceIncrement());
//            Console.WriteLine("permanenceDecrement        = " + GetPermanenceDecrement());
//        }
//        /// <summary>
//        /// If true this will initialize and run the spatial pooler multithreaded, this will 
//        /// make the network less deterministic because the random generator will return different values on different places,
//        /// even when a seed is used. (can be a problem for unit tests)
//        /// </summary>
//        public bool IsSpatialInParallelMode()
//        {
//            return spParallelMode;
//        }

//        /////////////////////////////// Temporal Memory //////////////////////////////

//        /**
//         * Returns the current {@link Set} of active <see cref="Cell"/>s
//         *
//         * @return  the current {@link Set} of active <see cref="Cell"/>s
//         */
//        public HashSet<Cell> GetActiveCells()
//        {
//            return activeCells;
//        }

//        /**
//         * Sets the current {@link Set} of active <see cref="Cell"/>s
//         * @param cells
//         */
//        public void SetActiveCells(HashSet<Cell> cells)
//        {
//            this.activeCells = cells;
//        }

//        /**
//         * Returns the current {@link Set} of winner cells
//         *
//         * @return  the current {@link Set} of winner cells
//         */
//        public HashSet<Cell> GetWinnerCells()
//        {
//            return winnerCells;
//        }

//        /**
//         * Sets the current {@link Set} of winner <see cref="Cell"/>s
//         * @param cells
//         */
//        public void SetWinnerCells(HashSet<Cell> cells)
//        {
//            this.winnerCells = cells;
//        }

//        /**
//         * Returns the {@link Set} of predictive cells.
//         * @return
//         */
//        public HashSet<Cell> GetPredictiveCells()
//        {
//            return predictiveCells;
//        }

//        /**
//         * Sets the current {@link Set} of predictive <see cref="Cell"/>s
//         * @param cells
//         */
//        public void SetPredictiveCells(HashSet<Cell> cells)
//        {
//            this.predictiveCells = cells;
//        }

//        /**
//         * Returns the Set of matching <see cref="Cell"/>s
//         * @return
//         */
//        public HashSet<Cell> GetMatchingCells()
//        {
//            return matchingCells;
//        }

//        /**
//         * Sets the Set of matching <see cref="Cell"/>s
//         * @param cells
//         */
//        public void SetMatchingCells(HashSet<Cell> cells)
//        {
//            this.matchingCells = cells;
//        }

//        /**
//         * Returns the {@link Set} of columns successfully predicted from t - 1.
//         *
//         * @return  the current {@link Set} of predicted columns
//         */
//        public HashSet<Column> GetSuccessfullyPredictedColumns()
//        {
//            return successfullyPredictedColumns;
//        }

//        /**
//         * Sets the {@link Set} of columns successfully predicted from t - 1.
//         * @param columns
//         */
//        public void SetSuccessfullyPredictedColumns(HashSet<Column> columns)
//        {
//            this.successfullyPredictedColumns = columns;
//        }

//        /**
//         * Returns the Set of learning {@link DistalDendrite}s
//         * @return
//         */
//        public HashSet<DistalDendrite> GetLearningSegments()
//        {
//            return learningSegments;
//        }

//        /**
//         * Sets the {@link Set} of learning segments
//         * @param segments
//         */
//        public void SetLearningSegments(HashSet<DistalDendrite> segments)
//        {
//            this.learningSegments = segments;
//        }

//        /**
//         * Returns the Set of active {@link DistalDendrite}s
//         * @return
//         */
//        public HashSet<DistalDendrite> GetActiveSegments()
//        {
//            return activeSegments;
//        }

//        /**
//         * Sets the {@link Set} of active {@link Segment}s
//         * @param segments
//         */
//        public void SetActiveSegments(HashSet<DistalDendrite> segments)
//        {
//            this.activeSegments = segments;
//        }

//        /**
//         * Returns the Set of matching {@link DistalDendrite}s
//         * @return
//         */
//        public HashSet<DistalDendrite> GetMatchingSegments()
//        {
//            return matchingSegments;
//        }

//        /**
//         * Sets the Set of matching {@link DistalDendrite}s
//         * @param segments
//         */
//        public void SetMatchingSegments(HashSet<DistalDendrite> segments)
//        {
//            this.matchingSegments = segments;
//        }

//        /**
//         * Returns the mapping of <see cref="Cell"/>s to their reverse mapped
//         * {@link Synapse}s.
//         *
//         * @param cell      the <see cref="Cell"/> used as a key.
//         * @return          the mapping of <see cref="Cell"/>s to their reverse mapped
//         *                  {@link Synapse}s.
//         */
//        public List<Synapse> GetReceptorSynapses(Cell cell)
//        {
//            if (cell == null)
//            {
//                throw new ArgumentNullException(nameof(cell), "Cell was null");
//            }

//            if (receptorSynapses == null)
//            {
//                receptorSynapses = new Map<Cell, List<Synapse>>();
//            }

//            List<Synapse> retVal;
//            if (!receptorSynapses.TryGetValue(cell, out retVal))
//            {
//                receptorSynapses.Add(cell, retVal = new List<Synapse>());
//            }

//            return retVal;
//        }

//        /**
//         * Returns the mapping of <see cref="Cell"/>s to their {@link DistalDendrite}s.
//         *
//         * @param cell      the <see cref="Cell"/> used as a key.
//         * @return          the mapping of <see cref="Cell"/>s to their {@link DistalDendrite}s.
//         */
//        public List<DistalDendrite> GetSegments(Cell cell)
//        {
//            if (cell == null)
//            {
//                throw new ArgumentNullException(nameof(cell), "Cell was null");
//            }

//            if (segments == null)
//            {
//                segments = new Map<Cell, List<DistalDendrite>>();
//            }

//            List<DistalDendrite> retVal = null;

//            if (!segments.TryGetValue(cell, out retVal))
//            {
//                segments.Add(cell, retVal = new List<DistalDendrite>());
//            }

//            return retVal;
//        }

//        /**
//         * Returns the mapping of {@link DistalDendrite}s to their {@link Synapse}s.
//         *
//         * @param segment   the {@link DistalDendrite} used as a key.
//         * @return          the mapping of {@link DistalDendrite}s to their {@link Synapse}s.
//         */
//        public List<Synapse> GetSynapses(DistalDendrite segment)
//        {
//            if (segment == null)
//            {
//                throw new ArgumentNullException(nameof(segment), "Segment was null");
//            }

//            if (synapses == null)
//            {
//                synapses = new Map<Segment, List<Synapse>>();
//            }

//            List<Synapse> retVal = null;
//            if (!synapses.TryGetValue(segment, out retVal))
//            {
//                synapses.Add(segment, retVal = new List<Synapse>());
//            }

//            return retVal;
//        }

//        /**
//         * Returns the mapping of {@link ProximalDendrite}s to their {@link Synapse}s.
//         *
//         * @param segment   the {@link ProximalDendrite} used as a key.
//         * @return          the mapping of {@link ProximalDendrite}s to their {@link Synapse}s.
//         */
//        public List<Synapse> GetSynapses(ProximalDendrite segment)
//        {
//            if (segment == null)
//            {
//                throw new ArgumentNullException(nameof(segment), "Segment was null");
//            }

//            if (synapses == null)
//            {
//                synapses = new Map<Segment, List<Synapse>>();
//            }

//            List<Synapse> retVal = null;
//            if (!synapses.TryGetValue(segment, out retVal))
//            {
//                synapses.Add(segment, retVal = new List<Synapse>());
//            }

//            return retVal;
//        }

//        /**
//         * Returns the column at the specified index.
//         * @param index
//         * @return
//         */
//        public Column GetColumn(int index)
//        {
//            return memory.GetObject(index);
//        }

//        /**
//         * Sets the number of <see cref="Column"/>.
//         *
//         * @param columnDimensions
//         */
//        public void SetColumnDimensions(int[] columnDimensions)
//        {
//            this.columnDimensions = columnDimensions;
//        }

//        /**
//         * Gets the number of <see cref="Column"/>.
//         *
//         * @return columnDimensions
//         */
//        public int[] GetColumnDimensions()
//        {
//            return this.columnDimensions;
//        }

//        /**
//         * A list representing the dimensions of the input
//         * vector. Format is [height, width, depth, ...], where
//         * each value represents the size of the dimension. For a
//         * topology of one dimension with 100 inputs use 100, or
//         * [100]. For a two dimensional topology of 10x5 use
//         * [10,5].
//         *
//         * @param inputDimensions
//         */
//        public void SetInputDimensions(int[] inputDimensions)
//        {
//            this.inputDimensions = inputDimensions;
//        }

//        /**
//         * Returns the configured input dimensions
//         * see {@link #setInputDimensions(int[])}
//         * @return the configured input dimensions
//         */
//        public int[] GetInputDimensions()
//        {
//            return inputDimensions;
//        }

//        /**
//         * Sets the number of <see cref="Cell"/>s per <see cref="Column"/>
//         * @param cellsPerColumn
//         */
//        public void SetCellsPerColumn(int cellsPerColumn)
//        {
//            this.cellsPerColumn = cellsPerColumn;
//        }

//        /**
//         * Gets the number of <see cref="Cell"/>s per <see cref="Column"/>.
//         *
//         * @return cellsPerColumn
//         */
//        public int GetCellsPerColumn()
//        {
//            return this.cellsPerColumn;
//        }

//        /**
//         * Sets the activation threshold.
//         *
//         * If the number of active connected synapses on a segment
//         * is at least this threshold, the segment is said to be active.
//         *
//         * @param activationThreshold
//         */
//        public void SetActivationThreshold(int activationThreshold)
//        {
//            this.activationThreshold = activationThreshold;
//        }

//        /**
//         * Returns the activation threshold.
//         * @return
//         */
//        public int GetActivationThreshold()
//        {
//            return activationThreshold;
//        }

//        /**
//         * Radius around cell from which it can
//         * sample to form distal dendrite connections.
//         *
//         * @param   learningRadius
//         */
//        public void SetLearningRadius(int learningRadius)
//        {
//            this.learningRadius = learningRadius;
//        }

//        /**
//         * Returns the learning radius.
//         * @return
//         */
//        public int GetLearningRadius()
//        {
//            return learningRadius;
//        }

//        /**
//         * If the number of synapses active on a segment is at least this
//         * threshold, it is selected as the best matching
//         * cell in a bursting column.
//         *
//         * @param   minThreshold
//         */
//        public void SetMinThreshold(int minThreshold)
//        {
//            this.minThreshold = minThreshold;
//        }

//        /**
//         * Returns the minimum threshold of active synapses to be picked as best.
//         * @return
//         */
//        public int GetMinThreshold()
//        {
//            return minThreshold;
//        }

//        /**
//         * The maximum number of synapses added to a segment during learning.
//         *
//         * @param   maxNewSynapseCount
//         */
//        public void SetMaxNewSynapseCount(int maxNewSynapseCount)
//        {
//            this.maxNewSynapseCount = maxNewSynapseCount;
//        }

//        /**
//         * Returns the maximum number of synapses added to a segment during
//         * learning.
//         *
//         * @return
//         */
//        public int GetMaxNewSynapseCount()
//        {
//            return maxNewSynapseCount;
//        }

//        /**
//         * Initial permanence of a new synapse
//         *
//         * @param   initialPermanence
//         */
//        public void SetInitialPermanence(double initialPermanence)
//        {
//            this.initialPermanence = initialPermanence;
//        }

//        /**
//         * Returns the initial permanence setting.
//         * @return
//         */
//        public double GetInitialPermanence()
//        {
//            return initialPermanence;
//        }

//        /**
//         * If the permanence value for a synapse
//         * is greater than this value, it is said
//         * to be connected.
//         *
//         * @param connectedPermanence
//         */
//        public void SetConnectedPermanence(double connectedPermanence)
//        {
//            this.connectedPermanence = connectedPermanence;
//        }

//        /// <summary>
//        /// If the permanence value for a synapse is greater than this value, it is said to be connected.
//        /// </summary>
//        /// <returns></returns>
//        public double GetConnectedPermanence()
//        {
//            return connectedPermanence;
//        }

//        /**
//         * Amount by which permanences of synapses
//         * are incremented during learning.
//         *
//         * @param   permanenceIncrement
//         */
//        public void SetPermanenceIncrement(double permanenceIncrement)
//        {
//            this.permanenceIncrement = permanenceIncrement;
//        }

//        /**
//         * Amount by which permanences of synapses
//         * are incremented during learning.
//         */
//        public double GetPermanenceIncrement()
//        {
//            return this.permanenceIncrement;
//        }

//        /**
//         * Amount by which permanences of synapses
//         * are decremented during learning.
//         *
//         * @param   permanenceDecrement
//         */
//        public void SetPermanenceDecrement(double permanenceDecrement)
//        {
//            this.permanenceDecrement = permanenceDecrement;
//        }

//        /**
//         * Amount by which permanences of synapses
//         * are decremented during learning.
//         */
//        public double GetPermanenceDecrement()
//        {
//            return this.permanenceDecrement;
//        }

//        /**
//         * Amount by which active permanences of synapses of previously predicted but inactive segments are decremented.
//         * @param predictedSegmentDecrement
//         */
//        public void SetPredictedSegmentDecrement(double predictedSegmentDecrement)
//        {
//            this.predictedSegmentDecrement = predictedSegmentDecrement;
//        }

//        /**
//         * Returns the predictedSegmentDecrement amount.
//         * @return
//         */
//        public double GetPredictedSegmentDecrement()
//        {
//            return this.predictedSegmentDecrement;
//        }

//        /**
//         * Converts a {@link Collection} of <see cref="Cell"/>s to a list
//         * of cell indexes.
//         *
//         * @param cells
//         * @return
//         */
//        public static List<int> AsCellIndexes(IEnumerable<Cell> cells)
//        {
//            List<int> ints = new List<int>();
//            //for (Cell cell : cells)
//            foreach(Cell cell in cells)
//            {
//                ints.Add(cell.GetIndex());
//            }

//            return ints;
//        }

//        /**
//         * Converts a {@link Collection} of <see cref="Column"/>s to a list
//         * of column indexes.
//         *
//         * @param columns
//         * @return
//         */
//        public static List<int> AsColumnIndexes(IEnumerable<Column> columns)
//        {
//            List<int> ints = new List<int>();
//            foreach (Column col in columns)
//            {
//                ints.Add(col.GetIndex());
//            }

//            return ints;
//        }

//        /**
//         * Returns a list of the <see cref="Cell"/>s specified.
//         * @param cells		the indexes of the <see cref="Cell"/>s to return
//         * @return	the specified list of cells
//         */
//        public List<Cell> AsCellObjects(IEnumerable<int> cells)
//        {
//            List<Cell> objs = new List<Cell>();
//            foreach (int i in cells)
//            {
//                objs.Add(this.cells[i]);
//            }
//            return objs;
//        }

//        /**
//         * Returns a list of the <see cref="Column"/>s specified.
//         * @param cols		the indexes of the <see cref="Column"/>s to return
//         * @return		the specified list of columns
//         */
//        public List<Column> AsColumnObjects(IEnumerable<int> cols)
//        {
//            List<Column> objs = new List<Column>();
//            //for (int i : cols)
//            foreach(int i in cols)
//            {
//                objs.Add(this.memory.GetObject(i));
//            }
//            return objs;
//        }

//        /**
//         * Returns a {@link Set} view of the <see cref="Column"/>s specified by
//         * the indexes passed in.
//         *
//         * @param indexes		the indexes of the Columns to return
//         * @return				a set view of the specified columns
//         */
//        public HashSet<Column> GetColumnSet(int[] indexes)
//        {
//            HashSet<Column> retVal = new HashSet<Column>();
//            for (int i = 0; i < indexes.Length; i++)
//            {
//                retVal.Add(memory.GetObject(indexes[i]));
//            }
//            return retVal;
//        }

//        /**
//         * Returns a {@link List} view of the <see cref="Column"/>s specified by
//         * the indexes passed in.
//         *
//         * @param indexes		the indexes of the Columns to return
//         * @return				a List view of the specified columns
//         */
//        public List<Column> GetColumnList(int[] indexes)
//        {
//            List<Column> retVal = new List<Column>();
//            for (int i = 0; i < indexes.Length; i++)
//            {
//                retVal.Add(memory.GetObject(indexes[i]));
//            }
//            return retVal;
//        }
//    }
//}