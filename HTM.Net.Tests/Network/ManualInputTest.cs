using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network
{
    [TestClass]
    public class ManualInputTest
    {

        /**
         * ManualInput retVal = new ManualInput();
            retVal.classifierInput = new HashMap<String, NamedTuple>(this.classifierInput);
            retVal.classifiers = this.classifiers;
            retVal.layerInput = this.layerInput;
            retVal.sdr = this.sdr;
            retVal.encoding = this.encoding;
            retVal.activeColumns = this.activeColumns;
            retVal.sparseActives = this.sparseActives;
            retVal.previousPrediction = this.previousPrediction;
            retVal.currentPrediction = this.currentPrediction;
            retVal.classification = this.classification;
            retVal.anomalyScore = this.anomalyScore;
            retVal.customObject = this.customObject;
         */
        [TestMethod]
        public void TestCopy()
        {
            Map<string, NamedTuple> classifierInput = new Map<string, NamedTuple>();
            NamedTuple classifiers = new NamedTuple(new[] { "one", "two" }, 1, 2);
            object layerInput = new object();
            int[] sdr = new[] { 20 };
            int[] encoding = new int[40];
            int[] activeColumns = new int[25];
            int[] sparseActives = new int[2];
            HashSet<Cell> activeCells = new HashSet<Cell>(); activeCells.Add(new Cell(new Column(4, 0), 1));
            HashSet<Cell> previousPrediction = new HashSet<Cell>(); previousPrediction.Add(new Cell(new Column(4, 0), 2));
            HashSet<Cell> currentPrediction = new HashSet<Cell>(); currentPrediction.Add(new Cell(new Column(4, 0), 3));
            Classification<object> classification = new Classification<object>();
            double anomalyScore = 0.48d;
            object customObject = new Net.Network.Network("MI Network", NetworkTestHarness.GetNetworkDemoTestEncoderParams());

            ManualInput mi = new ManualInput()
                .SetClassifierInput(classifierInput)
                .SetLayerInput(layerInput)
                .SetSdr(sdr)
                .SetEncoding(encoding)
                .SetFeedForwardActiveColumns(activeColumns)
                .SetFeedForwardSparseActives(sparseActives)
                .SetPredictiveCells(previousPrediction)
                .SetPredictiveCells(currentPrediction) // last prediction internally becomes previous
                .SetActiveCells(activeCells)
                .SetClassifiers(classifiers)
                .StoreClassification("foo", classification)
                .SetAnomalyScore(anomalyScore)
                .SetCustomObject(customObject);

            ManualInput copy = mi.Copy();
            Assert.IsTrue(copy.GetClassifierInput().DictEquals(classifierInput));
            Assert.IsFalse(copy.GetClassifierInput() == classifierInput);

            Assert.IsTrue(copy.GetLayerInput() == layerInput);

            Assert.IsTrue(Arrays.AreEqual(copy.GetSdr(), sdr));
            Assert.IsFalse(copy.GetSdr() == sdr);

            Assert.IsTrue(Arrays.AreEqual(copy.GetEncoding(), encoding));
            Assert.IsFalse(copy.GetEncoding() == encoding);

            Assert.IsTrue(Arrays.AreEqual(copy.GetFeedForwardActiveColumns(), activeColumns));
            Assert.IsFalse(copy.GetFeedForwardActiveColumns() == activeColumns);

            Assert.IsTrue(Arrays.AreEqual(copy.GetFeedForwardSparseActives(), sparseActives));
            Assert.IsFalse(copy.GetFeedForwardSparseActives() == sparseActives);

            Assert.IsTrue(copy.GetPredictiveCells().SetEquals(currentPrediction));
            Assert.IsFalse(copy.GetPredictiveCells() == currentPrediction);

            Assert.IsTrue(copy.GetActiveCells().SetEquals(activeCells));
            Assert.IsFalse(copy.GetActiveCells() == activeCells);

            Assert.IsTrue(copy.GetPreviousPredictiveCells().SetEquals(previousPrediction));
            Assert.IsFalse(copy.GetPreviousPredictiveCells() == previousPrediction);

            Assert.IsTrue(copy.GetClassifiers().Equals(classifiers));
            Assert.IsFalse(copy.GetClassifiers() == classifiers);

            Assert.IsTrue(copy.GetClassification("foo").Equals(classification));

            Assert.AreEqual(copy.GetAnomalyScore(), anomalyScore, 0.0); // zero deviation

            Assert.AreEqual(copy.GetCustomObject(), customObject);
        }

        

    }

    [TestClass]
    public class TupleTest
    {
        [TestMethod]
        public void TestTupleEquals()
        {
            Tuple tuple1 = new Tuple();
            Tuple tuple2 = new Tuple();

            Assert.IsTrue(tuple1.Equals(tuple2));

            tuple1 = new Tuple("one", 1);
            tuple2 = new Tuple("one", 1);

            Assert.IsTrue(tuple1.Equals(tuple2));

            tuple1 = new Tuple("one", 1, "two", 2);
            tuple2 = new Tuple("one", 1);

            Assert.IsFalse(tuple1.Equals(tuple2));

            tuple1 = new Tuple("one", 1, "two", null);
            tuple2 = new Tuple("one", 1, "two", null);

            Assert.IsTrue(tuple1.Equals(tuple2));
        }

        [TestMethod]
        public void TestEquality()
        {
            Tuple t1 = new Tuple("1", 1.0);
            Tuple t2 = new Tuple("1", 1.0);
            Assert.AreEqual(t1, t2);
            Assert.AreEqual(t1.GetHashCode(), t2.GetHashCode());
            Assert.IsTrue(t1.Equals(t2));

            t1 = new Tuple("1", 1.0);
            t2 = new Tuple("2", 1.0);
            Assert.AreNotEqual(t1, t2);
            Assert.AreNotEqual(t1.GetHashCode(), t2.GetHashCode());
            Assert.IsFalse(t1.Equals(t2));

            t1 = new Tuple("1", 1.0);
            t2 = new Tuple("1", 1.0, 1);
            Assert.AreNotEqual(t1, t2);
            Assert.AreNotEqual(t1.GetHashCode(), t2.GetHashCode());
            Assert.IsFalse(t1.Equals(t2));
        }
    }
}