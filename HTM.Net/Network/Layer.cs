using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Threading;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Network
{
    /// <summary>
    /// <p>
    /// Implementation of the biological layer of a region in the neocortex. Here, a
    /// <see cref="ILayer"/> contains the physical structure (columns, cells, dendrites etc)
    /// shared by a sequence of algorithms which serve to implement the predictive
    /// inferencing present in this, the allegory to its biological equivalent.
    /// </p>
    /// <p>
    /// <b>COMPOSITION:</b> A Layer is constructed with <see cref="Parameters"/> which
    /// configure the behavior of most of the key algorithms a Layer may contain. It
    /// is also <em>optionally</em> constructed with each of the algorithms in turn.
    /// A Layer that includes an <see cref="IEncoder"/> is always initially configured with
    /// a <see cref="MultiEncoder"/>. The child encoders contained within the MultiEncoder
    /// are configured from the Map included with the specified Parameters, keyed by
    /// <see cref="Parameters.KEY.FIELD_ENCODING_MAP"/>.
    /// </p>
    /// <p>
    /// A field encoding map consists of one map for each of the fields to be
    /// encoded. Each individual map in the field encoding map contains the typical
    /// <see cref="IEncoder"/> parameters, plus a few "meta" parameters needed to describe
    /// the field and its data type as follows:
    /// </p>
    /// 
    /// <pre>
    ///      Map&lt;String, Map&lt;String, Object&gt;&gt; fieldEncodings = new HashMap&lt;&gt;();
    ///      
    ///      Map&lt;String, Object&gt; inner = new HashMap&lt;&gt;();
    ///      inner.put("n", n);
    ///      inner.put("w", w);
    ///      inner.put("minVal", min);
    ///      inner.put("maxVal", max);
    ///      inner.put("radius", radius);
    ///      inner.put("resolution", resolution);
    ///      inner.put("periodic", periodic);
    ///      inner.put("clip", clip);
    ///      inner.put("forced", forced);
    ///      // These are meta info to aid in Encoder construction
    ///      inner.put("fieldName", fieldName);
    ///      inner.put("fieldType", fieldType); (see <see cref="FieldMetaType"/> for type examples)
    ///      inner.put("encoderType", encoderType); (i.e. ScalarEncoder, SDRCategoryEncoder, DateEncoder...etc.)
    ///      
    ///      Map&lt;String, Object&gt; inner2 = new HashMap&lt;&gt;();
    ///      inner.put("n", n);
    ///      inner.put("w", w);
    ///      inner.put("minVal", min);
    ///      inner.put("maxVal", max);
    ///      inner.put("radius", radius);
    ///      inner.put("resolution", resolution);
    ///      inner.put("periodic", periodic);
    ///      inner.put("clip", clip);
    ///      inner.put("forced", forced);
    ///      // These are meta info to aid in Encoder construction
    ///      inner.put("fieldName", fieldName);
    ///      inner.put("fieldType", fieldType); (see <see cref="FieldMetaType"/> for type examples)
    ///      inner.put("encoderType", encoderType); (i.e. ScalarEncoder, SDRCategoryEncoder, DateEncoder...etc.)
    ///      
    ///      fieldEncodings.put("consumption", inner);  // Where "consumption" is an example field name (field name is "generic" in above code)
    ///      fieldEncodings.put("temperature", inner2);
    ///      
    ///      Parameters p = Parameters.GetDefaultParameters();
    ///      p.setParameterByKey(KEY.FIELD_ENCODING_MAP, fieldEncodings);
    /// </pre>
    /// 
    /// For an example of how to create the field encodings map in a reusable way,
    /// see NetworkTestHarness and its usage within the LayerTest class.
    /// 
    /// <p>
    /// The following is an example of Layer construction with everything included
    /// (i.e. Sensor, SpatialPooler, TemporalMemory, CLAClassifier, Anomaly
    /// (computer))
    /// 
    /// <pre>
    /// // See the test harness for more information
    /// Parameters p = NetworkTestHarness.GetParameters();
    /// 
    /// // How to merge (union) two <see cref="Parameters"/> objects. This one merges
    /// // the Encoder parameters into default parameters.
    /// p = p.union(NetworkTestHarness.GetHotGymTestEncoderParams());
    /// 
    /// // You can overwrite parameters as needed like this
    /// p.setParameterByKey(KEY.RANDOM, new MersenneTwister(42));
    /// p.setParameterByKey(KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
    /// p.setParameterByKey(KEY.POTENTIAL_RADIUS, 200);
    /// p.setParameterByKey(KEY.INHIBITION_RADIUS, 50);
    /// p.setParameterByKey(KEY.GLOBAL_INHIBITIONS, true);
    /// 
    /// Map&lt;String, Object&gt; params = new HashMap&lt;&gt;();
    /// params.put(KEY_MODE, Mode.PURE);
    /// params.put(KEY_WINDOW_SIZE, 3);
    /// params.put(KEY_USE_MOVING_AVG, true);
    /// Anomaly anomalyComputer = Anomaly.create(params);
    /// 
    /// Layer&lt;?&gt; l = Network.createLayer(&quot;TestLayer&quot;, p).alterParameter(KEY.AUTO_CLASSIFY, true).add(anomalyComputer).add(new TemporalMemory()).add(new SpatialPooler())
    ///                 .add(Sensor.create(FileSensor::create, SensorParams.create(Keys::path, &quot;&quot;, ResourceLocator.path(&quot;rec-center-hourly-small.csv&quot;))));
    /// </pre>
    /// 
    /// </summary>
    /// <typeparam name="T"></typeparam>
    [Serializable]
    public class Layer<T> : Persistable, ILayer
    {
        #region Fields

        [NonSerialized]
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Layer<T>));

        protected int numColumns;

        protected Network ParentNetwork;
        protected Region ParentRegion;

        protected Parameters Params;
        protected SensorParams SensorParams;
        protected Connections Connections;
        protected IHTMSensor Sensor;
        protected MultiEncoder Encoder;
        protected SpatialPooler SpatialPooler;
        protected TemporalMemory TemporalMemory;
        protected bool? AutoCreateClassifiers;
        protected Anomaly AnomalyComputer;

        [NonSerialized]
        protected ConcurrentQueue<IObserver<IInference>> _subscribers = new ConcurrentQueue<IObserver<IInference>>();
        [NonSerialized]
        protected Subject<object> Publisher = null;

        [NonSerialized]
        private IDisposable _subscription; //Subscription 
        [NonSerialized]
        private IObservable<IInference> _userObservable;

        protected IInference CurrentInference;

        protected FunctionFactory _factory;

        /// <summary>
        /// Used to track and document the # of records processed
        /// </summary>
        protected int _recordNum = -1;
        /// <summary>
        /// Keeps track of number of records to skip on restart
        /// </summary>
        protected int _skip = -1;

        protected string Name;

        private bool _isClosed;
        private bool _isHalted;
        private bool _isPostSerialized;
        private bool _isLearn = true;

        private Layer<IInference> _next;
        private Layer<IInference> _previous;

        [NonSerialized]
        protected List<IObserver<IInference>> _observers = new List<IObserver<IInference>>();
        [NonSerialized]
        private CheckPointOperator _checkPointOp;
        [NonSerialized]
        protected List<IObserver<byte[]>> _checkPointOpObservers = new List<IObserver<byte[]>>();

        /// <summary>
        /// Retains the order of added items - for use with interposed <see cref="IObservable{T}"/>
        /// </summary>
        private readonly List<object> _addedItems = new List<object>();

        /// <summary>
        /// Indicates whether there is a generic processing node entered
        /// </summary>
        private bool _hasGenericProcess;

        /// <summary>
        /// List of <see cref="Encoders"/> used when storing bucket information 
        /// see <see cref="DoEncoderBucketMapping(IInference, IDictionary{string, object})"/>
        /// </summary>
        private List<EncoderTuple> _encoderTuples;

        [NonSerialized]
        protected Map<Type, IObservable<ManualInput>> ObservableDispatch = new Map<Type, IObservable<ManualInput>>();

        /// <summary>
        /// Gets or sets the layer's current thread
        /// </summary>
        [NonSerialized]
        protected Task LayerThread;

        protected LayerMask AlgoContentMask = 0;

        #endregion

        #region Constructor(s)

        /// <summary>
        /// Creates a new <see cref="ILayer"/> using the <see cref="Network"/> level <see cref="Parameters"/>
        /// </summary>
        /// <param name="n">the parent <see cref="Network"/></param>
        public Layer(Network n)
            : this(n, n.GetParameters())
        {

        }

        /// <summary>
        /// Creates a new <see cref="ILayer"/> using the specified <see cref="Parameters"/>
        /// </summary>
        /// <param name="n">the parent <see cref="Network"/></param>
        /// <param name="p">the <see cref="Parameters"/> to use with this <see cref="ILayer"/></param>
        public Layer(Network n, Parameters p)
            : this("[Layer " + TimeUtils.CurrentTimeMillis() + "]", n, p)
        {

        }

        /// <summary>
        /// Creates a new <see cref="ILayer"/> using the specified <see cref="Parameters"/>
        /// </summary>
        /// <param name="name">the name identifier of this <see cref="ILayer"/></param>
        /// <param name="n">the parent <see cref="Network"/></param>
        /// <param name="p">the <see cref="Parameters"/> to use with this <see cref="ILayer"/></param>
        public Layer(string name, Network n, Parameters p)
        {
            Name = name;
            ParentNetwork = n;
            Params = p;

            Connections = new Connections();

            AutoCreateClassifiers = (bool)p.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY, false);

            _factory = new FunctionFactory(this);

            ObservableDispatch = CreateDispatchMap();
        }

        /// <summary>
        /// Creates a new <see cref="ILayer"/> initialized with the specified algorithmic components.
        /// </summary>
        /// <param name="params"> A <see cref="Parameters"/> object containing configurations for a SpatialPooler, TemporalMemory, and Encoder (all or none may be used).</param>
        /// <param name="e">(optional) The Network API only uses a <see cref="MultiEncoder"/> at the top level because of its ability to delegate to child encoders.</param>
        /// <param name="sp">(optional) <see cref="SpatialPooler"/></param>
        /// <param name="tm">(optional) <see cref="TemporalMemory"/></param>
        /// <param name="autoCreateClassifiers">(optional) Indicates that the <see cref="Parameters"/> object contains the configurations necessary to create the required encoders.</param>
        /// <param name="a">(optional) An <see cref="Anomaly"/> computer.</param>
        public Layer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm, bool? autoCreateClassifiers, Anomaly a)
        {
            // Make sure we have a valid parameters object
            if (@params == null)
            {
                throw new InvalidOperationException("No parameters specified.");
            }

            // Check to see if the Parameters include the encoder configuration.
            if (@params.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP) == null && e != null)
            {
                throw new InvalidOperationException("The passed in Parameters must contain a field encoding map " + "specified by org.numenta.nupic.Parameters.KEY.FIELD_ENCODING_MAP");
            }

            Params = @params;
            Encoder = e;
            SpatialPooler = sp;
            TemporalMemory = tm;
            AutoCreateClassifiers = autoCreateClassifiers;
            AnomalyComputer = a;

            Connections = new Connections();
            _factory = new FunctionFactory(this);

            ObservableDispatch = CreateDispatchMap();

            InitializeMask();

            if (Logger.IsDebugEnabled)
            {
                Logger.DebugFormat("Layer successfully created containing: {0}{1}{2}{3}{4}",
                    (Encoder == null ? "" : "MultiEncoder,"),
                    (SpatialPooler == null ? "" : "SpatialPooler,"),
                    (TemporalMemory == null ? "" : "TemporalMemory,"),
                    (autoCreateClassifiers == null ? "" : "Auto creating CLAClassifiers for each input field."),
                    (AnomalyComputer == null ? "" : "Anomaly"));
            }
        }

        #endregion

        #region Serialisation

        public override object PreSerialize()
        {
            _isPostSerialized = false;
            return this;
        }

        public override object PostDeSerialize()
        {
            RecreateSensors();

            FunctionFactory old = _factory;
            _factory = new FunctionFactory(this);
            _factory.Inference = (ManualInput)old.Inference.PostDeSerialize(old.Inference);

            _checkPointOpObservers = new List<IObserver<byte[]>>();

            if (Sensor != null)
            {
                Sensor.SetLocalParameters(this.Params);
                // Initialize encoders and recreate encoding index mapping.
                Sensor.PostDeSerialize();
            }
            else
            {
                // Dispatch functions (Observables) are transient & non-serializable so they must be rebuilt.
                ObservableDispatch = CreateDispatchMap();
                // Dispatch chain will not propagate unless it has subscribers.
                ParentNetwork.AddDummySubscriber();
            }
            // Flag which lets us know to skip or do certain setups during initialization.
            _isPostSerialized = true;

            _observers = new List<IObserver<IInference>>();

            return this;
        }

        #endregion

        #region Getters and Setters

        /// <summary>
        /// Sets the parent <see cref="Network"/> on this <see cref="Layer{T}"/>
        /// </summary>
        /// <param name="network"></param>
        public void SetNetwork(Network network)
        {
            ParentNetwork = network;
        }

        /// <summary>
        /// Returns the parent network
        /// </summary>
        /// <returns></returns>
        public Network GetNetwork()
        {
            return ParentNetwork;
        }

        /// <summary>
        /// Sets the parent region which contains this <see cref="Layer{T}"/>
        /// </summary>
        /// <param name="r"></param>
        public void SetRegion(Region r)
        {
            ParentRegion = r;
        }

        /// <summary>
        /// Returns the parent region
        /// </summary>
        /// <returns></returns>
        public Region GetRegion()
        {
            return ParentRegion;
        }

        /// <summary>
        /// Returns the spatial pooler if it's there
        /// </summary>
        /// <returns></returns>
        public SpatialPooler GetSpatialPooler()
        {
            return SpatialPooler;
        }

        /// <summary>
        /// Returns the Temporal Memory if it's there
        /// </summary>
        /// <returns></returns>
        public TemporalMemory GetTemporalMemory()
        {
            return TemporalMemory;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Finalizes the initialization in one method call so that side effect
        /// operations to share objects and other special initialization tasks can
        /// happen all at once in a central place for maintenance ease.
        /// </summary>
        /// <returns>Layer instance</returns>
        public virtual ILayer Close()
        {
            if (IsClosed())
            {
                Logger.Warn("Close called on Layer " + GetName() + " which is already closed.");
                return this;
            }

            Params.Apply(Connections);

            if (Sensor != null)
            {
                Encoder = Encoder ?? Sensor.GetEncoder();
                Sensor.InitEncoder(Params);
                Connections.SetNumInputs(Encoder.GetWidth());
                if (ParentNetwork != null && ParentRegion != null)
                {
                    ParentNetwork.SetSensorRegion(ParentRegion);

                    object supplier;
                    if ((supplier = Sensor.GetSensorParams().Get("ONSUB")) != null)
                    {
                        if (supplier is PublisherSupplier)
                        {
                            ((PublisherSupplier)supplier).SetNetwork(ParentNetwork);
                            ParentNetwork.SetPublisher(((PublisherSupplier)supplier).Get());
                        }
                    }
                }
            }

            // Create Encoder hierarchy from definitions & auto create classifiers
            // if specified
            if (Encoder != null)
            {
                if (Encoder.GetEncoders(Encoder) == null || Encoder.GetEncoders(Encoder).Count < 1)
                {
                    var fieldEncodingMap = Params.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP) as EncoderSettingsList;
                    if (fieldEncodingMap == null || fieldEncodingMap.Count < 1)
                    {
                        Logger.Error("No field encoding map found for specified MultiEncoder");
                        throw new InvalidOperationException("No field encoding map found for specified MultiEncoder");
                    }

                    Encoder.AddMultipleEncoders(fieldEncodingMap);
                }

                // Make the declared column dimensions match the actual input
                // dimensions retrieved from the encoder
                int product = 0, inputLength, columnLength;
                if (((inputLength = ((int[])Params.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS)).Length) != (columnLength = ((int[])Params.GetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS)).Length))
                            || Encoder.GetWidth() != (product = ArrayUtils.Product((int[])Params.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS))))
                {

                    Logger.Warn("The number of Input Dimensions (" + inputLength + ") != number of Column Dimensions " + "(" + columnLength + ") --OR-- Encoder width (" + Encoder.GetWidth()
                                    + ") != product of dimensions (" + product + ") -- now attempting to fix it.");

                    int[] inferredDims = InferInputDimensions(Encoder.GetWidth(), columnLength);
                    if (inferredDims != null && inferredDims.Length > 0 && Encoder.GetWidth() == ArrayUtils.Product(inferredDims))
                    {
                        Logger.Info("Input dimension fix successful!");
                        Logger.Info("Using calculated input dimensions: " + Arrays.ToString(inferredDims));
                    }

                    Params.SetInputDimensions(inferredDims);
                    Connections.SetInputDimensions(inferredDims);
                }

            }

            AutoCreateClassifiers = AutoCreateClassifiers != null && (AutoCreateClassifiers.GetValueOrDefault() | (bool)Params.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY));

            if (AutoCreateClassifiers != null && AutoCreateClassifiers.GetValueOrDefault()
                && (_factory.Inference.GetClassifiers() == null || _factory.Inference.GetClassifiers().Count < 1))
            {
                _factory.Inference.SetClassifiers(MakeClassifiers(Encoder == null ? ParentNetwork?.GetEncoder() : Encoder));

                // Note classifier addition by setting content mask
                AlgoContentMask |= LayerMask.ClaClassifier;
            }

            // We must adjust this Layer's inputDimensions to the size of the input
            // received from the previous Region's output vector.
            if (ParentRegion != null && ParentRegion.GetUpstreamRegion() != null)
            {
                int[] upstreamDims = new int[] { CalculateInputWidth() };
                Params.SetInputDimensions(upstreamDims);
                Connections.SetInputDimensions(upstreamDims);
            }
            else if (ParentRegion != null && ParentNetwork != null
                  && ParentRegion.Equals(ParentNetwork.GetSensorRegion()) && Encoder == null
                  && SpatialPooler != null)
            {
                ILayer curr = this;
                while ((curr = curr.GetPrevious()) != null)
                {
                    if (curr.GetEncoder() != null)
                    {
                        int[] dims = (int[])curr.GetParameters().GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS);
                        Params.SetInputDimensions(dims);
                        Connections.SetInputDimensions(dims);
                    }
                }
            }

            // Let the SpatialPooler initialize the matrix with its requirements
            if (SpatialPooler != null)
            {
                // The exact dimensions don't have to be the same but the number of
                // dimensions do!
                int inputLength, columnLength;
                if ((inputLength = ((int[])Params.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS)).Length) !=
                     (columnLength = ((int[])Params.GetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS)).Length))
                {

                    Logger.Error("The number of Input Dimensions (" + inputLength + ") is not same as the number of Column Dimensions " +
                        "(" + columnLength + ") in Parameters! - SpatialPooler not initialized!");

                    return this;
                }
                SpatialPooler.Init(Connections);
            }

            // Let the TemporalMemory initialize the matrix with its requirements
            if (TemporalMemory != null)
            {
                TemporalMemory.Init(Connections);
            }

            this.numColumns = Connections.GetNumColumns();

            _isClosed = true;

            Logger.Debug("Layer " + Name + " content initialize mask = " + AlgoContentMask); // Integer.toBinaryString(algo_content_mask)

            return this;
        }

        /// <summary>
        /// Called from <see cref="FunctionFactory.CreateSpatialFunc(SpatialPooler)"/> and from <see cref="Close()"/>
        /// to calculate the size of the input vector given the output source either being a <see cref="TemporalMemory"/>
        /// or a <see cref="SpatialPooler"/> - from this <see cref="Region"/> or a previous <see cref="Region"/>.
        /// </summary>
        /// <returns>the length of the input vector</returns>
        internal int CalculateInputWidth()
        {
            // If no previous Layer, check upstream region for its output layer's output.
            if (_previous == null)
            {
                if (ParentRegion.GetUpstreamRegion() != null)
                {
                    // Upstream region with TM
                    Layer<T> upstreamLayer = (Layer<T>) ParentRegion.GetUpstreamRegion().GetHead();
                    if ((upstreamLayer.GetMask() & LayerMask.TemporalMemory) == LayerMask.TemporalMemory)
                    {
                        var @out = (upstreamLayer.GetConnections().GetCellsPerColumn() *
                                    (upstreamLayer.GetConnections().GetMemory().GetMaxIndex() + 1));

                        return @out;
                    }
                    // Upstream region but no TM, so input is the upstream region's SP

                    return new SparseBinaryMatrix(upstreamLayer.GetConnections().GetColumnDimensions()).GetMaxIndex() + 1;
                }
                // No previous Layer, and no upstream region
                // layer contains a TM so compute by cells;
                if (HasTemporalMemory() && !HasSpatialPooler())
                {
                    return GetConnections().GetCellsPerColumn() * (GetConnections().GetMemory().GetMaxIndex() + 1);
                }
                // layer only contains a SP
                return Connections.GetNumInputs();
            }
            else
            {
                // There is a previous Layer and that layer contains a TM so compute by cells;
                if ((_previous.AlgoContentMask & LayerMask.TemporalMemory) == LayerMask.TemporalMemory)
                {
                    SparseBinaryMatrix matrix = new SparseBinaryMatrix(_previous.GetConnections().GetColumnDimensions());
                    return _previous.GetConnections().GetCellsPerColumn() * (matrix.GetMaxIndex() + 1);
                }
                // Previous Layer but it has no TM so use the previous' column output (from SP)
                return new SparseBinaryMatrix(_previous.GetConnections().GetColumnDimensions()).GetMaxIndex() + 1;
            }
        }

        /// <summary>
        /// For internal use only. Returns a flag indicating whether this <see cref="Layer{T}"/>
        /// contains a <see cref="Algorithms.TemporalMemory"/>
        /// </summary>
        internal bool HasTemporalMemory()
        {
            return (AlgoContentMask & LayerMask.TemporalMemory) == LayerMask.TemporalMemory;
        }

        /// <summary>
        /// For internal use only. Returns a flag indicating whether this <see cref="Layer{T}"/>
        /// contains a <see cref="Algorithms.SpatialPooler"/>
        /// </summary>
        internal bool HasSpatialPooler()
        {
            return (AlgoContentMask & LayerMask.SpatialPooler) == LayerMask.SpatialPooler;
        }

        /// <summary>
        /// Given an input field width and Spatial Pooler dimensionality; this method
        /// will return an array of dimension sizes whose number is equal to the
        /// number of column dimensions. The sum of the returned dimensions will be
        /// equal to the flat input field width specified.
        /// 
        /// This method should be called when a disparity in dimensionality between
        /// the input field and the number of column dimensions is detected.
        /// Otherwise if the input field dimensionality is correctly specified, this
        /// method should <b>not</b> be used.
        /// </summary>
        /// <param name="inputWidth">the flat input width of an <see cref="IEncoder"/>'s output or the vector used as input to the <see cref="SpatialPooler"/></param>
        /// <param name="numDims">a number specifying the number of column dimensions that should be returned.</param>
        /// <returns></returns>
        public int[] InferInputDimensions(int inputWidth, int numDims)
        {
            double flatSize = inputWidth;
            double numColDims = numDims;
            //double sliceArrangement = Math.Pow(flatSize, 1 / numColDims);
            //double remainder = sliceArrangement % (int)sliceArrangement;
            int[] retVal = new int[(int)numColDims];
            double log = Math.Log10(flatSize);
            double dimensions = numColDims;
            double sliceArrangement = Math.Round(Math.Pow(10, (log / dimensions)), 8, MidpointRounding.AwayFromZero);//MathContext.DECIMAL32);.doubleValue();
            double remainder = sliceArrangement % (int)sliceArrangement;
            if (remainder > double.Epsilon)
            {
                for (int i = 0; i < numColDims - 1; i++)
                    retVal[i] = 1;
                retVal[(int)numColDims - 1] = (int)flatSize;
            }
            else
            {
                for (int i = 0; i < numColDims; i++)
                    retVal[i] = (int)sliceArrangement;
            }

            return retVal;
        }

        /// <summary>
        /// Returns an <see cref="IObservable{IInference}"/> that can be subscribed to, or otherwise
        /// operated upon by another Observable or by an Observable chain.
        /// </summary>
        /// <returns>this <see cref="ILayer"/>'s output <see cref="IObservable{IInference}"/></returns>
        public IObservable<IInference> Observe()
        {
            // This will be called again after the Network is halted so we have to prepare
            // for rebuild of the Observer chain
            if (IsHalted())
            {
                ClearSubscriberObserverLists();
            }

            if (_userObservable == null)
            {
                _userObservable = Observable.Create<IInference>(t1 =>
                {
                    if (_observers == null)
                    {
                        _observers = new List<IObserver<IInference>>();
                    }
                    _observers.Add(t1);
                    return () => { }; // why is this?
                });
            }

            return _userObservable;
        }

        /// <summary>
        /// Called by the <see cref="ILayer"/> client to receive output <see cref="IInference"/>s from the configured algorithms.
        /// </summary>
        /// <param name="subscriber">a <see cref="IObserver{IInference}"/> to be notified as data is published.</param>
        /// <returns>A Subscription disposable</returns>
        public IDisposable Subscribe(IObserver<IInference> subscriber)
        {
            // This will be called again after the Network is halted so we have to prepare
            // for rebuild of the Observer chain
            if (IsHalted())
            {
                ClearSubscriberObserverLists();
            }

            if (subscriber == null)
            {
                throw new InvalidOperationException("Subscriber cannot be null.");
            }
            if (_subscribers == null)
            {
                _subscribers = new ConcurrentQueue<IObserver<IInference>>();
            }
            _subscribers.Enqueue(subscriber);

            return CreateSubscription(subscriber);
        }

        /// <summary>
        /// Allows the user to define the <see cref="Connections"/> object data structure
        /// to use. Or possibly to share connections between two <see cref="ILayer"/>s
        /// </summary>
        /// <param name="c">the <see cref="Connections"/> object to use.</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Using(Connections c)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }
            Connections = c;
            return this;
        }

        /// <summary>
        /// Allows the user to specify the <see cref="Parameters"/> object used by this
        /// <see cref="ILayer"/>. If the intent is to share Parameters across multiple
        /// Layers, it must be kept in mind that two Layers containing the same
        /// algorithm may require specification of locally different parameter
        /// settings. In this case, one could use
        /// <see cref="BaseLayer.AlterParameter(Parameters.KEY,object)"/> method to change a local setting
        /// without impacting the same setting in the source parameters object. This
        /// is made possible because the <see cref="BaseLayer.AlterParameter(Parameters.KEY,object)"/> method
        /// first makes a local copy of the <see cref="Parameters"/> object, then modifies
        /// the specified parameter.
        /// </summary>
        /// <param name="p">the <see cref="Parameters"/> to use in this <see cref="ILayer"/></param>
        /// <returns>this <see cref="ILayer"/></returns>
        public ILayer Using(Parameters p)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }
            Params = p;
            return this;
        }

        /// <summary>
        /// Adds an <see cref="HTMSensor{T}"/> to this <see cref="ILayer"/>. An HTMSensor is a regular
        /// <see cref="ISensor"/> (i.e. <see cref="FileSensor"/>, <see cref="URISensor"/>, or <see cref="ObservableSensor{T}"/>)
        /// which has had an <see cref="IEncoder"/> configured and added to it. HTMSensors are
        /// HTM Aware, where as regular Sensors have no knowledge of HTM requirements.
        /// </summary>
        /// <param name="sensor">the <see cref="HTMSensor{T}"/></param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(ISensor sensor)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }

            Sensor = (IHTMSensor)sensor;
            if (ParentNetwork != null && ParentRegion != null)
            {
                ParentNetwork.SetSensorRegion(ParentRegion);
                ParentNetwork.SetSensor(Sensor);
            }

            // Store the SensorParams for Sensor rebuild after deserialisation
            this.SensorParams = this.Sensor.GetSensorParams();

            return this;
        }

        /// <summary>
        /// Adds a <see cref="MultiEncoder"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="encoder">the added MultiEncoder</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(MultiEncoder encoder)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }

            Encoder = encoder;
            return this;
        }

        /// <summary>
        /// Adds a <see cref="SpatialPooler"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="sp">the added SpatialPooler</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(SpatialPooler sp)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }

            // Preserve addition order
            _addedItems.Add(sp);

            AlgoContentMask |= LayerMask.SpatialPooler;
            SpatialPooler = sp;
            return this;
        }

        /// <summary>
        /// Adds a <see cref="TemporalMemory"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="tm">the added TemporalMemory</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(TemporalMemory tm)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }

            // Preserve addition order
            _addedItems.Add(tm);

            AlgoContentMask |= LayerMask.TemporalMemory;
            TemporalMemory = tm;

            return this;
        }

        /// <summary>
        /// Adds an <see cref="Anomaly"/> computer to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="anomalyComputer">the Anomaly instance</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(Anomaly anomalyComputer)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }

            // Preserve addition order
            _addedItems.Add(anomalyComputer);

            AlgoContentMask |= LayerMask.AnomalyComputer;
            AnomalyComputer = anomalyComputer;
            return this;
        }

        /// <summary>
        /// Adds a "generic" processing node into this <see cref="ILayer"/>'s processing
        /// chain.
        /// 
        /// <em><b>NOTE: When adding a generic node, the order of calls to
        /// the addXXX() methods becomes crucially important. Make sure you 
        /// have added items in a valid order in your "fluent" add call declarations.</b></em>
        /// </summary>
        /// <param name="func">a <see cref="Func{ManualInput, ManualInput}"/> function to be performed at the point 
        /// of insertion within the <see cref="ILayer"/>'s declaration.</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer Add(Func<ManualInput, ManualInput> func)
        {
            if (_isClosed)
            {
                throw new LayerAlreadyClosedException();
            }
            if (func == null)
            {
                throw new ArgumentNullException(nameof(func), "Cannot add a null Function");
            }

            _hasGenericProcess = true;
            // Preserve addition order
            _addedItems.Add(func);
            return this;
        }

        /// <summary>
        /// Adds the ability to alter a given parameter in place during a fluent
        /// creation statement. This {@code Layer}'s {@link Parameters} object is
        /// copied and then the specified key/value pair are set on the internal
        /// copy. This call does not affect the original Parameters object so that
        /// local modifications may be made without having to reset them afterward
        /// for subsequent use with another network structure.
        /// </summary>
        /// <param name="key">The parameter key</param>
        /// <param name="value">The value of the parameter</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public ILayer AlterParameter(Parameters.KEY key, object value)
        {
            if (IsClosed())
            {
                throw new LayerAlreadyClosedException();
            }

            // Preserve any input dimensions that might have been set prior to this
            // in
            // previous layers
            int[] inputDims = (int[])Params.GetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS);

            Params = Params.Copy();
            Params.SetParameterByKey(key, value);
            Params.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, inputDims);

            if (key == Parameters.KEY.AUTO_CLASSIFY)
            {
                AutoCreateClassifiers = value != null && ((bool)value);
                // Note the addition of a classifier
                AlgoContentMask |= LayerMask.ClaClassifier;
            }
            return this;
        }

        /// <summary>
        /// Returns the configured <see cref="ISensor"/> if any exists in this <see cref="ILayer"/>, or null if one does not.
        /// </summary>
        /// <returns>any existing HTMSensor applied to this <see cref="ILayer"/></returns>
        public ISensor GetSensor()
        {
            return Sensor;
        }

        /// <summary>
        /// Returns the <see cref="Model.Connections"/> object being used by this <see cref="ILayer"/>
        /// </summary>
        /// <returns>this <see cref="ILayer"/>'s <see cref="Model.Connections"/></returns>
        public Connections GetConnections()
        {
            return Connections;
        }

        /// <summary>
        /// Processes a single element, sending the specified input up the configured
        /// chain of algorithms or components within this <see cref="ILayer"/>; resulting in
        /// any {@link Subscriber}s or {@link Observer}s being notified of results
        /// corresponding to the specified input (unless a <see cref="SpatialPooler"/>
        /// "primer delay" has been configured).
        /// 
        /// The first input to the Layer invokes a method to resolve the transformer
        /// at the bottom of the input chain, therefore the "type" (&lt;T&gt;) of the
        /// input cannot be changed once this method is called for the first time.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="t">the input object who's type is generic.</param>
        public virtual void Compute<TInput>(TInput t)
        {
            if (!_isClosed)
            {
                Close();
            }

            Increment();

            if (!DispatchCompleted())
            {
                CompleteDispatch(t);
            }

            Publisher.OnNext(t);
        }

        /// <summary>
        /// Stops the processing of this <see cref="ILayer"/>'s processing thread.
        /// </summary>
        public void Halt()
        {
            object supplier = null;
            if (Sensor != null && (supplier = Sensor.GetSensorParams().Get("ONSUB")) != null)
            {
                if (supplier is PublisherSupplier)
                {
                    ((PublisherSupplier)supplier).ClearSuppliedInstance();
                }
            }

            // Signal the Observer chain to complete
            if (LayerThread == null)
            {
                Publisher.OnCompleted();
                if (_next != null)
                {
                    _next.Halt();
                }
            }
            _isHalted = true;
        }

        /// <summary>
        /// Returns a flag indicating whether this layer's processing thread has been halted or not.
        /// </summary>
        public bool IsHalted()
        {
            return _isHalted;
        }

        /// <summary>
        /// Sets the learning mode.
        /// </summary>
        /// <param name="learningMode">true when in learning mode, false otherwise</param>
        public void SetLearn(bool learningMode)
        {
            _isLearn = learningMode;
        }

        /// <summary>
        /// Returns the learning mode setting.
        /// </summary>
        public bool IsLearn()
        {
            return _isLearn;
        }

        /// <summary>
        /// Completes the dispatch chain of algorithm <see cref="IObservable{T}"/>s with
        /// specialized <see cref="Transformer"/>s for each algorithm contained within this
        /// Layer. This method then starts the output stream processing of its
        /// <see cref="ISensor"/> in a separate Thread (if it exists) - logging this
        /// event.
        /// 
        /// Calling this method sets a flag on the underlying Sensor marking it as
        /// "Terminal" meaning that it cannot be restarted and its output stream
        /// cannot be accessed again.
        /// </summary>
        public void Start()
        {
            if (_isHalted)
            {
                Restart(true);
                return;
            }

            // Save boilerplate setup steps by automatically closing when start is
            // called.
            if (!_isClosed)
            {
                Close();
            }

            if (Sensor == null)
            {
                throw new InvalidOperationException("A sensor must be added when the mode is not Network.Mode.MANUAL");
            }

            Encoder = Encoder ?? Sensor.GetEncoder();

            try
            {
                CompleteDispatch(new int[] { });
            }
            catch (Exception e)
            {
                NotifyError(e);
            }

            StartLayerThread();

            Logger.Debug($"Start called on Layer thread {LayerThread.Id}");
        }

        /// <summary>
        /// Restarts this <see cref="ILayer"/>
        /// </summary>
        /// <param name="startAtIndex">flag indicating whether the Layer should be started and run from the previous save point or not.</param>
        public void Restart(bool startAtIndex)
        {
            _isHalted = false;

            if (!_isClosed)
            {
                Start();
            }
            else
            {
                if (Sensor == null)
                {
                    throw new InvalidOperationException("A sensor must be added when the mode is not Network.Mode.MANUAL");
                }

                // Re-init the Sensor only if we're halted and haven't already been initialized
                // following a deserialization.
                if (!_isPostSerialized)
                {
                    // Recreate the Sensor and its underlying Stream
                    RecreateSensors();
                }

                if (ParentNetwork != null)
                {
                    ParentNetwork.SetSensor(Sensor);
                }

                ObservableDispatch = CreateDispatchMap();

                this.Encoder = Encoder ?? Sensor.GetEncoder();

                _skip = startAtIndex ?
                    (Sensor.GetSensorParams().Get("ONSUB")) != null ? -1 : _recordNum :
                        (_recordNum = -1);

                try
                {
                    CompleteDispatch(new int[] { });
                }
                catch (Exception e)
                {
                    NotifyError(e);
                }

                StartLayerThread();

                Logger.Debug($"Re-Start called on Layer thread {LayerThread.Id}");
            }
        }

        /// <summary>
        /// Sets a pointer to the "next" Layer in this <see cref="ILayer"/>'s <see cref="IObservable{T}"/> sequence.
        /// </summary>
        /// <param name="l"></param>
        public void Next(Layer<IInference> l)
        {
            _next = l;
        }

        /// <summary>
        /// Returns the next Layer following this Layer in order of process flow.
        /// </summary>
        public ILayer GetNext()
        {
            return _next;
        }

        /// <summary>
        /// Sets a pointer to the "previous" Layer in this <see cref="ILayer"/>'s <see cref="IObservable{T}"/> sequence.
        /// </summary>
        /// <param name="l"></param>
        public void Previous(Layer<IInference> l)
        {
            _previous = l;
        }

        /// <summary>
        /// Returns the previous Layer preceding this Layer in order of process flow.
        /// </summary>
        public ILayer GetPrevious()
        {
            return _previous;
        }

        /// <summary>
        /// Returns a flag indicating whether this <see cref="BaseLayer"/> is configured 
        /// with a <see cref="ISensor"/> which requires starting up.
        /// </summary>
        /// <returns>true when a sensor is found</returns>
        public bool HasSensor()
        {
            return Sensor != null;
        }

        /// <summary>
        /// Returns the <see cref="Task"/> from which this <see cref="ILayer"/> is currently outputting data.
        /// 
        /// </summary>
        public Task GetLayerThread()
        {
            if (LayerThread != null)
            {
                return LayerThread;
            }

            throw new InvalidOperationException("No thread found? normally current thread is returned but this is an issue with 'Tasks'");
        }

        /// <summary>
        /// Returns the <see cref="Parameters"/> used to configure this layer.
        /// </summary>
        /// <returns></returns>
        public Parameters GetParameters()
        {
            return Params;
        }

        /// <summary>
        /// Returns the current predictive <see cref="Cell"/>s
        /// </summary>
        /// <returns>the binary vector representing the current prediction.</returns>
        public HashSet<Cell> GetPredictiveCells()
        {
            return CurrentInference.GetPredictiveCells();
        }

        /// <summary>
        /// Returns the previous predictive <see cref="Cell"/>s
        /// </summary>
        /// <returns>the binary vector representing the current prediction.</returns>
        public HashSet<Cell> GetPreviousPredictiveCells()
        {
            return CurrentInference.GetPreviousPredictiveCells();
        }

        /// <summary>
        /// Returns the current (dense) array of column indexes which represent
        /// columns which have become activated during the current input sequence
        /// from the SpatialPooler.
        /// </summary>
        /// <returns>the array of active column indexes</returns>
        public int[] GetFeedForwardActiveColumns()
        {
            return CurrentInference.GetFeedForwardActiveColumns();
        }

        /// <summary>
        /// Returns the <see cref="Cell"/>s activated in the <see cref="TemporalMemory"/> at time "t"
        /// </summary>
        /// <returns></returns>
        public HashSet<Cell> GetActiveCells()
        {
            return CurrentInference.GetActiveCells();
        }

        /// <summary>
        /// Returns the SpatialPooler column activations in sparse form (indexes of the on bits).
        /// </summary>
        public int[] GetFeedForwardSparseActives()
        {
            return CurrentInference.GetFeedForwardSparseActives();
        }

        /// <summary>
        /// Returns the<see cref="Model.Connections"/> object being used as the structural matrix and state.
        /// </summary>
        public Connections GetMemory()
        {
            return Connections;
        }

        /// <summary>
        /// Returns the count of records historically inputted into this
        /// </summary>
        /// <returns>the current record input count</returns>
        public int GetRecordNum()
        {
            return _recordNum;
        }

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> has had
        /// its <see cref="Close()"/> method called, or not.
        /// </summary>
        public bool IsClosed()
        {
            return _isClosed;
        }

        /// <summary>
        /// Resets the internal record count to zero
        /// </summary>
        public ILayer ResetRecordNum()
        {
            _recordNum = 0;
            return this;
        }

        /// <summary>
        /// Resets the <see cref="TemporalMemory"/> if it exists.
        /// </summary>
        public virtual void Reset()
        {
            if (TemporalMemory == null)
            {
                Logger.Debug("Attempt to reset Layer: " + GetName() + "without TemporalMemory");
            }
            else
            {
                TemporalMemory.Reset(Connections);
                ResetRecordNum();
            }
        }

        /// <summary>
        /// Increments the current record sequence number.
        /// </summary>
        public ILayer Increment()
        {
            if (_skip > -1)
            {
                --_skip;
            }
            else
            {
                ++_recordNum;
            }
            return this;
        }

        /// <summary>
        /// Sets the name and returns this <see cref="ILayer"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        public ILayer SetName(string name)
        {
            Name = name;
            return this;
        }

        /// <summary>
        /// Returns the String identifier of this <see cref="ILayer"/>
        /// </summary>
        /// <returns></returns>
        public string GetName()
        {
            return Name;
        }

        /// <summary>
        /// Returns the last computed <see cref="IInference"/> of this <see cref="Layer{T}"/>
        /// </summary>
        /// <returns>the last computed inference.</returns>
        public IInference GetInference()
        {
            return CurrentInference;
        }

        /// <summary>
        /// Returns the resident <see cref="MultiEncoder"/> or the encoder residing in this
        /// <see cref="ILayer"/>'s <see cref="ISensor"/>, if any.
        /// </summary>
        public MultiEncoder GetEncoder()
        {
            if (Encoder != null)
            {
                return Encoder;
            }
            if (HasSensor())
            {
                return Sensor.GetEncoder();
            }

            MultiEncoder e = ParentNetwork.GetEncoder();
            if (e != null)
            {
                return e;
            }

            return null;
        }

        /// <summary>
        /// Returns the values submitted to this <see cref="ILayer"/> in an array whose
        /// indexes correspond to the indexes of probabilities returned when calling
        /// <see cref="GetAllPredictions(string,int)"/>.
        /// </summary>
        /// <typeparam name="TV"></typeparam>
        /// <param name="field">The field name of the required prediction</param>
        /// <param name="step">The step for the required prediction</param>
        public TV[] GetAllValues<TV>(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            Classification<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No Classification exists for the specified field: {0}", field));
            }

            return c?.GetActualValues().Select(av => av != null ? (TV)av : default(TV)).ToArray();

            //return (V[])c.GetActualValues().Cast<V>().ToArray();
        }

        /// <summary>
        /// Returns a double[] containing a prediction confidence measure for each
        /// bucket (unique entry as determined by an encoder). In order to relate the
        /// probability to an actual value, call <see cref="GetAllValues{V}(string,int)"/>
        /// which returns an array containing the actual values submitted to this
        /// <see cref="ILayer"/> - the indexes of each probability will match the index of
        /// each actual value entered.
        /// </summary>
        /// <param name="field">The field name of the required prediction</param>
        /// <param name="step">The step for the required prediction</param>
        public double[] GetAllPredictions(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            Classification<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No Classification exists for the specified field: {0}", field));
            }

            return c?.GetStats(step);
        }

        /// <summary>
        /// Returns the value whose probability is calculated to be the highest for the specified field and step.
        /// </summary>
        /// <typeparam name="TK"></typeparam>
        /// <param name="field">The field name of the required prediction</param>
        /// <param name="step">The step for the required prediction</param>
        public TK GetMostProbableValue<TK>(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            Classification<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No Classification exists for the specified field: {0}", field));
            }

            return (TK)c?.GetMostProbableValue(step);
        }

        /// <summary>
        /// Returns the bucket index of the value with the highest calculated probability for the specified field and step.
        /// </summary>
        /// <param name="field">The field name of the required prediction</param>
        /// <param name="step">The step for the required prediction</param>
        public int GetMostProbableBucketIndex(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            Classification<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No Classification exists for the specified field: {0}", field));
            }

            return c.GetMostProbableBucketIndex(step);
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Notify all subscribers through the delegate that stream processing has been completed or halted.
        /// </summary>
        internal void NotifyComplete()
        {
            foreach (IObserver<IInference> o in _subscribers)
            {
                o.OnCompleted();
            }
            foreach (IObserver<IInference> o in _observers)
            {
                o.OnCompleted();
            }
            Publisher.OnCompleted();
        }

        /// <summary>
        /// Called internally to propagate the specified <see cref="Exception"/> up the network hierarchy
        /// </summary>
        /// <param name="e">the exception to notify users of</param>
        private void NotifyError(Exception e)
        {
            foreach (IObserver<IInference> o in _subscribers)
            {
                o.OnError(e);
            }
            foreach (IObserver<IInference> o in _observers)
            {
                o.OnError(e);
            }
            Publisher.OnError(e);
        }

        /// <summary>
        /// Returns the content mask used to indicate what algorithm contents this <see cref="BaseLayer"/> has. 
        /// This is used to determine whether the <see cref="IInference"/> object passed between layers should share values.
        /// </summary>
        /// <remarks>
        /// If any algorithms are repeated then <see cref="IInference"/>s will
        /// <em><b>NOT</b></em> be shared between layers. <see cref="Region"/>s
        /// <em><b>NEVER</b></em> share <see cref="IInference"/>s
        /// </remarks>
        public LayerMask GetMask()
        {
            return AlgoContentMask;
        }

        /// <summary>
        /// Initializes the algorithm content mask used for detection of repeated algorithms 
        /// among <see cref="Layer{T}"/>s in a <see cref="Region"/>
        /// See <see cref="GetMask()"/> for more information.
        /// </summary>
        private void InitializeMask()
        {
            AlgoContentMask |= (SpatialPooler == null ? 0 : LayerMask.SpatialPooler);
            AlgoContentMask |= (TemporalMemory == null ? 0 : LayerMask.TemporalMemory);
            AlgoContentMask |= (AutoCreateClassifiers == null || !AutoCreateClassifiers.GetValueOrDefault() ? LayerMask.None : LayerMask.ClaClassifier);
            AlgoContentMask |= (AnomalyComputer == null ? 0 : LayerMask.AnomalyComputer);
        }

        /// <summary>
        /// Returns a flag indicating whether we've connected the first observable in
        /// the sequence (which lazily does the input type of {T} to
        /// <see cref="IInference"/> transformation) to the Observables connecting the rest
        /// of the algorithm components.
        /// </summary>
        /// <returns>flag indicating all observables connected. True if so, false if not</returns>
        private bool DispatchCompleted()
        {
            return ObservableDispatch == null;
        }

        /// <summary>
        /// Connects the first observable which does the transformation of input
        /// types, to the rest of the sequence - then clears the helper map and sets it to null.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="t"></param>
        private void CompleteDispatch<TInput>(TInput t)
        {
            // Get the Input Transformer for the specified input type
            IObservable<ManualInput> sequence = ResolveObservableSequence(t);

            // If this Layer has a Sensor, map its encoder buckets
            sequence = MapEncoderBuckets(sequence);

            // Add the rest of the chain observables for the other added algorithms.
            sequence = FillInSequence(sequence);

            // All subscribers and observers are notified from a single delegate.
            if (_subscribers == null) _subscribers = new ConcurrentQueue<IObserver<IInference>>();
            _subscribers.Enqueue(GetDelegateObserver());
            _subscription = sequence.Subscribe(GetDelegateSubscriber());

            // The map of input types to transformers is no longer needed.
            ObservableDispatch.Clear();
            ObservableDispatch = null;

            // Handle global network sensor access.
            if (Sensor == null && ParentNetwork != null && ParentNetwork.IsTail(this))
            {
                Sensor = (IHTMSensor)ParentNetwork?.GetSensor();
            }
            else if (ParentNetwork != null && Sensor != null)
            {
                ParentNetwork.SetSensor(Sensor);
            }
        }

        /// <summary>
        /// We cannot create the <see cref="IObservable{T}"/> sequence all at once because the
        /// first step is to transform the input type to the type the rest of the
        /// sequence uses (<see cref="IObservable{IInference}"/>). This can only happen
        /// during the actual call to <see cref="Compute{T}(T)"/> which presents the
        /// input type - so we create a map of all types of expected inputs, and then
        /// connect the sequence at execution time; being careful to only incur the
        /// cost of sequence assembly on the first call to <see cref="Compute{T}(T)"/>.
        /// After the first call, we dispose of this map and its contents.
        /// </summary>
        /// <returns>the map of input types to <see cref="Transformer"/></returns>
        private Map<Type, IObservable<ManualInput>> CreateDispatchMap()
        {
            Map<Type, IObservable<ManualInput>> observableDispatch = new Map<Type, IObservable<ManualInput>>();

            Publisher = new Subject<object>(); //PublishSubject.create();

            observableDispatch.Add(typeof(IDictionary), _factory.CreateMultiMapFunc(Publisher.Select(t => t)));
            observableDispatch.Add(typeof(ManualInput), _factory.CreateManualInputFunc(Publisher.Select(t => t)));
            observableDispatch.Add(typeof(string[]), _factory.CreateEncoderFunc(Publisher.Select(t => t)));
            observableDispatch.Add(typeof(int[]), _factory.CreateVectorFunc(Publisher.Select(t => t)));
            observableDispatch.Add(typeof(ImageDefinition), _factory.CreateImageFunc(Publisher.Select(t => t)));

            return observableDispatch;
        }

        /// <summary>
        /// If this Layer has a Sensor, map its encoder's buckets
        /// </summary>
        /// <param name="sequence"></param>
        /// <returns></returns>
        private IObservable<ManualInput> MapEncoderBuckets(IObservable<ManualInput> sequence)
        {
            if (HasSensor())
            {
                if (GetSensor().GetMetaInfo().GetFieldTypes()
                    .Any(ft => ft == FieldMetaType.SparseArray || ft == FieldMetaType.DenseArray || ft == FieldMetaType.Coord || ft == FieldMetaType.Geo))
                {
                    if (AutoCreateClassifiers.GetValueOrDefault())
                    {
                        throw new InvalidOperationException("Cannot autoclassify with raw array input or " + " Coordinate based encoders... Remove auto classify setting.");
                    }
                    return sequence;
                }

                sequence = sequence.Select(m =>
                {
                    DoEncoderBucketMapping(m, ((IHTMSensor)GetSensor()).GetInputMap());
                    return m;
                });
            }

            return sequence;
        }

        /// <summary>
        /// This method is necessary to be able to retrieve the mapped <see cref="IObservable{ManualInput}"/> types 
        /// to input types or their subclasses if any.
        /// </summary>
        /// <typeparam name="TInput"></typeparam>
        /// <param name="t">the input type. The "expected" types are:
        /// <ul>
        ///     <li><see cref="Map{K,V}"/></li>
        ///     <li><see cref="ManualInput"/></li>
        ///     <li>String[]</li>
        ///     <li>int[]</li>
        /// </ul>
        /// or their subclasses.
        /// </param>
        /// <returns></returns>
        private IObservable<ManualInput> ResolveObservableSequence<TInput>(TInput t)
        {
            IObservable<ManualInput> sequenceStart = null;

            if (ObservableDispatch == null)
            {
                ObservableDispatch = CreateDispatchMap();
            }

            if (ObservableDispatch != null)
            {
                if (t is ManualInput)
                {
                    sequenceStart = ObservableDispatch[typeof(ManualInput)];
                }
                else if (t is IDictionary)
                {
                    sequenceStart = ObservableDispatch[typeof(IDictionary)];
                }
                else if (t.GetType().IsArray)
                {
                    if (t is string[])
                    {
                        sequenceStart = ObservableDispatch[typeof(string[])];
                    }
                    else if (t is int[])
                    {
                        sequenceStart = ObservableDispatch[typeof(int[])];
                    }
                }
                else if (t is ImageDefinition)
                {
                    sequenceStart = ObservableDispatch[typeof(ImageDefinition)];
                }
                else
                {
                    throw new ArgumentException("Input type is not mappable to IInference, there is no dispatcher defined. " + t.GetType().Name, nameof(t));
                }
            }

            // Insert skip observable operator if initializing with an advanced record number
            // (i.e. Serialized Network)
            if (_recordNum > 0 && _skip != -1)
            {
                sequenceStart = sequenceStart.Skip(_recordNum + 1);

                int? skipCount;
                if ((skipCount = (int?)Params.GetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY)) != null)
                {
                    // No need to "warm up" the SpatialPooler if we're deserializing an SP
                    // that has been running... However "skipCount - recordNum" is there so 
                    // we make sure the Network has run at least long enough to satisfy the 
                    // original requested "primer delay".
                    Params.SetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY, Math.Max(0, skipCount.GetValueOrDefault() - _recordNum));
                }
            }

            sequenceStart = sequenceStart.Where(m =>
            {
                if (_checkPointOpObservers.Any() && ParentNetwork != null)
                {
                    // Execute check point logic
                    DoCheckPoint();
                }

                return true;
            });

            return sequenceStart;
        }

        /// <summary>
        /// Executes the check point logic, handles the return of the serialized byte array
        /// by delegating the call to <see cref="IObserver{T}"/>where T = byte[] of all the currently queued
        /// Observers; then clears the list of Observers.
        /// </summary>
        private void DoCheckPoint()
        {
            byte[] bytes = ParentNetwork.InternalCheckPointOp();

            if (bytes != null)
            {
                Logger.Debug("Layer [" + GetName() + "] checkPointed file: " +
                    Persistence.Get().GetLastCheckPointFileName());
            }
            else
            {
                Logger.Debug("Layer [" + GetName() + "] checkPoint   F A I L E D   at: " + (new DateTime()));
            }

            foreach (IObserver<byte[]> o in _checkPointOpObservers)
            {
                o.OnNext(bytes);
                o.OnCompleted();
            }

            _checkPointOpObservers.Clear();
        }

        /// <summary>
        /// Stores a <see cref="NamedTuple"/> which contains the input values and bucket
        /// information - keyed to the encoded field name so that a classifier can
        /// retrieve it later on in the processing sequence.
        /// </summary>
        /// <param name="inference"></param>
        /// <param name="encoderInputMap"></param>
        private void DoEncoderBucketMapping(IInference inference, IDictionary<string, object> encoderInputMap)
        {
            if (_encoderTuples == null)
            {
                _encoderTuples = Encoder.GetEncoders(Encoder);
            }

            // Store the encoding
            int[] encoding = inference.GetEncoding();

            foreach (EncoderTuple t in _encoderTuples)
            {
                string name = t.GetName();
                string fieldName = t.GetFieldName();
                IEncoder e = t.GetEncoder();

                int bucketIdx;
                object o;
                if (encoderInputMap.GetType().FullName.Contains("InputMap"))
                {
                    var getMethod = encoderInputMap.GetType().GetMethod("Get");
                    getMethod = getMethod.MakeGenericMethod(e.GetEncoderType());
                    o = getMethod.Invoke(encoderInputMap, new object[] { fieldName }); // encoderInputMap[name]
                }
                else
                {
                    o = encoderInputMap[fieldName];
                }

                if (o is DateTime)
                {
                    bucketIdx = ((DateEncoder)e).GetBucketIndices((DateTime)o)[0];
                }
                else if (o is double)
                {
                    bucketIdx = e.GetBucketIndices((double)o)[0];
                }
                else if (o is int)
                {
                    bucketIdx = e.GetBucketIndices((int)o)[0];
                }
                else
                {
                    bucketIdx = e.GetBucketIndices((string)o)[0];
                }

                int offset = t.GetOffset();
                int[] tempArray = new int[e.GetWidth()];
                Array.Copy(encoding, offset, tempArray, 0, tempArray.Length);

                inference.GetClassifierInput().Add(name, new NamedTuple(new[] { "name", "inputValue", "bucketIdx", "encoding" }, name, o, bucketIdx, tempArray));
            }
        }

        /// <summary>
        /// Connects the <see cref="Transformer"/> to the rest of the <see cref="IObservable{T}"/> sequence.
        /// </summary>
        /// <param name="o">the Transformer part of the sequence.</param>
        /// <returns>the completed <see cref="IObservable{T}"/> sequence.</returns>
        private IObservable<ManualInput> FillInSequence(IObservable<ManualInput> o)
        {
            // Route to ordered dispatching if required.
            if (_hasGenericProcess)
            {
                return FillInOrderedSequence(o);
            }

            // Spatial Pooler config
            if (SpatialPooler != null)
            {
                int? skipCount;
                if ((skipCount = ((int?)Params.GetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY))) != null)
                {
                    o = o.Select(_factory.CreateSpatialFunc(SpatialPooler)).Skip(skipCount.GetValueOrDefault());
                    //o = o.map(factory.CreateSpatialFunc(spatialPooler)).skip(skipCount);
                }
                else
                {
                    o = o.Select(_factory.CreateSpatialFunc(SpatialPooler));
                }
            }

            // Temporal Memory config
            if (TemporalMemory != null)
            {
                o = o.Select(_factory.CreateTemporalFunc(TemporalMemory));
            }

            // Classifier config
            if (AutoCreateClassifiers != null && AutoCreateClassifiers.GetValueOrDefault())
            {
                o = o.Select(_factory.CreateClassifierFunc());
            }

            // Anomaly config
            if (AnomalyComputer != null)
            {
                o = o.Select(_factory.CreateAnomalyFunc(AnomalyComputer));
            }

            return o;
        }

        /// <summary>
        /// Connects <see cref="IObservable{T}"/> or <see cref="Transformer"/> emissions in the order they are declared.
        /// </summary>
        /// <param name="o">first <see cref="IObservable{T}"/> in sequence.</param>
        /// <returns></returns>
        private IObservable<ManualInput> FillInOrderedSequence(IObservable<ManualInput> o)
        {
            _addedItems.Reverse();

            foreach (object node in _addedItems)
            {
                var func = node as Func<ManualInput, ManualInput>;
                if (func != null) // Func<,>
                {
                    o = o.Select(func);
                }
                else if (node is SpatialPooler)
                {
                    int? skipCount;
                    if ((skipCount = ((int?)Params.GetParameterByKey(Parameters.KEY.SP_PRIMER_DELAY))) != null)
                    {
                        o = o.Select(_factory.CreateSpatialFunc(SpatialPooler)).Skip(skipCount.GetValueOrDefault());
                    }
                    else
                    {
                        o = o.Select(_factory.CreateSpatialFunc(SpatialPooler));
                    }
                }
                else if (node is TemporalMemory)
                {
                    o = o.Select(_factory.CreateTemporalFunc(TemporalMemory));
                }
            }

            // Classifier config
            if (AutoCreateClassifiers != null && AutoCreateClassifiers.GetValueOrDefault())
            {
                o = o.Select(_factory.CreateClassifierFunc());
            }

            // Anomaly config
            if (AnomalyComputer != null)
            {
                o = o.Select(_factory.CreateAnomalyFunc(AnomalyComputer));
            }

            return o;
        }

        /// <summary>
        /// Called internally to create a subscription on behalf of the specified <see cref="IObserver{IInference}"/>
        /// </summary>
        /// <param name="sub">the LayerObserver (subscriber).</param>
        /// <returns></returns>
        private IDisposable CreateSubscription(IObserver<IInference> sub)
        {
            ISubject<IInference> subject = new Subject<IInference>();
            return subject.Subscribe(sub);

            //return new Subscription()
            //{

            //    private Observer<Inference> observer = sub;

            //    @Override
            //    public void unsubscribe()
            //    {
            //        subscribers.remove(observer);
            //        if (subscribers.isEmpty())
            //        {
            //            subscription.unsubscribe();
            //        }
            //    }

            //    @Override
            //    public boolean isUnsubscribed()
            //    {
            //        return subscribers.contains(observer);
            //    }
            //};
        }

        /// <summary>
        /// Returns the <see cref="IObserver{IInference}"/>'s subscriber which delegates to all
        /// the <see cref="Layer{T}"/> subsribers.
        /// </summary>
        /// <returns></returns>
        private IObserver<IInference> GetDelegateSubscriber()
        {
            //return new Observer<IInference>()
            return Observer.Create<IInference>
                (
                    i =>
                    {
                        // OnNext
                        CurrentInference = i;
                        foreach (var o in _subscribers)
                        {
                            o.OnNext(i);
                        }
                    },
                    e =>
                    {
                        // OnError
                        foreach (var o in _subscribers)
                        {
                            o.OnError(e);
                        }
                    },
                    () =>
                    {
                        // OnCompleted
                        foreach (var o in _subscribers)
                        {
                            o.OnCompleted();
                        }
                    }
                );
        }

        /// <summary>
        /// Returns the <see cref="IObserver{IInference}"/>'s subscriber which delegates to all
        /// the <see cref="Layer{T}"/> subsribers.
        /// </summary>
        /// <returns></returns>
        protected IObserver<IInference> GetDelegateObserver()
        {
            return Observer.Create<IInference>
                (
                    i =>
                    {
                        // Next
                        CurrentInference = i;
                        foreach (var o in _observers)
                        {
                            o.OnNext(i);
                        }
                    },
                    e =>
                    {
                        // Error
                        foreach (var o in _observers)
                        {
                            o.OnError(e);
                            Console.WriteLine(e);
                        }
                    },
                    () =>
                    {
                        // Completed
                        foreach (var o in _observers)
                        {
                            o.OnCompleted();
                        }
                    }
                );
        }

        /// <summary>
        /// Clears the subscriber and observer lists so they can be rebuilt during restart or deserialization.
        /// </summary>
        private void ClearSubscriberObserverLists()
        {
            if (_observers == null) _observers = new List<IObserver<IInference>>();
            if (_subscribers == null) _subscribers = new ConcurrentQueue<IObserver<IInference>>();
            if (!_subscribers.IsEmpty)
            {
                // Clear the subscribers
                while (!_subscribers.IsEmpty)
                {
                    IObserver<IInference> obs;
                    _subscribers.TryDequeue(out obs);
                }
            }

            _userObservable = null;
        }

        /// <summary>
        /// Creates the <see cref="NamedTuple"/> of names to encoders used in the observable sequence.
        /// </summary>
        /// <param name="encoder"></param>
        /// <returns></returns>
        private NamedTuple MakeClassifiers(MultiEncoder encoder)
        {
            Type classificationType = (Type)Params.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY_TYPE);

            string[] names = new string[encoder.GetEncoders(encoder).Count];
            IClassifier[] ca = new IClassifier[names.Length];
            int i = 0;
            foreach (EncoderTuple et in encoder.GetEncoders(encoder))
            {
                names[i] = et.GetName();
                ca[i] = (IClassifier)Activator.CreateInstance(classificationType); //new CLAClassifier();
                ca[i].ApplyParameters(this.Params);
                i++;
            }
            var result = new NamedTuple(names, (object[])ca);
            return result;
        }

        /// <summary>
        /// Called internally to invoke the <see cref="SpatialPooler"/>
        /// </summary>
        /// <param name="input">the current input vector</param>
        /// <returns></returns>
        protected virtual int[] SpatialInput(int[] input)
        {
            if (input == null)
            {
                Logger.Info("Layer " + GetName() + " received null input");
            }
            else if (input.Length < 1)
            {
                Logger.Info("Layer " + GetName() + " received zero length bit vector");
                return input;
            }

            int[] activeColumns = new int[numColumns];
            SpatialPooler.Compute(Connections, input, activeColumns, _isLearn || (Sensor != null && Sensor.GetMetaInfo().IsLearn()));

            return activeColumns;
        }

        /// <summary>
        /// Called internally to invoke the <see cref="TemporalMemory"/>
        /// </summary>
        /// <param name="input"> the current input vector</param>
        /// <param name="mi">the current input inference container</param>
        /// <returns></returns>
        protected virtual int[] TemporalInput(int[] input, ManualInput mi)
        {
            ComputeCycle cc;
            if (Sensor != null)
            {
                if (Sensor.GetMetaInfo().IsReset())
                {
                    TemporalMemory.Reset(Connections);
                }

                cc = TemporalMemory.Compute(Connections, input, Sensor.GetMetaInfo().IsLearn());
            }
            else
            {
                cc = TemporalMemory.Compute(Connections, input, _isLearn);
            }

            // Store the predictive columns
            mi.SetPredictiveCells(cc.PredictiveCells());
            // Store activeCells
            mi.SetActiveCells(cc.ActiveCells());
            // Store the Compute Cycle
            mi.SetComputeCycle(cc);
            return SDR.AsCellIndices(cc.ActiveCells());
        }

        /// <summary>
        /// Starts this<see cref="ILayer"/>'s thread
        /// </summary>
        protected void StartLayerThread()
        {
            LayerThread = new Task(() =>
            {
                Logger.Debug("Layer [" + GetName() + "] started Sensor output stream processing.");

                //////////////////////////

                //var outputStream = (IBaseStream)Sensor.GetOutputStream();

                // Applies "terminal" function, at this point the input stream is "sealed"
                ((IStream<int[]>)Sensor.GetOutputStream())
                    .Filter(i =>
                    {
                        if (_isHalted)
                        {
                            NotifyComplete();
                            if (_next != null)
                            {
                                _next.Halt();
                            }
                            return false;
                        }
                        if (!Thread.CurrentThread.IsAlive)
                        {
                            NotifyError(new ApplicationException("Unknown exception whil filtering input"));
                        }
                        return true;
                    })
                    .ForEach(intArray =>
                    {
                        _factory.Inference.SetEncoding(intArray);

                        Compute(intArray);

                        

                    }, false);

                // Notify all downstream observers that the stream is closed
                if (Sensor.EndOfStream())
                {
                    NotifyComplete();
                }

                //int[] intArray = null;
                //object inputObject;
                //while (!outputStream.EndOfStream)
                //{
                //    inputObject = outputStream.ReadUntyped();
                //    if (inputObject is int[])
                //    {
                //        intArray = (int[])inputObject;
                //    }
                //    bool doComputation = false;
                //    bool computed = false;
                //    try
                //    {
                //        if (_isHalted)
                //        {
                //            NotifyComplete();
                //            if (_next != null)
                //            {
                //                _next.Halt();
                //            }
                //        }
                //        else
                //        {
                //            doComputation = true;
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e);
                //        NotifyError(new ApplicationException("Unknown Exception while filtering input", e));
                //        throw;
                //    }

                //    if (!doComputation) continue;

                //    try
                //    {

                //        //Debug.WriteLine("Computing in the foreach loop: " + Arrays.ToString(intArray));
                //        if (intArray != null) _factory.Inference.SetEncoding(intArray);

                //        Compute(intArray);
                //        computed = true;

                //        // Notify all downstream observers that the stream is closed
                //        if (!Sensor.EndOfStream())
                //        {
                //            NotifyComplete();
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e);
                //        if (Debugger.IsAttached) Debugger.Break();
                //        NotifyError(e);
                //    }

                //    if (!computed)
                //    {
                //        // Wait a little while, because new work can come
                //        Thread.Sleep(5000);
                //    }
                //}

                Debug.WriteLine("#> Layer [" + GetName() + "] thread has exited.");
                //////////////////////////

                // Applies "terminal" function, at this point the input stream
                // is "sealed".
                //sensor.GetOutputStream().Filter(i =>
                //{
                //    try
                //    {
                //        if (isHalted)
                //        {
                //            NotifyComplete();
                //            if (next != null)
                //            {
                //                next.Halt();
                //            }
                //            return false;
                //        }
                //        return true;
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e);
                //        NotifyError(new ApplicationException("Unknown Exception while filtering input", e));
                //        throw;
                //    }
                //}).ForEach(intArray =>
                //{
                //    try
                //    {

                //        //Debug.WriteLine("Computing in the foreach loop: " + Arrays.ToString(intArray));
                //        factory.inference.Encoding(intArray);

                //        //T computeInput = (T)Convert.ChangeType(intArray, typeof(int[]));

                //        Compute(intArray);

                //        // Notify all downstream observers that the stream is closed
                //        if (!sensor.HasNext())
                //        {
                //            NotifyComplete();
                //        }
                //    }
                //    catch (Exception e)
                //    {
                //        Console.WriteLine(e);
                //        if (Debugger.IsAttached) Debugger.Break();
                //        NotifyError(e);
                //    }

                //});
            }, TaskCreationOptions.LongRunning);

            //LayerThread.Name = "Sensor Layer [" + GetName() + "] Thread";
            LayerThread.Start();
            Logger.Debug($"Start called on Layer thread {LayerThread}");
        }

        /// <summary>
        /// Returns an <see cref="IObservable{T}"/> operator that when subscribed to, invokes an operation
        /// that stores the state of this {@code Network} while keeping the Network up and running.
        /// The Network will be stored at the pre-configured location (in binary form only, not JSON).
        /// </summary>
        /// <returns>the <see cref="ICheckPointOp{T}"/> operator</returns>
        public ICheckPointOp<byte[]> GetCheckPointOperator()
        {
            if (_checkPointOp == null)
            {
                _checkPointOp = new CheckPointOperator(this);
            }
            return (ICheckPointOp<byte[]>)_checkPointOp;
        }

        /// <summary>
        /// Re-initializes the <see cref="IHTMSensor"/> following deserialization or restart after halt.
        /// </summary>
        private void RecreateSensors()
        {
            if (Sensor != null)
            {
                // Recreate the Sensor and its underlying Stream
                Type sensorKlass = Sensor.GetType();
                if (sensorKlass.FullName.IndexOf("File") != -1)
                {
                    Object path = Sensor.GetSensorParams().Get("PATH");
                    Sensor = (IHTMSensor)Sensor<FileSensor>.Create(
                         FileSensor.Create, SensorParams.Create(SensorParams.Keys.Path, "", path));
                }
                else if (sensorKlass.FullName.IndexOf("Observ") != -1)
                {
                    Object supplierOfObservable = Sensor.GetSensorParams().Get("ONSUB");
                    Sensor = (IHTMSensor)Sensor<ObservableSensor<string[]>>.Create(
                         ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, "", supplierOfObservable));
                }
                else if (sensorKlass.FullName.IndexOf("URI") != -1)
                {
                    //    Object url = Sensor.GetSensorParams().Get("URI");
                    //    Sensor = (IHTMSensor)Sensor.Create(
                    //         UriSensor.Create, SensorParams.Create(SensorParams.Keys.Uri, "", url));
                    throw new NotImplementedException("Need to code the URI sensor first");
                }
            }
        }

        #endregion

        #region Nested Types

        //////////////////////////////////////////////////////////////
        //   Inner Class Definition for CheckPointer (Observable)   //
        //////////////////////////////////////////////////////////////

        /**
         * <p>
         * Implementation of the CheckPointOp interface which serves to checkpoint
         * and register a listener at the same time. The {@link rx.Observer} will be
         * notified with the byte array of the {@link Network} being serialized.
         * </p><p>
         * The layer thread automatically tests for the list of observers to 
         * contain > 0 elements, which indicates a check point operation should
         * be executed.
         * </p>
         * 
         * @param <T>       {@link rx.Observer}'s return type
         */
        internal class CheckPointOperator : ICheckPointOp<byte[]>
        {
            [NonSerialized]
            private IObservable<byte[]> _instance;

            internal CheckPointOperator(ILayer l)
            //: this()
            {
                _instance = Observable.Create<byte[]>(o =>
                {
                    if (l.GetLayerThread() != null)
                    {
                        // The layer thread automatically tests for the list of observers to 
                        // contain > 0 elements, which indicates a check point operation should
                        // be executed.
                        ((Layer<T>)l)._checkPointOpObservers.Add(o);
                    }
                    else
                    {
                        ((Layer<T>)l).DoCheckPoint();
                    }
                    return Observable.Empty<byte[]>().Subscribe();
                });

            }

            /**
             * Constructs this {@code CheckPointOperator}
             * @param f     a subscriber function
             */
            //protected CheckPointOperator(rx.Observable.OnSubscribe<T> f)
            //{
            //    super(f);
            //}

            /**
             * Queues the specified {@link rx.Observer} for notification upon
             * completion of a check point operation.
             */
            public IDisposable CheckPoint(IObserver<byte[]> t)
            {
                return _instance.Subscribe(t);
            }
        }

        //////////////////////////////////////////////////////////////
        //        Inner Class Definition Transformer Example        //
        //////////////////////////////////////////////////////////////

        /// <summary>
        /// Factory which returns an <see cref="IObservable{T}"/> capable of transforming known
        /// input formats to the universal format object passed between all
        /// Observables in the Observable chains making up the processing steps
        /// involving HTM algorithms.
        /// 
        /// The <see cref="Transformer"/> implementations are used to transform various
        /// inputs into a <see cref="ManualInput"/>, and thus are used at the beginning of
        /// any Observable chain; each succeding Observable in a given chain would
        /// then communicate via passed ManualInputs or <see cref="IInference"/>s (which are
        /// the same thing).
        /// 
        /// <see cref="Layer{T}.CompleteDispatch{V}(V)"/>
        /// <see cref="Layer{T}.ResolveObservableSequence{V}(V)"/>
        /// <see cref="Layer{T}.FillInSequence(System.IObservable{HTM.Net.Network.ManualInput})"/>
        /// </summary>
        [Serializable]
        public class FunctionFactory
        {
            internal Layer<T> Layer { get; set; }

            public ManualInput Inference = new ManualInput();

            public FunctionFactory(Layer<T> layer)
            {
                Layer = layer;
            }

            //////////////////////////////////////////////////////////////////////////////
            //                              TRANSFORMERS                                //
            //////////////////////////////////////////////////////////////////////////////
            /**
             * WARNING: UNIMPLEMENTED
             * 
             * <p>
             * Emits an <see cref="IObservable{T}"/> which is transformed from a String[] of
             * csv input to one that emits <see cref="IInference"/>s.
             * </p>
             * <p>
             * This class currently lacks the implementation of csv parsing into
             * distinct Object types - which is necessary to compose a "multi-map"
             * necessary to input data into the <see cref="MultiEncoder"/> necessary to
             * encode multiple field inputs.
             * </p>
             * <p>
             * TODO: Implement later
             * </p>
             */
            class String2Inference : Transformer<string[], ManualInput>
            {
                private FunctionFactory FunctionFactory { get; }

                public String2Inference(FunctionFactory functionFactory)
                {
                    FunctionFactory = functionFactory;
                }

                protected override ManualInput DoMapping(string[] t1)
                {
                    ////////////////////////////////////////////////////////////////////////
                    //                  Do transformative work here                       //
                    //                                                                    //
                    // In "real life", this will send data through the MultiEncoder       //
                    // Below is simply a faked out place holder...                        //
                    ////////////////////////////////////////////////////////////////////////
                    int[] sdr = new int[t1.Length];
                    for (int i = 0; i < sdr.Length; i++)
                    {
                        sdr[i] = int.Parse(t1[i]);
                    }

                    return FunctionFactory.Inference.SetRecordNum(FunctionFactory.Layer.GetRecordNum()).SetSdr(sdr).SetLayerInput(sdr);
                }

                //    public override Observable<ManualInput> call(Observable<String[]> t1)
                //    {
                //        return t1.map(new Func<String[], ManualInput>() {

                //        @Override
                //        public ManualInput call(String[] t1)
                //    {

                //        ////////////////////////////////////////////////////////////////////////
                //        //                  Do transformative work here                       //
                //        //                                                                    //
                //        // In "real life", this will send data through the MultiEncoder       //
                //        // Below is simply a faked out place holder...                        //
                //        ////////////////////////////////////////////////////////////////////////
                //        int[] sdr = new int[t1.length];
                //        for (int i = 0; i < sdr.length; i++)
                //        {
                //            sdr[i] = Integer.parseInt(t1[i]);
                //        }

                //        return inference.recordNum(GetRecordNum()).sdr(sdr).layerInput(sdr);
                //    }
                //});
                //}
            }

            /**
             * <p>
             * Emits an <see cref="IObservable{T}"/> which is transformed from a Map input
             * type to one that emits <see cref="IInference"/>s.
             * </p>
             * <p>
             * This <see cref="Transformer"/> is used when the input to a given
             * {@link Layer} is a map of fields to input Objects. It is typically
             * used when a Layer is configured with a <see cref="MultiEncoder"/> (which is
             * the only encoder type that may be contained within a Layer, because
             * it can be used to hold any combination of encoders required.).
             * </p>
             * 
             */
            class Map2Inference : Transformer<IDictionary<string, object>, ManualInput>
            {
                private FunctionFactory FunctionFactory { get; }

                public Map2Inference(FunctionFactory functionFactory)
                {
                    FunctionFactory = functionFactory;
                }

                protected override ManualInput DoMapping(IDictionary<string, object> input)
                {
                    var layer = FunctionFactory.Layer;
                    // Indicates a value that skips the encoding step
                    if (layer._encoderTuples == null)
                    {
                        layer._encoderTuples = layer.Encoder.GetEncoders(layer.Encoder);
                    }
                    // Store the encoding
                    int[] encoding = layer.Encoder.Encode(input);
                    FunctionFactory.Inference.SetSdr(encoding).SetEncoding(encoding);
                    layer.DoEncoderBucketMapping(FunctionFactory.Inference, input);
                    return FunctionFactory.Inference.SetRecordNum(layer.GetRecordNum()).SetLayerInput(input);
                }

                //public override ObservableCollection<ManualInput> Call(ObservableCollection<IDictionary> t1)
                //{
                //        return t1.Select(new Func<Map, ManualInput>()
                //        {

                //            public override ManualInput call(Map t1)
                //    {
                //        if (encoderTuples == null)
                //        {
                //            encoderTuples = encoder.GetEncoders(encoder);
                //        })

                //                // Store the encoding
                //                int[] encoding = encoder.encode(t1);
                //        inference.sdr(encoding).encoding(encoding);

                //        DoEncoderBucketMapping(inference, t1);

                //        return inference.recordNum(GetRecordNum()).layerInput(t1);
                //    }
                //});
                //}
            }

            /**
             * <p>
             * Emits an <see cref="IObservable{T}"/> which is transformed from a binary vector
             * input type to one that emits <see cref="IInference"/>s.
             * </p>
             * <p>
             * This type is used when bypassing an encoder and possibly any other
             * algorithm usually connected in a sequence of algorithms within a
             * {@link Layer}
             * </p>
             */
            class Vector2Inference : Transformer<int[], ManualInput>
            {
                private FunctionFactory FunctionFactory { get; }

                public Vector2Inference(FunctionFactory functionFactory)
                {
                    FunctionFactory = functionFactory;
                }

                protected override ManualInput DoMapping(int[] input)
                {
                    // Indicates a value that skips the encoding step
                    return FunctionFactory.Inference
                        .SetRecordNum(FunctionFactory.Layer.GetRecordNum())
                        .SetSdr(input)
                        .SetLayerInput(input);
                }

                //    public Observable<ManualInput> call(Observable<int[]> t1)
                //    {
                //        return t1.map(new Func<int[], ManualInput>() {

                //                @Override
                //                public ManualInput call(int[] t1)
                //    {
                //        // Indicates a value that skips the encoding step
                //        return inference.recordNum(GetRecordNum()).sdr(t1).layerInput(t1);
                //    }
                //});
                //        }
            }

            /// <summary>
            /// Transforms image definition to manual input
            /// </summary>
            class ImageDefinition2Inference : Transformer<ImageDefinition, ManualInput>
            {
                private FunctionFactory FunctionFactory { get; }

                public ImageDefinition2Inference(FunctionFactory functionFactory)
                {
                    FunctionFactory = functionFactory;
                }

                protected override ManualInput DoMapping(ImageDefinition input)
                {
                    // Indicates a value that skips the encoding step
                    return FunctionFactory.Inference
                        .SetRecordNum(FunctionFactory.Layer.GetRecordNum())
                        .SetSdr(input.InputVector)
                        // TODO: find where the category in is set
                        .SetClassifierInput(new Map<string, object>
                        {
                            { "categoryIn", input.CategoryIndices }
                        })
                        .SetLayerInput(input.InputVector);
                }
            }

            /**
             * Emits an <see cref="IObservable{T}"/> which copies an Inference input to the
             * output, storing relevant information in this layer's inference object
             * along the way.
             */
            class Copy2Inference : Transformer<ManualInput, ManualInput>
            {
                private NamedTuple _swap;
                private bool _swapped;

                private FunctionFactory FunctionFactory { get; }

                public Copy2Inference(FunctionFactory functionFactory)
                {
                    FunctionFactory = functionFactory;
                }

                protected override ManualInput DoMapping(ManualInput t1)
                {
                    // Inference is shared along entire network
                    if (!_swapped)
                    {
                        _swap = FunctionFactory.Inference.GetClassifiers();
                        FunctionFactory.Inference = t1;
                        FunctionFactory.Inference.SetClassifiers(_swap);
                        _swapped = true;
                    }
                    // Indicates a value that skips the encoding step
                    return FunctionFactory.Inference
                        .SetRecordNum(FunctionFactory.Layer.GetRecordNum())
                        .SetSdr(t1.GetSdr())
                        .SetRecordNum(t1.GetRecordNum())
                        .SetLayerInput(t1);
                }

                //public Observable<ManualInput> call(Observable<ManualInput> t1)
                //{
                //    return t1.map(new Func<ManualInput, ManualInput>() {

                //    NamedTuple swap;
                //    boolean swapped;

                //    @Override
                //            public ManualInput call(ManualInput t1)
                //{
                //    // Inference is shared along entire network
                //    if (!swapped)
                //    {
                //        swap = inference.GetClassifiers();
                //        inference = t1;
                //        inference.classifiers(swap);
                //        swapped = true;
                //    }
                //    // Indicates a value that skips the encoding step
                //    return inference.recordNum(GetRecordNum()).sdr(t1.GetSDR()).recordNum(t1.GetRecordNum()).layerInput(t1);
                //}
                //    });
                //}
            }

            //////////////////////////////////////////////////////////////////////////////
            //                    INPUT TRANSFORMATION FUNCTIONS                        //
            //////////////////////////////////////////////////////////////////////////////
            public IObservable<ManualInput> CreateEncoderFunc(IObservable<object> input)
            {
                var transformer = new String2Inference(this);
                return transformer.TransformFiltered(input, t => t is string);
                //return @in.ofType(typeof(String[])).Compose(new String2Inference());
            }

            public IObservable<ManualInput> CreateMultiMapFunc(IObservable<object> input)
            {
                var transformer = new Map2Inference(this);
                return transformer.TransformFiltered(input, t => t is IDictionary<string, object>);

                //return @in.ofType(typeof(Map)).Compose(new Map2Inference());
            }

            public IObservable<ManualInput> CreateVectorFunc(IObservable<object> input)
            {
                var transformer = new Vector2Inference(this);
                return transformer.TransformFiltered(input, t => t is int[]);
                //return @in.ofType(typeof(int[])).compose(new Vector2Inference());
            }

            public IObservable<ManualInput> CreateImageFunc(IObservable<object> input)
            {
                var transformer = new ImageDefinition2Inference(this);
                return transformer.TransformFiltered(input, t => t is ImageDefinition);
                //return @in.ofType(typeof(int[])).compose(new Vector2Inference());
            }

            public IObservable<ManualInput> CreateManualInputFunc(IObservable<object> input)
            {
                var transformer = new Copy2Inference(this);
                return transformer.TransformFiltered(input, t => t is ManualInput);
                //return @in.ofType(typeof(ManualInput)).compose(new Copy2Inference());
            }

            //////////////////////////////////////////////////////////////////////////////
            //                   OBSERVABLE COMPONENT CREATION METHODS                  //
            //////////////////////////////////////////////////////////////////////////////
            public Func<ManualInput, ManualInput> CreateSpatialFunc(SpatialPooler sp)
            {
                return t1 =>
                {
                    int inputWidth = -1;
                    if (t1.GetSdr().Length > 0 && ArrayUtils.IsSparse(t1.GetSdr()))
                    {
                        if (inputWidth == -1)
                        {
                            inputWidth = Layer.CalculateInputWidth();
                        }
                        t1.SetSdr(ArrayUtils.AsDense(t1.GetSdr(), inputWidth));
                    }

                    return t1.SetSdr(Layer.SpatialInput(t1.GetSdr())).SetFeedForwardActiveColumns(t1.GetSdr());
                };
                //return new Func<ManualInput, ManualInput>() {

                //            int inputWidth = -1;

                //            public ManualInput call(ManualInput t1)
                //            {
                //                if (t1.GetSDR().length > 0 && ArrayUtils.isSparse(t1.GetSDR()))
                //                {
                //                    if (inputWidth == -1)
                //                    {
                //                        inputWidth = calculateInputWidth();
                //                    }
                //                    t1.sdr(ArrayUtils.asDense(t1.GetSDR(), inputWidth));
                //                }

                //                return t1.sdr(spatialInput(t1.GetSDR())).feedForwardActiveColumns(t1.GetSDR());
                //            }
                //        };
            }

            public Func<ManualInput, ManualInput> CreateTemporalFunc(TemporalMemory tm)
            {
                return t1 =>
                {
                    int[] sdr = t1.GetSdr();
                    if (!ArrayUtils.IsSparse(t1.GetSdr()))
                    {
                        // Set on Layer, then set sparse actives as the sdr,
                        // then set on Manual Input (t1)
                        sdr = ArrayUtils.Where(sdr, ArrayUtils.WHERE_1);
                        t1 = t1.SetSdr(sdr).SetFeedForwardSparseActives(sdr);
                    }
                    return t1.SetSdr(Layer.TemporalInput(sdr, t1));
                };
            }

            public Func<ManualInput, ManualInput> CreateClassifierFunc()
            {
                object bucketIdx = null, actValue = null;
                IDictionary<string, object> inputMap = new CustomGetDictionary<string, object>(k => k.Equals("bucketIdx") ? bucketIdx : actValue);
                return t1 =>
                {
                    Map<string, object> ci = t1.GetClassifierInput();
                    int recordNum = Layer.GetRecordNum();
                    foreach (string key in ci.Keys)
                    {
                        NamedTuple inputs = (NamedTuple)ci[key];
                        bucketIdx = inputs.Get("bucketIdx");
                        actValue = inputs.Get("inputValue");

                        IClassifier c = (IClassifier)t1.GetClassifiers().Get(key);
                        Classification<object> result = c.Compute<object>(recordNum, inputMap, t1.GetSdr(), Layer.IsLearn(), true);

                        t1.SetRecordNum(recordNum).StoreClassification((string)inputs.Get("name"), result);
                    }
                    return t1;
                };
                //        return new Func<ManualInput, ManualInput>()
                //        {

                //        private Object bucketIdx;
                //        private Object actValue;
                //        Map<String, Object> inputMap = new HashMap<String, Object>()
                //        {

                //                private static final long serialVersionUID = 1L;

                //               public Object get(Object o)
                //                {
                //                    return o.equals("bucketIdx") ? bucketIdx : actValue;
                //                }
                //         };

                //         public ManualInput call(ManualInput t1)
                //      {
                //      Map<String, NamedTuple> ci = t1.GetClassifierInput();
                //      int recordNum = getRecordNum();
                //      for (String key : ci.keySet())
                //      {
                //          NamedTuple inputs = ci.Get(key);
                //          bucketIdx = inputs.Get("bucketIdx");
                //          actValue = inputs.Get("inputValue");

                //         CLAClassifier c = (CLAClassifier)t1.GetClassifiers().Get(key);
                //         Classification<Object> result = c.Compute(recordNum, inputMap, t1.GetSDR(), isLearn, true);

                //         t1.recordNum(recordNum).storeClassification((String)inputs.Get("name"), result);
                //       }

                //          return t1;
                //      }
                //     };
            }

            public Func<ManualInput, ManualInput> CreateAnomalyFunc(Anomaly an)
            {
                int cellsPerColumn = Layer.Connections.GetCellsPerColumn();
                return t1 =>
                {
                    if (t1.GetFeedForwardSparseActives() == null || t1.GetPreviousPredictiveCells() == null)
                    {
                        return t1.SetAnomalyScore(1.0);
                    }
                    return t1.SetAnomalyScore(Layer.AnomalyComputer.Compute(t1.GetFeedForwardSparseActives(),
                        SDR.CellsAsColumnIndices(t1.GetPreviousPredictiveCells(), cellsPerColumn), 0, 0));
                };
                //return new Func<ManualInput, ManualInput>() {

                //    int cellsPerColumn = connections.GetCellsPerColumn();

                //    public ManualInput call(ManualInput t1)
                //{
                //    if (t1.GetFeedForwardSparseActives() == null || t1.GetPreviousPredictiveCells() == null)
                //    {
                //        return t1.anomalyScore(1.0);
                //    }
                //    return t1.anomalyScore(anomalyComputer.compute(t1.GetFeedForwardSparseActives(),
                //        SDR.cellsAsColumnIndices(t1.GetPreviousPredictiveCells(), cellsPerColumn), 0, 0));
                //}
                //    };
            }

        }

        #endregion

        #region Equality Members

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((Name == null) ? 0 : Name.GetHashCode());
            result = prime * result + _recordNum;
            result = prime * result + (int)AlgoContentMask;
            result = prime * result + ((CurrentInference == null) ? 0 : CurrentInference.GetHashCode());
            result = prime * result + (_hasGenericProcess ? 1231 : 1237);
            result = prime * result + (_isClosed ? 1231 : 1237);
            result = prime * result + (_isHalted ? 1231 : 1237);
            result = prime * result + (_isLearn ? 1231 : 1237);
            result = prime * result + ((ParentRegion == null) ? 0 : ParentRegion.GetHashCode());
            result = prime * result + ((SensorParams == null) ? 0 : SensorParams.GetHashCode());
            return result;
        }


        public override bool Equals(object obj)
        {
            if (this == obj)
                return true;
            if (obj == null)
                return false;
            if (GetType() != obj.GetType())
                return false;
            Layer<T> other = (Layer<T>)obj;
            if (Name == null)
            {
                if (other.Name != null)
                    return false;
            }
            else if (!Name.Equals(other.Name))
                return false;
            if (AlgoContentMask != other.AlgoContentMask)
                return false;
            if (CurrentInference == null)
            {
                if (other.CurrentInference != null)
                    return false;
            }
            else if (!CurrentInference.Equals(other.CurrentInference))
                return false;
            if (_recordNum != other._recordNum)
                return false;
            if (_hasGenericProcess != other._hasGenericProcess)
                return false;
            if (_isClosed != other._isClosed)
                return false;
            if (_isHalted != other._isHalted)
                return false;
            if (_isLearn != other._isLearn)
                return false;
            if (ParentRegion == null)
            {
                if (other.ParentRegion != null)
                    return false;
            }
            else if (other.ParentRegion == null || !ParentRegion.GetName().Equals(other.ParentRegion.GetName()))
                return false;
            if (SensorParams == null)
            {
                if (other.SensorParams != null)
                    return false;
            }
            else if (!SensorParams.Equals(other.SensorParams))
                return false;

            return true;
        }

        #endregion
    }
}