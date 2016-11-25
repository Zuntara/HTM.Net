using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Util;

namespace HTM.Net.Network.Sensor
{
    public interface IHTMSensor : ISensor, IPersistable
    {
        IBaseStream GetOutputStream();

        IDictionary<string, object> GetInputMap();
        void AssignBasicInputMap(string[] rawData);

        void SetEncoder(MultiEncoder encoder);

        /// <summary>
        /// DO NOT CALL THIS METHOD! 
        /// Used internally by deserialization routines.
        /// </summary>
        /// <param name="localParameters"></param>
        void SetLocalParameters(Parameters localParameters);
    }

    /**
     * <p>
     * Decorator for {@link Sensor} types adding HTM
     * specific functionality to sensors and streams.
     * </p><p>
     * The {@code HTMSensor} decorates the sensor with the expected
     * meta data containing field name and field type information 
     * together with the information needed in order to auto-create
     * {@link Encoder}s necessary to output a bit vector specifically
     * tailored for HTM (Hierarchical Temporal Memory) input.
     * </p><p>
     * This class also has very specific date handling capability for
     * the "timestamp" data field type.
     * </p><p>
     * Output is attained by calling {@link #getOutputStream()}. This class
     * extends the Stream API to be able to "fork" streams, so that
     * a single stream can supply multiple fanouts.
     * </p><p>
     * <b>Warning:</b> if {@link #getOutputStream()} is called multiple times,
     * all calls must precede any operations on any of the supplied streams. 
     * </p><p>
     *
     * @param <T>   the input type (i.e. File, URL, etc.)
     */
    [Serializable]
    public class HTMSensor<T> : Sensor<T>, IHTMSensor
    {
        private ISensor @delegate;
        private SensorParams sensorParams;
        private Header header;
        private Parameters localParameters;
        private MultiEncoder encoder;
        [NonSerialized]
        private IBaseStream outputStream;
        [NonSerialized]
        private List<int[]> output;
        [NonSerialized]
        private InputMap inputMap;

        private Map<int, IEncoder> indexToEncoderMap;
        private Map<string, int> indexFieldMap = new Map<string, int>();


        //private IEnumerator mainIterator;
        //private List<LinkedList<int[]>> fanOuts = new List<LinkedList<int[]>>();

        /** Protects {@ #mainIterator} formation and the next() call */
        private readonly object _criticalAccessLock = new object();

        /**
         * Decorator pattern to construct a new HTMSensor wrapping the specified {@link Sensor}.
         * 
         * @param sensor
         */
        public HTMSensor(ISensor sensor)
        {
            this.@delegate = sensor;
            sensorParams = sensor.GetSensorParams();
            header = new Header(sensor.GetInputStream().GetMeta());
            if (header == null || header.Size() < 3)
            {
                throw new InvalidOperationException("Header must always be present; and have 3 lines.");
            }
            CreateEncoder();
        }

        /**
         * DO NOT CALL THIS METHOD! 
         * Used internally by deserialization routines.
         * 
         * Sets the {@link Parameters} reconstituted from deserialization 
         * @param localParameters   the Parameters to use.
         */
        public void SetLocalParameters(Parameters localParameters)
        {
            this.localParameters = localParameters;
        }

        #region Overrides of Persistable

        public override object PostDeSerialize()
        {
            InitEncoder(localParameters);
            MakeIndexEncoderMap();
            return this;
        }

        #endregion

        /**
        * Called internally during construction to build the encoders
        * needed to process the configured field types.
        */
        private void CreateEncoder()
        {
            encoder = (MultiEncoder)MultiEncoder.GetBuilder().Name("MultiEncoder").Build();

            Map<string, Map<string, object>> encoderSettings;
            if (localParameters != null &&
                (encoderSettings = (Map<string, Map<string, object>>)localParameters.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP)) != null &&
                    encoderSettings.Any())
            {
                InitEncoders(encoderSettings);
                MakeIndexEncoderMap();
            }
        }

