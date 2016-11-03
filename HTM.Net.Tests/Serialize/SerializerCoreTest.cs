using System;
using System.Collections.Generic;
using HTM.Net.Network;
using HTM.Net.Serialize;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Serialize
{
    [TestClass]
    public class SerializerCoreTest
    {
        [TestMethod]
        public void testSerializeDeSerialize()
        {
            List<IInference> callVerify = new List<IInference>();

            SerializerCore serializer = new SerializerCore();

            IInference inf = new ManualInputWithPostDeserialize();
    //        {

            //        public <T> T postDeSerialize(T i)
            //    {
            //        Inference retVal = (Inference)super.postDeSerialize(i);
            //        assertNotNull(retVal);
            //        assertTrue(retVal != i); // Ensure Objects not same
            //        assertTrue(retVal.equals(i)); // However they are still equal!
            //        callVerify.add(retVal);
            //        assertTrue(callVerify.size() == 1);

            //        return (T)retVal;
            //    }
            //};

            byte[] bytes = serializer.Serialize(inf);
            Assert.IsNotNull(bytes);

            IInference serializedInf = serializer.Deserialize<ManualInput>(bytes);
            Assert.IsNotNull(serializedInf);
        }

        [Serializable]
        public class ManualInputWithPostDeserialize : ManualInput
        {
            #region Overrides of Persistable<ManualInput>

            public override ManualInput PostDeSerialize(ManualInput i)
            {
                IInference retVal = (IInference)base.PostDeSerialize(i);
                Assert.IsNotNull(retVal);
                Assert.IsTrue(retVal != i); // Ensure Objects not same
                Assert.IsTrue(retVal.Equals(i)); // However they are still equal!
                //callVerify.add(retVal);
                //Assert.IsTrue(callVerify.size() == 1);

                return (ManualInput) retVal;
            }

            #endregion
        }
    }
}