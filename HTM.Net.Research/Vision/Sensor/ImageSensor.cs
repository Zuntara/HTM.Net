using System;
using System.Collections.Generic;
using System.Drawing;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Vision.Sensor
{
    /// <summary>
    /// ImageSensor, an extensible sensor for images.
    /// </summary>
    public class ImageSensor : Sensor<Bitmap>
    {
        protected RawImageStream _imageStream;
        protected readonly SensorParams _params;

        public static Sensor<Bitmap> Create(SensorParams p)
        {
            Sensor<Bitmap> fs = new ImageSensor(p);
            return fs;
        }

        protected ImageSensor(SensorParams @params)
        {
            _params = @params;
            if (!@params.HasKey("DIMENSIONS"))
            {
                throw new ArgumentException("Passed improperly formed Tuple: no key for \"DIMENSIONS\"");
            }
            _imageStream = new RawImageStream();
        }

        public override SensorParams GetParams()
        {
            throw new System.NotImplementedException();
        }
        /// <summary>
        /// Used for maken the HTM Sensor output stream, 
        /// gets mapped in <see cref="HTMSensor{T}.GetOutputStream()"/>
        /// </summary>
        /// <returns></returns>
        public override IMetaStream GetInputStream()
        {
            return _imageStream;
        }

        public override IValueList GetMetaInfo()
        {
            throw new System.NotImplementedException();
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
            return (IBaseStream) _contentStream.Map(bitmapDef =>
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