using System;
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
}