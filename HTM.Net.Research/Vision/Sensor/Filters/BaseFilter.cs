using System;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Research.Vision.Image;
using HTM.Net.Research.Vision.Sensor.Explorers;
using HTM.Net.Util;
using Kaliko.ImageLibrary;
using Kaliko.ImageLibrary.Filters;

namespace HTM.Net.Research.Vision.Sensor.Filters
{
    public abstract class BaseFilter : IFilter
    {
        protected IRandom random, _randomState;
        protected bool reproducable;
        protected int background;
        protected string mode;

        /// <summary>
        /// Creates a new instance of a BaseFilter
        /// </summary>
        /// <param name="seed">Seed for the random number generator. A specific random number generator instance 
        /// is always created for each filter, so that they do not affect each other.</param>
        /// <param name="reproducable">Seed the random number generator with a hash of the image pixels on each call to process(), 
        /// in order to ensure that the filter always generates the same output for a particular input image.</param>
        protected BaseFilter(int? seed = null, bool reproducable = false)
        {
            if (seed.HasValue && reproducable)
                throw new InvalidOperationException("Cannot use 'seed' and 'reproducible' together");
            if (seed.HasValue)
                random = new XorshiftRandom(seed.GetValueOrDefault());

            this.reproducable = reproducable;
            mode = "gray";
            background = 0;
        }
        /// <summary>
        /// Returns a single image, or a list containing one or more images.
        /// Post filtersIt can also return an additional raw output numpy array
        /// that will be used as the output of the ImageSensor
        /// </summary>
        /// <param name="image">The image to process.</param>
        /// <returns></returns>
        public virtual object Process(KalikoImage image)
        {
            if (reproducable)
            {
                // Seed the random instance with a hash of the image pixels
                random = new XorshiftRandom(image.GetHashCode());
            }
            return null;
        }
        /// <summary>
        /// Return the number of images returned by each call to process().
        /// 
        /// If the filter creates multiple simultaneous outputs, return a tuple:
        /// (outputCount, simultaneousOutputCount).
        /// </summary>
        public virtual object GetOutputCount()
        {
            return 1;
        }
        /// <summary>
        /// Accept new parameters from ImageSensor and update state.
        /// </summary>
        /// <param name="mode"></param>
        /// <param name="background"></param>
        public virtual void Update(string mode = null, int? background = null)
        {
            if (mode != null)
            {
                this.mode = mode;
            }
            if (background.HasValue)
            {
                this.background = background.Value;
            }
        }

        public static BaseFilter CreateWithParameters(Type filterType, Map<string, object> filterArgs)
        {
            BaseFilter filter = (BaseFilter)Activator.CreateInstance(filterType);

            if (filterArgs != null)
            {
                Initialize(filter, filterArgs);
            }

            return filter;
        }

        private static void Initialize(BaseFilter filter, Map<string, object> filterArgs)
        {
            var beanUtil = BeanUtil.GetInstance();
            foreach (var key in filterArgs.Keys)
            {
                beanUtil.SetSimpleProperty(filter, key, filterArgs[key]);
            }
        }

        public abstract void Run(KalikoImage image);
    }

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
            var alpha = image.Split()[3];

            // ---------------------------------------------
            // Blank and white
            if (mode == "bw")
            {
                // For black and white images, our doBackground pixels are 255 and our figure pixels are 0
                Debug.Assert(noiseThickness != 0.0, "ImageSensor parameter noiseThickness cannot be 0");
                var pixels = image.ByteArray.Select(b => (int)b).ToArray();
                var imageWidth = image.Size.Width;
                var imageHeight = image.Size.Height;
                var pixels2d = ArrayUtils.Reshape(pixels, imageWidth * 4);

            }
            else if (mode == "gray")
            {
                var pixels = image.ByteArray.Select(b => (int)b).ToArray();
                var noise = random.GetMatrix(1, pixels.Length)[0];

            }
            throw new NotImplementedException("todo");
        }
    }
}