using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.B2B
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
        public void TestGetHotGymTestEncoderParams()
        {
            Map<String, Map<String, Object>> fieldEncodings = NetworkDemoHarness.GetHotGymFieldEncodingMap();
            Assert.AreEqual(2, fieldEncodings.Count);
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
            Map<String, Map<String, Object>> m = NetworkDemoHarness.SetupMap(null, 23, 2, 0.0, 0.9, 22.0, 3.0, false, false, null, "cogmission", "ai", EncoderTypes.None);
            Assert.IsNotNull(m);

            // Make sure omission of key doesn't insert null or a default value
            Assert.IsTrue(!m.ContainsKey("forced"));

            Assert.AreEqual(1, m.Count);
            Assert.AreEqual(12, m.Get("cogmission").Count);
        }

        #endregion

        //[TestMethod]
        //[DeploymentItem("Resources\\rec-center-hourly.Csv")]
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
        //[DeploymentItem("Resources\\b2b_2014_15_min.csv")]
        public void CreateB2BOutputFile()
        {
            using (FileStream file = File.Create(@"C:\temp\b2b_output.csv"))
            {
                StreamReader sr = new StreamReader("b2b_2014_15_min.csv", Encoding.UTF8);
                using (StreamWriter sw = new StreamWriter(file))
                {
                    int i = 0;
                    for (i = 0; i < 3; i++)
                    {
                        string line = sr.ReadLine();
                        sw.WriteLine(string.Join(",", line.Split('\t')));
                    }
                    string read = sr.ReadLine();
                    while (read != null)
                    {
                        string[] parts = read.Split('\t');
                        DateTime dt = DateTime.Parse(parts[0], CultureInfo.GetCultureInfo("nl-be").DateTimeFormat);
                        int sequence = int.Parse(parts[1]);
                        double consumption = double.Parse(parts[2]);
                        DateTime offset = dt.AddMinutes((sequence - 1) * 15);
                        sw.WriteLine(string.Join(",", offset.ToString("dd/MM/yyyy HH:mm"), consumption.ToString(NumberFormatInfo.InvariantInfo)));

                        read = sr.ReadLine();
                    }
                }
            }

        }

        //[TestMethod]
        //[DeploymentItem("Resources\\b2b_2014_output.csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC);
            demo.RunNetwork();

            Console.WriteLine("Accurancy pct from behind");
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