using System;
using System.Linq;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.PaHotGym
{
    [TestClass]
    public class NetworkApiDemoTests
    {
        #region Parameter Tests

        [TestMethod]
        public void TestGetParameters()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            Assert.AreEqual(66, p.Size());
        }

        [TestMethod]
        public void TestGetDayDemoTestEncoderParams()
        {
            Parameters p = NetworkDemoHarness.GetDayDemoTestEncoderParams();
            Assert.AreEqual(15, p.Size());
        }

        [TestMethod]
        public void TestGetDayDemoFieldEncodingMap()
        {
            Map<String, Map<string, object>> fieldEncodings = NetworkDemoHarness.GetDayDemoFieldEncodingMap();
            Assert.AreEqual(1, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestGetHotGymTestEncoderParams()
        {
            Map<String, Map<String, Object>> fieldEncodings = NetworkDemoHarness.GetHotGymFieldEncodingMap();
            Assert.AreEqual(2, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestGetNetworkDemoTestEncoderParams()
        {
            Parameters p = NetworkDemoHarness.GetNetworkDemoTestEncoderParams();
            Assert.AreEqual(30, p.Size());
        }

        [TestMethod]
        public void TestSetupMap()
        {
            Map<String, Map<String, Object>> m = NetworkDemoHarness.SetupMap(null, 23, 2, 0.0, 0.9, 22.0, 3.0, false, false, null, "cogmission", "ai", "works");
            Assert.IsNotNull(m);

            // Make sure omission of key doesn't insert null or a default value
            Assert.IsTrue(!m.ContainsKey("forced"));

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(12, m.Get("cogmission").Count);
        }

        #endregion

        [TestMethod]
        [DeploymentItem("Resources\\rec-center-15m.csv")]
        public void TestCreateBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC);
            Network.Network n = demo.CreateBasicNetwork();
            Assert.AreEqual(1, n.GetRegions().Count);
            Assert.IsNotNull(n.GetRegions().First().Lookup("Layer 2/3"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 4"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 5"));
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //public void TestGetSubscriber()
        //{
        //    NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTIREGION);
        //    IObserver<IInference> s = demo.GetSubscriber();
        //    Assert.IsNotNull(s);
        //}

        //[TestMethod]
        [DeploymentItem("Resources\\rec-center-15m.csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC);
            demo.RunNetwork();
        }

        //[TestMethod]
        [DeploymentItem("Resources\\rec-center-15m.csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiLayerNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTILAYER);
            demo.RunNetwork();
        }

        //[TestMethod]
        [DeploymentItem("Resources\\rec-center-15m.csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiRegionNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTIREGION);
            demo.RunNetwork();
        }
    }
}