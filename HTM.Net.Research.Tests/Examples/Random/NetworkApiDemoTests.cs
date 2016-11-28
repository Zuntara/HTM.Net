﻿using System;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Examples.Random
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
        public void TestGetRandomDataFieldEncodingMap()
        {
            EncoderSettingsList fieldEncodings = NetworkDemoHarness.GetRandomDataFieldEncodingMap();
            Assert.AreEqual(8, fieldEncodings.Count);
        }

        [TestMethod]
        public void TestGetNetworkDemoTestEncoderParams()
        {
            Parameters p = NetworkDemoHarness.GetNetworkDemoTestEncoderParams();
            Assert.AreEqual(30, p.Size());
        }

        #endregion

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
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

        [TestMethod]
        [DeploymentItem("Resources\\RandomData.csv")]
        //[DeploymentItem("Resources\\rec-center-15m.Csv")]
        public void RunBasicNetwork()
        {
            NetworkAPIDemo demo = new NetworkAPIDemo(NetworkAPIDemo.Mode.BASIC_CLA);
            demo.RunNetwork();

            Console.WriteLine("From last predictions to first predictions");
            for (int i = 10; i > 0; i--)
            {
                double pct = i / 10.0;
                Console.WriteLine("Pct: {1}; CorrectGuesses: max = {0}/7 avg = {2}", demo.GetHighestCorrectGuesses(pct, true), pct, demo.GetAverageCorrectGuesses(pct, true));
            }
            //Console.WriteLine("From first predictions to last predictions");
            //for (int i = 1; i <= 10; i++)
            //{
            //    double pct = i / 10.0;
            //    Console.WriteLine("Pct: {1}; CorrectGuesses: {0}/7", demo.GetHighestCorrectGuesses(pct, false), pct);
            //}

            Console.WriteLine("Guesses bucket list (grouped)");
            var allGuesses = demo.GetGuesses();
            for (int i = 0; i < 8; i++)
            {
                Console.WriteLine($"Correct: {i} = {allGuesses.Count(g => g == i)}");
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
                //Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, true), pct);
            }
            for (int i = 1; i <= 10; i++)
            {
                double pct = i / 10.0;
                //Console.WriteLine("Pct: {1}; Accurancy: {0}", demo.GetTotalAccurancy(pct, false), pct);
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