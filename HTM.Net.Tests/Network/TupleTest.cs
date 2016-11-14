using System.Diagnostics;
using HTM.Net.Model;
using HTM.Net.Serialize;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HTM.Net.Tests.Network
{
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

        // test serialisation of tuples to json and back

        [TestMethod]
        public void TestTupleSerializeBinary()
        {
            var tuple1 = new Tuple("one", 1, "two", 2);

            SerializerCore core = new SerializerCore(typeof(Tuple));
            var serialized = core.Serialize(tuple1);
            Tuple tuple2 = core.Deserialize<Tuple>(serialized);

            Assert.IsTrue(tuple1.Equals(tuple2), "Fault in serializing or deserializing to binary of Tuple");
        }

        [TestMethod]
        public void TestTupleSerializeJson()
        {
            Tuple tuple1 = new Tuple("one", 1, "two", 2);

            string serialized = JsonConvert.SerializeObject(tuple1, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            Debug.WriteLine(serialized);

            Tuple tuple2 = JsonConvert.DeserializeObject<Tuple>(serialized, new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.All });

            Assert.IsTrue(tuple1.Equals(tuple2), "Fault in serializing or deserializing to json of Tuple");
        }
    }
}