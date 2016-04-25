using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Taurus.HtmEngine.runtime
{
    // For use with AnomalyService
    public class MutableMetricDataRow
    {
        public MutableMetricDataRow(double anomalyScore, string displayValue, double metricValue, double rawAnomalyScore,
            int rowid, DateTime timestamp, string uid)
        {
            AnomalyScore = anomalyScore;
            DisplayValue = displayValue;
            MetricValue = metricValue;
            RawAnomalyScore = rawAnomalyScore;
            Rowid = rowid;
            Timestamp = timestamp;
            Uid = uid;
        }

        public override string ToString()
        {
            return string.Format("{0}<uid={1}, rowid={2}, ts={3}, value={4}, raw={5}, anomlik={6}, display={7}>",
                GetType().Name, Uid, Rowid, Timestamp, MetricValue, RawAnomalyScore, AnomalyScore, DisplayValue);
        }

        public double AnomalyScore { get; set; }
        public string DisplayValue { get; set; }
        public double MetricValue { get; set; }
        public double RawAnomalyScore { get; set; }
        public long Rowid { get; set; }
        public DateTime Timestamp { get; set; }
        public string Uid { get; set; }
    }

    /// <summary>
    /// Anomaly Service for processing CLA model results, calculating Anomaly
    ///     Likelihood scores, and updating the associated metric data records
    ///     Records are processed in batches from
    /// ``ModelSwapperInterface().consumeResults()`` and the associated
    /// ``MetricData`` rows are updated with the results of applying
    /// ``AnomalyLikelihoodHelper().updateModelAnomalyScores()`` and finally the
    /// results are packaged up as as objects complient with
    /// ``model_inference_results_msg_schema.json`` and published to the model
    /// results exchange, as identified by the ``results_exchange_name``
    /// configuration directive from the ``metric_streamer`` section of
    /// ``config``.
    /// Other services may be subscribed to the model results fanout exchange for
    /// subsequent(and parallel) processing.For example,
    /// ``htmengine.runtime.notification_service.NotificationService`` is one example
    /// of a use-case for that exchange.Consumers must deserialize inbound messages
    /// with ``AnomalyService.deserializeModelResult()``.
    /// </summary>
    public class AnomalyService
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(AnomalyService));
        private bool _profiling;
        private int _statisticsSampleSize;
        private string _modelResultsExchange;
        private AnomalyLikelihoodHelper _likelihoodHelper;

        public AnomalyService()
        {
            _profiling = _log.IsDebugEnabled;
            _modelResultsExchange = ""; // from config
            _statisticsSampleSize = 10; // from config
            _likelihoodHelper = new AnomalyLikelihoodHelper(_log);

        }
    }

    // https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/().py

    /// <summary>
    /// Helper class for running AnomalyLikelihood calculations in AnomalyService
    ///   Usage::
    ///     likelihoodHelper = AnomalyLikelihoodHelper(log, config)
    ///     likelihoodHelper.updateModelAnomalyScores(engine=engine,
    ///         metric=metric, metricDataRows=metricDataRows)
    /// </summary>
    public class AnomalyLikelihoodHelper
    {
        private ILog _log;
        private int _minStatisticsRefreshInterval;
        private int _statisticsMinSampleSize;
        private int _statisticsSampleSize;
        private AnomalyLikelihood algorithms;

        public const int NUM_SKIP_RECORDS = 288;    // one day of records

        /// <summary>
        /// 
        /// </summary>
        /// <param name="log">htmengine logger</param>
        /// <param name="config">htmlengine config</param>
        public AnomalyLikelihoodHelper(ILog log, object config = null)
        {
            //config.LoadConfig();
            _log = log;
            // from config
            _minStatisticsRefreshInterval = 10;
            _statisticsMinSampleSize = 200;
            _statisticsSampleSize = 1000;

            Parameters pars = Parameters.Empty();
            pars.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.LIKELIHOOD);
            algorithms = (AnomalyLikelihood)Anomaly.Create(pars);
        }

        /// <summary>
        /// Generate the model's anomaly likelihood parameters from the given sample cache.
        /// </summary>
        /// <param name="metricId">the metric ID</param>
        /// <param name="statsSampleCache">a sequence of MetricData instances that
        /// comprise the cache of samples for the current inference result batch with
        /// valid raw_anomaly_score in the processed order(by rowid/timestamp). At
        /// least self._statisticsMinSampleSize samples are needed.</param>
        /// <param name="defaultAnomalyParams">the default anomaly params value; if can't
        /// generate new ones(not enough samples in cache), this value will be
        /// returned verbatim</param>
        /// <returns>new anomaly likelihood parameters; defaultAnomalyParams, if there 
        /// are not enough samples in statsSampleCache.</returns>
        internal Map<string, object> GenerateAnomalyParams(string metricId, List<MetricData> statsSampleCache, Map<string, object> defaultAnomalyParams)
        {
            if (string.IsNullOrWhiteSpace(metricId)) throw new ArgumentNullException("metricId");
            if (statsSampleCache == null) throw new ArgumentNullException("statsSampleCache");
            if (statsSampleCache.Count < _statisticsMinSampleSize)
            {
                // Not enough samples in cache
                // TODO: unit test this
                _log.ErrorFormat("Not enough samples in cache to update anomaly params for model={0}: have={1}, which is less than min={2}; firstRowID={3}; lastRowID={4}.",
                    metricId, statsSampleCache.Count, _statisticsMinSampleSize, statsSampleCache.FirstOrDefault()?.Rowid, statsSampleCache.LastOrDefault()?.Rowid);
                return defaultAnomalyParams;
            }
            // We have enough samples to generate anomaly params
            long lastRowId = statsSampleCache.Last().Rowid;
            int numSamples = Math.Min(statsSampleCache.Count, _statisticsSampleSize);

            // Create input sequence for algorithms
            var samples = statsSampleCache.Skip(statsSampleCache.Count - numSamples).Take(statsSampleCache.Count);

            var scores = samples.Select(row => new Sample(row.Timestamp, row.MetricValue, row.RawAnomalyScore.GetValueOrDefault())).ToList();

            Debug.Assert(scores.Count >= _statisticsMinSampleSize);
            Debug.Assert(scores.Count <= _statisticsSampleSize);

            // Calculate estimator parameters
            // We ignore statistics from the first day of data (288 records) since the
            // CLA is still learning. For simplicity, this logic continues to ignore the
            // first day of data even once the window starts sliding.
            var metrics = algorithms.EstimateAnomalyLikelihoods(scores, skipRecords: NUM_SKIP_RECORDS);

            AnomalyLikelihood.AnomalyParams anPars = metrics.GetParams();

            var anomalyParams = new Map<string, object>();
            anomalyParams["last_rowid_for_stats"] = lastRowId;
            anomalyParams["params"] = anPars;
            return anomalyParams;
        }

        /// <summary>
        /// Calculate the anomaly scores based on the anomaly likelihoods. Update
        /// anomaly scores in the given metricDataRows MetricData instances, and
        /// calculate new anomaly likelihood params for the model.
        /// </summary>
        /// <param name="metricObj">the model's Metric instance</param>
        /// <param name="metricDataRows"> a sequence of MetricData instances in the
        /// processed order(ascending by timestamp) with updated raw_anomaly_score
        /// and zeroed out anomaly_score corresponding to the new model inference
        /// results, but not yet updated in the database.Will update their
        /// anomaly_score properties, as needed.</param>
        /// <returns>new anomaly likelihood params for the model</returns>
        /// <remarks>
        /// *NOTE:*
        ///     the processing must be idempotent due to the "at least once" delivery
        ///     semantics of the message bus
        /// *NOTE:*
        ///     the performance goal is to minimize costly database access and avoid
        ///     falling behind while processing model results, especially during the
        ///     model's initial "catch-up" phase when large inference result batches are
        ///     prevalent.
        /// </remarks>
        public Map<string, object> UpdateModelAnomalyScores(Metric metricObj, List<MetricData> metricDataRows)
        {
            // When populated, a cached list of MetricData instances for updating anomaly likelyhood params
            List<MetricData> statsSampleCache = null;

            // Index into metricDataRows where processing is to resume
            int startRowIndex = 0;

            var statisticsRefreshInterval = GetStatisticsRefreshInterval(batchSize: metricDataRows.Count);

            if (metricObj.Status != MetricStatus.Active)
            {
                throw new MetricNotActiveError(string.Format("Metric {0} is not active.", metricObj.Uid));
            }

            ModelParams modelParams = JsonConvert.DeserializeObject<ModelParams>(metricObj.ModelParams, new JsonSerializerSettings
            {
                TypeNameHandling = TypeNameHandling.All
            });
            var anomalyParams = (Map<string, object>)modelParams.AnomalyLikelihoodParams;
            if (anomalyParams == null)
            {
                // We don't have a likelihood model yet. Create one if we have sufficient
                // records with raw anomaly scores
                var initData = InitAnomalyLikelihoodModel(metricObj, metricDataRows);
                anomalyParams = (Map<string, object>)initData["anomalyParams"];
                statsSampleCache = (List<MetricData>)initData["statsSampleCache"];
                startRowIndex = (int)initData["startRowIndex"];
            }

            // Do anomaly likelihood processing on the rest of the new samples
            // NOTE: this loop will be skipped if there are still not enough samples for
            // creating the anomaly likelihood params
            while (startRowIndex < metricDataRows.Count)
            {
                long endRowID;
                // Determine where to stop processing rows prior to next statistics refresh
                if (statsSampleCache == null || statsSampleCache.Count >= _statisticsMinSampleSize)
                {
                    // We're here if:
                    // a. We haven't tried updating anomaly likelihood stats yet
                    // OR
                    // b. We already updated anomaly likelyhood stats (we had sufficient
                    //      samples for it)
                    // TODO: unit-test
                    endRowID = (long)anomalyParams["last_rowid_for_stats"] + statisticsRefreshInterval;
                    if (endRowID < metricDataRows[startRowIndex].Rowid)
                    {
                        // We're here if:
                        // a. Statistics refresh interval is smaller than during last stats
                        //  update; this is the typical/normal case when backlog catch-up
                        //  is tapering off, and refresh interval is reduced for smaller
                        //  batches. OR
                        // b. There is a gap of anomaly scores preceeding the start of the
                        //  current chunk. OR
                        // c. Statistics config changed.
                        // TODO: unit-test
                        _log.WarnFormat("Anomaly run cutoff precedes samples (smaller stats refreshInterval or gap in anomaly scores or statistics config changed): model={0}; rows={1}..{2}",
                            metricObj.Uid, metricDataRows[startRowIndex].Rowid, endRowID);

                        if (statsSampleCache != null)
                        {
                            // We already attempted to update anomaly likelihood params, so fix
                            // up endRowID to make sure we make progress and don't get stuck in
                            // an infinite loop
                            endRowID = metricDataRows[startRowIndex].Rowid;
                            _log.WarnFormat("Advanced anomaly run cutoff to make progress: model={0}; rows={1}..{2}",
                                metricObj.Uid, metricDataRows[startRowIndex].Rowid, endRowID);
                        }
                    }
                }
                else
                {
                    // During prior iteration, there were not enough samples in cache for
                    // updating anomaly params
                    //
                    // We extend the end row so that there will be enough samples
                    // to avoid getting stuck in this rut in the current and following
                    // iterations
                    // TODO: unit-test this
                    endRowID = metricDataRows[startRowIndex].Rowid + (_statisticsMinSampleSize - statsSampleCache.Count - 1);
                }

                // Translate endRowID into metricDataRows limitIndex for current run
                long limitIndex;
                if (endRowID < metricDataRows[startRowIndex].Rowid)
                {
                    // Cut-off precedes the remaining samples
                    // Normally shouldn't be here (unless statistics config changed or there
                    // is a gap in anomaly scores in metric_data table)
                    // TODO: unit-test this
                    //
                    // Set limit to bypass processing of samples for immediate refresh of
                    // anomaly likelihood params
                    limitIndex = startRowIndex;
                    _log.WarnFormat("Anomaly run cutoff precedes samples, so forcing refresh of anomaly likelihood params: model=<{0}>; rows={1}..{2}",
                               GetMetricLogPrefix(metricObj), metricDataRows[startRowIndex].Rowid, endRowID);
                }
                else
                {
                    // Cutoff is either inside or after the remaining samples
                    //  TODO: unit-test this
                    limitIndex = startRowIndex + Math.Min(
                        metricDataRows.Count - startRowIndex,
                        endRowID + 1 - metricDataRows[startRowIndex].Rowid);
                }

                // Process the next new sample run
                _log.DebugFormat("Starting anomaly run: model={0}; startRowIndex={1}; limitIndex={2}; rows=[{3}..{4}]; last_rowid_for_stats={5}; refreshInterval={6}; batchSize={7}",
                    metricObj.Uid,
                    startRowIndex, limitIndex, metricDataRows[startRowIndex].Rowid,
                    endRowID, anomalyParams["last_rowid_for_stats"],
                    statisticsRefreshInterval, metricDataRows.Count);

                List<MetricData> consumedSamples = new List<MetricData>();

                foreach (var md in metricDataRows.Skip(startRowIndex).Take((int)limitIndex))
                {
                    consumedSamples.Add(md);
                    var computed = algorithms.UpdateAnomalyLikelihoods(new List<Sample> { new Sample(md.Timestamp, md.MetricValue, md.RawAnomalyScore.GetValueOrDefault()) },
                        (AnomalyLikelihood.AnomalyParams)anomalyParams["params"]);
                    var likelihood = computed.GetLikelihoods().First();

                    md.AnomalyScore = 1.0 - likelihood;

                    // If anomaly score > 0.99 then we greedily update the statistics. 0.99
                    // should not repeat too often, but to be safe we wait a few more
                    // records before updating again, in order to avoid overloading the DB.
                    //
                    // TODO: the magic 0.99 and the magic 3 value below should either
                    // be constants or config settings. Where should they be defined?
                    if (md.AnomalyScore > 0.99 && ((int)anomalyParams["last_rowid_for_stats"] + 1) < md.Rowid)
                    {
                        if (statsSampleCache == null ||
                            (statsSampleCache.Count + consumedSamples.Count) >= _statisticsMinSampleSize)
                        {
                            // TODO: unit-test this
                            _log.InfoFormat("Forcing refresh of anomaly params for model={0} due to exceeded anomaly_score threshold in sample={1}",
                                metricObj.Uid, md);
                            break;
                        }
                    }
                }

                if (startRowIndex + consumedSamples.Count < metricDataRows.Count ||
                    (consumedSamples.Last().Rowid >= endRowID))
                {
                    // We stopped before the end of new samples, including a bypass-run,
                    // or stopped after processing the last item and need one final refresh
                    // of anomaly params
                    var result = RefreshAnomalyParams(metricId: metricObj.Uid, statsSampleCache: statsSampleCache,
                        consumedSamples: consumedSamples, defaultAnomalyParams: anomalyParams);
                    anomalyParams = (Map<string, object>)result["anomalyParams"];
                    statsSampleCache = (List<MetricData>)result["statsSampleCache"];
                }
                startRowIndex += consumedSamples.Count;
            }

            return anomalyParams;
        }

        /// <summary>
        /// Refresh anomaly likelihood parameters from the tail of statsSampleCache and consumedSamples 
        /// up to self._statisticsSampleSize.
        /// 
        /// Update statsSampleCache, including initializing from metric_data table, if
        /// needed, and appending of consumedSamples content.
        /// </summary>
        /// <param name="metricId">the metric ID</param>
        /// <param name="statsSampleCache">A list of MetricData instances. null, if the
        /// cache hasn't been initialized yet as is the case when the anomaly
        /// likelihood model is being built for the first time for the model or are
        /// being refreshed for the first time within a given result batch, in which
        /// case it will be initialized as follows: up to the balance of
        /// self._statisticsSampleSize in excess of consumedSamples will be loaded
        /// from the metric_data table.</param>
        /// <param name="consumedSamples">A sequence of samples that have been consumed by
        /// anomaly processing, but are not yet in statsSampleCache.They will be
        /// appended to statsSampleCache</param>
        /// <param name="defaultAnomalyParams">the default anomaly params value; if can't
        /// generate new ones, this value will be returned in the result tuple</param>
        /// <returns>the tuple (anomalyParams, statsSampleCache)</returns>
        /// <remarks>
        /// If statsSampleCache was None on entry, it will be initialized as follows:
        /// up to the balance of self._statisticsSampleSize in excess of
        /// 
        /// consumedSamples metric data rows with non-null raw anomaly scores will be
        /// loaded from the metric_data table and consumedSamples will be appended to
        /// it.If statsSampleCache was not None on entry, then elements from
        /// consumedSamples will be appended to it.The returned statsSampleCache will
        /// be a list NOTE: it may be an empty list, if there was nothing to fill it with.
        /// 
        /// If there are not enough total samples to satisfy
        /// self._statisticsMinSampleSize, then the given defaultAnomalyParams will be
        /// returned in the tuple.
        /// </remarks>
        internal NamedTuple RefreshAnomalyParams(string metricId, List<MetricData> statsSampleCache, List<MetricData> consumedSamples,
            Map<string, object> defaultAnomalyParams)
        {
            // Update the samples cache
            if (statsSampleCache == null)
            {
                // The samples cache hasn't been initialized yet, so build it now;
                // this happens when the model is being built for the first time or when
                // anomaly params are being refreshed for the first time within an
                // inference result batch.
                // TODO: unit-test this
                var tail = TailMetricDataWithRawAnomalyScores(metricId,
                    Math.Max(0, _statisticsSampleSize - consumedSamples.Count));

                statsSampleCache = new List<MetricData>(tail);
                statsSampleCache.AddRange(consumedSamples);
            }
            else
            {
                // TODO: unit-test this
                statsSampleCache.AddRange(consumedSamples);
            }

            var anomalyParams = GenerateAnomalyParams(metricId, statsSampleCache, defaultAnomalyParams);

            return new NamedTuple(new[] { "anomalyParams", "statsSampleCache" }, anomalyParams, statsSampleCache);
        }

        /// <summary>
        /// Fetch the tail of metric_data rows with non-null raw_anomaly_score for the given metric ID
        /// </summary>
        /// <param name="metricId">the metric ID</param>
        /// <param name="limit">max number of tail rows to fetch</param>
        /// <returns>an iterable that yields up to `limit` MetricData tail
        /// instances with non-null raw_anomaly_score ordered by metric data timestamp
        /// in ascending order</returns>
        internal List<MetricData> TailMetricDataWithRawAnomalyScores(string metricId, int limit)
        {
            if (limit == 0) return new List<MetricData>();
            var rows = RepositoryFactory.Metric.GetMetricDataWithRawAnomalyScoresTail(metricId, limit);
            return rows.OrderBy(r => r.Timestamp).ToList();
        }

        internal object GetMetricLogPrefix(Metric metricObj)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Create the anomaly likelihood model for the given Metric instance.
        /// Assumes that the metric doesn't have anomaly params yet.
        /// </summary>
        /// <param name="metricObj">Metric instance with no anomaly likelihood params</param>
        /// <param name="metricDataRows">
        /// a sequence of MetricData instances corresponding to the inference results batch in the processed order
        /// (ascending by rowid and timestamp) with updated raw_anomaly_score and zeroed out anomaly_score corresponding 
        /// to the new model inference results, but not yet updated in the database.Will not alter this sequence.</param>
        /// <returns>
        /// the tuple (anomalyParams, statsSampleCache, startRowIndex)
        ///   anomalyParams: None, if there are too few samples; otherwise, the anomaly
        ///   likelyhood objects as returned by algorithms.estimateAnomalyLikelihoods
        /// statsSampleCache: None, if there are too few samples; otherwise, a list of
        ///   MetricData instances comprising of a concatenation of rows sourced
        ///   from metric_data tail and topped off with necessary items from the
        ///   given metricDataRows for a minimum of self._statisticsMinSampleSize and
        ///   a maximum of self._statisticsSampleSize total items.
        /// startRowIndex: Index into the given metricDataRows where processing of
        ///   anomaly scores is to start; if there are too few samples to generate
        ///   the anomaly likelihood params, then startRowIndex will reference past
        ///   the last item in the given metricDataRows sequence.
        /// </returns>
        internal NamedTuple InitAnomalyLikelihoodModel(Metric metricObj, List<MetricData> metricDataRows)
        {
            if (metricObj.Status != MetricStatus.Active)
            {
                throw new MetricNotActiveError(string.Format("getAnomalyLikelihoodParams failed because metric={0} is not ACTIVE; status={1}; resource={2}",
                    metricObj.Uid, metricObj.Status, metricObj.Server));
            }
            ModelParams modelParams = JsonConvert.DeserializeObject<ModelParams>(metricObj.ModelParams);
            var anomalyParams = modelParams.AnomalyLikelihoodParams;
            //Debug.Assert(anomalyParams != null, "anomalyParams");

            List<MetricData> statsSampleCache = null;

            // Index into metricDataRows where processing of anomaly scores is to start
            int startRowIndex = 0;

            int numProcessedRows = RepositoryFactory.Metric.GetProcessedMetricDataCount(metricObj.Uid);

            if (numProcessedRows + metricDataRows.Count >= _statisticsMinSampleSize)
            {
                // We have enough samples to initialize the anomaly likelihood model
                // TODO: unit test

                // Determine how many samples will be used from metricDataRows
                int numToConsume = Math.Max(0, _statisticsMinSampleSize - numProcessedRows);
                var consumedSamples = metricDataRows.Take(numToConsume).ToList();// [:numToConsume]
                startRowIndex += numToConsume;

                // Create the anomaly likelihood model
                NamedTuple refeshResult = RefreshAnomalyParams(metricObj.Uid, null, consumedSamples, anomalyParams);
                anomalyParams = (Map<string, object>)refeshResult["anomalyParams"];
                statsSampleCache = (List<MetricData>)refeshResult["statsSampleCache"];

                // If this assertion fails, it implies that the count retrieved by our
                // call to MetricData.count above is no longer correct
                _log.DebugFormat("Generated initial anomaly params for model={0}: umSamples={1}; firstRowID={2}; lastRowID={3};",
                    metricObj.Uid, statsSampleCache.Count, statsSampleCache[0].Rowid, statsSampleCache.Last().Rowid);
            }
            else
            {
                // Not enough raw scores yet to begin anomaly likelyhoods processing
                // TODO: unit-test
                startRowIndex = metricDataRows.Count;
            }

            return new NamedTuple(new[] { "anomalyParams", "statsSampleCache", "startRowIndex" },
                anomalyParams, statsSampleCache, startRowIndex);
        }

        /// <summary>
        /// Determine the interval for refreshing anomaly likelihood parameters.
        /// 
        /// The strategy is to use larger refresh intervals in large batches, which are
        /// presumably older catch-up data, in order to speed up our processing.
        /// Config-based self._minStatisticsRefreshInterval serves as the baseline.
        /// </summary>
        /// <param name="batchSize">number of elements in the inference result batch being processed.</param>
        /// <returns>an integer that indicates how many samples should be processed until the next refresh of anomaly likelihood parameters</returns>
        internal int GetStatisticsRefreshInterval(int batchSize)
        {
            return (int)Math.Max(_minStatisticsRefreshInterval, batchSize * 0.1);
        }
    }


    /// <summary>
    /// Metric states stored in the "metric" SQL table
    /// </summary>
    [Flags]
    public enum MetricStatus
    {
        /// <summary>
        /// This is used when a metric exists but is not monitored. HTM metrics
        /// utilize this when data is sent in but the metric isn't monitored yet.
        /// </summary>
        Unmonitored = 0,
        /// <summary>
        /// This means the model has been created in the engine and there are no errors.
        /// </summary>
        Active = 1,
        /// <summary>
        /// This state is used when a model creation command has been sent to the
        /// engine but hasn't been processed yet.
        /// </summary>
        CreatePending = 2,
        /// <summary>
        /// When there is an irrecoverable error with a model it is put into this state
        /// and the message field is populated with the reason.
        /// </summary>
        Error = 4,
        /// <summary>
        /// The state is used for delayed model creation when there is a specified min
        /// and max and there isn't sufficient data to estimate the min and max with confidence.
        /// </summary>
        PendingData = 8
    }

    #region Exceptions

    public class MetricNotActiveError : Exception
    {
        public MetricNotActiveError(string message)
            : base(message)
        {

        }
    }
    /// <summary>
    /// Raised when too many models or "instances" have been created
    /// </summary>
    public class ModelQuotaExceededError : Exception
    {

    }

    /// <summary>
    /// Generic exception for non-specific error while attempting to monitor a metric
    /// </summary>
    public class ModelMonitorRequestError : Exception
    {

    }
    /// <summary>
    /// Generic exception for non-specific error while attempting to unmonitor a metric
    /// </summary>
    public class ModelUnmonitorRequestError : Exception
    {

    }
    /// <summary>
    /// Generic exception for non-specific error while attempting to delete a metric
    /// </summary>
    public class MetricDeleteRequestError : Exception
    {

    }
    /// <summary>
    /// Specified metric was not found
    /// </summary>
    public class MetricNotFound : Exception
    {

    }

    public class MetricsChangeError : Exception
    {
        public MetricsChangeError(string format, params object[] args)
            : base(string.Format(format, args))
        {

        }
    }

    /// <summary>
    /// The requested model was not found (already deleted?)
    /// </summary>
    public class ModelNotFound : Exception
    {

    }

    /// <summary>
    /// Generic exception for non-specific error while getting all models
    /// </summary>
    public class GetModelsRequestError : Exception
    {

    }
    /// <summary>
    /// Exceeded max retries without a single successful execution
    /// </summary>
    public class RetriesExceededError : Exception
    {

    }

    public class MetricAlreadyMonitored : Exception
    {
        public MetricAlreadyMonitored(string uid, string format, params object[] args)
            : base(string.Format(format, args))
        {
            Uid = uid;
        }

        public string Uid { get; set; }
    }

    public class MetricAlreadyExists : Exception
    {
        public MetricAlreadyExists(string uid, string message)
            : base(message)
        {
            Uid = uid;
        }
        public string Uid { get; set; }
    }

    #endregion

    /// <summary>
    /// Agent for calling metric operations on the API server.
    /// </summary>
    public class MetricUtils
    {
        public static MetricsConfiguration GetMetricsConfiguration()
        {
            // Load the metrics.json file
            string metrics = Properties.Resources.metrics;
            return JsonConvert.DeserializeObject<MetricsConfiguration>(metrics);
        }

        /// <summary>
        /// Return all metric names from the given metrics configuration
        /// </summary>
        /// <param name="metricsConfig">metrics configuration as returned by `getMetricsConfiguration()`</param>
        /// <returns>all metric names from the given metricsConfig</returns>
        public static List<string> GetMetricNamesFromConfig(MetricsConfiguration metricsConfig)
        {
            return metricsConfig.Values
                    .SelectMany(m => m.Metrics.Keys)
                    .ToList();
        }

        /// <summary>
        /// Create a model for a metric
        /// </summary>
        /// <param name="host">API server's hostname or IP address</param>
        /// <param name="apiKey">API server's API Key</param>
        /// <param name="modelParams">model parameters dict per _models POST API</param>
        /// <returns>model info dictionary from the result of the _models POST request on success.</returns>
        public static Map<string, object> CreateHtmModel(string host, string apiKey, CreateModelRequest modelParams)
        {
            throw new InvalidOperationException("posts to /_models endpoint webapi");
        }

        /// <summary>
        /// Create a model for a metric
        /// </summary>
        /// <param name="host">API server's hostname or IP address</param>
        /// <param name="apiKey">API server's API Key</param>
        /// <param name="userInfo">A dict containing custom user info to be included in metricSpec</param>
        /// <param name="modelParams">A dict containing custom model params to be included in modelSpec</param>
        /// <param name="metricName">Name of the metric</param>
        /// <param name="resourceName">Name of the resource with which the metric is associated</param>
        /// <returns>model info dictionary from the result of the _models POST request on success.</returns>
        public static Map<string, object> CreateCustomHtmModel(string host, string apiKey, string metricName, string resourceName, string userInfo, ModelParams modelParams)
        {
            CreateModelRequest mParams = new CreateModelRequest();
            mParams.DataSource = "custom";
            mParams.MetricSpec = new CustomMetricSpec
            {
                Metric = metricName,
                Resource = resourceName,
                UserInfo = userInfo
            };
            mParams.ModelParams = modelParams;
            return CreateHtmModel(host, apiKey, mParams);
        }
    }

    public class MetricsConfiguration : Dictionary<string, MetricConfigurationEntry>
    {

    }

    public class MetricConfigurationEntry
    {
        [JsonProperty("metrics")]
        public Dictionary<string, MetricConfigurationEntryData> Metrics { get; set; }
        [JsonProperty("stockExchange")]
        public string StockExchange { get; set; }
        [JsonProperty("symbol")]
        public string Symbol { get; set; }
    }

    public class MetricConfigurationEntryData
    {
        [JsonProperty("metricType")]
        public string MetricType { get; set; }
        [JsonProperty("metricTypeName")]
        public string MetricTypeName { get; set; }
        [JsonProperty("modelParams")]
        public Dictionary<string, object> ModelParams { get; set; }
        [JsonProperty("provider")]
        public string Provider { get; set; }
        [JsonProperty("sampleKey")]
        public string SampleKey { get; set; }
        [JsonProperty("screenNames")]
        public string[] ScreenNames { get; set; }
    }

    public class ModelsControllerApi
    {

        /// <summary>
        /// POST /_models
        /// 
        /// Data: Use the metric as returned by the datasource metric list.
        /// For example, create a custom model, include the following data in the
        /// POST request (uid is the same for the metric and model):
        /// 
        /// ::
        ///  {
        ///      "uid": "2a123bb1dd4d46e7a806d62efc29cbb9",
        ///      "datasource": "custom",
        ///      "min": 0.0,
        ///      "max": 5000.0
        ///  }
        /// 
        /// The "min" and "max" options are optional.
        /// </summary>
        /// <param name="modelId"></param>
        /// <param name="model"></param>
        /// <returns></returns>
        public object Put(string modelId, CreateModelRequest[] model)
        {
            if (modelId != null)
            {
                // ModelHandler is overloaded to handle both single-model requests, and
                // multiple-model requests.  As a result, if a user makes a POST, or PUT
                // request, it's possible that the request can be routed to this handler
                // if the url pattern matches.  This specific POST handler is not meant
                // to operate on a known model, therefore, raise an exception, and return
                // a `405 Method Not Allowed` response.
                //throw new NotAllowedResponse(new { result = "Not supported" });
                throw new NotSupportedException();
            }
            List<Metric> response = new List<Metric>();
            if (model != null)
            {
                var request = model;

                foreach (var nativeMetric in request)
                {
                    try
                    {
                        //Validate(nativeMetric);
                    }
                    catch (Exception)
                    {

                        throw;
                    }


                }
            }
            else
            {
                // Metric data is missing
                throw new NotImplementedException("bad request");
            }

            try
            {
                // AddStandardHeaders() // TODO: check what this does.
                var metricRowList = CreateModels(model);
                List<Metric> metricDictList = metricRowList.Select(FormatMetricRowProxy).ToList();
                response = metricDictList;

                // return Created(response);
                return response;
            }
            catch (Exception)
            {
                // TODO: log as error
                throw;
            }
        }

        private Metric FormatMetricRowProxy(Metric metricObj)
        {
            string displayName;
            if (metricObj.TagName != null && metricObj.TagName.Length > 0)
            {
                displayName = string.Format("{0} ({1})", metricObj.TagName, metricObj.Server);
            }
            else
            {
                displayName = metricObj.Server;
            }
            var parameters = metricObj.Parameters;

            string[] allowedKeys = GetMetricDisplayFields();

            Metric metricDict = metricObj.Clone(allowedKeys);
            metricDict.Parameters = parameters;
            metricDict.DisplayName = displayName;

            return metricDict;
        }

        public string[] GetMetricDisplayFields()
        {
            return new[]
            {
                "uid", "datasource", "name",
                "description",
                "server",
                "location",
                "parameters",
                "status",
                "message",
                "lastTimestamp",
                "pollInterval",
                "tagName",
                "lastRowid"
            };
        }

        private static List<Metric> CreateModels(CreateModelRequest[] request = null)
        {
            if (request != null)
            {
                List<Metric> response = new List<Metric>();
                foreach (var nativeMetric in request)
                {
                    try
                    {
                        response.Add(ModelHandler.CreateModel(nativeMetric));
                    }
                    catch (Exception)
                    {
                        // response.append("Model failed during creation. Please try again.")
                        throw;
                    }
                }
                return response;
            }
            throw new NotImplementedException("bad request");
        }
    }

    internal class ModelHandler
    {
        public static Metric CreateModel(CreateModelRequest modelSpec = null)
        {
            if (modelSpec == null) throw new ArgumentNullException("modelSpec");
            bool importing = false;
            if (modelSpec.DataSource == "custom")
            {
                if (modelSpec.Data != null)
                {
                    importing = true;
                }
            }
            string metricId = null;
            try
            {
                var adapter = DataAdapterFactory.CreateDatasourceAdapter(modelSpec.DataSource);
                try
                {
                    if (importing)
                    {
                        metricId = adapter.ImportModel(modelSpec);
                    }
                    else
                    {
                        metricId = adapter.MonitorMetric(modelSpec);
                    }
                }
                catch (MetricAlreadyMonitored e)
                {
                    metricId = e.Uid;
                }
            }
            catch (Exception e) // MetricNotSupportedError
            {
                throw;
            }
            return RepositoryFactory.Metric.GetMetric(metricId);
        }


    }

    public static class DataAdapterFactory
    {
        private static List<IDataSourceAdapter> _adapterRegistry = new List<IDataSourceAdapter>();
        /// <summary>
        /// Factory for Datasource adapters
        /// </summary>
        /// <param name="dataSource">datasource (e.g., "cloudwatch", "custom", ...)</param>
        /// <returns></returns>
        public static IDataSourceAdapter CreateDatasourceAdapter(string dataSource)
        {
            if (dataSource == "custom")
            {
                return new CustomDatasourceAdapter();
            }
            throw new NotImplementedException();
        }

        public static void RegisterDatasourceAdapter(IDataSourceAdapter clientCls)
        {
            if (!_adapterRegistry.Contains(clientCls))
            {
                _adapterRegistry.Add(clientCls);
            }
        }
    }

    public interface IDataSourceAdapter
    {
        string ImportModel(ModelSpec modelSpec);
        string MonitorMetric(ModelSpec modelSpec);
    }

    // https://github.com/numenta/numenta-apps/blob/master/htmengine/htmengine/adapters/datasource/custom/__init__.py
    public class CustomDatasourceAdapter : IDataSourceAdapter
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(CustomDatasourceAdapter));
        /// <summary>
        /// Minimum records needed before creating a model; assumes 24 hours worth of 5-minute data samples
        /// </summary>
        private const int MODEL_CREATION_RECORD_THRESHOLD = (60 / 5) * 24;

        // Default metric period value to use when it's unknown
        // TODO: Should we use 0 since it's unknown "unknown" or default to 5 min?
        // Consider potential impact on web charts, htm-it-mobile
        public const int DEFAULT_METRIC_PERIOD = 300;  // 300 sec = 5 min

        public CustomDatasourceAdapter()
        {

        }

        public string ImportModel(ModelSpec modelSpec)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Start monitoring a metric; perform model creation logic specific to custom metrics.
        /// Start the model if possible: this will happen if modelParams includes both
        /// "min" and "max" or there is enough data to estimate them.
        /// </summary>
        /// <param name="modelSpec">model specification for HTM model; per ``model_spec_schema.json`` 
        /// with the ``metricSpec`` property per ``custom_metric_spec_schema.json``</param>
        /// <remarks>
        /// 1st variant: `uid` is the unique id of an existing metric;
        /// # TODO: it would be preferable to punt on this variant, and refer
        /// #  to custom metric by name in modelSpec for consistency with
        /// # import/export. Web GUI uses metric name; some tests use this variant,
        /// # though.
        /// {
        ///     "datasource": "custom",
        ///     "metricSpec": {
        ///         "uid": "4a833e2294494b4fbc5004e03bad45b6",
        ///         "unit": "Count",  # optional
        ///         "resource": "prod.web1",  # optional
        ///         "userInfo": {"symbol": "<TICKER_SYMBOL>"} # optional
        ///     },
        ///     # Optional model params
        ///     "modelParams": {
        ///         "min": min-value,  # optional
        ///         "max": max-value  # optional
        ///     }
        /// }
        ///  ::
        ///  2nd variant: `metric` is the unique name of the metric; a new custom
        ///  metric row will be created with this name, if it doesn't exit
        ///    {
        ///      "datasource": "custom",
        ///      "metricSpec": {
        ///        "metric": "prod.web.14.memory",
        ///        "unit": "Count",  # optional
        ///        "resource": "prod.web1",  # optional
        ///        "userInfo": {"symbol": "<TICKER_SYMBOL>"} # optional
        ///      },
        ///      # Optional model params
        ///      "modelParams": {
        ///        "min": min-value,  # optional
        ///        "max": max-value  # optional
        ///      }
        ///    }
        /// </remarks>
        /// <returns>datasource-specific unique model identifier</returns>
        public string MonitorMetric(ModelSpec modelSpec)
        {
            var metricSpec = modelSpec.MetricSpec;

            string metricId;
            if (metricSpec.Uid != null)
            {
                // Via metric ID
                metricId = metricSpec.Uid;
                // Convert modelSpec to canonical form
                modelSpec = modelSpec.Clone() as ModelSpec;
                modelSpec.MetricSpec.Uid = null;
                ((CustomMetricSpec)modelSpec.MetricSpec).Metric = RepositoryFactory.Metric.GetMetric(metricId).Name;
            }
            else if (!string.IsNullOrWhiteSpace(metricSpec.Metric))
            {
                // Via metric name
                metricId = CreateMetric(metricSpec.Metric);
            }
            else
            {
                throw new InvalidOperationException(string.Format("Neither uid nor metric name present in metricSpec; modelSpec={0}", modelSpec));
            }

            var modelParams = modelSpec.ModelParams ?? new ModelParams();
            var minVal = modelParams.Min;
            var maxVal = modelParams.Max;
            var minResolution = modelParams.MinResolution;
            if (minVal.HasValue != maxVal.HasValue)
            {
                throw new InvalidOperationException(string.Format("min and max params must both be None or non-None; metric={0}; modelSpec={1}", metricId, modelSpec));
            }

            // Start monitoring
            if (!minVal.HasValue || !maxVal.HasValue)
            {
                minVal = maxVal = null;
            }

            int numDataRows = RepositoryFactory.Metric.GetMetricDataCount(metricId);
            if (numDataRows > MODEL_CREATION_RECORD_THRESHOLD)
            {
                var mstats = GetMetricStatistics(metricId);
                _log.InfoFormat("MonitorMetric: trigger numDataRows={0}, stats={1}", numDataRows, mstats);
                minVal = mstats.Min;
                maxVal = mstats.Max;
            }

            var stats = new MetricStatistic(min: minVal, max: maxVal, minResolution: minResolution);
            _log.DebugFormat("monitorMetric: metric={0}, stats={1}", metricId, stats);

            var swarmParams = ScalarMetricUtils.GenerateSwarmParams(stats);

            StartMonitoringWithRetries(metricId, modelSpec, swarmParams);

            return metricId;
        }

        /// <summary>
        /// Perform the start-monitoring operation atomically/reliably
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="modelSpec">same as `modelSpec`` from `monitorMetric`</param>
        /// <param name="swarmParams">object returned by scalar_metric_utils.generateSwarmParams()</param>
        private void StartMonitoringWithRetries(string metricId, ModelSpec modelSpec, BestSingleMetricAnomalyParamsDescription swarmParams)
        {
            var metricObj = RepositoryFactory.Metric.GetMetric(metricId);
            if (metricObj.DataSource != "custom")
            {
                throw new InvalidOperationException("Not an HTM metric");
            }
            if (metricObj.Status != MetricStatus.Unmonitored)
            {
                _log.InfoFormat("MonitorMetric: already monitored; metric={0}", metricObj);
                throw new MetricAlreadyMonitored(metricId, "Custom metric={0} is already monitored by model={1}", metricObj.Name, metricObj);
            }
            // Save model specification in metric row
            var update = new Map<string, object>
            {
                {
                    "parameters",
                    JsonConvert.SerializeObject(modelSpec, new JsonSerializerSettings
                    {
                        TypeNameHandling = TypeNameHandling.All
                    })
                }
            };
            var instanceName = GetInstanceNameForModelSpec(modelSpec);
            if (instanceName != null)
            {
                update["server"] = instanceName;
            }
            RepositoryFactory.Metric.UpdateMetricColumns(metricId, update);

            var modelStarted = ScalarMetricUtils.StartMonitoring(metricId, swarmParams, logger: _log);
            if (modelStarted)
            {
                ScalarMetricUtils.SendBacklogDataToModel(metricId, logger: _log);
            }
        }

        /// <summary>
        /// Get canonical instance name from a model spec
        /// </summary>
        /// <param name="modelSpec">Datasource-specific model specification</param>
        /// <returns>Canonical instance name</returns>
        private string GetInstanceNameForModelSpec(ModelSpec modelSpec)
        {
            return modelSpec.MetricSpec.Resource;
        }

        /// <summary>
        /// Get metric data statistics
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <returns>a dictionary with the metric's statistics  {"min": <min-value>, "max": <max-value>}</returns>
        private MetricStatistic GetMetricStatistics(string metricId)
        {
            MetricStatistic stats = RepositoryFactory.Metric.GetMetricStats(metricId);

            var minVal = stats.Min;
            var maxVal = stats.Max;
            if (maxVal < minVal)
            {
                _log.WarnFormat("Max encoder value ({0}) is not greater than min ({1}).",
                    maxVal, minVal);
                maxVal = minVal + 1;
            }

            // Add a 20% buffer on both ends of the range
            // (borrowed from legacy custom adapter)
            var buff = (maxVal - minVal) * 0.2;
            minVal -= buff;
            maxVal += buff;

            _log.DebugFormat("getMetricStatistics for metric={0}: minVal={1},maxVal={2}", metricId, minVal, maxVal);

            MetricStatistic stat = new MetricStatistic(minVal, maxVal, null);
            return stat;
        }

        /// <summary>
        /// Create scalar HTM metric if it doesn't exist
        /// NOTE: this method is specific to HTM Metrics, where metric creation happens separately from model creation.
        /// </summary>
        /// <param name="metricName">name of the HTM metric</param>
        /// <returns>unique metric identifier</returns>
        private string CreateMetric(string metricName)
        {
            var resource = MakeDefaultResourceName(metricName);

            Metric metricObj = RepositoryFactory.Metric.GetCustomMetricByName(metricName);

            if (metricObj != null)
            {
                throw new MetricAlreadyExists(metricObj.Uid, "Custom metric with matching name already exists");
            }

            Metric metricDict = RepositoryFactory.Metric.AddMetric(name: metricName, description: "Custom metric " + metricName, server: resource, location: "", pollInterval: DEFAULT_METRIC_PERIOD, status: MetricStatus.Unmonitored, datasource: "custom");

            return metricDict.Uid;
        }

        /// <summary>
        /// Construct the default resource name for a given metric
        /// </summary>
        /// <param name="metricName">unique name of the metric</param>
        /// <returns></returns>
        private static string MakeDefaultResourceName(string metricName)
        {
            return metricName;
        }
    }

    /// <summary>
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/runtime/scalar_metric_utils.py
    /// </summary>
    public class ScalarMetricUtils
    {
        /// <summary>
        /// Generate parameters for creating a model
        /// </summary>
        /// <param name="stats">dict with "min", "max" and optional "minResolution"; values must be integer, float or null.</param>
        /// <returns>if either minVal or maxVal is None, returns None; otherwise returns
        /// swarmParams object that is suitable for passing to startMonitoring and
        /// startModel</returns>
        public static BestSingleMetricAnomalyParamsDescription GenerateSwarmParams(MetricStatistic stats)
        {
            var minVal = stats.Min;
            var maxVal = stats.Max;
            var minResolution = stats.MinResolution;
            if (minVal == null || maxVal == null)
            {
                return null;
            }
            // Create possible swarm parameters based on metric data
            BestSingleMetricAnomalyParamsDescription swarmParams =
                GetScalarMetricWithTimeOfDayAnomalyParams(metricData: new[] { 0 }, minVal: minVal, maxVal: maxVal, minResolution: minResolution);

            // 
            swarmParams.inputRecordSchema = new Map<string, Tuple<FieldMetaType, SensorFlags>>
            {
                {"c0", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.DateTime, SensorFlags.Timestamp )} ,
                {"c1", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.Float, SensorFlags.Blank ) },
            };
            /*
             swarmParams["inputRecordSchema"] = (
                fieldmeta.FieldMetaInfo("c0", fieldmeta.FieldMetaType.datetime,
                                        fieldmeta.FieldMetaSpecial.timestamp),
                fieldmeta.FieldMetaInfo("c1", fieldmeta.FieldMetaType.float,
                                        fieldmeta.FieldMetaSpecial.none),
              )
             */
            return swarmParams;
        }

        // https://github.com/numenta/nupic/blob/a5a7f52e39e30c5356c561547fc6ac3ffd99588c/src/nupic/frameworks/opf/common_models/cluster_params.py

        /// <summary>
        /// Return a dict that can be used to create an anomaly model via OPF's ModelFactory.
        /// </summary>
        /// <param name="metricData">numpy array of metric data. Used to calculate minVal and maxVal if either is unspecified</param>
        /// <param name="minVal">Minimum value of metric. Used to set up encoders. If null will be derived from metricData.</param>
        /// <param name="maxVal">Maximum value of metric. Used to set up input encoders. If null will be derived from metricData</param>
        /// <param name="minResolution">minimum resolution of metric. Used to set up encoders.If None, will use default value of 0.001.</param>
        /// <returns>
        /// a dict containing "modelConfig" and "inferenceArgs" top-level properties.
        /// The value of the "modelConfig" property is for passing to the OPF `ModelFactory.create()` method as the `modelConfig` parameter.
        /// The "inferenceArgs" property is for passing to the resulting model's `enableInference()` method as the inferenceArgs parameter.
        /// 
        /// NOTE: the timestamp field corresponds to input "c0"; 
        /// the predicted field corresponds to input "c1".
        /// </returns>
        /// <remarks>
        /// Example:
        ///    from nupic.frameworks.opf.modelfactory import ModelFactory
        /// 
        ///    from nupic.frameworks.opf.common_models.cluster_params import (
        /// 
        ///      getScalarMetricWithTimeOfDayAnomalyParams)
        ///  params = getScalarMetricWithTimeOfDayAnomalyParams(
        ///    metricData=[0],
        ///    minVal= 0.0,
        ///    maxVal= 100.0)
        ///  model = ModelFactory.create(modelConfig=params["modelConfig"])
        ///  model.enableLearning()
        ///  model.enableInference(params["inferenceArgs"])
        /// </remarks>
        private static BestSingleMetricAnomalyParamsDescription GetScalarMetricWithTimeOfDayAnomalyParams(int[] metricData, double? minVal, double? maxVal, double? minResolution)
        {
            // Default values
            if (!minResolution.HasValue) minResolution = 0.001;
            // Compute min and/or max from the data if not specified
            if (!minVal.HasValue || !maxVal.HasValue)
            {
                var rangeGen = RangeGen(metricData);
                if (!minVal.HasValue)
                {
                    minVal = (double?)rangeGen.Get(0);
                }
                if (!maxVal.HasValue)
                {
                    maxVal = (double?)rangeGen.Get(1);
                }
            }
            //  Handle the corner case where the incoming min and max are the same
            if (minVal == maxVal)
            {
                maxVal = minVal + 1;
            }
            // Load model parameters and update encoder params
            var paramSet = BestSingleMetricAnomalyParamsDescription.BestSingleMetricAnomalyParams;
            FixupRandomEncoderParams(paramSet, minVal, maxVal, minResolution);
            return paramSet;
        }

        /// <summary>
        /// Return reasonable min/max values to use given the data.
        /// </summary>
        /// <returns></returns>
        private static Tuple RangeGen(int[] data, int std = 1)
        {
            double average = data.Average();
            double sumOfSquaresOfDifferences = data.Select(val => (val - average) * (val - average)).Sum();
            double dataStd = Math.Sqrt(sumOfSquaresOfDifferences / data.Length);

            if (Math.Abs(dataStd) < double.Epsilon)
            {
                dataStd = 1;
            }
            double minval = ArrayUtils.Min(data) - std * dataStd;
            double maxval = ArrayUtils.Max(data) + std * dataStd;
            return new Tuple(minval, maxval);
        }

        /// <summary>
        /// Given model params, figure out the correct parameters for the RandomDistributed encoder.
        /// Modifies params in place.
        /// </summary>
        /// <param name="paramSet"></param>
        /// <param name="minVal"></param>
        /// <param name="maxVal"></param>
        /// <param name="minResolution"></param>
        private static void FixupRandomEncoderParams(BestSingleMetricAnomalyParamsDescription paramSet, double? minVal, double? maxVal,
            double? minResolution)
        {
            Map<string, Map<string, object>> encodersDict = paramSet.modelConfig.modelParams.sensorParams.encoders;
            foreach (var encoder in encodersDict.Values)
            {
                if (encoder != null)
                {
                    if (encoder["type"] == "RandomDistributedScalarEncoder")
                    {
                        var resolution = Math.Max(minResolution.Value,
                            (maxVal.Value - minVal.Value) / (double)encoder["numBuckets"]);
                        encoder.Remove("numBuckets");
                        encodersDict["c1"]["resolution"] = resolution;
                    }
                }
            }
        }
        /// <summary>
        /// Start monitoring an UNMONITORED metric.
        /// NOTE: typically called either inside a transaction and/or with locked tables
        /// Starts the CLA model if provided non-None swarmParams; otherwise defers model
        /// creation to a later time and places the metric in MetricStatus.PENDING_DATA state.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="swarmParams">swarmParams generated via scalar_metric_utils.generateSwarmParams() or None.</param>
        /// <param name="logger">logger object</param>
        /// <returns> True if model was started; False if not</returns>
        public static bool StartMonitoring(string metricId, BestSingleMetricAnomalyParamsDescription swarmParams, ILog logger)
        {
            bool modelStarted = false;
            DateTime startTime = DateTime.Now;
            Metric metricObj = RepositoryFactory.Metric.GetMetric(metricId);
            Debug.Assert(metricObj.Status == MetricStatus.Unmonitored);

            if (swarmParams != null)
            {
                // We have swarmParams, so start the model
                modelStarted = StartModelHelper(metricObj, swarmParams, logger);
            }
            else
            {
                // Put the metric into the PENDING_DATA state until enough data arrives for
                // stats
                MetricStatus refStatus = metricObj.Status;
                RepositoryFactory.Metric.SetMetricStatus(metricId, MetricStatus.PendingData, refStatus: refStatus);
                // refresh
                var metricStatus = RepositoryFactory.Metric.GetMetric(metricId).Status;
                if (metricStatus == MetricStatus.PendingData)
                {
                    logger.InfoFormat(
                        "startMonitoring: promoted metric to model in PENDING_DATA; metric={0}; duration={1}",
                        metricId, DateTime.Now - startTime);
                }
                else
                {
                    throw new InvalidOperationException("metric status change failed");
                }
            }
            return modelStarted;
        }
        /// <summary>
        /// Start the model
        /// </summary>
        /// <param name="metricObj">metric, freshly-loaded</param>
        /// <param name="swarmParams">non-None swarmParams generated via GenerateSwarmParams().</param>
        /// <param name="logger">logger object</param>
        /// <returns>True if model was started; False if not</returns>
        private static bool StartModelHelper(Metric metricObj, BestSingleMetricAnomalyParamsDescription swarmParams, ILog logger)
        {
            if (swarmParams == null)
                throw new ArgumentNullException("swarmParams", "startModel: 'swarmParams' must be non-None: metric=" + metricObj.Uid);

            string metricName = metricObj.Name;

            if (!new[] { MetricStatus.Unmonitored, MetricStatus.PendingData, }.Contains(metricObj.Status))
            {
                if (new[] { MetricStatus.CreatePending, MetricStatus.Active, }.Contains(metricObj.Status))
                {
                    return false;
                }
                logger.ErrorFormat("Unexpected metric status; metric={0}", metricObj.Uid);
                throw new InvalidOperationException(string.Format("startModel: unexpected metric status; metric={0}", metricObj.Uid));
            }

            var startTime = DateTime.Now;

            // Save swarm parameters and update metric status
            MetricStatus refStatus = metricObj.Status;
            RepositoryFactory.Metric.UpdateMetricColumnsForRefStatus(metricObj.Uid, refStatus,
                new Map<string, object>
                {
                    {"status", MetricStatus.CreatePending},
                    {"modelParams", JsonConvert.SerializeObject(swarmParams)}
                });

            metricObj = RepositoryFactory.Metric.GetMetric(metricObj.Uid);

            if (metricObj.Status != MetricStatus.CreatePending)
            {
                throw new MetricsChangeError("startModel: unable to start model={0}; metric status morphed from {1} to {2}",
                    metricObj.Uid, refStatus, metricObj.Status);
            }

            // Request to create the CLA Model
            try
            {
                ModelSwapperUtils.CreateHtmModel(metricObj.Uid, swarmParams);
            }
            catch (Exception e)
            {
                logger.ErrorFormat("startModel: createHTMModel failed. -> {0}", e);
                RepositoryFactory.Metric.SetMetricStatus(metricObj.Uid, MetricStatus.Error, message: e.ToString());
                throw;
            }

            logger.InfoFormat("startModel: started model uid={0}, name={1}; duration={2}",
                metricObj.Uid, metricName, DateTime.Now - startTime);

            return true;
        }

        /// <summary>
        /// Send backlog data to OPF/CLA model. Do not call this before starting the  model.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="logger">logger object</param>
        public static void SendBacklogDataToModel(string metricId, ILog logger)
        {
            List<ModelInputRow> backlogData = RepositoryFactory.Metric.GetMetricData(metricId)
                .Select(md => new ModelInputRow(rowId: md.Rowid,
                    data: new List<string>
                    {
                        md.Timestamp.ToString("G"),
                        md.MetricValue.ToString(NumberFormatInfo.InvariantInfo)
                    }))
                .ToList();

            if (backlogData != null && backlogData.Any())
            {
                ModelSwapperInterface modelSwapper = new ModelSwapperInterface();

                ModelDataFeeder.SendInputRowsToModel(modelId: metricId, inputRows: backlogData, batchSize: 100,
                    modelSwapper: modelSwapper, logger: logger, profiling: logger.IsDebugEnabled);
            }

            logger.InfoFormat("sendBacklogDataToModel: sent {0} backlog data rows to model={1}",
                backlogData.Count, metricId);
        }
    }

    #region Swarming model stuff

    public class BestSingleMetricAnomalyParamsDescription : DescriptionBase
    {
        public BestSingleMetricAnomalyParamsDescription()
        {
            inferenceArgs = new Map<string, object>
            {
                {"predictionSteps", new[] {1}},
                {"predictedField", "c1"},
                {"inputPredictedField", "auto"}
            };

            var config = new DescriptionConfigModel
            {
                // Type of model that the rest of these parameters apply to.
                model = "CLA",

                // Version that specifies the format of the config.
                version = 1,

                // Intermediate variables used to compute fields in modelParams and also
                // referenced from the control section.
                aggregationInfo = new Map<string, object>
                        {
                            {"seconds", 0},
                            {
                                "fields", new Map<string, object>()
                            },
                            {"months", 0},
                            {"days", 0},
                            {"years", 0},
                            {"hours", 0},
                            {"microseconds", 0},
                            {"weeks", 0},
                            {"minutes", 0},
                            {"milliseconds", 0},
                        },

                predictAheadTime = null,

                // Model parameter dictionary.
                modelParams = new ModelDescriptionParamsDescrModel
                {
                    // The type of inference that this model will perform
                    inferenceType = InferenceType.TemporalAnomaly,

                    sensorParams = new SensorParamsDescrModel
                    {
                        // Sensor diagnostic output verbosity control;
                        // if > 0: sensor region will print out on screen what it"s sensing
                        // at each step 0: silent; >=1: some info; >=2: more info;
                        // >=3: even more info (see compute() in py/regions/RecordSensor.py)
                        verbosity = 0,

                        // Example:
                        //     dsEncoderSchema = [
                        //       DeferredDictLookup("__field_name_encoder"),
                        //     ],
                        //
                        // (value generated from DS_ENCODER_SCHEMA)
                        encoders = new Map<string, Map<string, object>>
                                {
                                    {
                                        "c0_timeOfDay", new Map<string, object>
                                        {
                                            {"dayOfWeek", new Tuple(21, 9.49)},
                                            {"fieldname", "c0"},
                                            {"name", "c0"},
                                            {"type", "DateEncoder"}
                                        }
                                    },
                                    {
                                        "c0_dayOfWeek", null
                                    },
                                    {
                                        "c0_weekend", null
                                    },
                                    {
                                        "c1", new Map<string, object>
                                        {
                                            {"fieldname", "c1"},
                                            {"name", "c1"},
                                            {"type", "RandomDistributedScalarEncoder"},
                                            {"numBuckets", 130.0 }
                                        }
                                    }
                                },

                        // A dictionary specifying the period for automatically-generated
                        // resets from a RecordSensor;
                        //
                        // None = disable automatically-generated resets (also disabled if
                        // all of the specified values evaluate to 0).
                        // Valid keys is the desired combination of the following:
                        //   days, hours, minutes, seconds, milliseconds, microseconds, weeks
                        //
                        // Example for 1.5 days: sensorAutoReset = dict(days=1,hours=12),
                        //
                        // (value generated from SENSOR_AUTO_RESET)
                        sensorAutoReset = null,
                    },

                    spEnable = true,

                    spParams = new SpatialParamsDescr
                    {
                        // SP diagnostic output verbosity control;
                        // 0: silent; >=1: some info; >=2: more info;
                        spVerbosity = 0,

                        globalInhibition = true,

                        // Number of cell columns in the cortical region (same number for
                        // SP and TP)
                        // (see also tpNCellsPerCol)
                        columnCount = new int[] { 2048 },

                        inputWidth = new int[] { 0 },

                        // SP inhibition control (absolute value);
                        // Maximum number of active columns in the SP region"s output (when
                        // there are more, the weaker ones are suppressed)
                        numActiveColumnsPerInhArea = 40.0,

                        seed = 1956,

                        // potentialPct
                        // What percent of the columns"s receptive field is available
                        // for potential synapses. At initialization time, we will
                        // choose potentialPct * (2*potentialRadius+1)^2
                        potentialPct = 0.8,

                        // The default connected threshold. Any synapse whose
                        // permanence value is above the connected threshold is
                        // a "connected synapse", meaning it can contribute to the
                        // cell"s firing. Typical value is 0.10. Cells whose activity
                        // level before inhibition falls below minDutyCycleBeforeInh
                        // will have their own internal synPermConnectedCell
                        // threshold set below this default value.
                        // (This concept applies to both SP and TP and so "cells"
                        // is correct here as opposed to "columns")
                        synPermConnected = 0.2,

                        synPermActiveInc = 0.003,

                        synPermInactiveDec = 0.0005,

                        maxBoost = 1.0
                    },

                    // Controls whether TP is enabled or disabled;
                    // TP is necessary for making temporal predictions, such as predicting
                    // the next inputs.  Without TP, the model is only capable of
                    // reconstructing missing sensor inputs (via SP).
                    tpEnable = true,

                    tpParams = new TemporalParamsDescr
                    {
                        // TP diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        // (see verbosity in nupic/trunk/py/nupic/research/TP.py and TP10X*.py)
                        verbosity = 0,

                        // Number of cell columns in the cortical region (same number for
                        // SP and TP)
                        // (see also tpNCellsPerCol)
                        columnCount = new[] { 2048 },

                        // The number of cells (i.e., states), allocated per column.
                        cellsPerColumn = 32,

                        inputWidth = new[] { 2048 },

                        seed = 1960,

                        // Temporal Pooler implementation selector (see _getTPClass in
                        // CLARegion.py).
                        temporalImp = "cpp",

                        // New Synapse formation count
                        // NOTE: If None, use spNumActivePerInhArea
                        //
                        // TODO: need better explanation
                        newSynapseCount = 20,

                        // Maximum number of synapses per segment
                        //  > 0 for fixed-size CLA
                        // -1 for non-fixed-size CLA
                        //
                        // TODO: for Ron: once the appropriate value is placed in TP
                        // constructor, see if we should eliminate this parameter from
                        // description.py.
                        maxSynapsesPerSegment = 32,

                        // Maximum number of segments per cell
                        //  > 0 for fixed-size CLA
                        // -1 for non-fixed-size CLA
                        //
                        // TODO: for Ron: once the appropriate value is placed in TP
                        // constructor, see if we should eliminate this parameter from
                        // description.py.
                        maxSegmentsPerCell = 128,

                        // Initial Permanence
                        // TODO: need better explanation
                        initialPerm = 0.21,

                        // Permanence Increment
                        permanenceInc = 0.1,

                        // Permanence Decrement
                        // If set to None, will automatically default to tpPermanenceInc
                        // value.
                        permanenceDec = 0.1,

                        globalDecay = 0.0,

                        maxAge = 0,

                        // Minimum number of active synapses for a segment to be considered
                        // during search for the best-matching segments.
                        // None=use default
                        // Replaces: tpMinThreshold
                        minThreshold = 10,

                        // Segment activation threshold.
                        // A segment is active if it has >= tpSegmentActivationThreshold
                        // connected synapses that are active due to infActiveState
                        // None=use default
                        // Replaces: tpActivationThreshold
                        activationThreshold = 13,

                        outputType = "normal",

                        // "Pay Attention Mode" length. This tells the TP how many new
                        // elements to append to the end of a learned sequence at a time.
                        // Smaller values are better for datasets with short sequences,
                        // higher values are better for datasets with long sequences.
                        pamLength = 3,
                    },

                    clEnable = false,

                    clParams = new ClassifierParamsDescr
                    {
                        regionName = typeof(CLAClassifier).AssemblyQualifiedName,// "CLAClassifierRegion",

                        // Classifier diagnostic output verbosity control;
                        // 0: silent; [1..6]: increasing levels of verbosity
                        clVerbosity = 0,

                        // This controls how fast the classifier learns/forgets. Higher values
                        // make it adapt faster and forget older patterns faster.
                        alpha = 0.035828933612157998,

                        // This is set after the call to updateConfigFromSubConfig and is
                        // computed from the aggregationInfo and predictAheadTime.
                        steps = 1,
                    },
                    anomalyParams = new AnomalyParamsDescr
                    {
                        anomalyCacheRecords = null,
                        autoDetectThreshold = null,
                        autoDetectWaitRecords = 5030
                    },
                    trainSPNetOnlyIfRequested = false,
                }
            };
            // end of config dictionary

            // Adjust base config dictionary for any modifications if imported from a
            // sub-experiment
            updateConfigFromSubConfig(config);
            modelConfig = config;

            // Compute predictionSteps based on the predictAheadTime and the aggregation
            // period, which may be permuted over.
            if (config.predictAheadTime != null)
            {
                int predictionSteps = (int)Math.Round(Utils.aggregationDivide(config.predictAheadTime, config.aggregationInfo));
                Debug.Assert(predictionSteps >= 1);
                config.modelParams.clParams.steps = predictionSteps;
            }


        }

        public void updateConfigFromSubConfig(DescriptionConfigModel config)
        {

        }

        public override IDescription Clone()
        {
            throw new NotImplementedException();
        }

        public override Network.Network BuildNetwork()
        {
            throw new NotImplementedException();
        }

        public override Parameters GetParameters()
        {
            Parameters p = Parameters.GetAllDefaultParameters();

            // Spatial pooling parameters
            SpatialParamsDescr spParams = this.modelConfig.modelParams.spParams;
            TemporalParamsDescr tpParams = this.modelConfig.modelParams.tpParams;

            Parameters.ApplyParametersFromDescription(spParams, p);
            Parameters.ApplyParametersFromDescription(tpParams, p);

            return p;
        }

        public static BestSingleMetricAnomalyParamsDescription BestSingleMetricAnomalyParams
        {
            get { return new BestSingleMetricAnomalyParamsDescription(); }
        }
    }

    public class ModelParams
    {
        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? MinResolution { get; set; }

        public Map<string, object> AnomalyLikelihoodParams { get; set; }
        public DescriptionConfigModel ModelConfig { get; set; }
        public Map<string, object> InferenceArgs { get; set; }
        public Map<string, Tuple<FieldMetaType, SensorFlags>> InputSchema { get; set; }

        public ModelParams Clone()
        {
            return new ModelParams
            {
                Min = Min,
                Max = Max,
                AnomalyLikelihoodParams = new Map<string, object>(AnomalyLikelihoodParams),
                MinResolution = MinResolution,
                ModelConfig = ModelConfig,
                InferenceArgs = new Map<string, object>(InferenceArgs),
                InputSchema = new Map<string, Tuple<FieldMetaType, SensorFlags>>(InputSchema)
            };
        }
    }

    #endregion

    public class MetricStatistic
    {
        public MetricStatistic(double? min, double? max, double? minResolution)
        {
            Min = min;
            Max = max;
            MinResolution = minResolution;
        }

        public double? Min { get; set; }
        public double? Max { get; set; }
        public double? MinResolution { get; set; }
    }

    public class ModelSpec : ICloneable
    {
        public string DataSource { get; set; }

        public MetricSpec MetricSpec { get; set; }
        public object Data { get; set; }
        public ModelParams ModelParams { get; set; }

        public virtual object Clone()
        {
            throw new NotImplementedException();
        }
    }

    public class MetricSpec
    {
        /// <summary>
        /// Unique identifier of metric
        /// </summary>
        public string Uid { get; set; }

        /// <summary>
        /// Metric name
        /// </summary>
        public string Metric { get; set; }

        /// <summary>
        /// Optional identifier of resource that this metric applies to
        /// </summary>
        public string Resource { get; set; }

        public virtual object Clone()
        {
            return new MetricSpec
            {
                Metric = Metric,
                Resource = Resource,
                Uid = Uid
            };
        }
    }

    /// <summary>
    /// Custom-adapter-specific metric specification that is stored in metric row's properties field, 
    /// embedded inside a modelSpec; describes custom datasource's metricSpec property in model_spec_schema.json
    /// </summary>
    public class CustomMetricSpec : MetricSpec
    {

        /// <summary>
        /// Optional user-defined metric data unit name
        /// </summary>
        public string Unit { get; set; }

        /// <summary>
        /// Optional custom user info.
        /// </summary>
        public string UserInfo { get; set; }

        public override object Clone()
        {
            return new CustomMetricSpec
            {
                Metric = Metric,
                Resource = Resource,
                Uid = Uid,
                UserInfo = UserInfo,
                Unit = Unit
            };
        }
    }

    public class CreateModelRequest : ModelSpec
    {
        public override object Clone()
        {
            CreateModelRequest req = new CreateModelRequest();
            req.DataSource = DataSource;
            req.Data = Data;
            req.MetricSpec = MetricSpec?.Clone() as MetricSpec;
            req.ModelParams = ModelParams?.Clone() as ModelParams;
            return req;
        }
    }

    #region Repository stuff

    public static class RepositoryFactory
    {
        private static IMetricRepository _metricRepository;

        static RepositoryFactory()
        {
            _metricRepository = new MetricMemRepository();
        }

        public static IMetricRepository Metric { get { return _metricRepository; } }
    }

    public interface IMetricRepository
    {
        event Action<Metric> MetricAdded;

        Metric GetMetric(string metricId);
        int GetMetricDataCount(string metricId);
        /// <summary>
        /// Update existing metric
        /// </summary>
        /// <param name="metricId"></param>
        /// <param name="update">fields with values to update</param>
        void UpdateMetricColumns(string metricId, Map<string, object> update);
        void UpdateMetricColumnsForRefStatus(string uid, MetricStatus refStatus, Map<string, object> objects);

        MetricStatistic GetMetricStats(string metricId);
        Metric GetCustomMetricByName(string metricName);

        /// <summary>
        /// Add metric
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="datasource"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="server"></param>
        /// <param name="location"></param>
        /// <param name="parameters"></param>
        /// <param name="status"></param>
        /// <param name="message"></param>
        /// <param name="collectorError"></param>
        /// <param name="lastTimestamp"></param>
        /// <param name="pollInterval"></param>
        /// <param name="tagName"></param>
        /// <param name="modelParams"></param>
        /// <param name="lastRowId"></param>
        /// <returns>Key-value pairs for inserted columns and values</returns>
        Metric AddMetric(string uid = null, string datasource = null, string name = null,
            string description = null, string server = null, string location = null,
            string parameters = null, MetricStatus? status = null, string message = null, string collectorError = null,
            DateTime? lastTimestamp = null, int? pollInterval = null, string tagName = null, string modelParams = null,
            long lastRowId = 0);

        int GetProcessedMetricDataCount(string metricId);
        List<MetricData> GetMetricDataWithRawAnomalyScoresTail(string metricId, int limit);
        /// <summary>
        /// Set metric status
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="status">metric status value to set</param>
        /// <param name="message">message to set; clears message field by default</param>
        /// <param name="refStatus">reference status; if None (default), the requested values
        /// will be set regardless of the current value of the row's status field;
        /// otherwise, the status will be updated only if the metric row's current
        /// status is refStatus(checked automically). If the current value was not
        /// refStatus, then upon return, the reloaded metric dao's `status`
        /// attribute will reflect the status value that was in the metric row at
        /// the time the update was attempted instead of the requested status value.</param>
        void SetMetricStatus(string metricId, MetricStatus status, string message = null, MetricStatus? refStatus = null);

        List<MetricData> GetMetricData(string metricId);

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        List<MetricData> AddMetricData(string metricId, List<Tuple<double, DateTime>> data);
    }

    /// <summary>
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/repository/queries.py
    /// </summary>
    public class MetricSqlRepository : IMetricRepository
    {
        public event Action<Metric> MetricAdded;

        public Metric GetMetric(string metricId)
        {
            // sel = select(fields).where(schema.metric.c.uid == metricId).First()
            throw new NotImplementedException();
        }

        public int GetMetricDataCount(string metricId)
        {
            //sel = (select([func.count()], from_obj=schema.metric_data).where(schema.metric_data.c.uid == metricId)).first()[0]
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update existing metric
        /// </summary>
        /// <param name="metricId"></param>
        /// <param name="update">fields with values to update</param>
        public void UpdateMetricColumns(string metricId, Map<string, object> update)
        {
            throw new NotImplementedException();
        }

        public void UpdateMetricColumnsForRefStatus(string uid, MetricStatus refStatus, Map<string, object> objects)
        {
            throw new NotImplementedException();
        }

        public MetricStatistic GetMetricStats(string metricId)
        {
            /*
             sel = (select([func.min(schema.metric_data.c.metric_value),
                            func.max(schema.metric_data.c.metric_value)],
                        from_obj=schema.metric_data)
                .where(schema.metric_data.c.uid == metricId))

            if result.rowcount > 0: 
                statMin, statMax = result.first().values()
            if statMin is not None and statMax is not None:
                return {"min": statMin, "max": statMax}
             raise MetricStatisticsNotReadyError()
             */
            throw new NotImplementedException();
        }

        public Metric GetCustomMetricByName(string metricName)
        {
            // where = ((schema.metric.c.name == name) & (schema.metric.c.datasource == "custom")).first()
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add metric
        /// </summary>
        /// <param name="uid"></param>
        /// <param name="datasource"></param>
        /// <param name="name"></param>
        /// <param name="description"></param>
        /// <param name="server"></param>
        /// <param name="location"></param>
        /// <param name="parameters"></param>
        /// <param name="status"></param>
        /// <param name="message"></param>
        /// <param name="collectorError"></param>
        /// <param name="lastTimestamp"></param>
        /// <param name="pollInterval"></param>
        /// <param name="tagName"></param>
        /// <param name="modelParams"></param>
        /// <param name="lastRowId"></param>
        /// <returns>Key-value pairs for inserted columns and values</returns>
        public Metric AddMetric(string uid = null, string datasource = null, string name = null,
            string description = null, string server = null, string location = null,
            string parameters = null, MetricStatus? status = null, string message = null, string collectorError = null,
            DateTime? lastTimestamp = null, int? pollInterval = null, string tagName = null, string modelParams = null,
            long lastRowId = 0)
        {
            uid = uid ?? Guid.NewGuid().ToString();

            // TODO: insert into database

            throw new NotImplementedException();
        }

        public int GetProcessedMetricDataCount(string metricId)
        {
            throw new NotImplementedException();
        }

        public List<MetricData> GetMetricDataWithRawAnomalyScoresTail(string metricId, int limit)
        {
            throw new NotImplementedException();
        }

        public void SetMetricStatus(string metricId, MetricStatus status, string message = null, MetricStatus? refStatus = null)
        {
            throw new NotImplementedException();
        }

        public List<MetricData> GetMetricData(string metricId)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        public List<MetricData> AddMetricData(string metricId, List<Tuple<double, DateTime>> data)
        {
            throw new NotImplementedException();
        }
    }

    public class MetricMemRepository : IMetricRepository
    {
        private List<Metric> _metric;
        private List<MetricData> _metricData;
        private int _lastRowId;
        public event Action<Metric> MetricAdded;

        public MetricMemRepository()
        {
            _metric = new List<Metric>();
            _metricData = new List<MetricData>();
            _lastRowId = 0;
        }

        public Metric GetMetric(string metricId)
        {
            return _metric.First(m => m.Uid == metricId);
        }

        public int GetMetricDataCount(string metricId)
        {
            //sel = (select([func.count()], from_obj=schema.metric_data).where(schema.metric_data.c.uid == metricId)).first()[0]
            return _metricData.Count(md => md.MetricId == metricId);
        }

        public void UpdateMetricColumns(string metricId, Map<string, object> fields)
        {
            var metric = _metric.Single(m => m.Uid == metricId);

            var bu = BeanUtil.GetInstance();
            foreach (KeyValuePair<string, object> pair in fields)
            {
                bu.SetSimpleProperty(metric, pair.Key, pair.Value);
            }
        }

        public void UpdateMetricColumnsForRefStatus(string metricId, MetricStatus refStatus, Map<string, object> fields)
        {
            var metric = _metric.Single(m => m.Uid == metricId && m.Status == refStatus);

            var bu = BeanUtil.GetInstance();
            foreach (KeyValuePair<string, object> pair in fields)
            {
                bu.SetSimpleProperty(metric, pair.Key, pair.Value);
            }
        }

        public MetricStatistic GetMetricStats(string metricId)
        {
            if (_metricData.Any(md => md.Uid == metricId))
            {
                var statMin = _metricData.Where(md => md.Uid == metricId).Min(md => md.MetricValue);
                var statMax = _metricData.Where(md => md.Uid == metricId).Max(md => md.MetricValue);
                return new MetricStatistic(statMin, statMax, null);
            }
            throw new InvalidOperationException("MetricStatisticsNotReadyError");
        }

        public Metric GetCustomMetricByName(string metricName)
        {
            return _metric.FirstOrDefault(m => m.Name.Equals(metricName, StringComparison.InvariantCultureIgnoreCase)
                                               && m.DataSource == "custom");
        }

        public Metric AddMetric(string uid = null, string datasource = null, string name = null, string description = null,
            string server = null, string location = null, string parameters = null, MetricStatus? status = null,
            string message = null, string collectorError = null, DateTime? lastTimestamp = null, int? pollInterval = null,
            string tagName = null, string modelParams = null, long lastRowId = 0)
        {
            uid = uid ?? Guid.NewGuid().ToString();

            Metric newMetric = new Metric();

            newMetric.Uid = uid;
            newMetric.DataSource = datasource;
            newMetric.Name = name;
            newMetric.Description = description;
            newMetric.Server = server;
            newMetric.Location = location;
            newMetric.Parameters = parameters;
            newMetric.Status = status.GetValueOrDefault(MetricStatus.Unmonitored);
            newMetric.Message = message;
            //newMetric.c = collectorError;
            newMetric.LastTimeStamp = lastTimestamp;
            newMetric.PollInterval = pollInterval;
            newMetric.TagName = tagName;
            newMetric.ModelParams = modelParams;
            newMetric.LastRowId = lastRowId;

            _metric.Add(newMetric);

            MetricAdded?.Invoke(newMetric);

            return newMetric;
        }

        /// <summary>
        /// Get count of processed MetricData for the given metricId.
        /// </summary>
        /// <param name="metricId"></param>
        /// <returns></returns>
        public int GetProcessedMetricDataCount(string metricId)
        {
            return _metricData.Count(md => md.MetricId == metricId && md.RawAnomalyScore != null);
        }

        /// <summary>
        /// Get MetricData ordered by timestamp, descending
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="limit">Limit on number of results to return</param>
        /// <returns></returns>
        public List<MetricData> GetMetricDataWithRawAnomalyScoresTail(string metricId, int limit)
        {
            return _metricData
                .Where(md => md.Uid == metricId && md.RawAnomalyScore != null)
                .OrderByDescending(md => md.Timestamp)
                .Take(limit)
                .ToList();
        }

        public void SetMetricStatus(string metricId, MetricStatus status, string message = null, MetricStatus? refStatus = null)
        {
            Metric metric;
            if (refStatus != null)
            {
                // Add refStatus match to the predicate
                metric = _metric.Single(m => m.Uid == metricId && m.Status == refStatus);
            }
            else
            {
                metric = _metric.Single(m => m.Uid == metricId);
            }

            metric.Status = status;
            metric.Message = message;
        }

        public List<MetricData> GetMetricData(string metricId)
        {
            return _metricData.Where(md => md.MetricId == metricId).ToList();
        }
        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        public List<MetricData> AddMetricData(string metricId, List<Tuple<double, DateTime>> data)
        {
            int numRows = data.Count;
            if (numRows == 0)
                return null;

            List<MetricData> rows = new List<MetricData>();

            foreach (var pair in data)
            {
                rows.Add(new MetricData(metricId, pair.Item2, pair.Item1, null, _lastRowId++));
            }
            _metricData.AddRange(rows);
            return rows;
        }
    }

    // Table Metric
    public class Metric
    {
        public string Uid { get; set; }
        public string DataSource { get; set; }
        public string Name { get; set; }
        public string Server { get; set; }
        public string Description { get; set; }
        public string Location { get; set; }
        public string Parameters { get; set; }
        public MetricStatus Status { get; set; }
        public string Message { get; set; }
        public DateTime? LastTimeStamp { get; set; }
        public int? PollInterval { get; set; }
        public string TagName { get; set; }
        public string ModelParams { get; set; }
        public long LastRowId { get; set; }

        // Ignored in db
        public string DisplayName { get; set; }

        public Metric Clone(string[] allowedKeys)
        {
            BeanUtil bu = BeanUtil.GetInstance();
            Metric m = new Metric();

            var props = GetType().GetProperties();

            foreach (string key in allowedKeys)
            {
                var prop = props.SingleOrDefault(p => p.Name.Equals(key, StringComparison.InvariantCultureIgnoreCase));
                if (prop == null)
                {
                    Debug.WriteLine("!!!! prop not found > " + key);
                }
                bu.SetSimpleProperty(m, key, prop.GetValue(this));
            }

            return m;
        }
    }
    // Table MetricData
    public class MetricData
    {
        public MetricData()
        {

        }
        public MetricData(string metricId, DateTime timestamp, double metricValue,
            double? anomalyScore, long rowid)
        {
            Uid = Guid.NewGuid().ToString();

            MetricId = metricId;
            Timestamp = timestamp;
            MetricValue = metricValue;
            AnomalyScore = anomalyScore;
            Rowid = rowid;
        }
        public string Uid { get; set; }
        public long Rowid { get; set; }
        public DateTime Timestamp { get; set; }
        public double MetricValue { get; set; }
        public double? AnomalyScore { get; set; }
        public double? RawAnomalyScore { get; set; }


        public int DisplayValue { get; set; }
        public string MetricId { get; set; }
    }

    #endregion

    #region Model swapper stuff

    // https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/model_swapper/model_swapper_interface.py
    // TODO:

    /// <summary>
    /// This is the interface class to connect the application layer to the Model Swapper.
    /// </summary>
    public class ModelSwapperInterface
    {
        public void DefineModel(string modelId, BestSingleMetricAnomalyParamsDescription args, Guid commandId)
        {
            // Sends defineModel command over the bus, for now we ignore the bus system
            // Calls the modelRunner which is the other end of the bus system.

            ModelRunner modelRunner = new ModelRunner(modelId);
            modelRunner.DefineModel(new ModelCommand
            {
                Id = commandId,
                Args = args,
                ModelId = modelId
            });
        }

        public List<object> SubmitRequests(string modelId, List<ModelInputRow> input)
        {
            List<object> results = new List<object>();
            ModelRunner modelRunner = new ModelRunner(modelId);
            foreach (ModelInputRow row in input)
            {
                results.Add(modelRunner.ProcessInputRow(row, null));
            }
            return results;
        }
    }

    public class ModelInputRow
    {
        public long RowId { get; set; }
        public List<string> Data { get; set; }

        public ModelInputRow(long rowId, List<string> data)
        {
            RowId = rowId;
            Data = data;
        }
    }

    public class ModelRunner
    {
        private readonly string _modelId;
        public CheckPointManager _checkpointMgr;
        private opf.Model _model;
        private bool _hasCheckpoint;
        private InputRowEncoder _inputRowEncoder;

        public ModelRunner(string modelId)
        {
            _modelId = modelId;
            _checkpointMgr = new CheckPointManager();
            _inputRowEncoder = null;
        }

        /// <summary>
        /// Handle the "defineModel" command
        /// </summary>
        public void DefineModel(ModelCommand command)
        {
            // Save the model to persistent storage (the parameters)

            ModelDefinition newModelDefinition = new ModelDefinition
            {
                ModelParams = new ModelParams()
                {
                    ModelConfig = command.Args.modelConfig,
                    InferenceArgs = command.Args.inferenceArgs,
                    InputSchema = command.Args.inputRecordSchema
                }
            };

            _checkpointMgr.Define(modelId: _modelId, definition: command.Args);
        }

        /// <summary>
        /// 
        /// </summary>
        /// <param name="row">ModelInputRow instance</param>
        /// <param name="currentRunInputSamples">a list; the input row's data will be appended
        /// to this list if the row is processed successfully</param>
        /// <returns>a ModelInferenceResult instance</returns>
        public object ProcessInputRow(ModelInputRow row, List<object> currentRunInputSamples)
        {
            if (_model == null)
            {
                LoadModel();
            }
            // Convert a flat input row into a format that is consumable by an OPF model
            _inputRowEncoder.AppendRecord(row.Data);
            Map<string, object> inputRecord = _inputRowEncoder.GetNextRecordDict();
            // Infer
            ModelResult r = _model.run(inputRecord);

            currentRunInputSamples?.Add(row.Data);

            return new ModelInferenceResult(rowId: row.RowId, status: 0, anomalyScore: (double)r.inferences[InferenceElement.AnomalyScore]);
        }

        /// <summary>
        /// Load the model and construct the input row encoder
        /// Side-effect: self._model and self._inputRowEncoder are loaded; 
        ///     self._modelLoadSec is set if profiling is turned on
        /// </summary>
        private void LoadModel()
        {
            if (_model == null)
            {
                DescriptionBase modelDefinition = null;
                try
                {
                    _model = _checkpointMgr.Load(_modelId);
                    _hasCheckpoint = true;
                }
                catch (ModelNotFound)
                {
                    // So, we didn't have a checkpoint... try to create our model from model
                    // definition params
                    _hasCheckpoint = false;
                    modelDefinition = _checkpointMgr.LoadModelDefinition(_modelId);
                    //ModelParams modelParams = modelDefinition.modelParams;
                    _model = ModelFactory.Create(modelConfig: modelDefinition);
                    _model.enableLearning();
                    _model.enableInference(modelDefinition.inferenceArgs);
                }

                // Construct the object for converting a flat input row into a format
                // that is consumable by an OPF model
                if (modelDefinition == null)
                {
                    modelDefinition = _checkpointMgr.LoadModelDefinition(_modelId);
                }

                var inputSchema = modelDefinition.inputRecordSchema;

                var inputFieldsMeta = inputSchema;
                _inputRowEncoder = new InputRowEncoder(inputFieldsMeta);

                // TODO: check https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/model_swapper/model_runner.py
                // that we need some extra lines
            }
        }
    }



    internal class InputRowEncoder
    {
        private Map<string, Tuple<FieldMetaType, SensorFlags>> _fieldMeta;
        private List<string> _fieldNames;
        private List<string> _row;
        private ModelRecordEncoder _modelRecordEncoder;

        public InputRowEncoder(Map<string, Tuple<FieldMetaType, SensorFlags>> inputFieldsMeta)
        {
            _fieldMeta = inputFieldsMeta;
            _fieldNames = inputFieldsMeta.Keys.ToList();
            _row = null;
        }

        public void AppendRecord(List<string> record)
        {
            Debug.Assert(_row == null);

            _row = record;
        }

        public List<string> GetFieldNames()
        {
            return _fieldNames;
        }

        public Map<string, Tuple<FieldMetaType, SensorFlags>> GetFields()
        {
            return _fieldMeta;
        }

        public List<string> GetNextRecord(bool useCache = true)
        {
            Debug.Assert(_row != null);
            var row = _row;
            _row = null;
            return row;
        }

        public Map<string, object> GetNextRecordDict()
        {
            var values = GetNextRecord();
            if (values == null) return null;
            if (!values.Any()) return new Map<string, object>();
            if (_modelRecordEncoder == null)
            {
                _modelRecordEncoder = new ModelRecordEncoder(fields: GetFields(),
                    aggregationPeriod: GetAggregationMonthAndSeconds());
            }
            return _modelRecordEncoder.Encode(values);
        }

        private TimeSpan? GetAggregationMonthAndSeconds()
        {
            return null;
        }
    }

    /// <summary>
    /// Encodes metric data input rows  for consumption by OPF models. See the `ModelRecordEncoder.encode` method for more details.
    /// </summary>
    internal class ModelRecordEncoder
    {
        private Map<string, Tuple<FieldMetaType, SensorFlags>> _fields;
        private TimeSpan? _aggregationPeriod;
        private int _sequenceId;
        private List<string> _fieldNames;
        private int? _timestampFieldIndex;

        public ModelRecordEncoder(Map<string, Tuple<FieldMetaType, SensorFlags>> fields, TimeSpan? aggregationPeriod = null)
        {
            if (fields == null || !fields.Any())
            {
                throw new ArgumentNullException("fields", "fields arg must be non-empty");
            }
            _fields = fields;
            _aggregationPeriod = aggregationPeriod;
            _sequenceId = -1;
            _fieldNames = fields.Keys.ToList();

            _timestampFieldIndex = GetFieldIndexBySpecial(fields, SensorFlags.Timestamp);
        }

        private int? GetFieldIndexBySpecial(Map<string, Tuple<FieldMetaType, SensorFlags>> fields, SensorFlags sensorFlags)
        {
            return fields.Values.Select(t=>t.Item2).ToList().IndexOf(sensorFlags);
        }

        /// <summary>
        /// Encodes the given input row as a dict, with the
        /// keys being the field names.This also adds in some meta fields:
        /// '_category': The value from the category field(if any)
        /// '_reset': True if the reset field was True(if any)
        /// '_sequenceId': the value from the sequenceId field(if any)
        /// </summary>
        /// <param name="inputRow">sequence of values corresponding to a single input metric data row</param>
        /// <returns></returns>
        public Map<string, object> Encode(List<string> inputRow)
        {
            var result = new Map<string, object>(ArrayUtils.Zip(_fieldNames, inputRow).ToDictionary(k => k.Get(0) as string, v => v.Get(1)));

            // TODO add the special field handling (category etc)
            if (_timestampFieldIndex.HasValue && _timestampFieldIndex >= 0)
            {
                result["_timestamp"] = inputRow[_timestampFieldIndex.Value];
                // Compute the record index based on timestamp
                result["_timestampRecordIdx"] = ComputeTimestampRecordIdx(inputRow[_timestampFieldIndex.Value]);
            }
            else
            {
                result["_timestamp"] = null;
            }

            result["_category"] = null;
            result["_reset"] = 0;
            result["_sequenceId"] = null;
            return result;
        }

        private string ComputeTimestampRecordIdx(string recordTs)
        {
            if (_aggregationPeriod == null)
                return null;
            throw new NotImplementedException("check this");
        }
    }

    public class ModelDefinition
    {
        public ModelParams ModelParams { get; set; }


    }

    public class ModelCommand
    {
        public Guid Id { get; set; }
        public string ModelId { get; set; }

        public BestSingleMetricAnomalyParamsDescription Args { get; set; }
    }

    public class ModelInferenceResult
    {
        private double anomalyScore;
        private long rowId;
        private int status;

        public ModelInferenceResult(long rowId, int status, double anomalyScore)
        {
            this.rowId = rowId;
            this.status = status;
            this.anomalyScore = anomalyScore;
        }
    }

    public class ModelSwapperUtils
    {
        /// <summary>
        /// Dispatch command to create HTM model
        /// </summary>
        /// <param name="modelId"> unique identifier of the metric row</param>
        /// <param name="params">model params for creating a scalar model per ModelSwapper interface</param>
        public static void CreateHtmModel(string modelId, BestSingleMetricAnomalyParamsDescription @params)
        {
            ModelSwapperInterface modelSwapper = new ModelSwapperInterface();
            modelSwapper.DefineModel(modelId: modelId, args: @params, commandId: Guid.NewGuid());
        }
    }

    public class CheckPointManager
    {
        private static Dictionary<string, DescriptionBase> _storedDefinitions = new Dictionary<string, DescriptionBase>();

        /// <summary>
        /// Retrieve a model instance from checkpoint.
        /// </summary>
        /// <param name="modelId">unique model ID</param>
        /// <returns>an OPF model instance</returns>
        public opf.Model Load(string modelId)
        {
            throw new ModelNotFound();
        }

        public DescriptionBase LoadModelDefinition(string modelId)
        {
            var definition = _storedDefinitions
                .Where(sd => sd.Key == modelId)
                .Select(sd => sd.Value)
                .FirstOrDefault();

            return definition;
        }

        public void Define(string modelId, DescriptionBase definition)
        {
            if (!_storedDefinitions.ContainsKey(modelId))
                _storedDefinitions.Add(modelId, definition);
        }
    }

    public class ModelDataFeeder
    {
        /// <summary>
        /// Send input rows to CLA model for processing
        /// </summary>
        /// <param name="modelId">unique identifier of the model</param>
        /// <param name="inputRows">sequence of model_swapper_interface.ModelInputRow objects</param>
        /// <param name="batchSize">max number of data records per input batch</param>
        /// <param name="modelSwapper">model_swapper_interface.ModelSwapperInterface object</param>
        /// <param name="logger">logger object</param>
        /// <param name="profiling">True if profiling is enabled</param>
        public static void SendInputRowsToModel(string modelId, List<ModelInputRow> inputRows, int batchSize,
            ModelSwapperInterface modelSwapper, ILog logger, bool profiling)
        {
            logger.DebugFormat("Streaming numRecords={0} to model={1}", inputRows.Count, modelId);

            // Stream data to HTM model in batches
            // TODO: now it stream everything, chunk it
            //foreach (var batch in inputRows)
            var batch = inputRows;
            {
                DateTime startTime = DateTime.Now;

                var batchId = modelSwapper.SubmitRequests(modelId, batch);

                if (profiling)
                {
                    var headTS = batch.First().Data.First();
                    var tailTS = batch.Last().Data.First();
                    logger.InfoFormat(
                        "{{TAG:STRM.DATA.TO_MODEL.DONE}} Submitted batch={0} to model={1}; numRows={2}; rows=[{3}]; ts=[{4}]; duration={5}s",
                        batchId, modelId, batch.Count,
                        string.Format("{0}..{1}", batch.First().RowId, batch.Last().RowId),
                        string.Format("{0}..{1}", headTS, tailTS), DateTime.Now - startTime);
                }
            }
        }
    }

    #endregion
}