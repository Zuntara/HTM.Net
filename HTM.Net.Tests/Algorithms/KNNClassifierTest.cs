using System;
using System.Diagnostics;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Util;
using MathNet.Numerics.LinearAlgebra;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Algorithms
{
    [TestClass]
    public class KNNClassifierTest
    {
        private KNNClassifier InitClassifier(Parameters p)
        {
            KNNClassifier.Builder builder = KNNClassifier.GetBuilder();
            KNNClassifier knn = builder.Build();
            p.Apply(knn);

            return knn;
        }

        [TestMethod]
        public void TestBuilder()
        {
            KNNClassifier.Builder builder = KNNClassifier.GetBuilder();

            builder.K(42)
            .Exact(true)
            .DistanceNorm(12.5)
            .DistanceMethod(DistanceMethod.PctInputOverlap)
            .DistanceThreshold(2.3)
            .DoBinarization(true)
            .BinarizationThreshold(3.0)
            .UseSparseMemory(true)
            .SparseThreshold(349.0)
            .RelativeThreshold(true)
            .NumWinners(100)
            .NumSVDSamples(4)
            .NumSVDDims((int)KnnMode.ADAPTIVE)
            .FractionOfMax(0.84)
            .MaxStoredPatterns(30)
            .ReplaceDuplicates(true)
            .CellsPerCol(32);

            KNNClassifier knn = builder.Build();

            Assert.AreEqual(42, knn.GetK());
            Assert.IsTrue(knn.IsExact());
            Assert.AreEqual(12.5, knn.GetDistanceNorm(), 0.0);
            Assert.AreEqual(DistanceMethod.PctInputOverlap, knn.GetDistanceMethod());
            Assert.AreEqual(2.3, knn.GetDistanceThreshold(), 0.0);
            Assert.IsTrue(knn.IsDoBinarization());
            Assert.AreEqual(3.0, knn.GetBinarizationThreshold(), 0.0);
            Assert.IsTrue(knn.IsRelativeThreshold());
            Assert.IsTrue(knn.IsUseSparseMemory());
            Assert.AreEqual(349.0, knn.GetSparseThreshold(), 0.0);
            Assert.AreEqual(100, knn.GetNumWinners());
            Assert.AreEqual(4, knn.GetNumSVDSamples());
            Assert.AreEqual((int)KnnMode.ADAPTIVE, knn.GetNumSVDDims());
            Assert.AreEqual(0.84, knn.GetFractionOfMax().GetValueOrDefault(), 0.0);
            Assert.AreEqual(30, knn.GetMaxStoredPatterns());
            Assert.IsTrue(knn.IsReplaceDuplicates());
            Assert.AreEqual(32, knn.GetCellsPerCol());
        }

        [TestMethod]
        public void TestParameterBuild()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.Norm);
            p.SetParameterByKey(Parameters.KEY.DISTANCE_NORM, 12.5);
            p.SetParameterByKey(Parameters.KEY.K, 42);
            p.SetParameterByKey(Parameters.KEY.EXACT, true);
            p.SetParameterByKey(Parameters.KEY.DISTANCE_THRESHOLD, 2.3);
            p.SetParameterByKey(Parameters.KEY.DO_BINARIZATION, true);
            p.SetParameterByKey(Parameters.KEY.BINARIZATION_THRESHOLD, 3.0);
            p.SetParameterByKey(Parameters.KEY.RELATIVE_THRESHOLD, true);
            p.SetParameterByKey(Parameters.KEY.USE_SPARSE_MEMORY, true);
            p.SetParameterByKey(Parameters.KEY.SPARSE_THRESHOLD, 349.0);
            p.SetParameterByKey(Parameters.KEY.NUM_WINNERS, 100);
            p.SetParameterByKey(Parameters.KEY.NUM_SVD_SAMPLES, 4);
            p.SetParameterByKey(Parameters.KEY.NUM_SVD_DIMS, (int?)KnnMode.ADAPTIVE);
            p.SetParameterByKey(Parameters.KEY.FRACTION_OF_MAX, .84);
            p.SetParameterByKey(Parameters.KEY.MAX_STORED_PATTERNS, 30);
            p.SetParameterByKey(Parameters.KEY.REPLACE_DUPLICATES, true);
            p.SetParameterByKey(Parameters.KEY.KNN_CELLS_PER_COL, 32);

            KNNClassifier knn = InitClassifier(p);

            Assert.AreEqual(knn.GetK(), 42);
            Assert.IsTrue(knn.IsExact());
            Assert.AreEqual(12.5, knn.GetDistanceNorm(), 0.0);
            Assert.AreEqual(DistanceMethod.Norm, knn.GetDistanceMethod());
            Assert.AreEqual(2.3, knn.GetDistanceThreshold(), 0.0);
            Assert.IsTrue(knn.IsDoBinarization());
            Assert.AreEqual(3.0, knn.GetBinarizationThreshold(), 0.0);
            Assert.IsTrue(knn.IsRelativeThreshold());
            Assert.IsTrue(knn.IsUseSparseMemory());
            Assert.AreEqual(349.0, knn.GetSparseThreshold(), 0.0);
            Assert.AreEqual(100, knn.GetNumWinners());
            Assert.AreEqual(4, knn.GetNumSVDSamples());
            Assert.AreEqual((int)KnnMode.ADAPTIVE, knn.GetNumSVDDims());
            Assert.AreEqual(0.84, knn.GetFractionOfMax().GetValueOrDefault(), 0.0);
            Assert.AreEqual(30, knn.GetMaxStoredPatterns());
            Assert.IsTrue(knn.IsReplaceDuplicates());
            Assert.AreEqual(32, knn.GetCellsPerCol());
        }

        [TestMethod]
        public void SparsifyVector()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.Norm);
            p.SetParameterByKey(Parameters.KEY.DISTANCE_NORM, 2.0);

            // Each of the 4 tests correspond with the each decisional branch in the
            // sparsifyVector method

            // tests: if not relativeThreshold:
            KNNClassifier classifier = InitClassifier(p);
            Vector<double> inputPattern = Vector<double>.Build.SparseOfArray(new [] { 0.0, 1, 3, 7, 11 });
            Vector<double> outputPattern = classifier.SparsifyVector(inputPattern, true);
            Assert.IsTrue(Arrays.AreEqual(inputPattern, outputPattern.ToArray()));

            // tests: elif self.sparseThreshold > 0:
            p.SetParameterByKey(Parameters.KEY.RELATIVE_THRESHOLD, true);
            p.SetParameterByKey(Parameters.KEY.SPARSE_THRESHOLD, 0.2);
            classifier = InitClassifier(p);
            outputPattern = classifier.SparsifyVector(inputPattern, true);
            Assert.IsTrue(Arrays.AreEqual(new double[] { 0, 0, 3, 7, 11 }, outputPattern.ToArray()));

            // tests: if doWinners:
            p.SetParameterByKey(Parameters.KEY.RELATIVE_THRESHOLD, true);
            p.SetParameterByKey(Parameters.KEY.SPARSE_THRESHOLD, 0.2);
            p.SetParameterByKey(Parameters.KEY.NUM_WINNERS, 2);
            classifier = InitClassifier(p);
            outputPattern = classifier.SparsifyVector(inputPattern, true);
            Assert.IsTrue(Arrays.AreEqual(new double[] { 0, 0, 0, 0, 0 }, outputPattern.ToArray()), "doWinners failed");

            // tests: Do binarization
            p.SetParameterByKey(Parameters.KEY.RELATIVE_THRESHOLD, true);
            p.SetParameterByKey(Parameters.KEY.SPARSE_THRESHOLD, 0.2);
            p.SetParameterByKey(Parameters.KEY.DO_BINARIZATION, true);
            p.ClearParameter(Parameters.KEY.NUM_WINNERS);
            classifier = InitClassifier(p);
            outputPattern = classifier.SparsifyVector(inputPattern, true);
            Assert.IsTrue(Arrays.AreEqual(new double[] { 0.0, 0.0, 1.0, 1.0, 1.0 }, outputPattern.ToArray()));
        }

        [TestMethod]
        public void TestDistanceMetrics()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.Norm);
            p.SetParameterByKey(Parameters.KEY.DISTANCE_NORM, 2.0);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            double[] protoA = { 0, 1, 3, 7, 11 };
            double[] protoB = { 20, 28, 30 };

            classifier.Learn(protoA, 0, null, dimensionality);
            classifier.Learn(protoB, 0, null, dimensionality);

            // input is an arbitrary point, close to protoA, orthogonal to protoB
            Vector<double> input = Vector<double>.Build.Dense(dimensionality, i => i < 4 ? 1.0 : 0.0);
     
            // input0 is used to test that the distance from a point to itself is 0
            Vector<double> input0 = Vector<double>.Build.Dense(dimensionality, i => protoA.Contains(i) ? 1.0 : 0.0);

            // Test l2 norm metric
            var result = classifier.Infer(input);
            Vector<double> dist = result.GetProtoDistance();
            var l2Distances = new[] { 0.65465367, 1.0 };
            foreach (var tuple in ArrayUtils.Zip(l2Distances, dist))
            {
                Assert.AreEqual((double)tuple.Item1, (double)tuple.Item2, 0.00001, "l2 distance norm is not calculated as expected.");
            }

            result = classifier.Infer(input0);
            var dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "l2 norm did not calculate 0 distance as expected.");

            // Test l1 norm metric
            p.SetParameterByKey(Parameters.KEY.DISTANCE_NORM, 1.0);
            p.Apply(classifier);
            result = classifier.Infer(input.Select(i => i).ToArray());
            dist = result.GetProtoDistance();
            var l1Distances = new[] { 0.42857143, 1.0 };
            foreach (var tuple in ArrayUtils.Zip(l1Distances, dist))
            {
                Assert.AreEqual((double)tuple.Item1, (double)tuple.Item2, 0.00001, "l1 distance norm is not calculated as expected.");
            }

            result = classifier.Infer(input0.Select(i => i).ToArray());
            dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "l1 norm did not calculate 0 distance as expected.");

            // Test raw overlap metric
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);
            p.Apply(classifier);

            result = classifier.Infer(input.Select(i => i).ToArray());
            dist = result.GetProtoDistance();
            double[] rawOverlaps = new[] { 1.0, 4.0 };
            foreach (var tuple in ArrayUtils.Zip(rawOverlaps, dist))
            {
                Assert.AreEqual(tuple.Item1, tuple.Item2, "Raw overlap is not calculated as expected.");
            }

            result = classifier.Infer(input0.Select(i => i).ToArray());
            dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "Raw overlap did not calculate 0 distance as expected.");

            // Test pctOverlapOfInput metric
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.PctInputOverlap);
            p.Apply(classifier);

            result = classifier.Infer(input.Select(i => i).ToArray());
            dist = result.GetProtoDistance();
            double[] pctOverlaps = new[] { 0.25, 1.0 };
            foreach (var tuple in ArrayUtils.Zip(pctOverlaps, dist))
            {
                Assert.AreEqual(tuple.Item1, tuple.Item2, "pctOverlapOfInput is not calculated as expected.");
            }

            result = classifier.Infer(input0.Select(i => i).ToArray());
            dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "pctOverlapOfInput did not calculate 0 distance as expected.");

            // Test pctOverlapOfProto  metric
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.PctProtoOverlap);
            p.Apply(classifier);

            result = classifier.Infer(input.Select(i => i).ToArray());
            dist = result.GetProtoDistance();
            pctOverlaps = new[] { 0.40, 1.0 };
            foreach (var tuple in ArrayUtils.Zip(pctOverlaps, dist))
            {
                Assert.AreEqual(tuple.Item1, tuple.Item2, "pctOverlapOfProto  is not calculated as expected.");
            }

            result = classifier.Infer(input0.Select(i => i).ToArray());
            dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "pctOverlapOfProto  did not calculate 0 distance as expected.");

            // Test pctOverlapOfLarger   metric
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.PctLargerOverlap);
            p.Apply(classifier);

            result = classifier.Infer(input.Select(i => i).ToArray());
            dist = result.GetProtoDistance();
            pctOverlaps = new[] { 0.40, 1.0 };
            foreach (var tuple in ArrayUtils.Zip(pctOverlaps, dist))
            {
                Assert.AreEqual(tuple.Item1, tuple.Item2, "pctOverlapOfLarger   is not calculated as expected.");
            }

            result = classifier.Infer(input0.Select(i => i).ToArray());
            dist0 = result.GetProtoDistance();
            Assert.AreEqual(0.0, dist0[0], "pctOverlapOfLarger   did not calculate 0 distance as expected.");
        }

        [TestMethod]
        public void TestOverlapDistanceMethodStandard()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            var b = new double[] { 2, 4, 8, 12, 14, 18, 20, 28, 30 };

            int numPatterns = classifier.Learn(a, 0, isSparse: dimensionality);
            Assert.AreEqual(1, numPatterns);

            numPatterns = classifier.Learn(b, 1, isSparse: dimensionality);
            Assert.AreEqual(2, numPatterns);

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }
            var result = classifier.Infer(denseA);
            var category = result.GetWinner();
            Assert.AreEqual(0, category);

            var denseB = new double[dimensionality];
            foreach (int index in b)
            {
                denseB[index] = 1;
            }
            result = classifier.Infer(denseB);
            category = result.GetWinner();
            Assert.AreEqual(1, category);
        }

        [TestMethod]
        public void TestPartitionIdExcluded()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            var b = new double[] { 2, 4, 8, 12, 14, 18, 20, 28, 30 };

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }

            var denseB = new double[dimensionality];
            foreach (int index in b)
            {
                denseB[index] = 1;
            }

            int numPatterns = classifier.Learn(a, 0, isSparse: dimensionality, partitionId: 0);
            Assert.AreEqual(1, numPatterns);

            numPatterns = classifier.Learn(b, 1, isSparse: dimensionality, partitionId: 1);
            Assert.AreEqual(2, numPatterns);

            var result = classifier.Infer(denseA, partitionId: 1);
            var category = result.GetWinner();
            Assert.AreEqual(0, category);

            result = classifier.Infer(denseA, partitionId: 0);
            category = result.GetWinner();
            Assert.AreEqual(1, category);

            result = classifier.Infer(denseB, partitionId: 0);
            category = result.GetWinner();
            Assert.AreEqual(1, category);

            result = classifier.Infer(denseB, partitionId: 1);
            category = result.GetWinner();
            Assert.AreEqual(0, category);

            // Ensure it works even if you invoke learning again. To make it a bit more
            // complex this time we insert A again but now with Id=2
            classifier.Learn(a, 0, isSparse: dimensionality, partitionId: 2);
            // Even though first A should be ignored, the second instance of A should
            // not be ignored.
            result = classifier.Infer(denseA, partitionId: 0);
            category = result.GetWinner();
            Assert.AreEqual(0, category);
        }

        /// <summary>
        /// Test a sequence of calls to KNN to ensure we can retrieve partition Id:
        /// - We first learn on some patterns(including one pattern with no
        ///   partitionId in the middle) and test that we can retrieve Ids.
        /// - We then invoke inference and then check partitionId again.
        /// - We check incorrect indices to ensure we get an exception.
        /// - We check the case where the partitionId to be ignored is not in
        ///   the list.
        /// - We learn on one more pattern and check partitionIds again
        /// - We remove rows and ensure partitionIds still work
        /// </summary>
        [TestMethod]
        public void TestGetPartitionId()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            var b = new double[] { 2, 4, 8, 12, 14, 18, 20, 28, 30 };
            var c = new double[] { 1, 2, 3, 14, 16, 19, 22, 24, 33 };
            var d = new double[] { 2, 4, 8, 12, 14, 19, 22, 24, 33 };
            var e = new double[] { 1, 3, 7, 12, 14, 19, 22, 24, 33 };

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }

            classifier.Learn(a, 0, isSparse: dimensionality, partitionId: 433);
            classifier.Learn(b, 1, isSparse: dimensionality, partitionId: 213);
            classifier.Learn(c, 1, isSparse: dimensionality, partitionId: null);
            classifier.Learn(d, 1, isSparse: dimensionality, partitionId: 433);

            Assert.AreEqual(433, classifier.GetPartitionId(0));
            Assert.AreEqual(213, classifier.GetPartitionId(1));
            Assert.AreEqual(null, classifier.GetPartitionId(2));
            Assert.AreEqual(433, classifier.GetPartitionId(3));

            var result = classifier.Infer(denseA, partitionId: 213);
            var category = result.GetWinner();
            Assert.AreEqual(0, category);

            // Test with patternId not in classifier
            result = classifier.Infer(denseA, partitionId: 666);
            category = result.GetWinner();
            Assert.AreEqual(0, category);

            // Partition Ids should be maintained after inference
            Assert.AreEqual(433, classifier.GetPartitionId(0));
            Assert.AreEqual(213, classifier.GetPartitionId(1));
            Assert.AreEqual(null, classifier.GetPartitionId(2));
            Assert.AreEqual(433, classifier.GetPartitionId(3));

            // Should return exceptions if we go out of bounds
            try
            {
                classifier.GetPartitionId(4);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException) { }
            catch { Assert.Fail(); }

            try
            {
                classifier.GetPartitionId(-1);
                Assert.Fail();
            }
            catch (IndexOutOfRangeException) { }
            catch { Assert.Fail(); }

            // Learn again
            classifier.Learn(e, 4, isSparse: dimensionality, partitionId: 413);
            Assert.AreEqual(413, classifier.GetPartitionId(4));

            // Test getPatternIndicesWithPartitionId
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 3 }, classifier.GetPatternIndicesWithPartitionId(433)));
            Assert.IsTrue(Arrays.AreEqual(new int[0], classifier.GetPatternIndicesWithPartitionId(666)));
            Assert.IsTrue(Arrays.AreEqual(new[] { 4 }, classifier.GetPatternIndicesWithPartitionId(413)));

            Assert.AreEqual(3, classifier.GetNumPartitionIds());

            // Check that the full set of partition ids is what we expect
            Assert.IsTrue(Arrays.AreEqual(new[] { 433, 213, int.MaxValue, 433, 413 }, classifier.GetPartitionIdPerPattern()));
            Assert.IsTrue(Arrays.AreEqual(new[] { 433, 213, 413 }, classifier.GetPartitionIdList()));

            // Remove two rows - all indices shift down
            Assert.AreEqual(2, classifier.RemoveRows(new[] { 0, 2 }));
            Assert.IsTrue(Arrays.AreEqual(new[] { 1 }, classifier.GetPatternIndicesWithPartitionId(433)));
            Assert.IsTrue(Arrays.AreEqual(new[] { 2 }, classifier.GetPatternIndicesWithPartitionId(413)));

            // Remove another row and check number of partitions have decreased
            Assert.AreEqual(1, classifier.RemoveRows(new[] { 0 }));
            Assert.AreEqual(2, classifier.GetNumPartitionIds());
            // Check that the full set of partition ids is what we expect
            Assert.IsTrue(Arrays.AreEqual(new[] { 433, 413 }, classifier.GetPartitionIdPerPattern()));
            Assert.IsTrue(Arrays.AreEqual(new[] { 433, 413 }, classifier.GetPartitionIdList()));
        }

        [TestMethod]
        public void TestGetPartitionIdWithNoIdsAtFirst()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            var b = new double[] { 2, 4, 8, 12, 14, 18, 20, 28, 30 };
            var c = new double[] { 1, 2, 3, 14, 16, 19, 22, 24, 33 };
            var d = new double[] { 2, 4, 8, 12, 14, 19, 22, 24, 33 };

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }

            var denseD = new double[dimensionality];
            foreach (int index in d)
            {
                denseD[index] = 1;
            }

            classifier.Learn(a, 0, isSparse: dimensionality, partitionId: null);
            classifier.Learn(b, 1, isSparse: dimensionality, partitionId: null);
            classifier.Learn(c, 2, isSparse: dimensionality, partitionId: 211);
            classifier.Learn(d, 1, isSparse: dimensionality, partitionId: 405);

            var result = classifier.Infer(denseA, partitionId: 405);
            var category = result.GetWinner();
            Assert.AreEqual(0, category);

            result = classifier.Infer(denseD, partitionId: 405);
            category = result.GetWinner();
            Assert.AreEqual(2, category);

            result = classifier.Infer(denseD);
            category = result.GetWinner();
            Assert.AreEqual(1, category);
        }

        /// <summary>
        /// Sparsity (input dimensionality) less than input array
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestOverlapDistanceMethodBadSparsity()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            // Learn with incorrect dimensionality, less than some bits (23, 29)
            classifier.Learn(a, 0, isSparse: 20);
        }

        /// <summary>
        /// Inconsistent sparsity (input dimensionality)
        /// </summary>
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestOverlapDistanceMethodInconsistentDimensionality()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            // Learn with incorrect dimensionality, greater than largest ON bit, but
            // inconsistent when inferring
            int numPatterns = classifier.Learn(a, 0, isSparse: 31);
            Assert.AreEqual(1, numPatterns);

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }

            var result = classifier.Infer(denseA);
            var category = result.Get(0);
            Assert.AreEqual(0, category);
        }

        /// <summary>
        /// If sparse representation indices are unsorted expect error.
        /// </summary>
        [TestMethod]
        public void TestOverlapDistanceMethodStandardUnsorted()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 29, 3, 7, 11, 13, 17, 19, 23, 1 };
            var b = new double[] { 2, 4, 20, 12, 14, 18, 8, 28, 30 };

            try
            {
                classifier.Learn(a, 0, isSparse: dimensionality);
                Assert.Fail("Should throw exception");
            }
            catch (InvalidOperationException) { }
            catch (Exception e) { Assert.Fail(e.ToString()); }

            try
            {
                classifier.Learn(b, 1, isSparse: dimensionality);
                Assert.Fail("Should throw exception");
            }
            catch (InvalidOperationException) { }
            catch (Exception e) { Assert.Fail(e.ToString()); }
        }

        /// <summary>
        /// Tests case where pattern has no ON bits
        /// </summary>
        [TestMethod]
        public void TestOverlapDistanceMethodEmptyArray()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { };

            int numPatterns = classifier.Learn(a, 0, isSparse: dimensionality);
            Assert.AreEqual(1, numPatterns);

            var denseA = new double[dimensionality];
            foreach (int index in a)
            {
                denseA[index] = 1;
            }

            var result = classifier.Infer(denseA);
            var category = result.GetWinner();
            Assert.AreEqual(0, category);
        }

        // Finish when infer has options for sparse and dense: https://github.com/numenta/nupic/issues/2198
        //[TestMethod]
        public void TestOverlapDistanceMethod_ClassifySparse()
        {
            Parameters p = Parameters.GetKnnDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.DISTANCE_METHOD, DistanceMethod.RawOverlap);

            KNNClassifier classifier = InitClassifier(p);

            int dimensionality = 40;
            var a = new double[] { 1, 3, 7, 11, 13, 17, 19, 23, 29 };
            var b = new double[] { 2, 4, 20, 12, 14, 18, 8, 28, 30 };

            classifier.Learn(a, 0, isSparse: dimensionality);
            classifier.Learn(b, 1, isSparse: dimensionality);

            // TODO Test case where infer is passed a sparse representation after
            // infer() has been extended to handle sparse and dense

            var result = classifier.Infer(a.Select(i => (double)i).ToArray());
            var category = result.Get(0);
            Assert.AreEqual(0, category);

            result = classifier.Infer(b.Select(i => (double)i).ToArray());
            category = result.Get(0);
            Assert.AreEqual(1, category);
        }
    }
}