using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    public interface ILayer : IPersistable
    {
        /// <summary>
        /// Returns the String identifier of this <see cref="ILayer"/>
        /// </summary>
        /// <returns></returns>
        string GetName();
        Region GetParentRegion();
        /// <summary>
        /// Returns the previous Layer preceding this Layer in order of process flow.
        /// </summary>
        ILayer GetPrevious();
        MultiEncoder GetEncoder();
        Parameters GetParameters();
        bool HasSensor();
        ISensor GetSensor();
        ILayer SetName(string layerName);
        void SetRegion(Region region);
        Region GetRegion();
        void SetNetwork(Network network);
        Network GetNetwork();
        bool HasTemporalMemory();
        bool HasSpatialPooler();
        SpatialPooler GetSpatialPooler();
        TemporalMemory GetTemporalMemory();
        int GetRecordNum();
        ILayer ResetRecordNum();
        Task GetLayerThread();
        LayerMask GetMask();
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

        void Restart(bool startAtIndex);

        /// <summary>
        /// Stops the processing of this <see cref="ILayer"/>'s processing thread.
        /// </summary>
        void Halt();

        /// <summary>
        /// Returns a flag indicating whether this layer's processing thread has been halted or not.
        /// </summary>
        bool IsHalted();
        IObservable<IInference> Observe();
        IDisposable Subscribe(IObserver<IInference> subscriber);
        ILayer Close();

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> has had
        /// its <see cref="Close()"/> method called, or not.
        /// </summary>
        bool IsClosed();

        Connections GetConnections();
        /// <summary>
        /// Allows the user to define the <see cref="Connections"/> object data structure
        /// to use. Or possibly to share connections between two <see cref="ILayer"/>s
        /// </summary>
        /// <param name="connections">the <see cref="Connections"/> object to use.</param>
        /// <returns>this Layer instance (in fluent-style)</returns>
        ILayer Using(Connections connections);
        bool SetIsLearn();
        /// <summary>
        /// Returns the next Layer following this Layer in order of process flow.
        /// </summary>
        ILayer GetNext();
        IInference GetInference();
        void SetLearn(bool isLearn);
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
        
        int CalculateInputWidth();
        void Reset();
        void NotifyComplete();
        ILayer AlterParameter(Parameters.KEY key, object value);

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
        ILayer Add(Func<ManualInput, ManualInput> func);
        /// <summary>
        /// Returns the classifier assigned to this layer
        /// </summary>
        /// <returns></returns>
        IClassifier GetClassifier(MultiEncoder encoder, string predictedFieldName);

        ICheckPointOp<byte[]> GetCheckPointOperator();
    }


}