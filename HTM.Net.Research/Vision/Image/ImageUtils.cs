using System.Drawing;
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
            image.ApplyFilter(new BlackAndWhiteFilter(threshold));
            return image;
        }

        public static KalikoImage Blur(KalikoImage image, double radius, double? sigma=null, object edgeColor = null)
        {
            image = image.Clone();
            image.ApplyFilter(new GaussianBlurFilter((float) radius));
            return image;
        }
    }

    public class BlackAndWhiteFilter : IFilter
    {
        private readonly int _threshold;

        public BlackAndWhiteFilter(int threshold)
        {
            _threshold = threshold;
        }

        public void Run(KalikoImage image)
        {
            // Lets get the byte array to work with
            var bytes = image.ByteArray;
            // Loop through all pixels. If you depend of knowing the x and y coordinates 
            // of the pixels use loops on image.Width and image.Height instead
            for (int i = 0, l = bytes.Length; i < l; i += 4)
            {
                //int b = bytes[i];       // Blue channel
                //int g = bytes[i + 1];   // Green channel
                //int r = bytes[i + 2];   // Red channel
                //int a = bytes[i + 3];   // Alpha channel

                bytes[i] = (byte) (bytes[i] > _threshold ? 255 : 0);
                bytes[i+1] = (byte) (bytes[i+1] > _threshold ? 255 : 0);
                bytes[i+2] = (byte) (bytes[i+2] > _threshold ? 255 : 0);
            }
            // Write back the manipulated bytes to the image
            // (This is important, otherwise nothing will be changed!)
            image.ByteArray = bytes;
        }
    }
}