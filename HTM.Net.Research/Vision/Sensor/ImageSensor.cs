using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Vision.Image;
using HTM.Net.Research.Vision.Sensor.Explorers;
using HTM.Net.Research.Vision.Sensor.Filters;
using HTM.Net.Util;
using Kaliko.ImageLibrary;
using Kaliko.ImageLibrary.Filters;
using log4net;
using log4net.Repository.Hierarchy;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Vision.Sensor
{
    /// <summary>
    /// ImageSensor, an extensible sensor for images.
    /// </summary>
    public class ImageSensor : Sensor<ImageDefinition>
    {
        private static readonly ILog Logger = LogManager.GetLogger(typeof(ImageSensor));

        protected const int HEADER_SIZE = 3;
        protected const int BATCH_SIZE = 20;
        protected const bool DEFAULT_PARALLEL_MODE = false;

        protected RawImageStream _imageStream;
        protected ReplaySubject<ImageDefinition> _obsStream;
        protected readonly SensorParams _params;
        protected readonly ImageSensorConfig _imageParameters;

        private List<ImageListEntry> _imageList;
        private List<int> _imageQueue;
        private int _pixelCount;
        private int _holdForOffset;
        private int _iteration;
        private List<Tuple> _filterQueue;
        private ExplorerPosition _prevPosition;

        private List<CategoryEntry> categoryInfo;
        private ExplorerEntry explorer;
        private List<FilterEntry> filters;
        private List<FilterEntry> postFilters;
        private int enabledWidth;
        private int enabledHeight;
        private List<KalikoImage> outputImage;
        private KalikoImage locationImage;
        private int _auxDataWidth;
        private object _cubeOutputs;

        /// <summary>
        /// Creates a new Image sensor with sensor parameters
        /// </summary>
        /// <param name="p"></param>
        /// <returns></returns>
        public static Sensor<ImageDefinition> Create(SensorParams p)
        {
            ImageSensor fs = new ImageSensor(p);
            return fs;
        }

        /// <summary>
        /// Creates a new instance of this <see cref="ImageSensor"/>
        /// </summary>
        /// <param name="params"></param>
        protected ImageSensor(SensorParams @params)
        {
            _params = @params;
            if (!_params.HasKey("IMAGECONFIG"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"IMAGECONFIG\"");
            }
            if (!(_params["IMAGECONFIG"] is ImageSensorConfig))
            {
                throw new ArgumentException("Passed improperly formed Tuple: key \"IMAGECONFIG\" is not of type ImageSensorConfig");
            }
            ImageSensorConfig config = (ImageSensorConfig)_params["IMAGECONFIG"];
            _imageParameters = config;

            // TODO: add some validations?

            // In NuPIC 2, these are all None
            if (_imageParameters.CategoryOut != null && _imageParameters.CategoryOut.Length != 1)
            {
                throw new InvalidOperationException("The 'CategoryOut' output element count must be 1.");
            }
            if (_imageParameters.PartitionOut != null && _imageParameters.PartitionOut.Length != 1)
            {
                throw new InvalidOperationException("The 'PartitionOut' output element count must be 1.");
            }
            if (_imageParameters.ResetOut != null && _imageParameters.ResetOut.Length != 1)
            {
                throw new InvalidOperationException("The 'ResetOut' output element count must be 1.");
            }
            if (_imageParameters.BboxOut != null && _imageParameters.BboxOut.Length != 4)
            {
                throw new InvalidOperationException("The 'BboxOut' output element count must be 4.");
            }
            if (_imageParameters.AlphaOut != null && _imageParameters.AlphaOut.Length != _imageParameters.Width * _imageParameters.Height)
            {
                throw new InvalidOperationException("The 'AlphaOut' output element count must be equal to width * height");
            }

            if (_imageParameters.Mode == "bw" && _imageParameters.Background != 0)
            {
                _imageParameters.Background = 255;
            }
            enabledHeight = _imageParameters.Height;
            enabledWidth = _imageParameters.Width;

            // The imageList data structure contains all the information about all the
            // images which have been loaded via and of the load* methods. Some images
            // may not be in memory, but their metadata is always kept in imageList.
            // imageList[imageIndex] returns all the information about the image with
            // the specified index, in a dictionary. The keys in the dictionary are:
            //   'image': The unfiltered image.
            //   'imagePath': The path from which the image was loaded.
            //   'maskPath': The path from which the mask was loaded.
            //   'categoryName': The name of the image's category.
            //   'categoryIndex': The index of the image's category.
            //   'filtered': A dictionary of filtered images created from this image.
            // In general, images are only loaded once they are needed. But if an image
            // is loaded via loadSerializedImage, then its entry in imageList has an
            //   'image' value but no 'imagePath' value. Thus, it will never be deleted
            // from memory because it cannot be recovered. All other images are fair
            // game.
            // The 'filtered' dictionary requires more explanation. Each key in the
            // dictionary is a tuple specifying the positions of the filters that
            // generated the image. (Filters can generate multiple outputs, so an
            // image that comes out of the filter pipeline must be referenced by its
            // position in the outputs of each filter in the pipeline). The dictionary
            // also contains images that have been run through only part of the filter
            // pipeline, which are kept around for us as inputs for the remaining
            // filters.
            // Here is an example with 3 filters in the pipeline:
            //   0: A Resize filter that generates 3 outputs (small, medium, large)
            //   1: An EqualizeHistogram filter that generates 1 output
            //   2: A Rotation2D filter that generates 5 outputs (5 rotation angles)
            // A typical key for an image would be (0, 0, 2), specifying the smallest
            // scale from the Resize filter (0), the only output from the
            // EqualizeHistogram filter (0), and the middle rotation angle (2).
            // Another valid key would be (1), specifying an image that has gone through
            // the Resize filter (the middle scale), but which has not been through
            // the other filters yet. This image would neven be shown to the network,
            // but it would be used by ImageSensor to compute other images.
            // The _getFilteredImages method is the only method which directly accesses
            // the filtered images in imageList. Filtering is only done on-demand.
            // If _getFilteredImages is called and the requested images have not yet
            // been created, _applyFilter is called to run each filter, and the
            // resulting images are stored in imageList for later use. They may be
            // deleted due to the memoryLimit parameter, in which case they will be
            // recreated later if necessary.

            _imageList = new List<ImageListEntry>();
            categoryInfo = new List<CategoryEntry>(); // (categoryName, canonicalImage) for each category
            _imageQueue = new List<int>(); // Queue of image indices for managing memory
            _filterQueue = new List<Tuple>(); // Queue of filter outputs for mananging memory
            _pixelCount = 0; // Count of total loaded pixels for mananging memory
            outputImage = null; // Copy of the last image sent to the network
            locationImage = null; // Copy of the location image for the last output
            _prevPosition = null; // Position used for the last compute iteration
            _imageParameters.CategoryOutputFile = null; // To write the category on each iteration
            _iteration = 0; // Internal iteration counter
            _holdForOffset = 0;
            _auxDataWidth = _imageParameters.AuxDataWidth.GetValueOrDefault();
            explorer = null;
            SetFilters();
            SetExplorer();

            // Create stream that will spit out the images
            _obsStream = new ReplaySubject<ImageDefinition>();
            var internalStream = new ComputedStream<ImageDefinition>(_obsStream, () =>
            {
                // Call compute method before we are going to read
                Compute();
            });
            _imageStream = RawImageStream.Batch(internalStream, BATCH_SIZE, DEFAULT_PARALLEL_MODE, HEADER_SIZE);
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
            return _iteration == _imageList.Count;
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
        /// Add the specified image to the list of images.
        /// </summary>
        /// <param name="imageObj">Image to load.</param>
        /// <param name="maskPath">Path to mask to load with this image.</param>
        /// <param name="categoryName">Name of the category of this image.</param>
        /// <param name="clearImageList">If True, all loaded images are removed before this image is loaded. If False, this image is appended to the list of images.</param>
        /// <param name="skipExplorerUpdate"></param>
        /// <param name="auxPath">Path to the auxiliary data for the image.</param>
        /// <param name="userAuxData"></param>
        /// <param name="sequenceIndex">Unique sequence index.</param>
        /// <param name="frameIndex">The frame number within the sequence.</param>
        public Tuple LoadSingleImage(KalikoImage imageObj, string maskPath = null, string categoryName = null, bool clearImageList = true,
            bool skipExplorerUpdate = false, string[] auxPath = null, object userAuxData = null, int? sequenceIndex = null,
            int? frameIndex = null)
        {
            if (clearImageList)
            {
                ClearImageList(skipExplorerUpdate: true);
            }

            bool manualAux = userAuxData != null;

            AddImage(image: imageObj, maskPath: maskPath,
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
        /// Add multiple images to the list of images.
        /// 
        /// This command is equivalent to calling loadSingleImage repeatedly, but it
        /// is faster because it avoids updating the explorer between each image, and
        /// because it only involves one call to the runtime engine.
        /// </summary>
        /// <param name="images">List of images.</param>
        /// <param name="categoryNames">Category name for each image (or can be a single string 
        /// with the category name that should be applied to all images).</param>
        /// <param name="clearImageList">If True, all loaded images are removed before this
        /// image is loaded.If False, this image is appended to the list of images.</param>
        /// <returns></returns>
        public Tuple LoadSpecificImages(KalikoImage[] images, string[] categoryNames, bool clearImageList = true)
        {
            if (categoryNames != null && categoryNames.Length < images.Length && categoryNames.Length == 1)
            {
                // One category for all
                string categoryName = categoryNames[0];
                categoryNames = new string[images.Length];
                for (int i = 0; i < categoryNames.Length; i++)
                {
                    categoryNames[i] = categoryName;
                }
            }
            if (categoryNames != null && categoryNames.Length != images.Length)
            {
                throw new InvalidOperationException("category name count does not match images");
            }
            if (clearImageList)
            {
                ClearImageList(true);
            }
            for (int i = 0; i < images.Length; i++)
            {
                string categoryName = null;
                if (categoryNames != null)
                {
                    categoryName = categoryNames[i];
                }

                LoadSingleImage(images[i], categoryName: categoryName, clearImageList: false, skipExplorerUpdate: true);
            }
            explorer.Explorer.Update(new Map<string, object> { {"numImages", _imageList.Count} });

            return new Tuple(_imageList.Count, _imageList.Count(i => i.HasMask));
        }

        /// <summary>
        /// Generate the next sensor output and send it out.
        /// This method is called by the runtime engine.
        /// </summary>
        public void Compute(ImageSensorOutput output = null)
        {
            if (_imageList.Count == 0)
            {
                throw new InvalidOperationException("ImageSensor can't run compute: no images loaded");
            }

            Debug.WriteLine("Calling Compute() with {0} images in memory.", _imageList.Count);

            if (output == null)
            {
                output = _imageParameters;
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

            UpdatePrevPosition();
            if (prevPosition == null) prevPosition = _prevPosition;

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
            var outputImages = (List<KalikoImage>)outputImgsTuple.Get(0);
            var finalOutput = (KalikoImage)outputImgsTuple.Get(1);

            // Compile information about this iteration and log it
            ImageListEntry imageInfo = GetImageInfo();
            string fileName = "";
            if (string.IsNullOrWhiteSpace(imageInfo.ImagePath))
            {
                fileName = "";
            }
            else
            {
                fileName = Path.GetFileName(imageInfo.ImagePath);
            }
            int category = imageInfo.CategoryIndex.GetValueOrDefault(-1);
            string categoryName;
            if (category == -1)
            {
                categoryName = "";
            }
            else
            {
                categoryName = categoryInfo[category].CategoryName;
            }
            LogCommand(new NamedTuple(new[] { "iteration", "position", "filename", "categoryIndex", "categoryName", "erode", "blank" },
                _iteration, explorer.Explorer.GetPosition(), fileName, category, categoryName, imageInfo.Erode, prevPosition.Reset.GetValueOrDefault() && _imageParameters.BlankWithReset), null);

            // If we don"t have a partition ID at this point (e.g., because
            // of memory limits), then we need to try and pull from the
            // just-loaded image
            if (imageInfo.PartitionId == null)
            {
                int? imgPos = explorer.Explorer.GetPosition().Image;
                imageInfo.PartitionId = _imageList[imgPos.GetValueOrDefault()].PartitionId;
            }

            if (_imageParameters.Depth == 1)
            {
                outputImage = new List<KalikoImage> { outputImages[0] };
            }
            else
            {
                outputImage = outputImages;
            }

            // Invalidate the old location image
            locationImage = null;

            // Log the images and locations if specified
            if (_imageParameters.LogOutputImages) LogOutputImages();
            if (_imageParameters.LogOriginalImages) LogOriginalImage();
            if (_imageParameters.LogLocationImages) LogLocationImage();

            // Save category to file
            WriteCategoryToFile(category);

            // Process the output
            // --------------------------------------------------------------------------

            // Convert the output images to a numpy vector
            var croppedArrays = outputImages.Select(image => ArrayUtils.ReshapeAverage(image.ByteArray, image.Width, 3, 1)).ToList();
            // Pad the images to fit the full output size if necessary generating
            // a stack of images, each of them self.width X self.height
            var pad = _cubeOutputs != null &&
                      (_imageParameters.Depth > 1 || croppedArrays[0].Length != _imageParameters.Height ||
                       croppedArrays[0][0].Length != _imageParameters.Width);

            List<int[][]> fullArrays;
            if (pad)
            {
                fullArrays = ArrayUtils.XRange(0, _imageParameters.Depth, 1)
                    .Select(i => ArrayUtils.CreateJaggedArray<int>(_imageParameters.Height, _imageParameters.Width))
                    .ToList();
                throw new NotImplementedException("todo");
            }
            else
            {
                fullArrays = croppedArrays;
            }
            // Flatten and concatenate the arrays
            int[] outputArray = fullArrays.SelectMany(fa => ArrayUtils.Reshape(fa)).ToArray();

            // Send black and white images as binary (0, 1) instead of (0..255)
            if (_imageParameters.Mode == "bw")
            {
                outputArray = outputArray.Select(o => (int)Math.Round(o / 255.0)).ToArray();
            }

            // dataOut - main output
            if (finalOutput == null)
            {
                output.DataOut = outputArray;
            }
            else
            {
                output.DataOut = finalOutput.IntArray;
            }

            // categoryOut - category index
            output.CategoryOut = new[] { category };
            // auxDataOut - auxiliary data
            var auxDataOut = imageInfo.AuxData;
            if (auxDataOut != null)
            {
                output.AuxDataOut = auxDataOut;
            }
            // resetOut - reset flag
            if (output.ResetOut != null)
            {
                output.ResetOut = new[] { prevPosition.Reset.GetValueOrDefault() ? 1 : 0 };
            }
            // bboxOut - bounding box
            if (output.BboxOut != null && output.BboxOut.Length == 4)
            {
                var bbox = outputImages[0].SplitGrayscale()[1].GetBBox();
                if (bbox == null)
                {
                    bbox = new Rectangle(0, 0, 0, 0);
                }
                output.BboxOut = new[] { bbox.X, bbox.Y, bbox.Width, bbox.Height };
                // Optionally log the bounding box information
                if (_imageParameters.LogBoundingBox)
                {
                    LogBoundingBox(bbox);
                }
            }
            // alphaOut - alpha channel
            if (output.AlphaOut != null && output.AlphaOut.Length > 1)
            {
                var alphaOut = outputImages[0].SplitGrayscale()[1].IntArray;
                if (!imageInfo.Erode)
                {
                    // Change the 0th element of the output to signal that the alpha
                    // channel should be dilated, not eroded
                    alphaOut[0] = -alphaOut[0] - 1;
                }
                output.AlphaOut = alphaOut;
            }
            //  partitionOut - partition ID (defaults to zero)
            if (output.PartitionOut != null)
            {
                var partition = imageInfo.PartitionId.GetValueOrDefault(0);
                output.PartitionOut = new[] { partition };
            }

            WriteToInputStream(output);
        }

        /// <summary>
        /// Writes the output of the compute() method to the input stream for usage in the network
        /// </summary>
        /// <param name="output"></param>
        private void WriteToInputStream(ImageSensorOutput output)
        {
            ImageDefinition definition = new ImageDefinition();
            definition.CategoryIndices = output.CategoryOut;
            definition.InputVector = output.DataOut;
            definition.RecordNum = _iteration - 1;
            //definition.ImageInputField = null;

            _obsStream.OnNext(definition);
        }

        /// <summary>
        ///  Deep copy position to self.prevPosition.
        /// </summary>
        private void UpdatePrevPosition()
        {
            var position = explorer.Explorer.GetPosition();
            _prevPosition = new ExplorerPosition
            {
                Image = position.Image,
                Filters = new List<int>(position.Filters),
                Offset = position.Offset,
                Reset = position.Reset
            };
        }

        /// <summary>
        /// Get the dictionary of info for the image, excluding actual PIL images.
        /// </summary>
        /// <param name="imageIndex">Image index to use. Uses current position if not specified.</param>
        private ImageListEntry GetImageInfo(int? imageIndex = null)
        {
            if (!imageIndex.HasValue)
            {
                imageIndex = explorer.Explorer.GetPosition().Image;
            }
            var item = _imageList[imageIndex.GetValueOrDefault()].Copy();
            item.Image = null;
            item.Filtered = null;
            return item;
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
            if (_prevPosition.Reset.GetValueOrDefault() && _imageParameters.BlankWithReset)
            {
                var images = new KalikoImage[_imageParameters.Depth];
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
                int dstImgWidth = Math.Max(_imageParameters.Width, enabledWidth);
                int dstImgHeight = Math.Max(_imageParameters.Width, enabledHeight);

                // Cut out the relevant part of each image
                List<KalikoImage> newImages = new List<KalikoImage>();

                for (int i = 0; i < allImages.Count; i++)
                {
                    KalikoImage image = allImages[i];

                    int x = (int)(offset.X * scaleX[i]);
                    int y = (int)(offset.Y * scaleY[i]);

                    var croppedImage = image.Clone();
                    croppedImage.BackgroundColor = Color.FromArgb(_imageParameters.Background, _imageParameters.Background, _imageParameters.Background);
                    croppedImage.Crop(Math.Max(0, x), Math.Max(0, y),
                        Math.Min(x + dstImgWidth, image.Width), Math.Min(x + dstImgHeight, image.Height));
                    // TODO : review , the original code adds alpha mask and pastes image into new one with max(0,-x) and max(0,-y)

                    // TODO: copy cropped image into other image
                    croppedImage = croppedImage.Scale(new Kaliko.ImageLibrary.Scaling.PadScaling(image.Width, image.Height,
                        image.BackgroundColor));
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
                if (croppedImages.Count != _imageParameters.Depth)
                {
                    throw new InvalidOperationException(string.Format("The filters and postFilters created {0} images to send out simultaneously, which does not match ImageSensor's depth parameter, set to {1}.",
                        croppedImages.Count, _imageParameters.Depth));
                }

                // Invert output if necessary
                if (_imageParameters.InvertOutput)
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

            if (_imageParameters.LogFilteredImages)
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
            if (filters == null || !filters.Any())
            {
                // No filters - return original version
                return new List<KalikoImage> { _imageList[position.Image.GetValueOrDefault()].Image };
            }

            // Iterate through the specified list of filter positions
            // Run filters as necessary
            Dictionary<Tuple, List<KalikoImage>> allFilteredImages = _imageList[position.Image.GetValueOrDefault()].Filtered;
            Tuple filterPosition = new Tuple();
            int filterIndex = 0;
            foreach (var pos in position.Filters)
            {
                filterPosition += new Tuple(pos);

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
                    var newFilteredImages = ApplyFilter(imageToFilter, position.Image, filterIndex);
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
                filterIndex++;
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
        /// Get the specified image, loading it if necessary.
        /// </summary>
        /// <param name="index">Index of the image to retrieve. Retrieves the current image if not specified.</param>
        /// <returns></returns>
        private KalikoImage GetOriginalImages(int? index = null)
        {
            if (!index.HasValue)
            {
                index = explorer.Explorer.GetPosition().Image;
            }
            if (_imageList[index.GetValueOrDefault()].Image == null)
            {
                LoadImage(index.GetValueOrDefault());
            }
            return _imageList[index.GetValueOrDefault()].Image;
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
            if ((int)outputCount.Get(0) != filtered.Count || filtered.Any(outputs => outputs.Count != (int)outputCount.Get(0)))
            {
                throw new InvalidOperationException("The " + filters[filterIndex].FilterName +
                         " filter did not return the correct number of outputs. The number of images that it returned does not " +
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

            if (_imageParameters.LogFilteredImages)
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
        /// <param name="image"></param>
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
                Image = image?.Clone(),
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
                // Look up the category in categoryInfo
                for (int i = 0; i < categoryInfo.Count; i++)
                {
                    if (categoryInfo.ElementAt(i).CategoryName.Equals(item.CategoryName, StringComparison.InvariantCultureIgnoreCase))
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
                    KalikoImage original = LoadImage(_imageList.Count - 1, returnOriginal: true, setErodeFlag: setErodeFlag);

                    if (image == null)
                    {
                        _imageQueue.Insert(0, _imageList.Count - 1);
                    }
                    // Append this category to categoryInfo
                    categoryInfo.Add(new CategoryEntry { CategoryName = item.CategoryName, CanonicalImage = original });
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
                    var extrema = item.Image.SplitGrayscale()[1].ByteArray.GetExtrema(); // alpha channel
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
            Logger.Info("Clearing images");
            
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

        /// <summary>
        /// Write the specified category index to the file at self.categoryOutputFile.
        /// </summary>
        /// <param name="category">Category index (integer).</param>
        private void WriteCategoryToFile(int category)
        {
            // TODO: implement when needed.
        }

        private void LogLocationImage()
        {
            // TODO: implement when needed.
        }

        private void LogOriginalImage()
        {
            // TODO: implement when needed.
        }

        private void LogOutputImages()
        {
            // TODO: implement when needed.
        }

        private void LogBoundingBox(Rectangle bbox)
        {
            // TODO: implement when needed.
        }

        /// <summary>
        /// Change one or more filters, and recompute the ones that changed.
        /// Filters should be located next to the BaseFilter.
        /// </summary>
        private void SetFilters()
        {
            var configs = _imageParameters.FilterConfigs;

            filters = new List<FilterEntry>();

            if (configs != null)
            {
                foreach (FilterConfig config in configs)
                {
                    FilterEntry entry = new FilterEntry();
                    entry.FilterName = config.FilterName;
                    entry.FilterArgs = config.FilterArgs;

                    filters.Add(entry);
                    //Type filterType = Type.GetType(config.FilterName) ?? 
                    //    Type.GetType(typeof(BaseFilter).Namespace + "." + config.FilterName);

                    //entry.Filter = BaseFilter.CreateWithParameters(filterType, config.FilterArgs);
                }
                ImportFilters(filters);

                // Validate no filter except the last returns simultaneous responses
                for (int i = 0; i < filters.Count - 1; i++)
                {
                    var outputCount = filters[i].Filter.GetOutputCount();
                    if (outputCount is Tuple && ((Tuple)outputCount).Count > 1 && (int)((Tuple)outputCount).Get(1) > 1)
                    {
                        throw new InvalidOperationException(string.Format("Only the last filter can return a nested list of images (multiple simultaneous responses). " +
                                                            "The {0} filter index {1} of {2} creates {3} simultaneous responses.",
                                                            filters[i].FilterName, i, filters.Count - 1, (int)((Tuple)outputCount).Get(1)));
                    }
                }
                // Invalidate the filtered versions of all images
                foreach (var item in _imageList)
                {
                    if (item.Filtered != null && item.Filtered.Any())
                    {
                        item.Filtered = new Dictionary<Tuple, List<KalikoImage>>();
                    }
                }
                _filterQueue = new List<Tuple>();
                // Update the pixel count to only count to the original images
                _pixelCount = 0;
                foreach (var i in _imageQueue)
                {
                    var image = _imageList[i].Image;
                    _pixelCount += image.Width * image.Height;
                }
                // Tell the explorer about these new filters
                if (explorer != null && explorer.Explorer != null)
                {
                    explorer.Explorer.Update(new Map<string, object> { { "numFilters", filters.Count }, { "numFilterOutputs", GetNumFilterOutputs(filters) } });
                }
            }
        }

        /// <summary>
        /// Import and instantiate all the specified filters.
        /// This method lives on its own so that it can be used by both _setFilters
        /// and _setPostFilters.
        /// </summary>
        private void ImportFilters(List<FilterEntry> filterEntries)
        {
            for (int i = 0; i < filterEntries.Count; i++)
            {
                // Import the filter
                // If name is just the class name, such as 'PadToFit', we assume the same
                // name for the module: names = ['PadToFit', 'PadToFit']
                // If name is of the form 'ModuleName.ClassName' (useful to try multiple
                // versions of the same filter): names = ['ModuleName', 'ClassName']
                // By default, ImageSensor searches for filters in
                // nupic.vision.regions.ImageSensorFilters. If the import fails, it tries
                // the import unmodified - so you may use filters that are located
                // anywhere that Python knows about.
                string moduleName = "";
                string className = "";
                if (!filterEntries[i].FilterName.Contains("."))
                {
                    moduleName = className = filterEntries[i].FilterName;
                }
                else
                {
                    string[] components = filterEntries[i].FilterName.Split('.');
                    moduleName = string.Join(".", components.Take(components.Length - 1));
                    className = components.Last();
                }

                // Search the filter
                Type filterType = Type.GetType(moduleName + "." + className) ??
                    Type.GetType(typeof(BaseFilter).Namespace + "." + className);
                if (filterType == null)
                {
                    throw new InvalidOperationException("Could not find filter: " + filterEntries[i].FilterName);
                }
                filterEntries[i].Filter = BaseFilter.CreateWithParameters(filterType, filterEntries[i].FilterArgs);
                filterEntries[i].Filter.Update(_imageParameters.Mode, _imageParameters.Background);
            }
        }

        /// <summary>
        /// Set the explorer (algorithm used to explore the input space).
        /// </summary>
        private void SetExplorer()
        {
            var config = _imageParameters.ExplorerConfig;
            if (config == null)
            {
                throw new InvalidOperationException("Must specify explorer (try 'Flash' for no sweeping)");
            }
            explorer = new ExplorerEntry();
            explorer.ExplorerName = config.ExplorerName;
            explorer.ExplorerArgs = config.ExplorerArgs;

            Type explorerType = Type.GetType(config.ExplorerName) ??
                Type.GetType(typeof(BaseExplorer).Namespace + "." + config.ExplorerName);

            explorer.Explorer = BaseExplorer.CreateWithParameters(explorerType, GetOriginalImages, GetFilteredImages, GetImageInfo,
                (int?)config.ExplorerArgs.Get("seed"), (int)config.ExplorerArgs.Get("holdFor", 1), config.ExplorerArgs);

            explorer.Explorer.Update(new Map<string, object>
            {
                {"numImages", _imageList.Count },
                {"numFilters", filters.Count },
                {"numFilterOutputs", GetNumFilterOutputs(filters) },
                {"enabledWidth", enabledWidth },
                {"enabledHeight", enabledHeight },
                {"blankWithReset", _imageParameters.BlankWithReset },
            });
        }



        /// <summary>
        /// Return the number of outputs for each filter.
        /// Ignores simultaneous outputs.
        /// </summary>
        /// <param name="filters"></param>
        /// <returns></returns>
        private static List<int> GetNumFilterOutputs(List<FilterEntry> filters)
        {
            var numFilterOutputs = new List<int>();
            foreach (var f in filters)
            {
                var n = f.Filter.GetOutputCount();
                if (n is Tuple)
                {
                    numFilterOutputs.Add((int)((Tuple)n).Get(0));
                }
                else if (n is int)
                {
                    numFilterOutputs.Add((int)n);
                }
                else
                {
                    throw new InvalidOperationException(string.Format("{0} filter must return an int or a tuple of two ints from getOutputCount()", f.FilterName));
                }
            }
            return numFilterOutputs;
        }

        #endregion
    }
}