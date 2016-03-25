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
        private bool __learningEnabled;
        private bool __inferenceEnabled;
        private Map<string, object> __inferenceArgs;

        /// <summary>
        /// Model constructor
        /// </summary>
        /// <param name="inferenceType">A value that specifies the type of inference (i.e. TemporalNextStep, Classification, etc.).</param>
        protected Model(InferenceType inferenceType)
        {
            this._numPredictions = 0;
            this.__inferenceType = inferenceType;
            this.__learningEnabled = true;
            this.__inferenceEnabled = true;
            this.__inferenceArgs = new Map<string, object>();
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
            __learningEnabled = true;
        }
        public void disableLearning()
        {
            __learningEnabled = false;
        }
        public bool isLearningEnabled()
        {
            return __learningEnabled;
        }

        public void enableInference(Map<string, object> inferenceArgs = null)
        {
            __inferenceEnabled = true;
            __inferenceArgs = inferenceArgs;
        }
        public Map<string, object> getInferenceArgs()
        {
            return __inferenceArgs;
        }
        public void disableInference()
        {
            __inferenceEnabled = false;
        }
        public bool isInferenceEnabled()
        {
            return __inferenceEnabled;
        }


    }
}