using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    /// <summary>
    /// Contains the Base properties of a layer
    /// </summary>
    [Serializable]
    public abstract class BaseLayer : Persistable<BaseLayer>, ILayer
    {
        /// <summary>
        /// Gets or sets the layer's current thread
        /// </summary>
        protected Task LayerThread;

        protected LayerMask AlgoContentMask = 0;
        /** Used to track and document the # of records processed */
        protected int _recordNum = -1;
        /** Keeps track of number of records to skip on restart */
        protected int _skip = -1;
        protected string Name;
        protected bool IsLearn = true;
        protected bool _isClosed;
        protected IInference CurrentInference;
        protected Network ParentNetwork;
        protected Region ParentRegion;

        protected Parameters Params;
        protected Connections Connections;
        protected IHTMSensor Sensor;
        protected MultiEncoder Encoder;
        protected SpatialPooler SpatialPooler;
        protected TemporalMemory TemporalMemory;
        protected bool? AutoCreateClassifiers;
        protected Anomaly AnomalyComputer;

        protected bool _isPostSerialized;

        /**
        * Creates a new {@code Layer} using the specified {@link Parameters}
        * 
        * @param name  the name identifier of this {@code Layer}
        * @param n     the parent <see cref="Network"/>
        * @param p     the {@link Parameters} to use with this {@code Layer}
        */
        protected BaseLayer(string name, Network n, Parameters p)
        {
            Name = name;
            ParentNetwork = n;
            Params = p;

            Connections = new Connections();

            AutoCreateClassifiers = (bool)p.GetParameterByKey(Parameters.KEY.AUTO_CLASSIFY);
        }

        protected BaseLayer(Parameters @params, MultiEncoder e, SpatialPooler sp, TemporalMemory tm,
            bool? autoCreateClassifiers, Anomaly a)
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

            InitializeMask();
        }

        /// <summary>
        /// Returns the next Layer following this Layer in order of process flow.
        /// </summary>
        public abstract ILayer GetNext();

        /// <summary>
        /// Returns the last computed <see cref="IInference"/> of this <see cref="BaseLayer"/>
        /// </summary>
        /// <returns>the last computed inference.</returns>
        public IInference GetInference()
        {
            return CurrentInference;
        }

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
        /// Returns a flag indicating whether this <see cref="BaseLayer"/> is configured 
        /// with a <see cref="ISensor"/> which requires starting up.
        /// </summary>
        /// <returns>true when a sensor is found</returns>
        public bool HasSensor()
        {
            return Sensor != null;
        }

        /// <summary>
        /// Returns the <see cref="Thread"/> from which this <see cref="ILayer"/> is currently outputting data.
        /// </summary>
        public Task GetLayerThread()
        {
            if (LayerThread != null)
            {
                return LayerThread;
            }
            // Get thread from tail layer
            if (ParentNetwork.GetTail().GetTail() != this)
            {
                return ParentNetwork.GetTail().GetTail().GetLayerThread();
            }
            
            throw new InvalidOperationException("No thread found?");
        }

        /// <summary>
        /// Returns the<see cref="Model.Connections"/> object being used as the structural matrix and state.
        /// </summary>
        /// <returns></returns>
        public Connections GetMemory()
        {
            return Connections;
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
        public abstract void Start();

        public abstract void Restart(bool startAtIndex);

        /// <summary>
        /// Stops the processing of this <see cref="ILayer"/>'s processing thread.
        /// </summary>
        public abstract void Halt();
        public abstract IObservable<IInference> Observe();
        public abstract ILayer Close();

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> has had
        /// its <see cref="Close()"/> method called, or not.
        /// </summary>
        public virtual bool IsClosed()
        {
            return _isClosed;
        }

        /// <summary>
        /// Initializes the algorithm content mask used for detection of repeated algorithms 
        /// among <see cref="BaseLayer"/>s in a <see cref="Region"/>
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
        /// Sets the learning mode.
        /// </summary>
        /// <param name="learningMode">true when in learning mode, false otherwise</param>
        public void SetLearn(bool learningMode)
        {
            IsLearn = learningMode;
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
        public abstract void Compute<TInput>(TInput t);

        public abstract int CalculateInputWidth();
        public Region GetParentRegion()
        {
            return ParentRegion;
        }

        /// <summary>
        /// Resets the <see cref="TemporalMemory"/> if it exists.
        /// </summary>
        public abstract void Reset();

        /// <summary>
        /// Notify all subscribers through the delegate that stream processing has been completed or halted.
        /// </summary>
        public abstract void NotifyComplete();
        /// <summary>
        /// Allows the user to define the <see cref="Connections"/> object data structure
        /// to use. Or possibly to share connections between two <see cref="ILayer"/>s
        /// </summary>
        /// <param name="connections">the <see cref="Connections"/> object to use.</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Using(Connections connections);

        /// <summary>
        /// Returns the learning mode setting.
        /// </summary>
        public bool SetIsLearn()
        {
            return IsLearn;
        }

        /// <summary>
        /// For internal use only. Returns a flag indicating whether this <see cref="BaseLayer"/>
        /// contains a <see cref="Algorithms.TemporalMemory"/>
        /// </summary>
        public bool HasTemporalMemory()
        {
            return (AlgoContentMask & LayerMask.TemporalMemory) == LayerMask.TemporalMemory;
        }

        /// <summary>
        /// For internal use only. Returns a flag indicating whether this <see cref="BaseLayer"/>
        /// contains a <see cref="Algorithms.SpatialPooler"/>
        /// </summary>
        public bool HasSpatialPooler()
        {
            return (AlgoContentMask & LayerMask.SpatialPooler) == LayerMask.SpatialPooler;
        }

        /// <summary>
        /// Returns the current spatial pooler if there is one
        /// </summary>
        public SpatialPooler GetSpatialPooler()
        {
            return SpatialPooler;
        }

        /// <summary>
        /// Returns the current temporal memory if there is one
        /// </summary>
        public TemporalMemory GetTemporalMemory()
        {
            return TemporalMemory;
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
        /// Returns the <see cref="Parameters"/> used to configure this layer.
        /// </summary>
        /// <returns></returns>
        public Parameters GetParameters()
        {
            return Params;
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
                throw new InvalidOperationException("Layer already \"closed\"");
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
        /// Returns the previous Layer preceding this Layer in order of process flow.
        /// </summary>
        public abstract ILayer GetPrevious();

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
        /// Returns the classifier(s) for this layer
        /// </summary>
        /// <returns></returns>
        public abstract IClassifier GetClassifier(MultiEncoder encoder, string predictedFieldName);

        /// <summary>
        /// Returns the count of records historically inputted into this
        /// </summary>
        /// <returns>the current record input count</returns>
        public int GetRecordNum()
        {
            return _recordNum;
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
        /// Increments the current record sequence number.
        /// </summary>
        public ILayer Increment()
        {
            ++_recordNum;
            return this;
        }

        /// <summary>
        /// Skips the specified count of records and internally alters the record
        /// sequence number.
        /// </summary>
        /// <param name="count"></param>
        /// <returns></returns>
        public ILayer Skip(int count)
        {
            _recordNum += count;
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
        public abstract ILayer Using(Parameters p);

        /// <summary>
        /// Adds an <see cref="HTMSensor{T}"/> to this <see cref="ILayer"/>. An HTMSensor is a regular
        /// <see cref="ISensor"/> (i.e. <see cref="FileSensor"/>, <see cref="URISensor"/>, or <see cref="ObservableSensor{T}"/>)
        /// which has had an <see cref="IEncoder"/> configured and added to it. HTMSensors are
        /// HTM Aware, where as regular Sensors have no knowledge of HTM requirements.
        /// </summary>
        /// <param name="sensor">the <see cref="HTMSensor{T}"/></param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Add(ISensor sensor);

        /// <summary>
        /// Adds a <see cref="MultiEncoder"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="encoder">the added MultiEncoder</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Add(MultiEncoder encoder);

        /// <summary>
        /// Adds a <see cref="Algorithms.SpatialPooler"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="sp">the added SpatialPooler</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Add(SpatialPooler sp);

        /// <summary>
        /// Adds a <see cref="Algorithms.TemporalMemory"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="tm">the added TemporalMemory</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Add(TemporalMemory tm);

        /// <summary>
        /// Adds an <see cref="Anomaly"/> computer to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="anomalyComputer">the Anomaly instance</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        public abstract ILayer Add(Anomaly anomalyComputer);

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
        public abstract ILayer Add(Func<ManualInput, ManualInput> func);
    }
}