using System;
using System.Collections.Generic;
using System.Linq;
using System.Reactive;
using HTM.Net.Model;
using HTM.Net.Util;
using log4net;

namespace HTM.Net.Network
{
    /// <summary>
    /// <p>
    /// Regions are collections of <see cref="ILayer"/>s, which are in turn collections
    /// of algorithmic components. Regions can be connected to each other to establish
    /// a hierarchy of processing. To connect one Region to another, typically one 
    /// would do the following:
    /// </p>
    /// <pre>
    ///      Parameters p = Parameters.GetDefaultParameters(); // May be altered as needed
    ///      Network n = Network.Create("Test Network", p);
    ///      Region region1 = n.CreateRegion("r1"); // would typically add Layers to the Region after this
    ///      Region region2 = n.CreateRegion("r2"); 
    ///      region1.Connect(region2);
    /// </pre>
    /// <b>--OR--</b>
    /// <pre>
    ///      n.Connect(region1, region2);
    /// </pre>
    /// <b>--OR--</b>
    /// <pre>
    ///      Network.Lookup("r1").Connect(Network.Lookup("r2"));
    /// </pre>    
    /// </summary>
    [Serializable]
    public class Region : Persistable
    {
        #region Fields

        private const long SerialVersionUid = 1;

        [NonSerialized]
        private static readonly ILog Logger = LogManager.GetLogger(typeof(Region));

        private Network _parentNetwork;
        private Region _upstreamRegion;
        private Region _downstreamRegion;
        private ILayer _tail;
        private ILayer _head;

        private readonly Map<string, Layer<IInference>> _layers = new Map<string, Layer<IInference>>();

        [NonSerialized]
        private IObservable<IInference> _regionObservable;

        /// <summary>
        /// Marker flag to indicate that assembly is finished and Region initialized
        /// </summary>
        private bool _assemblyClosed;

        /// <summary>
        /// stores the learn setting
        /// </summary>
        private bool _isLearn = true;

        // Temporary variables used to determine endpoints of observable chain
        private HashSet<Layer<IInference>> _sources;
        private HashSet<Layer<IInference>> _sinks;

        /// <summary>
        /// Stores the overlap of algorithms state for <see cref="IInference"/> sharing determination
        /// </summary>
        internal LayerMask _flagAccumulator = 0;

        /// <summary>
        /// Indicates whether algorithms are repeated, if true then no, if false then yes
        /// (for <see cref="IInference"/> sharing determination) see <see cref="ConfigureConnection{I,O}"/> {@link Region#connect(Layer, Layer)} 
        /// and <see cref="Layer{T}.GetMask"/>
        /// </summary>
        internal bool _layersDistinct = true;

        private object _input;

        private readonly string _name; 

        #endregion

        /// <summary>
        /// Constructs a new <see cref="Region"/>
        /// </summary>
        /// <param name="name">A unique identifier for this Region (uniqueness is enforced)</param>
        /// <param name="network">The containing <see cref="Network"/> </param>
        public Region(string name, Network network)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                throw new InvalidOperationException("Name may not be null or empty. " +
                                                    "...not that anyone here advocates name calling!");
            }

