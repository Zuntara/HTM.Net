using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Taurus.MetricCollectors;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository;

public interface ITwitterTweetSamplesRepository
{
    IDictionary<string, int> QueryVolumesAggWithMetricFilter(DateTime aggDatetime, string[] metrics);
    void InsertRows(List<TwitterTweetSample> twitterTweetSamples);
}

public interface ITwitterTweetRepository
{
    void InsertRows(List<TwitterTweet> tweetRows);
}

public class TwitterTweetSamplesMemRepository : ITwitterTweetSamplesRepository
{
    private List<TwitterTweetSample> _twitterTweetSamples;

    public TwitterTweetSamplesMemRepository()
    {
        _twitterTweetSamples = new List<TwitterTweetSample>();
    }

    public IDictionary<string, int> QueryVolumesAggWithMetricFilter(DateTime aggDatetime, string[] metrics)
    {
        var query = _twitterTweetSamples
            .Where(tt => tt.agg_ts == aggDatetime);

        if (metrics != null)
        {
            query = query.Where(tt => metrics.Any(m => m == tt.Metric));
        }

        var groupedQuery = query.GroupBy(k => k.Metric, e => e.Metric, (m, c) => new { metric = m, cnt = c.Count() });

        return groupedQuery.ToDictionary(a => a.metric, a => a.cnt);
    }

    public void InsertRows(List<TwitterTweetSample> twitterTweetSamples)
    {
        _twitterTweetSamples.AddRange(twitterTweetSamples);
    }
}

public class TwitterTweetRepository : ITwitterTweetRepository
{
    private List<TwitterTweet> _twitterTweets;

    public TwitterTweetRepository()
    {
        _twitterTweets = new List<TwitterTweet>();
    }

    public void InsertRows(List<TwitterTweet> tweetRows)
    {
        _twitterTweets.AddRange(tweetRows);
    }
}