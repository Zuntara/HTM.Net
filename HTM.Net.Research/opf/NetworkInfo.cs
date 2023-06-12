using HTM.Net.Network;

namespace HTM.Net.Research.opf
{
    /// <summary>
    /// Data type used as return value type by CLAModel.__createCLANetwork()
    /// </summary>
    public class NetworkInfo
    {
        public object StatisticCollectors { get; private set; }
        public Network.Network Network { get; private set; }
        public int NumRecords { get; set; }

        public NetworkInfo(Network.Network network, object statisticsCollectors, int numRecords)
        {
            this.Network = network;
            this.StatisticCollectors = statisticsCollectors;
            NumRecords = numRecords;
        }

        public Layer<IInference> GetLayer()
        {
            return (Layer<IInference>)Network.GetHead().GetHead();
        }
    }
}