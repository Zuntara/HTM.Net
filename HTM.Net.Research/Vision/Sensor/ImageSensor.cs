using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Vision.Image;
using HTM.Net.Util;
using Kaliko.ImageLibrary;
using Kaliko.ImageLibrary.Filters;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Vision.Sensor
{
    /// <summary>
    /// ImageSensor, an extensible sensor for images.
    /// </summary>
    public class ImageSensor : Sensor<Bitmap>
    {
        protected const int HEADER_SIZE = 3;
        protected const int BATCH_SIZE = 20;
        protected const bool DEFAULT_PARALLEL_MODE = false;

        protected RawImageStream _imageStream;
        protected readonly SensorParams _params;
        private List<ImageListEntry> _imageList;
        private Map<string, KalikoImage> categoryInfo;
        private ExplorerEntry explorer;
        private List<int> _imageQueue;
        private int _pixelCount;
        private List<Tuple> _filterQueue;
        private ExplorerPosition _prevPosition;
        private bool blankWithReset;
        private int depth;
        private int enabledWidth, width;
        private int enabledHeight, height;
        private List<FilterEntry> filters;
        private Color background;
        private List<FilterEntry> postFilters;
        private bool invertOutput;
        private bool logFilteredImages;
        private int _iteration;
        private int _holdForOffset;

        /// <summary>
        /// Creates a new Image sensor with sensor parameters
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Sensor<Bitmap> Create(SensorParams p)
        {
            Sensor<Bitmap> fs = new ImageSensor(p);
            return fs;
        }

        /// <summary>
        /// Creates a new instance of this <see cref="ImageSensor"/>
        /// </summary>
        /// <param name="params"></param>
        protected ImageSensor(SensorParams @params)
        {
            _params = @params;
            if (!_params.HasKey("DIMENSIONS"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"DIMENSIONS\"");
            }

            dynamic dims = _params["DIMENSIONS"];
            width = dims.width;
            height = dims.height;

            //IObservable<ImageDefinition> obs = null;
            //obs = (IObservable<ImageDefinition>)@params.Get("ONSUB");

            //this._imageStream = RawImageStream.Batch(
            //    new Stream<ImageDefinition>(obs), BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);
        }

        /// <summary>
        /// Returns an instance of <see cref="SensorParams"/> used 
        /// to initialize the different types of Sensors with
        /// their resource location or source object.
        /// </summary>
        public override SensorParams GetParams()
        {
            return _params;
        }

        /// <summary>
        /// Used for making the HTM Sensor output stream, 
        /// gets mapped in <see cref="HTMSensor{T}.GetOutputStream()"/>
        /// </summary>
        /// <returns></returns>
        public override IMetaStream GetInputStream()
        {
            return _imageStream;
        }

        public override IValueList GetMetaInfo()
        {
            return _imageStream.GetMeta();
        }

        public override void InitEncoder(Parameters p)
        {
            throw new System.NotImplementedException();
        }

        public override MultiEncoder GetEncoder()
        {
            throw new System.NotImplementedException();
        }

        public override bool EndOfStream()
        {
            throw new System.NotImplementedException();
        }

        #region Specifics
        /// <summary>
        /// Add the specified image to the list of images.
        /// </summary>
        /// <param name="imagePath">Path to the image to load.</param>
        /// <param name="maskPath">Path to the mask to load with this image.</param>
        /// <param name="categoryName">Name of the category of this image.</param>
        /// <param name="clearImageList">If True, all loaded images are removed before this image is loaded. If False, this image is appended to the list of images.</param>
        /// <param name="skipExplorerUpdate"></param>
        /// <param name="auxPath">Path to the auxiliary data for the image.</param>
        /// <param name="userAuxData"></param>
        /// <param name="sequenceIndex">Unique sequence index.</param>
        /// <param name="frameIndex">The frame number within the sequence.</param>
        public Tuple LoadSingleImage(string imagePath, string maskPath = null, string categoryName = null, bool clearImageList = true,
            bool skipExplorerUpdate = false, string[] auxPath = null, object userAuxData = null, int? sequenceIndex = null,
            int? frameIndex = null)
        {
            if (clearImageList)
            {
                ClearImageList(skipExplorerUpdate: true);
            }

            bool manualAux = userAuxData != null;

            AddImage(imagePath: imagePath, maskPath: maskPath,
                   categoryName: categoryName, auxPath: auxPath,
                   manualAux: manualAux, userAuxData: userAuxData,
                   sequenceIndex: sequenceIndex, frameIndex: frameIndex);

            if (!skipExplorerUpdate)
            {
                explorer.Explorer.Update(new Map<string, object> { { "numImages", _imageList.Count } });
            }

            LogCommand(new NamedTuple(new[] { "index" }, _imageList.Count - 1));

            if (clearImageList)
            {
                explorer.Explorer.First();
            }

            return new Tuple(_imageList.Count, _imageList.Count(i => i.HasMask));
        }

        /// <summary>
        /// Generate the next sensor output and send it out.
        /// This method is called by the runtime engine.
        /// </summary>
        public void Compute()
        {
            if (_imageList.Count == 0)
            {
                throw new InvalidOperationException("ImageSensor can't run compute: no images loaded");
            }

            // Check to see if new image belongs to a new sequence, if so force Reset
            var prevPosition = _prevPosition;
            int? prevSequenceID;
            if (prevPosition != null)
            {
                prevSequenceID = _imageList[prevPosition.Image.GetValueOrDefault()].SequenceIndex;
            }
            else
            {
                prevSequenceID = null;
            }

            //UpdatePrevPosition(); // TODO 

            var newPosition = _prevPosition;
            int? newSequenceID;
            if (newPosition != null)
            {
                newSequenceID = _imageList[newPosition.Image.GetValueOrDefault()].SequenceIndex;
            }
            else
            {
                newSequenceID = null;
            }
            if (newSequenceID != prevSequenceID)
            {
                _prevPosition.Reset = true;
            }

            int holdFor = explorer.Explorer.GetHoldFor();
            _holdForOffset += 1;
            if (_holdForOffset >= holdFor)
            {
                _holdForOffset = 0;
                explorer.Explorer.Next();
            }
            _iteration += 1;

            Tuple outputImgsTuple = GetOutputImages();
            var outputImages = outputImgsTuple.Get(0);
            var finalOutput = outputImgsTuple.Get(1);

            // Compile information about this iteration and log it
            //var imageInfo = GetImageInfo();

            // TODO: finish implementation and write tests
        }

        /// <summary>
        /// Get the current image(s) to send out, based on the current position.
        /// 
        /// A post filter may want to provide the finall output of the node. In
        /// this case it will return a non-null final output that the ImageSensor will
        /// use as the output of the node regardless of the output images.
        /// </summary>
        private Tuple GetOutputImages()
        {
            if (_prevPosition.Reset.GetValueOrDefault() && blankWithReset)
            {
                var images = new KalikoImage[depth];
                for (int i = 0; i < images.Length; i++)
                {
                    images[i] = new KalikoImage(enabledWidth, enabledHeight);
                }
                // blank
                return new Tuple(images, null);
            }
            else
            {
                // get the image(s) to send out
                var allImages = GetFilteredImages();

                // Calculate a scale factor in each dimension for adjusting the offset
                double[] scaleX = allImages.Select(image => image.Width / (double)allImages[0].Width).ToArray();
                double[] scaleY = allImages.Select(image => image.Height / (double)allImages[0].Height).ToArray();
                Point offset = explorer.Explorer.GetPosition().Offset.GetValueOrDefault();

                // Normally, the enabledSize is smaller than the sensor size. But, there
                // are some configurations where the user might want to explore in a
                // larger size, then run it through a post-filter to get the end sensor
                // size (for example, when using a fish-eye post filter). If we detect
                // that the enabledSize is greater than the sensor size, then change our
                // crop bounds
                int dstImgWidth = Math.Max(width, enabledWidth);
                int dstImgHeight = Math.Max(width, enabledHeight);

                // Cut out the relevant part of each image
                List<KalikoImage> newImages = new List<KalikoImage>();

                for (int i = 0; i < allImages.Count; i++)
                {
                    KalikoImage image = allImages[i];

                    int x = (int)(offset.X * scaleX[i]);
                    int y = (int)(offset.Y * scaleY[i]);

                    var croppedImage = image.Clone();
                    croppedImage.BackgroundColor = background;
                    croppedImage.Crop(Math.Max(0, x), Math.Max(0, y),
                        Math.Min(x + dstImgWidth, image.Width), Math.Min(x + dstImgHeight, image.Height));
                    // TODO : review , the original code adds alpha mask and pastes image into new one with max(0,-x) and max(0,-y)
                    newImages.Add(croppedImage);
                }

                // Crop the shifted images back to the enabled size
                // TODO: don't think i need this now
                /*
                croppedImages = [image.crop((0, 0,
                                       int(round(self.enabledWidth * scaleX[i])),
                                       int(round(self.enabledHeight * scaleY[i]))))
                           for i, image in enumerate(newImages)]    
                */
                var croppedImages = newImages;

                // Filter through the post filters
                KalikoImage finalOutput = null;
                if (postFilters != null)
                {
                    List<KalikoImage> newCroppedImages = new List<KalikoImage>();
                    for (int i = 0; i < croppedImages.Count; i++)
                    {
                        Tuple respOutput = ApplyPostFilters(croppedImages[i]);
                        var responses = (IList)respOutput.Get(0);
                        var rawOutput = respOutput.Get(1);
                        if (rawOutput != null)
                        {
                            Debug.Assert(finalOutput == null);
                            finalOutput = (KalikoImage)rawOutput;
                        }
                        while (responses[0] is IList)
                        {
                            responses = (IList)responses[0];
                        }
                        newCroppedImages.AddRange(responses.Cast<KalikoImage>());
                    }
                    croppedImages = newCroppedImages;
                }

                // Check that the number of images matches the depth
                if (croppedImages.Count != depth)
                {
                    throw new InvalidOperationException(string.Format("The filters and postFilters created {0} images to send out simultaneously, which does not match ImageSensor's depth parameter, set to {1}.",
                        croppedImages.Count, depth));
                }

                // Invert output if necessary
                if (invertOutput)
                {
                    foreach (KalikoImage image in croppedImages)
                    {
                        image.ApplyFilter(new InvertFilter());
                    }
                }

                return new Tuple(croppedImages, finalOutput);
            }
        }

        /// <summary>
        /// Recursively apply the postFilters to the image and return a list of images.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="filterIndex"></param>
        /// <returns></returns>
        private Tuple ApplyPostFilters(KalikoImage image, int filterIndex = 0)
        {
            // Filter the image
            object rawOutput = null;
            var filtered = postFilters[filterIndex].Filter.Process(image);

            // Handle special case where the post filter wants to control the output
            // of the image sensor (e.g convolution post filters)
            if (filtered is Tuple)
            {
                Debug.Assert(((Tuple)filtered).Count == 2);
                rawOutput = ((Tuple)filtered).Get(1);
                Debug.Assert(rawOutput is IList);
                filtered = ((IList)((Tuple)filtered).Get(1))[0];
            }

            // Flatten all responses into a single list
            if (!(filtered is IList))
            {
                // One response
                filtered = new List<object> { filtered };
            }
            else
            {
                if (((IList)filtered)[0] is IList)
                {
                    // Simultaneous responses
                    List<object> filtered2 = new List<object>();
                    foreach (IList<object> fResponses in (IList)filtered)
                    {
                        filtered2.AddRange(fResponses);
                    }
                    filtered = filtered2;
                }
            }
            // Verify that the filter produced the correct number of outputs
            object outputCount = postFilters[filterIndex].Filter.GetOutputCount();
            if (outputCount is Tuple || outputCount is IList)
            {
                if ((outputCount as Tuple)?.Count == 1 || (outputCount as IList)?.Count == 1)
                {
                    outputCount = (outputCount as Tuple)?.Get(0) ?? (outputCount as IList)?[0];
                }
                else
                {
                    outputCount = (int)((outputCount as Tuple)?.Get(0) ?? (outputCount as IList)?[0]) *
                                  (int)((outputCount as Tuple)?.Get(1) ?? (outputCount as IList)?[1]);
                }
            }
            if (((IList)filtered).Count != (int)outputCount)
            {
                throw new InvalidOperationException(string.Format("{0} postFilter did not return the correct number of outputs",
                    postFilters[filterIndex].FilterName));
            }

            if (logFilteredImages)
            {
                string filterSavePath = filterIndex + postFilters[filterIndex].FilterName;
                if (!Directory.Exists(filterSavePath))
                {
                    Directory.CreateDirectory(filterSavePath);
                }
                if (((IList)filtered).Count > 1)
                {
                    int i = 0;
                    foreach (KalikoImage fImage in (IList)filtered)
                    {
                        string name = Path.Combine(filterSavePath, string.Format("{0}_{1}.png", _iteration, i));
                        i++;
                        fImage.SavePng(name);
                    }
                }
                else
                {
                    string name = Path.Combine(filterSavePath, string.Format("{0}.png", _iteration));
                    ((KalikoImage)((IList)filtered)[0]).SavePng(name);
                }
            }

            if (filterIndex == postFilters.Count - 1)
            {
                return new Tuple(filtered, rawOutput);
            }

            // Concatenate all responses into one flat list of simultaneous responses
            List<object> responses = new List<object>();
            foreach (KalikoImage kImage in (IList)filtered)
            {
                var response = ApplyPostFilters(kImage, filterIndex + 1);
                if (rawOutput != null)
                {
                    Debug.Assert(response.Get(1) == null);
                    // Only one post-filter can determine rawOutput
                }
                responses.Add(response.Get(0));
            }
            return new Tuple(responses, rawOutput);
        }

        /// <summary>
        /// Get the filtered images specified by the position.
        /// </summary>
        /// <returns>Position to use. Uses current position if not specified.</returns>
        private List<KalikoImage> GetFilteredImages(ExplorerPosition position = null)
        {
            if (position == null) position = explorer.Explorer.GetPosition();

            if (_imageList[position.Image.GetValueOrDefault()].Image == null)
            {
                // Image needs to be loaded
                LoadImage(position.Image.GetValueOrDefault());
            }
            if (filters == null)
            {
                // No filters - return original version
                return new List<KalikoImage> { _imageList[position.Image.GetValueOrDefault()].Image };
            }

            // Iterate through the specified list of filter positions
            // Run filters as necessary
            Dictionary<Tuple, List<KalikoImage>> allFilteredImages = _imageList[position.Image.GetValueOrDefault()].Filtered;
            Tuple filterPosition = new Tuple();
            foreach (var filterPair in position.Filters)
            {
                int? filterIndex = filterPair.Item1;
                filterPosition += new Tuple(filterPair.Item2);

                if (!allFilteredImages.ContainsKey(filterPosition))
                {
                    KalikoImage imageToFilter;
                    // Run the filter
                    if (filterPosition.Count > 1)
                    {
                        // Use the first of the simultaneous responses
                        imageToFilter = allFilteredImages[filterPosition][0] as KalikoImage;
                    }
                    else
                    {
                        imageToFilter = _imageList[position.Image.GetValueOrDefault()].Image;
                        // Inject the original image path to the Image"s info
                        // dict in case the filter wants to use it.
                        // TODO ?
                    }
                    var newFilteredImages = ApplyFilter(imageToFilter, position.Image, filterIndex.GetValueOrDefault());
                    int j = 0;
                    foreach (List<KalikoImage> newFilteredImage in newFilteredImages)
                    {
                        List<KalikoImage> image = (List<KalikoImage>)newFilteredImage;
                        // Store in the dictionary of filtered images
                        var thisFilterPosition = filterPosition + new Tuple(j);
                        allFilteredImages[thisFilterPosition] = image;
                        // Update the filter queue
                        var thisFilterTuple = new Tuple(position.Image, thisFilterPosition);
                        if (_filterQueue.Contains(thisFilterTuple))
                        {
                            _filterQueue.Remove(thisFilterTuple);
                        }
                        _filterQueue.Insert(0, thisFilterTuple);

                    }
                }
            }
            // Update the queues to mark this image as recently accessed
            // Only mark the original image if it could be loaded from disk again
            if (position.Image.HasValue && !string.IsNullOrWhiteSpace(_imageList[position.Image.Value].ImagePath))
            {
                if (_imageQueue.Contains(position.Image.Value))
                {
                    _imageQueue.Remove(position.Image.Value);
                }
                _imageQueue.Insert(0, position.Image.Value);
            }
            // Mark all precursors to the current filter
            for (int i = 1; i < position.Filters.Count + 1; i++)
            {
                var partialFilterTuple = new Tuple(position.Image, new Tuple(position.Filters.Take(i)));
                if (_filterQueue.Contains(partialFilterTuple))
                {
                    _filterQueue.Remove(partialFilterTuple);
                }
                _filterQueue.Insert(0, partialFilterTuple);
            }

            //MeetMemoryLimit();     // TODO: implement

            return allFilteredImages[filterPosition];
        }

        /// <summary>
        /// Apply the specified filter to the image.
        /// </summary>
        /// <param name="image"></param>
        /// <param name="imageIndex"></param>
        /// <param name="filterIndex"></param>
        /// <returns></returns>
        private IEnumerable<List<KalikoImage>> ApplyFilter(KalikoImage image, int? imageIndex, int filterIndex)
        {
            var filteredProcessed = filters[filterIndex].Filter.Process(image);

            List<List<KalikoImage>> filtered = new List<List<KalikoImage>>();
            if (!(filteredProcessed is IList))
            {
                filtered.Add(new List<KalikoImage> { filteredProcessed as KalikoImage });
            }
            else
            {
                foreach (var item in (IList)filteredProcessed)
                {
                    if (!(item is IList))
                    {
                        filtered.Add(new List<KalikoImage> { item as KalikoImage });
                    }
                    else
                    {
                        List<KalikoImage> imgList = new List<KalikoImage>();
                        foreach (var lItem in (IList)item)
                        {
                            imgList.Add((KalikoImage)lItem);
                        }
                        filtered.Add(imgList);
                    }
                }
            }

            //  Verify that the filter produced the correct number of outputs
            object oOutputCount = filters[filterIndex].Filter.GetOutputCount();
            Tuple outputCount;
            if (!(oOutputCount is Tuple))
            {
                outputCount = new Tuple(oOutputCount, 1);
            }
            else
            {
                outputCount = (Tuple)oOutputCount;
            }
            if ((int)outputCount.Get(0) != filtered.Count || filtered.Any(outputs => outputCount.Count != (int)outputCount.Get(0)))
            {
                throw new InvalidOperationException("The %s filter " + filters[filterIndex].FilterName +
                         "did not return the correct number of outputs. The number of images that it returned does not " +
                         "match the return value of the filter's getOutputCount() method.");
            }

            foreach (var item in filtered)
            {
                foreach (var img in item)
                {
                    // Verify that the image has the correct mode
                    // TODO

                    // Update the pixel count
                    _pixelCount += image.Width * image.Height;
                }
            }

            if (logFilteredImages)
            {
                // Save filter output to disk
                // TODO
            }
            return filtered;
        }

        /// <summary>
        /// Calculate how many samples the explorer will provide.
        /// </summary>
        /// <param name="image">If None, returns the sum of the iterations for all the loaded
        /// images. Otherwise, image should be an integer specifying the image for which to calculate iterations.</param>
        public int GetNumIterations(int? image = null)
        {
            return explorer.Explorer.GetNumIterations(image) * explorer.Explorer.GetHoldFor();
        }

        /// <summary>
        /// Create a dictionary for an image and metadata and add to the imageList.
        /// </summary>
        /// <param name="imagePath"></param>
        /// <param name="maskPath"></param>
        /// <param name="categoryName"></param>
        /// <param name="erode"></param>
        /// <param name="auxPath"></param>
        /// <param name="manualAux"></param>
        /// <param name="userAuxData"></param>
        /// <param name="sequenceIndex"></param>
        /// <param name="frameIndex"></param>
        private void AddImage(KalikoImage image = null, string imagePath = null, string maskPath = null,
            string categoryName = null, bool? erode = null, string[] auxPath = null, bool manualAux = false,
            object userAuxData = null, int? sequenceIndex = null, int? frameIndex = null)
        {
            ImageListEntry item = new ImageListEntry
            {
                Image = null,
                ImagePath = imagePath,
                AuxData = userAuxData,
                AuxPath = auxPath,
                ManualAux = manualAux,
                MaskPath = maskPath,
                Erode = true,
                CategoryName = categoryName,
                CategoryIndex = null,
                PartitionId = null,
                Filtered = new Dictionary<Tuple, List<KalikoImage>>(),
                SequenceIndex = sequenceIndex,
                FrameIndex = frameIndex
            };
            _imageList.Add(item);

            bool setErodeFlag;
            if (erode != null)
            {
                item.Erode = erode.GetValueOrDefault();
                setErodeFlag = false;
            }
            else
            {
                setErodeFlag = true;
            }

            // Lookup category index from name
            if (string.IsNullOrWhiteSpace(item.CategoryName))
            {
                // Unspecified category
                item.CategoryName = string.Empty;
                item.CategoryIndex = -1;
            }
            else
            {
                for (int i = 0; i < categoryInfo.Count; i++)
                {
                    if (categoryInfo.ElementAt(i).Key.Equals(item.CategoryName, StringComparison.InvariantCultureIgnoreCase))
                    {
                        item.CategoryIndex = i;
                        break;
                    }
                }
                if (!item.CategoryIndex.HasValue)
                {
                    // This is the first image of this category (blank categories ignored)
                    item.CategoryIndex = categoryInfo.Count;
                    // Load the image in order to use it for categoryInfo
                    var original = LoadImage(_imageList.Count - 1, returnOriginal: true, setErodeFlag: setErodeFlag);

                    if (image == null)
                    {
                        _imageQueue.Insert(0, _imageList.Count - 1);
                    }
                    // Append this category to categoryInfo
                    categoryInfo.Add(item.CategoryName, original);
                }
                else if (image != null)
                {
                    // Image is already present, just prepare it
                    // Not necessary if it was already loaded for categoryInfo
                    LoadImage(_imageList.Count - 1, setErodeFlag: setErodeFlag);
                }
            }
        }

        /// <summary>
        /// Load an image that exists in the imageList but is not loaded into memory.
        /// </summary>
        /// <param name="index">Index of the image to load.</param>
        /// <param name="returnOriginal">Whether to return an unmodified version of the image for categoryInfo.</param>
        /// <param name="setErodeFlag"></param>
        /// <returns></returns>
        private KalikoImage LoadImage(int index, bool returnOriginal = false, bool setErodeFlag = true)
        {
            var item = _imageList[index];

            if (item.Image == null)
            {
                // Load the image from disk
                item.Image = new KalikoImage(System.Drawing.Image.FromFile(item.ImagePath));
                // Update the pixel count
                _pixelCount = item.Image.Width * item.Image.Height;
            }

            // Extraxt auxiliary data
            if (!item.ManualAux)
            {
                if (item.AuxPath != null)
                {
                    if (item.AuxData == null)
                    {
                        List<double> auxDataList = new List<double>();
                        // Load the auxiliary data from disk
                        for (int i = 0; i < item.AuxPath.Length; i++)
                        {
                            if (item.AuxData == null)
                            {
                                // file should contain binary data of doubles
                                using (var reader = new BinaryReader(File.OpenRead(item.AuxPath[i])))
                                {
                                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                                    {
                                        double read = reader.ReadDouble();
                                        auxDataList.Add(read);
                                    }
                                }
                                item.AuxData = auxDataList;
                            }
                            else
                            {
                                // file should contain binary data of doubles, concatinate
                                using (var reader = new BinaryReader(File.OpenRead(item.AuxPath[i])))
                                {
                                    while (reader.BaseStream.Position < reader.BaseStream.Length)
                                    {
                                        double read = reader.ReadDouble();
                                        auxDataList.Add(read);
                                    }
                                }
                            }
                        }
                    }
                }
            }

            // Extract partition ID if it exists
            item.PartitionId = -1;  // TODO: review

            // Convert to grayscale
            item.Image.ApplyFilter(new GrayScaleFilter());

            KalikoImage original = null;
            if (returnOriginal)
            {
                original = item.Image.Clone();
            }

            Rectangle? bbox = null;

            if (!string.IsNullOrWhiteSpace(item.MaskPath))
            {
                KalikoImage mask = new KalikoImage(System.Drawing.Image.FromFile(item.MaskPath));
                // Load the mask image and add it to the image as the alpha channel
                // If the image already has an alpha channel, it will be overwritten
                //item.Image.ApplyFilter(new AlphaFilter(mask));

                throw new NotImplementedException("Implement masking features from ImageSensor.py");
            }

            if (setErodeFlag)
            {
                // Check if the image has a nonuniform alpha channel
                // If so, set the "erode" option to False, indicating that the alpha
                // channel is meaningful and it does not need to be eroded by GaborNode
                // to avoid "phantom edges"
                // If a bounding box was used to generated the alpha channel, use the box
                // directly to avoid the expense of scanning the pixels
                if (bbox != null)
                {
                    if (bbox.Value.Left != 0 && bbox.Value.Top != 0
                        && bbox.Value.Width != item.Image.Width
                        && bbox.Value.Height != item.Image.Height)
                    {
                        // Nonuniform alpha channel (from bounding box)
                        item.Erode = false;
                    }
                }
                else
                {
                    var extrema = item.Image.Split()[3].ByteArray.GetExtrema(); // alpha channel
                    if (extrema.Item1 != extrema.Item2)
                    {
                        // Nonuniform alpha channel
                        item.Erode = false;
                    }
                }
            }
            if (returnOriginal)
            {
                return original;
            }
            return null;
        }

        private void LogCommand(params NamedTuple[] namedTuples)
        {
            // Ignore for now
        }

        /// <summary>
        /// Clear the list of images.
        /// </summary>
        /// <param name="skipExplorerUpdate"></param>
        private void ClearImageList(bool skipExplorerUpdate)
        {
            _imageList = new List<ImageListEntry>();
            _imageQueue = new List<int>();
            _filterQueue = new List<Tuple>();
            _pixelCount = 0;
            _prevPosition = null;
            if (!skipExplorerUpdate)
            {
                explorer.Explorer.Update(new Map<string, object> { { "numImages", 0 } });
            }
        }

        #endregion
    }

    internal class ImageListEntry
    {
        public KalikoImage Image { get; set; }
        public string ImagePath { get; set; }
        public object AuxData { get; set; }
        public string[] AuxPath { get; set; }
        public bool ManualAux { get; set; }
        public string MaskPath { get; set; }
        public bool Erode { get; set; }
        public string CategoryName { get; set; }
        public int? CategoryIndex { get; set; }
        public int? PartitionId { get; set; }
        public Dictionary<Tuple, List<KalikoImage>> Filtered { get; set; }
        public int? SequenceIndex { get; set; }
        public int? FrameIndex { get; set; }

        public bool HasMask { get { return !string.IsNullOrWhiteSpace(MaskPath); } }
    }


    internal class ExplorerEntry
    {
        public string ExplorerName { get; set; }
        public Map<string, object> ExplorerArgs { get; set; }
        public BaseExplorer Explorer { get; set; }
    }

    internal class FilterEntry
    {
        public string FilterName { get; set; }
        public Map<string, object> FilterArgs { get; set; }
        public BaseFilter Filter { get; set; }
    }

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
        protected readonly Func<KalikoImage> _getOriginalImage;
        protected readonly Func<ExplorerPosition, List<KalikoImage>> _getFilteredImages;
        protected readonly Func<object> _getImageInfo;
        protected readonly int? _seed;
        protected readonly int _holdFor, _initialSetSeed;
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
        protected BaseExplorer(Func<KalikoImage> getOriginalImage, Func<ExplorerPosition, List<KalikoImage>> getFilteredImages,
            Func<object> getImageInfo, int? seed = null, int holdFor = 1)
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
                Filters = new List<Tuple<int?, ExplorerPosition>>(), //new int[_numFilters],
                Offset = new Point(0, 0),
                Reset = false
            };
            for (int i = 0; i < _numFilters; i++)
            {
                _position.Filters.Add(null);
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
            KalikoImage mask = _getFilteredImages(position)[0].Split()[3];
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
            Rectangle bbox = new Rectangle(new Point(), _getFilteredImages(position)[0].Split()[3].Size);
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
            var filterPosition = new List<Tuple<int?, ExplorerPosition>>();
            for (int i = 0; i < _numFilters; i++)
            {
                filterPosition.Add(null);
            }

            var emptyPositions = new List<Tuple<int?, ExplorerPosition>>();
            for (int i = 0; i < _numFilters; i++)
            {
                emptyPositions.Add(null);
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
                    filterPosition[i] = new Tuple<int?, ExplorerPosition>(filterPosition[i].Item1 + 1, filterPosition[i].Item2);

                    if (filterPosition[i].Item1 == _numFilterOutputs[i])
                    {
                        filterPosition[i] = new Tuple<int?, ExplorerPosition>(0, filterPosition[i].Item2);
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

    public abstract class BaseFilter
    {
        protected IRandom random;
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
    }

    public class ExplorerPosition
    {
        public int? Image { get; set; }
        public List<Tuple<int?, ExplorerPosition>> Filters { get; set; }
        public Point? Offset { get; set; }
        public bool? Reset { get; set; }
    }

    public class ObservableImageSensor : Sensor<IObservable<ImageDefinition>>
    {
        private const int HEADER_SIZE = 3;
        private const int BATCH_SIZE = 20;
        private const bool DEFAULT_PARALLEL_MODE = false;

        private RawImageStream stream;
        private SensorParams @params;

        private ObservableImageSensor(SensorParams @params)
        {
            if (!@params.HasKey("ONSUB"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"ONSUB\"");
            }
            if (!@params.HasKey("DIMENSIONS"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"DIMENSIONS\"");
            }
            this.@params = @params;

            IObservable<ImageDefinition> obs = null;
            obs = (IObservable<ImageDefinition>)@params.Get("ONSUB");
            this.stream = RawImageStream.Batch(
                new Stream<ImageDefinition>(obs), BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);

        }

        public static ObservableImageSensor Create(SensorParams p)
        {
            ObservableImageSensor sensor = new ObservableImageSensor(p);

            return sensor as ObservableImageSensor;
        }

        /**
         * Returns the {@link SensorParams} object used to configure this
         * {@code ObservableSensor}
         * 
         * @return the SensorParams
         */
        public override SensorParams GetParams()
        {
            return @params;
        }

        /**
         * Returns the configured {@link MetaStream}.
         * 
         * @return  the MetaStream
         */
        public override IMetaStream GetInputStream()
        {
            return (IMetaStream)stream;
        }

        public override MultiEncoder GetEncoder()
        {
            throw new NotImplementedException();
        }

        public override bool EndOfStream()
        {
            throw new NotImplementedException();
        }

        /**
         * Returns the values specifying meta information about the 
         * underlying stream.
         */
        public override IValueList GetMetaInfo()
        {
            return stream.GetMeta();
        }

        public override void InitEncoder(Parameters p)
        {
            throw new NotImplementedException();
        }
    }

    public class RawImageStream : IMetaStream
    {
        private static readonly ILog LOGGER = LogManager.GetLogger(typeof(RawImageStream));

        private readonly int _headerLength;
        private ImageHeader _header;
        private bool _isArrayType;
        internal IStream<ImageDefinition> _contentStream;

        public RawImageStream()
        {

        }

        public RawImageStream(IStream<ImageDefinition> stream, int headerSize, Func<ImageDefinition, ImageDefinition> mappingFunc)
        {
            _headerLength = headerSize;
            MakeHeader(stream);

            if (mappingFunc != null)
            {
                _contentStream = stream.Map(mappingFunc);
            }
            else
            {
                _contentStream = stream.Map(bitmapDef =>
                {

                    return bitmapDef;
                });
            }
            LOGGER.Debug("Created RawImageStream");
        }

        private void MakeHeader(IStream<ImageDefinition> stream)
        {
            //List<string[]> contents = new List<string[]>();

            //for (int i = 0; i < _headerLength; i++)
            //{
            //    contents.Add(stream.Read().Split(','));
            //}
            //stream.SetOffset(_headerLength);
            //BatchedCsvStream<>.BatchedCsvHeader<string[]> header = new BatchedCsvStream<>.BatchedCsvHeader<string[]>(contents, _headerLength);
            //_header = header;
            //_isArrayType = IsArrayType();

            //if (LOGGER.IsDebugEnabled)
            //{
            //    LOGGER.Debug("Created Header:");
            //    foreach (string[] h in contents)
            //    {
            //        LOGGER.Debug("\t" + Arrays.ToString(h));
            //    }
            //    LOGGER.Debug("Successfully created BatchedCsvHeader.");
            //}
            _header = new ImageHeader();
            _isArrayType = true;
        }

        public IValueList GetMeta()
        {
            return _header;
        }

        public bool IsTerminal()
        {
            return _contentStream.IsTerminal();
        }

        public bool IsParallel()
        {
            return false;
        }

        public IBaseStream Map(Func<string[], int[]> mapFunc)
        {
            throw new NotSupportedException("This conversion is not for images");
            // return (IBaseStream)_contentStream.Map(mapFunc);
        }

        public IBaseStream DoStreamMapping()
        {
            // todo: replace with configurable vector transformers
            return (IBaseStream)_contentStream.Map(bitmapDef =>
           {
               // Convert to bitmap to an array of ints
               bitmapDef.InputVector = bitmapDef.Image.ToVector();
               return bitmapDef;
           });

            //return _contentStream.Map(b => b.ToVector());    
        }

        public void ForEach(Action<object> action)
        {
            throw new NotImplementedException();
        }

        public long Count()
        {
            throw new NotImplementedException();
        }

        /// <summary>
        /// Returns true when a string[] to int[] conversion is needed (when the raw input is string)
        /// </summary>
        /// <returns></returns>
        public bool NeedsStringMapping()
        {
            return false;
        }

        /// <summary>
        /// Factory method to create a {@code BatchedCsvStream}. If isParallel is false,
        /// this stream will behave like a typical stream. See also {@link BatchedCsvStream#batch(Stream, int, boolean, int, int)}
        /// for more fine grained setting of characteristics.
        /// </summary>
        /// <param name="stream">Incomming stream</param>
        /// <param name="batchSize">the "chunk" length to be processed by each Threaded task</param>
        /// <param name="isParallel">if true, batching will take place, otherwise not</param>
        /// <param name="headerLength">number of header lines</param>
        /// <returns></returns>
        public static RawImageStream Batch(IStream<ImageDefinition> stream, int batchSize, bool isParallel, int headerLength, Func<ImageDefinition, ImageDefinition> mappingFunc = null)
        {
            //Notice the Type of the Stream becomes String[] - This is an important optimization for 
            //parsing the sequence number later. (to avoid calling String.split() on each entry)
            //Initializes and creates the CsvHeader here:

            // Create a new string that returns arrays of strings
            RawImageStream csv = new RawImageStream(stream, headerLength, mappingFunc);
            stream.SetOffset(headerLength);
            if (isParallel)
            {
                throw new NotImplementedException("Check the spliterator stuff");
            }
            IStream<ImageDefinition> s = !isParallel ? csv.Continuation(isParallel) : null;
            csv._contentStream = s;
            return csv;
        }

        /// <summary>
        /// Returns the portion of the <see cref="Stream"/> <em>not containing</em>
        /// the header. To obtain the header, refer to: <see cref="GetHeader()"/>
        /// </summary>
        /// <param name="parallel">flag indicating whether the underlying stream should be parallelized.</param>
        /// <returns>the stream continuation</returns>
        internal IStream<ImageDefinition> Continuation(bool parallel)
        {
            //_isTerminal = true;

            if (_contentStream == null)
            {
                throw new InvalidOperationException("You must first create a BatchCsvStream by calling batch(Stream, int, bool, int)");
            }

            int i = 0;

            IStream<ImageDefinition> stream = _contentStream.Map(value =>
            {
                value.RecordNum = i++;
                return value;
                //string[] retVal = new string[value.Length + 1];
                //Array.Copy(value, 0, retVal, 1, value.Length);
                //retVal[0] = i++.ToString();
                //return retVal;
            });

            return stream;
        }
    }

    public class ImageHeader : IValueList
    {
        public Tuple GetRow(int row)
        {
            if (row == 0)
            {
                return new Tuple("category", "imageIn");
            }
            if (row == 1)
            {
                return new Tuple("int", "darr");
            }
            if (row == 2)
            {
                return new Tuple(",");
            }
            throw new NotImplementedException();
        }

        public int Size()
        {
            return 3;
        }

        public bool IsLearn()
        {
            throw new NotImplementedException();
        }

        public bool IsReset()
        {
            throw new NotImplementedException();
        }

        public List<FieldMetaType> GetFieldTypes()
        {
            throw new NotImplementedException();
        }
    }


}