using System;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;

namespace HTM.Net.Network
{
    public interface ILayer : IPersistable
    {
        /// <summary>
        /// Returns the resident <see cref="MultiEncoder"/> or the encoder residing in this
        /// <see cref="ILayer"/>'s <see cref="ISensor"/>, if any.
        /// </summary>
        MultiEncoder GetEncoder();

        /// <summary>
        /// Returns the <see cref="Parameters"/> used to configure this layer.
        /// </summary>
        /// <returns></returns>
        Parameters GetParameters();

        /// <summary>
        /// Returns the previous Layer preceding this Layer in order of process flow.
        /// </summary>
        ILayer GetPrevious();

        /// <summary>
        /// Returns the next Layer following this Layer in order of process flow.
        /// </summary>
        ILayer GetNext();

        /// <summary>
        /// Returns the <see cref="Task"/> from which this <see cref="ILayer"/> is currently outputting data.
        /// 
        /// </summary>
        Task GetLayerThread();

        /// <summary>
        /// Finalizes the initialization in one method call so that side effect
        /// operations to share objects and other special initialization tasks can
        /// happen all at once in a central place for maintenance ease.
        /// </summary>
        /// <returns>Layer instance</returns>
        ILayer Close();

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
        void Compute<TInput>(TInput t);

        /// <summary>
        /// Sets the learning mode.
        /// </summary>
        /// <param name="learningMode">true when in learning mode, false otherwise</param>
        void SetLearn(bool learningMode);

        /// <summary>
        /// Returns the learning mode setting.
        /// </summary>
        bool IsLearn();

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> is configured 
        /// with a <see cref="ISensor"/> which requires starting up.
        /// </summary>
        /// <returns>true when a sensor is found</returns>
        bool HasSensor();

        /// <summary>
        /// Returns the configured <see cref="ISensor"/> if any exists in this <see cref="ILayer"/>, or null if one does not.
        /// </summary>
        /// <returns>any existing HTMSensor applied to this <see cref="ILayer"/></returns>
        ISensor GetSensor();

        /// <summary>
        /// Returns the <see cref="Model.Connections"/> object being used by this <see cref="ILayer"/>
        /// </summary>
        /// <returns>this <see cref="ILayer"/>'s <see cref="Model.Connections"/></returns>
        Connections GetConnections();

        /// <summary>
        /// Returns the String identifier of this <see cref="ILayer"/>
        /// </summary>
        /// <returns></returns>
        string GetName();

        /// <summary>
        /// Sets the name and returns this <see cref="ILayer"/>.
        /// </summary>
        /// <param name="name"></param>
        /// <returns></returns>
        ILayer SetName(string name);

        /// <summary>
        /// Sets the parent region which contains this <see cref="Layer{T}"/>
        /// </summary>
        /// <param name="r"></param>
        void SetRegion(Region r);

        /// <summary>
        /// Returns the parent region
        /// </summary>
        /// <returns></returns>
        Region GetRegion();

        /// <summary>
        /// Sets the parent <see cref="Network"/> on this <see cref="Layer{T}"/>
        /// </summary>
        /// <param name="network"></param>
        void SetNetwork(Network network);

        /// <summary>
        /// Returns the parent network
        /// </summary>
        /// <returns></returns>
        Network GetNetwork();

        /// <summary>
        /// Returns the count of records historically inputted into this
        /// </summary>
        /// <returns>the current record input count</returns>
        int GetRecordNum();

        /// <summary>
        /// Returns a flag indicating whether this layer's processing thread has been halted or not.
        /// </summary>
        bool IsHalted();

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> has had
        /// its <see cref="Close()"/> method called, or not.
        /// </summary>
        bool IsClosed();

        /// <summary>
        /// Resets the <see cref="TemporalMemory"/> if it exists.
        /// </summary>
        void Reset();

        /// <summary>
        /// Returns an <see cref="IObservable{IInference}"/> that can be subscribed to, or otherwise
        /// operated upon by another Observable or by an Observable chain.
        /// </summary>
        /// <returns>this <see cref="ILayer"/>'s output <see cref="IObservable{IInference}"/></returns>
        IObservable<IInference> Observe();

        /// <summary>
        /// Called by the <see cref="ILayer"/> client to receive output <see cref="IInference"/>s from the configured algorithms.
        /// </summary>
        /// <param name="subscriber">a <see cref="IObserver{IInference}"/> to be notified as data is published.</param>
        /// <returns>A Subscription disposable</returns>
        IDisposable Subscribe(IObserver<IInference> subscriber);

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
        void Start();

        /// <summary>
        /// Restarts this <see cref="ILayer"/>
        /// </summary>
        /// <param name="startAtIndex">flag indicating whether the Layer should be started and run from the previous save point or not.</param>
        void Restart(bool startAtIndex);

        /// <summary>
        /// Stops the processing of this <see cref="ILayer"/>'s processing thread.
        /// </summary>
        void Halt();

        /// <summary>
        /// Returns an <see cref="IObservable{T}"/> operator that when subscribed to, invokes an operation
        /// that stores the state of this {@code Network} while keeping the Network up and running.
        /// The Network will be stored at the pre-configured location (in binary form only, not JSON).
        /// </summary>
        /// <returns>the <see cref="ICheckPointOp{T}"/> operator</returns>
        ICheckPointOp<byte[]> GetCheckPointOperator();

        /// <summary>
        /// Adds an <see cref="HTMSensor{T}"/> to this <see cref="ILayer"/>. An HTMSensor is a regular
        /// <see cref="ISensor"/> (i.e. <see cref="FileSensor"/>, <see cref="URISensor"/>, or <see cref="ObservableSensor{T}"/>)
        /// which has had an <see cref="IEncoder"/> configured and added to it. HTMSensors are
        /// HTM Aware, where as regular Sensors have no knowledge of HTM requirements.
        /// </summary>
        /// <param name="sensor">the <see cref="HTMSensor{T}"/></param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Add(ISensor sensor);

        /// <summary>
        /// Adds a <see cref="MultiEncoder"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="encoder">the added MultiEncoder</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Add(MultiEncoder encoder);

        /// <summary>
        /// Adds a <see cref="SpatialPooler"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="sp">the added SpatialPooler</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Add(SpatialPooler sp);

        /// <summary>
        /// Adds a <see cref="TemporalMemory"/> to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="tm">the added TemporalMemory</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Add(TemporalMemory tm);

        /// <summary>
        /// Adds an <see cref="Anomaly"/> computer to this <see cref="ILayer"/>
        /// </summary>
        /// <param name="anomalyComputer">the Anomaly instance</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Add(Anomaly anomalyComputer);

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
        ILayer AlterParameter(Parameters.KEY key, object value);

        /// <summary>
        /// Allows the user to define the <see cref="Connections"/> object data structure
        /// to use. Or possibly to share connections between two <see cref="ILayer"/>s
        /// </summary>
        /// <param name="c">the <see cref="Connections"/> object to use.</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Using(Connections c);

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
        ILayer Using(Parameters p);

        /// <summary>
        /// Returns the last computed <see cref="IInference"/> of this <see cref="Layer{T}"/>
        /// </summary>
        /// <returns>the last computed inference.</returns>
        IInference GetInference();
    }
}