using System;
using System.Collections.Generic;
using System.IO;
using HTM.Net.Network;
using HTM.Net.Serialize;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Serialize
{
    [TestClass]
    public class SerializerCoreTest
    {
        [TestMethod]
        public void TestGetSerializer()
        {
            SerializerCore serializer = Persistence.Get().Serializer();
            Assert.IsNotNull(serializer);

            SerializerCore serializer2 = Persistence.Get().Serializer();
            Assert.IsTrue(serializer == serializer2);
        }

        static List<IInference> callVerify = new List<IInference>();

        [TestMethod]
        public void TestSerializeDeSerialize()
        {

            SerializerCore serializer = Persistence.Get().Serializer();

            IInference inf = new ManualInputWithPostDeserialize();

            byte[] bytes = serializer.Serialize(inf);
            Assert.IsNotNull(bytes);

            IInference serializedInf = serializer.Deserialize<ManualInput>(bytes);
            Assert.IsNotNull(serializedInf);

            Assert.IsTrue(callVerify.Count == 1);

        }

        [Serializable]
        public class ManualInputWithPostDeserialize : ManualInput
        {
            #region Overrides of Persistable<ManualInput>

            public override object PostDeSerialize(object i)
            {
                IInference retVal = (IInference)base.PostDeSerialize(i);
                Assert.IsNotNull(retVal);
                Assert.IsTrue(retVal != i); // Ensure Objects not same
                Assert.IsTrue(retVal.Equals(i)); // However they are still equal!
                                                 //callVerify.add(retVal);
                                                 //Assert.IsTrue(callVerify.size() == 1);
                callVerify.Add(retVal);
                Assert.IsTrue(callVerify.Count == 1);
                return (ManualInput)retVal;
            }

            #endregion
        }
    }
}