namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public static class RepositoryFactory
    {
        static RepositoryFactory()
        {
            Metric = new MetricMemRepository();
        }

        public static IMetricRepository Metric { get; set; }
        public static IMetricDataRepository MetricData { get; set; }
    }
}