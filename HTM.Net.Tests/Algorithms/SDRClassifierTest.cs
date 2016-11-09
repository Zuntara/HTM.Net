using System;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class SdrClassifierTest
    {
        [TestMethod]
        public void TestInitialization()
        {
            var c = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            Assert.IsNotNull(c);
        }

        [TestMethod]
        public void TestInvalidParams()
        {
            try
            {
                new SDRClassifier(new int[0], alpha: 0.1, actValueAlpha: 0.1);
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
                new SDRClassifier(new[] { -1 }, alpha: 0.1, actValueAlpha: 0.1);
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
                new SDRClassifier(new[] { 1 }, alpha: -1.0, actValueAlpha: 0.1);
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
                new SDRClassifier(new[] { 1 }, alpha: 0.1, actValueAlpha: -1.0);
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
            Classification<double> retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }
            Assert.AreEqual(retVal.GetActualValue(0), 10);
            Assert.IsTrue(retVal.GetStat(1,0) > 0.9, "value of 1 must be greater then 0.9 and is " + retVal.GetStat(1, 0));
        }

        /// <summary>
        /// Send same value 10 times and expect high likelihood for prediction using 0-step ahead prediction
        /// </summary>
        [TestMethod]
        public void TestSingleValue0Steps()
        {
            var classifier = new SDRClassifier(new[] { 0 }, 1.0);
            // Enough times to perform Inference and learn associations
            Classification<double> retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }
            Assert.AreEqual(retVal.GetActualValue(0), 10);
            Assert.IsTrue(retVal.GetStat(0, 0) > 0.9, "value of 1 must be greater then 0.9 and is " + retVal.GetStat(0, 0));

        }

        [TestMethod]
        public void TestComputeResultTypes()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            var result = classifier.Compute<double>(0, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } }, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            Assert.AreEqual(34.7, result.GetActualValue(0), 0.01);
        }

        [TestMethod]
        public void TestComputeInferOrLearnOnly()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1);
            int recordNum = 0;
            // Learn only
            var retVal = classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, false);
            Assert.IsNull(retVal);

            // Infer only
            var retVal1 = classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 2 }, { "actValue", 14.2 } },
                new[] { 1, 5, 9 }, false, true);
            recordNum += 1;
            var retVal2 = classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 3 }, { "actValue", 20.5 } },
                new[] { 1, 5, 9 }, false, true);
            recordNum += 1;
            Assert.IsTrue((retVal1.GetStats(1)).SequenceEqual(retVal2.GetStats(1)));

        }

        [TestMethod, ExpectedException(typeof(InvalidOperationException))]
        public void TestComputeInferAndLearnFalse()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1);
            int recordNum = 0;
            // Learn only
            var retVal = classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, false, false);
        }

        [TestMethod]
        public void TestCompute1()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            var retVal = classifier.Compute<double>(0, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, retVal.StepSet()));
            Assert.AreEqual(1, retVal.GetActualValueCount());

            double[] actValues = retVal.GetActualValues();
            Assert.AreEqual(1, actValues.Length);
            Assert.IsInstanceOfType(actValues[0], typeof(double));
            Assert.AreEqual(actValues[0], 34.7, 0.0001);
        }

        [TestMethod]
        public void TestCompute2()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            var retVal = classifier.Compute<double>(0, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, true);
            retVal = classifier.Compute<double>(1, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, retVal.StepSet()));
            Assert.AreEqual(5, retVal.GetActualValueCount());

            double[] actValues = retVal.GetActualValues();
            Assert.AreEqual(actValues[4], 34.7, 0.0001);
        }

        [TestMethod]
        public void TestComputeComplex()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 1.0, 0.1);
            int recordNum = 0;
            classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 5 }, { "actValue", 41.7 } },
                new[] { 0, 6, 9, 11 }, true, true);
            recordNum += 1;

            classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 5 }, { "actValue", 44.9 } },
                new[] { 6, 9 }, true, true);
            recordNum += 1;

            classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 42.9 } },
                new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            var result = classifier.Compute<double>(recordNum, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", 34.7 } },
                new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(6, result.GetActualValueCount());
            double[] actValues = result.GetActualValues();

            Assert.AreEqual((double)actValues[4], 35.520000457763672, 0.0001);
            Assert.AreEqual((double)actValues[5], 42.020000457763672, 0.0001);
            double[] resultDoubles = (double[])result.GetStats(1);
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
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            var result = classifier.Compute<double?>(0, new Map<string, object> { { "bucketIdx", null }, { "actValue", null } },
                new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            double?[] actValues = result.GetActualValues();
            Assert.AreEqual(1, actValues.Length);
            Assert.AreEqual(null, actValues[0]);
        }

        [TestMethod]
        public void TestComputeCategory()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            classifier.Compute<string>(0, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", "D" } },
                new[] { 1, 5, 9 }, true, true);
            var result = classifier.Compute<string>(1, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", "D" } },
                new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(5, result.GetActualValueCount());
            double[] actValues = result.GetStats(1);
            Assert.AreEqual("D", result.GetActualValue(4));
        }

        [TestMethod]
        public void TestComputeCategory2()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 0.1, 0.1);
            classifier.Compute<string>(0, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", "D" } },
               new[] { 1, 5, 9 }, true, true);
            var result = classifier.Compute<string>(1, new Map<string, object> { { "bucketIdx", 4 }, { "actValue", "E" } },
                new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(5, result.GetActualValueCount());
            string[] actValues = result.GetActualValues();
            Assert.AreEqual(5, actValues.Length);
            Assert.AreEqual(actValues[4], "D");
        }

        [TestMethod]
        public void TestOverlapPattern()
        {
            var classifier = new SDRClassifier(new[] { 1 }, 10.0);

            _compute(classifier, 0, new[] {1, 5}, 9, 9);
            _compute(classifier, 1, new[] {1, 5}, 9, 9);
            var retVal = _compute(classifier, 2, new[] {3, 5}, 2, 2);

            // Since overlap - should be previous with high likelihood
            double[] actValues = retVal.GetActualValues();
            Assert.AreEqual(actValues[9], 9);
            double[] resultDoubles = retVal.GetStats(1);
            Assert.IsTrue(resultDoubles[9]>0.9);

            retVal = _compute(classifier, 3, new[] { 3, 5 }, 2, 2);
            resultDoubles = retVal.GetStats(1);
            // Second example: now new value should be more probable than old
            Assert.IsTrue(resultDoubles[2] > resultDoubles[9]);

        }

        [TestMethod]
        public void TestMultistepSingleValue()
        {
            var classifier = new SDRClassifier(new[] { 1,2 });

            Classification<double> retVal = null;
            for (int i = 0; i < 10; i++)
            {
                retVal = _compute(classifier, i, new[] { 1, 5 }, 0, 10);
            }

            // Since overlap - should be previous with high likelihood
            double[] actValues = retVal.GetActualValues();

            Assert.AreEqual((double)actValues[0], 10);
            double[] resultDoubles1 = (double[]) retVal.GetStats(1);
            double[] resultDoubles2 = (double[]) retVal.GetStats(2);

            Assert.AreEqual(resultDoubles1[0], 1);
            Assert.AreEqual(resultDoubles2[0], 1);
        }

        /// <summary>
        /// Test multi-step predictions. We train the 0-step and the 1-step classifiers simultaneously on data stream
        /// (SDR1, bucketIdx0)
        /// (SDR2, bucketIdx1)
        /// (SDR1, bucketIdx0)
        /// (SDR2, bucketIdx1)
        /// ...
        /// 
        /// We intend the 0-step classifier to learn the associations:
        ///     SDR1    => bucketIdx 0
        ///     SDR2    => bucketIdx 1
        /// 
        /// and the 1-step classifier to learn the associations
        ///     SDR1    => bucketIdx 1
        ///     SDR2    => bucketIdx 0
        /// </summary>
        [TestMethod]
        public void TestMultistepPredictions()
        {
            var classifier = new SDRClassifier(new[] { 0, 1 }, 1.0, 0.1, 0);

            int[] sdr1 = new[] {1, 3, 5};
            int[] sdr2 = new[] {2, 4, 6};
            int recordNum = 0;

            for (int i = 0; i < 100; i++)
            {
                classifier.Compute<double>(recordNum: recordNum, patternNZ: sdr1,
                    classification: new Map<string, object> {{"bucketIdx", 0}, {"actValue", 0}}, 
                    learn: true, infer: false);
                recordNum++;

                classifier.Compute<double>(recordNum: recordNum, patternNZ: sdr2,
                    classification: new Map<string, object> { { "bucketIdx", 1 }, { "actValue", 1.0 } },
                    learn: true, infer: false);
                recordNum++;
            }

            var result1 = classifier.Compute<double>(recordNum: recordNum, patternNZ: sdr1,
                    classification: null, learn: false, infer: true);

            var result2 = classifier.Compute<double>(recordNum: recordNum, patternNZ: sdr2,
                    classification: null, learn: false, infer: true);

            Assert.AreEqual(1.0, result1.GetStats(0)[0], 0.01);
            Assert.AreEqual(0.0, result1.GetStats(0)[1], 0.01);
            Assert.AreEqual(0.0, result2.GetStats(0)[0], 0.01);
            Assert.AreEqual(1.0, result2.GetStats(0)[1], 0.01);
        }

        private Classification<double> _compute(SDRClassifier classifier, int recordNum, int[] pattern, int bucket, double value)
        {
            var classification = new Map<string, object> {{"bucketIdx", bucket}, {"actValue", value}};
            return classifier.Compute<double>(recordNum, classification, pattern, true, true);
        }
    }
}