using System;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    /// <summary>
    /// Table MetricData
    /// </summary>
    public class MetricData
    {
        public MetricData()
        {

        }
        public MetricData(string metricId, DateTime timestamp, double metricValue,
            double? anomalyScore, long rowid)
        {
            Uid = Guid.NewGuid().ToString();

            MetricId = metricId;
            Timestamp = timestamp;
            MetricValue = metricValue;
            AnomalyScore = anomalyScore;
            Rowid = rowid;
        }
        public string Uid { get; set; }
        public long Rowid { get; set; }
        public DateTime Timestamp { get; set; }
        public double MetricValue { get; set; }
        public double? AnomalyScore { get; set; }
        public double? RawAnomalyScore { get; set; }


        public int DisplayValue { get; set; }
        public string MetricId { get; set; }
    }
}