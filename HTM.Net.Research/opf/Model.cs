using System;
using System.Collections.Generic;
using HTM.Net.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
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
        protected int? NumPredictions;
        protected InferenceType InferenceType;
        private bool _learningEnabled;
        private bool _inferenceEnabled;
        private InferenceArgsDescription _inferenceArgs;

        /// <summary>
        /// Model constructor
        /// </summary>
        /// <param name="inferenceType">A value that specifies the type of inference (i.e. TemporalNextStep, Classification, etc.).</param>
        protected Model(InferenceType inferenceType)
        {
            this.NumPredictions = 0;
            this.InferenceType = inferenceType;
            this._learningEnabled = true;
            this._inferenceEnabled = true;
            this._inferenceArgs = new InferenceArgsDescription();
        }

        public virtual ModelResult Run((IDictionary<string, object>, string[]) inputRecord)
        {
            int? predictionNumber;
            if (NumPredictions.HasValue)
            {
                predictionNumber = this.NumPredictions.Value;
                NumPredictions++;
            }
            else
            {
                predictionNumber = null;
            }
            var result = new ModelResult(predictionNumber: predictionNumber, rawInput: inputRecord.Item1);
            return result;
        }

        public abstract void FinishLearning();
        public abstract void ResetSequenceStates();
        public abstract List<FieldMetaInfo> GetFieldInfo(bool includeClassifierOnlyField = false);
        public abstract void SetFieldStatistics(Map<string, double> fieldStats);
        public abstract void GetRuntimeStats();
        protected abstract ILog GetLogger();

        // Common learning/inference methods

        public virtual InferenceType GetInferenceType()
        {
            return InferenceType;
        }

        public void EnableLearning()
        {
            _learningEnabled = true;
        }
        public void DisableLearning()
        {
            _learningEnabled = false;
        }
        public bool IsLearningEnabled()
        {
            return _learningEnabled;
        }

        public void EnableInference(InferenceArgsDescription inferenceArgs = null)
        {
            _inferenceEnabled = true;
            _inferenceArgs = inferenceArgs;
        }
        public InferenceArgsDescription GetInferenceArgs()
        {
            return _inferenceArgs;
        }
        public void DisableInference()
        {
            _inferenceEnabled = false;
        }
        public bool IsInferenceEnabled()
        {
            return _inferenceEnabled;
        }


        public virtual void StartNetwork(int numRecords)
        {
        }
    }
}