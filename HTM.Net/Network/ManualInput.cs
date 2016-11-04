using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    /// <summary>
    /// Abstraction used within the Network API, to contain the significant return values of all <see cref="ILayer"/>
    /// inference participating algorithms.
    /// Namely:
    /// <ul>
    ///      <li>Input Value</li>
    ///      <li>Bucket Index</li>
    ///      <li>SDR</li>
    ///      <li>Previous SDR</li>
    ///      <li><see cref="Classification{T}"/></li>
    ///      <li>anomalyScore</li>
    /// </ul>
    /// </summary>
    /// <remarks>
    /// All of these fields are "optional", (meaning they depend on the configuration selected by the user 
    /// and may not exist depending on the user's choice of "terminal" point. 
    /// A "Terminal" point is the end point in a chain of a <see cref="ILayer"/>'s contained algorithms. 
    /// For instance, if the user does not include an <see cref="Encoder{T}"/> in the <see cref="ILayer"/> constructor, 
    /// the slot containing the "Bucket Index" will be empty.
    /// </remarks>
    [Serializable]
    public class ManualInput : Persistable, IInference
    {
        private int _recordNum;
        /// <summary>
        /// Tuple = { Name, inputValue, bucketIndex, encoding }
        /// </summary>
        private Map<string, NamedTuple> _classifierInput;
        /// <summary>
        /// Holds one classifier for each field
        /// </summary>
        private NamedTuple _classifiers;
        private object _layerInput;
        private int[] _sdr;
        private int[] _encoding;
        /// <summary>
        /// Active columns in the <see cref="SpatialPooler"/> at time "t"
        /// </summary>
        private int[] _feedForwardActiveColumns;
        /// <summary>
        /// Active column indexes from the <see cref="SpatialPooler"/> at time "t"
        /// </summary>
        private int[] _feedForwardSparseActives;
        /// <summary>
        /// Predictive <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t - 1"
        /// </summary>
        private HashSet<Cell> _previousPredictiveCells;
        /// <summary>
        /// Predictive <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t"
        /// </summary>
        private HashSet<Cell> _predictiveCells;
        /// <summary>
        /// Active <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t"
        /// </summary>
        private HashSet<Cell> _activeCells;

        private Map<string, Classification<object>> _classification;
        private double _anomalyScore;
        private object _customObject;

        private ComputeCycle _computeCycle;

        #region Fluent Setters

        /// <summary>
        /// Sets the current record num associated with this <see cref="ManualInput"/> instance
        /// </summary>
        /// <param name="num">The current sequence number.</param>
        /// <returns>this</returns>
        public ManualInput SetRecordNum(int num)
        {
            _recordNum = num;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="ComputeCycle"/> from the <see cref="TemporalMemory"/>
        /// </summary>
        /// <param name="computeCycle">ComputeCycle to set</param>
        /// <returns></returns>
        public ManualInput SetComputeCycle(ComputeCycle computeCycle)
        {
            this._computeCycle = computeCycle;
            return this;
        }

        /// <summary>
        /// Sets a custom Object during sequence processing where one or more 
        /// <see cref="ILayer.Add(Func{ManualInput,ManualInput})"/> were added to a <see cref="ILayer"/> 
        /// in between algorithmic components.
        /// </summary>
        /// <param name="o"></param>
        /// <returns></returns>
        public ManualInput SetCustomObject(object o)
        {
            this._customObject = o;
            return this;
        }

        /// <summary>
        /// Sets the current classifier input map
        /// </summary>
        /// <param name="classifierInput"></param>
        /// <returns></returns>
        public ManualInput SetClassifierInput(Map<string, NamedTuple> classifierInput)
        {
            this._classifierInput = classifierInput;
            return this;
        }

        /// <summary>
        /// Sets the <see cref="NamedTuple"/> containing the classifiers used for each particular input field.
        /// </summary>
        /// <param name="tuple"></param>
        /// <returns></returns>
        public ManualInput SetClassifiers(NamedTuple tuple)
        {
            this._classifiers = tuple;
            return this;
        }

        #endregion

        #region Getters

        /// <summary>
        /// Returns the current record num associated with this <see cref="ManualInput"/> instance
        /// </summary>
        /// <returns>The current sequence number</returns>
        public int GetRecordNum()
        {
            return _recordNum;
        }

        /// <summary>
        /// Returns the <see cref="ComputeCycle"/>
        /// </summary>
        public ComputeCycle GetComputeCycle()
        {
            return _computeCycle;
        }

        /// <summary>
        /// Returns a custom Object during sequence processing where one or more 
        /// <see cref="ILayer.Add(Func{ManualInput,ManualInput})"/> were added to a <see cref="ILayer"/> 
        /// in between algorithmic components.
        /// </summary>
        /// <returns>the custom object set during processing</returns>
        public object GetCustomObject()
        {
            return _customObject;
        }

        /// <summary>
        /// Returns the <see cref="Map{TKey,TValue}"/> used as input into the <see cref="CLAClassifier"/>
        /// 
        /// This mapping contains the name of the field being classified mapped
        /// to a <see cref="NamedTuple"/> containing:
        /// <ul>
        ///      <li>name</li>
        ///      <li>inputValue</li>
        ///      <li>bucketIdx</li>
        ///      <li>encoding</li>
        /// </ul>
        /// </summary>
        /// <returns>the current classifier input</returns>
        public Map<string, NamedTuple> GetClassifierInput()
        {
            if (_classifierInput == null)
            {
                _classifierInput = new Map<string, NamedTuple>();
            }
            return _classifierInput;
        }

        /// <summary>
        /// Returns a <see cref="NamedTuple"/> keyed to the input field
        /// names, whose values are the <see cref="CLAClassifier"/> used 
        /// to track the classification of a particular field
        /// </summary>
        public NamedTuple GetClassifiers()
        {
            return _classifiers;
        }

        #endregion


        /**
         * Returns the most recent input object
         * 
         * @return      the input
         */
        public object GetLayerInput()
        {
            return _layerInput;
        }

        /**
         * Sets the input object to be used and returns 
         * this <see cref="ManualInput"/>
         * 
         * @param inputValue
         * @return
         */
        public ManualInput SetLayerInput(object inputValue)
        {
            this._layerInput = inputValue;
            return this;
        }

        /**
         * Returns the <em>Sparse Distributed Representation</em> vector
         * which is the result of all algorithms in a series of algorithms
         * passed up the hierarchy.
         * 
         * @return
         */
        public int[] GetSdr()
        {
            return _sdr;
        }

        /**
         * Inputs an <em>Sparse Distributed Representation</em> vector and returns this {@code ManualInput}
         * 
         * @param sdr
         * @return
         */
        public ManualInput SetSdr(int[] sparseDistributedRepresentation)
        {
            this._sdr = sparseDistributedRepresentation;
            return this;
        }

        /**
         * Returns the initial encoding produced by an {@link Encoder}
         * or one of its subtypes.
         * 
         * @return
         */
        public int[] GetEncoding()
        {
            return _encoding;
        }

        /**
         * Inputs the initial encoding and return this {@code ManualInput}
         * @param sdr
         * @return
         */
        public ManualInput SetEncoding(int[] sdr)
        {
            this._encoding = sdr;
            return this;
        }

        /**
         * Convenience method to provide an isolated copy of 
         * this <see cref="IInference"/>
         *  
         * @return
         */
        internal ManualInput Copy()
        {
            ManualInput retVal = new ManualInput();
            retVal._classifierInput = new Map<string, NamedTuple>(this._classifierInput);
            retVal._classifiers = new NamedTuple(this._classifiers.GetKeys(), this._classifiers.Values().ToArray());
            retVal._layerInput = this._layerInput;
            retVal._sdr = Arrays.CopyOf(this._sdr, this._sdr.Length);
            retVal._encoding = Arrays.CopyOf(this._encoding, this._encoding.Length);
            retVal._feedForwardActiveColumns = Arrays.CopyOf(this._feedForwardActiveColumns, this._feedForwardActiveColumns.Length);
            retVal._feedForwardSparseActives = Arrays.CopyOf(this._feedForwardSparseActives, this._feedForwardSparseActives.Length);
            retVal._previousPredictiveCells = new HashSet<Cell>(this._previousPredictiveCells);
            retVal._predictiveCells = new HashSet<Cell>(this._predictiveCells);
            retVal._classification = new Map<string, Classification<object>>(this._classification);
            retVal._anomalyScore = this._anomalyScore;
            retVal._customObject = this._customObject;
            retVal._activeCells = new HashSet<Cell>(this._activeCells);

            return retVal;
        }

        /**
         * Returns the most recent {@link Classification}
         * 
         * @param fieldName
         * @return
         */
        public Classification<object> GetClassification(string fieldName)
        {
            return _classification[fieldName];
        }

        /**
         * Sets the specified field's last classifier computation and returns
         * this <see cref="IInference"/>
         * 
         * @param fieldName
         * @param classification
         * @return
         */
        public ManualInput StoreClassification(string fieldName, Classification<object> classification)
        {
            if (this._classification == null)
            {
                this._classification = new Map<string, Classification<object>>();
            }
            this._classification.Add(fieldName, classification);
            return this;
        }

        /// <summary>
        /// Returns the most recent anomaly calculation.
        /// </summary>
        /// <returns></returns>
        public double GetAnomalyScore()
        {
            return _anomalyScore;
        }

        /// <summary>
        /// Sets the current computed anomaly score and returns this <see cref="IInference"/>
        /// </summary>
        /// <param name="d"></param>
        /// <returns></returns>
        public ManualInput SetAnomalyScore(double d)
        {
            this._anomalyScore = d;
            return this;
        }

        /// <summary>
        /// Returns the column activation from a <see cref="SpatialPooler"/>
        /// </summary>
        /// <returns></returns>
        public int[] GetFeedForwardActiveColumns()
        {
            return _feedForwardActiveColumns;
        }

        /// <summary>
        /// Sets the column activation from a <see cref="SpatialPooler"/>
        /// </summary>
        /// <param name="cols"></param>
        /// <returns></returns>
        public ManualInput SetFeedForwardActiveColumns(int[] cols)
        {
            this._feedForwardActiveColumns = cols;
            return this;
        }

        /// <summary>
        /// Returns the column activation from a <see cref="TemporalMemory"/>
        /// </summary>
        /// <returns></returns>
        public HashSet<Cell> GetActiveCells()
        {
            return _activeCells;
        }

        /// <summary>
        /// Sets the column activation from a <see cref="TemporalMemory"/>
        /// </summary>
        /// <param name="cells"></param>
        /// <returns></returns>
        public ManualInput SetActiveCells(HashSet<Cell> cells)
        {
            this._activeCells = cells;
            return this;
        }

        /// <summary>
        /// Returns the column activations in sparse form
        /// </summary>
        public int[] GetFeedForwardSparseActives()
        {
            if (_feedForwardSparseActives == null && _feedForwardActiveColumns != null)
            {
                //feedForwardSparseActives = feedForwardActiveColumns.Where(n => n == 1).ToArray();
                _feedForwardSparseActives = ArrayUtils.Where(_feedForwardActiveColumns, ArrayUtils.WHERE_1);
            }
            return _feedForwardSparseActives;
        }

        /// <summary>
        /// Sets the column activations in sparse form.
        /// </summary>
        /// <param name="cols"></param>
        /// <returns></returns>
        public ManualInput SetFeedForwardSparseActives(int[] cols)
        {
            this._feedForwardSparseActives = cols;
            return this;
        }

        /// <summary>
        /// Returns the predicted output from the last inference cycle.
        /// </summary>
        public HashSet<Cell> GetPreviousPredictiveCells()
        {
            return _previousPredictiveCells;
        }

        /// <summary>
        /// Sets the previous predicted columns.
        /// Also set by <see cref="SetPredictiveCells"/>
        /// </summary>
        /// <param name="cells"></param>
        /// <returns>The current manual input instance</returns>
        public ManualInput SetPreviousPredictiveCells(HashSet<Cell> cells)
        {
            this._previousPredictiveCells = cells;
            return this;
        }

        /// <summary>
        /// Returns the currently predicted columns.
        /// </summary>
        public HashSet<Cell> GetPredictiveCells()
        {
            return _predictiveCells;
        }

        /// <summary>
        /// Sets the currently predicted columns
        /// </summary>
        /// <param name="cells"></param>
        /// <returns></returns>
        public ManualInput SetPredictiveCells(HashSet<Cell> cells)
        {
            SetPreviousPredictiveCells(_predictiveCells);
            _predictiveCells = cells;
            return this;
        }

        #region Implementation of IPersistable<IInference>

        

        public new virtual object PostDeSerialize(object manualInput)
        {
            ManualInput mi = (ManualInput)manualInput;

            ManualInput retVal = new ManualInput();
            retVal._activeCells = mi._activeCells;
            retVal._anomalyScore = mi._anomalyScore;
            retVal._classification = mi._classification;
            retVal._classifierInput = mi._classifierInput;
            retVal._classifiers = mi._classifiers;
            retVal._customObject = mi._customObject;
            retVal._encoding = mi._encoding;
            retVal._feedForwardActiveColumns = mi._feedForwardActiveColumns;
            retVal._feedForwardSparseActives = mi._feedForwardSparseActives;
            retVal._layerInput = mi._layerInput;
            retVal._predictiveCells = mi._predictiveCells;
            retVal._previousPredictiveCells = mi._previousPredictiveCells;
            retVal._sdr = mi._sdr;

            return retVal;
        }

        #endregion

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((_activeCells == null) ? 0 : _activeCells.GetHashCode());
            long temp;
            temp = BitConverter.DoubleToInt64Bits(_anomalyScore);
            result = prime * result + (int)(temp ^ (temp >> 32));
            result = prime * result + ((_classification == null) ? 0 : _classification.GetHashCode());
            result = prime * result + ((_classifierInput == null) ? 0 : _classifierInput.GetHashCode());
            result = prime * result + ((_computeCycle == null) ? 0 : _computeCycle.GetHashCode());
            result = prime * result + Arrays.GetHashCode(_encoding);
            result = prime * result + Arrays.GetHashCode(_feedForwardActiveColumns);
            result = prime * result + Arrays.GetHashCode(_feedForwardSparseActives);
            result = prime * result + ((_predictiveCells == null) ? 0 : _predictiveCells.GetHashCode());
            result = prime * result + ((_previousPredictiveCells == null) ? 0 : _previousPredictiveCells.GetHashCode());
            result = prime * result + _recordNum;
            result = prime * result + Arrays.GetHashCode(_sdr);
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (!typeof(IInference).IsAssignableFrom(obj.GetType()))
                return false;
            ManualInput other = (ManualInput)obj;
            if (_activeCells == null)
            {
                if (other._activeCells != null)
                    return false;
            }
            else if (!_activeCells.Equals(other._activeCells))
                return false;
            if (BitConverter.DoubleToInt64Bits(_anomalyScore) != BitConverter.DoubleToInt64Bits(other._anomalyScore))
                return false;
            if (_classification == null)
            {
                if (other._classification != null)
                    return false;
            }
            else if (!_classification.Equals(other._classification))
                return false;
            if (_classifierInput == null)
            {
                if (other._classifierInput != null)
                    return false;
            }
            else if (!_classifierInput.Equals(other._classifierInput))
                return false;
            if (_computeCycle == null)
            {
                if (other._computeCycle != null)
                    return false;
            }
            else if (!_computeCycle.Equals(other._computeCycle))
                return false;
            if (!Arrays.AreEqual(_encoding, other._encoding) && _encoding != null && other._encoding != null)
                return false;
            if (!Arrays.AreEqual(_feedForwardActiveColumns, other._feedForwardActiveColumns) && _feedForwardActiveColumns != null && other._feedForwardActiveColumns != null)
                return false;
            if (!Arrays.AreEqual(_feedForwardSparseActives, other._feedForwardSparseActives) && _feedForwardSparseActives != null && other._feedForwardSparseActives != null)
                return false;
            if (_predictiveCells == null)
            {
                if (other._predictiveCells != null)
                    return false;
            }
            else if (!_predictiveCells.Equals(other._predictiveCells))
                return false;
            if (_previousPredictiveCells == null)
            {
                if (other._previousPredictiveCells != null)
                    return false;
            }
            else if (!_previousPredictiveCells.Equals(other._previousPredictiveCells))
                return false;
            if (_recordNum != other._recordNum)
                return false;
            if (!Arrays.AreEqual(_sdr, other._sdr) && _sdr != null && other._sdr != null)
                return false;
            return true;
        }
    }
}