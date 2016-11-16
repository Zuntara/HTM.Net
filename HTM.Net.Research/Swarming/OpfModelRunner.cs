using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Data;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
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
        private ILog _logger = LogManager.GetLogger(typeof(OpfModelRunner));
        // The minimum number of records that need to have been read for this model
        // to be a candidate for 'best model'
        int? _MIN_RECORDS_TO_BE_BEST = null;

        // The number of points we look at when trying to figure out whether or not a
        // model has matured
        int? _MATURITY_NUM_POINTS = null;

        // The maximum rate of change in the model's metric for it to be considered 'mature'
        double? _MATURITY_MAX_CHANGE = null;
        private ulong? _modelID;
        private uint? _jobID;
        private string _predictedField;
        private IDescription _experimentDir;
        private string[] _reportKeyPatterns;
        private string _optimizeKeyPattern;
        private BaseClientJobDao _jobsDAO;
        private string _modelCheckpointGUID;
        private int? _predictionCacheMaxRecords;
        private bool _isMaturityEnabled;
        private string _optimizedMetricLabel;
        private string _cmpReason;
        private DescriptionControlModel _modelControl;
        private opf.Model _model;
        private MetricsManager __metricMgr;
        private BatchedCsvStream<string[]> _inputSource;
        private string[] __loggedMetricPatterns;
        private bool _isBestModel;
        private bool _isBestModelStored;
        private List<string> _reportMetricLabels;
        private PeriodicActivityMgr _periodic;
        private int? _currentRecordIndex;
        private bool _isKilled;
        private bool _isCanceled;
        private bool _isMature;
        private Queue<object> __predictionCache;

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
        public OpfModelRunner(ulong? modelID, uint? jobID, string predictedField, IDescription experimentDir,
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
            this.__predictionCache = new Queue<object>();

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
        public ModelCompletionStatus run()
        {
            // Load experiments description module
            var modelDescription = _experimentDir.modelConfig;
            this._modelControl = _experimentDir.control;

            // -----------------------------------------------------------------------
            // Create the input data stream for this task
            var streamDef = this._modelControl.dataset;

            string fileName = ((Map<string, object>)streamDef["streams"])["source"] as string;

            IStream<string> fileStream = new Stream<string>(YieldingFileReader.ReadAllLines(fileName, Encoding.UTF8));
            _inputSource = BatchedCsvStream<string>.Batch(fileStream, 20, false, 3);

            // -----------------------------------------------------------------------
            // Get field statistics from the input source
            var fieldStats = this._getFieldStats();
            // -----------------------------------------------------------------------
            // Construct the model instance
            this._model = ModelFactory.Create(_experimentDir /*modelDescription*/);
            this._model.setFieldStatistics(fieldStats);
            this._model.enableLearning();
            this._model.enableInference(this._modelControl.inferenceArgs);

            // -----------------------------------------------------------------------
            // Instantiate the metrics
            this.__metricMgr = new MetricsManager(this._modelControl.metrics,
                                              this._model.getFieldInfo(),
                                              this._model.getInferenceType());

            this.__loggedMetricPatterns = this._modelControl.loggedMetrics ?? new string[0];

            this._optimizedMetricLabel = this.__getOptimizedMetricLabel();
            this._reportMetricLabels = ArrayUtils.MatchPatterns(this._reportKeyPatterns, this._getMetricLabels());


            // -----------------------------------------------------------------------
            // Initialize periodic activities (e.g., for model result updates)
            this._periodic = this._initPeriodicActivities();

            // -----------------------------------------------------------------------
            // Create our top-level loop-control iterator
            int numIters = this._modelControl.iterationCount.GetValueOrDefault(-1);

            // Are we asked to turn off learning for a certain // of iterations near the
            //  end?
            int? learningOffAt = null;
            int iterationCountInferOnly = this._modelControl.iterationCountInferOnly.GetValueOrDefault();
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
        private PeriodicActivityMgr _initPeriodicActivities()
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
                Map<string, object> inputRecord;
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

                if (!inputRecord.Any())
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
        private void _finalize()
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
        private void _writePrediction(ModelResult result)
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

            _logger.Error("OPF Model runner __flushPredictionCache not yet implemented!");
            _logger.Error(Arrays.ToString(__predictionCache));
            //throw new NotImplementedException("flushing not yet implemented");
        }

        /// <summary>
        /// Retrieves the current results and updates the model's record in the Model database.
        /// </summary>
        private void _updateModelDBResults()
        {
            // -----------------------------------------------------------------------
            // Get metrics
            var metrics = _getMetrics();

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

        private Map<string, double?> _getMetrics()
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
                    ulong? prevBest = (ulong?)jobResults.Get("bestModel", null);
                    bool prevWasSaved = (bool)jobResults.Get("saved", false);

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
            _logger.Error("The model has not been saved yet in the OPF Model runner!");
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
            _logger.Error("__deleteOutputCache not implemented");
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

                ulong? bestModel = (ulong?) TypeConverter.Convert<ulong>(jobResults.Get("bestModel", null));
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

                var newResults = JsonConvert.SerializeObject(jobResults, new JsonSerializerSettings {TypeNameHandling = TypeNameHandling.All});
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

    public class JobFailException : Exception
    {
        public JobFailException(string message, Exception exception)
            : base(message, exception)
        {

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

    /*



  def run(self)
  {
    
    // -----------------------------------------------------------------------
    // Load the experiment's description.py module
    descriptionPyModule = opfhelpers.loadExperimentDescriptionScriptFromDir(
      this._experimentDir);
    expIface = opfhelpers.getExperimentDescriptionInterfaceFromModule(
      descriptionPyModule);
    expIface.normalizeStreamSources();

    modelDescription = expIface.getModelDescription();
    this._modelControl = expIface.getModelControl();

    // -----------------------------------------------------------------------
    // Create the input data stream for this task
    streamDef = this._modelControl['dataset'];

    from nupic.data.stream_reader import StreamReader;
    readTimeout = 0;

    this._inputSource = StreamReader(streamDef, isBlocking=False,
                                     maxTimeout=readTimeout);


    // -----------------------------------------------------------------------
    // Get field statistics from the input source
    fieldStats = this._getFieldStats();
    // -----------------------------------------------------------------------
    // Construct the model instance
    this._model = ModelFactory.create(modelDescription);
    this._model.setFieldStatistics(fieldStats);
    this._model.enableLearning();
    this._model.enableInference(this._modelControl.get("inferenceArgs", None));

    // -----------------------------------------------------------------------
    // Instantiate the metrics
    this.__metricMgr = MetricsManager(this._modelControl.get('metrics',None),
                                      this._model.getFieldInfo(),
                                      this._model.getInferenceType());

    this.__loggedMetricPatterns = this._modelControl.get("loggedMetrics", []);

    this._optimizedMetricLabel = this.__getOptimizedMetricLabel();
    this._reportMetricLabels = matchPatterns(this._reportKeyPatterns,
                                              this._getMetricLabels());


    // -----------------------------------------------------------------------
    // Initialize periodic activities (e.g., for model result updates)
    this._periodic = this._initPeriodicActivities();

    // -----------------------------------------------------------------------
    // Create our top-level loop-control iterator
    numIters = this._modelControl.get('iterationCount', -1);

    // Are we asked to turn off learning for a certain // of iterations near the
    //  end?
    learningOffAt = None;
    iterationCountInferOnly = this._modelControl.get('iterationCountInferOnly', 0);
    if( iterationCountInferOnly == -1)
    {
      this._model.disableLearning();
    }
    else if( iterationCountInferOnly > 0)
    {
      assert numIters > iterationCountInferOnly, "when iterationCountInferOnly " \
        "is specified, iterationCount must be greater than " \
        "iterationCountInferOnly.";
      learningOffAt = numIters - iterationCountInferOnly;
    }

    this.__runTaskMainLoop(numIters, learningOffAt=learningOffAt);

    // -----------------------------------------------------------------------
    // Perform final operations for model
    this._finalize();

    return (this._cmpReason, None);
  }


  def __runTaskMainLoop(self, numIters, learningOffAt=None)
  {
    """ Main loop of the OPF Model Runner.

    Parameters:
    -----------------------------------------------------------------------

    recordIterator:    Iterator for counting number of records (see _runTask)
    learningOffAt:     If not None, learning is turned off when we reach this
                        iteration number

    """;

    // // Reset sequence states in the model, so it starts looking for a new
    // // sequence
    this._model.resetSequenceStates();

    this._currentRecordIndex = -1;
    while( True)
    {

      // If killed by a terminator, stop running
      if( this._isKilled)
      {
        break;
      }

      // If job stops or hypersearch ends, stop running
      if( this._isCanceled)
      {
        break;
      }

      // If the process is about to be killed, set as orphaned
      if( this._isInterrupted.isSet())
      {
        this.__setAsOrphaned();
        break;
      }

      // If model is mature, stop running ONLY IF  we are not the best model
      // for the job. Otherwise, keep running so we can keep returning
      // predictions to the user
      if( this._isMature)
      {
        if( not this._isBestModel)
        {
          this._cmpReason = this._jobsDAO.CMPL_REASON_STOPPED;
          break;
        }
        else
        {
          this._cmpReason = this._jobsDAO.CMPL_REASON_EOF;
        }
      }

      // Turn off learning?
      {
        if( learningOffAt is not None \
                  and this._currentRecordIndex == learningOffAt)
        {
          this._model.disableLearning();
        }
      }

      // Read input record. Note that any failure here is a critical JOB failure
      //  and results in the job being immediately canceled and marked as
      //  failed. The runModelXXX code in hypesearch.utils, if it sees an
      //  exception of type utils.JobFailException, will cancel the job and
      //  copy the error message into the job record.
      try
      {
        inputRecord = this._inputSource.getNextRecordDict();
        if( this._currentRecordIndex < 0)
        {
          this._inputSource.setTimeout(10);
        }
      }
      catch( Exception, e)
      {
        raise utils.JobFailException(ErrorCodes.streamReading, str(e.args),
                                     traceback.format_exc());
      }

      if( inputRecord is None)
      {
        // EOF
        this._cmpReason = this._jobsDAO.CMPL_REASON_EOF;
        break;
      }

      if( inputRecord)
      {
        // Process input record
        this._currentRecordIndex += 1;

        result = this._model.run(inputRecord=inputRecord);

        // Compute metrics.
        result.metrics = this.__metricMgr.update(result);
        // If there are None, use defaults. see MetricsManager.getMetrics()
        // TODO remove this when JAVA API server is gone
        if( not result.metrics)
        {
          result.metrics = this.__metricMgr.getMetrics();
        }


        // Write the result to the output cache. Don't write encodings, if they
        // were computed
        if( InferenceElement.encodings in result.inferences)
        {
          result.inferences.pop(InferenceElement.encodings);
        }
        result.sensorInput.dataEncodings = None;
        this._writePrediction(result);

        // Run periodic activities
        this._periodic.tick();

        if( numIters >= 0 and this._currentRecordIndex >= numIters-1)
        {
          break;
        }
      }

      else
      {
        // Input source returned an empty record.
        // 
        // NOTE: This is okay with Stream-based Source (when it times out
        // waiting for next record), but not okay with FileSource, which should
        // always return either with a valid record or None for EOF.
        raise ValueError("Got an empty record from FileSource: %r" %
                         inputRecord);
      }
    }
  }


  def _finalize(self)
  {
    """Run final activities after a model has run. These include recording and
    logging the final score""";

    this._logger.info(
      "Finished: modelID=%r; %r records processed. Performing final activities",
      this._modelID, this._currentRecordIndex + 1);

    // =========================================================================
    // Dump the experiment metrics at the end of the task
    // =========================================================================
    this._updateModelDBResults();

    // =========================================================================
    // Check if the current model is the best. Create a milestone if necessary
    // If the model has been killed, it is not a candidate for "best model",
    // and its output cache should be destroyed
    // =========================================================================
    if( not this._isKilled)
    {
      this.__updateJobResults();
    }
    else
    {
      this.__deleteOutputCache(this._modelID);
    }

    // =========================================================================
    // Close output stream, if necessary
    // =========================================================================
    if( this._predictionLogger)
    {
      this._predictionLogger.close();
    }
  }


  def __createModelCheckpoint(self)
  {
    """ Create a checkpoint from the current model, and store it in a dir named
    after checkpoint GUID, and finally store the GUID in the Models DB """;

    if( this._model is None or this._modelCheckpointGUID is None)
    {
      return;
    }

    // Create an output store, if one doesn't exist already
    if( this._predictionLogger is None)
    {
      this._createPredictionLogger();
    }

    predictions = StringIO.StringIO();
    this._predictionLogger.checkpoint(
      checkpointSink=predictions,
      maxRows=int(Configuration.get('nupic.model.checkpoint.maxPredictionRows')));

    this._model.save(os.path.join(this._experimentDir, str(this._modelCheckpointGUID)));
    this._jobsDAO.modelSetFields(modelID,
                                 {'modelCheckpointId':str(this._modelCheckpointGUID)},
                                 ignoreUnchanged=True);

    this._logger.info("Checkpointed Hypersearch Model: modelID: %r, "
                      "checkpointID: %r", this._modelID, checkpointID);
    return;
  }


  def __deleteModelCheckpoint(self, modelID)
  {
    """
    Delete the stored checkpoint for the specified modelID. This function is
    called if the current model is now the best model, making the old model's
    checkpoint obsolete

    Parameters:
    -----------------------------------------------------------------------
    modelID:      The modelID for the checkpoint to delete. This is NOT the
                  unique checkpointID
    """;

    checkpointID = \
        this._jobsDAO.modelsGetFields(modelID, ['modelCheckpointId'])[0];

    if( checkpointID is None)
    {
      return;
    }

    try
    {
      shutil.rmtree(os.path.join(this._experimentDir, str(this._modelCheckpointGUID)));
    }
    catch()
    {
      this._logger.warn("Failed to delete model checkpoint %s. "\
                        "Assuming that another worker has already deleted it",
                        checkpointID);
      return;
    }

    this._jobsDAO.modelSetFields(modelID,
                                 {'modelCheckpointId':None},
                                 ignoreUnchanged=True);
    return;
  }


  def _createPredictionLogger(self)
  {
    """
    Creates the model's PredictionLogger object, which is an interface to write
    model results to a permanent storage location
    """;
    // Write results to a file
    this._predictionLogger = BasicPredictionLogger(
      fields=this._model.getFieldInfo(),
      experimentDir=this._experimentDir,
      label = "hypersearch-worker",
      inferenceType=this._model.getInferenceType());

    if( this.__loggedMetricPatterns)
    {
      metricLabels = this.__metricMgr.getMetricLabels();
      loggedMetrics = matchPatterns(this.__loggedMetricPatterns, metricLabels);
      this._predictionLogger.setLoggedMetrics(loggedMetrics);
    }
  }


  def __getOptimizedMetricLabel(self)
  {
    """ Get the label for the metric being optimized. This function also caches
    the label in the instance variable this._optimizedMetricLabel

    Parameters:
    -----------------------------------------------------------------------
    metricLabels:   A sequence of all the labels being computed for this model

    Returns:        The label for the metric being optmized over
    """;
    matchingKeys = matchPatterns([this._optimizeKeyPattern],
                                  this._getMetricLabels());

    if( len(matchingKeys) == 0)
    {
      raise Exception("None of the generated metrics match the specified "
                      "optimization pattern: %s. Available metrics are %s" % \
                       (this._optimizeKeyPattern, this._getMetricLabels()));
    }
    else if( len(matchingKeys) > 1)
    {
      raise Exception("The specified optimization pattern '%s' matches more "
              "than one metric: %s" % (this._optimizeKeyPattern, matchingKeys));
    }

    return matchingKeys[0];
  }


  def _getMetricLabels(self)
  {
    """
    Returns:  A list of labels that correspond to metrics being computed
    """;
    return this.__metricMgr.getMetricLabels();
  }


  def _getFieldStats(self)
  {
    """
    Method which returns a dictionary of field statistics received from the
    input source.

    Returns:

      fieldStats: dict of dicts where the first level is the field name and
        the second level is the statistic. ie. fieldStats['pounds']['min']

    """;

    fieldStats = dict();
    fieldNames = this._inputSource.getFieldNames();
    for( field in fieldNames)
    {
      curStats = dict();
      curStats['min'] = this._inputSource.getFieldMin(field);
      curStats['max'] = this._inputSource.getFieldMax(field);
      fieldStats[field] = curStats;
    }
    return fieldStats;
  }


  def _getMetrics(self)
  {
    """ Protected function that can be overriden by subclasses. Its main purpose
    is to allow the the OPFDummyModelRunner to override this with deterministic
    values

    Returns: All the metrics being computed for this model
    """;
    return this.__metricMgr.getMetrics();
  }


  def _updateModelDBResults(self)
  {
    """ Retrieves the current results and updates the model's record in
    the Model database.
    """;

    // -----------------------------------------------------------------------
    // Get metrics
    metrics = this._getMetrics();

    // -----------------------------------------------------------------------
    // Extract report metrics that match the requested report REs
    reportDict = dict([(k,metrics[k]) for k in this._reportMetricLabels]);

    // -----------------------------------------------------------------------
    // Extract the report item that matches the optimize key RE
    // TODO cache optimizedMetricLabel sooner
    metrics = this._getMetrics();
    optimizeDict = dict();
    if( this._optimizeKeyPattern is not None)
    {
      optimizeDict[this._optimizedMetricLabel] = \
                                      metrics[this._optimizedMetricLabel];
    }

    // -----------------------------------------------------------------------
    // Update model results
    results = json.dumps((metrics , optimizeDict));
    this._jobsDAO.modelUpdateResults(this._modelID,  results=results,
                              metricValue=optimizeDict.values()[0],
                              numRecords=(this._currentRecordIndex + 1));

    this._logger.debug(
      "Model Results: modelID=%s; numRecords=%s; results=%s" % \
        (this._modelID, this._currentRecordIndex + 1, results));

    return;
  }


  def __updateJobResultsPeriodic(self)
  {
    """
    Periodic check to see if this is the best model. This should only have an
    effect if this is the *first* model to report its progress
    """;
    if( this._isBestModelStored and not this._isBestModel)
    {
      return;
    }

    while( True)
    {
      jobResultsStr = this._jobsDAO.jobGetFields(this._jobID, ['results'])[0];
      if( jobResultsStr is None)
      {
          jobResults = {};
      }
      else
      {
        this._isBestModelStored = True;
        if( not this._isBestModel)
        {
          return;
        }

        jobResults = json.loads(jobResultsStr);
      }

      bestModel = jobResults.get('bestModel', None);
      bestMetric = jobResults.get('bestValue', None);
      isSaved = jobResults.get('saved', False);

      // If there is a best model, and it is not the same as the current model
      // we should wait till we have processed all of our records to see if
      // we are the the best
      if (bestModel is not None) and (this._modelID != bestModel)
      {
        this._isBestModel = False;
        return;
      }

      // Make sure prediction output stream is ready before we present our model
      // as "bestModel"; sometimes this takes a long time, so update the model's
      // timestamp to help avoid getting orphaned
      this.__flushPredictionCache();
      this._jobsDAO.modelUpdateTimestamp(this._modelID);

      metrics = this._getMetrics();

      jobResults['bestModel'] = this._modelID;
      jobResults['bestValue'] = metrics[this._optimizedMetricLabel];
      jobResults['metrics'] = metrics;
      jobResults['saved'] = False;

      newResults = json.dumps(jobResults);

      isUpdated = this._jobsDAO.jobSetFieldIfEqual(this._jobID,
                                                    fieldName='results',
                                                    curValue=jobResultsStr,
                                                    newValue=newResults);
      if( isUpdated or (not isUpdated and newResults==jobResultsStr))
      {
        this._isBestModel = True;
        break;
      }
    }
  }


  def __checkIfBestCompletedModel(self)
  {
    """
    Reads the current "best model" for the job and returns whether or not the
    current model is better than the "best model" stored for the job

    Returns: (isBetter, storedBest, origResultsStr)

    isBetter:
      True if the current model is better than the stored "best model"
    storedResults:
      A dict of the currently stored results in the jobs table record
    origResultsStr:
      The json-encoded string that currently resides in the "results" field
      of the jobs record (used to create atomicity)
    """;

    jobResultsStr = this._jobsDAO.jobGetFields(this._jobID, ['results'])[0];

    if( jobResultsStr is None)
    {
        jobResults = {};
    }
    else
    {
      jobResults = json.loads(jobResultsStr);
    }

    isSaved = jobResults.get('saved', False);
    bestMetric = jobResults.get('bestValue', None);

    currentMetric = this._getMetrics()[this._optimizedMetricLabel];
    this._isBestModel = (not isSaved) \
                        or (currentMetric < bestMetric);



    return this._isBestModel, jobResults, jobResultsStr;
  }


  def __updateJobResults(self)
  {
    """"
    Check if this is the best model
    If so:
      1) Write it's checkpoint
      2) Record this model as the best
      3) Delete the previous best's output cache
    Otherwise:
      1) Delete our output cache
     """;
    isSaved = False;
    while( True)
    {
      this._isBestModel, jobResults, jobResultsStr = \
                                              this.__checkIfBestCompletedModel();

      // -----------------------------------------------------------------------
      // If the current model is the best:
      //   1) Save the model's predictions
      //   2) Checkpoint the model state
      //   3) Update the results for the job
      if( this._isBestModel)
      {

        // Save the current model and its results
        if( not isSaved)
        {
          this.__flushPredictionCache();
          this._jobsDAO.modelUpdateTimestamp(this._modelID);
          this.__createModelCheckpoint();
          this._jobsDAO.modelUpdateTimestamp(this._modelID);
          isSaved = True;
        }

        // Now record the model as the best for the job
        prevBest = jobResults.get('bestModel', None);
        prevWasSaved = jobResults.get('saved', False);

        // If the current model is the best, it shouldn't already be checkpointed
        if( prevBest == this._modelID)
        {
          assert not prevWasSaved;
        }

        metrics = this._getMetrics();

        jobResults['bestModel'] = this._modelID;
        jobResults['bestValue'] = metrics[this._optimizedMetricLabel];
        jobResults['metrics'] = metrics;
        jobResults['saved'] = True;

        isUpdated = this._jobsDAO.jobSetFieldIfEqual(this._jobID,
                                                    fieldName='results',
                                                    curValue=jobResultsStr,
                                                    newValue=json.dumps(jobResults));
        if( isUpdated)
        {
          if( prevWasSaved)
          {
            this.__deleteOutputCache(prevBest);
            this._jobsDAO.modelUpdateTimestamp(this._modelID);
            this.__deleteModelCheckpoint(prevBest);
            this._jobsDAO.modelUpdateTimestamp(this._modelID);
          }

          this._logger.info("Model %d chosen as best model", this._modelID);
          break;
        }
      }

      // -----------------------------------------------------------------------
      // If the current model is not the best, delete its outputs
      else
      {
        // NOTE: we update model timestamp around these occasionally-lengthy
        //  operations to help prevent the model from becoming orphaned
        this.__deleteOutputCache(this._modelID);
        this._jobsDAO.modelUpdateTimestamp(this._modelID);
        this.__deleteModelCheckpoint(this._modelID);
        this._jobsDAO.modelUpdateTimestamp(this._modelID);
        break;
      }
    }
  }


  def _writePrediction(self, result)
  {
    """
    Writes the results of one iteration of a model. The results are written to
    this ModelRunner's in-memory cache unless this model is the "best model" for
    the job. If this model is the "best model", the predictions are written out
    to a permanent store via a prediction output stream instance


    Parameters:
    -----------------------------------------------------------------------
    result:      A opfutils.ModelResult object, which contains the input and
                  output for this iteration
    """;
    this.__predictionCache.append(result);

    if( this._isBestModel)
    {
     this.__flushPredictionCache();
    }
  }


  def __writeRecordsCallback(self)
  {
    """ This callback is called by this.__predictionLogger.writeRecords()
    between each batch of records it writes. It gives us a chance to say that
    the model is 'still alive' during long write operations.
    """;

    // This updates the engLastUpdateTime of the model record so that other
    //  worker's don't think that this model is orphaned.
    this._jobsDAO.modelUpdateResults(this._modelID);
  }


  def __flushPredictionCache(self)
  {
    """
    Writes the contents of this model's in-memory prediction cache to a permanent
    store via the prediction output stream instance
    """;

    if( not this.__predictionCache)
    {
      return;
    }

    // Create an output store, if one doesn't exist already
    if( this._predictionLogger is None)
    {
      this._createPredictionLogger();
    }

    startTime = time.time();
    this._predictionLogger.writeRecords(this.__predictionCache,
                                        progressCB=this.__writeRecordsCallback);
    this._logger.info("Flushed prediction cache; numrows=%s; elapsed=%s sec.",
                      len(this.__predictionCache), time.time() - startTime);
    this.__predictionCache.clear();
  }


  def __deleteOutputCache(self, modelID)
  {
    """
    Delete's the output cache associated with the given modelID. This actually
    clears up the resources associated with the cache, rather than deleting al
    the records in the cache

    Parameters:
    -----------------------------------------------------------------------
    modelID:      The id of the model whose output cache is being deleted

    """;

    // If this is our output, we should close the connection
    if( modelID == this._modelID and this._predictionLogger is not None)
    {
      this._predictionLogger.close();
      del this.__predictionCache;
      this._predictionLogger = None;
      this.__predictionCache = None;
    }
  }


  def _initPeriodicActivities(self)
  {
    """ Creates and returns a PeriodicActivityMgr instance initialized with
    our periodic activities

    Parameters:
    -------------------------------------------------------------------------
    retval:             a PeriodicActivityMgr instance
    """;

    // Activity to update the metrics for this model
    // in the models table
    updateModelDBResults = PeriodicActivityRequest(repeating=True,
                                                 period=100,
                                                 cb=this._updateModelDBResults);

    updateJobResults = PeriodicActivityRequest(repeating=True,
                                               period=100,
                                               cb=this.__updateJobResultsPeriodic);

    checkCancelation = PeriodicActivityRequest(repeating=True,
                                               period=50,
                                               cb=this.__checkCancelation);

    checkMaturity = PeriodicActivityRequest(repeating=True,
                                            period=10,
                                            cb=this.__checkMaturity);


    // Do an initial update of the job record after 2 iterations to make
    // sure that it is populated with something without having to wait too long
    updateJobResultsFirst = PeriodicActivityRequest(repeating=False,
                                               period=2,
                                               cb=this.__updateJobResultsPeriodic);


    periodicActivities = [updateModelDBResults,
                          updateJobResultsFirst,
                          updateJobResults,
                          checkCancelation];

    if( this._isMaturityEnabled)
    {
      periodicActivities.append(checkMaturity);
    }

    return PeriodicActivityMgr(requestedActivities=periodicActivities);
  }


  def __checkCancelation(self)
  {
    """ Check if the cancelation flag has been set for this model
    in the Model DB""";

    // Update a hadoop job counter at least once every 600 seconds so it doesn't
    //  think our map task is dead
    print >>sys.stderr, "reporter:counter:HypersearchWorker,numRecords,50";

    // See if the job got cancelled
    jobCancel = this._jobsDAO.jobGetFields(this._jobID, ['cancel'])[0];
    if( jobCancel)
    {
      this._cmpReason = ClientJobsDAO.CMPL_REASON_KILLED;
      this._isCanceled = True;
      this._logger.info("Model %s canceled because Job %s was stopped.",
                        this._modelID, this._jobID);
    }
    else
    {
      stopReason = this._jobsDAO.modelsGetFields(this._modelID, ['engStop'])[0];

      if( stopReason is None)
      {
        pass;
      }

      else if( stopReason == ClientJobsDAO.STOP_REASON_KILLED)
      {
        this._cmpReason = ClientJobsDAO.CMPL_REASON_KILLED;
        this._isKilled = True;
        this._logger.info("Model %s canceled because it was killed by hypersearch",
                          this._modelID);
      }

      else if( stopReason == ClientJobsDAO.STOP_REASON_STOPPED)
      {
        this._cmpReason = ClientJobsDAO.CMPL_REASON_STOPPED;
        this._isCanceled = True;
        this._logger.info("Model %s stopped because hypersearch ended", this._modelID);
      }
      else
      {
        raise RuntimeError ("Unexpected stop reason encountered: %s" % (stopReason));
      }
    }
  }


  def __checkMaturity(self)
  {
    """ Save the current metric value and see if the model's performance has
    'leveled off.' We do this by looking at some number of previous number of
    recordings """;

    if( this._currentRecordIndex+1 < this._MIN_RECORDS_TO_BE_BEST)
    {
      return;
    }

    // If we are already mature, don't need to check anything
    if( this._isMature)
    {
      return;
    }

    metric = this._getMetrics()[this._optimizedMetricLabel];
    this._metricRegression.addPoint(x=this._currentRecordIndex, y=metric);

    // Perform a linear regression to see if the error is leveled off
    // pctChange = this._metricRegression.getPctChange()
    // if pctChange  is not None and abs(pctChange ) <= this._MATURITY_MAX_CHANGE:
    pctChange, absPctChange = this._metricRegression.getPctChanges();
    if( pctChange  is not None and absPctChange <= this._MATURITY_MAX_CHANGE)
    {
      this._jobsDAO.modelSetFields(this._modelID,
                                   {'engMatured':True});

      // TODO: Don't stop if we are currently the best model. Also, if we
      // are still running after maturity, we have to periodically check to
      // see if we are still the best model. As soon we lose to some other
      // model, then we should stop at that point.
      this._cmpReason = ClientJobsDAO.CMPL_REASON_STOPPED;
      this._isMature = True;

      this._logger.info("Model %d has matured (pctChange=%s, n=%d). \n"\
                        "Scores = %s\n"\
                         "Stopping execution",this._modelID, pctChange,
                                              this._MATURITY_NUM_POINTS,
                                              this._metricRegression._window);
    }
  }


  def handleWarningSignal(self, signum, frame)
  {
    """
    Handles a "warning signal" from the scheduler. This is received when the
    scheduler is about to kill the the current process so that the worker can be
    allocated to another job.

    Right now, this function just sets the current model to the "Orphaned" state
    in the models table so that another worker can eventually re-run this model

    Parameters:
    -----------------------------------------------------------------------
    """;
    this._isInterrupted.set();
  }


  def __setAsOrphaned(self)
  {
    """
    Sets the current model as orphaned. This is called when the scheduler is
    about to kill the process to reallocate the worker to a different process.
    """;
    cmplReason = ClientJobsDAO.CMPL_REASON_ORPHAN;
    cmplMessage = "Killed by Scheduler";
    this._jobsDAO.modelSetCompleted(this._modelID, cmplReason, cmplMessage);
  }
}

class OPFModelRunner(object)
{
  def __init__(self,
               modelID,
               jobID,
               predictedField,
               experimentDir,
               reportKeyPatterns,
               optimizeKeyPattern,
               jobsDAO,
               modelCheckpointGUID,
               logLevel=None,
               predictionCacheMaxRecords=None)
  {
    // -----------------------------------------------------------------------
    // Initialize class constants
    // -----------------------------------------------------------------------
    this._MIN_RECORDS_TO_BE_BEST = int(Configuration.get('nupic.hypersearch.bestModelMinRecords'));
    this._MATURITY_MAX_CHANGE = float(Configuration.get('nupic.hypersearch.maturityPctChange'));
    this._MATURITY_NUM_POINTS = int(Configuration.get('nupic.hypersearch.maturityNumPoints'));

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

    this._isMaturityEnabled = bool(int(Configuration.get('nupic.hypersearch.enableModelMaturity')));

    this._logger = logging.getLogger(".".join( ['com.numenta',
                       this.__class__.__module__, this.__class__.__name__]));

    this._optimizedMetricLabel = None;
    this._reportMetricLabels = [];


    // Our default completion reason
    this._cmpReason = ClientJobsDAO.CMPL_REASON_EOF;

    if( logLevel is not None)
    {
      this._logger.setLevel(logLevel);
    }

    // The manager object to compute the metrics for this model
    this.__metricMgr = None;

    // Will be set to a new instance of OPFTaskDriver by __runTask()
    // this.__taskDriver = None

    // Current task control parameters. Will be set by __runTask()
    this.__task = None;

    // Will be set to a new instance of PeriodicActivityManager by __runTask()
    this._periodic = None;

    // Will be set to streamDef string by _runTask()
    this._streamDef = None;

    // Will be set to new OpfExperiment instance by run()
    this._model = None;

    // Will be set to new InputSource by __runTask()
    this._inputSource = None;

    // 0-based index of the record being processed;
    // Initialized and updated by __runTask()
    this._currentRecordIndex = None;

    // Interface to write predictions to a persistent storage
    this._predictionLogger = None;

    // In-memory cache for predictions. Predictions are written here for speed
    // when they don't need to be written to a persistent store
    this.__predictionCache = deque();

    // Flag to see if this is the best model in the job (as determined by the
    // model chooser logic). This is essentially a cache of the value in the
    // ClientJobsDB
    this._isBestModel = False;

    // Flag to see if there is a best model (not necessarily this one)
    // stored in the DB
    this._isBestModelStored = False;


    // -----------------------------------------------------------------------
    // Flags for model cancelation/checkpointing
    // -----------------------------------------------------------------------

    // Flag to see if the job that this model is part of
    this._isCanceled = False;

    // Flag to see if model was killed, either by the model terminator or by the
    // hypsersearch implementation (ex. the a swarm is killed/matured)
    this._isKilled = False;

    // Flag to see if the model is matured. In most cases, this means that we
    // should stop running the model. The only execption is if this model is the
    // best model for the job, in which case it should continue running.
    this._isMature = False;

    // Event to see if interrupt signal has been sent
    this._isInterrupted = threading.Event();

    // -----------------------------------------------------------------------
    // Facilities for measuring model maturity
    // -----------------------------------------------------------------------
    // List of tuples, (iteration, metric), used to see if the model has 'matured'
    this._metricRegression = regression.AveragePctChange(windowSize=this._MATURITY_NUM_POINTS);

    this.__loggedMetricPatterns = [];
  }



    */
}