using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    /// {@code Layer} contains the physical structure (columns, cells, dendrites etc)
    /// shared by a sequence of algorithms which serve to implement the predictive
    /// inferencing present in this, the allegory to its biological equivalent.
    /// </p>
    /// <p>
    /// <b>COMPOSITION:</b> A Layer is constructed with {@link Parameters} which
    /// configure the behavior of most of the key algorithms a Layer may contain. It
    /// is also <em>optionally</em> constructed with each of the algorithms in turn.
    /// A Layer that includes an {@link Encoder} is always initially configured with
    /// a {@link MultiEncoder}. The child encoders contained within the MultiEncoder
    /// are configured from the Map included with the specified Parameters, keyed by
    /// {@link Parameters.KEY#FIELD_ENCODING_MAP}.
    /// </p>
    /// <p>
    /// A field encoding map consists of one map for each of the fields to be
    /// encoded. Each individual map in the field encoding map contains the typical
    /// {@link Encoder} parameters, plus a few "meta" parameters needed to describe
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
    ///      inner.put("fieldType", fieldType); (see {@link FieldMetaType} for type examples)
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
    ///      inner.put("fieldType", fieldType); (see {@link FieldMetaType} for type examples)
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
    /// // How to merge (union) two {@link Parameters} objects. This one merges
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
    public class Layer<T> : BaseRxLayer
    {
        protected static readonly ILog Logger = LogManager.GetLogger(typeof(Layer<T>));

        private readonly FunctionFactory _factory;

        /// <summary>
        /// Active columns in the <see cref="SpatialPooler"/> at time "t"
        /// </summary>
        protected int[] FeedForwardActiveColumns;
        /// <summary>
        /// Active column indexes from the <see cref="SpatialPooler"/> at time "t"
        /// </summary>
        protected int[] FeedForwardSparseActives;
        /// <summary>
        /// Predictive <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t - 1"
        /// </summary>
        protected HashSet<Cell> PreviousPredictiveCells;
        /// <summary>
        /// Predictive <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t"
        /// </summary>
        protected HashSet<Cell> PredictiveCells;
        /// <summary>
        /// Active <see cref="Cell"/>s in the <see cref="TemporalMemory"/> at time "t"
        /// </summary>
        protected HashSet<Cell> ActiveCells;

        private bool _isHalted;

        private Layer<IInference> _next;
        private Layer<IInference> _previous;

        /// <summary>
        /// Retains the order of added items - for use with interposed {@link Observable}
        /// </summary>
        private readonly List<object> _addedItems = new List<object>();

        /// <summary>
        /// Indicates whether there is a generic processing node entered
        /// </summary>
        private bool _hasGenericProcess;

        /// <summary>
        /// List of {@link Encoders} used when storing bucket information 
        /// see <see cref="DoEncoderBucketMapping(IInference, IDictionary{string, object})"/>
        /// </summary>
        private List<EncoderTuple> _encoderTuples;

        /**
         * Creates a new {@code Layer} using the {@link Network} level
         * {@link Parameters}
         * 
         * @param n
         *            the parent {@link Network}
         */
        public Layer(Network n)
            : this(n, n.GetParameters())
        {

        }

        /**
         * Creates a new {@code Layer} using the specified {@link Parameters}
         * 
         * @param n
         *            the parent {@link Network}
         * @param p
         *            the {@link Parameters} to use with this {@code Layer}
         */
        public Layer(Network n, Parameters p)
            : this("[Layer " + TimeUtils.CurrentTimeMillis() + "]", n, p)
        {

        }

        /**
         * Creates a new {@code Layer} using the specified {@link Parameters}
         * 
         * @param name  the name identifier of this {@code Layer}
         * @param n     the parent {@link Network}
         * @param p     the {@link Parameters} to use with this {@code Layer}
         */
        public Layer(string name, Network n, Parameters p)
            : base(name, n, p)
        {
            _factory = new FunctionFactory(this);

            ObservableDispatch = CreateDispatchMap();
        }

        /**
         * Creates a new {@code Layer} initialized with the specified algorithmic
         * components.
         * 
         * @param params                    A {@link Parameters} object containing configurations for a
         *                                  SpatialPooler, TemporalMemory, and Encoder (all or none may be used).
         * @param e                         (optional) The Network API only uses a {@link MultiEncoder} at
         *                                  the top level because of its ability to delegate to child encoders.
         * @param sp                        (optional) {@link SpatialPooler}
         * @param tm                        (optional) {@link TemporalMemory}
         * @param autoCreateClassifiers     (optional) Indicates that the {@link Parameters} object
         *                                  contains the configurations necessary to create the required encoders.
         * @param a                         (optional) An {@link Anomaly} computer.
         */
        public Layer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm, bool? autoCreateClassifiers, Anomaly a)
            : base(@params, e, sp, tm, autoCreateClassifiers, a)
        {
            _factory = new FunctionFactory(this);

            ObservableDispatch = CreateDispatchMap();

            if (Logger.IsDebugEnabled)
            {
                Logger.Debug(string.Format("Layer successfully created containing: {0}{1}{2}{3}{4}", (Encoder == null ? "" : "MultiEncoder,"), (SpatialPooler == null ? "" : "SpatialPooler,"), (TemporalMemory == null ? ""
                                : "TemporalMemory,"), (autoCreateClassifiers == null ? "" : "Auto creating CLAClassifiers for each input field."), (AnomalyComputer == null ? "" : "Anomaly")));
            }
        }

        /// <summary>
        /// Finalizes the initialization in one method call so that side effect
        /// operations to share objects and other special initialization tasks can
        /// happen all at once in a central place for maintenance ease.
        /// </summary>
        /// <returns></returns>
        public override ILayer Close()
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
                }
            }

            // Create Encoder hierarchy from definitions & auto create classifiers
            // if specified
            if (Encoder != null)
            {
                if (Encoder.GetEncoders(Encoder) == null || Encoder.GetEncoders(Encoder).Count < 1)
                {
                    if (Params.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP) == null || ((Map<string, Map<string, object>>)Params.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP)).Count < 1)
                    {
                        Logger.Error("No field encoding map found for specified MultiEncoder");
                        throw new InvalidOperationException("No field encoding map found for specified MultiEncoder");
                    }

                    Encoder.AddMultipleEncoders((Map<string, Map<string, object>>)Params.GetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP));
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

            if (AutoCreateClassifiers != null && AutoCreateClassifiers.GetValueOrDefault() && (_factory.Inference.GetClassifiers() == null || _factory.Inference.GetClassifiers().Count < 1))
            {
                _factory.Inference.SetClassifiers(MakeClassifiers(Encoder == null ? ParentNetwork.GetEncoder() : Encoder));

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
            TemporalMemory?.Init(Connections);

            FeedForwardActiveColumns = new int[Connections.GetNumColumns()];

            _isClosed = true;

            Logger.Debug("Layer " + Name + " content initialize mask = " + AlgoContentMask); // Integer.toBinaryString(algo_content_mask)

            return this;
        }

        /**
         * Called from {@link FunctionFactory#createSpatialFunc(SpatialPooler)} and from {@link #close()}
         * to calculate the size of the input vector given the output source either being a {@link TemporalMemory}
         * or a {@link SpatialPooler} - from this {@link Region} or a previous {@link Region}.
         * 
         * @return  the length of the input vector
         */
        public override int CalculateInputWidth()
        {
            // If no previous Layer, check upstream region for its output layer's output.
            if (_previous == null)
            {
                if (ParentRegion.GetUpstreamRegion() != null)
                {
                    // Upstream region with TM
                    if ((ParentRegion.GetUpstreamRegion().GetHead().GetMask() & LayerMask.TemporalMemory) == LayerMask.TemporalMemory)
                    {
                        var @out = (ParentRegion.GetUpstreamRegion().GetHead().GetConnections().GetCellsPerColumn() *
                                    (ParentRegion.GetUpstreamRegion().GetHead().GetConnections().GetMemory().GetMaxIndex() + 1));

                        return @out;
                    }
                    // Upstream region but no TM, so input is the upstream region's SP

                    return new SparseBinaryMatrix(ParentRegion.GetUpstreamRegion().GetHead().GetConnections().GetColumnDimensions()).GetMaxIndex() + 1;
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
            else {
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

        

        /**
         * Given an input field width and Spatial Pooler dimensionality; this method
         * will return an array of dimension sizes whose number is equal to the
         * number of column dimensions. The sum of the returned dimensions will be
         * equal to the flat input field width specified.
         * 
         * This method should be called when a disparity in dimensionality between
         * the input field and the number of column dimensions is detected.
         * Otherwise if the input field dimensionality is correctly specified, this
         * method should <b>not</b> be used.
         * 
         * @param inputWidth        the flat input width of an {@link Encoder}'s output or the
         *                          vector used as input to the {@link SpatialPooler}
         * @param numColumnDims     a number specifying the number of column dimensions that
         *                          should be returned.
         * @return
         */
        public int[] InferInputDimensions(int inputWidth, int numDims)
        {
            double flatSize = inputWidth;
            double numColDims = numDims;
            //double sliceArrangement = Math.Pow(flatSize, 1 / numColDims);
            //double remainder = sliceArrangement % (int)sliceArrangement;
            int[] retVal = new int[(int)numColDims];
            double log = Math.Log10(flatSize);
            double dimensions = numColDims;
            double sliceArrangement = Math.Round(Math.Pow(10, (log/dimensions)),8, MidpointRounding.AwayFromZero);//MathContext.DECIMAL32);.doubleValue();
            double remainder = sliceArrangement % (int)sliceArrangement;
            if (remainder > double.Epsilon)
            {
                for (int i = 0; i < numColDims - 1; i++)
                    retVal[i] = 1;
                retVal[(int)numColDims - 1] = (int)flatSize;
            }
            else {
                for (int i = 0; i < numColDims; i++)
                    retVal[i] = (int)sliceArrangement;
            }

            return retVal;
        }

        /**
         * Allows the user to define the {@link Connections} object data structure
         * to use. Or possibly to share connections between two {@code Layer}s
         * 
         * @param c     the {@code Connections} object to use.
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Using(Connections c)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }
            Connections = c;
            return this;
        }

        /**
         * Allows the user to specify the {@link Parameters} object used by this
         * {@code Layer}. If the intent is to share Parameters across multiple
         * Layers, it must be kept in mind that two Layers containing the same
         * algorithm may require specification of locally different parameter
         * settings. In this case, one could use
         * {@link #alterParameter(KEY, Object)} method to change a local setting
         * without impacting the same setting in the source parameters object. This
         * is made possible because the {@link #alterParameter(KEY, Object)} method
         * first makes a local copy of the {@link Parameters} object, then modifies
         * the specified parameter.
         * 
         * @param p     the {@link Parameters} to use in this {@code Layer}
         * @return      this {@code Layer}
         */
        public Layer<T> Using(Parameters p)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }
            Params = p;
            return this;
        }

        /**
         * Adds an {@link HTMSensor} to this {@code Layer}. An HTMSensor is a regular
         * {@link Sensor} (i.e. {@link FileSensor}, {@link URISensor}, or {@link ObservableSensor})
         * which has had an {@link Encoder} configured and added to it. HTMSensors are
         * HTM Aware, where as regular Sensors have no knowledge of HTM requirements.
         * 
         * @param sensor    the {@link HTMSensor}
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Add(ISensor sensor)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }

            Sensor = (IHTMSensor)sensor;
            if (ParentNetwork != null && ParentRegion != null)
            {
                ParentNetwork.SetSensorRegion(ParentRegion);
            }
            return this;
        }

        /**
         * Adds a {@link MultiEncoder} to this {@code Layer}
         * 
         * @param encoder   the added MultiEncoder
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Add(MultiEncoder encoder)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }

            Encoder = encoder;
            return this;
        }

        /**
         * Adds a {@link SpatialPooler} to this {@code Layer}
         * 
         * @param sp    the added SpatialPooler
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Add(SpatialPooler sp)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }

            // Preserve addition order
            _addedItems.Add(sp);

            AlgoContentMask |= LayerMask.SpatialPooler;
            SpatialPooler = sp;
            return this;
        }

        /**
         * Adds a {@link TemporalMemory} to this {@code Layer}
         * 
         * @param tm    the added TemporalMemory
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Add(TemporalMemory tm)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }

            // Preserve addition order
            _addedItems.Add(tm);

            AlgoContentMask |= LayerMask.TemporalMemory;
            TemporalMemory = tm;

            return this;
        }

        /**
         * Adds an {@link Anomaly} computer to this {@code Layer}
         * 
         * @param anomalyComputer   the Anomaly instance
         * @return this Layer instance (in fluent-style)
         */
        public override ILayer Add(Anomaly anomalyComputer)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }

            // Preserve addition order
            _addedItems.Add(anomalyComputer);

            AlgoContentMask |= LayerMask.AnomalyComputer;
            AnomalyComputer = anomalyComputer;
            return this;
        }

        /**
         * Adds a "generic" processing node into this {@code Layer}'s processing
         * chain.
         * 
         * <em><b>NOTE: When adding a generic node, the order of calls to
         * the addXXX() methods becomes crucially important. Make sure you 
         * have added items in a valid order in your "fluent" add call declarations.</b></em>
         * 
         * @param func      a {@link Func1} function to be performed at the point of
         *                  insertion within the {@code Layer}'s declaration.
         * @return     this Layer instance (in fluent-style)
         */
        public override ILayer Add(Func<ManualInput, ManualInput> func)
        {
            if (_isClosed)
            {
                throw new InvalidOperationException("Layer already \"closed\"");
            }
            if (func == null)
            {
                throw new InvalidOperationException("Cannot add a null Function");
            }

            _hasGenericProcess = true;
            // Preserve addition order
            _addedItems.Add(func);
            return this;
        }

        

        /**
         * Processes a single element, sending the specified input up the configured
         * chain of algorithms or components within this {@code Layer}; resulting in
         * any {@link Subscriber}s or {@link Observer}s being notified of results
         * corresponding to the specified input (unless a {@link SpatialPooler}
         * "primer delay" has been configured).
         * 
         * The first input to the Layer invokes a method to resolve the transformer
         * at the bottom of the input chain, therefore the "type" (&lt;T&gt;) of the
         * input cannot be changed once this method is called for the first time.
         * 
         * @param t     the input object who's type is generic.
         */
        public override void Compute<TInput>(TInput t)
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

        public override object CustomCompute(int recordNum, List<int> patternNZ, Map<string, object> classification)
        {
            
            throw new NotImplementedException();
        }

        /**
         * Stops the processing of this {@code Layer}'s processing thread.
         */
        public override void Halt()
        {
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

        /**
         * Returns a flag indicating whether this layer's processing thread has been
         * halted or not.
         * 
         * @return
         */
        public bool IsHalted()
        {
            return _isHalted;
        }

        /**
         * Completes the dispatch chain of algorithm {@link Observable}s with
         * specialized {@link Transformer}s for each algorithm contained within this
         * Layer. This method then starts the output stream processing of its
         * {@link Sensor} in a separate {@link Thread} (if it exists) - logging this
         * event.
         * 
         * Calling this method sets a flag on the underlying Sensor marking it as
         * "Terminal" meaning that it cannot be restarted and its output stream
         * cannot be accessed again.
         */
        public override void Start()
        {
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
                //T inputVar = (T)Convert.ChangeType(new int[] { }, typeof(T));
                //if (typeof(T) == typeof(int[]))
                //{
                //    inputVar = (T)Convert.ChangeType(new int[] { }, typeof(T));
                //}
                CompleteDispatch(new int[] { });
            }
            catch (Exception e)
            {
                NotifyError(e);
            }
            
            LayerThread = new Task(() =>
            {
                Logger.Debug("Layer [" + GetName() + "] started Sensor output stream processing.");

                //////////////////////////

                var outputStream = Sensor.GetOutputStream();

                int[] intArray;
                while (!outputStream.EndOfStream)
                {
                    intArray = outputStream.Read();
                    bool doComputation = false;
                    bool computed = false;
                    try
                    {
                        if (_isHalted)
                        {
                            NotifyComplete();
                            if (_next != null)
                            {
                                _next.Halt();
                            }
                        }
                        else
                        {
                            doComputation = true;
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        NotifyError(new ApplicationException("Unknown Exception while filtering input", e));
                        throw;
                    }

                    if (!doComputation) continue;

                    try
                    {

                        //Debug.WriteLine("Computing in the foreach loop: " + Arrays.ToString(intArray));
                        _factory.Inference.SetEncoding(intArray);

                        Compute(intArray);
                        computed = true;

                        // Notify all downstream observers that the stream is closed
                        if (!Sensor.EndOfStream())
                        {
                            NotifyComplete();
                        }
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine(e);
                        if (Debugger.IsAttached) Debugger.Break();
                        NotifyError(e);
                    }

                    if (!computed)
                    {
                        // Wait a little while, because new work can come
                        Thread.Sleep(5000);
                    }
                }

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
            //        (LAYER_THREAD = new Thread("Sensor Layer [" + getName() + "] Thread")
            //        {

            //            public void run()
            //          {
            //    LOGGER.debug("Layer [" + getName() + "] started Sensor output stream processing.");

            //    // Applies "terminal" function, at this point the input stream
            //    // is "sealed".
            //    sensor.GetOutputStream().filter(i-> {
            //        if (isHalted)
            //        {
            //            notifyComplete();
            //            if (next != null)
            //            {
            //                next.halt();
            //            }
            //            return false;
            //        }

            //        if (Thread.currentThread().isInterrupted())
            //        {
            //            notifyError(new RuntimeException("Unknown Exception while filtering input"));
            //        }

            //        return true;
            //    }).forEach(intArray-> {
            //        ((ManualInput)Layer.this.factory.inference).encoding(intArray);

            //        Layer.this.compute((T)intArray);

            //        // Notify all downstream observers that the stream is closed
            //        if (!sensor.hasNext())
            //        {
            //            notifyComplete();
            //        }
            //    });
            //}
            //        }).start();

            Logger.Debug(string.Format("Start called on Layer thread {0}", LayerThread));
        }

        /**
         * Sets a pointer to the "next" Layer in this {@code Layer}'s
         * {@link Observable} sequence.
         * 
         * @param l
         */
        public void Next(Layer<IInference> l)
        {
            _next = l;
        }

        /**
         * Returns the next Layer following this Layer in order of process flow.
         * 
         * @return
         */
        public override ILayer GetNext()
        {
            return _next;
        }

        /**
         * Sets a pointer to the "previous" Layer in this {@code Layer}'s
         * {@link Observable} sequence.
         * 
         * @param l
         */
        public void Previous(Layer<IInference> l)
        {
            _previous = l;
        }

        /**
         * Returns the previous Layer preceding this Layer in order of process flow.
         * 
         * @return
         */
        public override ILayer GetPrevious()
        {
            return _previous;
        }

        /**
         * Returns the current predictive {@link Cell}s
         * 
         * @return the binary vector representing the current prediction.
         */
        public HashSet<Cell> GetPredictiveCells()
        {
            return PredictiveCells;
        }

        /**
         * Returns the previous predictive {@link Cells}
         * 
         * @return the binary vector representing the current prediction.
         */
        public HashSet<Cell> GetPreviousPredictiveCells()
        {
            return PreviousPredictiveCells;
        }

        /**
         * Returns the current (dense) array of column indexes which represent
         * columns which have become activated during the current input sequence
         * from the SpatialPooler.
         * 
         * @return the array of active column indexes
         */
        public int[] GetFeedForwardActiveColumns()
        {
            return FeedForwardActiveColumns;
        }

        /**
         * Returns the {@link Cell}s activated in the {@link TemporalMemory} at time
         * "t"
         * 
         * @return
         */
        public HashSet<Cell> GetActiveCells()
        {
            return ActiveCells;
        }

        /**
         * Sets the sparse form of the {@link SpatialPooler} column activations and
         * returns the specified array.
         * 
         * @param activesInSparseForm       the sparse column activations
         * @return
         */
        private int[] SetFeedForwardSparseActives(int[] activesInSparseForm)
        {
            FeedForwardSparseActives = activesInSparseForm;
            return FeedForwardSparseActives;
        }

        /**
         * Returns the SpatialPooler column activations in sparse form (indexes of
         * the on bits).
         * 
         * @return
         */
        public int[] GetFeedForwardSparseActives()
        {
            return FeedForwardSparseActives;
        }

        /**
         * Returns the values submitted to this {@code Layer} in an array whose
         * indexes correspond to the indexes of probabilities returned when calling
         * {@link #getAllPredictions(String, int)}.
         * 
         * @param field     The field name of the required prediction
         * @param step      The step for the required prediction
         * @return
         */
        public TV[] GetAllValues<TV>(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            ClassifierResult<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No ClassifierResult exists for the specified field: {0}", field));
            }

            return c?.GetActualValues().Select(av => av != null ? (TV) av : default(TV)).ToArray();

            //return (V[])c.GetActualValues().Cast<V>().ToArray();
        }

        /**
         * Returns a double[] containing a prediction confidence measure for each
         * bucket (unique entry as determined by an encoder). In order to relate the
         * probability to an actual value, call {@link #getAllValues(String, int)}
         * which returns an array containing the actual values submitted to this
         * {@code Layer} - the indexes of each probability will match the index of
         * each actual value entered.
         * 
         * @param field     The field name of the required prediction
         * @param step      The step for the required prediction
         * @return
         */
        public double[] GetAllPredictions(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            ClassifierResult<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No ClassifierResult exists for the specified field: {0}", field));
            }

            return c?.GetStats(step);
        }

        /**
         * Returns the value whose probability is calculated to be the highest for
         * the specified field and step.
         * 
         * @param field     The field name of the required prediction
         * @param step      The step for the required prediction
         * @return
         */
        public TK GetMostProbableValue<TK>(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            ClassifierResult<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No ClassifierResult exists for the specified field: {0}", field));
            }

            return (TK)c?.GetMostProbableValue(step);
        }

        /**
         * Returns the bucket index of the value with the highest calculated
         * probability for the specified field and step.
         * 
         * @param field     The field name of the required prediction
         * @param step      The step for the required prediction
         * @return
         */
        public int GetMostProbableBucketIndex(string field, int step)
        {
            if (CurrentInference == null || CurrentInference.GetClassifiers() == null)
            {
                throw new InvalidOperationException("Predictions not available. " + "Either classifiers unspecified or inferencing has not yet begun.");
            }

            ClassifierResult<object> c = CurrentInference.GetClassification(field);
            if (c == null)
            {
                Logger.Debug(string.Format("No ClassifierResult exists for the specified field: {0}", field));
            }

            Debug.Assert(c != null, "c != null");
            return c.GetMostProbableBucketIndex(step);
        }

        /// <summary>
        /// Resets the <see cref="TemporalMemory"/> if it exists.
        /// </summary>
        public override void Reset()
        {
            if (TemporalMemory == null)
            {
                Logger.Debug("Attempt to reset Layer: " + GetName() + "without TemporalMemory");
            }
            else {
                TemporalMemory.Reset(Connections);
                ResetRecordNum();
            }
        }

        /**
         * We cannot create the {@link Observable} sequence all at once because the
         * first step is to transform the input type to the type the rest of the
         * sequence uses (Observable<b>&lt;Inference&gt;</b>). This can only happen
         * during the actual call to {@link #compute(Object)} which presents the
         * input type - so we create a map of all types of expected inputs, and then
         * connect the sequence at execution time; being careful to only incur the
         * cost of sequence assembly on the first call to {@link #compute(Object)}.
         * After the first call, we dispose of this map and its contents.
         * 
         * @return the map of input types to {@link Transformer}
         */
        protected override Map<Type, IObservable<ManualInput>> CreateDispatchMap()
        {
            Map<Type, IObservable<ManualInput>> obsDispatch = new Map<Type, IObservable<ManualInput>>();

            Publisher = new Subject<object>(); //PublishSubject.create();

            obsDispatch.Add(typeof(IDictionary), _factory.CreateMultiMapFunc(Publisher.Select(t => t)));
            obsDispatch.Add(typeof(ManualInput), _factory.CreateManualInputFunc(Publisher.Select(t => t)));
            obsDispatch.Add(typeof(string[]), _factory.CreateEncoderFunc(Publisher.Select(t => t)));
            obsDispatch.Add(typeof(int[]), _factory.CreateVectorFunc(Publisher.Select(t => t)));

            return obsDispatch;
        }

        // ////////////////////////////////////////////////////////////
        // PRIVATE METHODS AND CLASSES BELOW HERE //
        // ////////////////////////////////////////////////////////////

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

            // Adds the delegate observer to the subscribers
            // Subscribes the sequence to the delegate subscriber
            // Clears the input types to transfers map
            CompleteSequenceDispatch(sequence);

            // Handle global network sensor access.
            if (Sensor == null)
            {
                Sensor = (IHTMSensor)ParentNetwork?.GetSensor();
            }
            else if (ParentNetwork != null)
            {
                ParentNetwork.SetSensor(Sensor);
            }
        }

        /**
         * If this Layer has a Sensor, map its encoder's buckets
         * 
         * @param sequence
         * @return
         */
        private IObservable<ManualInput> MapEncoderBuckets(IObservable<ManualInput> sequence)
        {
            if (HasSensor())
            {
                if (GetSensor().GetMetaInfo().GetFieldTypes().Any(ft => ft == FieldMetaType.SparseArray || ft == FieldMetaType.DenseArray || ft == FieldMetaType.Coord || ft == FieldMetaType.Geo))
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
                //if (GetSensor().GetMetaInfo().GetFieldTypes().stream().anyMatch(ft-> {
                //    return ft == FieldMetaType.SARR || ft == FieldMetaType.DARR || ft == FieldMetaType.COORD || ft == FieldMetaType.GEO;
                //})) {
                //    if (autoCreateClassifiers)
                //    {
                //        throw new InvalidOperationException("Cannot autoclassify with raw array input or " + " Coordinate based encoders... Remove auto classify setting.");
                //    }
                //    return sequence;
                //}
                //sequence = sequence.map(m-> {
                //    doEncoderBucketMapping(m, getSensor().GetInputMap());
                //    return m;
                //});
            }

            return sequence;
        }

        /**
         * Stores a {@link NamedTuple} which contains the input values and bucket
         * information - keyed to the encoded field name so that a classifier can
         * retrieve it later on in the processing sequence.
         * 
         * @param inference
         * @param encoderInputMap
         */
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
                IEncoder e = t.GetEncoder();

                int bucketIdx;

                object o;
                if (encoderInputMap.GetType().FullName.Contains("InputMap"))
                {
                    var getMethod = encoderInputMap.GetType().GetMethod("Get");
                    getMethod = getMethod.MakeGenericMethod(e.GetEncoderType());
                    o = getMethod.Invoke(encoderInputMap, new object[] { name }); // encoderInputMap[name]
                }
                else
                {
                    o = encoderInputMap[name];
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
                else {
                    bucketIdx = e.GetBucketIndices((string)o)[0];
                }

                int offset = t.GetOffset();
                int[] tempArray = new int[e.GetWidth()];
                Array.Copy(encoding, offset, tempArray, 0, tempArray.Length);

                inference.GetClassifierInput().Add(name, new NamedTuple(new[] { "name", "inputValue", "bucketIdx", "encoding" }, name, o, bucketIdx, tempArray));
            }
        }

        /**
         * Connects the {@link Transformer} to the rest of the {@link Observable}
         * sequence.
         * 
         * @param o     the Transformer part of the sequence.
         * @return the completed {@link Observable} sequence.
         */
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
                else {
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

        /**
         * Connects {@link Observable} or {@link Transformer} emissions in the order
         * they are declared.
         * 
         * @param o     first {@link Observable} in sequence.
         * @return
         */
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
                    else {
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

        /**
         * Creates the {@link NamedTuple} of names to encoders used in the
         * observable sequence.
         * 
         * @param encoder
         * @return
         */
        private NamedTuple MakeClassifiers(MultiEncoder encoder)
        {
            string[] names = new string[encoder.GetEncoders(encoder).Count];
            CLAClassifier[] ca = new CLAClassifier[names.Length];
            int i = 0;
            foreach (EncoderTuple et in encoder.GetEncoders(encoder))
            {
                names[i] = et.GetName();
                ca[i] = new CLAClassifier();
                i++;
            }
            return new NamedTuple(names, (object[])ca);
        }

        /// <summary>
        /// Called internally to invoke the <see cref="SpatialPooler"/>
        /// </summary>
        /// <param name="input">the current input vector</param>
        /// <returns></returns>
        internal virtual int[] SpatialInput(int[] input)
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
            SpatialPooler.Compute(Connections, input, FeedForwardActiveColumns, Sensor == null || Sensor.GetMetaInfo().IsLearn(), IsLearn);

            return FeedForwardActiveColumns;
        }

        /// <summary>
        /// Called internally to invoke the <see cref="TemporalMemory"/>
        /// </summary>
        /// <param name="input"> the current input vector</param>
        /// <param name="mi">the current input inference container</param>
        /// <returns></returns>
        internal virtual int[] TemporalInput(int[] input, ManualInput mi)
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
            else {
                cc = TemporalMemory.Compute(Connections, input, IsLearn);
            }

            PreviousPredictiveCells = PredictiveCells;

            // Store the predictive columns
            mi.SetPredictiveCells(PredictiveCells = cc.PredictiveCells());
            // Store activeCells
            mi.SetActiveCells(ActiveCells = cc.ActiveCells());
            // Store the Compute Cycle
            mi.SetComputeCycle(cc);
            return SDR.AsCellIndices(ActiveCells = cc.ActiveCells());
        }

        //////////////////////////////////////////////////////////////
        //        Inner Class Definition Transformer Example        //
        //////////////////////////////////////////////////////////////
        /**
         * Factory which returns an {@link Observable} capable of transforming known
         * input formats to the universal format object passed between all
         * Observables in the Observable chains making up the processing steps
         * involving HTM algorithms.
         * 
         * The {@link Transformer} implementations are used to transform various
         * inputs into a {@link ManualInput}, and thus are used at the beginning of
         * any Observable chain; each succeding Observable in a given chain would
         * then communicate via passed ManualInputs or {@link Inference}s (which are
         * the same thing).
         * 
         * @author David Ray
         * @see Layer#completeDispatch(Object)
         * @see Layer#resolveObservableSequence(Object)
         * @see Layer#fillInSequence(Observable)
         */
        internal class FunctionFactory
        {
            internal Layer<T> Layer { get; set; }

            internal ManualInput Inference = new ManualInput();

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
             * Emits an {@link Observable} which is transformed from a String[] of
             * csv input to one that emits {@link Inference}s.
             * </p>
             * <p>
             * This class currently lacks the implementation of csv parsing into
             * distinct Object types - which is necessary to compose a "multi-map"
             * necessary to input data into the {@link MultiEncoder} necessary to
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
             * Emits an {@link Observable} which is transformed from a Map input
             * type to one that emits {@link Inference}s.
             * </p>
             * <p>
             * This {@link Transformer} is used when the input to a given
             * {@link Layer} is a map of fields to input Objects. It is typically
             * used when a Layer is configured with a {@link MultiEncoder} (which is
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
             * Emits an {@link Observable} which is transformed from a binary vector
             * input type to one that emits {@link Inference}s.
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
                    return FunctionFactory.Inference.SetRecordNum(FunctionFactory.Layer.GetRecordNum()).SetSdr(input).SetLayerInput(input);
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

            /**
             * Emits an {@link Observable} which copies an Inference input to the
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
                    if (!ArrayUtils.IsSparse(t1.GetSdr()))
                    {
                        // Set on Layer, then set sparse actives as the sdr,
                        // then set on Manual Input (t1)
                        t1 = t1.SetSdr(Layer.SetFeedForwardSparseActives(ArrayUtils.Where(t1.GetSdr(), ArrayUtils.WHERE_1))).SetFeedForwardSparseActives(t1.GetSdr());
                    }
                    return t1.SetSdr(Layer.TemporalInput(t1.GetSdr(), t1));
                };
                //    return new Func<ManualInput, ManualInput>() {

                //    @Override
                //    public ManualInput call(ManualInput t1)
                //{
                //    if (!ArrayUtils.isSparse(t1.GetSDR()))
                //    {
                //        // Set on Layer, then set sparse actives as the sdr,
                //        // then set on Manual Input (t1)
                //        t1 = t1.sdr(feedForwardSparseActives(ArrayUtils.where(t1.GetSDR(), ArrayUtils.WHERE_1))).feedForwardSparseActives(t1.GetSDR());
                //    }
                //    return t1.sdr(temporalInput(t1.GetSDR(), t1));
                //}
                //    };
            }

            public Func<ManualInput, ManualInput> CreateClassifierFunc()
            {
                object bucketIdx = null, actValue = null;
                IDictionary<string, object> inputMap = new CustomGetDictionary<string, object>(k => k.Equals("bucketIdx") ? bucketIdx : actValue);
                return t1 =>
                {
                    var ci = t1.GetClassifierInput();
                    int recordNum = Layer.GetRecordNum();
                    foreach (string key in ci.Keys)
                    {
                        NamedTuple inputs = ci[key];
                        bucketIdx = inputs.Get("bucketIdx");
                        actValue = inputs.Get("inputValue");

                        CLAClassifier c = (CLAClassifier)t1.GetClassifiers().Get(key);
                        ClassifierResult<object> result = c.Compute<object>(recordNum, inputMap, t1.GetSdr(), Layer.IsLearn, true);

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
                //         ClassifierResult<Object> result = c.Compute(recordNum, inputMap, t1.GetSDR(), isLearn, true);

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


        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + ((Name == null) ? 0 : Name.GetHashCode());
            result = prime * result + ((ParentRegion == null) ? 0 : ParentRegion.GetHashCode());
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
            ILayer other = (ILayer)obj;
            if (Name == null)
            {
                if (other.GetName() != null)
                    return false;
            }
            else if (!Name.Equals(other.GetName()))
                return false;
            if (ParentRegion == null)
            {
                if (other.GetParentRegion() != null)
                    return false;
            }
            else if (!ParentRegion.Equals(other.GetParentRegion()))
                return false;
            return true;
        }
    }

}