using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Model;
using HTM.Net.Util;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;

namespace HTM.Net.Algorithms
{
    /**
     * Handles the relationships between the columns of a region 
     * and the inputs bits. The primary public interface to this function is the 
     * "compute" method, which takes in an input vector and returns a list of 
     * activeColumns columns.
     * Example Usage:
     * >
     * > SpatialPooler sp = SpatialPooler();
     * > Connections c = new Connections();
     * > sp.init(c);
     * > for line in file:
     * >   inputVector = prepared int[] (containing 1's and 0's)
     * >   sp.compute(inputVector)
     * 
     *
     */
     [Serializable]
    public class SpatialPooler : Persistable
     {
        /** Default Serial Version  */
        private const long serialVersionUID = 1L;

        /// <summary>
        /// Constructs a new <see cref="SpatialPooler"/>
        /// </summary>
        public SpatialPooler() { }

        /**
         * Initializes the specified {@link Connections} object which contains
         * the memory and structural anatomy this spatial pooler uses to implement
         * its algorithms.
         * 
         * @param c		a {@link Connections} object
         */
        public void Init(Connections c)
        {
            if (c.GetNumActiveColumnsPerInhArea() == 0 
                && (c.GetLocalAreaDensity() == 0 || c.GetLocalAreaDensity() > 0.5))
            {
                throw new InvalidSpatialPoolerParamValueException("Inhibition parameters are invalid");
            }

            c.DoSpatialPoolerPostInit();

            InitMatrices(c);
            ConnectAndConfigureInputs(c);
        }

        /// <summary>
        /// Called to initialize the structural anatomy with configured values and 
        /// prepare the anatomical entities for activation.
        /// </summary>
        /// <param name="c"></param>

        public void InitMatrices(Connections c)
        {
            Control.TryUseNativeMKL();
            Control.ConfigureAuto();

            //Control.UseMultiThreading();

            SparseObjectMatrix<Column> mem = c.GetMemory();
            c.SetMemory(mem == null ? mem = new SparseObjectMatrix<Column>(c.GetColumnDimensions()) : mem);

            c.SetInputMatrix(new SparseBinaryMatrix(c.GetInputDimensions()));

            // Initiate the topologies
            c.SetColumnTopology(new Topology(c.GetColumnDimensions()));
            c.SetInputTopology(new Topology(c.GetInputDimensions()));

            //Calculate numInputs and numColumns
            int numInputs = c.GetInputMatrix().GetMaxIndex() + 1;
            int numColumns = c.GetMemory().GetMaxIndex() + 1;
            if (numColumns <= 0)
            {
                throw new InvalidSpatialPoolerParamValueException("Invalid number of columns: " + numColumns);
            }
            if (numInputs <= 0)
            {
                throw new InvalidSpatialPoolerParamValueException("Invalid number of inputs: " + numInputs);
            }
            c.SetNumInputs(numInputs);
            c.SetNumColumns(numColumns);

            //Fill the sparse matrix with column objects
            for (int i = 0; i < numColumns; i++) { mem.Set(i, new Column(c.GetCellsPerColumn(), i)); }

            c.SetPotentialPools(new SparseObjectMatrix<Pool>(c.GetMemory().GetDimensions()));
            
            c.SetConnectedMatrix(Matrix<float>.Build.Sparse(numColumns, numInputs));

            //Initialize state meta-management statistics
            c.SetOverlapDutyCycles(new double[numColumns]);
            c.SetActiveDutyCycles(new double[numColumns]);
            c.SetMinOverlapDutyCycles(new double[numColumns]);
            c.SetMinActiveDutyCycles(new double[numColumns]);
            c.SetBoostFactors(new double[numColumns]);
            Arrays.Fill(c.GetBoostFactors(), 1);
        }

        //public void InitMatrices_Old(Connections c)
        //{
        //    bool runInParallel = c.IsSpatialInParallelMode();
        //    if (runInParallel)
        //    {
        //        Control.UseMultiThreading();
        //    }
        //    else
        //    {
        //        Control.TryUseNativeMKL();
        //    }

        //    if (c == null) throw new ArgumentNullException(nameof(c));
        //    SparseObjectMatrix<Column> mem = c.GetMemory() ?? new SparseObjectMatrix<Column>(c.GetColumnDimensions());
        //    c.SetMemory(mem);

        //    c.SetInputMatrix(new SparseBinaryMatrix(c.GetInputDimensions()));

        //    //Calculate numInputs and numColumns
        //    int numInputs = c.GetInputMatrix().GetMaxIndex() + 1;
        //    int numColumns = c.GetMemory().GetMaxIndex() + 1;
        //    c.SetNumInputs(numInputs);
        //    c.SetNumColumns(numColumns);

        //    //Fill the sparse matrix with column objects
        //    for (int i = 0; i < numColumns; i++)
        //    {
        //        mem.Set(i, new Column(c.GetCellsPerColumn(), i));
        //    }

        //    c.SetPotentialPools(new SparseObjectMatrix<Pool>(c.GetMemory().GetDimensions()));

        //    if (runInParallel)
        //    {
        //        c.SetConnectedMatrix(new DenseMatrix(numColumns, numInputs));
        //    }
        //    else
        //    {
        //        c.SetConnectedMatrix(new SparseMatrix(numColumns, numInputs));
        //    }

        //    double[] tieBreaker = new double[numColumns];
        //    for (int i = 0; i < numColumns; i++)
        //    {
        //        tieBreaker[i] = 0.01 * c.GetRandom().NextDouble();
        //    }
        //    c.SetTieBreaker(tieBreaker);

        //    //Initialize state meta-management statistics
        //    c.SetOverlapDutyCycles(new double[numColumns]);
        //    c.SetActiveDutyCycles(new double[numColumns]);
        //    c.SetMinOverlapDutyCycles(new double[numColumns]);
        //    c.SetMinActiveDutyCycles(new double[numColumns]);
        //    c.SetBoostFactors(new double[numColumns]);
        //    Arrays.Fill(c.GetBoostFactors(), 1);
        //}