        /**
         * Sets up a mapping which describes the order of occurrence of comma
         * separated fields - mapping their ordinal position to the {@link Encoder}
         * which services the encoding of the field occurring in that position. This
         * sequence of types is contained by an instance of {@link Header} which
         * makes available an array of {@link FieldMetaType}s.
         */
        private void MakeIndexEncoderMap()
        {
            indexToEncoderMap = new Map<int, IEncoder>();

            for (int i = 0, size = header.GetFieldNames().Count; i < size; i++)
            {
                switch (header.GetFieldTypes()[i])
                {
                    case FieldMetaType.DateTime:
                        Optional<DateEncoder> de = GetDateEncoder(encoder);
                        if (de.IsPresent)
                        {
                            indexToEncoderMap.Add(i, de.Value);
                        }
                        else
                        {
                            throw new ArgumentException("DateEncoder never initialized: " + header.GetFieldNames()[i]);
                        }
                        break;
                    case FieldMetaType.Boolean:
                    case FieldMetaType.Float:
                    case FieldMetaType.Integer:
                        Optional<IEncoder> ne = GetNumberEncoder(encoder);
                        if (ne.IsPresent)
                        {
                            indexToEncoderMap.Add(i, ne.Value);
                        }
                        else
                        {
                            throw new ArgumentException("Number (or Boolean) encoder never initialized: " + header.GetFieldNames()[i]);
                        }
                        break;
                    case FieldMetaType.List:
                    case FieldMetaType.String:
                        Optional<IEncoder> ce = GetCategoryEncoder(encoder);
                        if (ce.IsPresent)
                        {
                            indexToEncoderMap.Add(i, ce.Value);
                        }
                        else
                        {
                            throw new ArgumentException("Category encoder never initialized: " + header.GetFieldNames()[i]);
                        }
                        break;
                    case FieldMetaType.Coord:
                    case FieldMetaType.Geo:
                        Optional<IEncoder> ge = GetCoordinateEncoder(encoder);
                        if (ge.IsPresent)
                        {
                            indexToEncoderMap.Add(i, ge.Value);
                        }
                        else
                        {
                            throw new ArgumentException("Coordinate encoder never initialized: " + header.GetFieldNames()[i]);
                        }
                        break;
                    case FieldMetaType.SparseArray:
                    case FieldMetaType.DenseArray:
                        Optional<SDRPassThroughEncoder> spte = GetSDRPassThroughEncoder(encoder);
                        if (spte.IsPresent)
                        {
                            indexToEncoderMap.Add(i, spte.Value);
                        }
                        else
                        {
                            throw new ArgumentException("SDRPassThroughEncoder encoder never initialized: " + header.GetFieldNames()[i]);
                        }
                        break;
                    default:
                        break;
                }
            }

        }

        /**
         * Returns an instance of {@link SensorParams} used 
         * to initialize the different types of Sensors with
         * their resource location or source object.
         * 
         * @return a {@link SensorParams} object.
         */
        public override SensorParams GetSensorParams()
        {
            return sensorParams;
        }

        /**
         * <p>
         * Main method by which this Sensor's information is retrieved.
         * </p><p>
         * This method returns a subclass of Stream ({@link MetaStream})
         * capable of returning a flag indicating whether a terminal operation
         * has been performed on the stream (i.e. see {@link MetaStream#isTerminal()});
         * in addition the MetaStream returned can return meta information (see
         * {@link MetaStream#getMeta()}.
         * </p>
         * @return  a {@link MetaStream} instance.
         */
        public override IMetaStream GetInputStream()
        {
            return (IMetaStream)@delegate.GetInputStream();
        }

        /**
         * Specialized {@link Map} for the avoidance of key hashing. This
         * optimization overrides {@link Map#get(Object)directly accesses the input arrays providing input
         * and should be extremely faster.
         */
        [Serializable]
        public class InputMap : IDictionary<string, object>
        {
            private readonly HTMSensor<T> _parent;
            private const long serialVersionUID = 1L;

            internal FieldMetaType[] fTypes;
            internal string[] arr;

            public InputMap(HTMSensor<T> parent)
            {
                _parent = parent;
            }

            public object Get<TDecode>(string key)
            {
                int idx = _parent.indexFieldMap[key];
                FieldMetaType fmt = fTypes[idx];
                FieldMetaTypeHelper helper = new FieldMetaTypeHelper(key);
                return helper.DecodeType<TDecode>(fmt, arr[idx + 1], _parent.indexToEncoderMap[idx]);
            }

            public bool ContainsKey(string key)
            {
                return _parent.indexFieldMap.ContainsKey(key);
            }

            public void Add(string key, object value)
            {
                throw new NotImplementedException();
            }

            public bool Remove(string key)
            {
                throw new NotImplementedException();
            }

            public bool TryGetValue(string key, out object value)
            {
                throw new NotImplementedException();
            }

            public object this[string key]
            {
                get { throw new NotImplementedException(); }
                set { throw new NotImplementedException(); }
            }

            public ICollection<string> Keys { get; }
            public ICollection<object> Values { get; }

