using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using HTM.Net.Research.Taurus.HtmEngine.Runtime;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository;

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

public class MetricDataMemRepository : IMetricDataRepository
{
    private List<MetricData> _metricData;
    private int _lastRowId;

    public MetricDataMemRepository()
    {
        _metricData = new List<MetricData>();
        _lastRowId = 0;
    }

    public int GetMetricDataCount(string metricId)
    {
        //sel = (select([func.count()], from_obj=schema.metric_data).where(schema.metric_data.c.uid == metricId)).first()[0]
        return _metricData.Count(md => md.MetricId == metricId);
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
}