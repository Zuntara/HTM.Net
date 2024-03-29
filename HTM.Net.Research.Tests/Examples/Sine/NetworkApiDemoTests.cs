﻿using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.Sine
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
            Assert.AreEqual(14, p.Size());
        }

        [TestMethod]
        public void TestGetDayDemoFieldEncodingMap()
        {
            Map<String, Map<string, object>> fieldEncodings = NetworkDemoHarness.GetDayDemoFieldEncodingMap();
            Assert.AreEqual(1, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestGetNetworkDemoTestEncoderParams()
        {
            Parameters p = NetworkDemoHarness.GetNetworkDemoTestEncoderParams();
            Assert.AreEqual(29, p.Size());
        }

        [TestMethod]
        public void TestSetupMap()
        {
            Map<String, Map<String, Object>> m = NetworkDemoHarness.SetupMap(null, 23, 2, 0.0, 0.9, 22.0, 3.0, false, false, null, "cogmission", FieldMetaType.Float, EncoderTypes.None);
            Assert.IsNotNull(m);

            // Make sure omission of key doesn't insert null or a default value
            Assert.IsTrue(!m.ContainsKey("forced"));

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(12, m.Get("cogmission").Count);
        }

        #endregion

        //[TestMethod]
        [DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestCreateBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC);
            Network.Network n = demo.CreateBasicNetwork();
            Assert.AreEqual(1, n.GetRegions().Count);
            Assert.IsNotNull(n.GetRegions().First().Lookup("Layer 2/3"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 4"));
            Assert.IsNull(n.GetRegions().First().Lookup("Layer 5"));
        }


        [TestMethod]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC);
            demo.RunNetwork();

            Console.WriteLine($"Accurancy - prediction error ({demo._predictions.Count})");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetAccurancy(pct, true), pct);
            }

            /*Console.WriteLine("Accurancy pct from behind");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }

            Console.WriteLine("Accurancy pct from front");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
            }*/
        }


        [TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiLayerNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTILAYER);
            demo.RunNetwork();

            Console.WriteLine("Accurancy - prediction error");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetAccurancy(pct, true), pct);
            }
            /*Console.WriteLine("Accurancy pct from behind");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }

            Console.WriteLine("Accurancy pct from front");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
            }*/
        }

        [TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunMultiRegionNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.MULTIREGION);
            demo.RunNetwork();

            Console.WriteLine("Accurancy - prediction error");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetAccurancy(pct, true), pct);
            }
            
            /*Console.WriteLine("Accurancy pct from behind");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }

            Console.WriteLine("Accurancy pct from front");
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
            }*/
        }

        [TestMethod]
        public void CheckEncoder()
        {
            var encoderMap = NetworkDemoHarness.GetNetworkDemoFieldEncodingMap()["sinedata"];

            Encoder<double> encoder = (Encoder<double>)ScalarEncoder.GetBuilder()
                .Radius((double)encoderMap["radius"])
                .MinVal((double)encoderMap["minVal"])
                .MaxVal((double)encoderMap["maxVal"])
                .Resolution((double)encoderMap["resolution"])
                .N((int)encoderMap["n"])
                .W((int)encoderMap["w"])
                .Periodic((bool)encoderMap["periodic"])
                .Forced((bool)encoderMap["forced"])
                .Name((string)encoderMap["fieldName"])
                .Build();

            Dictionary<double, string> bitlist = new();
            for (double i = -10.0; i <= 10.0; i+=0.1)
            {
                var bits = encoder.Encode(Math.Round(i, 2));
                //Assert.AreEqual(231, bits.Length);
                bitlist.Add(Math.Round(i, 2), Arrays.ToString(bits).TrimEnd(','));
            }

            foreach (var bits in bitlist.Take(5))
            {
                Console.WriteLine($"{bits.Key} -> {bits.Value}");
            }

            int count = bitlist.Count;
            int distinctCount = bitlist.Values.Distinct().Count();
            Assert.AreEqual(count, distinctCount, $"{count / (double)distinctCount}");
        }
    }
}