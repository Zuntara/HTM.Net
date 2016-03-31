using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;

namespace HTM.Net.Research.Vision
{
    public static class BitmapExtentions
    {
        /// <summary>
        /// Returns a bit vector representation (list of ints) of a PIL image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static int[] ToVector(this Bitmap image)
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

            // TODO: revise ?
            List<int> grayScaleClipped = new List<int>();
            for (int i = 0, j = 0; i < bytes.Length; i += 4, j++)
            {
                int value = (bytes[i] + bytes[i + 1] + bytes[i + 2] + bytes[i + 3]) / 4;
                grayScaleClipped.Add(value < 100 ? 1 : 0);
            }

            return grayScaleClipped.ToArray();
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