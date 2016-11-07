using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using log4net;
using log4net.Repository.Hierarchy;

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
    /// <p>
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
    public class Network : Persistable
    {
        [NonSerialized]
        public readonly static ILog Logger = LogManager.GetLogger(typeof(Network));
        public enum Mode { MANUAL, AUTO, REACTIVE };

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

        private Func<Network, byte[]> _checkPointFunction;

        private bool _shouldDoHalt = true;

        /// <summary>
        /// Creates a new <see cref="Network"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        public Network(string name, Parameters parameters)
        {
            if (name == null || string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("All Networks must have a name. " +
                    "Increases digestion, and overall happiness!");
            }
            _name = name;
            _parameters = parameters;
            if (parameters == null)
            {
                throw new ArgumentException("Network Parameters were null.");
            }
        }

        /// <summary>
        /// Creates and returns an implementation of <see cref="Network"/>
        /// </summary>
        /// <param name="name"></param>
        /// <param name="parameters"></param>
        /// <returns></returns>
        public static Network Create(string name, Parameters parameters)
        {
            return new Network(name, parameters);
        }

        /**
         * Creates and returns a child <see cref="Region"/> of this {@code Network}
         * 
         * @param   name    The String identifier for the specified <see cref="Region"/>
         * @return
         */
        public static Region CreateRegion(string name)
        {
            CheckName(name);

            Region r = new Region(name, null);
            return r;
        }

        /**
         * Creates a {@link Layer} to hold algorithmic components and returns
         * it.
         * 
         * @param name  the String identifier for the specified {@link Layer}
         * @param p     the {@link Parameters} to use for the specified {@link Layer}
         * @return
         */
        public static Layer<T> CreateLayer<T>(string name, Parameters p)
        {
            CheckName(name);
            return new Layer<T>(name, null, p);
        }

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
                    this._tail = _regions.First();
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

        /**
         * INTERNAL METHOD: DO NOT CALL
         * 
         * Called from {@link Layer} to execute a check point from within the scope of 
         * this {@link Network}
         * checkPointFunction
         * @return  the serialized {@code Network} in byte array form.
         */
        internal byte[] InternalCheckPointOp()
        {
            _shouldDoHalt = false;
            byte[] serializedBytes = (byte[])_checkPointFunction(this);
            _shouldDoHalt = true;
            return serializedBytes;
        }

        /**
         * Sets the reference to the check point function.
         * @param f function which executes check point logic.
         */
        public void SetCheckPointFunction<T>(Func<Network, byte[]> f)
        {
            this._checkPointFunction = f;
        }

        /**
         * USED INTERNALLY, DO NOT CALL
         * Returns an {@link rx.Observable} operator that when subscribed to, invokes an operation
         * that stores the state of this {@code Network} while keeping the Network up and running.
         * The Network will be stored at the pre-configured location (in binary form only, not JSON).
         * 
         * @return  the {@link CheckPointOp} operator 
         */
        internal ICheckPointOp<byte[]> GetCheckPointOperator()
        {
            Logger.Debug("Network [" + GetName() + "] called checkPoint() at: " + (new DateTime()));

            if (_regions.Count == 1)
            {
                this._tail = _regions.First();
            }
            return _tail.GetCheckPointOperator();
        }

        /**
         * Restarts this {@code Network}. The network will run from the previous save point
         * of the stored Network.
         * 
         * @see {@link #restart(boolean)} for a start at "saved-index" behavior explanation. 
         */
        public void Restart()
        {
            Restart(true);
        }

        /**
         * Restarts this {@code Network}. If the "startAtIndex" flag is true, the Network
         * will start from the last record number (plus 1) at which the Network was saved -
         * continuing on from where it left off. The Network will achieve this by rebuilding
         * the underlying Stream (if necessary, i.e. not for {@link ObservableSensor}s) and skipping 
         * the number of records equal to the stored record number plus one, continuing from where it left off.
         * 
         * @param startAtIndex  flag indicating whether to start this {@code Network} from
         *                      its previous save point.
         */
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
            this._isThreadRunning = tail.Restart(startAtIndex);
        }

        /**
         * <p>
         * DO NOT CALL THIS METHOD!
         * </p><p>
         * Called internally by an {@link ObservableSensor}'s factory method's creation of a new 
         * {@code ObservableSensor}. This would usually happen following a halt or
         * deserialization.
         * </p>
         * @param p  the new Publisher created upon reconstitution of a new ObservableSensor  
         */

        internal void SetPublisher(Publisher p)
        {
            this._publisher = p;
            _publisher.SetNetwork(this);
        }

        /**
         * Returns the new {@link Publisher} created after halt or deserialization
         * of this {@code Network}, when a new Publisher must be created.
         * 
         * @return      the new Publisher created after deserialization or halt.
         * @see #getPublisherSupplier()
         */
        public Publisher GetPublisher()
        {
            if (_publisher == null)
            {
                throw new NullReferenceException("A Supplier must be built first. " +
                    "please see Network.getPublisherSupplier()");
            }
            return _publisher;
        }

        /**
        * Returns a flag indicating whether this {@code Network} contain multiple
        * {@link Region}s.
        * 
        * @return  true if so, false if not.
        */
        public bool IsMultiRegion()
        {
            return _regions.Count > 1;
        }

        /**
         * Returns the String identifier for this {@code Network}
         * @return
         */
        public string GetName()
        {
            return _name;
        }

        /**
         * If {@link Network.Mode} == {@link Mode#AUTO}, calling this 
         * method will start the main engine thread which pulls in data
         * from the connected {@link Sensor}(s).
         * 
         * <em>Warning:</em> Calling this method with any other Mode than 
         * {@link Mode#AUTO} will result in an {@link UnsupportedOperationException}
         * being thrown.
         */
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
        /// Closes all <see cref="Region"/> objects in this <see cref="Network"/>
        /// </summary>
        /// <returns></returns>
        public Network Close()
        {
            _regions.ForEach(r => r.Close());
            return this;
        }

        /**
         * Returns a flag indicating that the {@code Network} has an <see cref="IObservable{T}"/>
         * running on a thread.
         * 
         * @return  a flag indicating if threaded.
         */
        public bool IsThreadedOperation()
        {
            return _isThreadRunning;
        }

        /**
         * Halts this {@code Network}, stopping all threads and closing
         * all {@link SensorFactory} connections to incoming data, freeing up 
         * any resources associated with the input connections.
         */
        public void Halt()
        {
            if (_regions.Count == 1)
            {
                _tail = _regions[0];
            }
            _tail.Halt();
        }

        /**
         * Pauses all underlying {@code Network} nodes, maintaining any 
         * connections (leaving them open until they possibly time out).
         * Does nothing to prevent any sensor connections from timing out
         * on their own. 
         */
        public void Pause()
        {
            throw new InvalidOperationException("Pausing is not (yet) supported.");
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

        /**
         * Returns the learning mode setting.
         * @return
         */
        public bool IsLearn()
        {
            return _isLearn;
        }

        /**
         * Returns the current {@link Mode} with which this <see cref="Network"/> is 
         * currently configured.
         * 
         * @return
         */
        public Mode? GetMode()
        {
            // TODO Auto-generated method stub
            return null;
        }

        /**
         * Finds any <see cref="Region"/> containing a {@link Layer} which contains a {@link TemporalMemory} 
         * and resets them.
         */
        public void Reset()
        {
            foreach (Region r in _regions)
            {
                r.Reset();
            }
        }

        /**
         * Resets the recordNum in all <see cref="Region"/>s.
         */
        public void ResetRecordNum()
        {
            foreach (Region r in _regions)
            {
                r.ResetRecordNum();
            }
        }

        /**
         * Returns an <see cref="IObservable{T}"/> capable of emitting <see cref="IInference"/>s
         * which contain the results of this {@code Network}'s processing chain.
         * @return
         */
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

        /**
         * Returns the bottom-most (first in execution order from
         * bottom to top) <see cref="Region"/> in this {@code Network}
         * 
         * @return
         */
        public Region GetTail()
        {
            if (_regions.Count == 1)
            {
                _tail = _regions[0];
            }
            return _tail;
        }

        /**
         * For internal Use: Returns a boolean flag indicating whether
         * the specified {@link Layer} is the tail of the Network.
         * @param l     the layer to test   
         * @return  true if so, false if not
         */
        public bool IsTail(ILayer layer)
        {
            if (_regions.Count == 1)
            {
                this._tail = _regions.First();
            }
            return Equals(_tail.GetTail(), layer);
        }

        /**
         * Returns a {@link Iterator} capable of walking the tree of regions
         * from the root <see cref="Region"/> down through all the child Regions. In turn,
         * a <see cref="Region"/> may be queried for a {@link Iterator} which will return
         * an iterator capable of traversing the Region's contained {@link Layer}s.
         * 
         * @return
         */
        public List<Region>.Enumerator Iterator()
        {
            return GetRegions().GetEnumerator();
        }

        /**
         * Used to manually input data into a <see cref="Network"/>, the other way 
         * being the call to {@link Network#start()} for a Network that contains a
         * Region that contains a {@link Layer} which in turn contains a {@link Sensor} <em>-OR-</em>
         * subscribing a receiving Region to this Region's output Observable.
         * 
         * @param input One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)
         */
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

            _tail.Compute(input);
        }

        /**
         * Used to manually input data into a <see cref="Network"/> in a synchronous way, the other way 
         * being the call to {@link Network#start()} for a Network that contains a
         * Region that contains a {@link Layer} which in turn contains a {@link Sensor} <em>-OR-</em>
         * subscribing a receiving Region to this Region's output Observable.
         * 
         * @param input One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)
         */
        public IInference ComputeImmediate<T>(T input)
        //where T : IInference
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

            _tail.Compute(input);
            return _head.GetHead().GetInference();
        }

        /**
         * Added when a synchronous call is made and there is no subscriber. No
         * Subscriber leads to the observable chain not being constructed, therefore
         * we must always have at least one subscriber.
         */
        internal void AddDummySubscriber()
        {
            Observe().Subscribe(output => { }, e => Console.WriteLine(e));

            //        Observe().Subscribe(new Subscriber<IInference>() {
            //        @Override public void onCompleted() { }
            //    @Override public void onError(Throwable e) { e.printStackTrace(); }
            //    @Override public void onNext(IInference i) { }
            //});
        }

        /**
         * Connects the specified source to the specified sink (the order of
         * processing flows from source to sink, or lower level region to higher
         * level region). 
         * @param regionSink        the receiving end of the connection
         * @param regionSource      the source end of the connection
         * 
         * @return  this {@code Network}
         */
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

        /**
         * Adds a <see cref="Region"/> to this {@code Network}
         * @param region
         * @return
         */
        public Network Add(Region region)
        {
            _regions.Add(region);
            region.SetNetwork(this);
            return this;
        }

        /**
         * Returns a {@link List} view of the contained <see cref="Region"/>s.
         * @return
         */
        public List<Region> GetRegions()
        {
            return new List<Region>(_regions);
        }

        /**
         * Sets a reference to the <see cref="Region"/> which contains the {@link Sensor}
         * (if any).
         * 
         * @param r
         */
        public void SetSensorRegion(Region r)
        {
            _sensorRegion = r;
        }

        /**
         * Returns a reference to the <see cref="Region"/> which contains the {@link Sensor}
         * 
         * @return  the Region which contains the Sensor
         */
        public Region GetSensorRegion()
        {
            return _sensorRegion;
        }

        /**
         * Returns the <see cref="Region"/> with the specified name
         * or null if it doesn't exist within this {@code Network}
         * @param regionName
         * @return
         */
        public Region Lookup(string regionName)
        {
            return _regions.FirstOrDefault(r => r.GetName().Equals(regionName));
        }

        /**
         * Returns the network-level {@link Parameters}.
         * @return
         */
        public Parameters GetParameters()
        {
            return _parameters;
        }

        /**
         * Sets the reference to this {@code Network}'s Sensor
         * @param sensor
         */
        public void SetSensor(ISensor otherSensor)
        {
            _sensor = otherSensor;
            _sensor.InitEncoder(_parameters);
        }

        /**
         * Returns the encoder present in one of this {@code Network}'s
         * {@link Sensor}s
         * 
         * @return
         */
        public ISensor GetSensor()
        {
            return _sensor;
        }

        /**
         * Sets the {@link MultiEncoder} on this Network
         * @param e
         */
        public void SetEncoder(MultiEncoder e)
        {
            _encoder = e;
        }

        /**
         * Returns the {@link MultiEncoder} with which this Network is configured.
         * @return
         */
        public MultiEncoder GetEncoder()
        {
            return _encoder;
        }

        /**
         * Checks the name for suitability within a given network, 
         * checking for reserved characters and such.
         * 
         * @param name
         */
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