        /**
         * Step two of pooler initialization kept separate from initialization
         * of static members so that they may be set at a different point in 
         * the initialization (as sometimes needed by tests).
         * 
         * This step prepares the proximal dendritic synapse pools with their 
         * initial permanence values and connected inputs.
         * 
         * @param c		the {@link Connections} memory
         */
        public void ConnectAndConfigureInputs(Connections c)
        {
            // Initialize the set of permanence values for each column. Ensure that
            // each column is connected to enough input bits to allow it to be
            // activated.
            int numColumns = c.GetNumColumns();
            for (int i = 0; i < numColumns; i++)
            {
                int[] potential = MapPotential(c, i, c.IsWrapAround());
                Column column = c.GetColumn(i);
                c.GetPotentialPools().Set(i, column.CreatePotentialPool(c, potential));
                double[] perm = InitPermanence(c, potential, i, c.GetInitConnectedPct());
                UpdatePermanencesForColumn(c, perm, column, potential, true);
            }

            // The inhibition radius determines the size of a column's local
            // neighborhood.  A cortical column must overcome the overlap score of
            // columns in its neighborhood in order to become active. This radius is
            // updated every learning round. It grows and shrinks with the average
            // number of connected synapses per column.
            UpdateInhibitionRadius(c);
        }

        /// <summary>
        /// This is the primary public method of the SpatialPooler class. This
        /// function takes a input vector and outputs the indices of the active columns.
        /// If 'learn' is set to True, this method also updates the permanences of the
        /// columns. 
        /// </summary>
        /// <param name="c">Connection memory of the layer</param>
        /// <param name="inputVector">
        /// An array of 0's and 1's that comprises the input to the spatial pooler.
        /// The array will be treated as a one dimensional array, therefore the dimensions of the array
        /// do not have to match the exact dimensions specified in the class constructor. In fact, even a list would suffice.
        /// The number of input bits in the vector must, however, match the number of bits specified by the call to the
        /// constructor. Therefore there must be a '0' or '1' in the array for every input bit.
        /// </param>
        /// <param name="activeArray">
        /// An array whose size is equal to the number of columns.
        /// Before the function returns this array will be populated with 1's at the indices of the active columns, 
        /// and 0's everywhere else.
        /// </param>
        /// <param name="learn">
        /// A boolean value indicating whether learning should be performed. 
        /// Learning entails updating the permanence values of the synapses, and hence modifying the 'state' of the model.
        /// Setting learning to 'off' freezes the SP and has many uses.
        /// For example, you might want to feed in various inputs and examine the resulting SDR's.
        /// </param>
        public void Compute(Connections c, int[] inputVector, int[] activeArray, bool learn)
        {
            if (inputVector.Length != c.GetNumInputs())
            {
                throw new InvalidSpatialPoolerParamValueException(
                        "Input array must be same size as the defined number of inputs: From Params: " + c.GetNumInputs() +
                        ", From Input Vector: " + inputVector.Length);
            }

            UpdateBookeepingVars(c, learn);
            int[] overlaps = c.SetOverlaps(CalculateOverlap(c, inputVector));

            double[] boostedOverlaps;
            if (learn)
            {
                boostedOverlaps = ArrayUtils.Multiply(c.GetBoostFactors(), overlaps);
            }
            else
            {
                boostedOverlaps = ArrayUtils.ToDoubleArray(overlaps);
            }

            int[] activeColumns = InhibitColumns(c, c.SetBoostedOverlaps(boostedOverlaps));

            if (learn)
            {
                AdaptSynapses(c, inputVector, activeColumns);
                UpdateDutyCycles(c, overlaps, activeColumns);
                BumpUpWeakColumns(c);
                UpdateBoostFactors(c);
                if (IsUpdateRound(c))
                {
                    UpdateInhibitionRadius(c);
                    UpdateMinDutyCycles(c);
                }
            }

            Arrays.Fill(activeArray, 0);
            if (activeColumns.Length > 0)
            {
                ArrayUtils.SetIndexesTo(activeArray, activeColumns, 1);
            }
        }

        /**
         * Removes the set of columns who have never been active from the set of
         * active columns selected in the inhibition round. Such columns cannot
         * represent learned pattern and are therefore meaningless if only inference
         * is required. This should not be done when using a random, unlearned SP
         * since you would end up with no active columns.
         *  
         * @param activeColumns	An array containing the indices of the active columns
         * @return	a list of columns with a chance of activation
         */
        public int[] StripUnlearnedColumns(Connections c, int[] activeColumns)
        {
            HashSet<int> active = new HashSet<int>(activeColumns);
            HashSet<int> aboveZero = new HashSet<int>();
            int numCols = c.GetNumColumns();
            double[] colDutyCycles = c.GetActiveDutyCycles();
            for (int i = 0; i < numCols; i++)
            {
                if (colDutyCycles[i] <= 0)
                {
                    aboveZero.Add(i);
                }
            }
            active.ExceptWith(aboveZero);
            List<int> l = new List<int>(active);
            l.Sort();

            return activeColumns.Where(i => c.GetActiveDutyCycles()[i] > 0).ToArray();
            //return Arrays.stream(activeColumns).filter(i->c.getActiveDutyCycles()[i] > 0).toArray();
        }

        /**
         * Updates the minimum duty cycles defining normal activity for a column. A
         * column with activity duty cycle below this minimum threshold is boosted.
         *  
         * @param c
         */
        public void UpdateMinDutyCycles(Connections c)
        {
            if (c.GetGlobalInhibition() || c.GetInhibitionRadius() > c.GetNumInputs())
            {
                UpdateMinDutyCyclesGlobal(c);
            }
            else {
                UpdateMinDutyCyclesLocal(c);
            }
        }

