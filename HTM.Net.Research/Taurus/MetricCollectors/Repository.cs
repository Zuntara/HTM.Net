using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTM.Net.Research.Taurus.HtmEngine.Adapters;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Tweetinvi;
using Tweetinvi.Core.Authentication;
using Tweetinvi.Core.Interfaces;
using Tweetinvi.Core.Interfaces.Streaminvi;
using Tweetinvi.Logic;
using User = Tweetinvi.User;

namespace HTM.Net.Research.Taurus.MetricCollectors
{
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
                Uid = Uid,
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

    //Table
    [Table("TwitterTweet")]
    public class TwitterTweet
    {
        public const int MAX_TWEET_MSG_ID_LEN = 40;
        public const int MAX_TWEET_REAL_NAME_LEN = 100;
        public const int MAX_TWEET_USERID_LEN = 100;
        public const int MAX_TWEET_USERNAME_LEN = 100;

        [MaxLength(MAX_TWEET_MSG_ID_LEN)]
        public string Uid { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool ReTweet { get; set; }
        [MaxLength(10)]
        public string Lang { get; set; }
        [MaxLength]
        public string Text { get; set; }
        [MaxLength(MAX_TWEET_USERID_LEN)]
        public string UserId { get; set; }
        [MaxLength(MAX_TWEET_USERNAME_LEN)]
        public string UserName { get; set; }
        [MaxLength(MAX_TWEET_REAL_NAME_LEN)]
        public string RealName { get; set; }
        [MaxLength(MAX_TWEET_MSG_ID_LEN)]
        public string RetweetedStatusId { get; set; }
        public int? RetweetCount { get; set; }
        [MaxLength(MAX_TWEET_MSG_ID_LEN)]
        public string RetweetedUserid { get; set; }
        [MaxLength(MAX_TWEET_USERNAME_LEN)]
        public string RetweetedUsername { get; set; }
        [MaxLength(MAX_TWEET_REAL_NAME_LEN)]
        public string RetweetedRealName { get; set; }
        [MaxLength(MAX_TWEET_MSG_ID_LEN)]
        public string InReplyToStatusId { get; set; }
        [MaxLength(MAX_TWEET_USERID_LEN)]
        public string InReplyToUserid { get; set; }
        [MaxLength(MAX_TWEET_USERNAME_LEN)]
        public string InReplyToUsername { get; set; }
        [MaxLength]
        public string Contributors { get; set; }
        public DateTime StoredAt { get; set; }
    }
    // Table
    [Table("TwitterTweetSample")]
    public class TwitterTweetSample
    {
        public const int MAX_TWEET_MSG_ID_LEN = 40;
        public const int METRIC_NAME_MAX_LEN = 190;

        [Key]
        public long Seq { get; set; }
        [MaxLength(METRIC_NAME_MAX_LEN)]
        public string Metric { get; set; }
        [MaxLength(MAX_TWEET_MSG_ID_LEN)]
        public string msg_uid { get; set; }
        public DateTime agg_ts { get; set; }
        public DateTime? stored_at { get; set; }
    }

    // TODO: foresee an extra task for storing the picked up data (and manipulate if needed)
    /// <summary>
    /// Base class for agents who collect metrics
    /// </summary>
    public abstract class MetricCollectorAgent
    {
        protected readonly ILog Log = LogManager.GetLogger(typeof(CustomDatasourceAdapter));

        protected MetricCollectorAgent()
        {
            CancellationTokenSource = new CancellationTokenSource();
            CancelToken = CancellationTokenSource.Token;
            CollectionTask = new Task(ExecuteCollectionTask, CancelToken, TaskCreationOptions.LongRunning);
            StoringTask = new Task(ExecuteStoringTask, CancelToken, TaskCreationOptions.LongRunning);
            GarbageCollectionTask = new Task(ExecuteGarbageCollectionTask, CancelToken, TaskCreationOptions.LongRunning);
            ForwarderTask = new Task(ExecuteForwarderTask, CancelToken, TaskCreationOptions.LongRunning);
        }

        protected abstract void ExecuteCollectionTask();
        protected abstract void ExecuteStoringTask();
        protected abstract void ExecuteGarbageCollectionTask();
        protected abstract void ExecuteForwarderTask();

