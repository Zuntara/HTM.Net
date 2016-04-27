using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;
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
    public static class MetricUtils
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
            // fillsup the data section that will trigger an import (?)
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

    public class ModelHandler
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
            var adapter = _adapterRegistry.SingleOrDefault(a => a.Datasource.Equals(dataSource, StringComparison.InvariantCultureIgnoreCase));
            if (adapter == null && dataSource == "custom")
            {
                return new CustomDatasourceAdapter();
            }
            if (adapter == null)
            {
                throw new InvalidOperationException("Adapter not found");
            }
            return adapter;
        }

        public static void RegisterDatasourceAdapter(IDataSourceAdapter clientCls)
        {
            if (!_adapterRegistry.Contains(clientCls))
            {
                _adapterRegistry.Add(clientCls);
            }
        }

        public static void ClearRegistrations()
        {
            _adapterRegistry.Clear();
        }
    }

    public interface IDataSourceAdapter
    {
        string ImportModel(ModelSpec modelSpec);
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
        string MonitorMetric(ModelSpec modelSpec);
        /// <summary>
        /// Unmonitor a metric
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        void UnmonitorMetric(string metricId);

        /// <summary>
        /// Start a model that is PENDING_DATA, creating the OPF/CLA model
        /// NOTE: used by MetricStreamer when model is in PENDING_DATA state and
        /// sufficient data samples are available to get statistics and complete model
        /// creation.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        void ActivateModel(string metricId);
        string Datasource { get; }
        /// <summary>
        /// Create scalar HTM metric if it doesn't exist
        /// </summary>
        /// <param name="metricId"></param>
        /// <returns></returns>
        string CreateMetric(string metricId);
    }

    /// <summary>
    /// https://github.com/numenta/numenta-apps/blob/master/htmengine/htmengine/adapters/datasource/custom/__init__.py
    /// </summary>
    public class CustomDatasourceAdapter : IDataSourceAdapter
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(CustomDatasourceAdapter));
        /// <summary>
        /// Minimum records needed before creating a model; assumes 24 hours worth of 5-minute data samples
        /// </summary>
        public const int MODEL_CREATION_RECORD_THRESHOLD = (60 / 5) * 24;

        // Default metric period value to use when it's unknown
        // TODO: Should we use 0 since it's unknown "unknown" or default to 5 min?
        // Consider potential impact on web charts, htm-it-mobile
        public const int DEFAULT_METRIC_PERIOD = 300;  // 300 sec = 5 min

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
        /// Start a model that is PENDING_DATA, creating the OPF/CLA model
        /// NOTE: used by MetricStreamer when model is in PENDING_DATA state and
        /// sufficient data samples are available to get statistics and complete model
        /// creation.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        public void ActivateModel(string metricId)
        {
            var metricObj = RepositoryFactory.Metric.GetMetric(metricId);
            if (metricObj.DataSource != Datasource)
            {
                throw new InvalidOperationException(string.Format("activateModel: not an HTM metric={0}; datasource={1}",
                    metricId, metricObj.DataSource));
            }
            var stats = GetMetricStatistics(metricId);
            var swarmParams = ScalarMetricUtils.GenerateSwarmParams(stats);
            ScalarMetricUtils.StartModel(metricId, swarmParams: swarmParams, logger: _log);
        }

        /// <summary>
        /// Unmonitor a metric
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        public void UnmonitorMetric(string metricId)
        {
            RepositoryFactory.Metric.DeleteModel(metricId);

            ModelSwapperUtils.DeleteHtmModel(metricId);

            _log.InfoFormat("HTM Metric unmonitored: metric={0}", metricId);
        }

        public string Datasource { get { return "custom"; } }

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
        public string CreateMetric(string metricName)
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

        public static ModelParams FromDict(IDictionary<string, object> dict)
        {
            ModelParams pars = new ModelParams();
            var bu = BeanUtil.GetInstance();
            foreach (string key in dict.Keys)
            {
                bu.SetSimpleProperty(pars, key, dict[key]);
            }
            return pars;
        }

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
        public object UserInfo { get; set; }

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
        static RepositoryFactory()
        {
            Metric = new MetricMemRepository();
        }

        public static IMetricRepository Metric { get; set; }
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
        /// <summary>
        /// Get Metric Data
        /// The parameters {rowid}, {fromTimestamp ad toTimestamp}, and {start and stop}
        /// are to be used independently for different queries.
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="rowId">Specific MetricData row id</param>
        /// <param name="start">Starting MetricData row id; inclusive</param>
        /// <param name="stop">Max MetricData row id; inclusive</param>
        /// <param name="limit">Limit on number of results to return</param>
        /// <param name="fromTimestamp">Starting timestamp</param>
        /// <param name="toTimestamp">Ending timestamp</param>
        /// <param name="score">Return only rows with scores above this threshold (all non-null scores for score=0)</param>
        /// <param name="sort">Sort by this column</param>
        /// <param name="sortAsc">true when ascending, false otherwise (descending)</param>
        /// <returns>Metric data</returns>
        List<MetricData> GetMetricData(string metricId = null, long? rowId = null, long? start = null, long? stop = null,
            int? limit = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null,
            double? score = null, Expression<Func<MetricData, object>> sort = null, bool? sortAsc = null);

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        List<MetricData> AddMetricData(string metricId, List<Tuple<DateTime, double>> data);

        List<Metric> GetAllModels();
        void DeleteModel(string metricId);
        List<Metric> GetCustomMetrics();
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

        public List<MetricData> GetMetricData(string metricId = null, long? rowId = null, long? start = null, long? stop = null,
            int? limit = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null,
            double? score = null, Expression<Func<MetricData, object>> sort = null, bool? sortAsc = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        public List<MetricData> AddMetricData(string metricId, List<Tuple<DateTime, double>> data)
        {
            throw new NotImplementedException();
        }

        public List<Metric> GetAllModels()
        {
            throw new NotImplementedException();
        }

        public void DeleteModel(string metricId)
        {
            throw new NotImplementedException();
        }

        public List<Metric> GetCustomMetrics()
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
            if (_metric.Any(m => m.Uid == metricId))
                return _metric.First(m => m.Uid == metricId);
            throw new MetricNotFound();
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

        public List<MetricData> GetMetricData(string metricId = null, long? rowId = null, long? start = null, long? stop = null, int? limit = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null,
            double? score = null, Expression<Func<MetricData, object>> sort = null, bool? sortAsc = null)
        {
            var sel = _metricData.AsQueryable();

            if (sort == null)
            {
                if (sortAsc.GetValueOrDefault())
                {
                    sel = sel.OrderBy(md => md.Rowid);
                }
                else
                {
                    sel = sel.OrderByDescending(md => md.Rowid);
                }
            }
            else
            {
                if (sortAsc.GetValueOrDefault())
                {
                    sel = sel.OrderBy(sort);
                }
                else
                {
                    sel = sel.OrderByDescending(sort);
                }
            }

            if (!string.IsNullOrWhiteSpace(metricId))
            {
                sel = sel.Where(md => md.Uid == metricId);
            }
            if (rowId.HasValue)
            {
                sel = sel.Where(md => md.Rowid == rowId.Value);
            }
            else if (fromTimestamp.HasValue || toTimestamp.HasValue)
            {
                if (fromTimestamp.HasValue)
                {
                    sel = sel.Where(md => md.Timestamp >= fromTimestamp.Value);
                }
                if (toTimestamp.HasValue)
                {
                    sel = sel.Where(md => md.Timestamp <= toTimestamp.Value);
                }
            }
            else
            {
                if (start.HasValue)
                {
                    sel = sel.Where(md => md.Rowid >= start.Value);
                }
                if (stop.HasValue)
                {
                    sel = sel.Where(md => md.Rowid <= stop.Value);
                }
            }
            if (limit.HasValue)
            {
                sel = sel.Take(limit.Value);
            }
            if (score.HasValue && score.Value > 0)
            {
                sel = sel.Where(md => md.AnomalyScore >= score);
            }
            else if (score.HasValue && score.Value == 0.0)
            {
                sel = sel.Where(md => md.AnomalyScore.HasValue);
            }
            return sel.ToList();
        }

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        public List<MetricData> AddMetricData(string metricId, List<Tuple<DateTime, double>> data)
        {
            int numRows = data.Count;
            if (numRows == 0)
                return null;

            List<MetricData> rows = new List<MetricData>();

            foreach (var pair in data)
            {
                rows.Add(new MetricData(metricId, pair.Item1, pair.Item2, null, _lastRowId++));
            }
            _metricData.AddRange(rows);
            return rows;
        }

        public List<Metric> GetAllModels()
        {
            return _metric;
        }

        public void DeleteModel(string metricId)
        {
            var metric = _metric.Single(m => m.Uid == metricId);
            _metric.Remove(metric);
        }

        public List<Metric> GetCustomMetrics()
        {
            return _metric.Where(m => m.DataSource == "custom").ToList();
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
        public void DefineModel(string modelId, IDescription args, Guid commandId)
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

        public List<ModelInferenceResult> SubmitRequests(string modelId, List<ModelInputRow> input)
        {
            List<ModelInferenceResult> results = new List<ModelInferenceResult>();
            ModelRunner modelRunner = new ModelRunner(modelId);
            foreach (ModelInputRow row in input)
            {
                results.Add(modelRunner.ProcessInputRow(row, null));
            }
            return results;
        }

        public void DeleteModel(string modelId, Guid commandId)
        {
            ModelRunner modelRunner = new ModelRunner(modelId);
            modelRunner.DeleteModel(new ModelCommand
            {
                ModelId = modelId,
                Id = commandId
            });
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
        public ModelInferenceResult ProcessInputRow(ModelInputRow row, List<object> currentRunInputSamples)
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

            return new ModelInferenceResult(commandId: null, rowId: row.RowId, status: 0, anomalyScore: (double)r.inferences[InferenceElement.AnomalyScore]);
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
                IDescription modelDefinition = null;
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
        /// <summary>
        /// Handle the "deleteModel" command
        /// </summary>
        /// <param name="command">ModelCommand instance for the "deleteModel" command</param>
        public ModelInferenceResult DeleteModel(ModelCommand command)
        {
            _checkpointMgr.Remove(modelId: _modelId);

            return new ModelInferenceResult(commandId: command.Id, status: 0);
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
            return fields.Values.Select(t => t.Item2).ToList().IndexOf(sensorFlags);
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

        public IDescription Args { get; set; }
    }

    public class ModelInferenceResult
    {
        private double? anomalyScore;
        private long? rowId;
        private int? status;
        private Guid? commandId;

        public ModelInferenceResult(Guid? commandId = null, long? rowId = null, int? status = null, double? anomalyScore = null)
        {
            this.commandId = commandId;
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
        public static void CreateHtmModel(string modelId, IDescription @params)
        {
            ModelSwapperInterface modelSwapper = new ModelSwapperInterface();
            modelSwapper.DefineModel(modelId: modelId, args: @params, commandId: Guid.NewGuid());
        }

        public static void DeleteHtmModel(string modelId)
        {
            ModelSwapperInterface modelSwapper = new ModelSwapperInterface();
            modelSwapper.DeleteModel(modelId: modelId, commandId: Guid.NewGuid());
        }
    }

    public class CheckPointManager
    {
        private static Dictionary<string, IDescription> _storedDefinitions = new Dictionary<string, IDescription>();

        /// <summary>
        /// Retrieve a model instance from checkpoint.
        /// </summary>
        /// <param name="modelId">unique model ID</param>
        /// <returns>an OPF model instance</returns>
        public opf.Model Load(string modelId)
        {
            throw new ModelNotFound();
        }

        public IDescription LoadModelDefinition(string modelId)
        {
            var definition = _storedDefinitions
                .Where(sd => sd.Key == modelId)
                .Select(sd => sd.Value)
                .FirstOrDefault();

            return definition;
        }

        public void Define(string modelId, IDescription definition)
        {
            if (!_storedDefinitions.ContainsKey(modelId))
                _storedDefinitions.Add(modelId, definition);
        }

        /// <summary>
        /// Remove the model entry with the given model ID from storage
        /// </summary>
        /// <param name="modelId">model ID to remove</param>
        public void Remove(string modelId)
        {
            _storedDefinitions.Remove(modelId);
        }
    }

    #endregion
}