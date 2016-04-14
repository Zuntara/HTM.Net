using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using HTM.Net.Research.Vision.Image;
using HTM.Net.Util;
using Kaliko.ImageLibrary;

namespace HTM.Net.Research.Vision.Sensor.Explorers
{
    /// <summary>
    /// BaseExplorer is the base class for all ImageSensor explorers. An explorer is
    /// a plugin to ImageSensor that defines how the sensor moves through the "input
    /// space" of images, filtered images, and positions of the sensor "window" on
    /// the image.
    /// The basic job of the explorer is to take the current sensor position (image
    /// number, filtered version, (x,y) offset) and move to the next position. For
    /// example, the ExhaustiveSweep filter with default parameters shifts the
    /// offset one pixel to the right on each iteration, and then moves the offset
    /// down and back to the left side of the image when it falls off the edge of the
    /// bounding box. When it is done sweeping left-to-right, it sweeps
    /// top-to-bottom, and then moves on to the next image. The RandomSweep explorer
    /// works similarly, but after completely one sweep across the image, it randomly
    /// chooses a new image and a place to start the sweep. The Flash explorer is the
    /// simplest explorer; it just shows each image once and then moves to the next
    /// one.
    /// Explorers do a lot of ImageSensor's work. They maintain the sensor's position
    /// and increment it. They know how to seek to a certain image, iteration, and
    /// filtered version. They decide when to send a reset signal (end of a temporal
    /// sequence). Some of them can report how many iterations are necessary to
    /// explore all the inputs (though some cannot, like RandomSweep).
    /// All other ImageSensor explorers should subclass BaseExplorer and implement
    /// at least next(), and probably __init__(), first(), and seek() as well.
    /// Deterministic explorers that can calculate a total number of iterations
    /// should override the getNumIterations() method.
    /// </summary>
    public abstract class BaseExplorer
    {
        protected Func<int?, KalikoImage> _getOriginalImage;
        protected Func<ExplorerPosition, List<KalikoImage>> _getFilteredImages;
        protected Func<int?, ImageListEntry> _getImageInfo;
        protected int? _seed;
        protected int _holdFor, _initialSetSeed;
        protected IRandom _random;
        protected ExplorerPosition _position;
        protected int _numFilters;
        protected int _numImages;
        protected int _enabledWidth;
        protected int _enabledHeight;
        protected List<int> _numFilterOutputs;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="getOriginalImage">ImageSensor method to get an original image.</param>
        /// <param name="getFilteredImages">ImageSensor method to get filtered images.</param>
        /// <param name="getImageInfo">ImageSensor method to get imageInfo.</param>
        /// <param name="seed">Seed for the random number generator. A specific random number
        /// generator instance is always created for each explorer, so that they do not affect each other.
        /// </param>
        /// <param name="holdFor">how many iterations to hold each output image for. Default is 1.
        /// The sensor will take care of dealing with this - nothing special needs to be done by the explorer.
        /// </param>
        protected virtual void Initialize(Func<int?, KalikoImage> getOriginalImage, Func<ExplorerPosition, List<KalikoImage>> getFilteredImages,
            Func<int?, ImageListEntry> getImageInfo, int? seed = null, int holdFor = 1)
        {
            _getOriginalImage = getOriginalImage;
            _getFilteredImages = getFilteredImages;
            _getImageInfo = getImageInfo;
            _seed = seed;
            _holdFor = holdFor;
            if (seed.HasValue)
            {
                _initialSetSeed = seed.Value;
                _random = new XorshiftRandom(seed.Value);
            }
            else
            {
                _initialSetSeed = Environment.TickCount;
                _random = new XorshiftRandom(_initialSetSeed);
            }
        }

        public static BaseExplorer CreateWithParameters(Type explorerType, Func<int?, KalikoImage> getOriginalImage, Func<ExplorerPosition, List<KalikoImage>> getFilteredImages,
            Func<int?, ImageListEntry> getImageInfo, int? seed = null, int holdFor = 1, Map<string, object> extraAgruments = null)
        {
            BaseExplorer explorer = (BaseExplorer)Activator.CreateInstance(explorerType);

            explorer.Initialize(getOriginalImage, getFilteredImages, getImageInfo, seed, holdFor);

            return explorer;
        }