        /**
         * Updates the minimum duty cycles in a global fashion. Sets the minimum duty
         * cycles for the overlap and activation of all columns to be a percent of the
         * maximum in the region, specified by {@link Connections#getMinOverlapDutyCycles()} and
         * minPctActiveDutyCycle respectively. Functionality it is equivalent to
         * {@link #updateMinDutyCyclesLocal(Connections)}, but this function exploits the globalness of the
         * computation to perform it in a straightforward, and more efficient manner.
         * 
         * @param c
         */
        public void UpdateMinDutyCyclesGlobal(Connections c)
        {
            Arrays.Fill(c.GetMinOverlapDutyCycles(), c.GetMinPctOverlapDutyCycles() * ArrayUtils.Max(c.GetOverlapDutyCycles()));
            Arrays.Fill(c.GetMinActiveDutyCycles(), c.GetMinPctActiveDutyCycles() * ArrayUtils.Max(c.GetActiveDutyCycles()));
        }

        /**
         * Updates the minimum duty cycles. The minimum duty cycles are determined
         * locally. Each column's minimum duty cycles are set to be a percent of the
         * maximum duty cycles in the column's neighborhood. Unlike
         * {@link #updateMinDutyCyclesGlobal(Connections)}, here the values can be 
         * quite different for different columns.
         * 
         * @param c
         */
        public void UpdateMinDutyCyclesLocal(Connections c)
        {
            int len = c.GetNumColumns();
            int inhibitionRadius = c.GetInhibitionRadius();
            double[] activeDutyCycles = c.GetActiveDutyCycles();
            double minPctActiveDutyCycles = c.GetMinPctActiveDutyCycles();
            double[] overlapDutyCycles = c.GetOverlapDutyCycles();
            double minPctOverlapDutyCycles = c.GetMinPctOverlapDutyCycles();

            // Parallelize for speed up
            Array.ForEach(ArrayUtils.Range(0,len), i=> {
                int[] neighborhood = GetColumnNeighborhood(c, i, inhibitionRadius);

                double maxActiveDuty = ArrayUtils.Max(
                    ArrayUtils.Sub(activeDutyCycles, neighborhood));
                double maxOverlapDuty = ArrayUtils.Max(
                    ArrayUtils.Sub(overlapDutyCycles, neighborhood));

                c.GetMinActiveDutyCycles()[i] = maxActiveDuty * minPctActiveDutyCycles;

                c.GetMinOverlapDutyCycles()[i] = maxOverlapDuty * minPctOverlapDutyCycles;
            });
        }

        /**
         * Updates the duty cycles for each column. The OVERLAP duty cycle is a moving
         * average of the number of inputs which overlapped with each column. The
         * ACTIVITY duty cycles is a moving average of the frequency of activation for
         * each column.
         * 
         * @param c					the {@link Connections} (spatial pooler memory)
         * @param overlaps			an array containing the overlap score for each column.
         *              			The overlap score for a column is defined as the number
         *              			of synapses in a "connected state" (connected synapses)
         *              			that are connected to input bits which are turned on.
         * @param activeColumns		An array containing the indices of the active columns,
         *              			the sparse set of columns which survived inhibition
         */
        public void UpdateDutyCycles(Connections c, int[] overlaps, int[] activeColumns)
        {
            double[] overlapArray = new double[c.GetNumColumns()];
            double[] activeArray = new double[c.GetNumColumns()];
            ArrayUtils.GreaterThanXThanSetToYInB(overlaps, overlapArray, 0, 1);
            if (activeColumns.Length > 0)
            {
                ArrayUtils.SetIndexesTo(activeArray, activeColumns, 1);
            }

            int period = c.GetDutyCyclePeriod();
            if (period > c.GetIterationNum())
            {
                period = c.GetIterationNum();
            }

            c.SetOverlapDutyCycles(
                    UpdateDutyCyclesHelper(c, c.GetOverlapDutyCycles(), overlapArray, period));

            c.SetActiveDutyCycles(
                    UpdateDutyCyclesHelper(c, c.GetActiveDutyCycles(), activeArray, period));
        }

        /**
         * Updates a duty cycle estimate with a new value. This is a helper
         * function that is used to update several duty cycle variables in
         * the Column class, such as: overlapDutyCucle, activeDutyCycle,
         * minPctDutyCycleBeforeInh, minPctDutyCycleAfterInh, etc. returns
         * the updated duty cycle. Duty cycles are updated according to the following
         * formula:
         * 
         *  
         *            	  (period - 1)*dutyCycle + newValue
         *	dutyCycle := ----------------------------------
         *                        period
         *
         * @param c				the {@link Connections} (spatial pooler memory)
         * @param dutyCycles	An array containing one or more duty cycle values that need
         *              		to be updated
         * @param newInput		A new numerical value used to update the duty cycle
         * @param period		The period of the duty cycle
         * @return
         */
        public double[] UpdateDutyCyclesHelper(Connections c, double[] dutyCycles, double[] newInput, double period)
        {
            return ArrayUtils.Divide(ArrayUtils.Add(ArrayUtils.Multiply(dutyCycles, period - 1), newInput), period);
        }
        
        /**
         * Update the inhibition radius. The inhibition radius is a measure of the
         * square (or hypersquare) of columns that each a column is "connected to"
         * on average. Since columns are are not connected to each other directly, we
         * determine this quantity by first figuring out how many *inputs* a column is
         * connected to, and then multiplying it by the total number of columns that
         * exist for each input. For multiple dimension the aforementioned
         * calculations are averaged over all dimensions of inputs and columns. This
         * value is meaningless if global inhibition is enabled.
         * 
         * @param c     the {@link Connections} (spatial pooler memory)
         */
        public void UpdateInhibitionRadius(Connections c)
        {
            if (c.GetGlobalInhibition())
            {
                c.SetInhibitionRadius(ArrayUtils.Max(c.GetColumnDimensions()));
                return;
            }

            List<double> avgCollected = new List<double>();
            int len = c.GetNumColumns();
            for (int i = 0; i < len; i++)
            {
                avgCollected.Add(AvgConnectedSpanForColumnND(c, i));
            }
            double avgConnectedSpan = ArrayUtils.Average(avgCollected.ToArray());
            double diameter = avgConnectedSpan * AvgColumnsPerInput(c);
            double radius = (diameter - 1) / 2.0d;
            radius = Math.Max(1, radius);
            c.SetInhibitionRadius((int)(radius + 0.5));
        }

