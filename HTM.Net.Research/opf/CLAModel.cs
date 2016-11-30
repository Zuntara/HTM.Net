using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Research.opf
{
    public class CLAModel : Model
    {
        // Create publisher from input record strings in dictionary
        // observe the layer and take manual input record to get all the needed data (?)

        #region Fields

        private ClaExperimentParameters _modelConfig;

        private IInference _currentInferenceOutput;
        private Publisher _inputProvider;

        private bool __restoringFromState;
        private bool __restoringFromV1;
        private int __numRunCalls;
        private NetworkInfo _netInfo;
        private Dictionary<string, object> _input;
        private ILog __logger;

        public const double DEFAULT_LIKELIHOOD_THRESHOLD = 0.0001;
        public const int DEFAULT_MAX_PREDICTIONS_PER_STEP = 8;

        public const int DEFAULT_ANOMALY_TRAINRECORDS = 4000;
        public const double DEFAULT_ANOMALY_THRESHOLD = 1.1;
        public const int DEFAULT_ANOMALY_CACHESIZE = 10000;

        private List<InferenceType> __supportedInferenceKindSet = new List<InferenceType>
        {
            InferenceType.TemporalNextStep,
            InferenceType.TemporalClassification,
            InferenceType.NontemporalClassification,
            InferenceType.NontemporalAnomaly,
            InferenceType.TemporalAnomaly,
            InferenceType.TemporalMultiStep,
            InferenceType.NontemporalMultiStep
        };

        private List<InferenceType> __temporalInferenceKindSet = new List<InferenceType>
        {
            InferenceType.TemporalNextStep,
            InferenceType.TemporalClassification,
            InferenceType.TemporalAnomaly,
            InferenceType.TemporalMultiStep,
            InferenceType.NontemporalMultiStep
        };

        private double _minLikelihoodThreshold;
        private int _maxPredictionsPerStep;
        private bool __spLearningEnabled;
        private bool __tpLearningEnabled;
        private bool _hasSP;
        private bool _hasTP;
        private bool _hasCL;
        private Anomaly _anomalyInst;
        private int[] _prevPredictedColumns;
        private bool __trainSPNetOnlyIfRequested;
        private bool __finishedLearning;
        private int? _predictedFieldIdx;
        private string _predictedFieldName;
        private int? _numFields;
        private IEncoder _classifierInputEncoder;
        private double? _ms_prevVal;
        private Map<int, Deque<object>> _ms_predHistories;

        #endregion

        public CLAModel(ClaExperimentParameters modelConfig)
            : base(modelConfig.InferenceType)
        {
            _modelConfig = modelConfig;

            Parameters parameters = modelConfig;

            InferenceType inferenceType = modelConfig.InferenceType;

            if (!__supportedInferenceKindSet.Contains(inferenceType))
            {
                throw new ArgumentException(string.Format("{0} received incompatible inference type: {1}", GetType().Name, inferenceType));
            }

            // Call super class constructor
            //super(CLAModel, self).__init__(inferenceType);

            // this.__restoringFromState is set to True by our __setstate__ method
            // and back to False at completion of our _deSerializeExtraData() method.
            this.__restoringFromState = false;
            this.__restoringFromV1 = false;

            // Intitialize logging
            this.__logger = LogManager.GetLogger(typeof(CLAModel));
            this.__logger.Debug(string.Format("Instantiating {0}.", GetType().Name));

            var minLikelihoodThreshold = DEFAULT_LIKELIHOOD_THRESHOLD;
            var maxPredictionsPerStep = DEFAULT_MAX_PREDICTIONS_PER_STEP;

            this._minLikelihoodThreshold = minLikelihoodThreshold;
            this._maxPredictionsPerStep = maxPredictionsPerStep;

            // set up learning parameters (note: these may be replaced via
            // enable/disable//SP/TP//Learning methods)
            this.__spLearningEnabled = modelConfig.EnableSpatialPooler;
            this.__tpLearningEnabled = modelConfig.EnableTemporalMemory;
            var spEnable = __spLearningEnabled;
            var tpEnable = __tpLearningEnabled;
            var clEnable = modelConfig.EnableClassification;

            // Explicitly exclude the TP if this type of inference doesn't require it
            if (!__temporalInferenceKindSet.Contains(inferenceType)
                || this.getInferenceType() == InferenceType.NontemporalMultiStep)
            {
                tpEnable = false;
            }

            this._netInfo = null;
            this._hasSP = spEnable;
            this._hasTP = tpEnable;
            this._hasCL = clEnable;

            //var anomalyParams = modelConfig.modelParams.anomalyParams;

            //this._classifierInputEncoder = null;
            this._predictedFieldIdx = null;
            this._predictedFieldName = null;
            this._numFields = null;
            // init anomaly
            //int? windowSize = anomalyParams?.slidingWindowSize;// anomalyParams.get("slidingWindowSize", null);
            //Anomaly.Mode mode = anomalyParams?.mode ?? Anomaly.Mode.PURE; // anomalyParams.get("mode", "pure");
            //double? anomalyThreshold = anomalyParams?.autoDetectThreshold;// anomalyParams.get("autoDetectThreshold", null);

            //parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, windowSize);
            //parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, mode);
            //parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_autoDetectThreshold, anomalyThreshold);

            this._anomalyInst = Anomaly.Create(parameters);

            ////this._anomalyInst = new Anomaly(slidingWindowSize = windowSize, mode = mode,
            ////                            binaryAnomalyThreshold = anomalyThreshold);

            // -----------------------------------------------------------------------
            //if (network != null)
            //{
            //    this._netInfo = new NetworkInfo(net: network, statsCollectors:[]);
            //}
            //else
            //{
            // Create the network
            this._netInfo = this.CreateClaNetworkSingleRegionAndLayer(parameters);
            //}


            // Initialize Spatial Anomaly detection parameters
            if (this.getInferenceType() == InferenceType.NontemporalAnomaly)
            {
                //this._getSPRegion().setParameter("anomalyMode", true);
            }

            // Initialize Temporal Anomaly detection parameters
            if (this.getInferenceType() == InferenceType.TemporalAnomaly)
            {
                //this._getTPRegion().setParameter("anomalyMode", true);
                this._prevPredictedColumns = new int[0];
            }

            // -----------------------------------------------------------------------
            // This flag, if present tells us not to train the SP network unless
            //  the user specifically asks for the SP inference metric
            this.__trainSPNetOnlyIfRequested = modelConfig.TrainSPNetOnlyIfRequested;

            this.__numRunCalls = 0;

            // Tracks whether finishedLearning() has been called
            this.__finishedLearning = false;

            this.__logger.Debug("Instantiated " + GetType().Name);

            this._input = null;
        }

        //public CLAModel(IDescription description)
        //    : base(description.modelConfig.modelParams.inferenceType)
        //{
        //    _description = description;

        //    var parameters = description.GetParameters();

        //    InferenceType inferenceType = description.modelConfig.modelParams.inferenceType;

        //    if (!__supportedInferenceKindSet.Contains(inferenceType))
        //    {
        //        throw new ArgumentException(string.Format("{0} received incompatible inference type: {1}", GetType().Name, inferenceType));
        //    }

        //    // Call super class constructor
        //    //super(CLAModel, self).__init__(inferenceType);

        //    // this.__restoringFromState is set to True by our __setstate__ method
        //    // and back to False at completion of our _deSerializeExtraData() method.
        //    this.__restoringFromState = false;
        //    this.__restoringFromV1 = false;

        //    // Intitialize logging
        //    this.__logger = LogManager.GetLogger(typeof(CLAModel));
        //    this.__logger.Debug(string.Format("Instantiating {0}.", GetType().Name));

        //    var minLikelihoodThreshold = DEFAULT_LIKELIHOOD_THRESHOLD;
        //    var maxPredictionsPerStep = DEFAULT_MAX_PREDICTIONS_PER_STEP;

        //    this._minLikelihoodThreshold = minLikelihoodThreshold;
        //    this._maxPredictionsPerStep = maxPredictionsPerStep;

        //    // set up learning parameters (note: these may be replaced via
        //    // enable/disable//SP/TP//Learning methods)
        //    this.__spLearningEnabled = description.modelConfig.modelParams.spEnable;
        //    this.__tpLearningEnabled = description.modelConfig.modelParams.tpEnable;
        //    var spEnable = __spLearningEnabled;
        //    var tpEnable = __tpLearningEnabled;
        //    var clEnable = description.modelConfig.modelParams.clEnable;

        //    // Explicitly exclude the TP if this type of inference doesn't require it
        //    if (!__temporalInferenceKindSet.Contains(inferenceType)
        //        || this.getInferenceType() == InferenceType.NontemporalMultiStep)
        //    {
        //        tpEnable = false;
        //    }

        //    this._netInfo = null;
        //    this._hasSP = spEnable;
        //    this._hasTP = tpEnable;
        //    this._hasCL = clEnable;

        //    //var anomalyParams = description.modelConfig.modelParams

        //    //this._classifierInputEncoder = null;
        //    this._predictedFieldIdx = null;
        //    this._predictedFieldName = null;
        //    this._numFields = null;
        //    // init anomaly
        //    int? windowSize = null;// anomalyParams.get("slidingWindowSize", null);
        //    Anomaly.Mode mode = Anomaly.Mode.PURE; // anomalyParams.get("mode", "pure");
        //    int? anomalyThreshold = null;// anomalyParams.get("autoDetectThreshold", null);

        //    parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_WINDOW_SIZE, windowSize);
        //    parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, mode);
        //    //parameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_autoDetectThreshold, windowSize);

        //    this._anomalyInst = Anomaly.Create(parameters);

        //    ////this._anomalyInst = new Anomaly(slidingWindowSize = windowSize, mode = mode,
        //    ////                            binaryAnomalyThreshold = anomalyThreshold);

        //    // -----------------------------------------------------------------------
        //    //if (network != null)
        //    //{
        //    //    this._netInfo = new NetworkInfo(net: network, statsCollectors:[]);
        //    //}
        //    //else
        //    //{
        //    // Create the network
        //    this._netInfo = this.CreateClaNetwork(parameters);
        //    //}


        //    // Initialize Spatial Anomaly detection parameters
        //    if (this.getInferenceType() == InferenceType.NontemporalAnomaly)
        //    {
        //        //this._getSPRegion().setParameter("anomalyMode", true);
        //    }

        //    // Initialize Temporal Anomaly detection parameters
        //    if (this.getInferenceType() == InferenceType.TemporalAnomaly)
        //    {
        //        //this._getTPRegion().setParameter("anomalyMode", true);
        //        this._prevPredictedColumns = new int[0];
        //    }

        //    // -----------------------------------------------------------------------
        //    // This flag, if present tells us not to train the SP network unless
        //    //  the user specifically asks for the SP inference metric
        //    this.__trainSPNetOnlyIfRequested = description.modelConfig.modelParams.trainSPNetOnlyIfRequested;

        //    this.__numRunCalls = 0;

        //    // Tracks whether finishedLearning() has been called
        //    this.__finishedLearning = false;

        //    this.__logger.Debug("Instantiated " + GetType().Name);

        //    this._input = null;
        //}

        /// <summary>
        /// run one iteration of this model.
        /// </summary>
        /// <param name="inputRecord">
        /// inputRecord is a record object formatted according to nupic.data.RecordStream.getNextRecordDict() result format.
        /// </param>
        /// <returns></returns>
        public override ModelResult run(Tuple<Map<string, object>, string[]> inputRecord)
        {
            Debug.Assert(!__restoringFromState);
            Debug.Assert(inputRecord != null);
            var results = base.run(inputRecord);
            __numRunCalls++;

            results.inferences = new Map<InferenceElement, object>();
            this._input = inputRecord.Item1;
            // Turn learning on or off?
            if (inputRecord.Item1.ContainsKey("_learning"))
            {
                if ((bool)inputRecord.Item1["_learning"])
                {
                    enableLearning();
                }
                else
                {
                    disableLearning();
                }
            }

            //##########################################################################
            // Predictions and Learning
            //##########################################################################
            _layerCompute(inputRecord.Item1, inputRecord.Item2);
            //this._sensorCompute(inputRecord);
            //this._spCompute();
            //this._tpCompute();

            results.sensorInput = _getSensorInputRecord(inputRecord.Item1);

            Map<InferenceElement, object> inferences = this._multiStepCompute(rawInput: inputRecord.Item1);

            results.inferences.Update(inferences);

            inferences = this._anomalyCompute();
            results.inferences.Update(inferences);

            // -----------------------------------------------------------------------
            // Store the index and name of the predictedField
            results.predictedFieldIdx = this._predictedFieldIdx;
            results.predictedFieldName = this._predictedFieldName;
            results.classifierInput = this._getClassifierInputRecord(inputRecord.Item1);

            // =========================================================================
            // output
            Debug.Assert(!this.isInferenceEnabled() || results.inferences != null, "unexpected inferences: " + results.inferences);


            //// this.__logger.setLevel(logging.DEBUG)
            //if (this.__logger.IsDebugEnabled)
            //{
            //    this.__logger.Debug(string.Format("inputRecord: {0}, results: {1}", inputRecord, results));
            //}

            return results;

            //return base.run(inputRecord);
        }

        private void _layerCompute(Map<string, object> inputRecord, string[] rawData)
        {
            // Feed record to the sensor first
            try
            {
                // Push record into the sensor
                ((IHTMSensor) _getSensorRegion()).AssignBasicInputMap(inputRecord, rawData);
                _inputProvider.OnNext(string.Join(",", inputRecord.Values.Select(v => v?.ToString()).ToArray()));
                this._currentInferenceOutput = _netInfo.net.ComputeImmediate(inputRecord);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        private void _sensorCompute(Map<string, object> inputRecord)
        {
            //ISensor sensor = this._getSensorRegion().GetSensor();

            //this._getDataSource().push(inputRecord);
            //sensor.setParameter("topDownMode", false);
            //sensor.prepareInputs();
            //try
            //{
            //    sensor.compute();
            //}
            //catch (Exception)
            //{
            //    throw;
            //}
        }

        private void _spCompute()
        {
            var sp = this._getSPRegion();
            if (sp == null)
                return;

            //sp.setParameter("topDownMode", false);
            //sp.setParameter("inferenceMode", this.isInferenceEnabled());
            //sp.setParameter("learningMode", this.isLearningEnabled());
            //sp.prepareInputs();
            //sp.Compute();
        }

        /// <summary>
        /// Returns reference to the network's SP region
        /// </summary>
        /// <returns></returns>
        private SpatialPooler _getSPRegion()
        {
            return _netInfo.GetLayer().GetSpatialPooler();
            //return this._netInfo.net.regions.get("SP", null);
        }

        /// <summary>
        /// Returns reference to the network's TP region
        /// </summary>
        /// <returns></returns>
        private TemporalMemory _getTPRegion()
        {
            return _netInfo.GetLayer().GetTemporalMemory();
            //return this._netInfo.net.regions.get('TP', None);
        }
        /// <summary>
        /// Returns reference to the network's Sensor region
        /// </summary>
        /// <returns></returns>
        private ISensor _getSensorRegion()
        {
            return _netInfo.GetLayer().GetSensor();
        }

        /// <summary>
        /// Returns reference to the network's Classifier region
        /// </summary>
        /// <returns></returns>
        private IClassifier _getClassifierRegion()
        {
            if (_netInfo.net != null && _hasCL /*&& (bool)_netInfo.net.GetParameters().GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, false)*/)
            {
                var layer = _netInfo.GetLayer();
                var classifier = (IClassifier) layer.GetInference().GetClassifiers()[_predictedFieldName];
                return classifier;
            }
            return null;
        }

        /// <summary>
        /// Returns dict containing the input to the sensor Return a 'SensorInput' object, which represents the 'parsed'
        /// representation of the input record
        /// </summary>
        /// <param name="inputRecord">dict containing the input to the sensor</param>
        /// <returns></returns>
        private SensorInput _getSensorInputRecord(Map<string, object> inputRecord)
        {
            var sensor = _getSensorRegion();
            // inputRecordCategory = int(sensor.getOutputData('categoryOut')[0])
            // resetOut = sensor.getOutputData('resetOut')[0]
            var inference = (ManualInput)_netInfo.GetLayer().GetInference();
            var dataRow = inference.GetLayerInput();
            return new SensorInput(
                dataRow: dataRow,
                dataDict: new Map<string, object>(inputRecord),
                dataEncodings: inference.GetEncoding().Select(e => (object)e).ToList(),
                sequenceReset: null,
                category: null);// todo : fetch inputREcordCategory
        }

        ///// <summary>
        ///// 
        ///// </summary>
        ///// <param name="inputRecord">
        ///// dict containing the input to the sensor Return a 'SensorInput' object, which represents the 'parsed'
        ///// representation of the input record
        ///// </param>
        //private SensorInput _getSensorInputRecord(object inputRecord)
        //{
        //    var sensor = this._getSensorRegion();
        //    var dataRow = copy.deepcopy(sensor.getOutputValues("sourceOut"));
        //    var dataDict = copy.deepcopy(inputRecord);
        //    var inputRecordEncodings = sensor.getOutputValues("sourceEncodings");
        //    var inputRecordCategory = (int)(sensor.getOutputData("categoryOut")[0]);
        //    var resetOut = sensor.getOutputData("resetOut")[0];

        //    return new SensorInput(dataRow: dataRow,
        //                       dataDict: dataDict,
        //                       dataEncodings: inputRecordEncodings,
        //                       sequenceReset: resetOut,
        //                       category: inputRecordCategory);
        //}

        /// <summary>
        /// Compute Anomaly score, if required
        /// </summary>
        /// <returns></returns>
        private Map<InferenceElement, object> _anomalyCompute()
        {

            InferenceType inferenceType = this.getInferenceType();

            Map<InferenceElement, object> inferences = new Map<InferenceElement, object>();
            var sp = this._getSPRegion();
            double? score = null;
            if (inferenceType == InferenceType.NontemporalAnomaly)
            {
                score = _netInfo.GetLayer().GetInference().GetAnomalyScore();
                throw new NotImplementedException();
                //score = sp.getOutputData("anomalyScore")[0]; // TODO move from SP to Anomaly ?
            }

            else if (inferenceType == InferenceType.TemporalAnomaly)
            {
                int[] activeColumns;
                var tp = this._getTPRegion();

                if (sp != null)
                {
                    //activeColumns = sp.getOutputData("bottomUpOut").nonzero()[0];
                    activeColumns = _netInfo.GetLayer().GetInference().GetFeedForwardSparseActives();
                }
                else
                {
                    throw new NotImplementedException();
                    //var sensor = this._getSensorRegion();
                    //activeColumns = sensor.getOutputData('dataOut').nonzero()[0];
                }

                if (!this._input.ContainsKey(this._predictedFieldName))
                {
                    throw new InvalidOperationException(string.Format(
                        "Expected predicted field '{0}' in input row, but was not found!"
                        , this._predictedFieldName));
                }
                // Calculate the anomaly score using the active columns
                // and previous predicted columns.

                score = _netInfo.GetLayer().GetInference().GetAnomalyScore();

                //double anomalyInputValue;
                //if (_input[_predictedFieldName] is string)
                //{
                //    anomalyInputValue = double.Parse(_input[_predictedFieldName] as string,
                //        NumberFormatInfo.InvariantInfo);
                //}
                //else
                //{
                //    anomalyInputValue = (double)_input[_predictedFieldName];
                //}
                //score = _anomalyInst.Compute(activeColumns, _prevPredictedColumns, anomalyInputValue,
                //    this.__numRunCalls);
                ////score = this._anomalyInst.compute(
                ////                             activeColumns,
                ////                             this._prevPredictedColumns,
                ////                             inputValue: this._input[this._predictedFieldName]);

                //// Store the predicted columns for the next timestep.
                //var predictedColumns = tp.GetInference().GetPredictiveCells().Select(c => c.GetColumn().GetIndex()).ToArray();// tp.getOutputData("topDownOut").nonzero()[0];
                //this._prevPredictedColumns = predictedColumns;

                // Calculate the classifier's output and use the result as the anomaly
                // label. Stores as string of results.

                // TODO: make labels work with non-SP models
                if (sp != null)
                {
                    var ac = _getAnomalyClassifier();
                    //this._getAnomalyClassifier().setParameter("activeColumnCount", activeColumns.Length);
                    //this._getAnomalyClassifier().prepareInputs();
                    //this._getAnomalyClassifier().compute();
                    //var labels = this._getAnomalyClassifier().getLabelResults();
                    //inferences[InferenceElement.anomalyLabel] = "%s" % labels;
                }
            }

            inferences[InferenceElement.AnomalyScore] = score;
            return inferences;
        }

        private NamedTuple _getAnomalyClassifier()
        {
            return _netInfo.GetLayer().GetInference().GetClassifiers();
            //.get("AnomalyClassifier", None);
        }

        private void _tpCompute()
        {
            //var tp = this._getTPRegion();
            //if (tp == null)
            //    return;

            //bool topDownCompute;
            //if (this.getInferenceType() == InferenceType.TemporalAnomaly ||
            //    this._isReconstructionModel())
            //{
            //    topDownCompute = true;
            //}
            //else
            //{
            //    topDownCompute = false;
            //}

            //tp = this._getTPRegion();
            //tp.setParameter("topDownMode", topDownCompute);
            //tp.setParameter("inferenceMode", this.isInferenceEnabled());
            //tp.setParameter("learningMode", this.isLearningEnabled());
            //tp.prepareInputs();
            //tp.compute();
        }

        private bool _isReconstructionModel()
        {
            InferenceType inferenceType = this.getInferenceType();
            InferenceArgsDescription inferenceArgs = this.getInferenceArgs();

            if (inferenceType == InferenceType.TemporalNextStep)
            {
                return true;
            }

            if (inferenceArgs != null)
            {
                return inferenceArgs.useReconstruction.GetValueOrDefault(false);
            }
            return false;
        }

        private bool _isMultiStepModel()
        {
            return new List<InferenceType>
            {
                InferenceType.NontemporalMultiStep,
                InferenceType.NontemporalClassification,
                InferenceType.TemporalMultiStep,
                InferenceType.TemporalAnomaly
            }.Contains(this.getInferenceType());
        }

        private bool _isClassificationModel()
        {
            return (this.getInferenceType() & InferenceType.TemporalClassification) == InferenceType.TemporalClassification;
        }

        private Map<InferenceElement, object> _multiStepCompute(Map<string, object> rawInput)
        {
            // list of active input indices
            List<int> patternNZ = null;
            if (this._getTPRegion() != null)
            {
                var tp = this._getTPRegion();
                //var tpOutput = tp._tfdr.infActiveState['t'];
                var tpOutput = _netInfo.GetLayer().GetInference().GetActiveCells().Select(ac => ac.GetIndex());//._tfdr.infActiveState['t'];

                //patternNZ = tpOutput.reshape(-1).nonzero()[0];
                patternNZ = tpOutput.ToList();
            }
            else if (this._getSPRegion() != null)
            {
                var sp = this._getSPRegion();
                //spOutput = sp.getOutputData('bottomUpOut');
                var spOutput = _netInfo.GetLayer().GetInference().GetFeedForwardActiveColumns();
                //patternNZ = spOutput.nonzero()[0];
            }
            else if (this._getSensorRegion() != null)
            {
                var sensor = this._getSensorRegion();
                //sensorOutput = sensor.getOutputData('dataOut');
                //patternNZ = sensorOutput.nonzero()[0];
            }
            else
            {
                throw new InvalidOperationException("Attempted to make multistep prediction without TP, SP, or Sensor regions");
            }

            int? inputTSRecordIdx = null;
            int inputTsRecordIdxInt;
            if (int.TryParse(rawInput.Get("_timestampRecordIdx") as string, out inputTsRecordIdxInt))
            {
                inputTSRecordIdx = inputTsRecordIdxInt;
            }
            return this._handleCLAClassifierMultiStep(
                                                patternNZ: patternNZ,
                                                inputTSRecordIdx: inputTSRecordIdx,
                                                rawInput: rawInput);
        }

        /// <summary>
        ///  Handle the CLA Classifier compute logic when implementing multi-step
        ///  prediction.This is where the patternNZ is associated with one of the
        ///  other fields from the dataset 0 to N steps in the future. This method is
        ///  used by each type of network(encoder only, SP only, SP + TP) to handle the
        ///  compute logic through the CLA Classifier.It fills in the inference dict with
        ///  the results of the compute.
        /// </summary>
        /// <param name="patternNZ">The input the CLA Classifier as a list of active input indices</param>
        /// <param name="inputTSRecordIdx">The index of the record as computed from the timestamp and aggregation interval. 
        /// This normally increments by 1 each time unless there are missing records.If there is no 
        /// aggregation interval or timestamp in the data, this will be null.
        /// </param>
        /// <param name="rawInput">The raw input to the sensor, as a dict.</param>
        /// <returns></returns>
        private Map<InferenceElement, object> _handleCLAClassifierMultiStep(List<int> patternNZ,
            int? inputTSRecordIdx, Map<string, object> rawInput)
        {
            var inferenceArgs = this.getInferenceArgs();
            string predictedFieldName = inferenceArgs.predictedField;
            if (predictedFieldName == null)
            {
                throw new InvalidOperationException("No predicted field was enabled! Did you call enableInference()?");
            }
            this._predictedFieldName = predictedFieldName;

            var classifierLayer = this._getClassifierRegion();
            if (!this._hasCL || classifierLayer == null)
            {
                // No classifier so return an empty dict for inferences.
                return new Map<InferenceElement, object>();
            }

            var sensor = this._getSensorRegion();
            var minLikelihoodThreshold = this._minLikelihoodThreshold;
            var maxPredictionsPerStep = this._maxPredictionsPerStep;
            var needLearning = this.isLearningEnabled();
            var inferences = new Map<InferenceElement, object>();

            // Get the classifier input encoder, if we don't have it already
            if (_classifierInputEncoder == null)
            {
                if (predictedFieldName == null)
                {
                    throw new InvalidOperationException("This experiment description is missing the 'predictedField' in its config, which is required for multi-step prediction inference.");
                }

                List<EncoderTuple> encoderList = sensor.GetEncoder().GetEncoders(sensor.GetEncoder());//.getEncoderList();
                this._numFields = encoderList.Count;

                // This is getting index of predicted field if being fed to CLA.
                var fieldNames = sensor.GetEncoder().GetScalarNames();// encoderList.Select(et => et.GetFieldName()).ToList();
                if (fieldNames != null && fieldNames.Contains(predictedFieldName))
                {
                    this._predictedFieldIdx = fieldNames.OrderBy(k=>k).ToList().IndexOf(predictedFieldName);
                }
                else
                {
                    // Predicted field was not fed into the network, only to the classifier
                    this._predictedFieldIdx = null;
                }

                // In a multi-step model, the classifier input encoder is separate from
                //  the other encoders and always disabled from going into the bottom of
                // the network.
                //if (sensor.GetDisabledEncoder() != null)
                //{
                //    encoderList = sensor.GetDisabledEncoder().GetEncoders();
                //}
                //else
                //{
                encoderList = new List<EncoderTuple>();
                //}
                if (encoderList.Count >= 1)
                {
                    //    fieldNames = sensor.getSelf().disabledEncoder.getScalarNames();
                    //    this._classifierInputEncoder = encoderList[fieldNames.index(
                    //                                                    predictedFieldName)];
                }
                else
                {
                    // Legacy multi-step networks don't have a separate encoder for the
                    //  classifier, so use the one that goes into the bottom of the network
                    //encoderList = sensor.getSelf().encoder.getEncoderList();
                    encoderList = sensor.GetEncoder().GetEncoders(sensor.GetEncoder());
                    this._classifierInputEncoder = encoderList[_predictedFieldIdx.GetValueOrDefault()].GetEncoder();
                    //throw new NotImplementedException("check line above");
                }
            }



            // Get the actual value and the bucket index for this sample. The
            // predicted field may not be enabled for input to the network, so we
            // explicitly encode it outside of the sensor
            // TODO: All this logic could be simpler if in the encoder itself
            if (!rawInput.ContainsKey(predictedFieldName))
            {
                throw new InvalidOperationException("Input row does not contain a value for the predicted field configured for this model. Missing value for " + predictedFieldName);
            }
            double absoluteValue = TypeConverter.Convert<double>(rawInput[predictedFieldName]);
            int bucketIdx = this._classifierInputEncoder.GetBucketIndices(absoluteValue)[0];

            double actualValue;
            // Convert the absolute values to deltas if necessary
            // The bucket index should be handled correctly by the underlying delta encoder
            if ((this._classifierInputEncoder is DeltaEncoder))
            {
                // Make the delta before any values have been seen 0 so that we do not mess up the
                // range for the adaptive scalar encoder.
                if (!this._ms_prevVal.HasValue)
                {
                    this._ms_prevVal = absoluteValue;
                }
                var prevValue = this._ms_prevVal.GetValueOrDefault();
                this._ms_prevVal = absoluteValue;
                actualValue = absoluteValue - prevValue;
            }
            else
            {
                actualValue = absoluteValue;
            }

            if (double.IsNaN(actualValue))
            {
                actualValue = double.NaN;
            }


            // Pass this information to the classifier's custom compute method
            // so that it can assign the current classification to possibly
            // multiple patterns from the past and current, and also provide
            // the expected classification for some time step(s) in the future.
            //_netInfo.GetLayer().AlterParameter(Parameters.KEY.LEARN, needLearning);
            //        classifier.setParameter("inferenceMode", true);
            //        classifier.setParameter("learningMode", needLearning);
            var classificationIn = new Map<string, object> { { "buckedIdx", bucketIdx }, { "actValue", actualValue } };
            //        classificationIn = {
            //            'bucketIdx': bucketIdx,
            //                    'actValue': actualValue};

            // Handle missing records
            int recordNum;
            if (inputTSRecordIdx.HasValue)
            {
                recordNum = inputTSRecordIdx.Value;
            }
            else
            {
                recordNum = this.__numRunCalls;
            }
            // The parameters should be applied there automaticly through the parameters in the network
            IClassifier classifierImpl = _getClassifierRegion();
            //Debug.WriteLine($"-> Current alpha in classifier: {classifierImpl.Alpha}");

            Classification<double> clResults = classifierImpl.Compute<double>(recordNum: recordNum, patternNonZero: patternNZ.ToArray(),
                classification: classificationIn, learn: needLearning, infer: true);

            //        clResults = classifier.getSelf().customCompute(recordNum = recordNum,
            //                                               patternNZ = patternNZ,
            //                                               classification = classificationIn);

            // ---------------------------------------------------------------
            // Get the prediction for every step ahead learned by the classifier
            int[] predictionSteps = classifierImpl.Steps;//.getParameter('steps');
            //predictionSteps = [int(x) for x in predictionSteps.split(',')];

            // We will return the results in this dict. The top level keys
            // are the step number, the values are the relative likelihoods for
            // each classification value in that time step, represented as
            // another dict where the keys are the classification values and
            // the values are the relative likelihoods.
            inferences[InferenceElement.MultiStepPredictions] = new Map<int, Map<object, double?>>();
            inferences[InferenceElement.MultiStepBestPredictions] = new Map<int, double?>();
            inferences[InferenceElement.MultiStepBucketLikelihoods] = new Map<int, Map<int, double?>>();


            // ======================================================================
            // Plug in the predictions for each requested time step.
            foreach (int steps in predictionSteps)
            {
                // From the clResults, compute the predicted actual value. The
                // CLAClassifier classifies the bucket index and returns a list of
                // relative likelihoods for each bucket. Let's find the max one
                // and then look up the actual value from that bucket index
                double[] likelihoodsVec = clResults.GetStats(steps);//.[steps];
                double[] bucketValues = clResults.GetActualValues(); //clResults['actualValues'];

                // Create a dict of value:likelihood pairs. We can't simply use
                //  dict(zip(bucketValues, likelihoodsVec)) because there might be
                //  duplicate bucketValues (this happens early on in the model when
                //  it doesn't have actual values for each bucket so it returns
                //  multiple buckets with the same default actual value).
                var likelihoodsDict = new Map<object, double?>();
                object bestActValue = null;
                double? bestProb = null;
                foreach (var zipped in ArrayUtils.Zip(bucketValues, likelihoodsVec))
                {
                    // (actValue, prob)
                    var actValue = zipped.Item1;
                    var prob = (double)zipped.Item2;
                    if (likelihoodsDict.ContainsKey(actValue))
                    {
                        likelihoodsDict[actValue] += prob;
                    }
                    else
                    {
                        likelihoodsDict[actValue] = prob;
                    }
                    // Keep track of best
                    if (bestProb == null || likelihoodsDict[actValue] > bestProb)
                    {
                        bestProb = likelihoodsDict[actValue];
                        bestActValue = actValue;
                    }
                }

                // Remove entries with 0 likelihood or likelihood less than
                // minLikelihoodThreshold, but don't leave an empty dict.
                likelihoodsDict = (Map<object, double?>)CLAModel._removeUnlikelyPredictions(likelihoodsDict, minLikelihoodThreshold, maxPredictionsPerStep);

                // calculate likelihood for each bucket
                var bucketLikelihood = new Map<int, double?>();
                foreach (var k in likelihoodsDict.Keys)
                {
                    bucketLikelihood[this._classifierInputEncoder.GetBucketIndices((double)k)[0]] = (likelihoodsDict[k]);
                }

                // ---------------------------------------------------------------------
                // If we have a delta encoder, we have to shift our predicted output value
                //  by the sum of the deltas
                if (this._classifierInputEncoder is DeltaEncoder)
                {
                    // Get the prediction history for this number of timesteps.
                    // The prediction history is a store of the previous best predicted values.
                    // This is used to get the final shift from the current absolute value.
                    if (this._ms_predHistories == null)
                    {
                        this._ms_predHistories = new Map<int, Deque<object>>();
                    }
                    var predHistories = this._ms_predHistories;
                    if (!predHistories.ContainsKey(steps))
                    {
                        predHistories[steps] = new Deque<object>(-1);
                    }
                    var predHistory = predHistories[steps];

                    // Find the sum of the deltas for the steps and use this to generate
                    // an offset from the current absolute value
                    double sumDelta = predHistory.GetBackingList().Sum(s => (double)s); //sum(predHistory);
                    var offsetDict = new Map<object, double?>();
                    //for (k, v) in likelihoodsDict.iteritems()
                    foreach (var kv in likelihoodsDict)
                    {
                        var k = kv.Key;
                        var v = kv.Value;
                        if (k != null)
                        {
                            // Reconstruct the absolute value based on the current actual value,
                            // the best predicted values from the previous iterations,
                            // and the current predicted delta
                            offsetDict[absoluteValue + (double)k + sumDelta] = v;
                        }
                    }

                    // calculate likelihood for each bucket
                    var bucketLikelihoodOffset = new Map<int, double?>();
                    foreach (var k in offsetDict.Keys)
                    {
                        bucketLikelihoodOffset[this._classifierInputEncoder.GetBucketIndices((double)k)[0]] = (
                                                                                          offsetDict[k]);
                    }


                    // Push the current best delta to the history buffer for reconstructing the final delta
                    if (bestActValue != null)
                    {
                        predHistory.Append(bestActValue);
                    }
                    // If we don't need any more values in the predictionHistory, pop off
                    // the earliest one.
                    if (predHistory.Size() >= steps)
                    {
                        predHistory.TakeFirst();
                    }

                    // Provide the offsetDict as the return value
                    if (offsetDict.Count > 0)
                    {
                        ((Map<int, Map<object, double?>>)inferences[InferenceElement.MultiStepPredictions])[steps] = offsetDict;
                        ((Map<int, Map<int, double?>>)inferences[InferenceElement.MultiStepBucketLikelihoods])[steps] = bucketLikelihoodOffset;
                    }
                    else
                    {
                        ((Map<int, Map<object, double?>>)inferences[InferenceElement.MultiStepPredictions])[steps] = likelihoodsDict;
                        ((Map<int, Map<int, double?>>)inferences[InferenceElement.MultiStepBucketLikelihoods])[steps] = bucketLikelihood;
                    }

                    if (bestActValue != null)
                    {
                        ((Map<int, Map<object, double>>)inferences[InferenceElement.MultiStepPredictions])[steps] = null;
                    }
                    else
                    {
                        ((Map<int, double?>)inferences[InferenceElement.MultiStepBestPredictions])[steps] = (absoluteValue + sumDelta + (double)bestActValue);
                    }
                }

                // ---------------------------------------------------------------------
                // Normal case, no delta encoder. Just plug in all our multi-step predictions
                //  with likelihoods as well as our best prediction
                else
                {
                    // The multiStepPredictions element holds the probabilities for each
                    //  bucket
                    ((Map<int, Map<object, double?>>)inferences[InferenceElement.MultiStepPredictions])[steps] = likelihoodsDict;
                    ((Map<int, double?>)inferences[InferenceElement.MultiStepBestPredictions])[steps] = (double)(bestActValue ?? 0);
                    ((Map<int, Map<int, double?>>)inferences[InferenceElement.MultiStepBucketLikelihoods])[steps] = bucketLikelihood;
                }
            }

            return inferences;
            //return null;
        }

        //private Map<InferenceElement, object> _reconstructionCompute()
        //{
        //    if (!this.isInferenceEnabled())
        //    {
        //        return new Map<InferenceElement, object>();
        //    }

        //    var sp = this._getSPRegion();
        //    var sensor = this._getSensorRegion();

        //    // --------------------------------------------------
        //    // SP Top-down flow
        //    sp.setParameter("topDownMode", true);
        //    sp.prepareInputs();
        //    sp.Compute();

        //    // --------------------------------------------------
        //    // Sensor Top-down flow
        //    sensor.setParameter("topDownMode", true);
        //    sensor.prepareInputs();
        //    sensor.compute();

        //    // Need to call getOutputValues() instead of going through getOutputData()
        //    // because the return values may contain strings, which cannot be passed
        //    // through the Region.cpp code.

        //    // predictionRow is a list of values, one for each field. The value is
        //    //  in the same type as the original input to the encoder and may be a
        //    //  string for category fields for example.
        //    predictionRow = copy.copy(sensor.getOutputValues("temporalTopDownOut"));
        //    predictionFieldEncodings = sensor.getOutputValues("temporalTopDownEncodings");

        //    Map<InferenceElement, object> inferences = new Map<InferenceElement, object>();
        //    inferences[InferenceElement.prediction] = tuple(predictionRow);
        //    inferences[InferenceElement.encodings] = tuple(predictionFieldEncodings);

        //    return inferences;
        //}

        //private Map<InferenceElement, object> _classificationCompute()
        //{
        //    Map<InferenceElement, object> inference = new Map<InferenceElement, object>();
        //    var classifier = this._getClassifierRegion();
        //    classifier.setParameter("inferenceMode", true);
        //    classifier.setParameter("learningMode", this.isLearningEnabled());
        //    classifier.prepareInputs();
        //    classifier.compute();

        //    // What we get out is the score for each category. The argmax is
        //    // then the index of the winning category
        //    classificationDist = classifier.getOutputData('categoriesOut');
        //    classification = classificationDist.argmax();
        //    probabilities = classifier.getOutputData('categoryProbabilitiesOut');
        //    numCategories = classifier.getParameter('activeOutputCount');
        //    classConfidences = dict(zip(xrange(numCategories), probabilities));

        //    inference[InferenceElement.classification] = classification;
        //    inference[InferenceElement.classConfidences] = { 0: classConfidences};

        //    return inference;
        //}

        /// <summary>
        /// 
        /// </summary>
        /// <param name="inputRecord">
        /// dict containing the input to the sensor 
        /// </param>
        /// <returns>Return a 'ClassifierInput' object, which contains the mapped bucket index for input Record</returns>
        private object _getClassifierInputRecord(Map<string, object> inputRecord)
        {
            double? absoluteValue = null;
            int? bucketIdx = null;

            if (this._predictedFieldName != null && this._classifierInputEncoder != null)
            {
                absoluteValue = TypeConverter.Convert<double?>(inputRecord[this._predictedFieldName]);
                bucketIdx = _classifierInputEncoder.GetBucketIndices(absoluteValue.GetValueOrDefault())[0];
            }

            return new ClassifierInput(dataRow: absoluteValue,
                                   bucketIndex: bucketIdx);
        }

        ///// <summary>
        ///// Create a CLA network and return it. (using CLA Model description dictionary)
        ///// </summary>
        ///// <param name="sensorParams"></param>
        ///// <param name="spEnable"></param>
        ///// <param name="spParams"></param>
        ///// <param name="tpEnable"></param>
        ///// <param name="tpParams"></param>
        ///// <param name="clEnable"></param>
        ///// <param name="clParams"></param>
        ///// <param name="anomalyParams"></param>
        ///// <returns>NetworkInfo instance</returns>
        //internal NetworkInfo CreateClaNetwork(Parameters parameters)
        //{
        //    //Parameters p = _modelConfig.GetParameters();
        //    // --------------------------------------------------
        //    // Create the network
        //    var n = new Network.Network("CLANetwork", parameters);

        //    // --------------------------------------------------
        //    // Add the Sensor
        //    var topRegion = new Region("Top", n);
        //    n.Add(topRegion);
        //    //n.addRegion("sensor", "py.RecordSensor", json.dumps(dict(verbosity = sensorParams['verbosity'])));
        //    //sensor = n.regions['sensor'].getSelf();

        //    var fieldNames = _modelConfig.inputRecordSchema.Select(v => v.name).ToList();
        //    var dataTypes = _modelConfig.inputRecordSchema.Select(v => v.type).ToList();
        //    var sensorFlags = _modelConfig.inputRecordSchema.Select(v => v.special).ToList();
        //    var pubBuilder = Publisher.GetBuilder()
        //        .AddHeader(string.Join(", ", fieldNames))
        //        //.AddHeader("address, consumption, gym, timestamp")
        //        //.AddHeader("string, float, string, datetime")
        //        .AddHeader(string.Join(", ", dataTypes))
        //        .AddHeader(string.Join(", ", sensorFlags))
        //        .Build();
        //    _inputProvider = pubBuilder;

        //    //string dataFilePath = (string)((Map<string, object>)_description.control.dataset["streams"])["source"];
        //    SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, "name", pubBuilder);
        //    IHTMSensor sensor = (IHTMSensor)Sensor<ObservableSensor<string[]>>.Create(ObservableSensor<string[]>.Create, parms);

        //    ILayer sensorLayer = Network.Network.CreateLayer("sensor", parameters);
        //    //sensorLayer.Add(sensor);

        //    //enabledEncoders = copy.deepcopy(sensorParams['encoders']);
        //    EncoderSettingsList enabledEncoders = new EncoderSettingsList(_modelConfig.GetEncoderSettings());
        //    List<string> enabledEncodersToRemove = new List<string>();

        //    foreach (var pair in enabledEncoders)
        //    {
        //        string name = pair.Key;
        //        var @params = pair.Value;

        //        if (@params != null)
        //        {
        //            bool classifierOnly = @params.classifierOnly.GetValueOrDefault(false);
        //            @params.classifierOnly = null;
        //            if (classifierOnly)
        //            {
        //                enabledEncodersToRemove.Add(name);
        //                //enabledEncoders.Remove(name);
        //            }
        //        }
        //    }
        //    enabledEncoders = new EncoderSettingsList(enabledEncoders.Where(pr => !enabledEncodersToRemove.Contains(pr.Key)).ToDictionary(k => k.Key, v => v.Value));

        //    // Disabled encoders are encoders that are fed to CLAClassifierRegion but not
        //    // SP or TP Regions. This is to handle the case where the predicted field
        //    // is not fed through the SP/TP. We typically just have one of these now.
        //    EncoderSettingsList disabledEncoders = new EncoderSettingsList(_modelConfig.GetEncoderSettings());
        //    //disabledEncoders = copy.deepcopy(sensorParams['encoders']);
        //    List<string> disabledEncodersToRemove = new List<string>();
        //    foreach (var pair in disabledEncoders)
        //    {
        //        string name = pair.Key;
        //        var @params = pair.Value;

        //        if (@params == null)
        //        {
        //            disabledEncodersToRemove.Add(name);
        //        }
        //        else
        //        {
        //            bool classifierOnly = @params.classifierOnly.GetValueOrDefault(false);
        //            @params.classifierOnly = null;
        //            if (!classifierOnly)
        //            {
        //                disabledEncodersToRemove.Add(name);
        //            }
        //        }
        //    }
        //    disabledEncoders = new EncoderSettingsList(disabledEncoders.Where(pr => !disabledEncodersToRemove.Contains(pr.Key)).ToDictionary(k => k.Key, v => v.Value));

        //    MultiEncoder encoder = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build(); // enabledEncoders
        //    MultiEncoderAssembler.Assemble(encoder, enabledEncoders);
        //    sensorLayer.Add(encoder);
        //    encoder.SetScalarNames(fieldNames);

        //    //sensor.SetEncoder(encoder);
        //    //sensor.InitEncoder(parameters);
        //    topRegion.Add(sensorLayer);
        //    //sensor.encoder = encoder;
        //    //MultiEncoder disabledEncoder = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build(); // disabledEncoders
        //    //sensor.disabledEncoder = MultiEncoder(disabledEncoders);

        //    //sensor.dataSource = DataBuffer();

        //    string prevRegion = "sensor";
        //    int prevRegionWidth = encoder.GetWidth();

        //    bool spEnable = _modelConfig.EnableSpatialPooler;
        //    bool tpEnable = _modelConfig.EnableTemporalMemory;
        //    bool clEnable = _modelConfig.EnableClassification;

        //    //var spParams = _modelConfig.modelParams.spParams;
        //    //var tpParams = _modelConfig.modelParams.tpParams;

        //    // SP is not enabled for spatial classification network
        //    if (spEnable)
        //    {
        //        //spParams = spParams.copy();
        //        spParams.inputWidth = new[] { prevRegionWidth };
        //        this.__logger.Debug("Adding SPRegion; spParams: " + spParams);
        //        SpatialPooler spatialPooler = new SpatialPooler();

        //        ILayer spLayer = Network.Network.CreateLayer("SP", parameters);
        //        topRegion.Add(spLayer.Add(spatialPooler));

        //        //n.addRegion("SP", "py.SPRegion", json.dumps(spParams));

        //        // Link SP region
        //        topRegion.Connect("SP", "sensor");
        //        //n.link("sensor", "SP", "UniformLink", "");
        //        //n.link("sensor", "SP", "UniformLink", "", srcOutput = "resetOut", destInput = "resetIn");

        //        //n.link("SP", "sensor", "UniformLink", "", srcOutput = "spatialTopDownOut", destInput = "spatialTopDownIn");
        //        //n.link("SP", "sensor", "UniformLink", "", srcOutput = "temporalTopDownOut", destInput = "temporalTopDownIn");

        //        prevRegion = "SP";
        //        prevRegionWidth = spParams.columnCount[0];
        //    }

        //    if (tpEnable)
        //    {
        //        //tpParams = tpParams.copy();
        //        if (prevRegion == "sensor")
        //        {
        //            tpParams.inputWidth[0] = tpParams.columnCount[0] = prevRegionWidth;
        //        }
        //        else
        //        {
        //            Debug.Assert(tpParams.columnCount[0] == prevRegionWidth);
        //            tpParams.inputWidth = tpParams.columnCount;
        //        }

        //        this.__logger.Debug("Adding TPRegion; tpParams: " + tpParams);
        //        TemporalMemory tpMemory = new TemporalMemory();

        //        ILayer tpLayer = Network.Network.CreateLayer("TP", parameters);
        //        topRegion.Add(tpLayer.Add(tpMemory));

        //        //n.addRegion("TP", "py.TPRegion", json.dumps(tpParams));

        //        // Link TP region
        //        topRegion.Connect("TP", prevRegion);
        //        //n.link(prevRegion, "TP", "UniformLink", "");
        //        if (prevRegion != "sensor")
        //        {
        //            //n.Connect(prevRegion, "TP");
        //            //n.link("TP", prevRegion, "UniformLink", "", srcOutput = "topDownOut",destInput = "topDownIn");
        //        }
        //        else
        //        {
        //            //n.Connect(prevRegion, "TP");
        //            // n.link("TP", prevRegion, "UniformLink", "", srcOutput = "topDownOut",destInput = "temporalTopDownIn");
        //        }
        //        //n.link("sensor", "TP", "UniformLink", "", srcOutput = "resetOut",destInput = "resetIn");

        //        prevRegion = "TP";
        //        prevRegionWidth = tpParams.inputWidth[0];
        //    }

        //    var clParams = _modelConfig.modelParams.clParams;
        //    if (clEnable && clParams != null)
        //    {
        //        //clParams = clParams.copy();
        //        string clRegionName = clParams.regionName;
        //        this.__logger.Debug(string.Format("Adding {0}; clParams: {1}", clRegionName, clParams));

        //        //CLAClassifier claClassifier = new CLAClassifier();

        //        ILayer classifierLayer = Network.Network.CreateLayer("Classifier", parameters);

        //        topRegion.Add(classifierLayer.AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true));

        //        //n.addRegion("Classifier", "py.%s" + str(clRegionName), json.dumps(clParams));

        //        //n.link("sensor", "Classifier", "UniformLink", "", srcOutput = "categoryOut", destInput = "categoryIn");

        //        //n.link(prevRegion, "Classifier", "UniformLink", "");
        //    }

        //    if (this.getInferenceType() == InferenceType.TemporalAnomaly)
        //    {
        //        //topLayer.Add(Anomaly.Create(parameters));
        //        topRegion.Lookup("TP").Add(Anomaly.Create(parameters));
        //        //anomalyClParams = dict(
        //        //    trainRecords = anomalyParams.get('autoDetectWaitRecords', None),
        //        //    cacheSize = anomalyParams.get('anomalyCacheRecords', None)
        //        //);
        //        //this._addAnomalyClassifierRegion(n, anomalyClParams, spEnable, tpEnable);
        //    }

        //    // --------------------------------------------------
        //    // NuPIC doesn't initialize the network until you try to run it
        //    // but users may want to access components in a setup callback
        //    //n.initialize();
        //    n.GetHead().Close();

        //    //n.GetHead().GetTail().Observe().Subscribe(output =>
        //    //{
        //    //    _currentInferenceOutput = output;
        //    //});

        //    return new NetworkInfo(n, null);
        //}

        /// <summary>
        /// Create a CLA network and return it. (using CLA Model description dictionary)
        /// </summary>
        /// <returns>NetworkInfo instance</returns>
        internal NetworkInfo CreateClaNetworkSingleRegionAndLayer(Parameters parameters)
        {
            // --------------------------------------------------
            // Create the network
            var n = new Network.Network("CLANetwork", parameters);

            // --------------------------------------------------
            // Create the Region where we are going to host the layer in.
            var region = Network.Network.CreateRegion("Top");
            n.Add(region);

            // --------------------------------------------------
            // Create the Layer where we are going to host the algorithms in.
            var layer = Network.Network.CreateLayer("Layer 2/3", parameters);
            region.Add(layer);

            // --------------------------------------------------
            // Build sensor
            var fieldNames = _modelConfig.Control.InputRecordSchema.Select(v => v.name).ToList();
            var dataTypes = _modelConfig.Control.InputRecordSchema.Select(v => v.type).ToList();
            var sensorFlags = _modelConfig.Control.InputRecordSchema.Select(v => v.special).ToList();
            var pubBuilder = Publisher.GetBuilder()
                .AddHeader(string.Join(", ", fieldNames))
                //.AddHeader("address, consumption, gym, timestamp")
                //.AddHeader("string, float, string, datetime")
                .AddHeader(string.Join(", ", dataTypes))
                .AddHeader(string.Join(", ", sensorFlags))
                .Build();
            _inputProvider = pubBuilder;

            SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, "name", pubBuilder);
            IHTMSensor sensor = (IHTMSensor)Sensor<ObservableSensor<string[]>>.Create(ObservableSensor<string[]>.Create, parms);

            // --------------------------------------------------
            // Define encoders for sensor
            EncoderSettingsList enabledEncoders = new EncoderSettingsList(_modelConfig.GetEncoderSettings());
            List<string> enabledEncodersToRemove = new List<string>();

            foreach (var pair in enabledEncoders)
            {
                string name = pair.Key;
                var @params = pair.Value;

                if (@params != null)
                {
                    bool classifierOnly = @params.classifierOnly.GetValueOrDefault(false);
                    @params.classifierOnly = null;
                    if (classifierOnly)
                    {
                        enabledEncodersToRemove.Add(name);
                        //enabledEncoders.Remove(name);
                    }
                }
            }
            enabledEncoders = new EncoderSettingsList(enabledEncoders.Where(pr => !enabledEncodersToRemove.Contains(pr.Key)).ToDictionary(k => k.Key, v => v.Value));

            // Disabled encoders are encoders that are fed to CLAClassifierRegion but not
            // SP or TP Regions. This is to handle the case where the predicted field
            // is not fed through the SP/TP. We typically just have one of these now.
            EncoderSettingsList disabledEncoders = new EncoderSettingsList(_modelConfig.GetEncoderSettings());
            //disabledEncoders = copy.deepcopy(sensorParams['encoders']);
            List<string> disabledEncodersToRemove = new List<string>();
            foreach (var pair in disabledEncoders)
            {
                string name = pair.Key;
                var @params = pair.Value;

                if (@params == null)
                {
                    disabledEncodersToRemove.Add(name);
                }
                else
                {
                    bool classifierOnly = @params.classifierOnly.GetValueOrDefault(false);
                    @params.classifierOnly = null;
                    if (!classifierOnly)
                    {
                        disabledEncodersToRemove.Add(name);
                    }
                }
            }
            disabledEncoders = new EncoderSettingsList(disabledEncoders.Where(pr => !disabledEncodersToRemove.Contains(pr.Key)).ToDictionary(k => k.Key, v => v.Value));

            MultiEncoder encoder = (MultiEncoder)MultiEncoder.GetBuilder().Name("").Build(); // enabledEncoders
            MultiEncoderAssembler.Assemble(encoder, enabledEncoders);
            encoder.SetScalarNames(fieldNames);

            sensor.InitEncoder(parameters);
            sensor.SetEncoder(encoder);


            layer.Add(sensor);

            int prevRegionWidth = encoder.GetWidth();

            bool spEnable = _modelConfig.EnableSpatialPooler;
            bool tpEnable = _modelConfig.EnableTemporalMemory;
            bool clEnable = _modelConfig.EnableClassification;
            //var spParams = _modelConfig.modelParams.spParams;
            //var tpParams = _modelConfig.modelParams.tpParams;

            // SP is not enabled for spatial classification network
            if (spEnable)
            {
                //spParams.inputWidth = new[] { prevRegionWidth };
                parameters.SetInputDimensions(new[] { prevRegionWidth });

                this.__logger.Debug("Adding SPRegion");
                SpatialPooler spatialPooler = new SpatialPooler();

                // spParams get applies when the network closes because they are present in the parameters instance.
                layer.Add(spatialPooler);

                prevRegionWidth = ((int[])parameters.GetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS))[0];
            }

            if (tpEnable)
            {
                //tpParams = tpParams.copy();
                if (!spEnable)
                {
                    //tpParams.inputWidth[0] = tpParams.columnCount[0] = prevRegionWidth;
                    parameters.SetInputDimensions(new [] {prevRegionWidth});
                    parameters.SetColumnDimensions(new [] {prevRegionWidth});
                }
                else
                {
                    Debug.Assert(((int[])parameters.GetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS))[0] == prevRegionWidth);
                    //tpParams.inputWidth = tpParams.columnCount;
                    parameters.SetInputDimensions((int[])parameters.GetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS));
                    
                    //parameters.SetInputDimensions(tpParams.inputWidth);
                }

                __logger.Debug("Adding TPRegion;");
                TemporalMemory tpMemory = new TemporalMemory();

                layer.Add(tpMemory);

                prevRegionWidth = ((int[])parameters.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS))[0];
            }

            //var clParams = _modelConfig.modelParams.clParams;
            if (clEnable && (bool)parameters.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, false))
            {
                //clParams = clParams.copy();
                //string clRegionName = clParams.regionName;
                this.__logger.Debug(string.Format("Adding Classifier '{0}'", parameters.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY_TYPE)));

                //layer.AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true);
                //layer.AlterParameter(Parameters.KEY.AUTO_CLASSIFY_TYPE, Type.GetType(clRegionName, true));
            }

            if (this.getInferenceType() == InferenceType.TemporalAnomaly)
            {
                _anomalyInst = Anomaly.Create(parameters);
                layer.Add(_anomalyInst);
            }

            // --------------------------------------------------
            // NuPIC doesn't initialize the network until you try to run it
            // but users may want to access components in a setup callback
            //n.initialize();
            n.GetHead().Close();

            return new NetworkInfo(n, null);
        }

        /// <summary>
        /// Remove entries with 0 likelihood or likelihood less than
        /// minLikelihoodThreshold, but don't leave an empty dict.
        /// </summary>
        /// <returns></returns>
        public static IDictionary<object, double?> _removeUnlikelyPredictions(IDictionary<object, double?> likelihoodsDict,
            double minLikelihoodThreshold, int maxPredictionsPerStep)
        {
            var maxVal = new Util.Tuple(null, null);
            List<object> keysToRemove = new List<object>();
            foreach (var kvp in likelihoodsDict)
            {
                var k = kvp.Key;
                var v = (double)kvp.Value;

                if (keysToRemove.Contains(k)) continue;

                if (likelihoodsDict.Count <= 1) break;
                if (maxVal.Get(0) == null || (maxVal.Get(1) is double && v >= (double)maxVal.Get(1)))
                {
                    if (maxVal.Get(0) != null && maxVal.Get(1) is double && (double)maxVal.Get(1) < minLikelihoodThreshold)
                    {
                        keysToRemove.Add(maxVal.Get(0));
                    }
                    maxVal = new Util.Tuple(k, v);
                }
                else if (v < minLikelihoodThreshold)
                {
                    keysToRemove.Add(k);
                }
            }

            foreach (var key in keysToRemove)
            {
                likelihoodsDict.Remove(key);
            }
            // Limit the number of predictions to include.
            var retLikelihoodsDict = new Map<object, double?>();
            foreach (var item in likelihoodsDict.OrderByDescending(kvp => kvp.Key.ToString()).Take(maxPredictionsPerStep).Reverse())
            {
                retLikelihoodsDict.Add(item.Key, item.Value);
            }
            return retLikelihoodsDict;
        }

        public override void finishLearning()
        {
            throw new System.NotImplementedException();
        }

        public override void resetSequenceStates()
        {
            if (_hasTP)
            {
                _getTPRegion().Reset(_netInfo.GetLayer().GetConnections());
                __logger.Debug("CLAModel.resetSequenceStates(): reset temporal pooler's sequence states.");
            }
        }

        /// <summary>
        /// Returns the sequence of FieldMetaInfo objects specifying this
        /// Model's output; note that this may be different than the list of
        /// FieldMetaInfo objects supplied at initialization(e.g., due to the
        /// transcoding of some input fields into meta-fields, such as datetime 
        /// -> dayOfWeek, timeOfDay, etc.)
        ///  </summary>
        /// <param name="includeClassifierOnlyField"></param>
        /// <returns>List of FieldMetaInfo objects</returns>
        public override List<FieldMetaInfo> getFieldInfo(bool includeClassifierOnlyField = false)
        {
            //var fieldNames = _modelConfig.inputRecordSchema.Select(v => v.name).ToList();// _modelConfig.inputRecordSchema.Select(m=>m.name).ToList();
            var sensorFlags = _modelConfig.Control.InputRecordSchema.OrderBy(v => v.name).Select(v => v.special).ToList();

            MultiEncoder encoder = _getEncoder();

            var fieldNames = encoder.GetScalarNames();
            //var fieldTypes = encoder.GetDecoderOutputFieldTypes();
            var fieldTypes = _modelConfig.Control.InputRecordSchema.OrderBy(v => v.name).Select(v => v.type).ToList();
            Debug.Assert(fieldNames.Count == fieldTypes.Count);

            // Also include the classifierOnly field?
            MultiEncoder clEncoder = (MultiEncoder)_getClassifierOnlyEncoder();
            if (includeClassifierOnlyField && clEncoder != null)
            {
                var addFieldNames = clEncoder.GetScalarNames();
                var addFieldTypes = clEncoder.GetDecoderOutputFieldTypes();
                Debug.Assert(addFieldNames.Count == addFieldTypes.Count);
                fieldNames.AddRange(addFieldNames);
                fieldTypes.AddRange(addFieldTypes);
            }

            var fieldMetaList = ArrayUtils.Zip(fieldNames, fieldTypes, sensorFlags)
                .Select(t => new FieldMetaInfo((string)t.Get(0), (FieldMetaType)t.Get(1), (SensorFlags)t.Get(2)))
                .ToList();

            return fieldMetaList;
        }

        public override void setFieldStatistics(Map<string, Map<string, object>> fieldStats)
        {
            MultiEncoder encoder = _getEncoder();
            // Set the stats for the encoders. The first argument to setFieldStats
            // is the field name of the encoder. Since we are using a multiencoder
            // we leave it blank, the multiencoder will propagate the field names to the
            // underlying encoders
            encoder.SetFieldStats("", fieldStats);
        }

        public override void getRuntimeStats()
        {
            throw new System.NotImplementedException();
        }

        protected override ILog _getLogger()
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// sensor region's encoder for the given network
        /// </summary>
        /// <returns></returns>
        private MultiEncoder _getEncoder()
        {
            return _getSensorRegion().GetEncoder();
        }
        /// <summary>
        /// sensor region's encoder that is sent only to the classifier,
        /// not to the bottom of the network
        /// </summary>
        /// <returns></returns>
        private IEncoder _getClassifierOnlyEncoder()
        {
            //_getSensorRegion().disabledEncoder;
            return null;
        }
    }

    internal class ClassifierInput
    {
        private readonly double? _dataRow;
        private readonly int? _bucketIndex;

        public ClassifierInput(double? dataRow, int? bucketIndex)
        {
            _dataRow = dataRow;
            _bucketIndex = bucketIndex;
        }
    }

    /*

""" @file clamodel.py
Encapsulation of CLAnetwork that implements the ModelBase.
""";

import copy;
import math;
import os;
import json;
import itertools;
import logging;
import traceback;
from collections import deque;
from operator import itemgetter;

import numpy;

from nupic.frameworks.opf.model import Model;
from nupic.algorithms.anomaly import Anomaly;
from nupic.data import SENTINEL_VALUE_FOR_MISSING_DATA;
from nupic.data.fieldmeta import FieldMetaSpecial, FieldMetaInfo;
from nupic.encoders import MultiEncoder, DeltaEncoder;
from nupic.engine import Network;
from nupic.support.fshelpers import makeDirectoryFromAbsolutePath;
from nupic.frameworks.opf.opfutils import (InferenceType,
                      InferenceElement,
                      SensorInput,
                      ClassifierInput,
                      initLogger);

try
{
  import capnp;
}
catch( ImportError)
{
  capnp = None;
}
if( capnp)
{
  from nupic.frameworks.opf.CLAModelProto_capnp import CLAModelProto;
}


DEFAULT_LIKELIHOOD_THRESHOLD = 0.0001;
DEFAULT_MAX_PREDICTIONS_PER_STEP = 8;

DEFAULT_ANOMALY_TRAINRECORDS = 4000;
DEFAULT_ANOMALY_THRESHOLD = 1.1;
DEFAULT_ANOMALY_CACHESIZE = 10000;


def requireAnomalyModel(func)
{
  """
  Decorator for functions that require anomaly models.
  """;
  def _decorator(self, *args, **kwargs)
  {
    if( not this.getInferenceType() == InferenceType.TemporalAnomaly)
    {
      raise RuntimeError("Method required a TemporalAnomaly model.");
    }
    if( this._getAnomalyClassifier() is None)
    {
      raise RuntimeError("Model does not support this command. Model must"
          "be an active anomalyDetector model.");
    }
    return func(self, *args, **kwargs);
  }
  return _decorator;
}

class CLAModel(Model)
{

  __supportedInferenceKindSet = set((InferenceType.TemporalNextStep,
                                     InferenceType.TemporalClassification,
                                     InferenceType.NontemporalClassification,
                                     InferenceType.NontemporalAnomaly,
                                     InferenceType.TemporalAnomaly,
                                     InferenceType.TemporalMultiStep,
                                     InferenceType.NontemporalMultiStep));

  __myClassName = "CLAModel";


  def __init__(self,
      sensorParams={},
      inferenceType=InferenceType.TemporalNextStep,
      predictedField=None,
      spEnable=True,
      spParams={},

      // TODO: We can't figure out what this is. Remove?
      trainSPNetOnlyIfRequested=False,
      tpEnable=True,
      tpParams={},
      clEnable=True,
      clParams={},
      anomalyParams={},
      minLikelihoodThreshold=DEFAULT_LIKELIHOOD_THRESHOLD,
      maxPredictionsPerStep=DEFAULT_MAX_PREDICTIONS_PER_STEP,
      network=None)
  {
    """CLAModel constructor.
    Args:
      inferenceType: A value from the InferenceType enum class.
      predictedField: The field to predict for multistep prediction.
      sensorParams: A dictionary specifying the sensor parameters.
      spEnable: Whether or not to use a spatial pooler.
      spParams: A dictionary specifying the spatial pooler parameters. These
          are passed to the spatial pooler.
      trainSPNetOnlyIfRequested: If set, don't create an SP network unless the
          user requests SP metrics.
      tpEnable: Whether to use a temporal pooler.
      tpParams: A dictionary specifying the temporal pooler parameters. These
          are passed to the temporal pooler.
      clEnable: Whether to use the classifier. If false, the classifier will
          not be created and no predictions will be generated.
      clParams: A dictionary specifying the classifier parameters. These are
          are passed to the classifier.
      anomalyParams: Anomaly detection parameters
      minLikelihoodThreshold: The minimum likelihood value to include in
          inferences.  Currently only applies to multistep inferences.
      maxPredictionsPerStep: Maximum number of predictions to include for
          each step in inferences. The predictions with highest likelihood are
          included.
    """;
    if( not inferenceType in this.__supportedInferenceKindSet)
    {
      raise ValueError("{0} received incompatible inference type: {1}"\
                       .format(this.__class__, inferenceType));
    }

    // Call super class constructor
    super(CLAModel, self).__init__(inferenceType);

    // this.__restoringFromState is set to True by our __setstate__ method
    // and back to False at completion of our _deSerializeExtraData() method.
    this.__restoringFromState = False;
    this.__restoringFromV1 = False;

    // Intitialize logging
    this.__logger = initLogger(self);
    this.__logger.debug("Instantiating %s." % this.__myClassName);


    this._minLikelihoodThreshold = minLikelihoodThreshold;
    this._maxPredictionsPerStep = maxPredictionsPerStep;

    // set up learning parameters (note: these may be replaced via
    // enable/disable//SP/TP//Learning methods)
    this.__spLearningEnabled = bool(spEnable);
    this.__tpLearningEnabled = bool(tpEnable);

    // Explicitly exclude the TP if this type of inference doesn't require it
    if( not InferenceType.isTemporal(this.getInferenceType()) \
       or this.getInferenceType() == InferenceType.NontemporalMultiStep)
    {
      tpEnable = False;
    }

    this._netInfo = None;
    this._hasSP = spEnable;
    this._hasTP = tpEnable;
    this._hasCL = clEnable;

    this._classifierInputEncoder = None;
    this._predictedFieldIdx = None;
    this._predictedFieldName = None;
    this._numFields = None;
    // init anomaly
    windowSize = anomalyParams.get("slidingWindowSize", None);
    mode = anomalyParams.get("mode", "pure");
    anomalyThreshold = anomalyParams.get("autoDetectThreshold", None);
    this._anomalyInst = Anomaly(slidingWindowSize=windowSize, mode=mode,
                                binaryAnomalyThreshold=anomalyThreshold);

    // -----------------------------------------------------------------------
    if( network is not None)
    {
      this._netInfo = NetworkInfo(net=network, statsCollectors=[]);
    }
    else
    {
      // Create the network
      this._netInfo = this.__createCLANetwork(
          sensorParams, spEnable, spParams, tpEnable, tpParams, clEnable,
          clParams, anomalyParams);
    }


    // Initialize Spatial Anomaly detection parameters
    if( this.getInferenceType() == InferenceType.NontemporalAnomaly)
    {
      this._getSPRegion().setParameter("anomalyMode", True);
    }

    // Initialize Temporal Anomaly detection parameters
    if( this.getInferenceType() == InferenceType.TemporalAnomaly)
    {
      this._getTPRegion().setParameter("anomalyMode", True);
      this._prevPredictedColumns = numpy.array([]);
    }

    // -----------------------------------------------------------------------
    // This flag, if present tells us not to train the SP network unless
    //  the user specifically asks for the SP inference metric
    this.__trainSPNetOnlyIfRequested = trainSPNetOnlyIfRequested;

    this.__numRunCalls = 0;

    // Tracks whether finishedLearning() has been called
    this.__finishedLearning = False;

    this.__logger.debug("Instantiated %s" % this.__class__.__name__);

    this._input = None;

    return;
  }


  def getParameter(self, paramName)
  {
    if( paramName == '__numRunCalls')
    {
      return this.__numRunCalls;
    }
    else
    {
      raise RuntimeError("'%s' parameter is not exposed by clamodel." % \
        (paramName));
    }
  }


  def resetSequenceStates(self)
  {
    """ [virtual method override] Resets the model's sequence states. Normally
    called to force the delineation of a sequence, such as between OPF tasks.
    """;

    if( this._hasTP)
    {
      // Reset TP's sequence states
      this._getTPRegion().executeCommand(['resetSequenceStates']);

      this.__logger.debug("CLAModel.resetSequenceStates(): reset temporal "
                         "pooler's sequence states");

      return;
    }
  }


  def finishLearning(self)
  {
    """ [virtual method override] Places the model in a permanent "finished
    learning" mode where it will not be able to learn from subsequent input
    records.
    NOTE: Upon completion of this command, learning may not be resumed on
    the given instance of the model (e.g., the implementation may optimize
    itself by pruning data structures that are necessary for learning)
    """;
    assert not this.__finishedLearning;

    if( this._hasSP)
    {
      // Finish SP learning
      this._getSPRegion().executeCommand(['finishLearning']);
      this.__logger.debug(
        "CLAModel.finishLearning(): finished SP learning");
    }

    if( this._hasTP)
    {
      // Finish temporal network's TP learning
      this._getTPRegion().executeCommand(['finishLearning']);
      this.__logger.debug(
        "CLAModel.finishLearning(): finished TP learning");
    }

    this.__spLearningEnabled = this.__tpLearningEnabled = False;
    this.__finishedLearning = True;
    return;
  }


  def setFieldStatistics(self,fieldStats)
  {
    encoder = this._getEncoder();
    // Set the stats for the encoders. The first argument to setFieldStats
    // is the field name of the encoder. Since we are using a multiencoder
    // we leave it blank, the multiencoder will propagate the field names to the
    // underlying encoders
    encoder.setFieldStats('',fieldStats);
  }


  def enableLearning(self)
  {
    """[override] Turn Learning on for the current model """;
    super(CLAModel, self).enableLearning();
    this.setEncoderLearning(True);
  }


  def disableLearning(self)
  {
    """[override] Turn Learning off for the current model """;
    super(CLAModel, self).disableLearning();
    this.setEncoderLearning(False);
  }


  def setEncoderLearning(self,learningEnabled)
  {
    this._getEncoder().setLearning(learningEnabled);
  }


  // Anomaly Accessor Methods
  @requireAnomalyModel;
  def setAnomalyParameter(self, param, value)
  {
    """
    Set a parameter of the anomaly classifier within this model.
    """;
    this._getAnomalyClassifier().setParameter(param, value);
  }


  @requireAnomalyModel;
  def getAnomalyParameter(self, param)
  {
    """
    Get a parameter of the anomaly classifier within this model.
    """;
    return this._getAnomalyClassifier().getParameter(param);
  }


  @requireAnomalyModel;
  def anomalyRemoveLabels(self, start, end, labelFilter)
  {
    """
    Remove labels from the anomaly classifier within this model.
    """;
    this._getAnomalyClassifier().getSelf().removeLabels(start, end, labelFilter);
  }


  @requireAnomalyModel;
  def anomalyAddLabel(self, start, end, labelName)
  {
    """
    Add labels from the anomaly classifier within this model.
    """;
    this._getAnomalyClassifier().getSelf().addLabel(start, end, labelName);
  }


  @requireAnomalyModel;
  def anomalyGetLabels(self, start, end)
  {
    """
    Get labels from the anomaly classifier within this model.
    """;
    return this._getAnomalyClassifier().getSelf().getLabels(start, end);
  }


  def run(self, inputRecord)
  {
    """ run one iteration of this model.
            args:
                inputRecord is a record object formatted according to
                    nupic.data.RecordStream.getNextRecordDict() result format.
            return:
                An ModelResult class (see opfutils.py) The contents of
                ModelResult.inferences depends on the the specific inference
                type of this model, which can be queried by getInferenceType()
    """;
    assert not this.__restoringFromState;
    assert inputRecord;

    results = super(CLAModel, self).run(inputRecord);

    this.__numRunCalls += 1;

    if( this.__logger.isEnabledFor(logging.DEBUG))
    {
      this.__logger.debug("CLAModel.run() inputRecord=%s", (inputRecord));
    }

    results.inferences = {};
    this._input = inputRecord;

    // -------------------------------------------------------------------------
    // Turn learning on or off?
    if( '_learning' in inputRecord)
    {
      if( inputRecord['_learning'])
      {
        this.enableLearning();
      }
      else
      {
        this.disableLearning();
      }
    }


    ###########################################################################
    // Predictions and Learning
    ###########################################################################
    this._sensorCompute(inputRecord);
    this._spCompute();
    this._tpCompute();

    results.sensorInput = this._getSensorInputRecord(inputRecord);

    inferences = {};

    // TODO: Reconstruction and temporal classification not used. Remove
    if( this._isReconstructionModel())
    {
      inferences = this._reconstructionCompute();
    }
    else if( this._isMultiStepModel())
    {
      inferences = this._multiStepCompute(rawInput=inputRecord);
    }
    // For temporal classification. Not used, and might not work anymore
    else if( this._isClassificationModel())
    {
      inferences = this._classificationCompute();
    }

    results.inferences.update(inferences);

    inferences = this._anomalyCompute();
    results.inferences.update(inferences);

    // -----------------------------------------------------------------------
    // Store the index and name of the predictedField
    results.predictedFieldIdx = this._predictedFieldIdx;
    results.predictedFieldName = this._predictedFieldName;
    results.classifierInput = this._getClassifierInputRecord(inputRecord);

    // =========================================================================
    // output
    assert (not this.isInferenceEnabled() or results.inferences is not None), \
            "unexpected inferences: %r" %  results.inferences;


    #this.__logger.setLevel(logging.DEBUG)
    if( this.__logger.isEnabledFor(logging.DEBUG))
    {
      this.__logger.debug("inputRecord: %r, results: %r" % (inputRecord,
                                                            results));
    }

    return results;
  }


  def _getSensorInputRecord(self, inputRecord)
  {
    """
    inputRecord - dict containing the input to the sensor
    Return a 'SensorInput' object, which represents the 'parsed'
    representation of the input record
    """;
    sensor = this._getSensorRegion();
    dataRow = copy.deepcopy(sensor.getSelf().getOutputValues('sourceOut'));
    dataDict = copy.deepcopy(inputRecord);
    inputRecordEncodings = sensor.getSelf().getOutputValues('sourceEncodings');
    inputRecordCategory = int(sensor.getOutputData('categoryOut')[0]);
    resetOut = sensor.getOutputData('resetOut')[0];

    return SensorInput(dataRow=dataRow,
                       dataDict=dataDict,
                       dataEncodings=inputRecordEncodings,
                       sequenceReset=resetOut,
                       category=inputRecordCategory);
  }

  def _getClassifierInputRecord(self, inputRecord)
  {
    """
    inputRecord - dict containing the input to the sensor
    Return a 'ClassifierInput' object, which contains the mapped
    bucket index for input Record
    """;
    absoluteValue = None;
    bucketIdx = None;

    if( this._predictedFieldName is not None and this._classifierInputEncoder is not None)
    {
      absoluteValue = inputRecord[this._predictedFieldName];
      bucketIdx = this._classifierInputEncoder.getBucketIndices(absoluteValue)[0];
    }

    return ClassifierInput(dataRow=absoluteValue,
                           bucketIndex=bucketIdx);
  }

  def _sensorCompute(self, inputRecord)
  {
    sensor = this._getSensorRegion();
    this._getDataSource().push(inputRecord);
    sensor.setParameter('topDownMode', False);
    sensor.prepareInputs();
    try
    {
      sensor.compute();
    }
    catch( StopIteration as e)
    {
      raise Exception("Unexpected StopIteration", e,
                      "ACTUAL TRACEBACK: %s" % traceback.format_exc());
    }
  }


  def _spCompute(self)
  {
    sp = this._getSPRegion();
    if( sp is None)
    {
      return;
    }

    sp.setParameter('topDownMode', False);
    sp.setParameter('inferenceMode', this.isInferenceEnabled());
    sp.setParameter('learningMode', this.isLearningEnabled());
    sp.prepareInputs();
    sp.compute();
  }


  def _tpCompute(self)
  {
    tp = this._getTPRegion();
    if( tp is None)
    {
      return;
    }

    if (this.getInferenceType() == InferenceType.TemporalAnomaly or
        this._isReconstructionModel())
    {
      topDownCompute = True;
    }
    else
    {
      topDownCompute = False;
    }

    tp = this._getTPRegion();
    tp.setParameter('topDownMode', topDownCompute);
    tp.setParameter('inferenceMode', this.isInferenceEnabled());
    tp.setParameter('learningMode', this.isLearningEnabled());
    tp.prepareInputs();
    tp.compute();
  }


  def _isReconstructionModel(self)
  {
    inferenceType = this.getInferenceType();
    inferenceArgs = this.getInferenceArgs();

    if( inferenceType == InferenceType.TemporalNextStep)
    {
      return True;
    }

    if( inferenceArgs)
    {
      return inferenceArgs.get('useReconstruction', False);
    }
    return False;
  }


  def _isMultiStepModel(self)
  {
    return this.getInferenceType() in (InferenceType.NontemporalMultiStep,
                                       InferenceType.NontemporalClassification,
                                       InferenceType.TemporalMultiStep,
                                       InferenceType.TemporalAnomaly);
  }


  def _isClassificationModel(self)
  {
    return this.getInferenceType() in InferenceType.TemporalClassification;
  }


  def _multiStepCompute(self, rawInput)
  {
    patternNZ = None;
    if( this._getTPRegion() is not None)
    {
      tp = this._getTPRegion();
      tpOutput = tp.getSelf()._tfdr.infActiveState['t'];
      patternNZ = tpOutput.reshape(-1).nonzero()[0];
    }
    else if( this._getSPRegion() is not None)
    {
      sp = this._getSPRegion();
      spOutput = sp.getOutputData('bottomUpOut');
      patternNZ = spOutput.nonzero()[0];
    }
    else if( this._getSensorRegion() is not None)
    {
      sensor = this._getSensorRegion();
      sensorOutput = sensor.getOutputData('dataOut');
      patternNZ = sensorOutput.nonzero()[0];
    }
    else
    {
      raise RuntimeError("Attempted to make multistep prediction without"
                         "TP, SP, or Sensor regions");
    }

    inputTSRecordIdx = rawInput.get('_timestampRecordIdx');
    return this._handleCLAClassifierMultiStep(
                                        patternNZ=patternNZ,
                                        inputTSRecordIdx=inputTSRecordIdx,
                                        rawInput=rawInput);
  }


  def _classificationCompute(self)
  {
    inference = {};
    classifier = this._getClassifierRegion();
    classifier.setParameter('inferenceMode', True);
    classifier.setParameter('learningMode', this.isLearningEnabled());
    classifier.prepareInputs();
    classifier.compute();

    // What we get out is the score for each category. The argmax is
    // then the index of the winning category
    classificationDist = classifier.getOutputData('categoriesOut');
    classification = classificationDist.argmax();
    probabilities = classifier.getOutputData('categoryProbabilitiesOut');
    numCategories = classifier.getParameter('activeOutputCount');
    classConfidences = dict(zip(xrange(numCategories), probabilities));

    inference[InferenceElement.classification] = classification;
    inference[InferenceElement.classConfidences] = {0: classConfidences};

    return inference;
  }


  def _reconstructionCompute(self)
  {
    if( not this.isInferenceEnabled())
    {
      return {};
    }

    sp = this._getSPRegion();
    sensor = this._getSensorRegion();

    #--------------------------------------------------
    // SP Top-down flow
    sp.setParameter('topDownMode', True);
    sp.prepareInputs();
    sp.compute();

    #--------------------------------------------------
    // Sensor Top-down flow
    sensor.setParameter('topDownMode', True);
    sensor.prepareInputs();
    sensor.compute();

    // Need to call getOutputValues() instead of going through getOutputData()
    // because the return values may contain strings, which cannot be passed
    // through the Region.cpp code.

    // predictionRow is a list of values, one for each field. The value is
    //  in the same type as the original input to the encoder and may be a
    //  string for category fields for example.
    predictionRow = copy.copy(sensor.getSelf().getOutputValues('temporalTopDownOut'));
    predictionFieldEncodings = sensor.getSelf().getOutputValues('temporalTopDownEncodings');

    inferences =  {};
    inferences[InferenceElement.prediction] =  tuple(predictionRow);
    inferences[InferenceElement.encodings] = tuple(predictionFieldEncodings);

    return inferences;
  }


  def _anomalyCompute(self)
  {
    """
    Compute Anomaly score, if required
    """;
    inferenceType = this.getInferenceType();

    inferences = {};
    sp = this._getSPRegion();
    score = None;
    if( inferenceType == InferenceType.NontemporalAnomaly)
    {
      score = sp.getOutputData("anomalyScore")[0]; #TODO move from SP to Anomaly ?
    }

    else if( inferenceType == InferenceType.TemporalAnomaly)
    {
      tp = this._getTPRegion();

      if( sp is not None)
      {
        activeColumns = sp.getOutputData("bottomUpOut").nonzero()[0];
      }
      else
      {
        sensor = this._getSensorRegion();
        activeColumns = sensor.getOutputData('dataOut').nonzero()[0];
      }

      if( not this._predictedFieldName in this._input)
      {
        raise ValueError(
          "Expected predicted field '%s' in input row, but was not found!"
          % this._predictedFieldName
        );
      }
      // Calculate the anomaly score using the active columns
      // and previous predicted columns.
      score = this._anomalyInst.compute(
                                   activeColumns,
                                   this._prevPredictedColumns,
                                   inputValue=this._input[this._predictedFieldName]);

      // Store the predicted columns for the next timestep.
      predictedColumns = tp.getOutputData("topDownOut").nonzero()[0];
      this._prevPredictedColumns = copy.deepcopy(predictedColumns);

      // Calculate the classifier's output and use the result as the anomaly
      // label. Stores as string of results.

      // TODO: make labels work with non-SP models
      if( sp is not None)
      {
        this._getAnomalyClassifier().setParameter(
            "activeColumnCount", len(activeColumns));
        this._getAnomalyClassifier().prepareInputs();
        this._getAnomalyClassifier().compute();
        labels = this._getAnomalyClassifier().getSelf().getLabelResults();
        inferences[InferenceElement.anomalyLabel] = "%s" % labels;
      }
    }

    inferences[InferenceElement.anomalyScore] = score;
    return inferences;
  }


  def _handleCLAClassifierMultiStep(self, patternNZ,
                                    inputTSRecordIdx,
                                    rawInput)
  {
    """ Handle the CLA Classifier compute logic when implementing multi-step
    prediction. This is where the patternNZ is associated with one of the
    other fields from the dataset 0 to N steps in the future. This method is
    used by each type of network (encoder only, SP only, SP +TP) to handle the
    compute logic through the CLA Classifier. It fills in the inference dict with
    the results of the compute.
    Parameters:
    -------------------------------------------------------------------
    patternNZ:  The input the CLA Classifier as a list of active input indices
    inputTSRecordIdx: The index of the record as computed from the timestamp
                  and aggregation interval. This normally increments by 1
                  each time unless there are missing records. If there is no
                  aggregation interval or timestamp in the data, this will be
                  None.
    rawInput:   The raw input to the sensor, as a dict.
    """;
    inferenceArgs = this.getInferenceArgs();
    predictedFieldName = inferenceArgs.get('predictedField', None);
    if( predictedFieldName is None)
    {
      raise ValueError(
        "No predicted field was enabled! Did you call enableInference()?"
      );
    }
    this._predictedFieldName = predictedFieldName;

    classifier = this._getClassifierRegion();
    if( not this._hasCL or classifier is None)
    {
      // No classifier so return an empty dict for inferences.
      return {};
    }

    sensor = this._getSensorRegion();
    minLikelihoodThreshold = this._minLikelihoodThreshold;
    maxPredictionsPerStep = this._maxPredictionsPerStep;
    needLearning = this.isLearningEnabled();
    inferences = {};

    // Get the classifier input encoder, if we don't have it already
    if( this._classifierInputEncoder is None)
    {
      if( predictedFieldName is None)
      {
        raise RuntimeError("This experiment description is missing "
              "the 'predictedField' in its config, which is required "
              "for multi-step prediction inference.");
      }

      encoderList = sensor.getSelf().encoder.getEncoderList();
      this._numFields = len(encoderList);

      // This is getting index of predicted field if being fed to CLA.
      fieldNames = sensor.getSelf().encoder.getScalarNames();
      if( predictedFieldName in fieldNames)
      {
        this._predictedFieldIdx = fieldNames.index(predictedFieldName);
      }
      else
      {
        // Predicted field was not fed into the network, only to the classifier
        this._predictedFieldIdx = None;
      }

      // In a multi-step model, the classifier input encoder is separate from
      //  the other encoders and always disabled from going into the bottom of
      // the network.
      if( sensor.getSelf().disabledEncoder is not None)
      {
        encoderList = sensor.getSelf().disabledEncoder.getEncoderList();
      }
      else
      {
        encoderList = [];
      }
      if( len(encoderList) >= 1)
      {
        fieldNames = sensor.getSelf().disabledEncoder.getScalarNames();
        this._classifierInputEncoder = encoderList[fieldNames.index(
                                                        predictedFieldName)];
      }
      else
      {
        // Legacy multi-step networks don't have a separate encoder for the
        //  classifier, so use the one that goes into the bottom of the network
        encoderList = sensor.getSelf().encoder.getEncoderList();
        this._classifierInputEncoder = encoderList[this._predictedFieldIdx];
      }
    }



    // Get the actual value and the bucket index for this sample. The
    // predicted field may not be enabled for input to the network, so we
    // explicitly encode it outside of the sensor
    // TODO: All this logic could be simpler if in the encoder itself
    if( not predictedFieldName in rawInput)
    {
      raise ValueError("Input row does not contain a value for the predicted "
                       "field configured for this model. Missing value for '%s'"
                       % predictedFieldName);
    }
    absoluteValue = rawInput[predictedFieldName];
    bucketIdx = this._classifierInputEncoder.getBucketIndices(absoluteValue)[0];

    // Convert the absolute values to deltas if necessary
    // The bucket index should be handled correctly by the underlying delta encoder
    if( isinstance(this._classifierInputEncoder, DeltaEncoder))
    {
      // Make the delta before any values have been seen 0 so that we do not mess up the
      // range for the adaptive scalar encoder.
      if( not hasattr(self,"_ms_prevVal"))
      {
        this._ms_prevVal = absoluteValue;
      }
      prevValue = this._ms_prevVal;
      this._ms_prevVal = absoluteValue;
      actualValue = absoluteValue - prevValue;
    }
    else
    {
      actualValue = absoluteValue;
    }

    if( isinstance(actualValue, float) and math.isnan(actualValue))
    {
      actualValue = SENTINEL_VALUE_FOR_MISSING_DATA;
    }


    // Pass this information to the classifier's custom compute method
    // so that it can assign the current classification to possibly
    // multiple patterns from the past and current, and also provide
    // the expected classification for some time step(s) in the future.
    classifier.setParameter('inferenceMode', True);
    classifier.setParameter('learningMode', needLearning);
    classificationIn = {'bucketIdx': bucketIdx,
                        'actValue': actualValue};

    // Handle missing records
    if( inputTSRecordIdx is not None)
    {
      recordNum = inputTSRecordIdx;
    }
    else
    {
      recordNum = this.__numRunCalls;
    }
    clResults = classifier.getSelf().customCompute(recordNum=recordNum,
                                           patternNZ=patternNZ,
                                           classification=classificationIn);

    // ---------------------------------------------------------------
    // Get the prediction for every step ahead learned by the classifier
    predictionSteps = classifier.getParameter('steps');
    predictionSteps = [int(x) for x in predictionSteps.split(',')];

    // We will return the results in this dict. The top level keys
    // are the step number, the values are the relative likelihoods for
    // each classification value in that time step, represented as
    // another dict where the keys are the classification values and
    // the values are the relative likelihoods.
    inferences[InferenceElement.multiStepPredictions] = dict();
    inferences[InferenceElement.multiStepBestPredictions] = dict();
    inferences[InferenceElement.multiStepBucketLikelihoods] = dict();


    // ======================================================================
    // Plug in the predictions for each requested time step.
    for( steps in predictionSteps)
    {
      // From the clResults, compute the predicted actual value. The
      // CLAClassifier classifies the bucket index and returns a list of
      // relative likelihoods for each bucket. Let's find the max one
      // and then look up the actual value from that bucket index
      likelihoodsVec = clResults[steps];
      bucketValues = clResults['actualValues'];

      // Create a dict of value:likelihood pairs. We can't simply use
      //  dict(zip(bucketValues, likelihoodsVec)) because there might be
      //  duplicate bucketValues (this happens early on in the model when
      //  it doesn't have actual values for each bucket so it returns
      //  multiple buckets with the same default actual value).
      likelihoodsDict = dict();
      bestActValue = None;
      bestProb = None;
      for (actValue, prob) in zip(bucketValues, likelihoodsVec)
      {
        if( actValue in likelihoodsDict)
        {
          likelihoodsDict[actValue] += prob;
        }
        else
        {
          likelihoodsDict[actValue] = prob;
        }
        // Keep track of best
        if( bestProb is None or likelihoodsDict[actValue] > bestProb)
        {
          bestProb = likelihoodsDict[actValue];
          bestActValue = actValue;
        }
      }


      // Remove entries with 0 likelihood or likelihood less than
      // minLikelihoodThreshold, but don't leave an empty dict.
      likelihoodsDict = CLAModel._removeUnlikelyPredictions(
          likelihoodsDict, minLikelihoodThreshold, maxPredictionsPerStep);

      // calculate likelihood for each bucket
      bucketLikelihood = {};
      for( k in likelihoodsDict.keys())
      {
        bucketLikelihood[this._classifierInputEncoder.getBucketIndices(k)[0]] = (
                                                                likelihoodsDict[k]);
      }

      // ---------------------------------------------------------------------
      // If we have a delta encoder, we have to shift our predicted output value
      //  by the sum of the deltas
      if( isinstance(this._classifierInputEncoder, DeltaEncoder))
      {
        // Get the prediction history for this number of timesteps.
        // The prediction history is a store of the previous best predicted values.
        // This is used to get the final shift from the current absolute value.
        if( not hasattr(self, '_ms_predHistories'))
        {
          this._ms_predHistories = dict();
        }
        predHistories = this._ms_predHistories;
        if( not steps in predHistories)
        {
          predHistories[steps] = deque();
        }
        predHistory = predHistories[steps];

        // Find the sum of the deltas for the steps and use this to generate
        // an offset from the current absolute value
        sumDelta = sum(predHistory);
        offsetDict = dict();
        for (k, v) in likelihoodsDict.iteritems()
        {
          if( k is not None)
          {
            // Reconstruct the absolute value based on the current actual value,
            // the best predicted values from the previous iterations,
            // and the current predicted delta
            offsetDict[absoluteValue+float(k)+sumDelta] = v;
          }
        }

        // calculate likelihood for each bucket
        bucketLikelihoodOffset = {};
        for( k in offsetDict.keys())
        {
          bucketLikelihoodOffset[this._classifierInputEncoder.getBucketIndices(k)[0]] = (
                                                                            offsetDict[k]);
        }


        // Push the current best delta to the history buffer for reconstructing the final delta
        if( bestActValue is not None)
        {
          predHistory.append(bestActValue);
        }
        // If we don't need any more values in the predictionHistory, pop off
        // the earliest one.
        if( len(predHistory) >= steps)
        {
          predHistory.popleft();
        }

        // Provide the offsetDict as the return value
        if( len(offsetDict)>0)
        {
          inferences[InferenceElement.multiStepPredictions][steps] = offsetDict;
          inferences[InferenceElement.multiStepBucketLikelihoods][steps] = bucketLikelihoodOffset;
        }
        else
        {
          inferences[InferenceElement.multiStepPredictions][steps] = likelihoodsDict;
          inferences[InferenceElement.multiStepBucketLikelihoods][steps] = bucketLikelihood;
        }

        if( bestActValue is None)
        {
          inferences[InferenceElement.multiStepBestPredictions][steps] = None;
        }
        else
        {
          inferences[InferenceElement.multiStepBestPredictions][steps] = (
            absoluteValue + sumDelta + bestActValue);
        }
      }

      // ---------------------------------------------------------------------
      // Normal case, no delta encoder. Just plug in all our multi-step predictions
      //  with likelihoods as well as our best prediction
      else
      {
        // The multiStepPredictions element holds the probabilities for each
        //  bucket
        inferences[InferenceElement.multiStepPredictions][steps] = (
                                                      likelihoodsDict);
        inferences[InferenceElement.multiStepBestPredictions][steps] = (
                                                      bestActValue);
        inferences[InferenceElement.multiStepBucketLikelihoods][steps] = (
                                                      bucketLikelihood);
      }
    }


    return inferences;
  }


  @classmethod;
  def _removeUnlikelyPredictions(cls, likelihoodsDict, minLikelihoodThreshold,
                                 maxPredictionsPerStep)
  {
    """Remove entries with 0 likelihood or likelihood less than
    minLikelihoodThreshold, but don't leave an empty dict.
    """;
    maxVal = (None, None);
    for (k, v) in likelihoodsDict.items()
    {
      if( len(likelihoodsDict) <= 1)
      {
        break;
      }
      if( maxVal[0] is None or v >= maxVal[1])
      {
        if( maxVal[0] is not None and maxVal[1] < minLikelihoodThreshold)
        {
          del likelihoodsDict[maxVal[0]];
        }
        maxVal = (k, v);
      }
      else if( v < minLikelihoodThreshold)
      {
        del likelihoodsDict[k];
      }
    }
    // Limit the number of predictions to include.
    likelihoodsDict = dict(sorted(likelihoodsDict.iteritems(),
                                  key=itemgetter(1),
                                  reverse=True)[:maxPredictionsPerStep]);
    return likelihoodsDict;
  }


  def getRuntimeStats(self)
  {
    """ [virtual method override] get runtime statistics specific to this
    model, i.e. activeCellOverlapAvg
        return:
            a dict where keys are statistic names and values are the stats
    """;
    ret = {"numRunCalls" : this.__numRunCalls};

    #--------------------------------------------------
    // Query temporal network stats
    temporalStats = dict();
    if( this._hasTP)
    {
      for( stat in this._netInfo.statsCollectors)
      {
        sdict = stat.getStats();
        temporalStats.update(sdict);
      }
    }

    ret[InferenceType.getLabel(InferenceType.TemporalNextStep)] = temporalStats;


    return ret;
  }


  def getFieldInfo(self, includeClassifierOnlyField=False)
  {
    """ [virtual method override]
        Returns the sequence of FieldMetaInfo objects specifying this
        Model's output; note that this may be different than the list of
        FieldMetaInfo objects supplied at initialization (e.g., due to the
        transcoding of some input fields into meta-fields, such as datetime
        -> dayOfWeek, timeOfDay, etc.)
    Returns:    List of FieldMetaInfo objects (see description above)
    """;

    encoder = this._getEncoder();

    fieldNames = encoder.getScalarNames();
    fieldTypes = encoder.getDecoderOutputFieldTypes();
    assert len(fieldNames) == len(fieldTypes);

    // Also include the classifierOnly field?
    encoder = this._getClassifierOnlyEncoder();
    if( includeClassifierOnlyField and encoder is not None)
    {
      addFieldNames = encoder.getScalarNames();
      addFieldTypes = encoder.getDecoderOutputFieldTypes();
      assert len(addFieldNames) == len(addFieldTypes);
      fieldNames = list(fieldNames) + addFieldNames;
      fieldTypes = list(fieldTypes) + addFieldTypes;
    }

    fieldMetaList = map(FieldMetaInfo._make,
                        zip(fieldNames,
                            fieldTypes,
                            itertools.repeat(FieldMetaSpecial.none)));

    return tuple(fieldMetaList);
  }


  def _getLogger(self)
  {
    """ Get the logger for this object. This is a protected method that is used
    by the Model to access the logger created by the subclass
    return:
      A logging.Logger object. Should not be None
    """;
    return this.__logger;
  }


  def _getSPRegion(self)
  {
    """
    Returns reference to the network's SP region
    """;
    return this._netInfo.net.regions.get('SP', None);
  }


  def _getTPRegion(self)
  {
    """
    Returns reference to the network's TP region
    """;
    return this._netInfo.net.regions.get('TP', None);
  }


  def _getSensorRegion(self)
  {
    """
    Returns reference to the network's Sensor region
    """;
    return this._netInfo.net.regions['sensor'];
  }


  def _getClassifierRegion(self)
  {
    """
    Returns reference to the network's Classifier region
    """;
    if (this._netInfo.net is not None and
        "Classifier" in this._netInfo.net.regions)
    {
      return this._netInfo.net.regions["Classifier"];
    }
    else
    {
      return None;
    }
  }


  def _getAnomalyClassifier(self)
  {
    return this._netInfo.net.regions.get("AnomalyClassifier", None);
  }


  def _getEncoder(self)
  {
    """
    Returns:  sensor region's encoder for the given network
    """;
    return  this._getSensorRegion().getSelf().encoder;
  }

  def _getClassifierOnlyEncoder(self)
  {
    """
    Returns:  sensor region's encoder that is sent only to the classifier,
                not to the bottom of the network
    """;
    return  this._getSensorRegion().getSelf().disabledEncoder;
  }


  def _getDataSource(self)
  {
    """
    Returns: data source that we installed in sensor region
    """;
    return this._getSensorRegion().getSelf().dataSource;
  }


  def __createCLANetwork(self, sensorParams, spEnable, spParams, tpEnable,
                         tpParams, clEnable, clParams, anomalyParams)
  {
    """ Create a CLA network and return it.
    description:  CLA Model description dictionary (TODO: define schema)
    Returns:      NetworkInfo instance;
    """;

    #--------------------------------------------------
    // Create the network
    n = Network();


    #--------------------------------------------------
    // Add the Sensor
    n.addRegion("sensor", "py.RecordSensor", json.dumps(dict(verbosity=sensorParams['verbosity'])));
    sensor = n.regions['sensor'].getSelf();

    enabledEncoders = copy.deepcopy(sensorParams['encoders']);
    for( name, params in enabledEncoders.items())
    {
      if( params is not None)
      {
        classifierOnly = params.pop('classifierOnly', False);
        if( classifierOnly)
        {
          enabledEncoders.pop(name);
        }
      }
    }

    // Disabled encoders are encoders that are fed to CLAClassifierRegion but not
    // SP or TP Regions. This is to handle the case where the predicted field
    // is not fed through the SP/TP. We typically just have one of these now.
    disabledEncoders = copy.deepcopy(sensorParams['encoders']);
    for( name, params in disabledEncoders.items())
    {
      if( params is None)
      {
        disabledEncoders.pop(name);
      }
      else
      {
        classifierOnly = params.pop('classifierOnly', False);
        if( not classifierOnly)
        {
          disabledEncoders.pop(name);
        }
      }
    }

    encoder = MultiEncoder(enabledEncoders);

    sensor.encoder = encoder;
    sensor.disabledEncoder = MultiEncoder(disabledEncoders);
    sensor.dataSource = DataBuffer();

    prevRegion = "sensor";
    prevRegionWidth = encoder.getWidth();

    // SP is not enabled for spatial classification network
    if( spEnable)
    {
      spParams = spParams.copy();
      spParams['inputWidth'] = prevRegionWidth;
      this.__logger.debug("Adding SPRegion; spParams: %r" % spParams);
      n.addRegion("SP", "py.SPRegion", json.dumps(spParams));

      // Link SP region
      n.link("sensor", "SP", "UniformLink", "");
      n.link("sensor", "SP", "UniformLink", "", srcOutput="resetOut",
             destInput="resetIn");

      n.link("SP", "sensor", "UniformLink", "", srcOutput="spatialTopDownOut",
             destInput="spatialTopDownIn");
      n.link("SP", "sensor", "UniformLink", "", srcOutput="temporalTopDownOut",
             destInput="temporalTopDownIn");

      prevRegion = "SP";
      prevRegionWidth = spParams['columnCount'];
    }

    if( tpEnable)
    {
      tpParams = tpParams.copy();
      if( prevRegion == 'sensor')
      {
        tpParams['inputWidth'] = tpParams['columnCount'] = prevRegionWidth;
      }
      else
      {
        assert tpParams['columnCount'] == prevRegionWidth;
        tpParams['inputWidth'] = tpParams['columnCount'];
      }

      this.__logger.debug("Adding TPRegion; tpParams: %r" % tpParams);
      n.addRegion("TP", "py.TPRegion", json.dumps(tpParams));

      // Link TP region
      n.link(prevRegion, "TP", "UniformLink", "");
      if( prevRegion != "sensor")
      {
        n.link("TP", prevRegion, "UniformLink", "", srcOutput="topDownOut",
           destInput="topDownIn");
      }
      else
      {
        n.link("TP", prevRegion, "UniformLink", "", srcOutput="topDownOut",
           destInput="temporalTopDownIn");
      }
      n.link("sensor", "TP", "UniformLink", "", srcOutput="resetOut",
         destInput="resetIn");

      prevRegion = "TP";
      prevRegionWidth = tpParams['inputWidth'];
    }

    if( clEnable and clParams is not None)
    {
      clParams = clParams.copy();
      clRegionName = clParams.pop('regionName');
      this.__logger.debug("Adding %s; clParams: %r" % (clRegionName,
                                                      clParams));
      n.addRegion("Classifier", "py.%s" % str(clRegionName), json.dumps(clParams));

      n.link("sensor", "Classifier", "UniformLink", "", srcOutput="categoryOut",
             destInput="categoryIn");

      n.link(prevRegion, "Classifier", "UniformLink", "");
    }

    if( this.getInferenceType() == InferenceType.TemporalAnomaly)
    {
      anomalyClParams = dict(
          trainRecords=anomalyParams.get('autoDetectWaitRecords', None),
          cacheSize=anomalyParams.get('anomalyCacheRecords', None)
      );
      this._addAnomalyClassifierRegion(n, anomalyClParams, spEnable, tpEnable);
    }

    #--------------------------------------------------
    // NuPIC doesn't initialize the network until you try to run it
    // but users may want to access components in a setup callback
    n.initialize();

    return NetworkInfo(net=n, statsCollectors=[]);
  }


  def __getstate__(self)
  {
    """
    Return serializable state.  This function will return a version of the
    __dict__ with data that shouldn't be pickled stripped out. In particular,
    the CLA Network is stripped out because it has it's own serialization
    mechanism)
    See also: _serializeExtraData()
    """;

    // Remove ephemeral member variables from state
    state = this.__dict__.copy();

    state["_netInfo"] = NetworkInfo(net=None,
                        statsCollectors=this._netInfo.statsCollectors);


    for( ephemeral in [this.__manglePrivateMemberName("__restoringFromState"),
                      this.__manglePrivateMemberName("__logger")])
    {
      state.pop(ephemeral);
    }

    return state;
  }


  def __setstate__(self, state)
  {
    """
    Set the state of ourself from a serialized state.
    See also: _deSerializeExtraData
    """;

    this.__dict__.update(state);

    // Mark beginning of restoration.
    #
    // this.__restoringFromState will be reset to False upon completion of
    // object restoration in _deSerializeExtraData()
    this.__restoringFromState = True;

    // set up logging
    this.__logger = initLogger(self);


    // =========================================================================
    // TODO: Temporary migration solution
    if( not hasattr(self, "_Model__inferenceType"))
    {
      this.__restoringFromV1 = True;
      this._hasSP = True;
      if( this.__temporalNetInfo is not None)
      {
        this._Model__inferenceType = InferenceType.TemporalNextStep;
        this._netInfo = this.__temporalNetInfo;
        this._hasTP = True;
      }
      else
      {
        raise RuntimeError("The Nontemporal inference type is not supported");
      }

      this._Model__inferenceArgs = {};
      this._Model__learningEnabled = True;
      this._Model__inferenceEnabled = True;

      // Remove obsolete members
      this.__dict__.pop("_CLAModel__encoderNetInfo", None);
      this.__dict__.pop("_CLAModel__nonTemporalNetInfo", None);
      this.__dict__.pop("_CLAModel__temporalNetInfo", None);
    }


    // -----------------------------------------------------------------------
    // Migrate from v2
    if( not hasattr(self, "_netInfo"))
    {
      this._hasSP = False;
      this._hasTP = False;
      if( this.__encoderNetInfo is not None)
      {
        this._netInfo = this.__encoderNetInfo;
      }
      else if( this.__nonTemporalNetInfo is not None)
      {
        this._netInfo = this.__nonTemporalNetInfo;
        this._hasSP = True;
      }
      else
      {
        this._netInfo = this.__temporalNetInfo;
        this._hasSP = True;
        this._hasTP = True;
      }

      // Remove obsolete members
      this.__dict__.pop("_CLAModel__encoderNetInfo", None);
      this.__dict__.pop("_CLAModel__nonTemporalNetInfo", None);
      this.__dict__.pop("_CLAModel__temporalNetInfo", None);
    }


    // -----------------------------------------------------------------------
    // Migrate from when Anomaly was not separate class
    if( not hasattr(self, "_anomalyInst"))
    {
      this._anomalyInst = Anomaly();
    }


    // This gets filled in during the first infer because it can only be
    //  determined at run-time
    this._classifierInputEncoder = None;

    if( not hasattr(self, '_minLikelihoodThreshold'))
    {
      this._minLikelihoodThreshold = DEFAULT_LIKELIHOOD_THRESHOLD;
    }

    if( not hasattr(self, '_maxPredictionsPerStep'))
    {
      this._maxPredictionsPerStep = DEFAULT_MAX_PREDICTIONS_PER_STEP;
    }

    if( not hasattr(self, '_hasCL'))
    {
      this._hasCL = (this._getClassifierRegion() is not None);
    }

    this.__logger.debug("Restoring %s from state..." % this.__class__.__name__);
  }


  @staticmethod;
  def getProtoType()
  {
    return CLAModelProto;
  }


  def write(self, proto)
  {
    inferenceType = this.getInferenceType();
    // lower-case first letter to be compatible with capnproto enum naming
    inferenceType = inferenceType[:1].lower() + inferenceType[1:];
    proto.inferenceType = inferenceType;

    proto.numRunCalls = this.__numRunCalls;
    proto.minLikelihoodThreshold = this._minLikelihoodThreshold;
    proto.maxPredictionsPerStep = this._maxPredictionsPerStep;

    this._netInfo.net.write(proto.network);
  }


  @classmethod;
  def read(cls, proto)
  {
    inferenceType = str(proto.inferenceType);
    // upper-case first letter to be compatible with enum InferenceType naming
    inferenceType = inferenceType[:1].upper() + inferenceType[1:];
    inferenceType = InferenceType.getValue(inferenceType);

    network = Network.read(proto.network);
    spEnable = ("SP" in network.regions);
    tpEnable = ("TP" in network.regions);
    clEnable = ("Classifier" in network.regions);

    model = cls(spEnable=spEnable,
                tpEnable=tpEnable,
                clEnable=clEnable,
                inferenceType=inferenceType,
                network=network);

    model.__numRunCalls = proto.numRunCalls;
    model._minLikelihoodThreshold = proto.minLikelihoodThreshold;
    model._maxPredictionsPerStep = proto.maxPredictionsPerStep;

    model._getSensorRegion().getSelf().dataSource = DataBuffer();
    model._netInfo.net.initialize();

    // Mark end of restoration from state
    model.__restoringFromState = False;

    return model;
  }


  def _serializeExtraData(self, extraDataDir)
  {
    """ [virtual method override] This method is called during serialization
    with an external directory path that can be used to bypass pickle for saving
    large binary states.
    extraDataDir:
                  Model's extra data directory path
    """;
    makeDirectoryFromAbsolutePath(extraDataDir);

    #--------------------------------------------------
    // Save the network
    outputDir = this.__getNetworkStateDirectory(extraDataDir=extraDataDir);

    this.__logger.debug("Serializing network...");

    this._netInfo.net.save(outputDir);

    this.__logger.debug("Finished serializing network");

    return;
  }


  def _deSerializeExtraData(self, extraDataDir)
  {
    """ [virtual method override] This method is called during deserialization
    (after __setstate__) with an external directory path that can be used to
    bypass pickle for loading large binary states.
    extraDataDir:
                  Model's extra data directory path
    """;
    assert this.__restoringFromState;

    #--------------------------------------------------
    // Check to make sure that our Network member wasn't restored from
    // serialized data
    assert (this._netInfo.net is None), "Network was already unpickled";

    #--------------------------------------------------
    // Restore the network
    stateDir = this.__getNetworkStateDirectory(extraDataDir=extraDataDir);

    this.__logger.debug(
      "(%s) De-serializing network...", self);

    this._netInfo.net = Network(stateDir);

    this.__logger.debug(
      "(%s) Finished de-serializing network", self);


    // NuPIC doesn't initialize the network until you try to run it
    // but users may want to access components in a setup callback
    this._netInfo.net.initialize();


    // Used for backwards compatibility for anomaly classification models.
    // Previous versions used the CLAModelClassifierHelper class for utilizing
    // the KNN classifier. Current version uses KNNAnomalyClassifierRegion to
    // encapsulate all the classifier functionality.
    if( this.getInferenceType() == InferenceType.TemporalAnomaly)
    {
      classifierType = this._getAnomalyClassifier().getSelf().__class__.__name__;
      if( classifierType is 'KNNClassifierRegion')
      {

        anomalyClParams = dict(
          trainRecords=this._classifier_helper._autoDetectWaitRecords,
          cacheSize=this._classifier_helper._history_length,
        );

        spEnable = (this._getSPRegion() is not None);
        tpEnable = True;

        // Store original KNN region
        knnRegion = this._getAnomalyClassifier().getSelf();

        // Add new KNNAnomalyClassifierRegion
        this._addAnomalyClassifierRegion(this._netInfo.net, anomalyClParams,
                                         spEnable, tpEnable);

        // Restore state
        this._getAnomalyClassifier().getSelf()._iteration = this.__numRunCalls;
        this._getAnomalyClassifier().getSelf()._recordsCache = (
            this._classifier_helper.saved_states);
        this._getAnomalyClassifier().getSelf().saved_categories = (
            this._classifier_helper.saved_categories);
        this._getAnomalyClassifier().getSelf()._knnclassifier = knnRegion;

        // Set TP to output neccessary information
        this._getTPRegion().setParameter('anomalyMode', True);

        // Remove old classifier_helper
        del this._classifier_helper;

        this._netInfo.net.initialize();
      }
    }

    #--------------------------------------------------
    // Mark end of restoration from state
    this.__restoringFromState = False;

    this.__logger.debug("(%s) Finished restoring from state", self);

    return;
  }


  def _addAnomalyClassifierRegion(self, network, params, spEnable, tpEnable)
  {
    """
    Attaches an 'AnomalyClassifier' region to the network. Will remove current
    'AnomalyClassifier' region if it exists.
    Parameters
    -----------
    network - network to add the AnomalyClassifier region
    params - parameters to pass to the region
    spEnable - True if network has an SP region
    tpEnable - True if network has a TP region; Currently requires True
    """;

    allParams = copy.deepcopy(params);
    knnParams = dict(k=1,
                     distanceMethod='rawOverlap',
                     distanceNorm=1,
                     doBinarization=1,
                     replaceDuplicates=0,
                     maxStoredPatterns=1000);
    allParams.update(knnParams);

    // Set defaults if not set
    if( allParams['trainRecords'] is None)
    {
      allParams['trainRecords'] = DEFAULT_ANOMALY_TRAINRECORDS;
    }

    if( allParams['cacheSize'] is None)
    {
      allParams['cacheSize'] = DEFAULT_ANOMALY_CACHESIZE;
    }

    // Remove current instance if already created (used for deserializing)
    if( this._netInfo is not None and this._netInfo.net is not None \
              and this._getAnomalyClassifier() is not None)
    {
      this._netInfo.net.removeRegion('AnomalyClassifier');
    }

    network.addRegion("AnomalyClassifier",
                      "py.KNNAnomalyClassifierRegion",
                      json.dumps(allParams));

    // Attach link to SP
    if( spEnable)
    {
      network.link("SP", "AnomalyClassifier", "UniformLink", "",
          srcOutput="bottomUpOut", destInput="spBottomUpOut");
    }
    else
    {
      network.link("sensor", "AnomalyClassifier", "UniformLink", "",
          srcOutput="dataOut", destInput="spBottomUpOut");
    }

    // Attach link to TP
    if( tpEnable)
    {
      network.link("TP", "AnomalyClassifier", "UniformLink", "",
              srcOutput="topDownOut", destInput="tpTopDownOut");
      network.link("TP", "AnomalyClassifier", "UniformLink", "",
              srcOutput="lrnActiveStateT", destInput="tpLrnActiveStateT");
    }
    else
    {
      raise RuntimeError("TemporalAnomaly models require a TP region.");
    }
  }


  def __getNetworkStateDirectory(self, extraDataDir)
  {
    """
    extraDataDir:
                  Model's extra data directory path
    Returns:      Absolute directory path for saving CLA Network
    """;
    if( this.__restoringFromV1)
    {
      if( this.getInferenceType() == InferenceType.TemporalNextStep)
      {
        leafName = 'temporal'+ "-network.nta";
      }
      else
      {
        leafName = 'nonTemporal'+ "-network.nta";
      }
    }
    else
    {
      leafName = InferenceType.getLabel(this.getInferenceType()) + "-network.nta";
    }
    path = os.path.join(extraDataDir, leafName);
    path = os.path.abspath(path);
    return path;
  }


  def __manglePrivateMemberName(self, privateMemberName, skipCheck=False)
  {
    """ Mangles the given mangled (private) member name; a mangled member name
    is one whose name begins with two or more underscores and ends with one
    or zero underscores.
    privateMemberName:
                  The private member name (e.g., "__logger")
    skipCheck:    Pass True to skip test for presence of the demangled member
                  in our instance.
    Returns:      The demangled member name (e.g., "_CLAModel__logger")
    """;

    assert privateMemberName.startswith("__"), \
           "%r doesn't start with __" % privateMemberName;
    assert not privateMemberName.startswith("___"), \
           "%r starts with ___" % privateMemberName;
    assert not privateMemberName.endswith("__"), \
           "%r ends with more than one underscore" % privateMemberName;

    realName = "_" + (this.__myClassName).lstrip("_") + privateMemberName;

    if( not skipCheck)
    {
      // This will throw an exception if the member is missing
      getattr(self, realName);
    }

    return realName;
  }
}



class DataBuffer(object)
{
  """
      A simple FIFO stack. Add data when it's available, and
      implement getNextRecordDict() so DataBuffer can be used as a DataSource
      in a CLA Network.
      Currently, DataBuffer requires the stack to contain 0 or 1 records.
      This requirement may change in the future, and is trivially supported
      by removing the assertions.
  """;
  def __init__(self)
  {
    this.stack = [];
  }

  def push(self, data)
  {
    assert len(this.stack) == 0;

    // Copy the data, because sensor's pre-encoding filters (e.g.,
    // AutoResetFilter) may modify it.  Our caller relies on the input record
    // remaining unmodified.
    data = data.__class__(data);

    this.stack.append(data);
  }

  def getNextRecordDict(self)
  {
    assert len(this.stack) > 0;
    return this.stack.pop();
  }
}





    */
}