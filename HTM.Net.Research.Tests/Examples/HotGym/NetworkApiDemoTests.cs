﻿using System;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.HotGym
{
    [TestClass]
    public class NetworkApiDemoTests
    {
        #region Parameter Tests

        [TestMethod]
        public void TestGetParameters()
        {
            Parameters p = NetworkDemoHarness.GetParameters();
            Assert.AreEqual(69, p.Size());
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
            EncoderSettingsList fieldEncodings = NetworkDemoHarness.GetDayDemoFieldEncodingMap();
            Assert.AreEqual(1, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestGetHotGymTestEncoderParams()
        {
            EncoderSettingsList fieldEncodings = NetworkDemoHarness.GetHotGymFieldEncodingMap();
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
            EncoderSettingsList m = NetworkDemoHarness.SetupMap(null, 23, 2, 0.0, 0.9, 22.0, 3.0, false, false, null, "cogmission", FieldMetaType.SparseArray, "works");
            Assert.IsNotNull(m);

            // Make sure omission of key doesn't insert null or a default value
            Assert.IsTrue(!m.ContainsKey("forced"));

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(12, m.Get("cogmission").Keys.Count);
        }

        #endregion

        [TestMethod]
        [DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestCreateBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_CLA);
            Network.Network n = demo.CreateBasicNetworkCla();
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
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_CLA);
            demo.RunNetwork();

            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
            }
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void RunBasicNetworkSdr()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_SDR);
            demo.RunNetwork();

            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
            }
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiLayerNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTILAYER);
            demo.RunNetwork();
        }

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiRegionNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTIREGION);
            demo.RunNetwork();
        }
    }
}