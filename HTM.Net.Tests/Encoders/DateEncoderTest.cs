using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Encoders
{
    [TestClass]
    public class DateEncoderTest
    {
        private DateEncoder de;
        private DateEncoder.Builder builder;
        private DateTime dt;
        private int[] expected;
        private int[] bits;

        private void SetUp()
        {
            // 3 bits for season, 1 bit for day of week, 3 for weekend, 5 for time of day
            // use of forced is not recommended, used here for readability.
            builder = (DateEncoder.Builder)DateEncoder.GetBuilder();

            de = (DateEncoder)builder.Season(3)
                .DayOfWeek(1)
                .Weekend(3)
                .TimeOfDay(5)
                .Build();

            //in the middle of fall, Thursday, not a weekend, afternoon - 4th Nov, 2010, 14:55
            dt = new DateTime(2010, 11, 4, 14, 55, 00);
            DateTime comparison = new DateTime(2010, 11, 4, 13, 55, 00);

            bits = de.Encode(dt);
            int[] comparisonBits = de.Encode(comparison);

            Console.WriteLine(Arrays.ToString(bits));
            Console.WriteLine(Arrays.ToString(comparisonBits));

            //
            //dt.GetMillis();

            // season is aaabbbcccddd (1 bit/month) # TODO should be <<3?
            // should be 000000000111 (centered on month 11 - Nov)
            int[] seasonExpected = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1 };

            // week is MTWTFSS
            // contrary to local time documentation, Monday = 0 (for python
            //  datetime.datetime.timetuple()
            int[] dayOfWeekExpected = { 0, 0, 0, 1, 0, 0, 0 };

            // not a weekend, so it should be "False"
            int[] weekendExpected = { 1, 1, 1, 0, 0, 0 };

            // time of day has radius of 4 hours and w of 5 so each bit = 240/5 min = 48min
            // 14:55 is minute 14*60 + 55 = 895; 895/48 = bit 18.6
            // should be 30 bits total (30 * 48 minutes = 24 hours)
            int[] timeOfDayExpected = { 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0 };

            expected = ArrayUtils.ConcatAll(seasonExpected, dayOfWeekExpected, weekendExpected, timeOfDayExpected);
        }

        private void InitDe()
        {
            de = (DateEncoder)builder.Build();
        }

        /**
         * Creating date encoder instance
         */
        [TestMethod]
        public void TestDateEncoder()
        {
            SetUp();
            InitDe();

            List<Tuple> descs = de.GetDescription();
            Assert.IsNotNull(descs);
            // should be [("season", 0), ("day of week", 12), ("weekend", 19), ("time of day", 25)]

            List<Tuple> expectedDescs = new List<Tuple> {
                    new Tuple("season", 0),
                    new Tuple("day of week", 12),
                    new Tuple("weekend", 19),
                    new Tuple("time of day", 25)
            };

            Assert.AreEqual(expectedDescs.Count, descs.Count);

            for (int i = 0; i < expectedDescs.Count; ++i)
            {
                Tuple desc = descs[i];
                Assert.IsNotNull(desc);
                Assert.AreEqual(expectedDescs[i], desc);
            }

            Assert.IsTrue(expected.SequenceEqual(bits));

            Console.WriteLine();
            de.PPrintHeader("");
            de.PPrint(bits, "");
            Console.WriteLine();
        }

        //[TestMethod]
        public void TestScalarPeriodicDates()
        {
            builder = (DateEncoder.Builder)DateEncoder.GetBuilder();
            de = (DateEncoder)builder
                .DayOfWeek(11,24)
                .Weekend(0,1)
                .TimeOfDay(11,9.5)
                .Build();

            dt = new DateTime(2010, 7, 3, 6, 00, 00);
            int[] tempArray = new int[de.GetWidth()];
            de.EncodeIntoArray(dt, tempArray);
        }

        [TestMethod]
        public void TestScalarDayOfWeek()
        {
            builder = (DateEncoder.Builder)DateEncoder.GetBuilder();
            de = (DateEncoder)builder
                .DayOfWeek(11, 24)
                .Weekend(0, 1)
                .TimeOfDay(11, 9.5)
                .Build();

            dt = new DateTime(2016, 2, 15, 00, 00, 00); // monday, midnight
            for (int i = 0; i < 7; i++)
            {
                var scalars = de.GetScalars(dt.AddDays(i));
                var dayOfWeek = scalars.First(); // first one is day of week
                Assert.AreEqual(i, dayOfWeek);
            }
            for (int i = 0; i < 24; i++)
            {
                var timeScalar = de.GetScalars(dt.AddHours(i)).Last();
                Assert.AreEqual(i, timeScalar);
            }
        }

        [TestMethod]
        public void TestDateEncoderRanges()
        {
            // 3 bits for season, 1 bit for day of week, 3 for weekend, 5 for time of day
            // = 12 bits total
            // use of forced is not recommended, used here for readability.
            builder = (DateEncoder.Builder)DateEncoder.GetBuilder();

            de = (DateEncoder)builder
                .Season(3)
                .DayOfWeek(1)
                .Weekend(3)
                .TimeOfDay(5, 1)
                .Build();

            List<string> bitlist = new List<string>();

            Calendar c = new GregorianCalendar();
            int year = 2010;
            //for (int year = 1999; year < DateTime.Now.Year + 1; year++)
            {
                dt = new DateTime(year, 01, 01, 0, 0, 0);
                for (int day = 0; day < 7 /*c.GetDaysInYear(year)*/; day++)
                {
                    for (int hour = 0; hour < 24; hour++)
                    {
                        //for (int minute = 0; minute < 60; minute += 15)
                        {
                            DateTime dt2 = dt.AddDays(day).AddHours(hour);//.AddMinutes(minute);
                            try
                            {
                                bits = de.Encode(dt2);
                            }
                            catch (Exception)
                            {
                                Debug.WriteLine($"Fucked up on date: {dt2}");
                                Assert.Fail();
                            }

                            Assert.IsNotNull(bits);
                            //Assert.AreEqual(63, bits.Length);
                            bitlist.Add(Arrays.ToString(bits));
                        }
                    }
                }
            }

            //bitlist.ForEach(Console.WriteLine);

            // All bits must be different!
            int count = bitlist.Count;
            int distinctCount = bitlist.Distinct().Count();
            Assert.AreEqual(count, distinctCount, $"{count / (double)distinctCount}");
        }

        // TODO Current implementation of DateEncoder throws at invalid Date,
        // but testMissingValues in Python expects it to encode it as all-zero bits:
        //    def testMissingValues(self):
        //            '''missing values'''
        //    mvOutput = self._e.encode(SENTINEL_VALUE_FOR_MISSING_DATA)
        //            self.assertEqual(sum(mvOutput), 0)

        /**
         * Decoding date
         */
        [TestMethod]
        public void TestDecoding()
        {
            SetUp();
            InitDe();

            //TODO Why null is needed?
            Tuple decoded = de.Decode(bits, null);

            Console.WriteLine(decoded.ToString());
            Console.WriteLine($"decodedToStr => {de.DecodedToStr(decoded)}");

            Map<string, RangeList> fieldsMap = (Map<string, RangeList>)decoded.Get(0);
            List<string> fieldsOrder = (List<string>)decoded.Get(1);

            Assert.IsNotNull(fieldsMap);
            Assert.IsNotNull(fieldsOrder);
            Assert.AreEqual(4, fieldsMap.Count);

            Map<string, double> expectedMap = new Map<string, double>();
            expectedMap.Add("season", 305.0);
            expectedMap.Add("time of day", 14.4);
            expectedMap.Add("day of week", 3.0);
            expectedMap.Add("weekend", 0.0);

            foreach (string key in expectedMap.Keys)
            {
                double expected = expectedMap[key];
                RangeList actual = fieldsMap[key];
                Assert.AreEqual(1, actual.Count);
                MinMax minmax = actual.GetRange(0);
                Assert.AreEqual(expected, minmax.Min(), de.GetResolution());
                Assert.AreEqual(expected, minmax.Max(), de.GetResolution());
            }

            Console.WriteLine(decoded.ToString());
            Console.WriteLine($"decodedToStr => {de.DecodedToStr(decoded)}");
        }

        /**
         * Check topDownCompute
         */
        [TestMethod]
        public void TestTopDownCompute()
        {
            SetUp();
            InitDe();

            List<EncoderResult> topDown = de.TopDownCompute(bits);

            List<double> expectedList = new List<double> { 320.25, 3.5, .167, 14.8 };

            for (int i = 0; i < topDown.Count; i++)
            {
                EncoderResult r = topDown[i];
                double actual = (double)r.GetValue();
                double expected = expectedList[i];
                Assert.AreEqual(expected, actual, 4.0);
            }
        }

        /**
         * Check bucket index support
         */
        [TestMethod]
        public void TestBucketIndexSupport()
        {
            SetUp();
            InitDe();

            int[] bucketIndices = de.GetBucketIndices(dt);
            Console.WriteLine($"bucket indices: {Arrays.ToString(bucketIndices)}");
            List<EncoderResult> bucketInfo = de.GetBucketInfo(bucketIndices);

            List<double> expectedList = new List<double> { 320.25, 3.5, .167, 14.8 };

            List<int> encodings = new List<int>();

            for (int i = 0; i < bucketInfo.Count; i++)
            {
                EncoderResult r = bucketInfo[i];
                double actual = (double)r.GetValue();
                double expected1 = expectedList[i];
                Assert.AreEqual(expected1, actual, 4.0);

                encodings.AddRange(r.GetEncoding());
            }

            Assert.IsTrue(expected.SequenceEqual(encodings.ToArray()));
        }

        /**
         * look at holiday more carefully because of the smooth transition
         */
        [TestMethod]
        public void TestHoliday()
        {
            //use of forced is not recommended, used here for readability, see ScalarEncoder
            DateEncoder e = (DateEncoder)((DateEncoder.Builder)DateEncoder.GetBuilder()).Holiday(5).Forced(true).Build();
            int[] holiday = new int[] { 0, 0, 0, 0, 0, 1, 1, 1, 1, 1 };
            int[] notholiday = new int[] { 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 };
            int[] holiday2 = new int[] { 0, 0, 0, 1, 1, 1, 1, 1, 0, 0 };

            DateTime d = new DateTime(2010, 12, 25, 4, 55, 00);
            //Console.WriteLine(String.format("1:%s", Arrays.toString(e.encode(d))));
            Assert.IsTrue(holiday.SequenceEqual(e.Encode(d)));

            d = new DateTime(2008, 12, 27, 4, 55, 0);
            //Console.WriteLine(String.format("2:%s", Arrays.toString(e.encode(d))));
            Assert.IsTrue(notholiday.SequenceEqual(e.Encode(d)));

            d = new DateTime(1999, 12, 26, 8, 0, 0);
            //Console.WriteLine(String.Format("{0}", Arrays.ToString(e.Encode(d))));
            Assert.IsTrue(holiday2.SequenceEqual(e.Encode(d)));

            d = new DateTime(2011, 12, 24, 16, 0, 0);
            //Console.WriteLine(String.format("4:%s", Arrays.toString(e.encode(d))));
            Assert.IsTrue(holiday2.SequenceEqual(e.Encode(d)));
        }

        /**
         * Test weekend encoder
         */

        [TestMethod]
        public void TestWeekend()
        {
            // use of forced is not recommended, used here for readability, see ScalarEncoder
            DateEncoder e = (DateEncoder)((DateEncoder.Builder)DateEncoder.GetBuilder()).CustomDays(21, new List<DayOfWeek>
                {
                    DayOfWeek.Saturday,
                    DayOfWeek.Sunday,
                    DayOfWeek.Friday
                }).Forced(true).Build();
            DateEncoder mon = (DateEncoder)((DateEncoder.Builder)DateEncoder.GetBuilder()).CustomDays(21, new List<DayOfWeek>
                    {
                        DayOfWeek.Monday
                    })
                .Forced(true).Build();
            DateEncoder e2 = (DateEncoder)((DateEncoder.Builder)DateEncoder.GetBuilder()).Weekend(21, 1).Forced(true).Build();

            //DateTime d = new DateTime(1988,5,29,20,0);
            DateTime d = new DateTime(1988, 5, 29, 20, 0, 0);

            Console.WriteLine("DateEncoderTest.testWeekend(): e.encode(d)  = " + Arrays.ToString(e.Encode(d)));
            Console.WriteLine("DateEncoderTest.testWeekend(): e2.encode(d) = " + Arrays.ToString(e2.Encode(d)));
            Assert.IsTrue(e.Encode(d).SequenceEqual(e2.Encode(d)));

            for (int i = 0; i < 300; i++)
            {
                DateTime curDate = d.AddDays(i + 1);
                Assert.IsTrue(e.Encode(curDate).SequenceEqual(e2.Encode(curDate)));

                //Make sure
                Tuple decoded = mon.Decode(mon.Encode(curDate), null);

                Map<string, RangeList> fieldsMap = (Map<string, RangeList>)decoded.Get(0);
                List<string> fieldsOrder = (List<string>)decoded.Get(1);

                Assert.IsNotNull(fieldsMap);
                Assert.IsNotNull(fieldsOrder);
                Assert.AreEqual(1, fieldsMap.Count);

                RangeList range = fieldsMap["Monday"];
                Assert.AreEqual(1, range.Count);
                Assert.AreEqual(1, ((List<MinMax>)range.Get(0)).Count);
                MinMax minmax = range.GetRange(0);
                Console.WriteLine("DateEncoderTest.testWeekend(): minmax.min() = {0} -> {1}", minmax.Min(), curDate.DayOfWeek);

                if (minmax.Min() == 1.0)
                {
                    Assert.AreEqual(1, (int)curDate.DayOfWeek);
                }
                else {
                    Assert.AreNotEqual(1, (int)curDate.DayOfWeek);
                }
            }
        }
    }
}