        /// <summary>
        /// Set up the position.
        /// 
        /// BaseExplorer picks image 0, offset (0,0), etc., but explorers that wish
        /// to set a different first position should extend this method. Such explorers
        /// may wish to call BaseExplorer.first(center=False), which initializes the
        /// position tuple but does not call centerImage() (which could cause
        /// unnecessary filtering to occur).
        /// </summary>
        /// <param name="center"></param>
        public virtual void First(bool center = true)
        {
            _position = new ExplorerPosition
            {
                Image = 0,
                Filters = new List<int>(), //new int[_numFilters],
                Offset = new Point(0, 0),
                Reset = false
            };
            for (int i = 0; i < _numFilters; i++)
            {
                _position.Filters.Add(0);
            }

            if (_numImages > 0 && center)
            {
                CenterImage();
            }
        }

        /// <summary>
        /// Go to the next position (next iteration).
        /// 
        /// seeking -- Boolean that indicates whether the explorer is calling next()
        ///   from seek(). If True, the explorer should avoid unnecessary computation
        ///   that would not affect the seek command. The last call to next() from
        ///   seek() will be with seeking=False.
        /// </summary>
        /// <param name="seeking"></param>
        public virtual void Next(bool seeking = false)
        {
            // Do nothing here yet
        }

        /// <summary>
        /// Seek to the specified position or iteration.
        /// 
        /// ImageSensor checks validity of inputs, checks that one (but not both) of
        /// position and iteration are None, and checks that if position is not None,
        /// at least one of its values is not None.
        /// 
        /// Updates value of position.
        /// </summary>
        /// <param name="iteration">Target iteration number (or None)</param>
        /// <param name="position">Target position (or None)</param>
        public virtual void Seek(int? iteration = null, ExplorerPosition position = null)
        {
            if (!iteration.HasValue && position == null)
            {
                throw new InvalidOperationException("iteration or position must be given.");
            }
            if (iteration.HasValue)
            {
                RestoreRandomState();
                First();
                if (iteration > 0)
                {
                    for (int i = 0; i < iteration.Value - 1; i++)
                    {
                        Next(true);
                    }
                    Next();
                }
            }
            else
            {
                if (position.Image.HasValue) _position.Image = position.Image;
                if (position.Filters != null) _position.Filters = position.Filters;
                if (position.Offset.HasValue) _position.Offset = position.Offset;
                if (position.Reset.HasValue) _position.Reset = position.Reset;
            }
        }
        /// <summary>
        /// Update state with new parameters from ImageSensor and call first().
        /// </summary>
        /// <param name="kwargs"></param>
        public void Update(Map<string, object> kwargs)
        {
            var beanUtil = BeanUtil.GetInstance();
            foreach (var key in kwargs.Keys)
            {
                beanUtil.SetSimpleProperty(this, key, kwargs[key]);
            }
            First();
        }

        /// <summary>
        /// Get the number of iterations required to completely explore the input space.
        /// Explorers that do not wish to support this method should not override it.
        /// ImageSensor takes care of the input validation.
        /// </summary>
        /// <param name="image">If None, returns the sum of the iterations for all the loaded images. 
        /// Otherwise, image should be an integer specifying the image for which to calculate iterations.</param>
        public virtual int GetNumIterations(int? image)
        {
            return -1;
        }

        /// <summary>
        /// Restore the initial random state of the explorer.
        /// </summary>
        public void RestoreRandomState()
        {
            _random = new XorshiftRandom(_initialSetSeed);
        }

        /// <summary>
        /// Return True if the enabled region of the image specified by the current position is blank.
        /// </summary>
        /// <param name="fallOfObject">
        /// If True, the image is considered blank only if the mask is entirely black. 
        /// Otherwise, it is considered blank if any of mask is black.
        /// </param>
        /// <param name="position">Position to use. Uses current position if not specified.</param>
        /// <returns></returns>
        public bool IsBlank(bool fallOfObject, ExplorerPosition position = null)
        {
            if (position == null) position = _position;

            Point xy1 = position.Offset.GetValueOrDefault();
            int x2 = xy1.X + _enabledWidth;
            int y2 = xy1.Y + _enabledHeight;
            KalikoImage mask = _getFilteredImages(position)[0].SplitGrayscale()[1];
            //var extrema = 
            throw new NotImplementedException("Check BaseExporer.py line 195");
        }

