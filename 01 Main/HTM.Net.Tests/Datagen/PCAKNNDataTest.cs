using HTM.Net.Datagen;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Datagen
{
    [TestClass]
    public class PCAKNNDataTest
    {
        [TestMethod]
        [DeploymentItem("Resources\\test_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\test_pcaknnshort_data.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_class.txt")]
        [DeploymentItem("Resources\\train_pcaknnshort_data.txt")]
        public void TestGenerateForPCAKNNShort()
        {
            PCAKNNData data = new PCAKNNData();
            KNNDataArray[] dataArray = data.GetPcaKNNShortData();
            Assert.IsNotNull(dataArray);
            Assert.AreEqual(2, dataArray.Length);
            for (int i = 0; i < 2; i++)
            {
                switch (i)
                {
                    case 0:
                        { // Training Data
                            Assert.IsNotNull(dataArray[i].GetClassArray());
                            Assert.AreEqual(900, dataArray[i].GetClassArray().Length);
                            Assert.IsNotNull(dataArray[i].GetDataArray());
                            Assert.AreEqual(900, dataArray[i].GetDataArray().Length);
                            break;
                        }
                    case 1:
                        { // Actual Data
                            Assert.IsNotNull(dataArray[i].GetClassArray());
                            Assert.AreEqual(100, dataArray[i].GetClassArray().Length);
                            Assert.IsNotNull(dataArray[i].GetDataArray());
                            Assert.AreEqual(100, dataArray[i].GetDataArray().Length);
                            break;
                        }
                }
            }
        }
    }
}