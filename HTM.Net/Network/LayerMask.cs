using System;

namespace HTM.Net.Network
{
    [Flags]
    public enum LayerMask : byte
    {
        None = 0,
        SpatialPooler = 1,
        TemporalMemory = 2,
        ClaClassifier = 4,
        AnomalyComputer = 8,
        KnnClassifier = 16
    }
}