            public IEnumerator<KeyValuePair<string, object>> GetEnumerator()
            {
                throw new NotImplementedException();
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public void Add(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            public void Clear()
            {
                throw new NotImplementedException();
            }

            public bool Contains(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex)
            {
                throw new NotImplementedException();
            }

            public bool Remove(KeyValuePair<string, object> item)
            {
                throw new NotImplementedException();
            }

            public int Count
            {
                get { return arr.Length; }
            }
            public bool IsReadOnly { get; }
        }

        /// <summary>
        /// Returns the encoded output stream of the underlying <see cref="IStream{T}"/>'s encoder.
        /// </summary>
        /// <returns>the encoded output stream.</returns>
        public IBaseStream GetOutputStream()
        {
            if (IsTerminal())
            {
                throw new InvalidOperationException("Stream is already \"terminal\" (operated upon or empty)");
            }

            MultiEncoder encoder = GetEncoder();
            if (encoder == null)
            {
                throw new InvalidOperationException("setLocalParameters(Parameters) must be called before calling this method.");
            }

            // Protect outputStream formation and creation of "fan out" also make sure
            // that no other thread is trying to update the fan out lists
            IBaseStream retVal = null;
            try
            {
                System.Threading.Monitor.Enter(_criticalAccessLock);

                string[] fieldNames = GetFieldNames();
                FieldMetaType[] fieldTypes = GetFieldTypes();

                if (outputStream == null)
                {
                    if (!indexFieldMap.Any())
                    {
                        for (int i = 0; i < fieldNames.Length; i++)
                        {
                            indexFieldMap.Add(fieldNames[i], i);
                        }
                    }

                    // NOTE: The "inputMap" here is a special local implementation
                    //       of the "Map" interface, overridden so that we can access
                    //       the keys directly (without hashing). This map is only used
                    //       for this use case so it is ok to use this optimization as
                    //       a convenience.
                    if (inputMap == null)
                    {
                        inputMap = new InputMap(this);
                        inputMap.fTypes = fieldTypes;
                    }

                    bool isParallel = @delegate.GetInputStream().IsParallel();

                    output = new List<int[]>();

                    IMetaStream inputStream = @delegate.GetInputStream();
                    IBaseStream mappedInputStream;
                    if (inputStream.NeedsStringMapping())
                    {
                        mappedInputStream = inputStream.Map(i =>
                        {
                            //Debug.WriteLine(">> Calling HTM Sensor mapping");
                            string[] arr = i;
                            inputMap.arr = arr;
                            return Input(arr, fieldNames, fieldTypes, output, isParallel);
                        });
                        outputStream = mappedInputStream;
                    }
                    else
                    {
                        // TODO: use explorers and filters to map the image
                        mappedInputStream = inputStream.DoStreamMapping();
                        outputStream = mappedInputStream;
                    }
                }

                retVal = outputStream.CopyUntyped();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                System.Threading.Monitor.Exit(_criticalAccessLock);
            }

            return retVal;
        }

        public override bool EndOfStream()
        {
            //return ((ILocalEnumerator)mainIterator).HasNext(); // TODO: check that this is correct!
            if (outputStream != null)
            {
                return ((IStream<int[]>) outputStream).EndOfStream;
            }
            return @delegate.EndOfStream();
        }

        /**
         * Returns an array of field names in the order of column head occurrence.
         * 
         * @return
         */
        private string[] GetFieldNames()
        {
            //return (string[])header.GetFieldNames().ToArray(new string[header.GetFieldNames().Count]);
            return header.GetFieldNames().ToArray(); //.ToArray(new string[header.GetFieldNames().Count]);
        }

        /**
         * Returns an array of {@link FieldMetaType}s in the order of field occurrence.
         * @return
         */
        private FieldMetaType[] GetFieldTypes()
        {
            return header.GetFieldTypes().ToArray();
        }

        /**
         * <p>
         * Populates the specified outputStreamSource (List&lt;int[]&gt;) with an encoded
         * array of integers - using the specified field names and field types indicated.
         * </p><p>
         * This method process one single record, and is called iteratively to process an
         * input stream (internally by the {@link #getOutputStream()} method which will process
         * </p><p>
         * <b>WARNING:</b>  <em>When inserting data <b><em>MANUALLY</em></b>, you must remember that the first index
         * must be a sequence number, which means you may have to insert that by hand. Typically
         * this method is called internally where the underlying sensor does the sequencing automatically.</em>
         * </p>
         *  
         * @param arr                       The string array of field values           
         * @param fieldNames                The field names
         * @param fieldTypes                The field types
         * @param outputStreamSource        A list object to hold the encoded int[]
         * @param isParallel                Whether the underlying stream is parallel, if so this method
         *                                  executes a binary search for the proper insertion index. The {@link List}
         *                                  handed in should thus be a {@link LinkedList} for faster insertion.
         */
        private int[] Input(string[] arr, string[] fieldNames, FieldMetaType[] fieldTypes, List<int[]> outputStreamSource, bool isParallel)
        {
            ProcessHeader(arr);

            int[] encoding = encoder.Encode(inputMap);

            if (isParallel)
            {
                outputStreamSource[PadTo(int.Parse(arr[0]), outputStreamSource)] = encoding;
            }

            return encoding;
        }

        /**
         * Return the input mapping of field names to the last input
         * value for that field name. 
         * 
         * This method is typically used by client code which needs the 
         * input value for use with the {@link CLAClassifier}.
         * @return
         */
        public IDictionary<string, object> GetInputMap()
        {
            return inputMap;
        }

        // Used for setting the input map when we use the CLAModel
        public void AssignBasicInputMap(string[] rawData)
        {
            var fieldNames = GetFieldNames();
            if (!indexFieldMap.Any())
            {
                for (int i = 0; i < fieldNames.Length; i++)
                {
                    indexFieldMap.Add(fieldNames[i], i);
                }
            }

            inputMap = new InputMap(this);
            inputMap.fTypes = GetFieldTypes();
            inputMap.arr = rawData;
        }

        /**
         * Avoids the {@link IndexOutOfBoundsException} that can happen if inserting
         * into indexes which have gaps between insertion points.
         * 
         * @param i     the index whose lesser values are to have null inserted
         * @param l     the list to operate on.
         * @return      the index passed in (for fluent convenience at call site).
         */
        public static int PadTo<X>(int i, List<X> l)
        {
            for (int x = l.Count; x < i + 1; x++)
            {
                l.Add(default(X));
            }
            return i;
        }

        /**
         * Searches through the specified {@link MultiEncoder}'s previously configured 
         * encoders to find and return one that is of type {@link CoordinateEncoder} or
         * {@link GeospatialCoordinateEncoder}
         * 
         * @param enc   the containing {@code MultiEncoder}
         * @return
         */
        private Optional<IEncoder> GetCoordinateEncoder(MultiEncoder enc)
        {
            foreach (EncoderTuple t in enc.GetEncoders(enc))
            {
                if ((t.GetEncoder() is CoordinateEncoder) || (t.GetEncoder() is GeospatialCoordinateEncoder))
                {
                    return new Optional<IEncoder>(t.GetEncoder());  // Optional.of(t.getEncoder());
                }
            }

            return Optional<IEncoder>.Empty();
        }

        /**
         * Searches through the specified {@link MultiEncoder}'s previously configured 
         * encoders to find and return one that is of type {@link CategoryEncoder} or
         * {@link SDRCategoryEncoder}
         * 
         * @param enc   the containing {@code MultiEncoder}
         * @return
         */
        private Optional<IEncoder> GetCategoryEncoder(MultiEncoder enc)
        {
            foreach (EncoderTuple t in enc.GetEncoders(enc))
            {
                if ((t.GetEncoder() is CategoryEncoder) || (t.GetEncoder() is SDRCategoryEncoder))
                {
                    return new Optional<IEncoder>(t.GetEncoder()); //Optional.of(t.getEncoder());
                }
            }

            return Optional<IEncoder>.Empty();
        }

        /**
         * Searches through the specified {@link MultiEncoder}'s previously configured 
         * encoders to find and return one that is of type {@link DateEncoder}
         * 
         * @param enc   the containing {@code MultiEncoder}
         * @return
         */
        private Optional<DateEncoder> GetDateEncoder(MultiEncoder enc)
        {
            foreach (EncoderTuple t in enc.GetEncoders(enc))
            {
                if (t.GetEncoder() is DateEncoder)
                {
                    return new Optional<DateEncoder>(t.GetEncoder<DateEncoder>());//Optional.of((DateEncoder)t.GetEncoder());
                }
            }

            return Optional<DateEncoder>.Empty();
        }

        /**
         * Searches through the specified {@link MultiEncoder}'s previously configured 
         * encoders to find and return one that is of type {@link DateEncoder}
         * 
         * @param enc   the containing {@code MultiEncoder}
         * @return
         */
        private Optional<SDRPassThroughEncoder> GetSDRPassThroughEncoder(MultiEncoder enc)
        {
            foreach (EncoderTuple t in enc.GetEncoders(enc))
            {
                if (t.GetEncoder() is SDRPassThroughEncoder)
                {
                    return new Optional<SDRPassThroughEncoder>(t.GetEncoder<SDRPassThroughEncoder>()); // Optional.of((SDRPassThroughEncoder)t.getEncoder());
                }
            }

            return Optional<SDRPassThroughEncoder>.Empty();
        }

        /**
         * Searches through the specified {@link MultiEncoder}'s previously configured 
         * encoders to find and return one that is of type {@link ScalarEncoder},
         * {@link RandomDistributedScalarEncoder}, {@link AdaptiveScalarEncoder},
         * {@link LogEncoder} or {@link DeltaEncoder}.
         * 
         * @param enc   the containing {@code MultiEncoder}
         * @return
         */
        private Optional<IEncoder> GetNumberEncoder(MultiEncoder enc)
        {
            foreach (EncoderTuple t in enc.GetEncoders(enc))
            {
                if ((t.GetEncoder() is RandomDistributedScalarEncoder) ||
                     (t.GetEncoder() is ScalarEncoder) ||
                     (t.GetEncoder() is AdaptiveScalarEncoder) ||
                     (t.GetEncoder() is LogEncoder) ||
                     (t.GetEncoder() is DeltaEncoder))
                {

                    return new Optional<IEncoder>(t.GetEncoder()); // Optional.of(t.getEncoder());
                }
            }

            return Optional<IEncoder>.Empty();
        }

        /**
         * <p>
         * Returns a flag indicating whether the underlying stream has had
         * a terminal operation called on it, indicating that it can no longer
         * have operations built up on it.
         * </p><p>
         * The "terminal" flag if true does not indicate that the stream has reached
         * the end of its data, it just means that a terminating operation has been
         * invoked and that it can no longer support intermediate operation creation.
         * 
         * @return  true if terminal, false if not.
         */
        public bool IsTerminal()
        {
            return @delegate.GetInputStream().IsTerminal();
        }

        public ISensor GetDelegateSensor()
        {
            return @delegate;
        }

        /**
         * Returns the {@link Header} container for Sensor meta
         * information associated with the input characteristics and configured
         * behavior.
         * @return
         */
        public override IValueList GetMetaInfo()
        {
            return header;
        }

        /**
         * Sets the local parameters used to configure the major
         * algorithmic components.
         */
        public override void InitEncoder(Parameters p)
        {
            localParameters = p;

            Map<string, Map<string, object>> encoderSettings;
            if ((encoderSettings = (Map<string, Map<string, object>>)p.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP)) != null &&
                !encoder.GetEncoders().Any() &&
                    indexToEncoderMap == null)
            {

                InitEncoders(encoderSettings);
                MakeIndexEncoderMap();
            }
        }

