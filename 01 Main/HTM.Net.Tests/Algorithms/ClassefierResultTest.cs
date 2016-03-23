using HTM.Net.Algorithms;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class ClassifierResultTest
    {
        [TestMethod]
        public void TestCopy()
        {
            string mon = "Monday";
            string tue = "Tuesday";
            string wed = "Wednesday";

            double monVal = 0.01d;
            double tueVal = 0.80d;
            double wedVal = 0.30d;

            ClassifierResult<string> result = new ClassifierResult<string>();
            result.SetActualValues(new[] { mon, tue, wed });
            result.SetStats(1, new[] { monVal, tueVal, wedVal });
            Assert.IsTrue(result.GetMostProbableValue(1).Equals(tue));
            Assert.IsNull(result.GetMostProbableValue(2));

            ClassifierResult<string> result2 = result.Copy();
            Assert.AreEqual(result, result2);

            result2.SetStats(1, new[] { monVal, tueVal, 0.5d });
            Assert.AreNotEqual(result, result2);
        }

        [TestMethod]
        public void TestGetMostProbableValue()
        {
            string mon = "Monday";
            string tue = "Tuesday";
            string wed = "Wednesday";

            double monVal = 0.01d;
            double tueVal = 0.80d;
            double wedVal = 0.30d;

            ClassifierResult<string> result = new ClassifierResult<string>();
            result.SetActualValues(new[] { mon, tue, wed });
            result.SetStats(1, new[] { monVal, tueVal, wedVal });
            Assert.IsTrue(result.GetMostProbableValue(1).Equals(tue));
            Assert.IsNull(result.GetMostProbableValue(2));

            double monVal2 = 0.30d;
            double tueVal2 = 0.01d;
            double wedVal2 = 0.29d;
            result.SetStats(3, new[] { monVal2, tueVal2, wedVal2 });
            Assert.IsTrue(result.GetMostProbableValue(3).Equals(mon));
            Assert.IsNull(result.GetMostProbableValue(2));
        }

        [TestMethod]
        public void TestGetMostProbableBucketIndex()
        {
            string mon = "Monday";
            string tue = "Tuesday";
            string wed = "Wednesday";

            double monVal = 0.01d;
            double tueVal = 0.80d;
            double wedVal = 0.30d;

            ClassifierResult<string> result = new ClassifierResult<string>();
            result.SetActualValues(new[] { mon, tue, wed });
            result.SetStats(1, new[] { monVal, tueVal, wedVal });
            Assert.IsTrue(result.GetMostProbableBucketIndex(1) == 1);
            Assert.IsTrue(result.GetMostProbableBucketIndex(2) == -1);

            double monVal2 = 0.30d;
            double tueVal2 = 0.01d;
            double wedVal2 = 0.29d;
            result.SetStats(3, new[] { monVal2, tueVal2, wedVal2 });
            Assert.IsTrue(result.GetMostProbableBucketIndex(3) == 0);
            Assert.IsTrue(result.GetMostProbableBucketIndex(2) == -1);
        }

        [TestMethod]
        public void TestGetCorrectStepsCount()
        {
            string mon = "Monday";
            string tue = "Tuesday";
            string wed = "Wednesday";

            double monVal = 0.01d;
            double tueVal = 0.80d;
            double wedVal = 0.30d;

            ClassifierResult<string> result = new ClassifierResult<string>();
            result.SetActualValues(new[] { mon, tue, wed });
            result.SetStats(1, new[] { monVal, tueVal, wedVal });
            Assert.IsTrue(result.GetMostProbableBucketIndex(1) == 1);
            Assert.IsTrue(result.GetMostProbableBucketIndex(2) == -1);

            double monVal2 = 0.30d;
            double tueVal2 = 0.01d;
            double wedVal2 = 0.29d;
            result.SetStats(3, new[] { monVal2, tueVal2, wedVal2 });
            Assert.IsTrue(result.GetMostProbableBucketIndex(3) == 0);
            Assert.IsTrue(result.GetMostProbableBucketIndex(2) == -1);
            Assert.IsTrue(result.GetStepCount() == 2);
        }
    }
}