using System;
using System.Collections.Generic;
using HTM.Net.Data;
using HTM.Net.Research.Data;
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
        private InferenceArgsDescription _inferenceArgs;

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
            this._inferenceArgs = new InferenceArgsDescription();
        }

        public virtual ModelResult run(Tuple<Map<string, object>, string[]> inputRecord)
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
            var result = new ModelResult(predictionNumber: predictionNumber, rawInput: inputRecord.Item1);
            return result;
        }

        public abstract void finishLearning();
        public abstract void resetSequenceStates();
        public abstract List<FieldMetaInfo> getFieldInfo(bool includeClassifierOnlyField = false);
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

        public void enableInference(InferenceArgsDescription inferenceArgs = null)
        {
            _inferenceEnabled = true;
            _inferenceArgs = inferenceArgs;
        }
        public InferenceArgsDescription getInferenceArgs()
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