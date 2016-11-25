using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using log4net;

namespace HTM.Net.Network
{
    /// <summary>
    /// <p>
    /// A <see cref="Network"/> is the fundamental component of the HTM.net Network API.
    /// It is comprised of <see cref="Region"/>s which are in turn comprised of <see cref="ILayer"/>s;
    /// each Layer directly containing one or more algorithm or computational components
    /// such (i.e. <see cref="ISensor"/>, <see cref="MultiEncoder"/>, <see cref="SpatialPooler"/>, 
    /// <see cref="TemporalMemory"/>, <see cref="CLAClassifier"/> etc.)
    /// </p>
    /// </summary>
    /// <remarks>
    /// Networks in HTM.java are extremely easy to compose. For instance, here is an example 
    /// of a network which contains everything:
    /// <pre>
    /// Parameters p = NetworkTestHarness.getParameters();
    /// p = p.union(NetworkTestHarness.getNetworkDemoTestEncoderParams());
    /// 
    /// Network network = Network.create("test network", p)
    ///     .add(Network.createRegion("r1")
    ///         .add(Network.createLayer("1", p)
    ///             .alterParameter(KEY.AUTO_CLASSIFY, bool.TRUE)
    ///             .add(Anomaly.create())
    ///             .add(new TemporalMemory())
    ///             .add(new SpatialPooler())
    ///             .add(Sensor.create(FileSensor::create, SensorParams.create(
    ///                 Keys::path, "", ResourceLocator.path("rec-center-hourly.csv"))))));
    /// </pre>
    ///                 
    /// <p>
    /// As you can see, <see cref="Network"/>s can be composed in "fluent" style making their
    /// declaration much more concise.
    /// 
    /// While the above Network contains only 1 Region and only 1 Layer, Networks with many Regions
    /// and Layers within them; may be composed. For example:
    /// <pre>
    /// Connections cons = new Connections();
    /// 
    /// Network network = Network.create("test network", p)
    ///     .add(Network.createRegion("r1")
    ///         .add(Network.createLayer("1", p)
    ///             .add(Anomaly.create())
    ///             .add(new TemporalMemory())
    ///             .add(new SpatialPooler())))
    ///     .add(Network.createRegion("r2")
    ///         .add(Network.createLayer("1", p)
    ///             .using(cons)
    ///             .alterParameter(KEY.AUTO_CLASSIFY, bool.TRUE)
    ///             .add(new TemporalMemory()))                    
    ///         .add(Network.createLayer("2", p)
    ///             .using(cons)
    ///             .add(new SpatialPooler())
    ///             .add(Sensor.create(FileSensor::create, SensorParams.create(
    ///                 Keys::path, "", ResourceLocator.path("rec-center-hourly.csv")))))
    ///         .connect("1", "2")) // Tell the Region to connect the two layers <see cref="Region.Connect(string,string)"/>.
    ///     .connect("r1", "r2");   // Tell the Network to connect the two regions <see cref="Network.Connect(string,string)"/>.
    /// </pre>
    /// As you can see above, a <see cref="Connections"/> object is being shared among Region "r2"'s inner layers 
    /// via the use of the <see cref="ILayer.Using(Connections)"/> method. Additionally, <see cref="Parameters"/> may be
    /// altered in place without changing the values in the original Parameters object, by calling <see cref="ILayer.AlterParameter(Parameters.KEY, object)"/>
    /// as seen in the above examples.  
    /// </p>
    /// <p>
    /// Networks can be "observed", meaning they can return an <see cref="IObservable{IInference}"/> object
    /// which can be operated on in the map-reduce pattern of usage; and they can be 
    /// subscribed to, to receive <see cref="IInference"/> objects as output from every process
    /// cycle. For instance:
    /// <pre>
    /// network.observe().subscribe(new Subscriber&lt;Inference&gt;() {
    ///      public void onCompleted() { System.out.println("Input Completed!"); }
    ///      public void onError(Throwable e) { e.printStackTrace(); }
    ///      public void onNext(Inference i) {
    ///          System.out.println(String.format("%d: %s", i.getRecordNum(), Arrays.toString(i.getSDR())));
    ///      }
    /// });
    /// </pre>
    /// </p>
    /// 
    /// <p>
    /// Likewise, each Region within a Network may be "observed" and/or subscribed to; and each
    /// Layer may be observed and subscribed to as well - for those instances where you would like 
    /// to obtain the processing from an individual component within the network for use outside 
    /// the network in some other area of your application. To find and subscribe to individual
    /// components try:
    /// <pre>
    /// Network network = ...
    /// 
    /// Region region = network.lookup("&lt;region name&gt;");
    /// region.observe().subscribe(new Subscriber&lt;Inference&gt;() {
    ///     public void onCompleted() { System.out.println("Input Completed!"); }
    ///     public void onError(Throwable e) { e.printStackTrace(); }
    ///     public void onNext(Inference i) {
    ///         int[] sdr = i.getSDR();
    ///         do something...
    ///     }    
    /// }
    /// 
    /// Layer l2_3 = region.lookup("&lt;layer name&gt;");
    /// l2_3.observe().subscribe(new Subscriber&lt;Inference&gt;() {
    ///     public void onCompleted() { System.out.println("Input Completed!"); }
    ///     public void onError(Throwable e) { e.printStackTrace(); }
    ///     public void onNext(Inference i) {
    ///         int[] sdr = i.getSDR();
    ///         do something...
    ///     }    
    /// }
    /// </pre>
    /// 
    /// In addition there are many usage examples to be found in the {@link org.numenta.nupic.examples.napi.hotgym} package
    /// where there are tests which may be examined for details, and the {@link NetworkAPIDemo}
    /// </p>
    /// @see Region
    /// @see Layer
    /// @see Inference
    /// @see ManualInput
    /// @see NetworkAPIDemo
    /// </remarks>
    [Serializable]
    public class Network : Persistable, IEnumerable<Region>
    {
        #region Fields

