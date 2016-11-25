using HTM.Net.Network;

namespace HTM.Net.Research.opf
{
    /// <summary>
    /// Data type used as return value type by CLAModel.__createCLANetwork()
    /// </summary>
    public class NetworkInfo
    {
        public object statCollectors { get; private set; }
        public Network.Network net { get; private set; }

        public NetworkInfo(Network.Network net, object statsCollectors)
        {
            this.net = net;
            this.statCollectors = statsCollectors;
        }

        public Layer<IInference> GetLayer()
        {
            return (Layer<IInference>) net.GetHead().GetHead();
        }
    }
}