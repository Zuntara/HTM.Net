using HTM.Net.Research.Vision.Image;
using Kaliko.ImageLibrary;

namespace HTM.Net.Research.Vision
{
    public static class BitmapExtentions
    {
        /// <summary>
        /// Returns a bit vector representation (list of ints) of a PIL image.
        /// </summary>
        /// <param name="image"></param>
        /// <returns></returns>
        public static int[] ToVector(this KalikoImage image)
        {
            // Convert image to grayscale
            image = image.Clone();
            image.ApplyFilter(new GrayScaleFilter());
            image.ApplyFilter(new BlackAndWhiteFilter(100, true));

            return image.IntArray;
        }
    }
}