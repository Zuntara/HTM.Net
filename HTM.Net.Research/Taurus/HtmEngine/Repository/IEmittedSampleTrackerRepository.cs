using System;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public interface IEmittedSampleTrackerRepository
    {
        void Insert(string key, DateTime sampleTs);
        DateTime? GetSampleTsFromKey(string key);
    }
}