using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;

namespace HTM.Net.Research.Taurus.HtmEngine
{
    /// <summary>
    /// Helper class for running AnomalyLikelihood calculations in AnomalyService
    ///   Usage::
    ///     likelihoodHelper = AnomalyLikelihoodHelper(log, config)
    ///     likelihoodHelper.updateModelAnomalyScores(engine=engine,
    ///         metric=metric, metricDataRows=metricDataRows)
    /// 
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/().py
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
            var rows = RepositoryFactory.MetricData.GetMetricDataWithRawAnomalyScoresTail(metricId, limit);
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

            int numProcessedRows = RepositoryFactory.MetricData.GetProcessedMetricDataCount(metricObj.Uid);

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
}