        public void StartCollector()
        {
            Log.InfoFormat("Starting up the agent...");
            IsCanceled = false;
            CollectionTask.Start();
            StoringTask.Start();
            GarbageCollectionTask.Start();
            ForwarderTask.Start();

            CollectionTask.ContinueWith(t=>DoTaskCompletionJob(t, "CollectionTask"));
            StoringTask.ContinueWith(t => DoTaskCompletionJob(t, "StoringTask"));
            GarbageCollectionTask.ContinueWith(t => DoTaskCompletionJob(t, "GarbageCollectionTask"));
            ForwarderTask.ContinueWith(t => DoTaskCompletionJob(t, "ForwarderTask"));
        }

        protected virtual void DoTaskCompletionJob(Task parentTask, string taskName)
        {
            if (parentTask.Status == TaskStatus.Faulted)
            {
                Log.ErrorFormat("Task {0} failed with exception: {1}", taskName, parentTask.Exception);
            }
            else
            {
                Log.InfoFormat("Task {0} ended with status {1}", taskName, parentTask.Status);
            }
        }

        public void StopCollector()
        {
            Log.InfoFormat("Shutting down the agent...");
            IsCanceled = true;
            CancellationTokenSource.Cancel();
            try
            {
                Task.WaitAll(CollectionTask, GarbageCollectionTask, ForwarderTask, StoringTask);
            }
            catch
            {
                // We may suppress this
            }
        }

        private CancellationTokenSource CancellationTokenSource { get; }
        protected CancellationToken CancelToken { get; }

        public bool IsCanceled { get; protected set; }
        public Task CollectionTask { get; }
        public Task StoringTask { get; }
        public Task GarbageCollectionTask { get; }
        public Task ForwarderTask { get; }
    }

    public class TwitterStorerArguments
    {
        /// <summary>
        /// Metric aggregation period in seconds
        /// </summary>
        public int AggSec { get; set; }
        /// <summary>
        /// Wheter we should log incoming messages
        /// </summary>
        public bool EchoData { get; set; }
        /// <summary>
        /// Tweet tagging map as returned by`buildTaggingMapAndStreamFilterParams()`
        /// </summary>
        public TaggingMap TaggingMap { get; set; }
    }

    public class TwitterCollectorArguments
    {
        public List<TwitterMetricSpec> MetricSpecs { get; set; }
    }

    public class TwitterMessage
    {
        public ITweet TweetData { get; set; }
        public MatchOn MatchOn { get; set; }
        public List<string> MetricTagSet { get; set; }
    }

    public class ConnectionMarker : TwitterMessage
    {

    }

    public class TaggingMap
    {
        public Map<string, string> TagOnSymbols { get; set; }
        public Map<long, List<string>> TagOnSourceUsers { get; set; }
        public Map<long, List<string>> TagOnMentions { get; set; }
    }

    internal abstract class StreamingStatsBase
    {
        /// <summary>
        /// Count of tweets recevied from API
        /// </summary>
        public int NumTweets { get; set; }
        /// <summary>
        /// Count of tweets from current stream that didn't match our tagging logic
        /// </summary>
        public int NumUntaggedTweets { get; set; }
        /// <summary>
        /// Count of "delete" statuses
        /// </summary>
        public int NumDeleteStatuses { get; set; }
        /// <summary>
        /// Count of "limit" statuses
        /// </summary>
        public int NumLimitStatuses { get; set; }
        /// <summary>
        /// Count of rate-limited tweets as reaped from  "limit" statuses
        /// </summary>
        public int NumLimitedTweets { get; set; }
        /// <summary>
        /// Count of "disconnect" statuses
        /// </summary>
        public int NumDisconnectStatuses { get; set; }
        /// <summary>
        /// Count of "warning" statuses
        /// </summary>
        public int NumWarningStatuses { get; set; }
        /// <summary>
        /// Count of other statuses
        /// </summary>
        public int NumOtherStatuses { get; set; }
    }

    internal class CurrentStreamStats : StreamingStatsBase
    {
        public CurrentStreamStats()
        {
            StartingDatetime = null;
        }

        public DateTime? StartingDatetime { get; set; }
    }

    internal class RuntimeStreamingStats : StreamingStatsBase
    {
        public RuntimeStreamingStats()
        {
            StreamNumber = null;
        }

        public int? StreamNumber { get; set; }
    }

    public class TwitterMetricSpec
    {
        public string Resource { get; set; }
        public string Metric { get; set; }
        public string[] ScreenNames { get; set; }
        public string Symbol { get; set; }
    }

    public class TwitterCollectorAgent : MetricCollectorAgent
    {
        private readonly TwitterCollectorArguments _collectorArgs;
        private readonly TwitterStorerArguments _storerArgs;
        private readonly ConcurrentQueue<TwitterMessage> _messageQueue;
        /// <summary>
        /// Streaming stats of current stream
        /// </summary>
        private CurrentStreamStats _currentStreamStats;
        /// <summary>
        /// Overall runtime streaming stats
        /// </summary>
        private RuntimeStreamingStats _runtimeStreamingStats;

