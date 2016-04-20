using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;

namespace HTM.Net.Research.Taurus.HtmEngine.runtime
{
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
        public int Rowid { get; set; }
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

    // https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/anomaly_likelihood_helper.py

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
            _statisticsMinSampleSize = 10;
            _statisticsSampleSize = 10;

            algorithms = new AnomalyLikelihood(true, _statisticsSampleSize, true, 0, _statisticsMinSampleSize);
        }

        private void GenerateAnomalyParams()
        {

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
        public object UpdateModelAnomalyScores(Metric metricObj, List<MutableMetricDataRow> metricDataRows)
        {
            // When populated, a cached list of MetricData instances for updating anomaly likelyhood params
            List<MetricData> statsSampleCache = null;

            // Index into metricDataRows where processing is to resume
            int startRowIndex = 0;

            var statisticsRefreshInterval = GetStatisticsRefreshInterval(batchSize: metricDataRows.Count);

            if (metricObj.Status != MetricStatus.Active)
            {
                throw new MetricNotActiveError();
            }

            var modelParams = JsonConvert.DeserializeObject<Map<string, object>>(metricObj.ModelParams);
            var anomalyParams = (Map<string, object>)modelParams.Get("anomalyLikelihoodParams", null);
            if (anomalyParams == null)
            {
                // We don't have a likelihood model yet. Create one if we have sufficient
                // records with raw anomaly scores
                var initData = InitAnomalyLikelihoodModel(metricObj, metricDataRows);
                anomalyParams = initData.AnomalyParams;
                statsSampleCache = initData.StatsSampleCache;
                startRowIndex = initData.StartRowIndex;
            }

            // Do anomaly likelihood processing on the rest of the new samples
            // NOTE: this loop will be skipped if there are still not enough samples for
            // creating the anomaly likelihood params
            while (startRowIndex < metricDataRows.Count)
            {
                int endRowID;
                // Determine where to stop processing rows prior to next statistics refresh
                if (statsSampleCache == null || statsSampleCache.Count >= _statisticsMinSampleSize)
                {
                    // We're here if:
                    // a. We haven't tried updating anomaly likelihood stats yet
                    // OR
                    // b. We already updated anomaly likelyhood stats (we had sufficient
                    //      samples for it)
                    // TODO: unit-test
                    endRowID = (int)anomalyParams["last_rowid_for_stats"] + statisticsRefreshInterval;
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
                int limitIndex;
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

                foreach (var md in metricDataRows.Skip(startRowIndex).Take(limitIndex))
                {
                    var computed = algorithms.UpdateAnomalyLikelihoods(new List<Sample> { new Sample(md.Timestamp, md.MetricValue, md.RawAnomalyScore) },
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
                    anomalyParams = result.AnomalyParams;
                    statsSampleCache = result.StatsSampleCache;
                }
                startRowIndex += consumedSamples.Count;
            }

            return anomalyParams;
        }

        private dynamic RefreshAnomalyParams(string metricId, List<MetricData> statsSampleCache, List<MetricData> consumedSamples, 
            Map<string, object> defaultAnomalyParams)
        {
            throw new NotImplementedException();
        }

        private object GetMetricLogPrefix(Metric metricObj)
        {
            throw new NotImplementedException();
        }

        private dynamic InitAnomalyLikelihoodModel(Metric metricObj, List<MutableMetricDataRow> metricDataRows)
        {
            throw new NotImplementedException();
        }

        private int GetStatisticsRefreshInterval(int batchSize)
        {
            throw new NotImplementedException();
        }
    }

    public class MetricNotActiveError : Exception
    {

    }

    public class Metric
    {
        public MetricStatus Status { get; set; }
        public string ModelParams { get; set; }
        public string Uid { get; set; }
    }

    public class MetricData
    {
        public int Rowid { get; set; }

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
}