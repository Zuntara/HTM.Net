using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class SdrClassifierTest
    {
        [TestMethod]
        public void TestInitialization()
        {
            var c = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Assert.IsNotNull(c);
        }

        [TestMethod]
        public void TestInvalidParams()
        {
            try
            {
                SDRClassifier classifier = new SDRClassifier(new int[0], alpha: 0.1, actValueAlpha: 0.1);
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.Message.StartsWith("Steps cannot be empty"));
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            try
            {
                SDRClassifier classifier = new SDRClassifier(new[] { -1 }, alpha: 0.1, actValueAlpha: 0.1);
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.Message.StartsWith("steps must be a list of non-negative ints"));
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            // Invalid alpha
            try
            {
                SDRClassifier classifier = new SDRClassifier(new[] { 1 }, alpha: -1.0, actValueAlpha: 0.1);
            }
            catch (ArgumentException ae)
            {
                Assert.IsTrue(ae.Message.StartsWith("alpha (learning rate) must be a positive number"));
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }

            try
            {
                SDRClassifier classifier = new SDRClassifier(new[] { 1 }, alpha: 0.1, actValueAlpha: -1.0);
            }
            catch (ArgumentOutOfRangeException ae)
            {
                Assert.IsTrue(ae.Message.StartsWith("actValueAlpha be a number between 0 and 1"));
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        /// <summary>
        /// Send same value 10 times and expect high likelihood for prediction
        /// </summary>
        [TestMethod]
        public void TestSingleValue()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0);
            // Enough times to perform Inference and learn associations
            NamedTuple retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }
            Assert.AreEqual((double)((object[])retVal["actualValues"])[0], 10);
            Assert.IsTrue(((double[])retVal["1"])[0] > 0.9, "value of 1 must be greater then 0.9 and is " + ((double[])retVal["1"])[0]);
        }

        /// <summary>
        /// Send same value 10 times and expect high likelihood for prediction using 0-step ahead prediction
        /// </summary>
        [TestMethod]
        public void TestSingleValue0Steps()
        {
            var classifier = new SDRClassifier(new[] { 0 }, 1.0);
            // Enough times to perform Inference and learn associations
            NamedTuple retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }
            Assert.AreEqual((double)((object[])retVal["actualValues"])[0], 10);
            Assert.IsTrue(((double[])retVal["0"])[0] > 0.9, "value of 1 must be greater then 0.9 and is " + ((double[])retVal["0"])[0]);

        }

        [TestMethod]
        public void TestComputeResultTypes()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            var result = classifier.Compute(0, new[] { 1, 5, 9 }, new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);

            Assert.IsTrue(result.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            Assert.IsInstanceOfType(result["actualValues"], typeof(object[]));
            object[] actValues = (object[])result["actualValues"];
            Assert.AreEqual(1, actValues.Length);
            Assert.IsInstanceOfType(actValues[0], typeof(double));
            Assert.IsInstanceOfType(result["1"], typeof(double[]));
            Assert.AreEqual((double)actValues[0], 34.7, 0.0001);
        }

        [TestMethod]
        public void TestComputeInferOrLearnOnly()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1, 0);
            int recordNum = 0;
            // Learn only
            NamedTuple retVal = classifier.Compute(recordNum, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, false);
            Assert.IsNull(retVal);

            // Infer only
            NamedTuple retVal1 = classifier.Compute(recordNum, new[] { 1, 5, 9 },
               new NamedTuple(new[] { "bucketIdx", "actValue" }, 2, 14.2), false, true);
            recordNum += 1;
            NamedTuple retVal2 = classifier.Compute(recordNum, new[] { 1, 5, 9 },
               new NamedTuple(new[] { "bucketIdx", "actValue" }, 3, 20.5), false, true);
            recordNum += 1;
            Assert.IsTrue(((double[])retVal1["1"]).SequenceEqual((double[])retVal2["1"]));

        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void TestComputeInferAndLearnFalse()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1, 0);
            int recordNum = 0;
            // Learn only
            NamedTuple retVal = classifier.Compute(recordNum, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), false, false);
        }

        [TestMethod]
        public void TestCompute1()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            NamedTuple retVal = classifier.Compute(0, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);
            Assert.IsTrue(retVal.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])retVal["actualValues"];
            Assert.AreEqual(1, actValues.Length);
            Assert.IsInstanceOfType(actValues[0], typeof(double));
            Assert.AreEqual((double)actValues[0], 34.7, 0.0001);
        }

        [TestMethod]
        public void TestCompute2()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            NamedTuple retVal = classifier.Compute(0, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);
            retVal = classifier.Compute(1, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);

            Assert.IsTrue(retVal.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])retVal["actualValues"];
            Assert.AreEqual((double)actValues[4], 34.7, 0.0001);
        }

        [TestMethod]
        public void TestComputeComplex()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1, 0);
            int recordNum = 0;
            classifier.Compute(recordNum, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);
            recordNum += 1;

            classifier.Compute(recordNum, new[] { 0, 6, 9, 11 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 5, 41.7), true, true);
            recordNum += 1;

            classifier.Compute(recordNum, new[] { 6, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 5, 44.9), true, true);
            recordNum += 1;

            classifier.Compute(recordNum, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 42.9), true, true);
            recordNum += 1;

            var result = classifier.Compute(recordNum, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, 34.7), true, true);
            recordNum += 1;

            Assert.IsTrue(result.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])result["actualValues"];

            Assert.AreEqual((double)actValues[4], 35.520000457763672, 0.0001);
            Assert.AreEqual((double)actValues[5], 42.020000457763672, 0.0001);
            double[] resultDoubles = (double[])result["1"];
            Assert.AreEqual(6, resultDoubles.Length);
            Assert.AreEqual(resultDoubles[0], 0.034234, 0.0001);
            Assert.AreEqual(resultDoubles[1], 0.034234, 0.0001);
            Assert.AreEqual(resultDoubles[2], 0.034234, 0.0001);
            Assert.AreEqual(resultDoubles[3], 0.034234, 0.0001);
            Assert.AreEqual(resultDoubles[4], 0.093058, 0.0001);
            Assert.AreEqual(resultDoubles[5], 0.770004, 0.0001);
        }

        [TestMethod]
        public void TestComputeWithMissingValue()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            NamedTuple result = classifier.Compute(0, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, null, null), true, true);

            Assert.IsTrue(result.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])result["actualValues"];
            Assert.AreEqual(1, actValues.Length);
            Assert.AreEqual((double?)actValues[0], null);
        }

        [TestMethod]
        public void TestComputeCategory()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            NamedTuple result = classifier.Compute(0, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, "D"), true, true);
            result = classifier.Compute(1, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, "D"), true, true);

            Assert.IsTrue(result.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])result["actualValues"];
            Assert.AreEqual(5, actValues.Length);
            Assert.AreEqual((string)actValues[4], "D");
        }

        [TestMethod]
        public void TestComputeCategory2()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1, 0);
            NamedTuple result = classifier.Compute(0, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, "D"), true, true);
            result = classifier.Compute(1, new[] { 1, 5, 9 },
                new NamedTuple(new[] { "bucketIdx", "actValue" }, 4, "E"), true, true);

            Assert.IsTrue(result.GetKeys().SequenceEqual(new[] { "actualValues", "1" }));
            object[] actValues = (object[])result["actualValues"];
            Assert.AreEqual(5, actValues.Length);
            Assert.AreEqual((string)actValues[4], "D");
        }

        [TestMethod]
        public void TestOverlapPattern()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 10.0);

            _compute(classifier, 0, new[] {1, 5}, 9, 9);
            _compute(classifier, 1, new[] {1, 5}, 9, 9);
            var retVal = _compute(classifier, 2, new[] {3, 5}, 2, 2);

            // Since overlap - should be previous with high likelihood
            object[] actValues = (object[])retVal["actualValues"];
            Assert.AreEqual((double)actValues[9], 9);
            double[] resultDoubles = (double[])retVal["1"];
            Assert.IsTrue(resultDoubles[9]>0.9);

            retVal = _compute(classifier, 3, new[] { 3, 5 }, 2, 2);
            resultDoubles = (double[])retVal["1"];
            // Second example: now new value should be more probable than old
            Assert.IsTrue(resultDoubles[2] > resultDoubles[9]);

        }

        [TestMethod]
        public void TestMultistepSingleValue()
        {
            var classifier = new SDRClassifier(new[] { 1,2 });

            NamedTuple retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }
            
            // Since overlap - should be previous with high likelihood
            object[] actValues = (object[])retVal["actualValues"];
            Assert.AreEqual((double)actValues[0], 10);
            double[] resultDoubles1 = (double[])retVal["1"];
            double[] resultDoubles2 = (double[])retVal["2"];

            Assert.AreEqual(resultDoubles1[0], 1);
            Assert.AreEqual(resultDoubles2[0], 1);
        }

        private NamedTuple _compute(SDRClassifier classifier, int recordNum, int[] pattern, int bucket, double value)
        {
            var classification = new NamedTuple(new[] { "bucketIdx", "actValue" }, bucket, value);
            return classifier.Compute(recordNum, pattern, classification, true, true);
        }
    }
}