        private const string EMITTED_TWEET_VOLUME_SAMPLE_TRACKER_KEY = "twitter-tweets-volume";

        public TwitterCollectorAgent(TwitterCollectorArguments collectorArgs, TwitterStorerArguments storerArgs)
        {
            _messageQueue = new ConcurrentQueue<TwitterMessage>();
            _collectorArgs = collectorArgs;
            _storerArgs = storerArgs;
            if (_collectorArgs.MetricSpecs == null)
            {
                _collectorArgs.MetricSpecs = LoadMetricSpecs();
            }

            // Overall runtime streaming stats
            _runtimeStreamingStats = new RuntimeStreamingStats();
        }

        protected override void ExecuteCollectionTask()
        {
            var cred = Auth.SetUserCredentials("CONSUMER_KEY", "CONSUMER_SECRET", "ACCESS_TOKEN", "ACCESS_TOKEN_SECRET");

            // Get the tagging map and filter params

            var user = User.GetAuthenticatedUser();
            Debug.WriteLine("User:" + user?.ScreenName);

            IFilteredStream tweetStream = Stream.CreateFilteredStream(cred);
            tweetStream.MatchOn = MatchOn.Everything;

            // Apply filter and get tagging map
            TaggingMap taggingMap = BuildTagMapAndStreamFilterParams(_collectorArgs.MetricSpecs, cred, tweetStream);
            _storerArgs.TaggingMap = taggingMap;

            tweetStream.StreamStarted += (sender, args) =>
            {
                Log.Info("Connected to twitter streaming server.");
                CheckHealth();
                _messageQueue.Enqueue(new ConnectionMarker());
            };
            tweetStream.WarningFallingBehindDetected += (sender, args) =>
            {
                Log.Error("Connection falling behind");
                CheckHealth();
            };
            tweetStream.MatchingTweetReceived += (sender, args) =>
            {
                CheckHealth();
                _messageQueue.Enqueue(new TwitterMessage
                {
                    TweetData = args.Tweet,
                    MatchOn = args.MatchOn
                });
            };
            tweetStream.StartStreamMatchingAnyCondition();

            while (!CancelToken.IsCancellationRequested)
            {
                // monitor twitter and send data to internal queue to be picked up (events)
                // by storer and then by forwarder
                Thread.Sleep(1);
            }

            tweetStream.StopStream();

            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

        /// <summary>
        /// Thread function; preprocess and store incoming tweets deposited by
        /// twitter streamer into self._msgQ
        /// </summary>
        protected override void ExecuteStoringTask()
        {
            Thread.Sleep(1000); // give a chance to the collector to correctly start up and provide TaggingMap

            DateTime aggRefDateTime = MetricUtils.EstablishLastEmittedSampleDateTime(
                    key: EMITTED_TWEET_VOLUME_SAMPLE_TRACKER_KEY, aggSec: _storerArgs.AggSec);
            int statsIntervalSec = 600;
            DateTime nextStatsUpdateEpoch = DateTime.Now;
            int maxBatchSize = 100;

            while (!CancelToken.IsCancellationRequested)
            {
                // Accumulate batch of incoming messages for SQL insert performance
                List<TwitterMessage> messages = new List<TwitterMessage>();
                while (messages.Count < maxBatchSize)
                {
                    // Get the next incoming message
                    //var timeout = messages.Any() ? 500 : 0;
                    if (_messageQueue.IsEmpty)
                    {
                        break;
                    }
                    TwitterMessage msg;
                    if (_messageQueue.TryDequeue(out msg))
                    {
                        messages.Add(msg);
                    }
                }
                // Process the batch
                var reapStats = ReapMessages(messages);
                List<TwitterMessage> tweets = reapStats.Item1;
                List<TwitterMessage> deletes = reapStats.Item2;
                // Save (re)tweets
                if (tweets != null && tweets.Any())
                {
                    SaveTweets(messages: tweets, aggRefDatetime: aggRefDateTime);
                }
                // Save deletion requests
                if (deletes != null && deletes.Any())
                {
                    SaveTweetDeletionRequests(messages: deletes);
                }
                // Echo messages to stdout if requested
                if (_storerArgs.EchoData)
                {
                    foreach (var message in messages)
                    {
                        Console.WriteLine(message);
                    }
                }
                // Print stats
                DateTime now = DateTime.Now;
                if (now >= nextStatsUpdateEpoch)
                {
                    nextStatsUpdateEpoch = now.AddSeconds(statsIntervalSec);
                    LogStreamStats();
                }
                Thread.Sleep(1);
            }
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

        protected override void ExecuteGarbageCollectionTask()
        {
            while (!CancelToken.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

        protected override void ExecuteForwarderTask()
        {
            // metric and non metric data forwarding (MetricDataForwarder)

            // Metric data forwarding to metric listener
            var aggSec = _storerArgs.AggSec;
            DateTime lastEmittedAggTime =
                MetricUtils.EstablishLastEmittedSampleDateTime(EMITTED_TWEET_VOLUME_SAMPLE_TRACKER_KEY, aggSec);

            // Calculate next aggregation end time using lastEmittedAggTime as base
            // NOTE: an aggregation timestamp is the time of the beginning of the aggregation window
            var nextAggEndEpoch = MetricUtils.EpochFromNaiveUTCDatetime(lastEmittedAggTime) + aggSec + aggSec;
            // Fudge factor to account for streaming and processing latencies upstream
            var latencyAllowanceSec = aggSec;

            while (!CancelToken.IsCancellationRequested)
            {
                var aggHarvestEpoch = nextAggEndEpoch + latencyAllowanceSec;
                var now = DateTime.Now.TimeOfDay.TotalSeconds;
                while (now < aggHarvestEpoch) //Sleep until it's time to aggregate metric data
                {
                    Thread.Sleep((int) ((aggHarvestEpoch - now)* 1000));
                    now = DateTime.Now.TimeOfDay.TotalSeconds;
                }
                // Aggregate and forward metric samples to htmengine's Metric Listener
                lastEmittedAggTime = ForwardTweetVolumeMetrics(lastEmittedAggTime: lastEmittedAggTime,
                    stopDatetime: new DateTime(0, DateTimeKind.Utc) + TimeSpan.FromSeconds(nextAggEndEpoch));

                nextAggEndEpoch += aggSec;
                Thread.Sleep(1);
            }
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

        #region Forwarder Methods

        /// <summary>
        /// Query tweet volume metrics since the given last emitted aggregation time
        /// through stopDatetime and forward them to Taurus.Update
        /// the datetime of the last successfully-emitted tweet volume metric batch in
        /// the database.
        /// </summary>
        /// <param name="lastEmittedAggTime"></param>
        /// <param name="stopDatetime"></param>
        /// <returns></returns>
        private DateTime ForwardTweetVolumeMetrics(DateTime lastEmittedAggTime, DateTime stopDatetime)
        {
            var periodTimedelta = TimeSpan.FromSeconds(_storerArgs.AggSec);
            var aggStartDatetime = lastEmittedAggTime + periodTimedelta;

            while (aggStartDatetime < stopDatetime)
            {
                // Aggregate and forward Tweet Volume metrics for one aggregation interval
                try
                {
                    AggregateAndForward(aggStartDatetime: aggStartDatetime,
                        stopDatetime: aggStartDatetime + periodTimedelta);
                }
                catch (Exception)
                {
                    return lastEmittedAggTime;
                }
                // Update db with last successfully-emitted datetime
                MetricUtils.UpdateLastEmittedSampleDatetime(key: EMITTED_TWEET_VOLUME_SAMPLE_TRACKER_KEY, sampleDatetime: aggStartDatetime);
                // Set up for next iteration
                lastEmittedAggTime = aggStartDatetime;
                aggStartDatetime += periodTimedelta;
            }
            return lastEmittedAggTime;
        }

        /// <summary>
        /// Aggregate tweet volume metrics in the given datetime range and forward them to Taurus Engine.
        /// </summary>
        /// <param name="aggStartDatetime">UTC datetime of first aggregation to be performed and emitted</param>
        /// <param name="stopDatetime">non-inclusive upper bound UTC datetime for forwarding</param>
        /// <param name="metrics">optional sequence of metric names; if specified (non-None), 
        /// the operation will be limited to the given metric names</param>
        private void AggregateAndForward(DateTime aggStartDatetime, DateTime stopDatetime, string[] metrics = null)
        {
            // Emit samples to Taurus Engine
            // with metric_utils.metricDataBatchWrite(log=g_log) as putSample:
            foreach (var sample in GetSamples(aggStartDatetime, stopDatetime, metrics))
            {
                try
                {
                    DataBatchWritePutSample(sample);
                }
                catch (Exception e)
                {
                    Log.ErrorFormat("Failure while emiiting metric data sample={0} : {1}", sample, e);
                    throw;
                }
            }
        }

        /// <summary>
        /// Retrieve and yield metric data samples of interest
        /// </summary>
        /// <param name="aStartDatetime"></param>
        /// <returns></returns>
        private IEnumerable<TwitterMetricSample> GetSamples(DateTime aStartDatetime, DateTime stopDatetime, string[] metrics = null)
        {
            TimeSpan periodTimedelta = TimeSpan.FromSeconds(_storerArgs.AggSec);
            while (aStartDatetime < stopDatetime)
            {
                // Query Tweet Volume metrics for one aggregation interval
                IDictionary<string, int> metricToVolumeMap = QueryTweetVolumes(aStartDatetime, metrics);
                // Generate metric samples
                var epochTimestamp = MetricUtils.EpochFromNaiveUTCDatetime(aStartDatetime);
                var samples = _collectorArgs.MetricSpecs.Where(
                    spec => metrics == null || metrics.Any(m => m == spec.Metric))
                    .Select(spec => new TwitterMetricSample
                    {
                        MetricName = spec.Metric,
                        Value = metricToVolumeMap[spec.Metric],
                        EpochTimestamp = epochTimestamp
                    })
                    .ToList();
                if (Log.IsDebugEnabled)
                {
                    Log.DebugFormat("Samples={0}", Arrays.ToString(samples));
                }
                foreach (var sample in samples)
                {
                    yield return sample;
                }
                Log.InfoFormat("Yielded numSamples={0} for agg={1}", samples.Count, aStartDatetime);
                // Set up for next iteration
                aStartDatetime += periodTimedelta;
            }
        }

        /// <summary>
        /// Query the database for the counts of tweet metric volumes for the specified aggregation.
        /// </summary>
        /// <param name="aggDatetime">aggregation timestamp</param>
        /// <param name="metrics">optional sequence of metric names; if specified (non-None),
        /// the operation will be limited to the given metric names</param>
        /// <returns>a sparse sequence of two-tuples: (metric_name, count); metrics
        /// that have no tweets in the given aggregation period will be absent from
        /// the result.</returns>
        private IDictionary<string, int> QueryTweetVolumes(DateTime aggDatetime, string[] metrics = null)
        {
            return RepositoryFactory.TwitterTweetSamples.QueryVolumesAggWithMetricFilter(aggDatetime, metrics);
        }

        private void DataBatchWritePutSample(TwitterMetricSample sample)
        {
            // This normmaly makes batches and then sends it to the taurus engine, we
            // send it at this moment one by one.
            // destination on queue is in original version: taurus.metric.custom.data
            // metric listener just forwards and drops to the same queue
            MetricStorer.Instance.AddToQueue(new MetricMessage
            {
                MetricName = sample.MetricName,
                Timestamp = MetricUtils.DatetimeFromEpoch(sample.EpochTimestamp),
                Value = sample.Value
            });
            throw new NotImplementedException("ends up in the metric storer through the queue");
        }

        private class TwitterMetricSample
        {
            public string MetricName { get; set; }
            public int Value { get; set; }
            public double EpochTimestamp { get; set; }
        }

        #endregion

        #region Collector Methods

        /// <summary>
        /// Load metric specs for the "twitter" provider, called upon initialization
        /// </summary>
        /// <returns></returns>
        private List<TwitterMetricSpec> LoadMetricSpecs()
        {
            List<TwitterMetricSpec> specs = new List<TwitterMetricSpec>();
            foreach (var resPair in MetricUtils.GetMetricsConfiguration())
            {
                string resName = resPair.Key;
                var resVal = resPair.Value;
                foreach (var metricPair in resVal.Metrics)
                {
                    string metricName = metricPair.Key;
                    var metricVal = metricPair.Value;
                    if (metricVal.Provider != "twitter") continue;
                    specs.Add(new TwitterMetricSpec
                    {
                        Resource = resName,
                        Metric = metricName,
                        ScreenNames = metricVal.ScreenNames,
                        Symbol = resVal.Symbol.ToLower()
                    });
                }
            }
            return specs;
        }

        private void CheckHealth()
        {
            if (StoringTask.Status != TaskStatus.Running)
            {
                Log.Fatal("Exiting streaming process, because our storage thread has stopped");
                StopCollector();
            }
        }

        private TaggingMap BuildTagMapAndStreamFilterParams(List<TwitterMetricSpec> metricSpecs,
            ITwitterCredentials cred, IFilteredStream tweetStream)
        {
            Map<string, string> symbolToMetricMap = new Map<string, string>();
            Map<long, List<string>> userIdToMetricsMap = new Map<long, List<string>>();

            TaggingMap taggingMap = new TaggingMap
            {
                TagOnSymbols = symbolToMetricMap,
                TagOnMentions = userIdToMetricsMap,
                TagOnSourceUsers = userIdToMetricsMap
            };

            var screenNameToMetricsMap = new Map<string, List<string>>();

            foreach (var spec in metricSpecs)
            {
                symbolToMetricMap[spec.Symbol] = spec.Metric;
                foreach (string screenName in spec.ScreenNames)
                {
                    if (!screenNameToMetricsMap.ContainsKey(screenName))
                    {
                        screenNameToMetricsMap[screenName] = new List<string>();
                    }
                    screenNameToMetricsMap[screenName].Add(spec.Metric);
                }
            }

            // Get twitter Ids corresponding to screen names and build a userId-to-metric map
            int maxLookupItems = 100; // twitter's users/lookup limit
            var screenNames = screenNameToMetricsMap.Keys;
            var lookupSlices = ArrayUtils.XRange(0, screenNames.Count, maxLookupItems).Select(n => screenNames.Skip(n).Take(maxLookupItems)).ToList();
            HashSet<string> mappedScreenNames = new HashSet<string>();
            foreach (IEnumerable<string> names in lookupSlices)
            {
                // lookup user on twitter
                var users = User.GetUsersFromScreenNames(names);
                foreach (IUser user in users)
                {
                    string screenName = user.ScreenName.ToLower();
                    long userId = user.Id;
                    Log.InfoFormat("screenName={0} mapped to userId={1}", screenName, userId);
                    userIdToMetricsMap[userId] = screenNameToMetricsMap[screenName];
                    mappedScreenNames.Add(screenName);
                }
            }
            HashSet<string> unMappedScreenNames = new HashSet<string>(screenNames);
            unMappedScreenNames.ExceptWith(mappedScreenNames);
            if (unMappedScreenNames.Any())
            {
                Log.ErrorFormat("No mappings found for screennames: {0}", Arrays.ToString(unMappedScreenNames.AsEnumerable()));
            }
            // Generate stream filter parameters
            tweetStream.StallWarnings = true;
            foreach (var screen in screenNameToMetricsMap)
            {
                tweetStream.AddTrack("@" + screen.Key);
            }
            //foreach (var ticker in symbolToMetricMap)
            //{
            //    Debug.WriteLine("Adding " + ticker.Key);
            //    tweetStream.AddTrack("#" + ticker.Key);
            //}
            foreach (var userId in userIdToMetricsMap.Keys)
            {
                tweetStream.AddFollow(userId);
            }
            return taggingMap;
        }

        #endregion

        #region Storer Methods

        /// <summary>
        /// Process the messages from TwitterStreamListener and update stats; they
        /// could be(re)tweets or notifications, such as "limit", "delete", "warning",
        /// etc., or other meta information, such as ConnectionMarker that indicates a
        /// newly-created Streaming API connection.
        /// 
        /// See https://dev.twitter.com/streaming/overview/messages-types
        /// 
        /// Tweets that match one or more metrics and delete notifications are returned
        /// to caller. Other notifications of interest are logged.
        /// </summary>
        /// <param name="messages">messages received from our TwitterStreamListener</param>
        /// <returns>a pair (tweets, deletes), where `tweets` is a possibly empty 
        /// sequence of tweet status dicts each matching at least one metric and 
        /// tagged via `TweetStorer._tagMessage()`; and `deletes` is a possibly empty
        /// sequence of "delete" notification dicts representing tweet statuses to be
        /// deleted.</returns>
        private Tuple<List<TwitterMessage>, List<TwitterMessage>> ReapMessages(List<TwitterMessage> messages)
        {
            var streamStats = _currentStreamStats;
            var runtimeStats = _runtimeStreamingStats;

            List<TwitterMessage> tweets = new List<TwitterMessage>();
            List<TwitterMessage> deletes = new List<TwitterMessage>();

            foreach (TwitterMessage msg in messages)
            {
                if (msg is ConnectionMarker)
                {
                    _currentStreamStats = new CurrentStreamStats();
                    _currentStreamStats.StartingDatetime = DateTime.Now;
                    streamStats = _currentStreamStats;
                    if (!_runtimeStreamingStats.StreamNumber.HasValue)
                    {
                        _runtimeStreamingStats.StreamNumber = 1;
                    }
                    else
                    {
                        _runtimeStreamingStats.StreamNumber += 1;
                    }
                }
                else
                {
                    if (msg.TweetData.InReplyToStatusId.HasValue)
                    {
                        // Got a tweet of some sort
                        streamStats.NumTweets += 1;
                        runtimeStats.NumTweets += 1;
                        // Tag tweet with metric names that match it, if any
                        TagMessage(msg);
                        if (msg.MetricTagSet.Any())
                        {
                            // Matched one or more metrics
                            tweets.Add(msg);
                        }
                        else
                        {
                            streamStats.NumUntaggedTweets += 1;
                            runtimeStats.NumUntaggedTweets += 1;
                        }
                    }
                    else if (msg.TweetData.IsTweetDestroyed)
                    {
                        deletes.Add(msg);
                        streamStats.NumDeleteStatuses += 1;
                        runtimeStats.NumDeleteStatuses += 1;
                    }
                    //else if (msg.TweetData)
                    //{
                    //    deletes.Add(msg);
                    //    streamStats.NumDeleteStatuses += 1;
                    //    runtimeStats.NumDeleteStatuses += 1;
                    //}
                }
            }

            return new Tuple<List<TwitterMessage>, List<TwitterMessage>>(tweets, deletes);
        }

        /// <summary>
        /// Tag message: add "metricTagSet" attribute to the message; the value
        /// of "metricTagSet" is a possibly-empty set containing metric name(s) that
        /// match the containing message.
        /// </summary>
        /// <param name="msg">Twitter status object</param>
        private void TagMessage(TwitterMessage msg)
        {
            msg.MetricTagSet = new List<string>();
            foreach (var dataPair in _storerArgs.TaggingMap.TagOnSymbols)
            {
                //msg.TweetData.Hashtags[0].Text
                string tagger = dataPair.Key;
                if (msg.TweetData.Hashtags.Any(ht => ht.Text == tagger))
                {
                    string mappings = dataPair.Value;
                    msg.MetricTagSet.Add(mappings);
                }
            }
            foreach (var dataPair in _storerArgs.TaggingMap.TagOnMentions)
            {
                //msg.TweetData.Hashtags[0].Text
                long tagger = dataPair.Key;
                if (msg.TweetData.UserMentions.Any(um => um.Id == tagger))
                {
                    List<string> mappings = dataPair.Value;
                    msg.MetricTagSet.AddRange(mappings);
                }
            }
            foreach (var dataPair in _storerArgs.TaggingMap.TagOnSourceUsers)
            {
                //msg.TweetData.Hashtags[0].Text
                long tagger = dataPair.Key;
                if (msg.TweetData.Contributors.Any(c => c == tagger))
                {
                    List<string> mappings = dataPair.Value;
                    msg.MetricTagSet.AddRange(mappings);
                }
            }
        }

        /// <summary>
        /// Save tweets and references in database
        /// See https://dev.twitter.com/overview/api/tweets
        /// 
        /// </summary>
        /// <param name="messages">sequence of tweet dict received from twitter with an
        /// additional "metricTagSet" attribute; the value of "metricTagSet" is a set
        /// containing metric name(s) that match the containing message</param>
        /// <param name="aggRefDatetime">aggregation reference time for determining
        /// aggregation timestamp of the given messages</param>
        private void SaveTweets(List<TwitterMessage> messages, DateTime aggRefDatetime)
        {
            List<TwitterTweet> tweetRows = new List<TwitterTweet>();
            List<TwitterTweetSample> referenceRows = new List<TwitterTweetSample>();
            foreach (var msg in messages)
            {
                var tweetRefs = CreateTweetAndReferenceRows(msg, aggRefDatetime);
                var tweet = tweetRefs.Item1;
                var references = tweetRefs.Item2;
                tweetRows.Add(tweet);
                referenceRows.AddRange(references);
            }

            // Save twitter message
            RepositoryFactory.TwitterTweets.InsertRows(tweetRows);
            // Save corresponding references
            RepositoryFactory.TwitterTweetSamples.InsertRows(referenceRows);
        }

        /// <summary>
        /// Generate a tweet row and corresponding reference rows from a tagged
        /// tweet for saving to the database(some tweets may match multiple metrics)
        /// </summary>
        /// <param name="msg">a tweet dict received from twitter with an additional
        /// "metricTagSet" attribute; the value of "metricTagSet" is a set containing
        /// metric name(s) that match the containing message</param>
        /// <param name="aggRefDatetime">aggregation reference time for determining
        /// aggregation timestamp of the given messages</param>
        /// <returns>
        /// a two-tuple (<tweet_row>, <reference_rows>)
        ///   tweet_row: a dict representing the tweet row for inserting into
        ///     schema.twitterTweets
        ///   reference_rows: a sequence of dicts representing tweet reference rows for
        ///     inserting into schema.twitterTweetSamples
        /// </returns>
        /// <remarks>
        ///     Additional candidate fields:
        ///  coordinates
        ///    If saving coordinates, then must also honor "Location deletion notices
        ///    (scrub_geo)" per
        ///    https://dev.twitter.com/streaming/overview/messages-types
        ///  favorite_count (most likely not useful in this context, since subsequent
        ///    (un)favoriting would not be udpated in the db so the value will often be
        ///    way off.
        ///  retweet_count (same concern as with favorite_count)
        ///  retweeted_status (original tweet inside retweet; perhaps this may be used
        ///    to update retweet_count in the original, and even save it as original in
        ///    case we didn't already have it; *not helpful for updating
        ///    favorite_count, since something may be (un)favorited without being
        ///    retweted)
        ///  truncated
        ///  withheld_copyright, withheld_in_countries, withheld_scope: it turns out
        ///    that content may be withheld due to DMCA complaint in certain countries
        ///    or everywhere. *We also need to figure out whether we need to abide by
        ///    this in Taurus Client; if we do, then we need to start handling
        ///    "withheld content notices" described in
        ///    https://dev.twitter.com/streaming/overview/messages-types; also, the doc
        ///    doesn't shed light whether/how "unwithholding" is communicated (empty
        ///    country list?)
        /// </remarks>
        private Tuple<TwitterTweet, List<TwitterTweetSample>> CreateTweetAndReferenceRows(TwitterMessage msg, DateTime aggRefDatetime)
        {
            string msgId = msg.TweetData.IdStr;
            bool isRetweet = msg.TweetData.Retweeted;
            DateTime createdAt = msg.TweetData.CreatedAt;
            var contributors = msg.TweetData.Contributors;

            TwitterTweet tweetRow = new TwitterTweet();
            tweetRow.Uid = msgId;
            tweetRow.CreatedAt = createdAt;
            tweetRow.ReTweet = isRetweet;
            tweetRow.Lang = msg.TweetData.Language.ToString();
            tweetRow.Text = msg.TweetData.Text;
            tweetRow.UserId = msg.TweetData.CreatedBy.IdStr;
            tweetRow.UserName = msg.TweetData.CreatedBy.ScreenName;
            tweetRow.RealName = msg.TweetData.CreatedBy.Name;
            tweetRow.RetweetCount = isRetweet ? msg.TweetData.RetweetCount : (int?)null;
            tweetRow.RetweetedUserid = isRetweet ? msg.TweetData.RetweetedTweet.CreatedBy.IdStr : null;
            tweetRow.RetweetedUsername = isRetweet ? msg.TweetData.RetweetedTweet.CreatedBy.ScreenName : null;
            tweetRow.RetweetedRealName = isRetweet ? msg.TweetData.RetweetedTweet.CreatedBy.Name : null;
            tweetRow.InReplyToStatusId = msg.TweetData.InReplyToStatusIdStr;
            tweetRow.InReplyToUserid = msg.TweetData.InReplyToUserIdStr;
            tweetRow.InReplyToUsername = msg.TweetData.InReplyToScreenName;
            tweetRow.Contributors = JsonConvert.SerializeObject(contributors);

            // Compute aggregation timestamp as the lower aggregation boundary relative
            // to the given reference (required by Taurus-Mobile)
            DateTime aggDatetime = MetricUtils.AggTimestampFromSampleTimestamp(
                sampleDatetime: createdAt, aggRefDatetime : aggRefDatetime, aggSec: _storerArgs.AggSec);

            List<TwitterTweetSample> referenceRows = msg.MetricTagSet.Select(m => new TwitterTweetSample
            {
                agg_ts = aggDatetime,
                msg_uid = msgId,
                Metric = m
            }).ToList();

            return new Tuple<TwitterTweet, List<TwitterTweetSample>>(tweetRow, referenceRows);
        }

        /// <summary>
        /// Save tweet deletion request in database
        /// </summary>
        /// <param name="messages">sequence of Twitter "delete" status dicts https://dev.twitter.com/streaming/overview/messages-types</param>
        private void SaveTweetDeletionRequests(List<TwitterMessage> messages)
        {
            throw new NotImplementedException();
        }

        private void LogStreamStats()
        {
            Log.InfoFormat("Current stream stats: {0}", _currentStreamStats);
            Log.InfoFormat("Runtime streamin stats: {0}", _runtimeStreamingStats);
        }

        #endregion
    }

    // https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/taurus.metric_collectors/taurus/metric_collectors/twitterdirect/twitter_direct_agent.py
    // TODO
}