        /// <summary>
        /// Return True if the current position and enabled size contains at least
        /// some of the region specified by the bounding box.
        /// </summary>
        /// <param name="position">Position to use. Uses current position if not specified.</param>
        /// <returns></returns>
        public bool IsValid(ExplorerPosition position = null)
        {
            if (position == null) position = _position;
            Point xy1 = position.Offset.GetValueOrDefault();
            Rectangle bbox = new Rectangle(new Point(), _getFilteredImages(position)[0].SplitGrayscale()[1].Size);
            if ((bbox.Location.X - _enabledWidth >= xy1.X) ||
                (bbox.Location.Y - _enabledHeight >= xy1.Y) ||
                (bbox.Size.Width + _enabledWidth <= xy1.X) ||
                (bbox.Size.Height + _enabledHeight <= xy1.Y))
            {
                return false;
            }
            return true;
        }

        /// <summary>
        /// Get all the filtered versions of the image, as a flat list.
        /// Each item in the list is a list of images, containing an image for each
        /// simultaneous response.
        /// </summary>
        /// <param name="image">Image index to use. Uses current position if not specified.</param>
        public List<List<KalikoImage>> GetAllFilteredVersionsOfImage(int? image = null)
        {
            if (!image.HasValue) image = _position.Image.GetValueOrDefault();

            List<List<KalikoImage>> filteredImages = new List<List<KalikoImage>>();
            List<int> filterPosition = new List<int>();
            List<int> emptyPositions = new List<int>();
            for (int i = 0; i < _numFilters; i++)
            {
                filterPosition.Add(0);
                emptyPositions.Add(0);
            }

            var position = new ExplorerPosition
            {
                Image = image,
                Filters = filterPosition
            };
            while (true)
            {
                filteredImages.Add(_getFilteredImages(position));
                foreach (int i in ArrayUtils.XRange(_numFilters - 1, -1, -1))
                {
                    //filterPosition[i] = new Tuple<int?, ExplorerPosition>(filterPosition[i].Item1 + 1, filterPosition[i].Item2);
                    filterPosition[i] += 1;

                    if (filterPosition[i] == _numFilterOutputs[i])
                    {
                        filterPosition[i] = 0;
                    }
                    else
                    {
                        break;
                    }
                }
                if (filterPosition.SequenceEqual(emptyPositions))
                {
                    break;
                }
            }
            return filteredImages;
        }

        /// <summary>
        /// Pick a random image from a uniform distribution.
        /// </summary>
        /// <param name="random">Instance of random.Random.</param>
        /// <returns></returns>
        public int PickRandomImage(IRandom random)
        {
            return random.NextInt(_numImages - 1);
        }

        /// <summary>
        /// Pick a random position for each filter from uniform distributions.
        /// </summary>
        /// <param name="random">Instance of random.Random.</param>
        /// <returns></returns>
        public int[] PickRandomFilters(IRandom random)
        {
            return ArrayUtils.XRange(0, _numFilters, 1)
                .Select(i => random.NextInt(_numFilterOutputs[i] - 1))
                .ToArray();
        }
        /// <summary>
        /// Update the offset to center the current image.
        /// </summary>
        public void CenterImage()
        {
            var image = _getFilteredImages(null)[0];
            _position.Offset = new Point((image.Width - _enabledWidth) / 2, (image.Height - _enabledHeight) / 2);
        }

        /// <summary>
        /// Get the number of filtered versions for each original image.
        /// </summary>
        /// <returns></returns>
        private int GetNumFilteredVersionsPerImage()
        {
            int numFilteredVersions = 1;
            foreach (int i in ArrayUtils.XRange(0, _numFilters, 1))
            {
                numFilteredVersions *= _numFilterOutputs[i];
            }
            return numFilteredVersions;
        }

        /// <summary>
        /// Get the number of filtered versions for each original image.
        /// </summary>
        public int NumFilteredVersionsPerImage
        {
            get { return GetNumFilteredVersionsPerImage(); }
        }

        public int GetHoldFor()
        {
            return _holdFor;
        }

        public ExplorerPosition GetPosition()
        {
            return _position;
        }
    }
}