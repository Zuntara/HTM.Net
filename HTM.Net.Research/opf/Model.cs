using System.Collections.Generic;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Research.opf
{
    /// <summary>
    /// This is the base class that all OPF Model implementations should
    /// subclass.
    /// It includes a number of virtual methods, to be overridden by subclasses,
    /// as well as some shared functionality for saving/loading models
    /// </summary>
    public abstract class Model
    {
        protected int? _numPredictions;
        protected InferenceType __inferenceType;
        private bool _learningEnabled;
        private bool _inferenceEnabled;
        private Map<string, object> _inferenceArgs;

        /// <summary>
        /// Model constructor
        /// </summary>
        /// <param name="inferenceType">A value that specifies the type of inference (i.e. TemporalNextStep, Classification, etc.).</param>
        protected Model(InferenceType inferenceType)
        {
            this._numPredictions = 0;
            this.__inferenceType = inferenceType;
            this._learningEnabled = true;
            this._inferenceEnabled = true;
            this._inferenceArgs = new Map<string, object>();
        }

        public virtual ModelResult run(Map<string, object> inputRecord)
        {
            int? predictionNumber;
            if (_numPredictions.HasValue)
            {
                predictionNumber = this._numPredictions.Value;
                _numPredictions++;
            }
            else
            {
                predictionNumber = null;
            }
            var result = new ModelResult(predictionNumber: predictionNumber, rawInput: inputRecord);
            return result;
        }

        public abstract void finishLearning();
        public abstract void resetSequenceStates();
        public abstract void getFieldInfo(bool includeClassifierOnlyField = false);
        public abstract void setFieldStatistics(Map<string, Map<string, object>> fieldStats);
        public abstract void getRuntimeStats();
        protected abstract ILog _getLogger();

        // Common learning/inference methods

        public virtual InferenceType getInferenceType()
        {
            return __inferenceType;
        }

        public void enableLearning()
        {
            _learningEnabled = true;
        }
        public void disableLearning()
        {
            _learningEnabled = false;
        }
        public bool isLearningEnabled()
        {
            return _learningEnabled;
        }

        public void enableInference(Map<string, object> inferenceArgs = null)
        {
            _inferenceEnabled = true;
            _inferenceArgs = inferenceArgs;
        }
        public Map<string, object> getInferenceArgs()
        {
            return _inferenceArgs;
        }
        public void disableInference()
        {
            _inferenceEnabled = false;
        }
        public bool isInferenceEnabled()
        {
            return _inferenceEnabled;
        }


    }
}