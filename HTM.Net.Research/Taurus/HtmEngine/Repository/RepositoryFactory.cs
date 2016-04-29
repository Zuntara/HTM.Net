namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public static class RepositoryFactory
    {
        static RepositoryFactory()
        {
            Metric = new MetricMemRepository();
            MetricData = new MetricDataMemRepository();
            EmittedSampleTracker = new EmittedSampleTrackerMemRepository();
        }

        public static IMetricRepository Metric { get; set; }
        public static IMetricDataRepository MetricData { get; set; }
        public static IEmittedSampleTrackerRepository EmittedSampleTracker { get; set; }
    }
}