        /**
         * The average number of columns per input, taking into account the topology
         * of the inputs and columns. This value is used to calculate the inhibition
         * radius. This function supports an arbitrary number of dimensions. If the
         * number of column dimensions does not match the number of input dimensions,
         * we treat the missing, or phantom dimensions as 'ones'.
         *  
         * @param c		the {@link Connections} (spatial pooler memory)
         * @return
         */
        public virtual double AvgColumnsPerInput(Connections c)
        {
            int[] colDim = Arrays.CopyOf(c.GetColumnDimensions(), c.GetColumnDimensions().Length);
            int[] inputDim = Arrays.CopyOf(c.GetInputDimensions(), c.GetInputDimensions().Length);
            double[] columnsPerInput = ArrayUtils.Divide(
                    ArrayUtils.ToDoubleArray(colDim), ArrayUtils.ToDoubleArray(inputDim), 0, 0);
            return ArrayUtils.Average(columnsPerInput);
        }

        /**
         * The range of connectedSynapses per column, averaged for each dimension.
         * This value is used to calculate the inhibition radius. This variation of
         * the function supports arbitrary column dimensions.
         *  
         * @param c             the {@link Connections} (spatial pooler memory)
         * @param columnIndex   the current column for which to avg.
         * @return
         */
        public virtual double AvgConnectedSpanForColumnND(Connections c, int columnIndex)
        {
            int[] dimensions = c.GetInputDimensions();
            int[] connected = c.GetColumn(columnIndex).GetProximalDendrite().GetConnectedSynapsesSparse(c);
            if (connected == null || connected.Length == 0) return 0;

            int[] maxCoord = new int[c.GetInputDimensions().Length];
            int[] minCoord = new int[c.GetInputDimensions().Length];
            Arrays.Fill(maxCoord, -1);
            Arrays.Fill(minCoord, ArrayUtils.Max(dimensions));
            var inputMatrix = c.GetInputMatrix();
            for (int i = 0; i < connected.Length; i++)
            {
                maxCoord = ArrayUtils.MaxBetween(maxCoord, inputMatrix.ComputeCoordinates(connected[i]));
                minCoord = ArrayUtils.MinBetween(minCoord, inputMatrix.ComputeCoordinates(connected[i]));
            }
            return ArrayUtils.Average(ArrayUtils.Add(ArrayUtils.Subtract(maxCoord, minCoord), 1));
        }

        /**
         * The primary method in charge of learning. Adapts the permanence values of
         * the synapses based on the input vector, and the chosen columns after
         * inhibition round. Permanence values are increased for synapses connected to
         * input bits that are turned on, and decreased for synapses connected to
         * inputs bits that are turned off.
         * 
         * @param c                 the {@link Connections} (spatial pooler memory)
         * @param inputVector       a integer array that comprises the input to
         *                          the spatial pooler. There exists an entry in the array
         *                          for every input bit.
         * @param activeColumns     an array containing the indices of the columns that
         *                          survived inhibition.
         */
        public void AdaptSynapses(Connections c, int[] inputVector, int[] activeColumns)
        {
            int[] inputIndices = ArrayUtils.Where(inputVector, ArrayUtils.INT_GREATER_THAN_0);

            double[] permChanges = new double[c.GetNumInputs()];
            Arrays.Fill(permChanges, -1 * c.GetSynPermInactiveDec());
            ArrayUtils.SetIndexesTo(permChanges, inputIndices, c.GetSynPermActiveInc());
            for (int i = 0; i < activeColumns.Length; i++)
            {
                Pool pool = c.GetPotentialPools().Get(activeColumns[i]);
                double[] perm = pool.GetDensePermanences(c);
                int[] indexes = pool.GetSparsePotential();
                ArrayUtils.RaiseValuesBy(permChanges, perm);
                Column col = c.GetColumn(activeColumns[i]);
                UpdatePermanencesForColumn(c, perm, col, indexes, true);
            }
        }

        /**
         * This method increases the permanence values of synapses of columns whose
         * activity level has been too low. Such columns are identified by having an
         * overlap duty cycle that drops too much below those of their peers. The
         * permanence values for such columns are increased.
         *  
         * @param c
         */
        public void BumpUpWeakColumns(Connections c)
        {
            //int[] weakColumns = ArrayUtils.where(c.GetMemory().Get1DIndexes(), new Condition.Adapter<Integer>() {
            //        public boolean eval(int i)
            //    {
            //        return c.GetOverlapDutyCycles()[i] < c.GetMinOverlapDutyCycles()[i];
            //    }
            //});
            int[] weakColumns = ArrayUtils.Where(c.GetMemory().Get1DIndexes(), i => c.GetOverlapDutyCycles()[i] < c.GetMinOverlapDutyCycles()[i]);

            for (int i = 0; i < weakColumns.Length; i++)
            {
                Pool pool = c.GetPotentialPools().Get(weakColumns[i]);
                double[] perm = pool.GetSparsePermanences();
                ArrayUtils.RaiseValuesBy(c.GetSynPermBelowStimulusInc(), perm);
                int[] indexes = pool.GetSparsePotential();
                Column col = c.GetColumn(weakColumns[i]);
                UpdatePermanencesForColumnSparse(c, perm, col, indexes, true);
            }
        }

