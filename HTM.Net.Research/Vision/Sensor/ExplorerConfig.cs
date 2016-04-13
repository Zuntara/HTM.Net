using HTM.Net.Util;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ExplorerConfig
    {
        public ExplorerConfig()
        {
            ExplorerArgs = new Map<string, object>();
        }
        public string ExplorerName { get; set; }
        public Map<string, object> ExplorerArgs { get; set; }
    }
}