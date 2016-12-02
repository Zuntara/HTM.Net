using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;
using System.Threading;
using HTM.Net.Data;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming.Permutations;
using HTM.Net.Swarming.HyperSearch;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using MetricsManager = HTM.Net.Research.opf.PredictionMetricsManager;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Swarming
{
    /// <summary>
    /// This class runs an a given Model
    /// </summary>
    public class OpfModelRunner
    {
        #region Fields

        protected ILog _logger = LogManager.GetLogger(typeof(OpfModelRunner));
        // The minimum number of records that need to have been read for this model
        // to be a candidate for 'best model'
        int? _MIN_RECORDS_TO_BE_BEST = null;

        // The number of points we look at when trying to figure out whether or not a
        // model has matured
        int? _MATURITY_NUM_POINTS = null;

        // The maximum rate of change in the model's metric for it to be considered 'mature'
        double? _MATURITY_MAX_CHANGE = null;
        protected ulong? _modelID;
        protected uint? _jobID;
        private string _predictedField;
        private ExperimentParameters _experimentDir;
        private string[] _reportKeyPatterns;
        protected string _optimizeKeyPattern;
        protected BaseClientJobDao _jobsDAO;
        private string _modelCheckpointGUID;
        protected int? _predictionCacheMaxRecords;
        private bool _isMaturityEnabled;
        protected string _optimizedMetricLabel;
        protected string _cmpReason;
        private ExperimentParameters _modelControl; // ControlModelDescription
        protected opf.Model _model;
        private MetricsManager __metricMgr;
        private BatchedCsvStream<string[]> _inputSource;
        private string[] __loggedMetricPatterns;
        protected bool _isBestModel;
        private bool _isBestModelStored;
        protected List<string> _reportMetricLabels;
        private PeriodicActivityMgr _periodic;
        protected int? _currentRecordIndex;
        protected bool _isKilled;
        protected bool _isCanceled;
        protected bool _isMature;
        private Queue<ModelResult> __predictionCache;

        #endregion

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelID">ID for this model in the models table</param>
        /// <param name="jobID">ID for this hypersearch job in the jobs table</param>
        /// <param name="predictedField">Name of the input field for which this model is being optimized</param>
        /// <param name="experimentDir"> Directory path containing the experiment's description.py script</param>
        /// <param name="reportKeyPatterns">list of items from the results dict to include in the report. These can be regular expressions.</param>
        /// <param name="optimizeKeyPattern">Which report item, if any, we will be optimizing for. 
        /// This can also be a regular expression, but is an error if it matches more than one key from the experiment's results.
        /// </param>
        /// <param name="jobsDAO">Jobs data access object - the interface to the jobs database which has the model's table.</param>
        /// <param name="modelCheckpointGUID">A persistent, globally-unique identifier for constructing the model checkpoint key. If None, then don't bother creating a model checkpoint.</param>
        /// <param name="predictionCacheMaxRecords">Maximum number of records for the prediction output cache.
        /// Pass None for default value.</param>
        public OpfModelRunner(ulong? modelID, uint? jobID, string predictedField, ExperimentParameters experimentDir,
               string[] reportKeyPatterns, string optimizeKeyPattern, BaseClientJobDao jobsDAO,
               string modelCheckpointGUID, int? predictionCacheMaxRecords = null)
        {
            // -----------------------------------------------------------------------
            // Initialize class constants
            // -----------------------------------------------------------------------
            this._MIN_RECORDS_TO_BE_BEST = SwarmConfiguration.bestModelMinRecords;
            this._MATURITY_MAX_CHANGE = SwarmConfiguration.maturityPctChange;
            this._MATURITY_NUM_POINTS = SwarmConfiguration.maturityNumPoints;

            // -----------------------------------------------------------------------
            // Initialize instance variables
            // -----------------------------------------------------------------------
            this._modelID = modelID;
            this._jobID = jobID;
            this._predictedField = predictedField;
            this._experimentDir = experimentDir;
            this._reportKeyPatterns = reportKeyPatterns;
            this._optimizeKeyPattern = optimizeKeyPattern;
            this._jobsDAO = jobsDAO;
            this._modelCheckpointGUID = modelCheckpointGUID;
            this._predictionCacheMaxRecords = predictionCacheMaxRecords;

            this._isMaturityEnabled = SwarmConfiguration.enableModelMaturity;

            //this._logger = logging.getLogger(".".join( ['com.numenta',this.__class__.__module__, this.__class__.__name__]));

            this._optimizedMetricLabel = null;
            //      this._reportMetricLabels = [];


            // Our default completion reason
            this._cmpReason = BaseClientJobDao.CMPL_REASON_EOF;

            // The manager object to compute the metrics for this model
            this.__metricMgr = null;

            //    // Will be set to a new instance of OPFTaskDriver by __runTask()
            //    // this.__taskDriver = None

            //    // Current task control parameters. Will be set by __runTask()
            //    this.__task = null;

            // Will be set to a new instance of PeriodicActivityManager by __runTask()
            this._periodic = null;

            //    // Will be set to streamDef string by _runTask()
            //    this._streamDef = null;

            // Will be set to new OpfExperiment instance by run()
            this._model = null;

            // Will be set to new InputSource by __runTask()
            this._inputSource = null;

            // 0-based index of the record being processed;
            // Initialized and updated by __runTask()
            this._currentRecordIndex = null;

            // Interface to write predictions to a persistent storage
            //this._predictionLogger = null;

            // In-memory cache for predictions. Predictions are written here for speed
            // when they don't need to be written to a persistent store
            this.__predictionCache = new Queue<ModelResult>();

            // Flag to see if this is the best model in the job (as determined by the
            // model chooser logic). This is essentially a cache of the value in the
            // ClientJobsDB
            this._isBestModel = false;

            // Flag to see if there is a best model (not necessarily this one)
            // stored in the DB
            this._isBestModelStored = false;


            // -----------------------------------------------------------------------
            // Flags for model cancelation/checkpointing
            // -----------------------------------------------------------------------

            // Flag to see if the job that this model is part of
            this._isCanceled = false;

            // Flag to see if model was killed, either by the model terminator or by the
            // hypsersearch implementation (ex. the a swarm is killed/matured)
            this._isKilled = false;

            // Flag to see if the model is matured. In most cases, this means that we
            // should stop running the model. The only execption is if this model is the
            // best model for the job, in which case it should continue running.
            this._isMature = false;

            //    // Event to see if interrupt signal has been sent
            //    this._isInterrupted = threading.Event();

            //    // -----------------------------------------------------------------------
            //    // Facilities for measuring model maturity
            //    // -----------------------------------------------------------------------
            //    // List of tuples, (iteration, metric), used to see if the model has 'matured'
            //    this._metricRegression = regression.AveragePctChange(windowSize: this._MATURITY_NUM_POINTS);

            this.__loggedMetricPatterns = new string[0];
        }

        /// <summary>
        /// Runs the OPF Model
        /// </summary>
        /// <returns>(completionReason, completionMsg) where completionReason is one of the ClientJobsDAO.CMPL_REASON_XXX equates.</returns>
        public virtual ModelCompletionStatus run()
        {
            // Load experiments description module
            var modelDescription = _experimentDir;
            this._modelControl = _experimentDir;

            // -----------------------------------------------------------------------
            // Create the input data stream for this task
            var streamDef = this._modelControl.Control.DatasetSpec;

            string fileName = streamDef.streams[0].source;

            IStream<string> fileStream = new Stream<string>(YieldingFileReader.ReadAllLines(fileName, Encoding.UTF8));
            _inputSource = BatchedCsvStream<string>.Batch(fileStream, 20, false, 3);

            // -----------------------------------------------------------------------
            // Get field statistics from the input source
            var fieldStats = this._getFieldStats();
            // -----------------------------------------------------------------------
            // Construct the model instance
            this._model = ModelFactory.Create(modelDescription);
            this._model.setFieldStatistics(fieldStats);
            this._model.enableLearning();
            this._model.enableInference(this._modelControl.Control.InferenceArgs);

            // -----------------------------------------------------------------------
            // Instantiate the metrics
            this.__metricMgr = new MetricsManager(this._modelControl.Control.Metrics,
                                              this._model.getFieldInfo(),
                                              this._model.getInferenceType());

            this.__loggedMetricPatterns = this._modelControl.Control.LoggedMetrics ?? new string[0];

            this._optimizedMetricLabel = this.__getOptimizedMetricLabel();
            this._reportMetricLabels = ArrayUtils.MatchPatterns(this._reportKeyPatterns, this._getMetricLabels());


            // -----------------------------------------------------------------------
            // Initialize periodic activities (e.g., for model result updates)
            this._periodic = this._initPeriodicActivities();

            // -----------------------------------------------------------------------
            // Create our top-level loop-control iterator
            int numIters = this._modelControl.Control.IterationCount.GetValueOrDefault(-1);

            // Are we asked to turn off learning for a certain // of iterations near the
            //  end?
            int? learningOffAt = null;
            int iterationCountInferOnly = this._modelControl.Control.IterationCountInferOnly.GetValueOrDefault();
            if (iterationCountInferOnly == -1)
            {
                this._model.disableLearning();
            }
            else if (iterationCountInferOnly > 0)
            {
                Debug.Assert(numIters > iterationCountInferOnly,
                    "when iterationCountInferOnly is specified, iterationCount must be greater than iterationCountInferOnly.");
                learningOffAt = numIters - iterationCountInferOnly;
            }

            this.__runTaskMainLoop(numIters, learningOffAt: learningOffAt);

            // -----------------------------------------------------------------------
            // Perform final operations for model
            this._finalize();

            return new ModelCompletionStatus(this._cmpReason, null);
        }

        /// <summary>
        /// Get the label for the metric being optimized. This function also caches
        /// the label in the instance variable self._optimizedMetricLabel
        /// </summary>
        /// <returns></returns>
        private string __getOptimizedMetricLabel()
        {
            var matchingKeys = ArrayUtils.MatchPatterns(new[] { this._optimizeKeyPattern }, this._getMetricLabels());

            if (matchingKeys.Count == 0)
            {
                throw new Exception($"None of the generated metrics match the specified optimization pattern: {_optimizeKeyPattern} in {Arrays.ToString(_getMetricLabels())}");
            }
            else if (matchingKeys.Count > 1)
            {
                throw new Exception($"The specified optimization pattern {_optimizeKeyPattern} matches more than one metric");
            }
            return matchingKeys.Single();
        }

        /// <summary>
        /// Returns:  A list of labels that correspond to metrics being computed
        /// </summary>
        /// <returns></returns>
        private string[] _getMetricLabels()
        {
            return __metricMgr.getMetricLabels();
        }

        /// <summary>
        /// Method which returns a dictionary of field statistics received from the input source.
        /// </summary>
        /// <returns>
        /// dict of dicts where the first level is the field name and 
        ///  the second level is the statistic. ie. fieldStats['pounds']['min']
        /// </returns>
        private Map<string, Map<string, object>> _getFieldStats()
        {
            Map<string, Map<string, object>> fieldStats = new Map<string, Map<string, object>>();
            var fieldNames = _inputSource.GetHeader().GetFieldNames();
            foreach (string field in fieldNames)
            {
                var curStats = new Map<string, object>();
                curStats["min"] = _inputSource.GetFieldMin(field);
                curStats["max"] = _inputSource.GetFieldMax(field);
                fieldStats[field] = curStats;
            }
            return fieldStats;
        }

        /// <summary>
        /// Creates and returns a PeriodicActivityMgr instance initialized with
        /// our periodic activities
        /// </summary>
        /// <returns></returns>
        protected PeriodicActivityMgr _initPeriodicActivities()
        {
            // Activity to update the metrics for this model in the models table
            var updateModelDBResults = new PeriodicActivityRequest { repeating = true, period = 100, cb = _updateModelDBResults };
            var updateJobResults = new PeriodicActivityRequest { repeating = true, period = 100, cb = __updateJobResultsPeriodic };
            var checkCancelation = new PeriodicActivityRequest { repeating = true, period = 100, cb = __checkCancelation };
            var checkMaturity = new PeriodicActivityRequest { repeating = true, period = 100, cb = __checkMaturity };

            // Do an initial update of the job record after 2 iterations to make
            // sure that it is populated with something without having to wait too long
            var updateJobResultsFirst = new PeriodicActivityRequest()
            {
                repeating = false,
                period = 2,
                cb = __updateJobResultsPeriodic
            };

            var periodicActivities = new List<PeriodicActivityRequest>
            {
                updateModelDBResults,
                updateJobResultsFirst,
                updateJobResults,
                checkCancelation
            };
            if (_isMaturityEnabled)
            {
                periodicActivities.Add(checkMaturity);
            }
            return new PeriodicActivityMgr(requestedActivities: periodicActivities);
        }

        /// <summary>
        /// Main loop of the OPF Model Runner.
        /// </summary>
        /// <param name="numIters"></param>
        /// <param name="learningOffAt">If not null, learning is turned off when we reach this iteration number</param>
        public void __runTaskMainLoop(int numIters, int? learningOffAt = null)
        {
            // Reset sequence states in the model, so it starts looking for a new
            // sequence
            _model.resetSequenceStates();
            _currentRecordIndex = -1;

            while (true)
            {
                // If killed by a terminator, stop running
                if (_isKilled) break;
                // If job stops or hypersearch ends, stop running
                if (_isCanceled) break;

                // If the process is about to be killed, set as orphaned
                // TODO
                //if (_isInterrupted.isSet())
                //{
                //    __setAsOrphaned();
                //    break;
                //}
                // If model is mature, stop running ONLY IF  we are not the best model
                // for the job. Otherwise, keep running so we can keep returning
                // predictions to the user
                if (_isMature)
                {
                    if (!_isBestModel)
                    {
                        _cmpReason = BaseClientJobDao.CMPL_REASON_STOPPED;
                        break;
                    }
                    else
                    {
                        _cmpReason = BaseClientJobDao.CMPL_REASON_EOF;
                    }

                    // Turn off learning
                    if (learningOffAt.HasValue && _currentRecordIndex == learningOffAt.Value)
                    {
                        _model.disableLearning();
                    }
                }

                // Read input record. Note that any failure here is a critical JOB failure
                // and results in the job being immediately canceled and marked as
                // failed. The runModelXXX code in hypesearch.utils, if it sees an
                // exception of type utils.JobFailException, will cancel the job and
                // copy the error message into the job record.
                Tuple<Map<string, object>, string[]> inputRecord;
                try
                {
                    inputRecord = _inputSource.GetNextRecordDict();
                    if (_currentRecordIndex < 0)
                    {
                        //_inputSource.SetTimeout(10);
                    }
                }
                catch (Exception e)
                {
                    throw new JobFailException("StreamReading", e);
                }

                if (inputRecord == null)
                {
                    // EOF
                    _cmpReason = BaseClientJobDao.CMPL_REASON_EOF;
                    break;
                }

                if (!inputRecord.Item1.Any())
                {
                    throw new InvalidOperationException("Got an empty record from FileSource");
                }

                // Process input record
                _currentRecordIndex += 1;

                var result = _model.run(inputRecord);

                // Compute metrics
                result.metrics = __metricMgr.update(result);
                // If there are None, use defaults. see MetricManager.getMetrics()
                // TODO remove this when  API server is gone
                if (result.metrics == null)
                    result.metrics = __metricMgr.GetMetrics();

                // Write the result to the output cache. Don't write encodings, if they were computed.
                if (result.inferences.ContainsKey(InferenceElement.Encodings))
                {
                    result.inferences.Remove(InferenceElement.Encodings);
                }
                result.sensorInput.dataEncodings = null;
                _writePrediction(result);

                // run periodic activities
                _periodic.Tick();

                if (numIters >= 0 && _currentRecordIndex >= numIters - 1)
                {
                    break;
                }
            }
        }

        /// <summary>
        /// Run final activities after a model has run. These include recording and
        /// logging the final score
        /// </summary>
        protected void _finalize()
        {
            _logger.Info($"Finished: modelID={_modelID}, {_currentRecordIndex + 1} records processed. Performing final activities");

            // =========================================================================
            // Dump the experiment metrics at the end of the task
            // =========================================================================
            _updateModelDBResults();

            // =========================================================================
            // Check if the current model is the best. Create a milestone if necessary
            // If the model has been killed, it is not a candidate for "best model",
            // and its output cache should be destroyed
            // =========================================================================
            if (!_isKilled)
            {
                __updateJobResults();
            }
            else
            {
                __deleteOutputCache(_modelID);
            }

            // =========================================================================
            // Close output stream, if necessary
            // =========================================================================
            //if (_predictionLogger != null)
            //    _predictionLogger.close();
        }

        /// <summary>
        /// Writes the results of one iteration of a model. The results are written to
        /// this ModelRunner's in-memory cache unless this model is the "best model" for
        /// the job.If this model is the "best model", the predictions are written out
        /// to a permanent store via a prediction output stream instance
        /// </summary>
        /// <param name="result">ModelResult object, which contains the input and output for this iteration</param>
        protected void _writePrediction(ModelResult result)
        {
            __predictionCache.Enqueue(result);
            if (_isBestModel)
            {
                __flushPredictionCache();
            }
        }

        /// <summary>
        /// Writes the contents of this model's in-memory prediction cache to a permanent
        /// store via the prediction output stream instance
        /// </summary>
        private void __flushPredictionCache()
        {
            if (__predictionCache == null) return;

            _logger.Error("OPF Model runner __flushPredictionCache not yet fully implemented!");
            //_logger.Error(Arrays.ToString(__predictionCache));
            //throw new NotImplementedException("flushing not yet implemented");
            __predictionCache.Clear();
        }

        /// <summary>
        /// Retrieves the current results and updates the model's record in the Model database.
        /// </summary>
        private void _updateModelDBResults()
        {
            // -----------------------------------------------------------------------
            // Get metrics
            Map<string, double?> metrics = _getMetrics();

            // -----------------------------------------------------------------------
            // Extract report metrics that match the requested report REs
            var reportDict = _reportMetricLabels.ToDictionary(k => k, k => metrics[k]);

            // -----------------------------------------------------------------------
            // Extract the report item that matches the optimize key RE
            // TODO cache optimizedMetricLabel sooner
            metrics = _getMetrics();
            Map<string, double?> optimizeDict = new Map<string, double?>();
            if (_optimizeKeyPattern != null)
            {
                optimizeDict[_optimizedMetricLabel] = metrics[_optimizedMetricLabel];
            }

            // -----------------------------------------------------------------------
            // Update model results
            string results = Json.Serialize(new Tuple(metrics, optimizeDict));

            _jobsDAO.modelUpdateResults(_modelID, results: results, metricValue: optimizeDict.Values.First(),
                numRecords: (uint)(_currentRecordIndex + 1));

            _logger.Debug($"Model Results: modelID={_modelID}; numRecords={_currentRecordIndex + 1}; results={results}");
        }

        protected virtual Map<string, double?> _getMetrics()
        {
            return __metricMgr.GetMetrics();
        }

        /// <summary>
        /// Check if this is the best model
        /// If so:
        ///     1) Write it's checkpoint
        ///     2) Record this model as the best
        ///     3) Delete the previous best's output cache
        /// Otherwise:
        ///     1) Delete our output cache
        /// </summary>
        private void __updateJobResults()
        {
            bool isSaved = false;
            while (true)
            {
                Tuple cbm = __checkIfBestCompletedModel();
                _isBestModel = (bool)cbm.Get(0);
                Map<string, object> jobResults = (Map<string, object>)cbm.Get(1);
                string jobResultsStr = (string)cbm.Get(2);

                // -----------------------------------------------------------------------
                // If the current model is the best:
                //   1) Save the model's predictions
                //   2) Checkpoint the model state
                //   3) Update the results for the job
                if (_isBestModel)
                {
                    // Save the current model and its results
                    if (!isSaved)
                    {
                        __flushPredictionCache();
                        _jobsDAO.modelUpdateTimestamp(_modelID);
                        __createModelCheckpoint();
                        _jobsDAO.modelUpdateTimestamp(_modelID);
                        isSaved = true;
                    }

                    // Now record the model as the best for the job
                    ulong? prevBest = TypeConverter.Convert<ulong?>(jobResults.Get("bestModel", null));
                    bool prevWasSaved = TypeConverter.Convert<bool>(jobResults.Get("saved", false));

                    // If the current model is the best, it shouldn't already be checkpointed
                    if (prevBest == _modelID)
                    {
                        Debug.Assert(!prevWasSaved);
                    }

                    var metrics = _getMetrics();

                    jobResults["bestModel"] = _modelID;
                    jobResults["bestValue"] = metrics[_optimizedMetricLabel];
                    jobResults["metrics"] = metrics;
                    jobResults["saved"] = true;

                    bool isUpdated = _jobsDAO.jobSetFieldIfEqual(_jobID, fieldName: "results", curValue: jobResultsStr,
                        newValue:
                            JsonConvert.SerializeObject(jobResults,
                                new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All }));
                    if (isUpdated)
                    {
                        if (prevWasSaved)
                        {
                            __deleteOutputCache(prevBest);
                            _jobsDAO.modelUpdateTimestamp(_modelID);
                            __deleteModelCheckpoint(prevBest);
                            _jobsDAO.modelUpdateTimestamp(_modelID);
                        }
                        _logger.Info($"Model {_modelID} chosen as best model.");
                        break;
                    }
                }
                // -----------------------------------------------------------------------
                // If the current model is not the best, delete its outputs
                else
                {
                    // NOTE: we update model timestamp around these occasionally-lengthy
                    // operations to help prevent the model from becoming orphaned
                    __deleteOutputCache(_modelID);
                    _jobsDAO.modelUpdateTimestamp(_modelID);
                    __deleteModelCheckpoint(_modelID);
                    _jobsDAO.modelUpdateTimestamp(_modelID);
                    break;
                }
            }
        }

        /// <summary>
        /// Delete the stored checkpoint for the specified modelID. This function is
        /// called if the current model is now the best model, making the old model's
        /// checkpoint obsolete
        /// </summary>
        /// <param name="prevBest"></param>
        private void __deleteModelCheckpoint(ulong? modelID)
        {
            var checkpointID = _jobsDAO.modelsGetFields(modelID.GetValueOrDefault(), new[] { "modelCheckpointId" }).model_checkpoint_id;

            if (checkpointID == null) return;

            try
            {
                // whot?  // TODO
                throw new NotImplementedException("wtf do i have to do here?");
            }
            catch (Exception)
            {
                _logger.Warn($"Failed to delete model checkpoint {checkpointID}. Assuming that another worker has already deleted it.");
                //return;
            }

            _jobsDAO.modelSetFields(modelID, new Map<string, object> { { "modelCheckpointId", null } }, ignoreUnchanged: true);
        }

        /// <summary>
        /// Create a checkpoint from the current model, and store it in a dir named
        /// after checkpoint GUID, and finally store the GUID in the Models DB
        /// </summary>
        private void __createModelCheckpoint()
        {
            if (_model == null || _modelCheckpointGUID == null) return;

            _jobsDAO.modelSetFields(_modelID, new Map<string, object> { { "modelCheckpointId", _modelCheckpointGUID } }, ignoreUnchanged: true);

            _logger.Error("The model has not been saved yet in the OPF Model runner! (partially implemented)");
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Reads the current "best model" for the job and returns whether or not the
        /// current model is better than the "best model" stored for the job
        /// </summary>
        /// <remarks>
        /// isBetter:
        ///     True if the current model is better than the stored "best model"
        /// storedResults:
        ///     A dict of the currently stored results in the jobs table record
        /// origResultsStr:
        ///     The json-encoded string that currently resides in the "results" field
        ///     of the jobs record(used to create atomicity)
        /// </remarks>
        /// <returns>(isBetter, storedBest, origResultsStr)</returns>
        private Tuple __checkIfBestCompletedModel()
        {
            string jobResultsStr = _jobsDAO.jobGetFields(_jobID, new[] { "results" })[0] as string;

            Map<string, object> jobResults = null;
            if (jobResultsStr == null)
            {
                jobResults = new Map<string, object>();
            }
            else
            {
                jobResults = JsonConvert.DeserializeObject<Map<string, object>>(jobResultsStr, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
            }
            bool isSaved = (bool)jobResults.Get("saved", false);
            double? bestMetric = (double?)jobResults.Get("bestValue", null);

            var currentMetric = _getMetrics()[_optimizedMetricLabel];
            _isBestModel = !isSaved || (currentMetric < bestMetric);

            return new Tuple(_isBestModel, jobResults, jobResultsStr);
        }

        /// <summary>
        /// Delete's the output cache associated with the given modelID. This actually
        /// clears up the resources associated with the cache, rather than deleting al
        /// the records in the cache
        /// </summary>
        /// <param name="modelId">The id of the model whose output cache is being deleted</param>
        private void __deleteOutputCache(ulong? modelId)
        {
            if (modelId == _modelID /*&& _predictionLogger != null*/)
            {
                //_predictionLogger.Close();
                __predictionCache = null;
                // _predictionLogger = null;
            }

            _logger.Error("__deleteOutputCache not implemented fully");
            //throw new NotImplementedException();
        }

        /// <summary>
        /// Periodic check to see if this is the best model. This should only have an 
        /// effect if this is the* first* model to report its progress
        /// </summary>
        private void __updateJobResultsPeriodic()
        {
            if (_isBestModelStored && !_isBestModel)
            {
                return;
            }

            while (true)
            {
                string jobResultsStr = _jobsDAO.jobGetFields(_jobID, new[] { "results" })[0] as string;
                Map<string, object> jobResults;
                if (jobResultsStr == null)
                {
                    jobResults = new Map<string, object>();
                }
                else
                {
                    _isBestModelStored = true;
                    if (!_isBestModel) return;
                    jobResults = JsonConvert.DeserializeObject<Map<string, object>>(jobResultsStr,
                        new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                }

                ulong? bestModel = (ulong?)TypeConverter.Convert<ulong>(jobResults.Get("bestModel", null));
                var bestMetric = jobResults.Get("bestValue", null) == null ? (double?)null : (double?)TypeConverter.Convert<double>(jobResults.Get("bestValue", null));
                bool isSaved = (bool)jobResults.Get("saved", false);

                // If there is a best model, and it is not the same as the current model
                // we should wait till we have processed all of our records to see if
                // we are the the best
                if ((bestModel != null) && (_modelID != bestModel))
                {
                    _isBestModel = false;
                    return;
                }

                // Make sure prediction output stream is ready before we present our model
                // as "bestModel"; sometimes this takes a long time, so update the model's
                // timestamp to help avoid getting orphaned
                __flushPredictionCache();
                _jobsDAO.modelUpdateTimestamp(_modelID);

                var metrics = _getMetrics();

                jobResults["bestModel"] = _modelID;
                jobResults["bestValue"] = metrics[_optimizedMetricLabel];
                jobResults["metrics"] = metrics;
                jobResults["saved"] = false;

                var newResults = JsonConvert.SerializeObject(jobResults, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });
                bool isUpdated = _jobsDAO.jobSetFieldIfEqual(_jobID,
                    fieldName: "results",
                    curValue: jobResultsStr,
                    newValue: newResults);
                if (isUpdated || (!isUpdated && newResults == jobResultsStr))
                {
                    _isBestModel = true;
                    break;
                }
            }
        }

        private void __checkCancelation()
        {
            _logger.Error("__checkCancelation not implemented");
            //throw new NotImplementedException();
        }

        private void __checkMaturity()
        {
            _logger.Error("__checkMaturity not implemented");
            //throw new NotImplementedException();
        }
    }

    /// <summary>
    /// This class runs a 'dummy' OPF Experiment. It will periodically update the
    /// models db with a deterministic metric value.It can also simulate different
    /// amounts of computation time
    /// </summary>
    public class OpfDummyModelRunner : OpfModelRunner
    {
        public static StreamDef DUMMY_STREAMDEF = new StreamDef
        {
            version = 1,
            info = "test_NoProviders",
            streams = new[]
            {
                new StreamDef.StreamItem
                {
                    source = "joined_mosman_2011.csv",
                    info = "hotGym.csv",
                    columns = new[] {"*"}
                }
            },
            aggregation = new AggregationSettings
            {
                hours = 1,
                fields = new Map<string, object>
                {
                    {"consumption", "sum" },
                    {"timestamp", "first" },
                    {"TEMP", "mean" },
                    {"DEWP", "mean" },
                    {"MAX", "mean" },
                    {"MIN", "mean" },
                    {"PRCP", "sum" },
                }
            }
        };

        private static Func<int, double?>[] staticMetrics = new Func<int, double?>[]
        {
            x=> x+1.0,
            x=>100.0-x-1,
            x=>20.0*Math.Sin(x),
            x=> Math.Pow( x/9.0, 2)
        };

        private static IRandom random = new XorshiftRandom(42);

        private DummyModelParameters _params;
        private StreamDef _streamDef;
        private static int staticModelIndex = 0;
        private int modelIndex = 0;
        private double? _busyWaitTime;
        private int _iterations;
        private bool? _doFinalize;
        private double? _delay;
        private string _sleepModelRange;
        private bool _makeCheckpoint;
        private double? _finalDelay;
        private int? _exitAfter;
        private Func<int, double?> metrics;
        private double? metricValue;
        private int[] _sysExitModelRange;
        private int[] _delayModelRange;
        private int[] _errModelRange;
        private bool _jobFailErr;
        private List<FieldMetaInfo> __fieldInfo;
        private double? randomizeWait;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="modelID">ID of this model in the models table</param>
        /// <param name="jobID"></param>
        /// <param name="params">a dictionary of parameters for this dummy model.</param>
        /// <param name="predictedField">Name of the input field for which this model is being optimized</param>
        /// <param name="reportKeyPatterns">list of items from the results dict to include in the report.These can be regular expressions.</param>
        /// <param name="optimizeKeyPattern">Which report item, if any, we will be optimizing for.
        /// This can also be a regular expression, but is an error if it matches more than one key from the experiment's results.</param>
        /// <param name="jobsDAO">Jobs data access object - the interface to the jobs database which has the model's table.</param>
        /// <param name="modelCheckpointGUID">A persistent, globally-unique identifier for constructing the model checkpoint key</param>
        /// <param name="predictionCacheMaxRecords">Maximum number of records for the prediction output cache. Pass None for the default value.</param>
        public OpfDummyModelRunner(ulong? modelID, uint? jobID, DummyModelParameters @params, string predictedField,
            string[] reportKeyPatterns, string optimizeKeyPattern, BaseClientJobDao jobsDAO,
            string modelCheckpointGUID, int? predictionCacheMaxRecords = null)
            : base(modelID, jobID, predictedField, null, reportKeyPatterns, optimizeKeyPattern, jobsDAO, modelCheckpointGUID, null)
        {
            _predictionCacheMaxRecords = predictionCacheMaxRecords;
            _streamDef = DUMMY_STREAMDEF.Clone();
            _params = new DummyModelParameters(); // default values are initialized

            // -----------------------------------------------------------------------
            // Read the index of the current model in the test
            if (@params.permutationParams != null && @params.permutationParams.__model_num.HasValue)
            {
                modelIndex = @params.permutationParams.__model_num.Value;
            }
            else
            {
                modelIndex = staticModelIndex;
                staticModelIndex += 1;
            }
            // -----------------------------------------------------------------------
            _loadDummyModelParameters(@params);

            // -----------------------------------------------------------------------
            // Load parameters into instance variables
            // -----------------------------------------------------------------------
            _logger.Debug($"Using Dummy model params {_params}");

            this._busyWaitTime = _params.waitTime;
            this._iterations = _params.iterations;
            this._doFinalize = _params.finalize;
            this._delay = _params.delay;
            this._sleepModelRange = _params.sleepModelRange;
            this._makeCheckpoint = _params.makeCheckpoint;
            this._finalDelay = _params.finalDelay;
            this._exitAfter = _params.exitAfter;

            // -----------------------------------------------------------------------
            // Randomize Wait time, if necessary
            // -----------------------------------------------------------------------
            this.randomizeWait = _params.randomizeWait;
            if (_busyWaitTime.HasValue)
            {
                __computeWaitTime();
            }

            // -----------------------------------------------------------------------
            // Load the appropriate metric value or metric function
            // -----------------------------------------------------------------------
            if (_params.metricFunctions != null && _params.metricValue.HasValue)
            {
                throw new InvalidOperationException("Error, only 1 of 'metricFunctions' or 'metricValue' can be passed to OPFDummyModelRunner params");
            }
            metrics = null;
            metricValue = null;
            if (_params.metricFunctions != null)
            {
                metrics = _params.metricFunction;
            }
            else if (_params.metricValue.HasValue)
            {
                metricValue = _params.metricValue.Value;
            }
            else
            {
                metrics = staticMetrics[0];
            }
            // -----------------------------------------------------------------------
            // Create an OpfExperiment instance, if a directory is specified
            // -----------------------------------------------------------------------
            if (_params.experimentDirectory != null)
            {
                _model = __createModel(_params.experimentDirectory);
                __fieldInfo = _model.getFieldInfo();
            }

            // -----------------------------------------------------------------------
            // Get the sysExit model range
            // -----------------------------------------------------------------------
            if (_params.sysExitModelRange != null)
            {
                _sysExitModelRange = _params.sysExitModelRange.Split(',').Select(int.Parse).ToArray();
            }
            // -----------------------------------------------------------------------
            // Get the delay model range
            // -----------------------------------------------------------------------
            if (_params.delayModelRange != null)
            {
                _delayModelRange = _params.delayModelRange.Split(',').Select(int.Parse).ToArray();
            }
            // -----------------------------------------------------------------------
            // Get the errModel range
            // -----------------------------------------------------------------------
            if (_params.errModelRange != null)
            {
                _errModelRange = _params.errModelRange.Split(',').Select(int.Parse).ToArray();
            }
            _computModelDelay();

            // Get the jobFailErr boolean
            _jobFailErr = _params.jobFailErr;

            _logger.Debug($"Dummy Model {_modelID} params {_params}");
        }

        /// <summary>
        /// Loads all the parameters for this dummy model. For any parameters
        /// specified as lists, read the appropriate value for this model using the model index
        /// </summary>
        /// <param name="params"></param>
        public void _loadDummyModelParameters(DummyModelParameters @params)
        {
            _params = @params.Clone();
            if (@params.metricFunctions != null)
            {
                int index = modelIndex % @params.metricFunctions.Count;
                _params.metricFunction = @params.metricFunctions[index];
            }
        }

        /// <summary>
        /// Computes the amount of time (if any) to delay the run of this model.
        /// This can be determined by two mutually exclusive parameters:
        ///     delay and sleepModelRange.
        /// 
        /// 'delay' specifies the number of seconds a model should be delayed.If a list
        /// is specified, the appropriate amount of delay is determined by using the
        /// model's modelIndex property.
        /// 
        /// However, this doesn't work when testing orphaned models, because the
        /// modelIndex will be the same for every recovery attempt.Therefore, every
        /// recovery attempt will also be delayed and potentially orphaned.
        /// 
        /// 'sleepModelRange' doesn't use the modelIndex property for a model, but rather
        /// sees which order the model is in the database, and uses that to determine
        /// whether or not a model should be delayed.
        /// </summary>
        private void _computModelDelay()
        {
            // 'delay' and 'sleepModelRange' are mutually exclusive
            if (_params.delay.HasValue && _params.sleepModelRange != null)
            {
                throw new InvalidOperationException("Only one of 'delay' or 'sleepModelRange' may be specified");
            }

            // Get the sleepModel range
            if (_sleepModelRange != null)
            {
                string[] parts = _sleepModelRange.Split(':');
                var range = parts[0].Split(',').Select(int.Parse).ToList();
                double delay = double.Parse(parts[1]);
                List<ulong> modelIDs = _jobsDAO.jobGetModelIDs(_jobID);
                modelIDs.Sort();

                range[1] = Math.Min(range[1], modelIDs.Count);
                // if the model is in range, add the delay
                if (modelIDs.Skip(range[0]).Take(range[1] - range[0]).Contains(_modelID.GetValueOrDefault()))
                {
                    _delay = delay;
                }
            }
            else
            {
                _delay = _params.delay;
            }
        }

        private int _iterToolsCount = 0;
        private IEnumerable<int> IterToolsCount()
        {
            yield return _iterToolsCount++;
        }

        /// <summary>
        /// Runs the given OPF task against the given Model instance
        /// </summary>
        public override ModelCompletionStatus run()
        {
            _logger.Debug($"Starting Dummy Model: modelID={_modelID};");
            // =========================================================================
            // Initialize periodic activities (e.g., for model result updates)
            // =========================================================================
            var periodic = _initPeriodicActivities();

            _optimizedMetricLabel = _optimizeKeyPattern;
            _reportMetricLabels = new List<string> { _optimizeKeyPattern };

            // =========================================================================
            // Create our top-level loop-control iterator
            // =========================================================================
            IEnumerator<int> iterTracker;
            if (_iterations >= 0)
            {
                iterTracker = ArrayUtils.XRange(0, _iterations, 1).GetEnumerator();
            }
            else
            {
                iterTracker = IterToolsCount().GetEnumerator();
            }

            // =========================================================================
            // This gets set in the unit tests. It tells the worker to sys exit
            // the first N models. This is how we generate orphaned models
            bool doSysExit = false;
            if (_sysExitModelRange != null)
            {
                var modelAndCounters = _jobsDAO.modelsGetUpdateCounters(_jobID);
                var modelIDs = modelAndCounters.Select(x => (ulong?)x.Item1).ToList();
                modelIDs.Sort();
                int beg = _sysExitModelRange[0];
                int end = _sysExitModelRange[1];
                if (modelIDs.Skip(beg).Take(end - beg).Contains(_modelID))
                {
                    doSysExit = true;
                }
            }
            if (_delayModelRange != null)
            {
                var modelAndCounters = _jobsDAO.modelsGetUpdateCounters(_jobID);
                var modelIDs = modelAndCounters.Select(x => (ulong?)x.Item1).ToList();
                modelIDs.Sort();
                int beg = _delayModelRange[0];
                int end = _delayModelRange[1];
                if (modelIDs.Skip(beg).Take(end - beg).Contains(_modelID))
                {
                    Thread.Sleep(10);
                }
            }
            if (_errModelRange != null)
            {
                var modelAndCounters = _jobsDAO.modelsGetUpdateCounters(_jobID);
                var modelIDs = modelAndCounters.Select(x => (ulong?)x.Item1).ToList();
                modelIDs.Sort();
                int beg = _errModelRange[0];
                int end = _errModelRange[1];
                if (modelIDs.Skip(beg).Take(end - beg).Contains(_modelID))
                {
                    throw new InvalidOperationException("Exiting with error due to errModelRange parameter");
                }
            }

            // =========================================================================
            // Delay, if necessary
            if (_delay.HasValue)
            {
                Thread.Sleep((int)(_delay * 1000));
            }

            // =========================================================================
            // Run it!
            // =========================================================================
            _currentRecordIndex = 0;
            while (true)
            {
                // =========================================================================
                // Check if the model should be stopped
                // =========================================================================

                // If killed by a terminator, stop running
                if (_isKilled)
                    break;

                // If job stops or hypersearch ends, stop running
                if (_isCanceled)
                    break;

                // If model is mature, stop running ONLY IF  we are not the best model
                // for the job. Otherwise, keep running so we can keep returning
                // predictions to the user
                if (_isMature)
                {
                    if (!_isBestModel)
                    {
                        _cmpReason = BaseClientJobDao.CMPL_REASON_STOPPED;
                        break;
                    }
                }
                else
                {
                    _cmpReason = BaseClientJobDao.CMPL_REASON_EOF;
                }

                // =========================================================================
                // Get the the next record, and "write it"
                // =========================================================================
                try
                {
                    bool next = iterTracker.MoveNext();
                    if (!next) break;   // stopiteration
                    _currentRecordIndex = iterTracker.Current;
                }
                catch (Exception e)
                {
                    _logger.Error("Issue with iterTracker -> " + e);
                    break;
                }

                // "Write" a dummy output value. This is used to test that the batched
                // writing works properly
                _writePrediction(new ModelResult(null, null, null, null));

                periodic.Tick();

                // =========================================================================
                // Compute wait times. See if model should exit
                // =========================================================================
                if (__shouldSysExit(_currentRecordIndex))
                {
                    break;
                }
                // Simulate computation time
                if (_busyWaitTime.HasValue)
                {
                    Thread.Sleep((int)(_busyWaitTime.Value * 1000));
                    __computeWaitTime();
                }

                // Asked to abort after so many iterations?
                if (doSysExit)
                {
                    break;
                }

                if (_jobFailErr)
                {
                    throw new JobFailException("dummyModel's jobFailErr was True.", null);
                }
            }

            // =========================================================================
            // Handle final operations
            // =========================================================================
            if (_doFinalize.GetValueOrDefault())
            {
                if (!_makeCheckpoint)
                {
                    _model = null;
                }
                // Delay finalisation operation
                Thread.Sleep((int)(_finalDelay.GetValueOrDefault() * 1000));
                _finalize();
            }

            _logger.Info($"Finished: modelID={_modelID}");
            return new ModelCompletionStatus(_cmpReason, null);
        }

        private void __computeWaitTime()
        {
            if (randomizeWait.HasValue)
            {
                _busyWaitTime = random.NextDouble((1.0 - randomizeWait.Value) * _busyWaitTime.GetValueOrDefault(),
                    (1.0 + randomizeWait.Value) * _busyWaitTime.GetValueOrDefault());
            }
        }

        private opf.Model __createModel(ExperimentParameters expDir)
        {
            return ModelFactory.Create(expDir);
        }

        /// <summary>
        /// Checks to see if the model should exit based on the exitAfter dummy parameter
        /// </summary>
        /// <param name="iteration"></param>
        /// <returns></returns>
        private bool __shouldSysExit(int? iteration)
        {
            if (!_exitAfter.HasValue || iteration < _exitAfter.Value)
            {
                return false;
            }

            var results = _jobsDAO.modelsGetFieldsForJob(_jobID, new[] { "params" });

            var modelIDs = results.Select(e => e.Item1).ToArray();
            var modelNums = results.Select(e => Json.Deserialize<ExperimentPermutationParameters>(e.Item2[0] as string).__model_num).ToArray();

            var sameModelNumbers = ArrayUtils.Zip(modelIDs, modelNums).Where(x => (int)x.Get(1) == modelIndex).ToList();
            ulong? firstModelId = sameModelNumbers.Min(m => (ulong?)m.Get(0));

            return firstModelId == _modelID;
        }
        /// <summary>
        /// Protected function that can be overridden by subclasses. Its main purpose
        /// is to allow the the OPFDummyModelRunner to override this with deterministic
        /// values
        /// </summary>
        /// <returns>All the metrics being computed for this model</returns>
        protected override Map<string, double?> _getMetrics()
        {
            double? metric = null;
            if (metrics != null)
            {
                metric = metrics(_currentRecordIndex.Value + 1);
            }
            else if (metricValue.HasValue)
            {
                metric = metricValue;
            }
            else
            {
                throw new IOException("No metrics or metric value specified for dummy model");
            }
            return new Map<string, double?> { { _optimizeKeyPattern, metric } };
        }
    }

    [Serializable]
    public class DummyModelParameters
    {
        /// <summary>
        /// OPTIONAL-This specifies the amount of time (in seconds) that the experiment should wait
        /// before STARTING to process records.This is useful for simulating workers that start/end at different times
        /// </summary>
        public double? delay { get; set; }
        /// <summary>
        /// OPTIONAL-This specifies the amount of time (in seconds) that the experiment should wait
        /// before it conducts its finalization operations. 
        /// These operations include checking if the model is the best model, and writing out checkpoints.
        /// </summary>
        public double? finalDelay { get; set; }
        /// <summary>
        /// OPTIONAL-The amount of time (in seconds) to wait in a busy loop to simulate computation time on EACH ITERATION
        /// </summary>
        public double? waitTime { get; set; }
        /// <summary>
        /// OPTIONAL-([0.0-1.0] ). Default:None
        /// If set to a value, the above specified
        /// wait time will be randomly be dithered by +/- <randomizeWait>% of the specfied value.
        /// For example, if randomizeWait= 0.2, the wait time will be dithered by +/- 20% of its value.
        /// </summary>
        public double? randomizeWait { get; set; }
        /// <summary>
        /// OPTIONAL-How many iterations to run the model for. -1 means run forever(default=1)
        /// </summary>
        public int iterations { get; set; } = 1;
        /// <summary>
        /// OPTIONAL-A list of single argument functions
        /// serialized as strings, which return the metric value given the record number. (index of list)
        /// 
        /// Mutually exclusive with metricValue
        /// </summary>
        public List<Func<int, double?>> metricFunctions { get; set; }
        public Func<int, double?> metricFunction { get; set; }
        /// <summary>
        /// OPTIONAL-A single value to use for the metric
        /// value (used to debug hypersearch).
        /// 
        /// Mutually exclusive with metricFunctions
        /// </summary>
        public double? metricValue { get; set; }
        /// <summary>
        ///  OPTIONAL-(True/False). Default:True
        /// When False, this will prevent the model from recording it's metrics and performing other
        /// functions that it usually performs after the model has finished running
        /// </summary>
        public bool? finalize { get; set; } = true;
        /// <summary>
        /// A dict containing the instances of all the variables being permuted over
        /// </summary>
        public ExperimentPermutationParameters permutationParams { get; set; }
        /// <summary>
        /// REQUIRED-An absolute path to a directory with a valid description.py file.
        /// NOTE: This does not actually affect the running of the model or the metrics
        /// produced. It is required to create certain objects (such as the output stream)
        /// </summary>
        public ExperimentParameters experimentDirectory { get; set; }
        /// <summary>
        /// True to actually write a checkpoint out to disk (default: False)
        /// </summary>
        public bool makeCheckpoint { get; set; }
        /// <summary>
        /// A string containing two integers 'firstIdx,endIdx'. When present, 
        /// if we are running the firstIdx'th model up to but not including the
        /// endIdx'th model, then do a sys.exit() while running the model.
        /// This causes the worker to exit, simulating an orphaned model.
        /// </summary>
        public string sysExitModelRange { get; set; }
        /// <summary>
        /// A string containing two integers 'firstIdx,endIdx'. When present, 
        /// if we are running the firstIdx'th model up to but not including the
        /// endIdx'th model, then do a delay of 10 sec. while running the model.
        /// This causes the worker to run slower and for some other worker to think the model should be orphaned.
        /// </summary>
        public string delayModelRange { get; set; }
        /// <summary>
        /// The number of iterations after which the model should perform a sys exit.
        /// This is an alternative way of creating an orphaned model that use's the dummmy model's modelIndex instead of the modelID
        /// </summary>
        public int? exitAfter { get; set; }
        /// <summary>
        /// A string containing two integers 'firstIdx,endIdx'. When present, 
        /// if we are running the firstIdx'th model up to but not including the
        /// endIdx'th model, then raise an exception while running the model.
        /// This causes the model to fail with a CMPL_REASON_ERROR reason
        /// </summary>
        public string errModelRange { get; set; }
        /// <summary>
        /// A string containing 3 integers 'firstIdx,endIdx: delay'. 
        /// When present, if we are running the firstIdx'th model up to but not including
        /// the endIdx'th model, then sleep for delay seconds at the beginning of the run.
        /// </summary>
        public string sleepModelRange { get; set; }
        /// <summary>
        /// If true, model will raise a JobFailException
        /// which should cause the job to be marked as failed and immediately cancel all other workers.
        /// </summary>
        public bool jobFailErr { get; set; }

        public DummyModelParameters Clone()
        {
            BinaryFormatter formatter = new BinaryFormatter();
            MemoryStream ms = new MemoryStream();
            formatter.Serialize(ms, this);
            ms.Position = 0;
            DummyModelParameters obj = (DummyModelParameters)formatter.Deserialize(ms);
            return obj;
        }
    }

    public class PeriodicActivityRequest
    {
        public bool repeating;
        public int period;
        public Action cb;

        public IEnumerator<int>[] iterationHolder;
    }

    public class PeriodicActivityMgr
    {
        private List<PeriodicActivityRequest> __activities;

        public PeriodicActivityMgr(List<PeriodicActivityRequest> requestedActivities)
        {
            this.__activities = new List<PeriodicActivityRequest>();
            AppendActivities(requestedActivities);
        }

        /// <summary>
        /// Activity tick handler; services all activities
        /// </summary>
        /// <returns>True if controlling iterator says it's okay to keep going; False to stop</returns>
        public bool Tick()
        {
            // Run activities whose time has come
            foreach (var act in __activities)
            {
                if (act.iterationHolder[0] == null)
                {
                    continue;
                }

                try
                {
                    bool moved = act.iterationHolder[0].MoveNext();
                    if (!moved) throw new InvalidOperationException("cannot move cursor");
                }
                catch (Exception e)
                {
                    act.cb();
                    if (act.repeating)
                    {
                        act.iterationHolder[0] = ArrayUtils.XRange(0, act.period - 1, 1).GetEnumerator();
                    }
                    else
                    {
                        act.iterationHolder[0] = null;
                    }
                }
            }
            return true;
        }

        private void AppendActivities(List<PeriodicActivityRequest> periodicActivities)
        {
            foreach (PeriodicActivityRequest req in periodicActivities)
            {
                PeriodicActivityRequest act = new PeriodicActivityRequest
                {
                    repeating = req.repeating,
                    period = req.period,
                    cb = req.cb,
                    iterationHolder = new[] { ArrayUtils.XRange(0, req.period - 1, 1).GetEnumerator() }
                };
                __activities.Add(act);
            }
        }
    }
}