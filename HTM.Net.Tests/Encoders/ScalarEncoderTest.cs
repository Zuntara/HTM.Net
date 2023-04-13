using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Encoders
{
    [TestClass]
    public class ScalarEncoderTest
    {
        private ScalarEncoder se;
        private ScalarEncoder.Builder builder;

        private void SetUp()
        {
            builder = (ScalarEncoder.Builder)ScalarEncoder.GetBuilder()
                .N(14)
                .W(3)
                .Radius(0.0)
                .MinVal(1.0)
                .MaxVal(8.0)
                .Periodic(true)
                .Forced(true);
        }

        private void InitSe()
        {
            se = (ScalarEncoder)builder.Build();
        }

        [TestMethod]
        public void TestScalarEncoder()
        {
            SetUp();
            InitSe();

            int[] empty = se.Encode(Encoder<double>.SENTINEL_VALUE_FOR_MISSING_DATA);
            Console.WriteLine("\nEncoded missing data as: " + Arrays.ToString(empty));
            int[] expected = new int[14];
            Assert.IsTrue(Arrays.AreEqual(expected, empty));
        }

        [TestMethod]
        public void TestBottomUpEncodingPeriodicEncoder()
        {
            SetUp();
            InitSe();

            Assert.AreEqual("[1:8]", se.GetDescription()[0].Get(0));

            SetUp();
            builder.Name("scalar");
            InitSe();

            Assert.AreEqual("scalar", se.GetDescription()[0].Get(0));
            int[] res = se.Encode(3.0);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(3.1);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(3.5);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(3.6);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(3.7);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(4d);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(1d);
            Assert.IsTrue(Arrays.AreEqual(new[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 }, res));

            res = se.Encode(1.5);
            Assert.IsTrue(Arrays.AreEqual(new[] { 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 }, res));

            res = se.Encode(7d);
            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 }, res));

            res = se.Encode(7.5);
            Assert.IsTrue(Arrays.AreEqual(new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 }, res));

            Assert.AreEqual(0.5d, se.GetResolution(), 0);
            Assert.AreEqual(1.5d, se.GetRadius(), 0);
        }

        /**
         * Test that we get the same encoder when we construct it using resolution
         * instead of n
         */
        [TestMethod]
        public void TestCreateResolution()
        {
            SetUp();
            InitSe();
            List<Tuple> dict = se.Dict();

            SetUp();
            builder.Resolution(0.5);
            InitSe();
            List<Tuple> compare = se.Dict();
            Assert.AreEqual(dict.ToString(), compare.ToString());

            SetUp();
            builder.Radius(1.5);
            InitSe();
            compare = se.Dict();
            Assert.AreEqual(dict.ToString(), compare.ToString());

            //Negative test
            SetUp();
            builder.Resolution(0.5);
            InitSe();
            se.SetName("break this");
            compare = se.Dict();
            Assert.IsFalse(dict.SequenceEqual(compare));
        }

        /**
         * Test the input description generation, top-down compute, and bucket
         * support on a periodic encoder
         */
        [TestMethod]
        public void TestDecodeAndResolution()
        {
            SetUp();
            builder.Name("scalar");
            InitSe();
            double resolution = se.GetResolution();
            StringBuilder @out = new StringBuilder();
            for (double v = se.GetMinVal(); v < se.GetMaxVal(); v += (resolution / 4.0d))
            {
                int[] output = se.Encode(v);
                DecodeResult decodedInFor = (DecodeResult)se.Decode(output, "");

                Console.WriteLine(@out.Append("decoding ").Append(Arrays.ToString(output)).Append(" (").
                Append(string.Format("{0}", v)).Append(")=> ").Append(se.DecodedToStr(decodedInFor)));
                @out.Length = 0;

                Map<string, RangeList> fieldsMapInFor = decodedInFor.GetFields();
                Assert.AreEqual(1, fieldsMapInFor.Count);
                RangeList ranges = new List<RangeList>(fieldsMapInFor.Values)[0];
                Assert.AreEqual(1, ranges.Count);
                Assert.AreEqual(ranges.GetRange(0).Min(), ranges.GetRange(0).Max(), 0);
                Assert.IsTrue(ranges.GetRange(0).Min() - v < se.GetResolution());

                EncoderResult topDown = se.TopDownCompute(output)[0];
                Console.WriteLine("topdown => " + topDown);
                Assert.IsTrue(Arrays.AreEqual(topDown.GetEncoding(), output));
                Assert.IsTrue(Math.Abs(((double)topDown.Get(1)) - v) <= se.GetResolution() / 2);

                //Test bucket support
                int[] bucketIndices = se.GetBucketIndices(v);
                Console.WriteLine("bucket index => " + bucketIndices[0]);
                topDown = se.GetBucketInfo(bucketIndices)[0];
                Assert.IsTrue(Math.Abs(((double)topDown.Get(1)) - v) <= se.GetResolution() / 2);
                Assert.AreEqual(topDown.Get(1), se.GetBucketValues<double>(typeof(double)).ToArray()[bucketIndices[0]]);

                Assert.AreEqual(topDown.Get(2), topDown.Get(1));

                Assert.IsTrue(Arrays.AreEqual(topDown.GetEncoding(), output));
            }

            // -----------------------------------------------------------------------
            // Test the input description generation on a large number, periodic encoder
            SetUp();
            builder.Name("scalar")
                    .W(3)
                    .Radius(1.5)
                    .MinVal(1.0)
                    .MaxVal(8.0)
                    .Periodic(true)
                    .Forced(true);

            InitSe();

            Console.WriteLine("\nTesting periodic encoder decoding, resolution of " + se.GetResolution());

            //Test with a "hole"
            int[] encoded = new[] { 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 };
            DecodeResult decoded = (DecodeResult)se.Decode(encoded, "");
            Map<string, RangeList> fieldsMap = decoded.GetFields();

            Assert.AreEqual(1, fieldsMap.Count);
            Assert.AreEqual(1, decoded.GetRanges("scalar").Count);
            Assert.AreEqual("7.5, 7.5", decoded.GetRanges("scalar").GetRange(0).ToString());

            //Test with something wider than w, and with a hole, and wrapped
            encoded = new[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0 };
            decoded = (DecodeResult)se.Decode(encoded, "");
            fieldsMap = decoded.GetFields();

            Assert.AreEqual(1, fieldsMap.Count);
            Assert.AreEqual(2, decoded.GetRanges("scalar").Count);
            Assert.AreEqual("7.5, 8.0", decoded.GetRanges("scalar").GetRange(0).ToString());

            //Test with something wider than w, no hole
            encoded = new[] { 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            decoded = (DecodeResult)se.Decode(encoded, "");
            fieldsMap = decoded.GetFields();

            Assert.AreEqual(1, fieldsMap.Count);
            Assert.AreEqual(1, decoded.GetRanges("scalar").Count);
            Assert.AreEqual(decoded.GetRanges("scalar").GetRange(0).ToString(), "1.5, 2.5");

            //Test with 2 ranges
            encoded = new[] { 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0 };
            decoded = (DecodeResult)se.Decode(encoded, "");
            fieldsMap = decoded.GetFields();

            Assert.AreEqual(1, fieldsMap.Count);
            Assert.AreEqual(2, decoded.GetRanges("scalar").Count);
            Assert.AreEqual(decoded.GetRanges("scalar").GetRange(0).ToString(), "1.5, 1.5");
            Assert.AreEqual(decoded.GetRanges("scalar").GetRange(1).ToString(), "5.5, 6.0");

            //Test with 2 ranges, 1 of which is narrower than w
            encoded = new[] { 0, 1, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 0, 0 };
            decoded = (DecodeResult)se.Decode(encoded, "");
            fieldsMap = decoded.GetFields();

            Assert.AreEqual(1, fieldsMap.Count);
            Assert.AreEqual(2, decoded.GetRanges("scalar").Count);
            Assert.AreEqual(decoded.GetRanges("scalar").GetRange(0).ToString(), "1.5, 1.5");
            Assert.AreEqual(decoded.GetRanges("scalar").GetRange(1).ToString(), "5.5, 6.0");
        }

        /**
         * Test closenessScores for a periodic encoder
         */
        [TestMethod]
        public void TestCloseness()
        {
            SetUp();
            builder.Name("day of week")
                .W(7)
                .Radius(1.0)
                .MinVal(0.0)
                .MaxVal(7.0)
                .Periodic(true)
                .Forced(true);
            InitSe();

            List<double> expValues = new List<double>(new double[] { 2, 4, 7 });
            List<double> actValues = new List<double>(new double[] { 4, 2, 1 });

            List<double> scores = se.ClosenessScores(expValues, actValues, false);
            foreach (Tuple t in ArrayUtils.Zip(Arrays.AsList(2, 2, 1).ToArray(), Arrays.AsList((int)scores[0]).ToArray()))
            {
                double a = (int)t.Get(0);
                double b = (double)Convert.ChangeType(t.Get(1), typeof(double));
                Assert.AreEqual(a, b);
            }
        }

        [TestMethod]
        public void TestNonPeriodicBottomUp()
        {
            SetUp();
            builder.Name("day of week")
                .W(5)
                .N(14)
                .Radius(1.0)
                .MinVal(1.0)
                .MaxVal(10.0)
                .Periodic(false)
                .Forced(true);
            InitSe();

            Console.WriteLine(string.Format("Testing non-periodic encoder encoding resolution of {0}", se.GetResolution()));

            Assert.IsTrue(Arrays.AreEqual(se.Encode(1d), new[] { 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.IsTrue(Arrays.AreEqual(se.Encode(2d), new[] { 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0 }));
            Assert.IsTrue(Arrays.AreEqual(se.Encode(10d), new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 }));

            // Test that we get the same encoder when we construct it using resolution
            // instead of n
            SetUp();
            builder.Name("day of week")
                .W(5)
                .Radius(5.0)
                .MinVal(1.0)
                .MaxVal(10.0)
                .Periodic(false)
                .Forced(true);
            InitSe();

            double v = se.GetMinVal();
            while (v < se.GetMaxVal())
            {
                int[] output = se.Encode(v);
                DecodeResult decodedInWhile = (DecodeResult)se.Decode(output, "");
                Console.WriteLine("decoding " + Arrays.ToString(output) + string.Format("({0})=>", v) + se.DecodedToStr(decodedInWhile));

                Assert.AreEqual(decodedInWhile.GetFields().Count, 1, 0);
                List<RangeList> rangeListInWhile = new List<RangeList>(decodedInWhile.GetFields().Values);
                Assert.AreEqual(rangeListInWhile[0].Count, 1, 0);
                MinMax minMax = rangeListInWhile[0].GetRanges()[0];
                Assert.AreEqual(minMax.Min(), minMax.Max(), 0);
                Assert.IsTrue(Math.Abs(minMax.Min() - v) <= se.GetResolution());

                List<EncoderResult> topDowns = se.TopDownCompute(output);
                EncoderResult topDown = topDowns[0];
                Console.WriteLine("topDown => " + topDown);
                Assert.IsTrue(Arrays.AreEqual(topDown.GetEncoding(), output));
                Assert.IsTrue(Math.Abs(((double)topDown.GetValue()) - v) <= se.GetResolution());

                //Test bucket support
                int[] bucketIndices = se.GetBucketIndices(v);
                Console.WriteLine("bucket index => " + bucketIndices[0]);
                topDown = se.GetBucketInfo(bucketIndices)[0];
                Assert.IsTrue(Math.Abs(((double)topDown.GetValue()) - v) <= se.GetResolution() / 2);
                Assert.AreEqual(topDown.GetScalar(), (int)Convert.ChangeType(topDown.GetValue(), typeof(int)));
                Assert.IsTrue(Arrays.AreEqual(topDown.GetEncoding(), output));

                // Next value
                v += se.GetResolution() / 4;
            }

            // Make sure we can fill in holes
            DecodeResult decoded = (DecodeResult)se.Decode(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 1, 1 }, "");
            Assert.AreEqual(decoded.GetFields().Count, 1, 0);
            List<RangeList> rangeList = new List<RangeList>(decoded.GetFields().Values);
            Assert.AreEqual(1, rangeList[0].Count, 0);
            Console.WriteLine("decodedToStr of " + rangeList + " => " + se.DecodedToStr(decoded));

            decoded = (DecodeResult)se.Decode(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 1 }, "");
            Assert.AreEqual(decoded.GetFields().Count, 1, 0);
            rangeList = new List<RangeList>(decoded.GetFields().Values);
            Assert.AreEqual(1, rangeList[0].Count, 0);
            Console.WriteLine("decodedToStr of " + rangeList + " => " + se.DecodedToStr(decoded));

            // Test min and max
            SetUp();
            builder.Name("scalar")
                .W(3)
                .MinVal(1.0)
                .MaxVal(10.0)
                .Periodic(false)
                .Forced(true);
            InitSe();

            List<EncoderResult> decode = se.TopDownCompute(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1 });
            Assert.AreEqual(10, (double)decode[0].GetScalar(), 0);
            decode = se.TopDownCompute(new[] { 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 });
            Assert.AreEqual(1, (double)decode[0].GetScalar(), 0);

            // Make sure only the last and first encoding encodes to max and min, and there is no value greater than max or min
            SetUp();
            builder.Name("scalar")
                .W(3)
                .N(140)
                .Radius(1.0)
                .MinVal(1.0)
                .MaxVal(141.0)
                .Periodic(false)
                .Forced(true);
            InitSe();

            List<int[]> iterlist = new List<int[]>();
            for (int i = 0; i < 137; i++)
            {
                iterlist.Add(new int[140]);
                ArrayUtils.SetRangeTo(iterlist[i], i, i + 3, 1);
                decode = se.TopDownCompute(iterlist[i]);
                int value = (int)decode[0].GetScalar();
                Assert.IsTrue(value <= 141);
                Assert.IsTrue(value >= 1);
                Assert.IsTrue(value < 141 || i == 137);
                Assert.IsTrue(value > 1 || i == 0);
            }

            // -------------------------------------------------------------------------
            // Test the input description generation and top-down compute on a small number
            //   non-periodic encoder
            SetUp();
            builder.Name("scalar")
                .W(3)
                .N(15)
                .MinVal(.001)
                .MaxVal(.002)
                .Periodic(false)
                .Forced(true);
            InitSe();

            Console.WriteLine(string.Format("\nTesting non-periodic encoder decoding resolution of {0}...", se.GetResolution()));
            v = se.GetMinVal();
            while (v < se.GetMaxVal())
            {
                int[] output = se.Encode(v);
                decoded = (DecodeResult)se.Decode(output, "");
                Console.WriteLine(string.Format("decoding ({0})=>", v) + " " + se.DecodedToStr(decoded));

                Assert.AreEqual(decoded.GetFields().Count, 1, 0);
                rangeList = new List<RangeList>(decoded.GetFields().Values);
                Assert.AreEqual(rangeList[0].Count, 1, 0);
                MinMax minMax = rangeList[0].GetRanges()[0];
                Assert.AreEqual(minMax.Min(), minMax.Max(), 0);
                Assert.IsTrue(Math.Abs(minMax.Min() - v) <= se.GetResolution());

                decode = se.TopDownCompute(output);
                Console.WriteLine("topdown => " + Arrays.ToString(decode.ToArray()));
                Console.WriteLine("{0} <= {1}", Math.Abs(decode[0].GetScalar() - v), se.GetResolution() / 2);
                Assert.IsTrue(Math.Abs(decode[0].GetScalar() - v) <= se.GetResolution() / 2);

                v += (se.GetResolution() / 4);
            }

            // -------------------------------------------------------------------------
            // Test the input description generation on a large number, non-periodic encoder
            SetUp();
            builder.Name("scalar")
                .W(3)
                .N(15)
                .MinVal(1.0)
                .MaxVal(1000000000.0)
                .Periodic(false)
                .Forced(true);
            InitSe();

            Console.WriteLine(string.Format("\nTesting non-periodic encoder decoding resolution of {0}...", se.GetResolution()));
            v = se.GetMinVal();
            while (v < se.GetMaxVal())
            {
                int[] output = se.Encode(v);
                decoded = (DecodeResult)se.Decode(output, "");
                Console.WriteLine(string.Format("decoding ({0})=>", v) + " " + se.DecodedToStr(decoded));

                Assert.AreEqual(decoded.GetFields().Count, 1, 0);
                rangeList = new List<RangeList>(decoded.GetFields().Values);
                Assert.AreEqual(rangeList[0].Count, 1, 0);
                MinMax minMax = rangeList[0].GetRanges()[0];
                Assert.AreEqual(minMax.Min(), minMax.Max(), 0);
                Assert.IsTrue(Math.Abs(minMax.Min() - v) <= se.GetResolution());

                decode = se.TopDownCompute(output);
                Console.WriteLine("topdown => " + decode);
                Assert.IsTrue(Math.Abs(decode[0].GetScalar() - v) <= se.GetResolution() / 2);

                v += (se.GetResolution() / 4);
            }
        }

        /**
         * This should not cause an OutOfMemoryError due to no resolution being set.
         * Fix for #142  (see: https://github.com/numenta/htm.java/issues/142)
         */
        [TestMethod]
        public void EndlessLoopInTopDownCompute()
        {
            ScalarEncoder encoder = (ScalarEncoder)ScalarEncoder.GetBuilder()
                .W(5)
                .N(10)
                .Forced(true)
                .MinVal(0)
                .MaxVal(100)
                .Build();
            encoder.TopDownCompute(new[] { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1 });
        }

        [TestMethod]
        public void ScalarSpaceEncoderTest()
        {
            IEncoder encoder = ScalarSpaceEncoder.GetSpaceBuilder()
                .Space(ScalarSpaceEncoder.SpaceEnum.Delta)
                .W(1).MinVal(1).MaxVal(2).Periodic(false)
                .N(2).Radius(1).Resolution(1)
                .Name("Test").ClipInput(false)
                .Forced(true)
                .Build();
            Assert.IsInstanceOfType<DeltaEncoder>(encoder);

            encoder = ScalarSpaceEncoder.GetSpaceBuilder()
                .Space(ScalarSpaceEncoder.SpaceEnum.Absolute)
                .W(1).MinVal(1).MaxVal(2).Periodic(false)
                .N(2).Radius(1).Resolution(1)
                .Name("Test").ClipInput(false)
                .Forced(true)
                .Build();
            Assert.IsInstanceOfType<AdaptiveScalarEncoder>(encoder);
        }
    }
}