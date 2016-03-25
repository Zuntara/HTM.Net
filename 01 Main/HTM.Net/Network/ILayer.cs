using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Util;

namespace HTM.Net.Network
{
    public interface ILayer
    {
        string GetName();
        Region GetParentRegion();
        ILayer GetPrevious();
        MultiEncoder GetEncoder();
        Parameters GetParameters();
        bool HasSensor();
        ISensor GetSensor();
        ILayer SetName(string layerName);
        void SetRegion(Region region);
        void SetNetwork(Network network);
        bool HasTemporalMemory();
        bool HasSpatialPooler();
        SpatialPooler GetSpatialPooler();
        TemporalMemory GetTemporalMemory();
        int GetRecordNum();
        Task GetLayerThread();
        LayerMask GetMask();
        void Start();
        void Halt();
        IObservable<IInference> Observe();
        ILayer Close();

        /// <summary>
        /// Returns a flag indicating whether this <see cref="ILayer"/> has had
        /// its <see cref="Close()"/> method called, or not.
        /// </summary>
        bool IsClosed();

        Connections GetConnections();
        ILayer Using(Connections connections);
        bool SetIsLearn();
        ILayer GetNext();
        IInference GetInference();
        void SetLearn(bool isLearn);

        void Compute<TInput>(TInput t);
        ILayer Add(TemporalMemory temporalMemory);
        ILayer Add(Anomaly anomaly);
        ILayer Add(SpatialPooler spatialPooler);
        ILayer Add(ISensor sensor);
        ILayer Add(MultiEncoder encoder);
        ILayer Add(Func<ManualInput, ManualInput> func);
        int CalculateInputWidth();
        void Reset();
        void NotifyComplete();
        ILayer AlterParameter(Parameters.KEY key, object value);
        object CustomCompute(int recordNum, List<int> patternNZ, Map<string, object> classification);
    }


}