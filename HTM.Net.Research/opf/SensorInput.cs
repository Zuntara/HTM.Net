using System.Collections.Generic;
using HTM.Net.Util;

namespace HTM.Net.Research.opf
{
    public class SensorInput
    {
        public SensorInput(object dataRow = null, Map<string, object> dataDict = null, List<object> dataEncodings = null,
            bool? sequenceReset = null, int? category = null)
        {
            this.dataRow = dataRow;
            this.dataDict = dataDict;
            this.dataEncodings = dataEncodings;
            this.sequenceReset = sequenceReset;
            this.category = category;
        }

        public object dataRow { get; set; }
        public Map<string, object> dataDict { get; set; }
        public List<object> dataEncodings { get; set; }
        public bool? sequenceReset { get; set; }
        public int? category { get; set; }
    }
}