        /**
         * Returns the global Parameters object
         */
        public Parameters GetLocalParameters()
        {
            return localParameters;
        }

        /**
         * For each entry, the header runs its processing to calculate
         * meta state of the current input (i.e. is learning, should reset etc.)
         * 
         * @param entry     an array containing the current input entry.
         */
        private void ProcessHeader(string[] entry)
        {
            header.Process(entry);
        }

        /**
         * Called internally to initialize this sensor's encoders
         * @param encoderSettings
         */
        private void InitEncoders(Map<string, Map<string, object>> encoderSettings)
        {
            if (encoder is MultiEncoder)
            {
                if (encoderSettings == null || !encoderSettings.Any())
                {
                    throw new ArgumentException("Cannot initialize this Sensor's MultiEncoder with a null settings");
                }
            }

            MultiEncoderAssembler.Assemble(encoder, encoderSettings);
        }

        /**
         * Returns this {@code HTMSensor}'s {@link MultiEncoder}
         * @return
         */
        public override MultiEncoder GetEncoder()
        {
            return (MultiEncoder)encoder;
        }

        public void SetEncoder(MultiEncoder encoder)
        {
            this.encoder = encoder;
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((indexFieldMap == null) ? 0 : indexFieldMap.GetHashCode());
            result = prime * result + ((sensorParams == null) ? 0 : Arrays.GetHashCode(sensorParams.GetKeys()));
            return result;
        }

        public override bool Equals(Object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            HTMSensor <T> other = (HTMSensor <T>)obj;
            if (indexFieldMap == null)
            {
                if (other.indexFieldMap != null)
                    return false;
            }
            else if (!indexFieldMap.Equals(other.indexFieldMap))
                return false;
            if (sensorParams == null)
            {
                if (other.sensorParams != null)
                    return false;
            }
            else if (!Arrays.AreEqual(sensorParams.GetKeys(), other.sensorParams.GetKeys()))
                return false;
            return true;
        }
    }
}