        /**
         * This method ensures that each column has enough connections to input bits
         * to allow it to become active. Since a column must have at least
         * 'stimulusThreshold' overlaps in order to be considered during the
         * inhibition phase, columns without such minimal number of connections, even
         * if all the input bits they are connected to turn on, have no chance of
         * obtaining the minimum threshold. For such columns, the permanence values
         * are increased until the minimum number of connections are formed.
         * 
         * @param c					the {@link Connections} memory
         * @param perm				the permanence values
         * @param maskPotential			
         */
        public virtual void RaisePermanenceToThreshold(Connections c, double[] perm, int[] maskPotential)
        {
            if (maskPotential.Length < c.GetStimulusThreshold())
            {
                throw new InvalidOperationException("This is likely due to a " +
                    "value of stimulusThreshold that is too large relative " +
                    "to the input size. [len(mask) < self._stimulusThreshold]");
            }

            ArrayUtils.Clip(perm, c.GetSynPermMin(), c.GetSynPermMax());
            while (true)
            {
                int numConnected = ArrayUtils.ValueGreaterCountAtIndex(c.GetSynPermConnected(), perm, maskPotential);
                if (numConnected >= c.GetStimulusThreshold()) break;
                //Skipping version of "raiseValuesBy" that uses the maskPotential until bug #1322 is fixed
                //in NuPIC - for now increment all bits until numConnected >= stimulusThreshold
                ArrayUtils.RaiseValuesBy(c.GetSynPermBelowStimulusInc(), perm, maskPotential);
            }
        }

        /**
         * This method ensures that each column has enough connections to input bits
         * to allow it to become active. Since a column must have at least
         * 'stimulusThreshold' overlaps in order to be considered during the
         * inhibition phase, columns without such minimal number of connections, even
         * if all the input bits they are connected to turn on, have no chance of
         * obtaining the minimum threshold. For such columns, the permanence values
         * are increased until the minimum number of connections are formed.
         * 
         * Note: This method services the "sparse" versions of corresponding methods
         * 
         * @param c         The {@link Connections} memory
         * @param perm		permanence values
         */
        public virtual void RaisePermanenceToThresholdSparse(Connections c, double[] perm)
        {
            ArrayUtils.Clip(perm, c.GetSynPermMin(), c.GetSynPermMax());
            while (true)
            {
                int numConnected = ArrayUtils.ValueGreaterCount(c.GetSynPermConnected(), perm);
                if (numConnected >= c.GetStimulusThreshold()) break;
                ArrayUtils.RaiseValuesBy(c.GetSynPermBelowStimulusInc(), perm);
            }
        }

        /**
         * This method updates the permanence matrix with a column's new permanence
         * values. The column is identified by its index, which reflects the row in
         * the matrix, and the permanence is given in 'sparse' form, i.e. an array
         * whose members are associated with specific indexes. It is in
         * charge of implementing 'clipping' - ensuring that the permanence values are
         * always between 0 and 1 - and 'trimming' - enforcing sparseness by zeroing out
         * all permanence values below 'synPermTrimThreshold'. It also maintains
         * the consistency between 'permanences' (the matrix storing the
         * permanence values), 'connectedSynapses', (the matrix storing the bits
         * each column is connected to), and 'connectedCounts' (an array storing
         * the number of input bits each column is connected to). Every method wishing
         * to modify the permanence matrix should do so through this method.
         * 
         * @param c                 the {@link Connections} which is the memory model.
         * @param perm              An array of permanence values for a column. The array is
         *                          "dense", i.e. it contains an entry for each input bit, even
         *                          if the permanence value is 0.
         * @param column		    The column in the permanence, potential and connectivity matrices
         * @param maskPotential		The indexes of inputs in the specified <see cref="Column"/>'s pool.
         * @param raisePerm         a boolean value indicating whether the permanence values
         */
        public void UpdatePermanencesForColumn(Connections c, double[] perm, Column column, int[] maskPotential, bool raisePerm)
        {
            if (raisePerm)
            {
                RaisePermanenceToThreshold(c, perm, maskPotential);
            }

            ArrayUtils.LessThanOrEqualXThanSetToY(perm, c.GetSynPermTrimThreshold(), 0);
            ArrayUtils.Clip(perm, c.GetSynPermMin(), c.GetSynPermMax());
            column.SetProximalPermanences(c, perm);
        }

        /**
         * This method updates the permanence matrix with a column's new permanence
         * values. The column is identified by its index, which reflects the row in
         * the matrix, and the permanence is given in 'sparse' form, (i.e. an array
         * whose members are associated with specific indexes). It is in
         * charge of implementing 'clipping' - ensuring that the permanence values are
         * always between 0 and 1 - and 'trimming' - enforcing sparseness by zeroing out
         * all permanence values below 'synPermTrimThreshold'. Every method wishing
         * to modify the permanence matrix should do so through this method.
         * 
         * @param c                 the {@link Connections} which is the memory model.
         * @param perm              An array of permanence values for a column. The array is
         *                          "sparse", i.e. it contains an entry for each input bit, even
         *                          if the permanence value is 0.
         * @param column		    The column in the permanence, potential and connectivity matrices
         * @param raisePerm         a boolean value indicating whether the permanence values
         */
        public void UpdatePermanencesForColumnSparse(Connections c, double[] perm, Column column, int[] maskPotential, bool raisePerm)
        {
            if (raisePerm)
            {
                RaisePermanenceToThresholdSparse(c, perm);
            }

            ArrayUtils.LessThanOrEqualXThanSetToY(perm, c.GetSynPermTrimThreshold(), 0);
            ArrayUtils.Clip(perm, c.GetSynPermMin(), c.GetSynPermMax());
            column.SetProximalPermanencesSparse(c, perm, maskPotential);
        }

