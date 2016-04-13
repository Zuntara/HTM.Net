using System.Collections.Generic;
using HTM.Net.Util;
using Kaliko.ImageLibrary;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ImageListEntry
    {
        public KalikoImage Image { get; set; }
        public string ImagePath { get; set; }
        public object AuxData { get; set; }
        public string[] AuxPath { get; set; }
        public bool ManualAux { get; set; }
        public string MaskPath { get; set; }
        public bool Erode { get; set; }
        public string CategoryName { get; set; }
        public int? CategoryIndex { get; set; }
        public int? PartitionId { get; set; }
        public Dictionary<Tuple, List<KalikoImage>> Filtered { get; set; }
        public int? SequenceIndex { get; set; }
        public int? FrameIndex { get; set; }

        public bool HasMask { get { return !string.IsNullOrWhiteSpace(MaskPath); } }

        public ImageListEntry Copy()
        {
            ImageListEntry entry = new ImageListEntry();

            entry.Image = Image.Clone();
            entry.ImagePath = ImagePath;
            entry.AuxData = AuxData;
            entry.AuxPath = AuxPath;
            entry.ManualAux = ManualAux;
            entry.MaskPath = MaskPath;
            entry.Erode = Erode;
            entry.CategoryName = CategoryName;
            entry.CategoryIndex = CategoryIndex;
            entry.PartitionId = PartitionId;
            entry.Filtered = new Dictionary<Tuple, List<KalikoImage>>(Filtered);
            entry.SequenceIndex = SequenceIndex;
            entry.FrameIndex = FrameIndex;

            return entry;
        }
    }
}