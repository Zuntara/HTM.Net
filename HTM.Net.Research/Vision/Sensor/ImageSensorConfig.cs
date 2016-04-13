using System.Collections.Generic;

namespace HTM.Net.Research.Vision.Sensor
{
    public class ImageSensorOutput
    {
        /// <summary>
        /// The output element count of the 'dataOut' output.
        /// </summary>
        public int[] DataOut { get; set; }
        /// <summary>
        /// The output element count of the 'categoryOut' output (NuPIC 1 only).
        /// </summary> 
        public int[] CategoryOut { get; set; }
        public int[] PartitionOut { get; set; }
        /// <summary>
        /// The output element count of the 'resetOut' output (NuPIC 1 only).
        /// </summary>
        public int[] ResetOut { get; set; }
        /// <summary>
        /// The output element count of the 'bboxOut' output (NuPIC 1 only).
        /// </summary>
        public int[] BboxOut { get; set; }
        public int[] AlphaOut { get; set; }
        /// <summary>
        /// The output element count of the 'auxData' output (NuPIC2 only).
        /// </summary>
        public int? AuxDataWidth { get; set; }
        public object AuxDataOut { get; set; }
    }

    public class ImageSensorConfig : ImageSensorOutput
    {
        /// <summary>
        /// Width of the sensor's output to the network (pixels).
        /// </summary>
        public int Width { get; set; } = 1;
        /// <summary>
        /// Height of the sensor's output to the network (pixels).
        /// </summary>
        public int Height { get; set; } = 1;
        /// <summary>
        /// Optional parameter used to send multiple versions of an image out at the same time.
        /// </summary>
        public int Depth { get; set; } = 1;
        /// <summary>
        /// Current options are 'gray' (8-bit grayscale) and 'bw' (1-bit black and white).
        /// </summary>
        public string Mode { get; set; } = "gray";
        /// <summary>
        ///  ** DEPRECATED ** Whether to send a blank output every
        /// time the explorer generates a reset signal(such as when beginning
        /// a new sweep). Turning on blanks increases the number of iterations.
        /// </summary>
        public bool BlankWithReset { get; set; } = false;
        /// <summary>
        /// Pixel value of background, used for padding a cropped 
        /// image, and also for finding the bounding box in the absence of a mask.
        /// </summary>
        public int Background { get; set; } = 255;
        /// <summary>
        /// Inverts the output of the node (e.g. white pixels become black).
        /// </summary>
        public bool InvertOutput { get; set; } = false;
        /// <summary>
        /// YAML serialized list of filters to apply to each image. Each
        /// element in the list should be either a string (just the filter name) or a
        /// list containing both the filter name and a dictionary specifying its
        /// arguments.
        /// </summary>
        public FilterConfig[] FilterConfigs { get; set; }

        public List<FilterConfig> PostFilterConfigs { get; set; }
        /// <summary>
        /// YAML serialized list containing either a single string
        /// (the name of the explorer) or a list containing both the explorer name
        /// and a dictionary specifying its arguments.
        /// </summary>
        public ExplorerConfig ExplorerConfig { get; set; }
        /// <summary>
        /// Name of file to which to write category number on each compute
        /// (useful for analyzing network accuracy after inference).
        /// </summary>
        public string CategoryOutputFile { get; set; } = string.Empty;
        /// <summary>
        /// Toggle for verbose logging to imagesensor_log.txt.
        /// </summary>
        public bool LogText { get; set; } = false;
        /// <summary>
        /// Toggle for writing each output to disk (as an image) on each iteration.
        /// </summary>
        public bool LogOutputImages { get; set; } = false;
        /// <summary>
        /// Toggle for writing the original, unfiltered version of the current image to disk on each iteration.
        /// </summary>
        public bool LogOriginalImages { get; set; } = false;
        /// <summary>
        /// Toggle for writing the intermediate versions of images to disk as they pass through the filter chain.
        /// </summary>
        public bool LogFilteredImages { get; set; } = false;
        /// <summary>
        /// Toggle for writing an image to disk on each iteration which shows the location of the sensor window.
        /// </summary>
        public bool LogLocationImages { get; set; } = false;
        /// <summary>
        /// Whether to overlay the location rectangle on the original image instead of the filtered image.Does not work if
        /// the two images do not have the same size, and may be nonsensical even if they do 
        /// (for example, if a filter moved the object within the image).
        /// </summary>
        public bool LogLocationOnOriginalImage { get; set; } = false;
        /// <summary>
        /// Toggle for writing a log containing the bounding box information for each output image.
        /// </summary>
        public bool LogBoundingBox { get; set; } = false;
        public string LogDir { get; set; } = "imagesensor_log";
        /// <summary>
        /// Affects the process by which bounding box masks are automatically generated from images based on similarity to the
        /// specified 'background' pixel value.The bounding box will enclose all pixels in the image that differ from 'background' by more than 
        /// the value specified in 'automaskingTolerance'.  Default is 0, which generates bounding boxes that enclose all pixels that differ at all
        /// from the background.In general, increasing the value of 'automaskingTolerance' will produce tighter (smaller) bounding box masks.
        /// </summary>
        public int AutomaskingTolerance { get; set; } = 0;
        /// <summary>
        /// Affects the process by which bounding box masks
        /// are automatically generated from images.After computing the
        /// bounding box based on image similarity with respect to the background,
        /// the box will be expanded by 'automaskPadding' pixels in all four
        /// directions(constrained by the original size of the image.)
        /// </summary>
        public int AutomaskingPadding { get; set; } = 0;
        /// <summary>
        /// Maximum amount of memory that ImageSensor should use for storing images, 
        /// in megabytes.ImageSensor will unload images and filter outputs to stay beneath this ceiling.
        /// Set to -1 for no limit.
        /// </summary>
        public int MemoryLimit { get; set; } = 100;
        /// <summary>
        /// Whether the bounding box found by looking at the 
        /// image background should be set even if it touches one of 
        /// the sides of the image.Set to False to avoid chopping edges off certain images, or True if that is not an issue and you wish to use a sweeping explorer.
        /// </summary>
        public bool MinimalBoundingBox { get; set; } = false;
        public object Keywds { get; set; }
    }
}