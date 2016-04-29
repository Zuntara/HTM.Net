using System;
using System.Collections.Generic;
using System.Linq;

namespace HTM.Net.Research.Taurus.HtmEngine.Repository
{
    public class EmittedSampleTrackerMemRepository : IEmittedSampleTrackerRepository
    {
        private List<EmittedSampleTracker> _sampleData;

        public EmittedSampleTrackerMemRepository()
        {
            _sampleData = new List<EmittedSampleTracker>();
        }

        public void Insert(string key, DateTime sampleTs)
        {
            _sampleData.Add(new EmittedSampleTracker()
            {
                Key = key,
                SampleTs = sampleTs
            });
        }

        public DateTime? GetSampleTsFromKey(string key)
        {
            var found = _sampleData.FirstOrDefault(sd => sd.Key == key);
            return found?.SampleTs;
        }
    }
}