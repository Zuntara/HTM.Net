using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Util;
using Newtonsoft.Json;

namespace HTM.Net.Research.Taurus.MetricCollectors
{
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

        /// <summary>
        /// Query UTC timestamp of the last emitted sample batch; if one hasn't been
        /// saved yet, then synthesize one, using negative aggregation period offset
        /// from current time
        /// </summary>
        /// <param name="key"></param>
        /// <param name="aggSec">aggregation period in seconds</param>
        /// <returns>(possibly synthesized) UTC timestamp of the last successfully-emitted sample batch</returns>
        public static DateTime EstablishLastEmittedSampleDateTime(string key, int aggSec)
        {
            DateTime? lastEmittedTimestamp = QueryLastEmittedSampleDatetime(key);
            if (lastEmittedTimestamp.HasValue)
                return lastEmittedTimestamp.Value;
            // Start at the present to avoid re-sending metric data that we may have
            // already sent to Taurus.
            lastEmittedTimestamp = DateTime.UtcNow - TimeSpan.FromSeconds(aggSec);
            RepositoryFactory.EmittedSampleTracker.Insert(key: key, sampleTs: lastEmittedTimestamp.Value);

            return QueryLastEmittedSampleDatetime(key).GetValueOrDefault();
        }

        private static DateTime? QueryLastEmittedSampleDatetime(string key)
        {
            return RepositoryFactory.EmittedSampleTracker.GetSampleTsFromKey(key);
        }

        /// <summary>
        /// Compute aggregation timestamp from the sample's timestamp as the lower
        /// aggregation boundary relative to the given reference.
        /// </summary>
        /// <param name="sampleDatetime">offset-naive UTC timestamp of the sample (e.g., create_at property of a tweet)</param>
        /// <param name="aggRefDatetime">offset-naive UTC reference aggregation
        /// timestamp belonging to the sample stream; may precede, follow, or be equal
        /// to sampleDatetime</param>
        /// <param name="aggSec">the corresponding metric's aggregation period in seconds</param>
        /// <returns>
        /// offset=naive UTC timestamp of aggregation period that the sample
        /// belongs to, which is the bottom boundary of its aggregation window. E.g.,
        ///   sample="2015-02-20 2:14:00", ref="2015-02-20 2:00:00", aggSec=300 (5min)
        ///     would return "2015-02-20 2:10:00"
        ///   sample="2015-02-20 2:14:00", ref="2015-02-20 2:20:00", aggSec=300 (5min)
        ///     would return "2015-02-20 2:10:00"
        ///   sample="2015-02-20 2:15:00", ref="2015-02-20 2:15:00", aggSec=300 (5min)
        ///     would return "2015-02-20 2:15:00"
        /// </returns>
        public static DateTime AggTimestampFromSampleTimestamp(DateTime sampleDatetime, DateTime aggRefDatetime, int aggSec)
        {
            double sampleEpoch = EpochFromNaiveUTCDatetime(sampleDatetime);
            double aggRefEpoch = EpochFromNaiveUTCDatetime(aggRefDatetime);
            double deltaSec = sampleEpoch - aggRefEpoch;

            double deltaAggIntervalSec;
            double aggEpoch;
            if (deltaSec > 0)
            {
                // Sample timestamp equals or follows reference
                deltaAggIntervalSec = Math.Floor(deltaSec/aggSec)*aggSec;
                aggEpoch = aggRefEpoch + deltaAggIntervalSec;
            }
            else
            {
                // Sample timestamp precedes reference
                // Back up to beginning of aggregation window
                deltaAggIntervalSec = Math.Floor((Math.Abs(deltaSec) + (aggSec - 1))/aggSec)*aggSec;
                aggEpoch = aggRefEpoch - deltaAggIntervalSec;
            }
            return new DateTime(0, DateTimeKind.Utc) + TimeSpan.FromSeconds(aggEpoch);
        }

        public static double EpochFromNaiveUTCDatetime(DateTime? dt)
        {
            return (dt.GetValueOrDefault() - new DateTime(0, DateTimeKind.Utc)).TotalSeconds;
        }

        public static void UpdateLastEmittedSampleDatetime(string key, DateTime sampleDatetime)
        {
            RepositoryFactory.EmittedSampleTracker.UpdateSampleTsWithKey(key, sampleDatetime);
        }
    }
}