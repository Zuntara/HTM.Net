using System;
using System.Collections.Generic;
using System.Diagnostics;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Encoders
{
    [TestClass]
    public class CoordinateEncoderTest
    {
        private CoordinateEncoder ce;
        private CoordinateEncoder.Builder builder;

        private bool verbose;

        private void SetUp()
        {
            builder = (CoordinateEncoder.Builder)CoordinateEncoder.GetBuilder()
                .Name("coordinate")
                .N(33)
                .W(3);
        }

        private void InitCe()
        {
            ce = (CoordinateEncoder)builder.Build();
        }

        [TestMethod]
        public void TestInvalidW()
        {
            SetUp();
            InitCe();

            // Even
            try
            {
                SetUp();
                builder.N(45);
                builder.W(4);
                //Should fail here
                InitCe();

                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("w must be odd, and must be a positive integer", e.Message);
            }

            // 0
            try
            {
                SetUp();
                builder.N(45);
                builder.W(0);
                //Should fail here
                InitCe();

                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("w must be odd, and must be a positive integer", e.Message);
            }

            // Negative
            try
            {
                SetUp();
                builder.N(45);
                builder.W(-2);
                //Should fail here
                InitCe();

                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("w must be odd, and must be a positive integer", e.Message);
            }
        }

        [TestMethod]
        public void TestInvalidN()
        {
            SetUp();
            InitCe();

            // Even
            try
            {
                SetUp();
                builder.N(11);
                builder.W(3);
                //Should fail here
                InitCe();

                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("n must be an int strictly greater than 6*w. For " +
                    "good results we recommend n be strictly greater than 11*w", e.Message);
            }
        }

        [TestMethod]
        public void TestOrderForCoordinate()
        {
            CoordinateEncoder c = new CoordinateEncoder();
            double h1 = c.OrderForCoordinate(new[] { 2, 5, 10 });
            double h2 = c.OrderForCoordinate(new[] { 2, 5, 11 });
            double h3 = c.OrderForCoordinate(new[] { 2497477, -923478 });

            Assert.IsTrue(0 <= h1 && h1 < 1);
            Assert.IsTrue(0 <= h2 && h2 < 1);
            Assert.IsTrue(0 <= h3 && h3 < 1);

            Console.WriteLine(h1 + ", " + h2 + ", " + h3);

            Assert.AreNotEqual(h1, h2);
            Assert.AreNotEqual(h2, h3);
        }

        [TestMethod]
        public void TestBitForCoordinate()
        {
            CoordinateEncoder ce = new CoordinateEncoder();
            int n = 1000;
            double b1 = ce.BitForCoordinate(new[] { 2, 5, 10 }, n);
            double b2 = ce.BitForCoordinate(new[] { 2, 5, 11 }, n);
            double b3 = ce.BitForCoordinate(new[] { 2497477, -923478 }, n);

            Assert.IsTrue(0 <= b1 && b1 < n);
            Assert.IsTrue(0 <= b2 && b2 < n);
            Assert.IsTrue(0 <= b3 && b3 < n);

            Assert.AreNotEqual(b1, b2);
            Assert.AreNotEqual(b2, b3);

            // Small n
            n = 2;
            double b4 = ce.BitForCoordinate(new[] { 5, 10 }, n);

            Assert.IsTrue(0 <= b4 && b4 < n);
        }

        [TestMethod]
        public void TestTopWCoordinates()
        {
            int[][] coordinates = new[] { new[] { 1 }, new[] { 2 }, new[] { 3 }, new[] { 4 }, new[] { 5 } };

            //ICoordinateOrder mock = new CoordinateOrder() {
            //        @Override public double orderForCoordinate(int[] coordinate)
            //    {
            //        return ArrayUtils.sum(coordinate) / 5.0d;
            //    }

            //};

            var mock = new Mock<ICoordinateOrder>();
            mock.Setup(co => co.OrderForCoordinate(It.IsAny<int[]>()))
                .Returns<int[]>(coordinate => ArrayUtils.Sum(coordinate) / 5.0d);

            int[][] top = new CoordinateEncoder().TopWCoordinates(mock.Object, coordinates, 2);

            Assert.AreEqual(2, top.Length);

            Assert.IsTrue(Arrays.AreEqual(new[] { 4 }, top[0]));

            Assert.IsTrue(Arrays.AreEqual(new[] { 5 }, top[1]));
        }

        [TestMethod]
        public void TestNeighbors1D()
        {
            CoordinateEncoder ce = new CoordinateEncoder();

            int[] coordinate = { 100 };
            int radius = 5;
            List<int[]> neighbors = ce.Neighbors(coordinate, radius);
            Assert.AreEqual(11, neighbors.Count);
            Assert.IsTrue(Arrays.AreEqual(new[] { 95 }, neighbors[0]));
            Assert.IsTrue(Arrays.AreEqual(new[] { 100 }, neighbors[5]));
            Assert.IsTrue(Arrays.AreEqual(new[] { 105 }, neighbors[10]));
        }

        [TestMethod]
        public void TestNeighbors2D()
        {
            CoordinateEncoder ce = new CoordinateEncoder();

            int[] coordinate = new[] { 100, 200 };
            int radius = 5;
            List<int[]> neighbors = ce.Neighbors(coordinate, radius);
            Assert.AreEqual(121, neighbors.Count);
            Assert.IsTrue(ArrayUtils.Contains(new[] { 95, 195 }, neighbors));
            Assert.IsTrue(ArrayUtils.Contains(new[] { 95, 205 }, neighbors));
            Assert.IsTrue(ArrayUtils.Contains(new[] { 100, 200 }, neighbors));
            Assert.IsTrue(ArrayUtils.Contains(new[] { 105, 195 }, neighbors));
            Assert.IsTrue(ArrayUtils.Contains(new[] { 105, 205 }, neighbors));
        }

        [TestMethod]
        public void TestNeighbors0Radius()
        {
            CoordinateEncoder ce = new CoordinateEncoder();

            int[] coordinate = new[] { 100, 200, 300 };
            int radius = 0;
            List<int[]> neighbors = ce.Neighbors(coordinate, radius);
            Assert.AreEqual(1, neighbors.Count);
            Assert.IsTrue(ArrayUtils.Contains(new[] { 100, 200, 300 }, neighbors));
        }

        [TestMethod]
        public void TestEncodeIntoArray()
        {
            SetUp();
            builder.N(33);
            builder.W(3);
            InitCe();

            CoordinateEncoder.ResetRandomGenerator();

            int[] coordinate = new[] { 100, 200 };
            int[] output1 = Encode(ce, coordinate, 5);
            Assert.AreEqual(ArrayUtils.Sum(output1), ce.GetW());

            int[] output2 = Encode(ce, coordinate, 5);
            Assert.IsTrue(Arrays.AreEqual(output1, output2));
        }

        [TestMethod]
        public void TestEncodeSaturateArea()
        {
            SetUp();
            builder.N(1999);
            builder.W(25);
            builder.Radius(2);
            InitCe();

            CoordinateEncoder.ResetRandomGenerator();

            int[] outputA = Encode(ce, new[] { 0, 0 }, 2);
            int[] outputB = Encode(ce, new[] { 0, 1 }, 2);

            Assert.AreEqual(0.8, Overlap(outputA, outputB), 0.019);
        }

        /**
         * As you get farther from a coordinate, the overlap should decrease
         */
        [TestMethod]
        public void TestEncodeRelativePositions()
        {
            CoordinateEncoder.ResetRandomGenerator();

            // As you get farther from a coordinate, the overlap should decrease
            double[] overlaps = OverlapsForRelativeAreas(999, 25, new[] { 100, 200 }, 10,
                new[] { 2, 2 }, 0, 5, false);
            AssertDecreasingOverlaps(overlaps);
        }

        /**
         * As radius increases, the overlap should decrease
         */
        [TestMethod]
        public void TestEncodeRelativeRadii()
        {
            // As radius increases, the overlap should decrease
            double[] overlaps = OverlapsForRelativeAreas(999, 25, new[] { 100, 200 }, 5,
                null, 1, 5, false);
            AssertDecreasingOverlaps(overlaps);

            // As radius decreases, the overlap should decrease
            overlaps = OverlapsForRelativeAreas(999, 25, new[] { 100, 200 }, 20,
                null, -2, 5, false);
            AssertDecreasingOverlaps(overlaps);
        }

        /**
         * As radius increases, the overlap should decrease
         */
        [TestMethod]
        public void TestEncodeRelativePositionsAndRadii()
        {
            // As radius increases and positions change, the overlap should decrease
            double[] overlaps = OverlapsForRelativeAreas(999, 25, new[] { 100, 200 }, 5,
                new[] { 1, 1 }, 1, 5, false);
            AssertDecreasingOverlaps(overlaps);
        }

        [TestMethod]
        public void TestEncodeUnrelatedAreas()
        {
            double avgThreshold = 0.3;
            double maxThreshold = 0.14;

            CoordinateEncoder.ResetRandomGenerator();

            double[] overlaps = OverlapsForUnrelatedAreas(1499, 37, 5, 100, false);
            double maxOverlaps = ArrayUtils.Max(overlaps);
            Debug.WriteLine("Max overlaps = " + maxOverlaps);
            Assert.IsTrue(maxOverlaps < maxThreshold);
            Assert.IsTrue(ArrayUtils.Average(overlaps) < avgThreshold);

            maxThreshold = 0.12;
            overlaps = OverlapsForUnrelatedAreas(1499, 37, 10, 100, false);
            maxOverlaps = ArrayUtils.Max(overlaps);
            Debug.WriteLine("Max overlaps = " + maxOverlaps);
            Assert.IsTrue(maxOverlaps < maxThreshold);
            Assert.IsTrue(ArrayUtils.Average(overlaps) < avgThreshold);

            maxThreshold = 0.13;
            overlaps = OverlapsForUnrelatedAreas(999, 25, 10, 100, false);
            maxOverlaps = ArrayUtils.Max(overlaps);
            Debug.WriteLine("Max overlaps = " + maxOverlaps);
            Assert.IsTrue(maxOverlaps < maxThreshold);
            Assert.IsTrue(ArrayUtils.Average(overlaps) < avgThreshold);

            maxThreshold = 0.16;
            overlaps = OverlapsForUnrelatedAreas(499, 13, 10, 100, false);
            maxOverlaps = ArrayUtils.Max(overlaps);
            Debug.WriteLine("Max overlaps = " + maxOverlaps);
            Assert.IsTrue(maxOverlaps < maxThreshold);
            Assert.IsTrue(ArrayUtils.Average(overlaps) < avgThreshold);
        }

        [TestMethod]
        public void TestEncodeAdjacentPositions()
        {
            int repetitions = 100;
            int n = 999;
            int w = 25;
            int radius = 10;
            double minThreshold = 0.75;
            double avgThreshold = 0.90;
            double[] allOverlaps = new double[repetitions];

            for (int i = 0; i < repetitions; i++)
            {
                double[] overlaps = OverlapsForRelativeAreas(
                    n, w, new[] { i * 10, i * 10 }, radius, new[] { 0, 1 }, 0, 1, false);

                allOverlaps[i] = overlaps[0];
            }

            Assert.IsTrue(ArrayUtils.Min(allOverlaps) > minThreshold);
            Assert.IsTrue(ArrayUtils.Average(allOverlaps) > avgThreshold);

            if (verbose)
            {
                Console.WriteLine(String.Format("===== Adjacent positions overlap " +
                    "(n = {0}, w = {1}, radius = {2} ===", n, w, radius));
                Console.WriteLine(String.Format("Max: {0}", ArrayUtils.Max(allOverlaps)));
                Console.WriteLine(String.Format("Min: {0}", ArrayUtils.Min(allOverlaps)));
                Console.WriteLine(String.Format("Average: {0}", ArrayUtils.Average(allOverlaps)));
            }
        }

        public void AssertDecreasingOverlaps(double[] overlaps)
        {
            Assert.AreEqual(0,
                ArrayUtils.Sum(
                    ArrayUtils.Where(
                        ArrayUtils.Diff(overlaps), ArrayUtils.GREATER_THAN_0)));
        }

        public int[] Encode(CoordinateEncoder encoder, int[] coordinate, double radius)
        {
            int[] output = new int[encoder.GetWidth()];
            encoder.EncodeIntoArray(new Tuple(coordinate, radius), output);
            return output;
        }

        public double Overlap(int[] sdr1, int[] sdr2)
        {
            Assert.AreEqual(sdr1.Length, sdr2.Length);
            int sum = ArrayUtils.Sum(ArrayUtils.And(sdr1, sdr2));
            //		Console.WriteLine("and = " + Arrays.toString(ArrayUtils.where(ArrayUtils.and(sdr1, sdr2), ArrayUtils.WHERE_1)));
            //		Console.WriteLine("sum = " + ArrayUtils.sum(ArrayUtils.and(sdr1, sdr2)));
            return (double)sum / (double)ArrayUtils.Sum(sdr1);
        }

        public double[] OverlapsForRelativeAreas(int n, int w, int[] initPosition, int initRadius,
            int[] dPosition, int dRadius, int num, bool verbose)
        {
            SetUp();
            builder.N(n);
            builder.W(w);
            InitCe();

            double[] overlaps = new double[num];

            int[] outputA = Encode(ce, initPosition, initRadius);
            int[] newPosition;
            for (int i = 0; i < num; i++)
            {
                newPosition = dPosition == null ? initPosition :
                    ArrayUtils.Add(
                        newPosition = Arrays.CopyOf(initPosition, initPosition.Length),
                            ArrayUtils.Multiply(dPosition, (i + 1)));
                int newRadius = initRadius + (i + 1) * dRadius;
                int[] outputB = Encode(ce, newPosition, newRadius);
                overlaps[i] = Overlap(outputA, outputB);
            }

            return overlaps;
        }

        public double[] OverlapsForUnrelatedAreas(int n, int w, int radius, int repetitions, bool verbose)
        {
            return OverlapsForRelativeAreas(n, w, new[] { 0, 0 }, radius,
                new[] { 0, radius * 10 }, 0, repetitions, verbose);
        }

        [TestMethod]
        public void TestTopStrict()
        {
            int[][] input = new[]
            {
                new[] {95, 195},
                new[] {95, 196},
                new[] {95, 197},
                new[] {95, 198},
                new[] {95, 199},
                new[] {95, 200},
                new[] {95, 201},
                new[] {95, 202},
                new[] {95, 203},
                new[] {95, 204},
                new[] {95, 205},
                new[] {96, 195},
                new[] {96, 196},
                new[] {96, 197},
                new[] {96, 198},
                new[] {96, 199},
                new[] {96, 200},
                new[] {96, 201},
                new[] {96, 202},
                new[] {96, 203},
                new[] {96, 204},
                new[] {96, 205},
                new[] {97, 195},
                new[] {97, 196},
                new[] {97, 197},
                new[] {97, 198},
                new[] {97, 199},
                new[] {97, 200},
                new[] {97, 201},
                new[] {97, 202},
                new[] {97, 203},
                new[] {97, 204},
                new[] {97, 205},
                new[] {98, 195},
                new[] {98, 196},
                new[] {98, 197},
                new[] {98, 198},
                new[] {98, 199},
                new[] {98, 200},
                new[] {98, 201},
                new[] {98, 202},
                new[] {98, 203},
                new[] {98, 204},
                new[] {98, 205},
                new[] {99, 195},
                new[] {99, 196},
                new[] {99, 197},
                new[] {99, 198},
                new[] {99, 199},
                new[] {99, 200},
                new[] {99, 201},
                new[] {99, 202},
                new[] {99, 203},
                new[] {99, 204},
                new[] {99, 205},
                new[] {100, 195},
                new[] {100, 196},
                new[] {100, 197},
                new[] {100, 198},
                new[] {100, 199},
                new[] {100, 200},
                new[] {100, 201},
                new[] {100, 202},
                new[] {100, 203},
                new[] {100, 204},
                new[] {100, 205},
                new[] {101, 195},
                new[] {101, 196},
                new[] {101, 197},
                new[] {101, 198},
                new[] {101, 199},
                new[] {101, 200},
                new[] {101, 201},
                new[] {101, 202},
                new[] {101, 203},
                new[] {101, 204},
                new[] {101, 205},
                new[] {102, 195},
                new[] {102, 196},
                new[] {102, 197},
                new[] {102, 198},
                new[] {102, 199},
                new[] {102, 200},
                new[] {102, 201},
                new[] {102, 202},
                new[] {102, 203},
                new[] {102, 204},
                new[] {102, 205},
                new[] {103, 195},
                new[] {103, 196},
                new[] {103, 197},
                new[] {103, 198},
                new[] {103, 199},
                new[] {103, 200},
                new[] {103, 201},
                new[] {103, 202},
                new[] {103, 203},
                new[] {103, 204},
                new[] {103, 205},
                new[] {104, 195},
                new[] {104, 196},
                new[] {104, 197},
                new[] {104, 198},
                new[] {104, 199},
                new[] {104, 200},
                new[] {104, 201},
                new[] {104, 202},
                new[] {104, 203},
                new[] {104, 204},
                new[] {104, 205},
                new[] {105, 195},
                new[] {105, 196},
                new[] {105, 197},
                new[] {105, 198},
                new[] {105, 199},
                new[] {105, 200},
                new[] {105, 201},
                new[] {105, 202},
                new[] {105, 203},
                new[] {105, 204},
                new[] {105, 205}
            };

            CoordinateEncoder.ResetRandomGenerator();

            CoordinateEncoder c = new CoordinateEncoder();
            int[][] results = c.TopWCoordinates(c, input, 3);
            int[][] expected = new[] { new[] { 99, 196 }, new[] { 100, 195 }, new[] { 97, 202 } };

            for (int i = 0; i < results.Length; i++)
            {
                Assert.IsTrue(Arrays.AreEqual(results[i], expected[i]),
                    "Failure on array " + i + " expected: " + Arrays.ToString(expected[i]) + " actual: " + Arrays.ToString(results[i]));
            }

            Console.WriteLine("done");
        }
    }
}