using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HTM.Net.Research.Taurus.HtmEngine.runtime;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public interface IMetricDataRepository
    {
        int GetMetricDataCount(string metricId);

        MetricStatistic GetMetricStats(string metricId);

        /// <summary>
        /// Get count of processed MetricData for the given metricId.
        /// </summary>
        /// <param name="metricId"></param>
        /// <returns></returns>
        int GetProcessedMetricDataCount(string metricId);

        /// <summary>
        /// Get MetricData ordered by timestamp, descending
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="limit">Limit on number of results to return</param>
        /// <returns></returns>
        List<MetricData> GetMetricDataWithRawAnomalyScoresTail(string metricId, int limit);

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
        List<MetricData> GetMetricData(string metricId = null, long? rowId = null, long? start = null, long? stop = null, int? limit = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null,
            double? score = null, Expression<Func<MetricData, object>> sort = null, bool? sortAsc = null);

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        List<MetricData> AddMetricData(string metricId, List<Tuple<DateTime, double>> data);
    }
}