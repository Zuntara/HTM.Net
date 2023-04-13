using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Taurus.HtmEngine.Runtime;
using HTM.Net.Util;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository;

public interface IMetricRepository
{
    event Action<Metric> MetricAdded;

    Metric GetMetric(string metricId);
    /// <summary>
    /// Update existing metric
    /// </summary>
    /// <param name="metricId"></param>
    /// <param name="update">fields with values to update</param>
    void UpdateMetricColumns(string metricId, Map<string, object> update);
    void UpdateMetricColumnsForRefStatus(string uid, MetricStatus refStatus, Map<string, object> objects);

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

    List<Metric> GetAllModels();
    void DeleteModel(string metricId);
    List<Metric> GetCustomMetrics();
}

public class MetricMemRepository : IMetricRepository
{
    private List<Metric> _metric;
    public event Action<Metric> MetricAdded;

    public MetricMemRepository()
    {
        _metric = new List<Metric>();
    }

    public Metric GetMetric(string metricId)
    {
        if (_metric.Any(m => m.Uid == metricId))
            return _metric.First(m => m.Uid == metricId);
        throw new MetricNotFound();
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