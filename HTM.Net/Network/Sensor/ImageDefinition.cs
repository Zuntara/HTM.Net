using System.Drawing;

namespace HTM.Net.Network.Sensor
{
    /// <summary>
    /// Class used to transfer images with meta-data through the sensor and network
    /// </summary>
    public class ImageDefinition
    {
        public int RecordNum { get; set; }
        public Bitmap Image { get; set; }
        public string ImageInputField { get; set; }
        public int[] CategoryIndices { get; set; }

        public int[] InputVector { get; set; }
    }
}