using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using HTM.Net.Util;
using log4net;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Network.Sensor
{
    /// <summary>
    /// Defines a CSV stream that reads CSV files
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class BatchedCsvStream<T> : IMetaStream<T>
    {
        [NonSerialized]
        private static readonly ILog Logger = LogManager.GetLogger(typeof(BatchedCsvStream<T>));

        private readonly int _headerLength;
        private BatchedCsvHeader<string[]> _header;
        private bool _isArrayType;
        internal IStream<string[]> _contentStream;

        public BatchedCsvStream(IStream<string> stream, int headerSize, Func<string, string[]> mappingFunc)
        {
            _headerLength = headerSize;
            MakeHeader(stream);
            // return line.split("[\\s]*,[\\s]*", -1); 
            //string tempValue;
            //while ((tempValue = stream.Read()) != null)
            //{
            //    _contents.Add(tempValue.Split(','));
            //}

            //Regex regex = new Regex("[\\s]*,[\\s]*");
            if (mappingFunc != null)
            {
                _contentStream = stream.Map(mappingFunc);
            }
            else
            {
                _contentStream = stream.Map(s =>
                {
                    //Debug.WriteLine(">> Splitting CSV string");
                    if (s.StartsWith("[") || _isArrayType)
                    {
                        // todo: review this
                        //return Regex.Split(s, "[\\s]*,[\\s]*");
                        return new[] { s };
                    }
                    return s.Split(',');
                });
            }
            Logger.Debug("Created BatchedCsvStream");
        }

        /// <summary>
        /// Extracts the header from the CSV file
        /// </summary>
        private void MakeHeader(IStream<string> stream)
        {
            List<string[]> contents = new List<string[]>();

            for (int i = 0; i < _headerLength; i++)
            {
                contents.Add(stream.Read().Split(','));
            }
            stream.SetOffset(_headerLength);
            BatchedCsvHeader<string[]> header = new BatchedCsvHeader<string[]>(contents, _headerLength);
            _header = header;
            _isArrayType = IsArrayType();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug("Created Header:");
                foreach (string[] h in contents)
                {
                    Logger.Debug("\t" + Arrays.ToString(h));
                }
                Logger.Debug("Successfully created BatchedCsvHeader.");
            }
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
        public static BatchedCsvStream<string[]> Batch(IStream<string> stream, int batchSize, bool isParallel, int headerLength, Func<string, string[]> mappingFunc = null)
        {
            //Notice the Type of the Stream becomes String[] - This is an important optimization for 
            //parsing the sequence number later. (to avoid calling String.split() on each entry)
            //Initializes and creates the CsvHeader here:

            // Create a new string that returns arrays of strings
            BatchedCsvStream<string[]> csv = new BatchedCsvStream<string[]>(stream, headerLength, mappingFunc);
            stream.SetOffset(headerLength);
            if (isParallel)
            {
                throw new NotImplementedException("Check the spliterator stuff");
            }
            IStream<string[]> s = !isParallel ? csv.Continuation(isParallel) : null;
            csv._contentStream = s;
            return csv;
        }

        /// <summary>
        /// Returns the portion of the <see cref="Stream"/> <em>not containing</em>
        /// the header. To obtain the header, refer to: <see cref="GetHeader()"/>
        /// </summary>
        /// <param name="parallel">flag indicating whether the underlying stream should be parallelized.</param>
        /// <returns>the stream continuation</returns>
        internal IStream<string[]> Continuation(bool parallel)
        {
            //_isTerminal = true;

            if (_contentStream == null)
            {
                throw new InvalidOperationException("You must first create a BatchCsvStream by calling batch(Stream, int, bool, int)");
            }

            int i = 0;
            IStream<string[]> stream = _contentStream.Map(value =>
            {
                string[] retVal = new string[value.Length + 1];
                Array.Copy(value, 0, retVal, 1, value.Length);
                retVal[0] = i++.ToString();
                return retVal;
            });

            //_contentStream = stream;

            //for (int i = 0; i < _contents.Count; i++)
            //{
            //    string[] value = _contents[i];
            //    string[] retVal = new string[value.Length + 1];
            //    Array.Copy(value, 0, retVal, 1, value.Length);
            //    retVal[0] = i.ToString();
            //}


            //var splitIterator = GetSequenceIterator(it);

            //StreamWriter writer = new StreamWriter(stream);
            //while (splitIterator.MoveNext())
            //{
            //    var current = splitIterator.Current;
            //    writer.WriteLine(current);
            //}
            //return StreamSupport.stream(
            //    Spliterators.spliteratorUnknownSize(
            //        parallel ? it : isArrayType ? getArraySequenceIterator(it) : getSequenceIterator(it), // Return a sequencing iterator if not parallel
            //                                                                                              // otherwise the Spliterator handles the sequencing
            //                                                                                              // through the special SequencingConsumer
            //        Spliterator.ORDERED | Spliterator.NONNULL | Spliterator.IMMUTABLE),
            //        parallel);
            return stream;
        }

        /// <summary>
        /// Returns a flag indicating whether the input field is an array
        /// </summary>
        private bool IsArrayType()
        {
            Tuple[] headerValues = GetHeader().GetHeaderValues();
            if (headerValues.Length < 3)
            {
                return false;
            }
            if (headerValues[1].All().Any(o => o.ToString().ToLower().Equals("sarr") || o.ToString().ToLower().Equals("darr")))
            {
                return _isArrayType = true;
            }
            return false;
        }

        /// <summary>
        /// Returns the <see cref="BatchedCsvHeader{T}"/>
        /// </summary>
        /// <returns></returns>
        public BatchedCsvHeader<string[]> GetHeader()
        {
            return _header;
        }

        #region Implementation of IMetaStream

        public IStream<TResult> Map<TResult>(Func<T, TResult> mapFunc)
        {
            throw new NotImplementedException();
        }

        public IStream<int[]> Map(Func<string[], int[]> mapFunc)
        {
            return _contentStream.Map(mapFunc);
            //return new Stream<int[]>(_contents.Select(mapFunc).ToArray());
        }

        public long Count()
        {
            return _contentStream.Count();
        }

        /// <summary>
        /// Returns a <see cref="IValueList"/> containing meta information (i.e. header information)
        /// which can be used to infer the structure of the underlying stream.
        /// </summary>
        /// <returns> a <see cref="IValueList"/> describing meta features of this stream.</returns>
        public IValueList GetMeta()
        {
            return GetHeader();
        }

        /// <summary>
        /// <p>
        /// Returns a flag indicating whether the underlying stream has had
        /// a terminal operation called on it, indicating that it can no longer
        /// have operations built up on it.
        /// </p>
        /// <p>
        /// The "terminal" flag if true does not indicate that the stream has reached
        /// the end of its data, it just means that a terminating operation has been
        /// invoked and that it can no longer support intermediate operation creation.
        /// </p>
        /// </summary>
        /// <returns>true if terminal, false if not.</returns>
        public bool IsTerminal()
        {
            return _contentStream.IsTerminal();
        }

        public bool IsParallel()
        {
            return false;
        }

        #endregion

        [Serializable]
        public class BatchedCsvHeader<THeaderLine> : IValueList
        {
            /// <summary>
            /// Container for the field values
            /// </summary>
            private readonly Tuple[] _headerValues;

            /// <summary>
            /// Constructs a new <see cref="BatchedCsvHeader{T}"/>
            /// </summary>
            /// <param name="lines">List of csv strings</param>
            /// <param name="configuredHeaderLength">Number of header rows</param>
            public BatchedCsvHeader(List<THeaderLine> lines, int configuredHeaderLength)
            {

                if ((configuredHeaderLength < 1 || lines == null || lines.Count < 1) ||
                    (configuredHeaderLength > 1 && lines.Count != configuredHeaderLength))
                {

                    throw new InvalidOperationException("Actual Header was not the expected size: " +
                        (configuredHeaderLength < 1 ? "> 1" : configuredHeaderLength.ToString()) +
                            ", but was: " + (lines == null ? "null" : lines.Count.ToString()));
                }

                _headerValues = new Tuple[configuredHeaderLength];
                for (int i = 0; i < _headerValues.Length; i++)
                {
                    if (lines[i] is Array)
                    {
                        _headerValues[i] = new Tuple((Array)((object)lines[i]));
                    }
                    else
                    {
                        _headerValues[i] = new Tuple(lines[i]);
                    }
                }
            }

            public Tuple[] GetHeaderValues()
            {
                return _headerValues;
            }

            /// <summary>
            /// Returns the array of values  (<see cref="Tuple"/>) at the specified index.
            /// </summary>
            /// <param name="index">the index of the Tuple to be retrieved.</param>
            /// <returns></returns>
            public Tuple GetRow(int index)
            {
                if (index >= _headerValues.Length)
                {
                    return null;
                }
                return _headerValues[index];
            }

            /// <summary>
            /// Returns the current number of lines in the header.
            /// </summary>
            /// <returns>the current number of lines in the header.</returns>
            public int Size()
            {
                return _headerValues?.Length ?? 0;
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

            public override string ToString()
            {
                StringBuilder sb = new StringBuilder();
                _headerValues.ToList().ForEach(l => sb.Append(l).Append("\n"));
                return sb.ToString();
            }
        }

        public void ForEach(Action<object> action)
        {
            // TODO: ensure the foreach goes further on the mapped stream
            _contentStream.ForEach(action);
        }

        /// <summary>
        /// Convienience method for inserting data in the base stream
        /// </summary>
        /// <param name="input"></param>
        public void Write(string[] input)
        {
            _contentStream.Write(input);
        }
    }
}