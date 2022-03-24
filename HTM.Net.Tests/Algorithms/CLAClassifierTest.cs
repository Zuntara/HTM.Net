﻿using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Network;
using HTM.Net.Serialize;
using HTM.Net.Tests.Network;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class CLAClassifierTest
    {
        private CLAClassifier _classifier;

        public void SetUp()
        {
            _classifier = new CLAClassifier();
        }

        /**
         * Send same value 10 times and expect 100% likelihood for prediction.
         */
        [TestMethod]
        public void TestSingleValue()
        {
            SetUp();

            Classification<double> retVal = null;
            for (int recordNum = 0; recordNum < 10; recordNum++)
            {
                retVal = Compute<double>(_classifier, recordNum, new[] { 1, 5 }, 0, 10);
            }

            CheckValue(retVal, 0, 10.0, 1.0);
        }

        /**
         * Send same value 10 times and expect 100% likelihood for prediction 
         * using 0-step ahead prediction
         */
        [TestMethod]
        public void TestSingleValue0Steps()
        {
            _classifier = new CLAClassifier(new[] { 0 }, 0.001, 0.3, 0);

            // Enough times to perform Inference and learn associations
            Classification<double> retVal = null;
            for (int recordNum = 0; recordNum < 10; recordNum++)
            {
                retVal = Compute<double>(_classifier, recordNum, new[] { 1, 5 }, 0, 10);
            }

            Assert.AreEqual(10.0, retVal.GetActualValue(0), .00001);
            Assert.AreEqual(1.0, retVal.GetStat(0, 0), .00001);
        }

        /**
         * The meaning of this test is diminished in Java, because Java is already strongly typed and 
         * all expected value types are known and previously declared.
         */
        [TestMethod]
        public void TestComputeResultTypes()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            Classification<double> result = _classifier.Compute<double>(0, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            Assert.AreEqual(34.7, result.GetActualValue(0), 0.01);
        }

        [TestMethod]
        public void TestCompute1()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            Classification<double> result = _classifier.Compute<double>(0, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            Assert.AreEqual(34.7, result.GetActualValue(0), 0.01);
        }

        [TestMethod]
        public void TestCompute2()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            _classifier.Compute<double>(0, classification, new[] { 1, 5, 9 }, true, true);

            Classification<double> result = _classifier.Compute<double>(1, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(5, result.GetActualValueCount());
            Assert.AreEqual(34.7, result.GetActualValue(4), 0.01);
        }

        [TestMethod]
        public void TestComputeComplex()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            int recordNum = 0;
            Map<string, object> classification = new Map<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            Classification<double> result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 41.7);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 0, 6, 9, 11 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 44.9);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 6, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 42.9);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(35.520000457763672, result.GetActualValue(4), 0.00001);
            Assert.AreEqual(42.020000457763672, result.GetActualValue(5), 0.00001);
            Assert.AreEqual(6, result.GetStatCount(1));
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 3), 0.00001);
            Assert.AreEqual(0.12300123, result.GetStat(1, 4), 0.00001);
            Assert.AreEqual(0.87699877, result.GetStat(1, 5), 0.00001);
        }

        [TestMethod]
        public void TestComputeWithMissingValue()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Map<string, object> classification = new Map<string, object>();
            classification.Add("bucketIdx", null);
            classification.Add("actValue", null);
            Classification<double?> result = _classifier.Compute<double?>(0, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            Assert.AreEqual(null, result.GetActualValue(0));
        }

        [TestMethod]
        public void TestComputeCategory()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", "D");
            _classifier.Compute<string>(0, classification, new[] { 1, 5, 9 }, true, true);
            Classification<string> result = _classifier.Compute<string>(0, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual("D", result.GetActualValue(4));
        }

        [TestMethod]
        public void TestComputeCategory2()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            Map<string, object> classification = new Map<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", "D");
            _classifier.Compute<string>(0, classification, new[] { 1, 5, 9 }, true, true);
            classification.Add("actValue", "E");
            Classification<string> result = _classifier.Compute<string>(0, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual("D", result.GetActualValue(4));
        }

        [TestMethod]
        public void TestSerialization()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            int recordNum = 0;
            Map<string, object> classification = new Map<string, object>();
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            Classification<double> result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 41.7);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 0, 6, 9, 11 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 5);
            classification.Add("actValue", 44.9);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 6, 9 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 42.9);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            // Configure serializer
            SerialConfig config = new SerialConfig("testSerializerClassifier", SerialConfig.SERIAL_TEST_DIR);
            IPersistenceAPI api = Persistence.Get(config);

            // 1. Serialize
            byte[] data = api.Write(_classifier, "testSerializeClassifier");

            // 2. Deserialize
            CLAClassifier serialized = api.Read<CLAClassifier>(data);

            // Using the deserialized classifier, continue test
            classification.Add("bucketIdx", 4);
            classification.Add("actValue", 34.7);
            result = serialized.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);
            recordNum += 1;

            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, result.StepSet()));
            Assert.AreEqual(35.520000457763672, result.GetActualValue(4), 0.00001);
            Assert.AreEqual(42.020000457763672, result.GetActualValue(5), 0.00001);
            Assert.AreEqual(6, result.GetStatCount(1));
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 3), 0.00001);
            Assert.AreEqual(0.12300123, result.GetStat(1, 4), 0.00001);
            Assert.AreEqual(0.87699877, result.GetStat(1, 5), 0.00001);
        }

        [TestMethod]
        public void TestOverlapPattern()
        {
            SetUp();

            Classification<double> result = Compute<double>(_classifier, 0, new[] { 1, 5 }, 9, 9);
            result = Compute<double>(_classifier, 1, new[] { 1, 5 }, 9, 9);
            result = Compute<double>(_classifier, 1, new[] { 1, 5 }, 9, 9);
            result = Compute<double>(_classifier, 2, new[] { 3, 5 }, 2, 2);

            // Since overlap - should be previous with 100%
            CheckValue(result, 9, 9.0, 1.0);

            result = Compute<double>(_classifier, 3, new[] { 3, 5 }, 2, 2);

            // Second example: now new value should be more probable than old
            Assert.IsTrue(result.GetStat(1, 2) > result.GetStat(1, 9));
        }

        [TestMethod]
        public void TestScaling()
        {
            SetUp();

            int recordNum = 0;
            for (int i = 0; i < 100; i++, recordNum++)
            {
                Compute<double>(_classifier, recordNum, new[] { 1 }, 5, 5);
            }
            for (int i = 0; i < 1000; i++, recordNum++)
            {
                Compute<double>(_classifier, recordNum, new[] { 2 }, 9, 9);
            }
            for (int i = 0; i < 3; i++, recordNum++)
            {
                Compute<double>(_classifier, recordNum, new[] { 1, 2 }, 6, 6);
            }
        }

        [TestMethod]
        public void TestMultistepSingleValue()
        {
            SetUp();

            _classifier.Steps = new[] { 1, 2 };
            
            // Only should return one actual value bucket.
            Classification<double> result = null;
            int recordNum = 0;
            for (int i = 0; i < 10; i++, recordNum++)
            {
                result = Compute<double>(_classifier, recordNum, new[] { 1, 5 }, 0, 10);
            }

            Assert.IsTrue(Arrays.AreEqual(new double[] { 10.0 }, result.GetActualValues()));
            // Should have a probability of 100% for that bucket.
            Assert.IsTrue(Arrays.AreEqual(new double[] { 1.0 }, result.GetStats(1)));
            Assert.IsTrue(Arrays.AreEqual(new double[] { 1.0 }, result.GetStats(2)));
        }

        [TestMethod]
        public void TestMultistepSimple()
        {
            _classifier = new CLAClassifier(new[] { 1, 2 }, 0.001, 0.3, 0);

            Classification<double> result = null;
            int recordNum = 0;
            for (int i = 0; i < 100; i++, recordNum++)
            {
                result = Compute<double>(_classifier, recordNum, new[] { i % 10 }, i % 10, (i % 10) * 10);
            }

            // Only should return one actual value bucket.
            Assert.IsTrue(Arrays.AreEqual(new double[] { 0.0, 10.0, 20.0, 30.0, 40.0, 50.0, 60.0, 70.0, 80.0, 90.0 }, result.GetActualValues()));
            Assert.AreEqual(1.0, result.GetStat(1, 0), 0.1);
            for (int i = 1; i < 10; i++)
            {
                Assert.AreEqual(0.0, result.GetStat(1, i), 0.1);
            }
            Assert.AreEqual(1.0, result.GetStat(2, 1), 0.1);
        }

        /**
         * Test missing record support.
         *
         * Here, we intend the classifier to learn the associations:
         *   [1,3,5] => bucketIdx 1
         *   [2,4,6] => bucketIdx 2
         *   [7,8,9] => don't care
         *
         *  If it doesn't pay attention to the recordNums in this test, it will learn the
         *  wrong associations.
         */
        [TestMethod]
        public void TestMissingRecords()
        {
            _classifier = new CLAClassifier(new[] { 1 }, 0.1, 0.1, 0);
            int recordNum = 0;
            Map<string, object> classification = new Map<string, object>();
            classification.Add("bucketIdx", 0);
            classification.Add("actValue", 0);
            _classifier.Compute<double>(recordNum, classification, new[] { 1, 3, 5 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 1);
            classification.Add("actValue", 1);
            _classifier.Compute<double>(recordNum, classification, new[] { 2, 4, 6 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 2);
            classification.Add("actValue", 2);
            _classifier.Compute<double>(recordNum, classification, new[] { 1, 3, 5 }, true, true);
            recordNum += 1;

            classification.Add("bucketIdx", 1);
            classification.Add("actValue", 1);
            _classifier.Compute<double>(recordNum, classification, new[] { 2, 4, 6 }, true, true);
            recordNum += 1;

            // ----------------------------------------------------------------------------------
            // At this point, we should have learned [1, 3, 5] => bucket 1
            //                                       [2, 4, 6] => bucket 2
            classification.Add("bucketIdx", 2);
            classification.Add("actValue", 2);
            Classification<double> result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 3, 5 }, true, true);
            recordNum += 1;
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(1.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);

            classification.Add("bucketIdx", 1);
            classification.Add("actValue", 1);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 2, 4, 6 }, true, true);
            recordNum += 1;
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(1.0, result.GetStat(1, 2), 0.00001);


            // ----------------------------------------------------------------------------------
            // Feed in records that skip and make sure they don't mess up what we learned
            //
            // If we skip a record, the CLA should NOT learn that [2,4,6] from
            // the previous learning associates with bucket 0
            recordNum += 1; // <----- Does the skip

            classification.Add("bucketIdx", 0);
            classification.Add("actValue", 0);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 3, 5 }, true, true);
            recordNum += 1;
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(1.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);

            // If we skip a record, the CLA should NOT learn that [1,3,5] from
            // the previous learning associates with bucket 0
            recordNum += 1; // <----- Does the skip

            classification.Add("bucketIdx", 0);
            classification.Add("actValue", 0);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 2, 4, 6 }, true, true);
            recordNum += 1;
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(1.0, result.GetStat(1, 2), 0.00001);

            // If we skip a record, the CLA should NOT learn that [2,4,6] from
            // the previous learning associates with bucket 0
            recordNum += 1; // <----- Does the skip

            classification.Add("bucketIdx", 0);
            classification.Add("actValue", 0);
            result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 3, 5 }, true, true);
            recordNum += 1;
            Assert.AreEqual(0.0, result.GetStat(1, 0), 0.00001);
            Assert.AreEqual(1.0, result.GetStat(1, 1), 0.00001);
            Assert.AreEqual(0.0, result.GetStat(1, 2), 0.00001);
        }

        /**
         * Test missing record edge TestCase
         * Test an edge case in the classifier initialization when there is a missing
         * record in the first n records, where n is the # of prediction steps.
         */
        [TestMethod]
        public void TestMissingRecordInitialization()
        {
            _classifier = new CLAClassifier(new[] { 2 }, 0.1, 0.1, 0);
            int recordNum = 0;
            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", 0);
            classification.Add("actValue", 34.7);
            _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);

            recordNum = 2;
            Classification<double> result = _classifier.Compute<double>(recordNum, classification, new[] { 1, 5, 9 }, true, true);

            Assert.IsTrue(Arrays.AreEqual(new[] { 2 }, result.StepSet()));
            Assert.AreEqual(1, result.GetActualValueCount());
            Assert.AreEqual(34.7, result.GetActualValue(0), 0.01);
        }

        public void CheckValue<T>(Classification<T> retVal, int index, object value, double probability)
        {
            Assert.AreEqual(retVal.GetActualValue(index), value);
            Assert.AreEqual(probability, retVal.GetStat(1, index), 0.01);
        }

        public Classification<T> Compute<T>(CLAClassifier classifier, int recordNum, int[] pattern,
            int bucket, object value)
        {

            Dictionary<string, object> classification = new Dictionary<string, object>();
            classification.Add("bucketIdx", bucket);
            classification.Add("actValue", value);
            return classifier.Compute<T>(recordNum, classification, pattern, true, true);
        }
    }
}