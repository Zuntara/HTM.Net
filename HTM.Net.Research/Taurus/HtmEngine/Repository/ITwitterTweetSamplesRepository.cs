using System;
using System.Collections.Generic;
using HTM.Net.Research.Taurus.MetricCollectors;
using HTM.Net.Util;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public interface ITwitterTweetSamplesRepository
    {
        IDictionary<string, int> QueryVolumesAggWithMetricFilter(DateTime aggDatetime, string[] metrics);
        void InsertRows(List<TwitterTweetSample> twitterTweetSamples);
    }

    public interface ITwitterTweetRepository
    {
        void InsertRows(List<TwitterTweet> tweetRows);
    }
}