        [NonSerialized]
        public readonly static ILog Logger = LogManager.GetLogger(typeof(Network));

        private readonly string _name;
        private readonly Parameters _parameters;
        private ISensor _sensor;
        private MultiEncoder _encoder;
        private Region _head;
        private Region _tail;
        private Region _sensorRegion;
        private volatile Publisher _publisher;

        private bool _isLearn = true;
        private bool _isThreadRunning;

        private readonly List<Region> _regions = new List<Region>();

        /// <summary>
        /// Stored check pointer function
        /// </summary>
        private Func<Network, byte[]> _checkPointFunction;

        private bool _shouldDoHalt = true;

        //public enum Mode { MANUAL, AUTO, REACTIVE }; 

        #endregion

        /// <summary>
        /// Creates a new <see cref="Network"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        public Network(string name, Parameters parameters)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("All Networks must have a name. Increases digestion, and overall happiness!");
            }
            _name = name;
            _parameters = parameters;
            if (parameters == null)
            {
                throw new ArgumentNullException(nameof(parameters), "Network Parameters were null.");
            }
        }

        /// <summary>
        /// Creates and returns an implementation of <see cref="Network"/>
        /// Warning: name cannot be null or empty
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static Network Create(string name, Parameters parameters)
        {
            return new Network(name, parameters);
        }

        /// <summary>
        /// Creates and returns a child <see cref="Region"/> of this <see cref='Network'/>
        /// </summary>
        /// <param name="name">The String identifier for the specified <see cref="Region"/></param>
        public static Region CreateRegion(string name)
        {
            CheckName(name);

            Region r = new Region(name, null);
            return r;
        }

        /// <summary>
        /// Creates a <see cref="Layer{T}"/> to hold algorithmic components and returns it.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="name"></param>
        /// <param name="p">the <see cref="Parameters"/> to use for the specified <see cref="Layer{T}"/></param>
        /// <returns></returns>
        public static Layer<T> CreateLayer<T>(string name, Parameters p)
        {
            CheckName(name);
            return new Layer<T>(name, null, p);
        }

        /// <summary>
        /// Creates a <see cref="ILayer"/> to hold algorithmic components and returns it.
        /// </summary>
        /// <param name="name"></param>
        /// <param name="p">the <see cref="Parameters"/> to use for the specified <see cref="ILayer"/></param>
        /// <returns></returns>
        public static ILayer CreateLayer(string name, Parameters p)
        {
            CheckName(name);
            return new Layer<IInference>(name, null, p);
        }

        /// <summary>
        /// DO NOT CALL THIS METHOD! FOR INTERNAL USE ONLY!
        /// </summary>
        /// <returns></returns>
        public override object PreSerialize()
        {
            if (_shouldDoHalt && _isThreadRunning)
            {
                Halt();
            }
            else // Make sure "close()" has been called on the Network
            {
                if (_regions.Count == 1)
                {
                    _tail = _regions.First();
                }
                _tail.Close();
            }
            _regions.ForEach(r => r.PreSerialize());
            return this;
        }

        /// <summary>
        /// DO NOT CALL THIS METHOD! FOR INTERNAL USE ONLY!
        /// </summary>
        /// <returns></returns>
        public override object PostDeSerialize()
        {
            _regions.ForEach(r => r.SetNetwork(this));
            _regions.ForEach(r => r.PostDeSerialize());

            // Connect Layer Observable chains (which are transient so we must 
            // rebuild them and their subscribers)
            if (IsMultiRegion())
            {
                Region curr = _head;
                Region nxt = curr.GetUpstreamRegion();
                do
                {
                    curr.Connect(nxt);
                } while ((curr = nxt) != null && (nxt = nxt.GetUpstreamRegion()) != null);
            }

            return this;
        }

        /// <summary>
        /// INTERNAL METHOD: DO NOT CALL
        /// 
        /// Called from <see cref="ILayer"/> to execute a check point from within the scope of 
        /// this {@link Network}
        /// checkPointFunction
        /// </summary>
        /// <returns>the serialized <see cref="Network"/> in byte array form.</returns>
        internal byte[] InternalCheckPointOp()
        {
            _shouldDoHalt = false;
            byte[] serializedBytes = _checkPointFunction(this);
            _shouldDoHalt = true;
            return serializedBytes;
        }

        /// <summary>
        /// Sets the reference to the check point function.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="f">function which executes check point logic.</param>
        public void SetCheckPointFunction(Func<Network, byte[]> f)
        {
            _checkPointFunction = f;
        }

        /// <summary>
        /// USED INTERNALLY, DO NOT CALL
        /// Returns an <see cref="IObservable{T}"/> operator that when subscribed to, invokes an operation
        /// that stores the state of this <see cref="Network"/> while keeping the Network up and running.
        /// The Network will be stored at the pre-configured location (in binary form only, not JSON).
        /// </summary>
        /// <returns>the <see cref="ICheckPointOp{T}"/> operator </returns>
        internal ICheckPointOp<byte[]> GetCheckPointOperator()
        {
            Logger.Debug("Network [" + GetName() + "] called checkPoint() at: " + (new DateTime()));

            if (_regions.Count == 1)
            {
                _tail = _regions.First();
            }
            return _tail.GetCheckPointOperator();
        }

        /// <summary>
        /// Restarts this <see cref="Network"/>. The network will run from the previous save point of the stored Network.
        /// 
        /// <see cref="Restart(bool)"/> for a start at "saved-index" behavior explanation. 
        /// </summary>
        public void Restart()
        {
            Restart(true);
        }

        /// <summary>
        /// Restarts this <see cref="Network"/>. If the "startAtIndex" flag is true, the Network
        /// will start from the last record number (plus 1) at which the Network was saved -
        /// continuing on from where it left off. The Network will achieve this by rebuilding
        /// the underlying Stream (if necessary, i.e. not for <see cref="ObservableSensor{T}"/>s) and skipping 
        /// the number of records equal to the stored record number plus one, continuing from where it left off.
        /// </summary>
        /// <param name="startAtIndex">flag indicating whether to start this <see cref="Network"/> from its previous save point.</param>
        public void Restart(bool startAtIndex)
        {
            if (_regions.Count < 1)
            {
                throw new InvalidOperationException("Nothing to start - 0 regions");
            }

            Region tail = _regions.First();
            Region upstream = tail;
            while ((upstream = upstream.GetUpstreamRegion()) != null)
            {
                tail = upstream;
            }

            // Record thread start
            _isThreadRunning = tail.Restart(startAtIndex);
        }

        /// <summary>
        /// <p>
        /// DO NOT CALL THIS METHOD!
        /// </p><p>
        /// Called internally by an <see cref="ObservableSensor{T}"/>'s factory method's creation of a new 
        /// <see cref="ObservableSensor{T}"/>. This would usually happen following a halt or
        /// deserialization.
        /// </p>
        /// </summary>
        /// <param name="p">the new Publisher created upon reconstitution of a new <see cref="ObservableSensor{T}"/> </param>
        internal void SetPublisher(Publisher p)
        {
            _publisher = p;
            _publisher.SetNetwork(this);
        }

        /// <summary>
        /// Returns the new <see cref="Publisher"/> created after halt or deserialization
        /// of this <see cref="Network"/>, when a new Publisher must be created.
        /// </summary>
        /// <returns>the new Publisher created after deserialization or halt.</returns>
        public Publisher GetPublisher()
        {
            if (_publisher == null)
            {
                throw new NullReferenceException("A Supplier must be built first. " +
                    "please see Network.getPublisherSupplier()");
            }
            return _publisher;
        }

        /// <summary>
        /// Returns a flag indicating whether this <see cref="Network"/> contain multiple <see cref="Region"/>s.
        /// </summary>
        /// <returns>true if so, false if not.</returns>
        public bool IsMultiRegion()
        {
            return _regions.Count > 1;
        }

        /// <summary>
        /// Returns the String identifier for this  <see cref="Network"/>
        /// </summary>
        public string GetName()
        {
            return _name;
        }

        /// <summary>
        /// Calling this 
        /// method will start the main engine thread which pulls in data
        /// from the connected <see cref="Sensor{T}"/>
        /// </summary>
        public void Start()
        {
            if (_regions.Count < 1)
            {
                throw new InvalidOperationException("Nothing to start - 0 regions");
            }

            Region tail = _regions[0];
            Region upstream = tail;
            while ((upstream = upstream.GetUpstreamRegion()) != null)
            {
                tail = upstream;
            }

            // Record thread start
            _isThreadRunning = tail.Start();
        }

        /// <summary>
        /// Returns a flag indicating that the <see cref="Network"/> has an <see cref="IObservable{T}"/> running on a thread.
        /// </summary>
        /// <returns>a flag indicating if threaded.</returns>
        public bool IsThreadedOperation()
        {
            return _isThreadRunning;
        }

        /// <summary>
        /// Halts this <see cref="Network"/>, stopping all threads and closing
        /// all <see cref="ISensorFactory{T}"/> connections to incoming data, freeing up 
        /// any resources associated with the input connections.
        /// </summary>
        public void Halt()
        {
            // Call onComplete if using an ObservableSensor to complete the stream output.
            _publisher?.OnComplete();

            if (_regions.Count == 1)
            {
                _tail = _regions.First();
            }
            _tail.Halt();
        }

        /// <summary>
        /// Returns a flag indicating whether this Network has a Region whose tail (input <see cref="ILayer"/>) is halted.
        /// </summary>
        /// <returns>true if so, false if not</returns>
        public bool IsHalted()
        {
            if (_regions.Count == 1)
            {
                _tail = _regions.First();
            }
            return _tail.IsHalted();
        }

        /// <summary>
        /// Returns the index of the last record processed.
        /// </summary>
        /// <returns>the last recordNum processed</returns>
        public int GetRecordNum()
        {
            if (_regions.Count == 1)
            {
                _tail = _regions.First();
            }
            return _tail.GetTail().GetRecordNum();
        }

        /// <summary>
        /// Sets the learning mode.
        /// </summary>
        /// <param name="isLearn"></param>
        public void SetLearn(bool isLearn)
        {
            _isLearn = isLearn;
            foreach (Region r in _regions)
            {
                r.SetLearn(isLearn);
            }
        }

        /// <summary>
        /// Returns the learning mode setting.
        /// </summary>
        /// <returns></returns>
        public bool IsLearn()
        {
            return _isLearn;
        }

        /// <summary>
        /// Finds any <see cref="Region"/> containing a <see cref="ILayer"/> which contains a <see cref="TemporalMemory"/> and resets them.
        /// </summary>
        public void Reset()
        {
            foreach (Region r in _regions)
            {
                r.Reset();
            }
        }

        /// <summary>
        /// Resets the recordNum in all <see cref="Region"/>s.
        /// </summary>
        public void ResetRecordNum()
        {
            foreach (Region r in _regions)
            {
                r.ResetRecordNum();
            }
        }

        /// <summary>
        /// Returns an <see cref="IObservable{T}"/> capable of emitting <see cref="IInference"/>s
        /// which contain the results of this <see cref="Network"/>'s processing chain.
        /// </summary>
        public IObservable<IInference> Observe()
        {
            if (_regions.Count == 1)
            {
                _head = _regions.First();
            }
            return _head.Observe();
        }

        /// <summary>
        /// Returns the top-most (last in execution order from bottom to top) <see cref="Region"/> in this<see cref="Network"/>
        /// </summary>
        /// <returns></returns>
        public Region GetHead()
        {
            if (_regions.Count == 1)
            {
                _head = _regions[0];
            }
            return _head;
        }

        /// <summary>
        /// Returns the bottom-most (first in execution order from
        /// bottom to top) <see cref="Region"/> in this <see cref="Network"/>
        /// </summary>
        public Region GetTail()
        {
            if (_regions.Count == 1)
            {
                _tail = _regions[0];
            }
            return _tail;
        }

        /// <summary>
        /// For internal Use: Returns a boolean flag indicating whether
        /// the specified <see cref="ILayer"/> is the tail of the Network.
        /// </summary>
        /// <param name="layer">the layer to test   </param>
        /// <returns>true if so, false if not</returns>
        internal bool IsTail(ILayer layer)
        {
            if (_regions.Count == 1)
            {
                _tail = _regions.First();
            }
            return Equals(_tail.GetTail(), layer);
        }

        /// <summary>
        /// Returns a {@link Iterator} capable of walking the tree of regions
        /// from the root <see cref="Region"/> down through all the child Regions. In turn,
        /// a <see cref="Region"/> may be queried for a {@link Iterator} which will return
        /// an iterator capable of traversing the Region's contained <see cref="ILayer"/>s.
        /// </summary>
        /// <returns></returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        /// <summary>
        /// Returns a {@link Iterator} capable of walking the tree of regions
        /// from the root <see cref="Region"/> down through all the child Regions. In turn,
        /// a <see cref="Region"/> may be queried for a {@link Iterator} which will return
        /// an iterator capable of traversing the Region's contained <see cref="ILayer"/>s.
        /// </summary>
        /// <returns></returns>
        public IEnumerator<Region> GetEnumerator()
        {
            return _regions.GetEnumerator();
        }

        /// <summary>
        /// Used to manually input data into a <see cref="Network"/>, the other way 
        /// being the call to <see cref="Start"/> for a Network that contains a
        /// Region that contains a <see cref="ILayer"/> which in turn contains a <see cref="ISensor"/> <em>-OR-</em>
        /// subscribing a receiving Region to this Region's output Observable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)</param>
        public void Compute<T>(T input)
        {
            if (_tail == null && _regions.Count == 1)
            {
                _tail = _regions[0];
            }

            if (_head == null)
            {
                AddDummySubscriber();
            }

            _tail?.Compute(input);
        }

        /// <summary>
        /// Used to manually input data into a <see cref="Network"/>, the other way 
        /// being the call to <see cref="Start"/> for a Network that contains a
        /// Region that contains a <see cref="ILayer"/> which in turn contains a <see cref="ISensor"/> <em>-OR-</em>
        /// subscribing a receiving Region to this Region's output Observable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="input">One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)</param>
        public IInference ComputeImmediate<T>(T input)
        {
            if (_isThreadRunning)
            {
                throw new InvalidOperationException("Cannot call computeImmediate() when Network has been started.");
            }

            if (_tail == null && _regions.Count == 1)
            {
                _tail = _regions[0];
            }

            if (_head == null)
            {
                AddDummySubscriber();
            }

            if(_tail == null)throw new InvalidOperationException("Network is not closed?");
            if(_head == null)throw new InvalidOperationException("Network is not closed?");
            _tail.Compute(input);
            return _head.GetHead().GetInference();
        }

        /// <summary>
        /// Added when a synchronous call is made and there is no subscriber. No
        /// Subscriber leads to the observable chain not being constructed, therefore
        /// we must always have at least one subscriber.
        /// </summary>
        internal void AddDummySubscriber()
        {
            Observe().Subscribe(output => { }, e => Console.WriteLine(e));
        }

        /// <summary>
        /// Connects the specified source to the specified sink (the order of
        /// processing flows from source to sink, or lower level region to higher
        /// level region). 
        /// </summary>
        /// <param name="regionSink">the receiving end of the connection</param>
        /// <param name="regionSource">the source end of the connection</param>
        /// <returns>the current network</returns>
        public Network Connect(string regionSink, string regionSource)
        {
            Region source = Lookup(regionSource);
            if (source == null)
            {
                throw new ArgumentException("Region with name: " + regionSource + " not added to Network.");
            }

            Region sink = Lookup(regionSink);
            if (sink == null)
            {
                throw new ArgumentException("Region with name: " + regionSink + " not added to Network.");
            }

            sink.Connect(source);

            _tail = _tail ?? source;
            _head = _head ?? sink;

            Region bottom = source;
            while ((bottom = bottom.GetUpstreamRegion()) != null)
            {
                _tail = bottom;
            }

            Region top = sink;
            while ((top = top.GetDownstreamRegion()) != null)
            {
                _head = top;
            }

            return this;
        }

        /// <summary>
        /// Adds a <see cref="Region"/> to this <see cref="Network"/>
        /// </summary>
        /// <param name="region"></param>
        /// <returns>The current network</returns>
        public Network Add(Region region)
        {
            _regions.Add(region);
            region.SetNetwork(this);
            return this;
        }

        /// <summary>
        /// Closes all <see cref="Region"/> objects in this <see cref="Network"/>
        /// </summary>
        /// <returns></returns>
        public Network Close()
        {
            _regions.ForEach(r => r.Close());
            return this;
        }

        /// <summary>
        /// Returns a list of the contained <see cref="Region"/>s.
        /// </summary>
        public List<Region> GetRegions()
        {
            return new List<Region>(_regions);
        }

        /// <summary>
        /// Sets a reference to the <see cref="Region"/> which contains the <see cref="ISensor"/> (if any).
        /// </summary>
        /// <param name="r"></param>
        public void SetSensorRegion(Region r)
        {
            _sensorRegion = r;
        }

        /// <summary>
        /// Returns a reference to the <see cref="Region"/> which contains the <see cref="ISensor"/>
        /// </summary>
        /// <returns>the Region which contains the Sensor</returns>
        public Region GetSensorRegion()
        {
            return _sensorRegion;
        }

        /// <summary>
        /// Returns the <see cref="Region"/> with the specified name or null if it doesn't exist within this <see cref="Network"/>
        /// </summary>
        /// <param name="regionName">Region name to look for</param>
        /// <returns></returns>
        public Region Lookup(string regionName)
        {
            return _regions.FirstOrDefault(r => r.GetName().Equals(regionName));
        }

        /// <summary>
        /// Returns the network-level <see cref="Parameters"/>.
        /// </summary>
        public Parameters GetParameters()
        {
            return _parameters;
        }

        /// <summary>
        /// Sets the reference to this <see cref="Network"/>'s Sensor
        /// </summary>
        /// <param name="otherSensor"></param>
        public void SetSensor(ISensor otherSensor)
        {
            _sensor = otherSensor;
            _sensor.InitEncoder(_parameters);
        }

        /// <summary>
        /// Returns the encoder present in one of this <see cref="Network"/>'s
        /// </summary>
        public ISensor GetSensor()
        {
            return _sensor;
        }

        /// <summary>
        ///  Sets the <see cref="MultiEncoder"/> on this Network
        /// </summary>
        /// <param name="e"></param>
        public void SetEncoder(MultiEncoder e)
        {
            _encoder = e;
        }

        /// <summary>
        /// Returns the <see cref="MultiEncoder"/> with which this Network is configured.
        /// </summary>
        /// <returns></returns>
        public MultiEncoder GetEncoder()
        {
            return _encoder;
        }

        /// <summary>
        /// Checks the name for suitability within a given network, checking for reserved characters and such.
        /// </summary>
        /// <param name="name"></param>
        private static void CheckName(string name)
        {
            if (name == null) throw new ArgumentNullException(nameof(name));
            if (name.IndexOf(":", StringComparison.Ordinal) != -1)
            {
                throw new ArgumentException("\":\" is a reserved character.");
            }
        }


        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + (_isLearn ? 1231 : 1237);
            result = prime * result + ((_name == null) ? 0 : _name.GetHashCode());
            result = prime * result + ((_parameters == null) ? 0 : _parameters.GetHashCode());
            result = prime * result + ((_regions == null) ? 0 : _regions.GetHashCode());
            result = prime * result + ((_sensor == null) ? 0 : _sensor.GetHashCode());
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
            Network other = (Network)obj;
            if (_isLearn != other._isLearn)
                return false;
            if (_name == null)
            {
                if (other._name != null)
                    return false;
            }
            else if (!_name.Equals(other._name))
                return false;
            if (_parameters == null)
            {
                if (other._parameters != null)
                    return false;
            }
            else if (!_parameters.Equals(other._parameters))
                return false;
            if (_regions == null)
            {
                if (other._regions != null)
                    return false;
            }
            else if (! _regions.SequenceEqual(other._regions))
                return false;
            if (_sensor == null)
            {
                if (other._sensor != null)
                    return false;
            }
            else if (!_sensor.Equals(other._sensor))
                return false;
            return true;
        }
    }
}