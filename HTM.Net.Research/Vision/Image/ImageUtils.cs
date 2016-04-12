using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using Kaliko.ImageLibrary;
using Kaliko.ImageLibrary.Filters;
using Kaliko.ImageLibrary.Scaling;

namespace HTM.Net.Research.Vision.Image
{
    public static class ImageUtils
    {
        /// <summary>
        /// Scale to fit within the size, preserving aspect ratio.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="size"></param>
        /// <returns></returns>
        public static KalikoImage ScaleToFitPIL(KalikoImage image, Size size)
        {
            return image.Scale(new FitScaling(size.Width, size.Height));
        }

        /// <summary>
        /// Scale to fit the size, cropping if the aspect ratio is wrong.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static KalikoImage CropToFit(KalikoImage image, int width, int height)
        {
            return image.Scale(new CropScaling(width, height));
        }

        /// <summary>
        /// Crop the image to have the specified aspect ratio.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="width"></param>
        /// <param name="height"></param>
        /// <returns></returns>
        public static KalikoImage CropToAspectRatio(KalikoImage image, double ratio)
        {
            image = image.Clone();
            var width = image.Size.Width;
            var height = image.Size.Height;
            double imageRatio = width / (double)height;
            int x, y = x = 0;
            if (imageRatio > ratio)
            {
                // Original image is too wide
                x = (int)((width - height * ratio) / 2);
            }
            else
            {
                // Original image is too tall
                y = (int)((height - width * 1.0 / ratio) / 2);
            }
            //image.Resize(width, height);
            // Crop if necessary
            if (x > 0 || y > 0)
            {
                image.Crop(x, y, width - x, height - y);
            }
            return image;
        }
        /// <summary>
        /// Threshold the image to make it black and white.
        /// Returned image is mode 'L', but all values are 0 or 255.
        /// PIL's convert() method dithers rather than thresholds.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="threshold"></param>
        /// <returns></returns>
        public static KalikoImage ThresholdBW(KalikoImage image, int threshold = 128)
        {
            image = image.Clone();
            image.ApplyFilter(new BlackAndWhiteFilter(threshold, false));
            return image;
        }

        public static KalikoImage Blur(KalikoImage image, double radius, double? sigma = null, object edgeColor = null)
        {
            image = image.Clone();
            image.ApplyFilter(new GaussianBlurFilter((float)radius));
            return image;
        }

        public static List<KalikoImage> Split(this KalikoImage image)
        {
            List<KalikoImage> channels = new List<KalikoImage>();
            channels.Add(new KalikoImage(image.Width, image.Height));
            channels.Add(new KalikoImage(image.Width, image.Height));
            channels.Add(new KalikoImage(image.Width, image.Height));
            channels.Add(new KalikoImage(image.Width, image.Height));
            // Split in channels
            var bytes = image.ByteArray;
            for (int i = 0, l = bytes.Length; i < l; i += 4)
            {
                byte b = bytes[i];       // Blue channel
                byte g = bytes[i + 1];   // Green channel
                byte r = bytes[i + 2];   // Red channel
                byte a = bytes[i + 3];   // Alpha channel

                channels[0].ByteArray[i] = b;
                channels[1].ByteArray[i] = g;
                channels[2].ByteArray[i] = r;
                channels[3].ByteArray[i] = a;
            }
            return channels;
        }

        public static Rectangle GetBBox(this KalikoImage image)
        {
            throw new NotImplementedException("todo");
        }

        public static Tuple<byte, byte> GetExtrema(this byte[] imageBytes)
        {
            return new Tuple<byte,byte>(imageBytes.Min(), imageBytes.Max());
        }
    }

    public class BlackAndWhiteFilter : IFilter
    {
        private readonly int _threshold;
        private readonly bool _toBinary;

        public BlackAndWhiteFilter(int threshold, bool toBinary)
        {
            _threshold = threshold;
            _toBinary = toBinary;
        }

        public void Run(KalikoImage image)
        {
            // Lets get the byte array to work with
            var bytes = image.ByteArray;

            byte maxValue = (byte)(_toBinary ? 1 : 255);

            // Loop through all pixels. If you depend of knowing the x and y coordinates 
            // of the pixels use loops on image.Width and image.Height instead
            for (int i = 0, l = bytes.Length; i < l; i += 4)
            {
                //int b = bytes[i];       // Blue channel
                //int g = bytes[i + 1];   // Green channel
                //int r = bytes[i + 2];   // Red channel
                //int a = bytes[i + 3];   // Alpha channel

                bytes[i] = (byte)(bytes[i] > _threshold ? maxValue : 0);
                bytes[i + 1] = (byte)(bytes[i + 1] > _threshold ? maxValue : 0);
                bytes[i + 2] = (byte)(bytes[i + 2] > _threshold ? maxValue : 0);
            }
            // Write back the manipulated bytes to the image
            // (This is important, otherwise nothing will be changed!)
            image.ByteArray = bytes;
        }
    }

    public class GrayScaleFilter : IFilter
    {
        public void Run(KalikoImage image)
        {
            // Lets get the byte array to work with
            var bytes = image.ByteArray;
            // Loop through all pixels. If you depend of knowing the x and y coordinates 
            // of the pixels use loops on image.Width and image.Height instead
            for (int i = 0, l = bytes.Length; i < l; i += 4)
            {
                int b = bytes[i];       // Blue channel
                int g = bytes[i + 1];   // Green channel
                int r = bytes[i + 2];   // Red channel
                int a = bytes[i + 3];   // Alpha channel

                int lum = (int)(0.2126 * r + 0.7152 * g + 0.0722 * b);

                a = (a << 24);
                r = (lum << 16);
                g = (lum << 8);
                b = lum;

                bytes[i] = (byte)b;
                bytes[i + 1] = (byte)g;
                bytes[i + 2] = (byte)r;
                bytes[i + 3] = (byte)a;
            }
            // Write back the manipulated bytes to the image
            // (This is important, otherwise nothing will be changed!)
            image.ByteArray = bytes;
        }
    }
}