        /**
         * Returns a randomly generated permanence value for a synapse that is
         * initialized in a connected state. The basic idea here is to initialize
         * permanence values very close to synPermConnected so that a small number of
         * learning steps could make it disconnected or connected.
         *
         * Note: experimentation was done a long time ago on the best way to initialize
         * permanence values, but the history for this particular scheme has been lost.
         * 
         * @return  a randomly generated permanence value
         */
        public static double InitPermConnected(Connections c)
        {
            double p = c.GetSynPermConnected() + (c.GetSynPermMax() - c.GetSynPermConnected()) * c.randomSpatial.NextDouble();

            // Note from Python implementation on conditioning below:
            // Ensure we don't have too much unnecessary precision. A full 64 bits of
            // precision causes numerical stability issues across platforms and across
            // implementations
            p = ((int)(p * 100000)) / 100000.0d;
            return p;
        }

        /// <summary>
        /// Returns a randomly generated permanence value for a synapses that is to be
        /// initialized in a non-connected state.
        /// </summary>
        /// <param name="c"></param>
        /// <returns>a randomly generated permanence value</returns>
        public static double InitPermNonConnected(Connections c)
        {
            double p = c.GetSynPermConnected() * c.GetRandomForSpatialPooler().NextDouble();

            // Note from Python implementation on conditioning below:
            // Ensure we don't have too much unnecessary precision. A full 64 bits of
            // precision causes numerical stability issues across platforms and across
            // implementations
            p = ((int)(p * 100000)) / 100000.0d;
            return p;
        }
        /**
         * Initializes the permanences of a column. The method
         * returns a 1-D array the size of the input, where each entry in the
         * array represents the initial permanence value between the input bit
         * at the particular index in the array, and the column represented by
         * the 'index' parameter.
         * 
         * @param c                 the {@link Connections} which is the memory model
         * @param potentialPool     An array specifying the potential pool of the column.
         *                          Permanence values will only be generated for input bits
         *                          corresponding to indices for which the mask value is 1.
         *                          WARNING: potentialPool is sparse, not an array of "1's"
         * @param index             the index of the column being initialized
         * @param connectedPct      A value between 0 or 1 specifying the percent of the input
         *                          bits that will start off in a connected state.
         * @return
         */
        public double[] InitPermanence(Connections c, int[] potentialPool, int index, double connectedPct)
        {
            double[] perm = new double[c.GetNumInputs()];
            foreach (int idx in potentialPool)
            {
                if (c.randomSpatial.NextDouble() <= connectedPct)
                {
                    perm[idx] = InitPermConnected(c);
                }
                else
                {
                    perm[idx] = InitPermNonConnected(c);
                }

                perm[idx] = perm[idx] < c.GetSynPermTrimThreshold() ? 0 : perm[idx];
            }
            c.GetColumn(index).SetProximalPermanences(c, perm);
            return perm;
        }

        /**
         * Maps a column to its respective input index, keeping to the topology of
         * the region. It takes the index of the column as an argument and determines
         * what is the index of the flattened input vector that is to be the center of
         * the column's potential pool. It distributes the columns over the inputs
         * uniformly. The return value is an integer representing the index of the
         * input bit. Examples of the expected output of this method:
         * * If the topology is one dimensional, and the column index is 0, this
         *   method will return the input index 0. If the column index is 1, and there
         *   are 3 columns over 7 inputs, this method will return the input index 3.
         * * If the topology is two dimensional, with column dimensions [3, 5] and
         *   input dimensions [7, 11], and the column index is 3, the method
         *   returns input index 8. 
         *   
         * @param columnIndex   The index identifying a column in the permanence, potential
         *                      and connectivity matrices.
         * @return              A boolean value indicating that boundaries should be
         *                      ignored.
         */
        public int MapColumn(Connections c, int columnIndex)
        {
            int[] columnCoords = c.GetMemory().ComputeCoordinates(columnIndex);
            double[] colCoords = ArrayUtils.ToDoubleArray(columnCoords);
            double[] ratios = ArrayUtils.Divide(
                colCoords, ArrayUtils.ToDoubleArray(c.GetColumnDimensions()), 0, 0);
            double[] inputCoords = ArrayUtils.Multiply(
                ArrayUtils.ToDoubleArray(c.GetInputDimensions()), ratios, 0, 0);
            inputCoords = ArrayUtils.Add(
                inputCoords,
                ArrayUtils.Multiply(
                    ArrayUtils.Divide(
                        ArrayUtils.ToDoubleArray(c.GetInputDimensions()),
                        ArrayUtils.ToDoubleArray(c.GetColumnDimensions()), 0, 0),
                    0.5));
            int[] inputCoordInts = ArrayUtils.Clip(ArrayUtils.ToIntArray(inputCoords), c.GetInputDimensions(), -1);
            return c.GetInputMatrix().ComputeIndex(inputCoordInts);
        }

        /**
         * Maps a column to its input bits. This method encapsulates the topology of
         * the region. It takes the index of the column as an argument and determines
         * what are the indices of the input vector that are located within the
         * column's potential pool. The return value is a list containing the indices
         * of the input bits. The current implementation of the base class only
         * supports a 1 dimensional topology of columns with a 1 dimensional topology
         * of inputs. To extend this class to support 2-D topology you will need to
         * override this method. Examples of the expected output of this method:
         * * If the potentialRadius is greater than or equal to the entire input
         *   space, (global visibility), then this method returns an array filled with
         *   all the indices
         * * If the topology is one dimensional, and the potentialRadius is 5, this
         *   method will return an array containing 5 consecutive values centered on
         *   the index of the column (wrapping around if necessary).
         * * If the topology is two dimensional (not implemented), and the
         *   potentialRadius is 5, the method should return an array containing 25
         *   '1's, where the exact indices are to be determined by the mapping from
         *   1-D index to 2-D position.
         * 
         * @param c	            {@link Connections} the main memory model
         * @param columnIndex   The index identifying a column in the permanence, potential
         *                      and connectivity matrices.
         * @param wrapAround    A boolean value indicating that boundaries should be
         *                      ignored.
         * @return
         */
        public int[] MapPotential(Connections c, int columnIndex, bool wrapAround)
        {
            int centerInput = MapColumn(c, columnIndex);
            int[] columnInputs = GetInputNeighborhood(c, centerInput, c.GetPotentialRadius());

            // Select a subset of the receptive field to serve as the
            // the potential pool
            int numPotential = (int)(columnInputs.Length * c.GetPotentialPct() + 0.5);
            int[] retVal = new int[numPotential];
            return ArrayUtils.Sample(columnInputs, ref retVal, c.GetRandomForSpatialPooler());
        }

