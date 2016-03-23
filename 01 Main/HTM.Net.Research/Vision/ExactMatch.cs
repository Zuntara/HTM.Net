using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    /// <summary>
    /// This classifier builds a list of SDRs and their associated categories.
    /// When queried for the category of an SDR it returns the first category in the list that has a matching SDR.
    /// </summary>
    public class ExactMatch
    {
        /// <summary>
        /// This classifier has just two things to keep track off:
        /// - A list of the known categories 
        /// - A list of the SDRs associated with each category
        /// </summary>
        public ExactMatch()
        {
            SDRs = new List<int[]>();
            Categories = new Map<int, List<string>>();
        }

        public void Learn(int[] inputPattern, string inputCategory, bool isSparse = false)
        {
            if (!SDRs.Any(ip => ip.SequenceEqual(inputPattern)))
            {
                SDRs.Add(inputPattern);
                Categories.Add(Categories.Count, new List<string> { inputCategory });
            }
            else
            {
                Categories[SDRs.FindIndex(ip => ip.SequenceEqual(inputPattern))].Add(inputCategory);
            }
        }

        public Tuple Infer(int[] inputPattern)
        {
            if (!SDRs.Any(ip => ip.SequenceEqual(inputPattern)))
            {
                string winner = Categories[SDRs.FindIndex(ip => ip.SequenceEqual(inputPattern))].First();
                // format return values to match KNNClassifier
                return new Tuple(winner);
            }
            return null;
        }

        private List<int[]> SDRs { get; set; }
        private Map<int, List<string>> Categories { get; set; }
    }

    public static class BitmapExtentions
    {
        /// <summary>
        /// Returns a bit vector representation (list of ints) of a PIL image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static byte[] ToVector(this Bitmap image)
        {
            // Convert image to grayscale
            image.ConvertToGrayscale();

            var bitmapData = image.LockBits(new Rectangle(0, 0, image.Width, image.Height), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
            var length = bitmapData.Stride * bitmapData.Height;

            byte[] bytes = new byte[length];

            // Pull out the data, turn that into a list, then a numpy array,
            // then convert from 0 255 space to binary with a threshold.
            // Finally cast the values into a type CPP likes
            // vector = (numpy.array(list(image.getdata())) < 100).astype('uint32')

            // Copy bitmap to byte[]
            Marshal.Copy(bitmapData.Scan0, bytes, 0, length);
            image.UnlockBits(bitmapData);
            return bytes;
        }

        public static void ConvertToGrayscale(this Bitmap image)
        {
            for (int i = 0; i < image.Width; i++)
            {
                for (int j = 0; j < image.Height; j++)
                {
                    Color color = image.GetPixel(i, j);

                    int alpha = color.A;
                    int red = color.R;
                    int green = color.G;
                    int blue = color.B;

                    int lum = (int)(0.2126 * red + 0.7152 * green + 0.0722 * blue);

                    alpha = (alpha << 24);
                    red = (lum << 16);
                    green = (lum << 8);
                    blue = lum;

                    color = Color.FromArgb(alpha + red + green + blue);

                    image.SetPixel(i, j, color);
                }
            }
        }
    }
}