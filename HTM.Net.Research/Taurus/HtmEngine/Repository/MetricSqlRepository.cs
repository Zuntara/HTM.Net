using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using HTM.Net.Research.Taurus.HtmEngine.runtime;
using HTM.Net.Util;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    /// <summary>
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/repository/queries.py
    /// </summary>
    public class MetricSqlRepository : IMetricRepository
    {
        public event Action<Metric> MetricAdded;

        public Metric GetMetric(string metricId)
        {
            // sel = select(fields).where(schema.metric.c.uid == metricId).First()
            throw new NotImplementedException();
        }

        public int GetMetricDataCount(string metricId)
        {
            //sel = (select([func.count()], from_obj=schema.metric_data).where(schema.metric_data.c.uid == metricId)).first()[0]
            throw new NotImplementedException();
        }

        /// <summary>
        /// Update existing metric
        /// </summary>
        /// <param name="metricId"></param>
        /// <param name="update">fields with values to update</param>
        public void UpdateMetricColumns(string metricId, Map<string, object> update)
        {
            throw new NotImplementedException();
        }

        public void UpdateMetricColumnsForRefStatus(string uid, MetricStatus refStatus, Map<string, object> objects)
        {
            throw new NotImplementedException();
        }

        public MetricStatistic GetMetricStats(string metricId)
        {
            /*
             sel = (select([func.min(schema.metric_data.c.metric_value),
                            func.max(schema.metric_data.c.metric_value)],
                        from_obj=schema.metric_data)
                .where(schema.metric_data.c.uid == metricId))

            if result.rowcount > 0: 
                statMin, statMax = result.first().values()
            if statMin is not None and statMax is not None:
                return {"min": statMin, "max": statMax}
             raise MetricStatisticsNotReadyError()
             */
            throw new NotImplementedException();
        }

        public Metric GetCustomMetricByName(string metricName)
        {
            // where = ((schema.metric.c.name == name) & (schema.metric.c.datasource == "custom")).first()
            throw new NotImplementedException();
        }

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
        public Metric AddMetric(string uid = null, string datasource = null, string name = null,
            string description = null, string server = null, string location = null,
            string parameters = null, MetricStatus? status = null, string message = null, string collectorError = null,
            DateTime? lastTimestamp = null, int? pollInterval = null, string tagName = null, string modelParams = null,
            long lastRowId = 0)
        {
            uid = uid ?? Guid.NewGuid().ToString();

            // TODO: insert into database

            throw new NotImplementedException();
        }

        public int GetProcessedMetricDataCount(string metricId)
        {
            throw new NotImplementedException();
        }

        public List<MetricData> GetMetricDataWithRawAnomalyScoresTail(string metricId, int limit)
        {
            throw new NotImplementedException();
        }

        public void SetMetricStatus(string metricId, MetricStatus status, string message = null, MetricStatus? refStatus = null)
        {
            throw new NotImplementedException();
        }

        public List<MetricData> GetMetricData(string metricId = null, long? rowId = null, long? start = null, long? stop = null,
            int? limit = null, DateTime? fromTimestamp = null, DateTime? toTimestamp = null,
            double? score = null, Expression<Func<MetricData, object>> sort = null, bool? sortAsc = null)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Add Metric Data
        /// </summary>
        /// <param name="metricId">Metric uid</param>
        /// <param name="data"></param>
        public List<MetricData> AddMetricData(string metricId, List<Tuple<DateTime, double>> data)
        {
            throw new NotImplementedException();
        }

        public List<Metric> GetAllModels()
        {
            throw new NotImplementedException();
        }

        public void DeleteModel(string metricId)
        {
            throw new NotImplementedException();
        }

        public List<Metric> GetCustomMetrics()
        {
            throw new NotImplementedException();
        }
    }
}