        /**
         * Performs inhibition. This method calculates the necessary values needed to
         * actually perform inhibition and then delegates the task of picking the
         * active columns to helper functions.
         * 
         * @param c             the {@link Connections} matrix
         * @param overlaps      an array containing the overlap score for each  column.
         *                      The overlap score for a column is defined as the number
         *                      of synapses in a "connected state" (connected synapses)
         *                      that are connected to input bits which are turned on.
         * @return
         */
        public virtual int[] InhibitColumns(Connections c, double[] overlaps)
        {
            overlaps = Arrays.CopyOf(overlaps, overlaps.Length);

            double density;
            double inhibitionArea;
            if ((density = c.GetLocalAreaDensity()) <= 0)
            {
                inhibitionArea = Math.Pow(2 * c.GetInhibitionRadius() + 1, c.GetColumnDimensions().Length);
                inhibitionArea = Math.Min(c.GetNumColumns(), inhibitionArea);
                density = c.GetNumActiveColumnsPerInhArea() / inhibitionArea;
                density = Math.Min(density, 0.5);
            }

            //Add our fixed little bit of random noise to the scores to help break ties.
            //ArrayUtils.d_add(overlaps, c.getTieBreaker());

            if (c.GetGlobalInhibition() || c.GetInhibitionRadius() > ArrayUtils.Max(c.GetColumnDimensions()))
            {
                return InhibitColumnsGlobal(c, overlaps, density);
            }

            return InhibitColumnsLocal(c, overlaps, density);
        }

        /**
         * Perform global inhibition. Performing global inhibition entails picking the
         * top 'numActive' columns with the highest overlap score in the entire
         * region. At most half of the columns in a local neighborhood are allowed to
         * be active.
         * 
         * @param c             the {@link Connections} matrix
         * @param overlaps      an array containing the overlap score for each  column.
         *                      The overlap score for a column is defined as the number
         *                      of synapses in a "connected state" (connected synapses)
         *                      that are connected to input bits which are turned on.
         * @param density       The fraction of columns to survive inhibition.
         * 
         * @return
         */
        public virtual int[] InhibitColumnsGlobal(Connections c, double[] overlaps, double density)
        {
            int numCols = c.GetNumColumns();
            int numActive = (int)(density * numCols);

            var winnersBeforeSort = ArrayUtils.Range(0, overlaps.Length)
                .Select(i => new Tuple<int, double>(i, overlaps[i]))
                .ToList();
            winnersBeforeSort.Sort(c.inhibitionComparator);

            int[] sortedWinnerIndices = winnersBeforeSort.Select(p => p.Item1).ToArray();

            //int[] sortedWinnerIndices = ArrayUtils.Range(0, overlaps.Length)
            //    .Select(i=> new Tuple<int,double>(i, overlaps[i]))
            //    .OrderBy(k => c.inhibitionComparator)
            //    .Select(p=> p.Item1)
            //    .ToArray();

            // Enforce the stimulus threshold
            double stimulusThreshold = c.GetStimulusThreshold();
            int start = sortedWinnerIndices.Length - numActive;
            while (start < sortedWinnerIndices.Length)
            {
                int i = sortedWinnerIndices[start];
                if (overlaps[i] >= stimulusThreshold) break;
                ++start;
            }

            return sortedWinnerIndices.Skip(start).ToArray();
        }

        /**
         * Performs inhibition. This method calculates the necessary values needed to
         * actually perform inhibition and then delegates the task of picking the
         * active columns to helper functions.
         * 
         * @param c			the {@link Connections} matrix
         * @param overlaps	an array containing the overlap score for each  column.
         *              	The overlap score for a column is defined as the number
         *              	of synapses in a "connected state" (connected synapses)
         *              	that are connected to input bits which are turned on.
         * @return
         */
        public virtual int[] InhibitColumnsLocal(Connections c, double[] overlaps, double density)
        {
            double addToWinners = ArrayUtils.Max(overlaps) / 1000.0d;
            if (addToWinners == 0)
            {
                addToWinners = 0.001;
            }
            double[] tieBrokenOverlaps = Arrays.CopyOf(overlaps, overlaps.Length);

            List<int> winners = new List<int>();
            double stimulusThreshold = c.GetStimulusThreshold();
            int inhibitionRadius = c.GetInhibitionRadius();
            for (int i = 0; i < overlaps.Length; i++)
            {
                int column = i;
                if (overlaps[column] >= stimulusThreshold)
                {
                    int[] neighborhood = GetColumnNeighborhood(c, column, inhibitionRadius);
                    double[] neighborhoodOverlaps = ArrayUtils.Sub(tieBrokenOverlaps, neighborhood);

                    long numBigger = neighborhoodOverlaps
                        .AsParallel()
                        .Count(d => d > overlaps[column]);

                    int numActive = (int)(0.5 + density * neighborhood.Length);
                    if (numBigger < numActive)
                    {
                        winners.Add(column);
                        tieBrokenOverlaps[column] += addToWinners;
                    }
                }
            }

            return winners.ToArray();
        }

