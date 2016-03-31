using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Xml.Linq;

namespace HTM.Net.Research.Vision
{
    public class DatasetReader
    {
        /// <summary>
        /// This routine reads the XML files that contain the paths to the images and the
        /// tags which indicate what is in the image (i.e. "ground truth").
        /// </summary>
        public static Util.Tuple GetImagesAndTags(string filename)
        {
            Debug.WriteLine("Reading data set : " + filename);

            var xmlDocument = XDocument.Load(filename);

            string[] currentDirParts = Environment.CurrentDirectory.Split('\\');
            currentDirParts = currentDirParts.Take(currentDirParts.Length - 3).ToArray();

            string path = Path.GetDirectoryName(string.Join("/", currentDirParts) + "/HTM.Net.Research.Tests/Resources/DataSets/OCR/characters/");

            var imageList = xmlDocument.Root.Nodes()
                .Select(n => new { image = ((XElement)n).Attribute("file").Value, tag = ((XElement)n).Attribute("tag").Value })
                .ToList();

            List<Bitmap> images = new List<Bitmap>();
            List<string> tags = new List<string>();
            foreach (var o in imageList)
            {
                var image = (Bitmap)Image.FromFile(Path.Combine(path, o.image));

                images.Add(image);
                tags.Add(o.tag);
            }
            return new Util.Tuple(images, tags);
        }
    }
}
