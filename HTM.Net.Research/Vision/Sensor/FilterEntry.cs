using HTM.Net.Research.Vision.Sensor.Filters;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision.Sensor
{
    public class FilterConfig
    {
        public string FilterName { get; set; }
        public Map<string, object> FilterArgs { get; set; }
    }

    public class FilterEntry : FilterConfig
    {
        public BaseFilter Filter { get; set; }
    }
}