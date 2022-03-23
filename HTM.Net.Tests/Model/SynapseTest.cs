using HTM.Net.Model;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Model
{
    [TestClass]
    public class SynapseTest
    {
        [TestMethod]
        public void TestSynapseEquality()
        {
            // Make stuff we need to perform the tests
            Column column = new Column(1, 0);
            Cell cell1 = new Cell(column, 0);
            Cell cell2 = new Cell(column, 1);
            DistalDendrite segment1 = new DistalDendrite(cell1, 0, 0, 0);
            DistalDendrite segment2 = new DistalDendrite(cell1, 1, 1, 1);

            // These are the Synapse objects we will use for the tests
            Synapse synapse1 = new Synapse();
            Synapse synapse2 = new Synapse();

            /* ----- These are the equality tests: ----- */
            // synapse1 should equal itself
            Assert.IsTrue(synapse1.Equals(synapse1));

            // synapse1 should not equal null
            Assert.IsFalse(synapse1.Equals(null));

            // synapse1 should not equal a non-Synapse object
            Assert.IsFalse(synapse1.Equals("This is not a Synapse object"));

            // synapse1 should not equal synapse2 because synapse2's
            // inputIndex != synapse1's inputIndex
            synapse1.SetPresynapticCell(cell1);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // segment is null, but synapse2's segment is not null
            synapse2 = new Synapse(cell1, segment1, 0, 0);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // segment != synapse2's segment
            synapse1 = new Synapse(cell1, segment2, 0, 0);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // sourceCell is null, but synapse2's sourceCell is not null
            synapse1.SetPresynapticCell(null);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // sourceCell != synapse2's sourceCell
            synapse1.SetPresynapticCell(cell2);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // synapseIndex != synapse2's synapseIndex
            synapse1 = new Synapse(cell1, segment1, 0, 0);
            synapse2 = new Synapse(cell1, segment1, 1, 0);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should not equal synapse2 because synapse1's
            // permanence != synapse2's permanence
            synapse1 = new Synapse(cell1, segment1, 0, 0);
            synapse2 = new Synapse(cell1, segment1, 0, 1);
            Assert.IsFalse(synapse1.Equals(synapse2));

            // synapse1 should equal synapse2 because all of their
            // relevant properties are equal
            synapse1 = new Synapse(cell1, segment1, 0, 0);
            synapse2 = new Synapse(cell1, segment1, 0, 0);
            Assert.IsTrue(synapse1.Equals(synapse2));
        }
    }
}