        /**
         * Update the boost factors for all columns. The boost factors are used to
         * increase the overlap of inactive columns to improve their chances of
         * becoming active. and hence encourage participation of more columns in the
         * learning process. This is a line defined as: y = mx + b boost =
         * (1-maxBoost)/minDuty * dutyCycle + maxFiringBoost. Intuitively this means
         * that columns that have been active enough have a boost factor of 1, meaning
         * their overlap is not boosted. Columns whose active duty cycle drops too much
         * below that of their neighbors are boosted depending on how infrequently they
         * have been active. The more infrequent, the more they are boosted. The exact
         * boost factor is linearly interpolated between the points (dutyCycle:0,
         * boost:maxFiringBoost) and (dutyCycle:minDuty, boost:1.0).
         * 
         *         boostFactor
         *             ^
         * maxBoost _  |
         *             |\
         *             | \
         *       1  _  |  \ _ _ _ _ _ _ _
         *             |
         *             +--------------------> activeDutyCycle
         *                |
         *         minActiveDutyCycle
         */
        public void UpdateBoostFactors(Connections c)
        {
            double[] activeDutyCycles = c.GetActiveDutyCycles();
            double[] minActiveDutyCycles = c.GetMinActiveDutyCycles();

            //Indexes of values > 0
            int[] mask = ArrayUtils.Where(minActiveDutyCycles, ArrayUtils.GREATER_THAN_0);

            double[] boostInterim;
            if (mask.Length < 1)
            {
                boostInterim = c.GetBoostFactors();
            }
            else
            {
                double[] numerator = new double[c.GetNumColumns()];
                Arrays.Fill(numerator, 1 - c.GetMaxBoost());
                boostInterim = ArrayUtils.Divide(numerator, minActiveDutyCycles, 0, 0);
                boostInterim = ArrayUtils.Multiply(boostInterim, activeDutyCycles, 0, 0);
                boostInterim = ArrayUtils.Add(boostInterim, c.GetMaxBoost());
            }

            //ArrayUtils.SetIndexesTo(boostInterim, ArrayUtils.where(activeDutyCycles, new Condition.Adapter<Object>() {
            //int i = 0;
            // public boolean eval(double d) { return d > minActiveDutyCycles[i++]; }
            //}), 1.0d);

            int i = 0;
            ArrayUtils.SetIndexesTo(boostInterim, ArrayUtils.Where(activeDutyCycles, d => d > minActiveDutyCycles[i++]), 1.0d);

            c.SetBoostFactors(boostInterim);
        }

        /**
         * This function determines each column's overlap with the current input
         * vector. The overlap of a column is the number of synapses for that column
         * that are connected (permanence value is greater than '_synPermConnected')
         * to input bits which are turned on. Overlap values that are lower than
         * the 'stimulusThreshold' are ignored. The implementation takes advantage of
         * the SpraseBinaryMatrix class to perform this calculation efficiently.
         *  
         * @param c             the {@link Connections} memory encapsulation
         * @param inputVector   an input array of 0's and 1's that comprises the input to
         *                      the spatial pooler.
         * @return
         */
        public virtual int[] CalculateOverlap(Connections c, int[] inputVector)
        {
            int[] overlaps = new int[c.GetNumColumns()];
            c.GetConnectedCounts().RightVecSumAtNZ(inputVector, overlaps, c.GetStimulusThreshold());
            return overlaps;
        }

        /**
         * Return the overlap to connected counts ratio for a given column
         * @param c
         * @param overlaps
         * @return
         */
        public double[] CalculateOverlapPct(Connections c, int[] overlaps)
        {
            return ArrayUtils.Divide(overlaps, c.GetConnectedCounts().GetTrueCounts());
        }

        /**
         * Returns true if enough rounds have passed to warrant updates of
         * duty cycles
         * 
         * @param c	the {@link Connections} memory encapsulation
         * @return
         */
        public bool IsUpdateRound(Connections c)
        {
            return c.GetIterationNum() % c.GetUpdatePeriod() == 0;
        }

        /**
        * Updates counter instance variables each cycle.
        *  
        * @param c         the {@link Connections} memory encapsulation
        * @param learn     a boolean value indicating whether learning should be
        *                  performed. Learning entails updating the  permanence
        *                  values of the synapses, and hence modifying the 'state'
        *                  of the model. setting learning to 'off' might be useful
        *                  for indicating separate training vs. testing sets.
        */
        public void UpdateBookeepingVars(Connections c, bool learn)
        {
            c.spIterationNum += 1;
            if (learn) c.spIterationLearnNum += 1;
        }

        /**
         * Gets a neighborhood of columns.
         * 
         * Simply calls topology.neighborhood or topology.wrappingNeighborhood
         * 
         * A subclass can insert different topology behavior by overriding this method.
         * 
         * @param c                     the {@link Connections} memory encapsulation
         * @param centerColumn          The center of the neighborhood.
         * @param inhibitionRadius      Span of columns included in each neighborhood
         * @return                      The columns in the neighborhood (1D)
         */
        public int[] GetColumnNeighborhood(Connections c, int centerColumn, int inhibitionRadius)
        {
            return c.IsWrapAround() ?
                c.GetColumnTopology().WrappingNeighborhood(centerColumn, inhibitionRadius) :
                    c.GetColumnTopology().Neighborhood(centerColumn, inhibitionRadius);
        }

        /**
         * Gets a neighborhood of inputs.
         * 
         * Simply calls topology.wrappingNeighborhood or topology.neighborhood.
         * 
         * A subclass can insert different topology behavior by overriding this method.
         * 
         * @param c                     the {@link Connections} memory encapsulation
         * @param centerInput           The center of the neighborhood.
         * @param potentialRadius       Span of the input field included in each neighborhood
         * @return                      The input's in the neighborhood. (1D)
         */
        public int[] GetInputNeighborhood(Connections c, int centerInput, int potentialRadius)
        {
            return c.IsWrapAround() ?
                c.GetInputTopology().WrappingNeighborhood(centerInput, potentialRadius) :
                    c.GetInputTopology().Neighborhood(centerInput, potentialRadius);
        }

        public class InvalidSpatialPoolerParamValueException : Exception
        {
            public InvalidSpatialPoolerParamValueException(string message)
                : base(message)
            {

            }
        }
    }
}