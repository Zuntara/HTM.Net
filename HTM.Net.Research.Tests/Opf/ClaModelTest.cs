using System.Linq;
using HTM.Net.Research.opf;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.Opf
{
    [TestClass]
    public class ClaModelTest
    {
        [TestMethod]
        public void TestRemoveUnlikelyPredictionsEmpty()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>(), 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(0, result.Count);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsSingleValues()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { {1, 0.1 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.001, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsLikelihoodThresholds()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.1 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            var first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.1, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.001 }, { 2, 0.002 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(2, first.Key);
            Assert.AreEqual(0.002, first.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double> { { 1, 0.002 }, { 2, 0.001 } }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(1, result.Count);
            first = result.First();
            Assert.AreEqual(1, first.Key);
            Assert.AreEqual(0.002, first.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsMaxPredictions()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 0.01, 3 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            var item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.4 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(4, item.Key);
            Assert.AreEqual(0.4, item.Value);
        }

        [TestMethod]
        public void TestRemoveUnlikelyPredictionsComplex()
        {
            var result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.004 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            var item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.4 }, { 5, 0.005 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(4, item.Key);
            Assert.AreEqual(0.4, item.Value);

            result = CLAModel._removeUnlikelyPredictions(new Map<object, double>
            {
                { 1, 0.1 }, { 2, 0.2 }, { 3, 0.3 }, { 4, 0.004 }, { 5, 0.005 }
            }, 0.01, 3);
            Assert.IsNotNull(result);
            Assert.AreEqual(3, result.Count);
            item = result.First();
            Assert.AreEqual(1, item.Key);
            Assert.AreEqual(0.1, item.Value);
            item = result.Skip(1).First();
            Assert.AreEqual(2, item.Key);
            Assert.AreEqual(0.2, item.Value);
            item = result.Skip(2).First();
            Assert.AreEqual(3, item.Key);
            Assert.AreEqual(0.3, item.Value);
        }
    }
}