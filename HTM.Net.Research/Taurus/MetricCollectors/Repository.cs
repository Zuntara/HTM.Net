using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using HTM.Net.Research.Taurus.HtmEngine.Adapters;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using log4net;
using Newtonsoft.Json;

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
    public class TwitterTweets
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
        public int RetweetCount { get; set; }
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
    public class TwitterTweetSamples
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

            CollectionTask.ContinueWith(DoTaskCompletionJob);
            StoringTask.ContinueWith(DoTaskCompletionJob);
            GarbageCollectionTask.ContinueWith(DoTaskCompletionJob);
            ForwarderTask.ContinueWith(DoTaskCompletionJob);
        }

        protected virtual void DoTaskCompletionJob(Task parentTask)
        {
            if (parentTask.Status == TaskStatus.Faulted)
            {
                Log.ErrorFormat("Task failed with exception: {0}", parentTask.Exception);
            }
            else
            {
                Log.InfoFormat("Task ended with status {0}", parentTask.Status);
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
        public object TaggingMap { get; set; }
    }

    public class TwitterMessage
    {
        
    }

    public class TwitterCollectorAgent : MetricCollectorAgent
    {
        private readonly TwitterStorerArguments _storerArgs;
        private readonly ConcurrentQueue<TwitterMessage> _messageQueue;

        private const string EMITTED_TWEET_VOLUME_SAMPLE_TRACKER_KEY = "twitter-tweets-volume";

        protected override void ExecuteCollectionTask()
        {
            while (!CancelToken.IsCancellationRequested)
            {
                // TODO: monitor twitter and send data to internal queue to be picked up
                // by storer and then by forwarder
                Thread.Sleep(1);
            }
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

        public TwitterCollectorAgent(TwitterStorerArguments storerArgs)
        {
            _messageQueue = new ConcurrentQueue<TwitterMessage>();
            _storerArgs = storerArgs;
        }

        /// <summary>
        /// Thread function; preprocess and store incoming tweets deposited by
        /// twitter streamer into self._msgQ
        /// </summary>
        protected override void ExecuteStoringTask()
        {
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
                var tweets = reapStats.Item1;
                var deletes = reapStats.Item2;
                // Save (re)tweets
                if (tweets != null)
                {
                    SaveTweets(messages: tweets, affRefDatetime: aggRefDateTime);
                }
                // Save deletion requests
                if (deletes != null)
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
            while (!CancelToken.IsCancellationRequested)
            {
                Thread.Sleep(1);
            }
            if (CancelToken.IsCancellationRequested)
            {
                CancelToken.ThrowIfCancellationRequested();
            }
        }

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
            throw new NotImplementedException();
        }

        private void SaveTweets(List<TwitterMessage> messages, DateTime affRefDatetime)
        {
            throw new NotImplementedException();
        }

        private void SaveTweetDeletionRequests(List<TwitterMessage> messages)
        {
            throw new NotImplementedException();
        }

        private void LogStreamStats()
        {
            throw new NotImplementedException();
        }

        #endregion
    }

    // https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/taurus.metric_collectors/taurus/metric_collectors/twitterdirect/twitter_direct_agent.py
    // TODO
}