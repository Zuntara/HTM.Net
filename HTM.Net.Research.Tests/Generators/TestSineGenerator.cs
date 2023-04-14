using System;
using HTM.Net.Research.Generators;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Generators
{
    [TestClass]
    public class TestSineGenerator
    {
        [TestMethod]
        public void TestSampling()
        {
            foreach(double sample in SineGenerator.GenerateSineWave(100, 100, 10, 1))
            {
                Console.WriteLine(sample);
                Assert.AreEqual(0, sample, 10.0);
            }
        } 
    }
}