            _name = name;
            _parentNetwork = network;
        }

        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called prior to this object being serialized. Any
        /// preparation required for serialization should be done
        /// in this method.
        /// </summary>
        /// <returns></returns>
        public override object PreSerialize()
        {
            _layers.Values.ToList().ForEach(l => l.PreSerialize());
            return this;
        }

        /// <summary>
        /// <em>FOR INTERNAL USE ONLY</em>
        /// Called following deserialization to execute logic required
        /// to "fix up" any inconsistencies within the object being
        /// reified.
        /// </summary>
        /// <returns></returns>
        public override object PostDeSerialize()
        {
            _layers.Values.ToList().ForEach(l => l.PostDeSerialize());

            // Connect Layer Observable chains (which are transient so we must 
            // rebuild them and their subscribers)
            if (IsMultiLayer())
            {
                Layer<IInference> curr = (Layer<IInference>)_head;
                Layer<IInference> prev = (Layer<IInference>)curr.GetPrevious();
                do
                {
                    Connect(curr, prev);
                } while ((curr = prev) != null && (prev = (Layer<IInference>)prev.GetPrevious()) != null);
            }
            return this;
        }

        /// <summary>
        /// Sets the parent <see cref="Network"/> of this <see cref="Region"/>
        /// </summary>
        /// <param name="network"></param>
        public void SetNetwork(Network network)
        {
            _parentNetwork = network;
            foreach (Layer<IInference> l in _layers.Values)
            {
                l.SetNetwork(network);
                // Set the sensor & encoder reference for global access.
                if (l.HasSensor() && network != null)
                {
                    network.SetSensor(l.GetSensor());
                    network.SetEncoder(l.GetSensor().GetEncoder());
                }
                else if (network != null && l.GetEncoder() != null)
                {
                    network.SetEncoder(l.GetEncoder());
                }
            }
        }

        /// <summary>
        /// Returns a flag indicating whether this <see cref="Region"/> contain multiple <see cref="ILayer"/>s.
        /// </summary>
        /// <returns>true if so, false if not.</returns>
        public bool IsMultiLayer()
        {
            return _layers.Count > 1;
        }

        /// <summary>
        /// Closes the Region and completes the finalization of its assembly.
        /// After this call, any attempt to mutate the structure of a Region
        /// will result in an <see cref="InvalidOperationException"/> being thrown.
        /// </summary>
        /// <returns></returns>
        public Region Close()
        {
            if (_layers.Count < 1)
            {
                Logger.Warn("Closing region: " + _name + " before adding contents.");
                return this;
            }

            CompleteAssembly();

            ILayer l = _tail;
            do
            {
                l.Close();
            } while ((l = l.GetNext()) != null);

            return this;
        }

        /// <summary>
        /// Returns a flag indicating whether this <see cref="Region"/> has 
        /// had its <see cref="Close"/> method called, or not.
        /// </summary>
        public bool IsClosed()
        {
            return _assemblyClosed;
        }

        /// <summary>
        /// Sets the learning mode.
        /// </summary>
        /// <param name="learningMode"></param>
        public void SetLearn(bool learningMode)
        {
            _isLearn = learningMode;
            ILayer l = _tail;
            while (l != null)
            {
                l.SetLearn(learningMode);
                l = l.GetNext();
            }
        }

        /// <summary>
        /// Returns the learning mode setting.
        /// </summary>
        public bool IsLearn()
        {
            return _isLearn;
        }

        /**
         
         * 
         * @param input One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)
         */
        /// <summary>
        /// Used to manually input data into a <see cref="Region"/>, the other way 
        /// being the call to <see cref="Start"/> for a Region that contains
        /// a <see cref="ILayer"/> which in turn contains a <see cref="Sensor.ISensor"/> <em>-OR-</em>
        /// subscribing a receiving Region to this Region's output Observable.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="inputToCompute">One of (int[], String[], <see cref="ManualInput"/>, or Map&lt;String, Object&gt;)</param>
        public void Compute<T>(T inputToCompute)
        {
            if (!_assemblyClosed)
            {
                Close();
            }
            _input = inputToCompute;
            _tail.Compute(inputToCompute);
        }

        /// <summary>
        /// Returns the current input into the region. 
        /// This value may change after every call to <see cref="Compute{T}"/>.
        /// </summary>
        /// <returns></returns>
        public object GetInput()
        {
            return _input;
        }

        /// <summary>
        /// Adds the specified <see cref="ILayer"/> to this <see cref="Region"/>. 
        /// </summary>
        /// <param name="l"></param>
        /// <exception cref="RegionAlreadyClosedException">if Region is already closed</exception>
        /// <exception cref="InvalidOperationException">if a Layer with the same name already exists.</exception>
        public Region Add(ILayer l)
        {
            if (_assemblyClosed)
            {
                throw new RegionAlreadyClosedException("Cannot add Layers when Region has already been closed.");
            }

            if (_sources == null)
            {
                _sources = new HashSet<Layer<IInference>>();
                _sinks = new HashSet<Layer<IInference>>();
            }

            // Set the sensor reference for global access.
            if (l.HasSensor() && _parentNetwork != null)
            {
                _parentNetwork.SetSensor(l.GetSensor());
                _parentNetwork.SetEncoder(l.GetSensor().GetEncoder());
            }

            string layerName = _name + ":" + l.GetName();
            if (_layers.ContainsKey(layerName))
            {
                throw new InvalidOperationException("A Layer with the name: " + l.GetName() + " has already been added to this Region.");
            }

            l.SetName(layerName);
            _layers.Add(l.GetName(), (Layer<IInference>)l);
            l.SetRegion(this);
            l.SetNetwork(_parentNetwork);

            return this;
        }

        /// <summary>
        /// Returns the String identifier for this <see cref="Region"/>
        /// </summary>
        public string GetName()
        {
            return _name;
        }

        /// <summary>
        /// Returns an <see cref="IObservable{T}"/> which can be used to receive
        /// <see cref="IInference"/> emissions from this <see cref="Region"/>
        /// </summary>
        public IObservable<IInference> Observe()
        {
            if (_regionObservable == null && !_assemblyClosed)
            {
                Close();
            }
            if (_head.IsHalted() || _regionObservable == null)
            {
                _regionObservable = _head.Observe();
            }
            return _regionObservable;
        }

        /// <summary>
        /// Calls <see cref="ILayer.Start"/> on this Region's input <see cref="ILayer"/> if 
        /// that layer contains a <see cref="Sensor.ISensor"/>. If not, this method has no 
        /// effect.
        /// </summary>
        /// <returns>flag indicating that thread was started</returns>
        public bool Start()
        {
            if (!_assemblyClosed)
            {
                Close();
            }

            if (_tail.HasSensor())
            {
                Logger.Info("Starting Region [" + GetName() + "] input Layer thread.");
                _tail.Start();
                return true;
            }
            else
            {
                Logger.Warn("Start called on Region [" + GetName() + "] with no effect due to no Sensor present.");
            }

            return false;
        }

        /// <summary>
        /// Calls <see cref="ILayer.Restart"/> on this Region's input <see cref="ILayer"/> if
        /// that layer contains a <see cref="Sensor.ISensor"/>. If not, this method has no effect. If
        /// "startAtIndex" is true, the Network will start at the last saved index as 
        /// obtained from the serialized "recordNum" field; if false then the Network
        /// will restart from 0.
        /// </summary>
        /// <param name="startAtIndex">
        /// flag indicating whether to start from the previous save point or not. 
        /// If true, this region's Network will start at the previously stored index, 
        /// if false then it will start with a recordNum of zero.
        /// </param>
        /// <returns>flag indicating whether the call to restart had an effect or not.</returns>
        public bool Restart(bool startAtIndex)
        {
            if (!_assemblyClosed)
            {
                return Start();
            }

            if (_tail.HasSensor())
            {
                Logger.Info("Re-Starting Region [" + GetName() + "] input Layer thread.");
                _tail.Restart(startAtIndex);
                return true;
            }
            else
            {
                Logger.Warn("Re-Start called on Region [" + GetName() + "] with no effect due to no Sensor present.");
            }

            return false;
        }

        /// <summary>
        /// Returns an Observable operator that when subscribed to, invokes an operation
        /// that stores the state of this <see cref="Network"/> while keeping the Network up and running.
        /// The Network will be stored at the pre-configured location (in binary form only, not JSON).
        /// </summary>
        /// <returns>the <see cref="ICheckPointOp{T}"/> operator </returns>
        internal ICheckPointOp<byte[]> GetCheckPointOperator()
        {
            Logger.Debug("Region [" + GetName() + "] CheckPoint called at: " + (new DateTime()));
            if (_tail != null)
            {
                return _tail.GetCheckPointOperator();
            }
            Close();
            if(_tail==null)throw new InvalidOperationException("Something went wrong with closing this region for the tail");
            return _tail.GetCheckPointOperator();
        }

        /// <summary>
        /// Stops each <see cref="ILayer"/> contained within this <see cref="Region"/>
        /// </summary>
        public void Halt()
        {
            Logger.Debug("Stop called on Region [" + GetName() + "]");
            if (_tail != null)
            {
                _tail.Halt();
            }
            else
            {
                Close();
                _tail?.Halt();
            }
            Logger.Debug("Region [" + GetName() + "] stopped.");
        }

        /// <summary>
        /// Returns a flag indicating whether this Region has a Layer whose Sensor thread is halted.
        /// </summary>
        /// <returns>true if so, false if not</returns>
        public bool IsHalted()
        {
            if (_tail != null)
            {
                return _tail.IsHalted();
            }
            return false;
        }

        /// <summary>
        /// Finds any <see cref="ILayer"/> containing a <see cref="Algorithms.TemporalMemory"/> and resets them.
        /// </summary>
        public void Reset()
        {
            foreach (var l in _layers.Values)
            {
                if (l.HasTemporalMemory())
                {
                    l.Reset();
                }
            }
        }

        /// <summary>
        /// Resets the recordNum in all <see cref="ILayer"/>s.
        /// </summary>
        public void ResetRecordNum()
        {
            foreach (var l in _layers.Values)
            {
                l.ResetRecordNum();
            }
        }

        /// <summary>
        /// Connects the output of the specified <see cref="Region"/> to the input of this Region
        /// </summary>
        /// <param name="inputRegion">the Region who's emissions will be observed by this Region.</param>
        public Region Connect(Region inputRegion)
        {
            ManualInput localInf = new ManualInput();
            inputRegion.Observe().Subscribe(output =>
            {
                localInf.SetSdr(output.GetSdr()).SetRecordNum(output.GetRecordNum()).SetClassifierInput(output.GetClassifierInput()).SetLayerInput(output.GetSdr());
                if (output.GetSdr().Length > 0)
                {
                    ((Layer<IInference>)_tail).Compute(localInf);
                }
            }, Console.WriteLine, () =>
            {
                ((Layer<IInference>)_tail).NotifyComplete();
            });

            // Set the upstream region
            _upstreamRegion = inputRegion;
            inputRegion._downstreamRegion = this;

            return this;
        }

        /// <summary>
        /// Returns this <see cref="Region"/>'s upstream region, if it exists.
        /// </summary>
        public Region GetUpstreamRegion()
        {
            return _upstreamRegion;
        }

        /// <summary>
        /// Returns the <see cref="Region"/> that receives this Region's output.
        /// </summary>
        public Region GetDownstreamRegion()
        {
            return _downstreamRegion;
        }

        /// <summary>
        /// Returns the top-most (last in execution order from bottom to top) 
        /// <see cref="ILayer"/> in this <see cref="Region"/>
        /// </summary>
        public ILayer GetHead()
        {
            return _head;
        }

        /// <summary>
        /// Returns the bottom-most (first in execution order from bottom to top) <see cref="ILayer"/> in this <see cref="Region"/>
        /// </summary>
        public ILayer GetTail()
        {
            return _tail;
        }

        /// <summary>
        /// Connects two layers to each other in a unidirectional fashion 
        /// with "toLayerName" representing the receiver or "sink" and "fromLayerName"
        /// representing the sender or "source".
        /// </summary>
        /// <param name="toLayerName">the name of the sink layer</param>
        /// <param name="fromLayerName">the name of the source layer</param>
        /// <exception cref="RegionAlreadyClosedException">if Region is already closed</exception>
        /// <exception cref="InvalidOperationException">layers not found</exception>
        public Region Connect(string toLayerName, string fromLayerName)
        {
            if (_assemblyClosed)
            {
                throw new RegionAlreadyClosedException("Cannot connect Layers when Region has already been closed.");
            }

            Layer<IInference> @in = (Layer<IInference>)Lookup(toLayerName);
            Layer<IInference> @out = (Layer<IInference>)Lookup(fromLayerName);
            if (@in == null)
            {
                throw new InvalidOperationException("Could not lookup (to) Layer with name: " + toLayerName);
            }
            if (@out == null)
            {
                throw new InvalidOperationException("Could not lookup (from) Layer with name: " + fromLayerName);
            }

            // Set source's pointer to its next Layer --> (sink : going upward).
            @out.Next(@in);
            // Set the sink's pointer to its previous Layer --> (source : going downward)
            @in.Previous(@out);
            // Connect out to in
            ConfigureConnection(@in, @out);
            Connect(@in, @out);

            return this;
        }

        /// <summary>
        /// Does a straight associative lookup by first creating a composite
        /// key containing this <see cref="Region"/>'s name concatenated with the specified
        /// <see cref="ILayer"/>'s name, and returning the result.
        /// </summary>
        /// <param name="layerName">name of the layer</param>
        public ILayer Lookup(string layerName)
        {
            if (layerName.IndexOf(":", StringComparison.Ordinal) != -1)
            {
                return _layers[layerName];
            }
            string key = _name + ":" + layerName;
            if (_layers.ContainsKey(key))
            {
                return _layers[key];
            }
            return null;
        }

        /// <summary>
        /// Called by <see cref="Start"/>, <see cref="Observe"/> and <see cref="Connect(HTM.Net.Network.Region)"/>
        /// to finalize the internal chain of <see cref="ILayer"/>s contained by this <see cref="Region"/>.
        /// This method assigns the head and tail Layers and composes the <see cref="IObservable{T}"/>
        /// which offers this Region's emissions to any upstream <see cref="Region"/>s.
        /// </summary>
        private void CompleteAssembly()
        {
            if (!_assemblyClosed)
            {
                if (_layers.Count == 0) return;

                if (_layers.Count == 1)
                {
                    var enumerator = _layers.Values.GetEnumerator();
                    if (enumerator.MoveNext())
                    {
                        _head = _tail = enumerator.Current;
                    }
                    else
                    {
                        throw new InvalidOperationException("No layers to iterate through?");
                    }
                }

                if (_tail == null)
                {
                    HashSet<Layer<IInference>> temp = new HashSet<Layer<IInference>>(_sources);
                    temp.ExceptWith(_sinks);
                    if (temp.Count != 1)
                    {
                        throw new InvalidOperationException("Detected misconfigured Region too many or too few sinks.");
                    }
                    var enumerator = temp.GetEnumerator();
                    enumerator.MoveNext();
                    _tail = enumerator.Current;
                }

                if (_head == null)
                {
                    HashSet<Layer<IInference>> temp = new HashSet<Layer<IInference>>(_sinks);
                    temp.ExceptWith(_sources);
                    if (temp.Count != 1)
                    {
                        throw new InvalidOperationException("Detected misconfigured Region too many or too few sources.");
                    }
                    var enumerator = temp.GetEnumerator();
                    enumerator.MoveNext();
                    _head = enumerator.Current;
                }

                if(_head == null) throw new InvalidOperationException("Something went wrong in closing the region, no head found.");
                _regionObservable = _head.Observe();

                _assemblyClosed = true;
            }
        }

        /// <summary>
        /// Called internally to configure the connection between two <see cref="ILayer"/> 
        /// Observables taking care of other connection details such as passing
        /// the inference up the chain and any possible encoder.
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="in">the sink end of the connection between two layers</param>
        /// <param name="out">the source end of the connection between two layers</param>
        /// <exception cref="RegionAlreadyClosedException">if Region is already closed</exception>
        private void ConfigureConnection<TIn, TOut>(TIn @in, TOut @out)
            where TIn : Layer<IInference>
            where TOut : Layer<IInference>
        {
            if (_assemblyClosed)
            {
                throw new RegionAlreadyClosedException("Cannot add Layers when Region has already been closed.");
            }

            HashSet<ILayer> all = new HashSet<ILayer>(_sources);
            all.UnionWith(_sinks);
            LayerMask inMask = @in.GetMask();
            LayerMask outMask = @out.GetMask();
            if (!all.Contains(@out))
            {
                _layersDistinct = (int)(_flagAccumulator & outMask) < 1;
                _flagAccumulator |= outMask;
            }
            if (!all.Contains(@in))
            {
                _layersDistinct = (int)(_flagAccumulator & inMask) < 1;
                _flagAccumulator |= inMask;
            }

            _sources.Add(@out);
            _sinks.Add(@in);
        }

        /// <summary>
        /// Called internally to "connect" two <see cref="ILayer"/> <see cref="IObservable{T}"/>s
        /// taking care of other connection details such as passing the inference
        /// up the chain and any possible encoder.
        /// </summary>
        /// <typeparam name="TIn"></typeparam>
        /// <typeparam name="TOut"></typeparam>
        /// <param name="in">the sink end of the connection between two layers</param>
        /// <param name="out">the source end of the connection between two layers</param>
        /// <exception cref="RegionAlreadyClosedException">if Region is already closed</exception>
        private void Connect<TIn, TOut>(TIn @in, TOut @out) // <I extends Layer<IInference>, O extends Layer<IInference>> 
            where TIn : Layer<IInference>
            where TOut : Layer<IInference>
        {
            ManualInput localInf = new ManualInput();

            @out.Subscribe(Observer.Create<IInference>(i =>
            {
                if (_layersDistinct)
                {
                    @in.Compute(i);
                }
                else
                {
                    localInf.SetSdr(i.GetSdr()).SetRecordNum(i.GetRecordNum()).SetLayerInput(i.GetSdr());
                    @in.Compute(localInf);
                }
            }, Console.WriteLine, () =>
            {
                @in.NotifyComplete();
            }));
        }

        public override int GetHashCode()
        {
            const int prime = 31;
            int result = 1;
            result = prime * result + (_assemblyClosed ? 1231 : 1237);
            result = prime * result + (_isLearn ? 1231 : 1237);
            result = prime * result + ((_layers == null) ? 0 : _layers.Count);
            result = prime * result + ((_name == null) ? 0 : _name.GetHashCode());
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
            Region other = (Region)obj;
            if (_assemblyClosed != other._assemblyClosed)
                return false;
            if (_isLearn != other._isLearn)
                return false;
            if (_layers == null)
            {
                if (other._layers != null)
                    return false;
            }
            else if (!_layers.Equals(other._layers))
                return false;
            if (_name == null)
            {
                if (other._name != null)
                    return false;
            }
            else if (!_name.Equals(other._name))
                return false;
            return true;
        }
    }
}