using System.Collections.Generic;
using System.Drawing;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ExplorerPosition
    {
        public int? Image { get; set; }
        //public List<Tuple<int?, ExplorerPosition>> Filters { get; set; }
        public List<int> Filters { get; set; }
        public Point? Offset { get; set; }
        public bool? Reset { get; set; }
    }
}