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
            Map<string, object> classifierInput = new Map<string, object>();
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
}