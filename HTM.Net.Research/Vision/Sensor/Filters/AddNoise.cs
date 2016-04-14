using System;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Vision.Image;
using HTM.Net.Util;
using Kaliko.ImageLibrary;

namespace HTM.Net.Research.Vision.Sensor.Filters
{
    /// <summary>
    /// Add noise to the image.
    /// </summary>
    public class AddNoise : BaseFilter
    {
        private double noiseLevel;
        private bool doForeground;
        private bool doBackground;
        private bool dynamic;
        private double noiseThickness;

        public AddNoise()
        {
            // arguments are given through a dictionary in the config
            noiseLevel = 0.0;
            doForeground = true;
            doBackground = false;
            dynamic = true;
            noiseThickness = 1;

            var saveState = random;
            _randomState = new XorshiftRandom(0);
            random = saveState;
        }

        /// <summary>
        /// Returns a single image, or a list containing one or more images.
        /// </summary>
        /// <param name="image">The image to process.</param>
        /// <returns></returns>
        public override object Process(KalikoImage image)
        {
            // Get our random state back
            var saveState = random;
            random = _randomState;

            // Send through parent class first
            base.Process(image);

            image = image.Clone(); // take a copy first
            image.ApplyFilter(this);

            // If generating dynamic noise, change our random state each time.
            if (dynamic)
            {
                _randomState = random;
            }

            // Restore random state
            random = saveState;

            return image;
        }

        /// <summary>
        /// Method that will do the effective filtering
        /// </summary>
        /// <param name="image"></param>
        public override void Run(KalikoImage image)
        {
            byte[] alpha = image.SplitBytesGrayscale()[1];

            int[] pixels;
            // ---------------------------------------------
            // Blank and white
            if (mode == "bw")
            {
                // For black and white images, our doBackground pixels are 255 and our figure pixels are 0
                Debug.Assert(noiseThickness != 0.0, "ImageSensor parameter noiseThickness cannot be 0");
                pixels = image.ByteArray.Select(b => (int)b).ToArray();
                var imageWidth = image.Size.Width;
                var imageHeight = image.Size.Height;
                var pixels2d = ArrayUtils.Reshape(pixels, imageWidth * 4);

            }
            else if (mode == "gray")
            {
                pixels = image.SplitBytesGrayscale()[0].Select(b => (int) b).ToArray(); // Grayscale
                double[] noise = random.GetMatrix(1, pixels.Length)[0];
                // Add +/- self.noiseLevel to each pixel
                // noise = (noise - 0.5) * 2 * self.noiseLevel * 255
                noise = ArrayUtils.Multiply(
                    ArrayUtils.Multiply(ArrayUtils.Multiply(ArrayUtils.Sub(noise, 0.5), 2.0), noiseLevel), 255);
                int[] mask = alpha.Select(a => a != background ? 1 : 0).ToArray();
                if (doForeground && doBackground)
                {
                    pixels = ArrayUtils.RoundToInt(ArrayUtils.Clip(ArrayUtils.Add(pixels, noise), 0, 255)).ToArray();
                }
                else if (doForeground)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (mask[i] != 0)
                        {
                            pixels[i] += (int) Math.Round(noise[i]);
                        }
                    }
                }
                else if (doBackground)
                {
                    for (int i = 0; i < pixels.Length; i++)
                    {
                        if (mask[i] == 0)
                        {
                            pixels[i] += (int) Math.Round(noise[i]);
                        }
                    }
                }
                pixels = ArrayUtils.Clip(pixels, 0, 255);
            }
            else
            {
                throw new InvalidOperationException("This image mode is not supported : " + mode);
            }
            
            // Write out the new pixels
            byte[] convertedPixels = ArrayUtils.MergeGrouped(pixels.Select(p=>(byte)p).ToArray(), alpha, 3, 4);

            image.ByteArray = convertedPixels;
        }
    }
}