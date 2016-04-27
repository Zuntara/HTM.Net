using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Util;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
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
}