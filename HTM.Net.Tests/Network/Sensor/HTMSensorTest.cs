using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using HTM.Net.Datagen;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Network.Sensor
{
    /**
     * Higher level test than the individual sensor tests. These
     * tests ensure the complete functionality of sensors as a whole.
     * 
     * @author David Ray
     *
     */
     [TestClass]
    public class HtmSensorTest
    {
        private Map<string, Map<string, object>> SetupMap(
            Map<string, Map<string, object>> map,
                int n, int w, double min, double max, double radius, double resolution, bool? periodic,
                    bool? clip, bool? forced, string fieldName, string fieldType, string encoderType)
        {

            if (map == null)
            {
                map = new Map<string, Map<string, object>>();
            }
            Map<string, object> inner = null;
            if ( !map.TryGetValue(fieldName, out inner))
            {
                map.Add(fieldName, inner = new Map<string, object>());
            }

            inner.Add("n", n);
            inner.Add("w", w);
            inner.Add("minVal", min);
            inner.Add("maxVal", max);
            inner.Add("radius", radius);
            inner.Add("resolution", resolution);

            if (periodic != null) inner.Add("periodic", periodic);
            if (clip != null) inner.Add("clip", clip);
            if (forced != null) inner.Add("forced", forced);
            if (fieldName != null)
            {
                inner.Add("fieldName", fieldName);
                inner.Add("name", fieldName);
            }
            if (fieldType != null) inner.Add("fieldType", fieldType);
            if (encoderType != null) inner.Add("encoderType", encoderType);

            return map;
        }

        private Parameters GetArrayTestParams()
        {
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                            null,
                            884, // n
                            0, // w
                            0, 0, 0, 0, null, null, null,
                            "sdr_in", "darr", "SDRPassThroughEncoder");
            Parameters p = Parameters.Empty();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            return p;
        }

        private Parameters GetTestEncoderParams()
        {
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            fieldEncodings = SetupMap(
                fieldEncodings,
                25,
                3,
                0, 0, 0, 0.1, null, null, null,
                "consumption", "float", "RandomDistributedScalarEncoder");

            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_DOFW.GetFieldName(), new Tuple(1, 1.0)); // Day of week
            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_TOFD.GetFieldName(), new Tuple(5, 4.0)); // Time of day
            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_PATTERN.GetFieldName(), "MM/dd/YY HH:mm");

            // This will work also
            //fieldEncodings.Get("timestamp").Add(KEY.DATEFIELD_FORMATTER.GetFieldName(), DateEncoder.FULL_DATE);

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        private Parameters GetCategoryEncoderParams()
        {
            Map<string, Map<string, object>> fieldEncodings = SetupMap(
                null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            fieldEncodings = SetupMap(
                fieldEncodings,
                25,
                3,
                0, 0, 0, 0.1, null, null, null,
                "consumption", "float", "RandomDistributedScalarEncoder");
            fieldEncodings = SetupMap(
                fieldEncodings,
                25,
                3,
                0, 0, 0, 0.0, null, null, true,
                "type", "list", "SDRCategoryEncoder");

            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_DOFW.GetFieldName(), new Tuple(1, 1.0)); // Day of week
            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_TOFD.GetFieldName(), new Tuple(5, 4.0)); // Time of day
            fieldEncodings["timestamp"].Add(Parameters.KEY.DATEFIELD_PATTERN.GetFieldName(), "MM/dd/YY HH:mm");

            string categories = "ES;S1;S2;S3;S4;S5;S6;S7;S8;S9;S10;S11;S12;S13;S14;S15;S16;S17;S18;S19;GB;US";
            fieldEncodings["type"].Add(Parameters.KEY.CATEGORY_LIST.GetFieldName(), categories);

            // This will work also
            //fieldEncodings.Get("timestamp").Add(KEY.DATEFIELD_FORMATTER.GetFieldName(), DateEncoder.FULL_DATE);

            Parameters p = Parameters.GetEncoderDefaultParameters();
            p.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);

            return p;
        }

        [TestMethod]
        public void TestPadTo()
        {
            List<string[]> l = new List<string[]>();
            l.Add(new string[] { "0", "My" });
            l.Add(new string[] { "3", "list" });
            l.Add(new string[] { "4", "can " });
            l.Add(new string[] { "1", "really" });
            l.Add(new string[] { "6", "frustrate." });
            l.Add(new string[] { "2", "unordered" });
            l.Add(new string[] { "5", "also" });

            List<string> @out = new List<string>();
            foreach (string[] sa in l)
            {
                int idx = int.Parse(sa[0]);
                @out[HTMSensor<string>.PadTo(idx, @out)] = sa[1];
            }

            Assert.AreEqual("[My, really, unordered, list, can , also, frustrate.]", Arrays.ToString(@out.ToArray()));
        }

        /**
         * Tests that the creation mechanism detects insufficient state
         * for creating {@link Sensor}s.
         */
        [TestMethod]
        public void TestHandlesImproperInstantiation()
        {
            try
            {
                Sensor<object>.Create(null, null);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Factory cannot be null", e.Message);
            }

            try
            {
                Sensor<FileInfo>.Create(FileSensor.Create, null);
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Properties (i.e. \"SensorParams\") cannot be null", e.Message);
            }
        }

        /**
         * Tests the formation of meta constructs (i.E. may be header or other) which
         * describe the format of columnated data and processing hints (how and when to reset).
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestMetaFormation()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv")));

            // Cast the ValueList to the more complex type (Header)
            Header meta = (Header)sensor.GetMetaInfo();

            Assert.IsTrue(meta.GetFieldTypes().TrueForAll(
                l => l.Equals(FieldMetaType.DateTime) || l.Equals(FieldMetaType.Float)));
            Assert.IsTrue(meta.GetFieldNames().TrueForAll(
                l => l.Equals("timestamp") || l.Equals("consumption")));
            Assert.IsTrue(meta.GetFlags().TrueForAll(
                l => l.Equals(SensorFlags.T) || l.Equals(SensorFlags.B)));
        }

        /**
         * Tests the formation of meta constructs using test data with no flags (empty line).
         * This tests that the parsing can proceed and the there is a registered flag
         * of {@link SensorFlags#B} inserted for an empty 3rd line of a row header.
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small-noheaderflags.Csv")]
        public void testMetaFormation_NO_HEADER_FLAGS()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small-noheaderflags.Csv")));

            // Cast the ValueList to the more complex type (Header)
            Header meta = (Header)sensor.GetMetaInfo();
            Assert.IsTrue(meta.GetFieldTypes().TrueForAll(
                l => l.Equals(FieldMetaType.DateTime) || l.Equals(FieldMetaType.Float)));
            Assert.IsTrue(meta.GetFieldNames().TrueForAll(
                l => l.Equals("timestamp") || l.Equals("consumption")));
            Assert.IsTrue(meta.GetFlags().TrueForAll(
                l => l.Equals(SensorFlags.B)));
        }

        /**
         * Special test case for extra processing for category encoder lists
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4period-cat.Csv")]
        public void TestCategoryEncoderCreation()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create, SensorParams.Create(
                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4period-cat.Csv")));


            // Cast the ValueList to the more complex type (Header)
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            Header meta = (Header)htmSensor.GetMetaInfo();
            Assert.IsTrue(meta.GetFieldTypes().TrueForAll(
                l => l.Equals(FieldMetaType.DateTime) || l.Equals(FieldMetaType.Float) || l.Equals(FieldMetaType.List)));
            Assert.IsTrue(meta.GetFieldNames().TrueForAll(
                l => l.Equals("timestamp") || l.Equals("consumption") || l.Equals("type")));
            Assert.IsTrue(meta.GetFlags().TrueForAll(
                l => l.Equals(SensorFlags.T) || l.Equals(SensorFlags.B) || l.Equals(SensorFlags.C)));

            // Set the parameters on the sensor.
            // This enables it to auto-configure itself; a step which will
            // be done at the Region level.
            Encoder<object> multiEncoder = htmSensor.GetEncoder();
            Assert.IsNotNull(multiEncoder);
            Assert.IsTrue(multiEncoder is MultiEncoder);

            // Set the Local parameters on the Sensor
            htmSensor.InitEncoder(GetCategoryEncoderParams());
            List<EncoderTuple> encoders = multiEncoder.GetEncoders(multiEncoder);
            Assert.AreEqual(3, encoders.Count);

            DateEncoder dateEnc = (DateEncoder)encoders[1].GetEncoder();
            SDRCategoryEncoder catEnc = (SDRCategoryEncoder)encoders[2].GetEncoder();
            Assert.AreEqual("[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0]", Arrays.ToString(catEnc.Encode("ES")));

            // Now test the encoding of an input row
            Map<string, object> d = new Map<string, object>();
            d.Add("timestamp", dateEnc.Parse("7/12/10 13:10"));
            d.Add("consumption", 35.3);
            d.Add("type", "ES");
            int[] output = multiEncoder.Encode(d);
            int[] expected = { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0 };

            Debug.WriteLine(Arrays.ToString(expected));
            Debug.WriteLine(Arrays.ToString(output));
            Assert.IsTrue(Arrays.AreEqual(expected, output));
        }

        /**
         * Tests that a meaningful exception is thrown when no list category encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestListCategoryEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("list")
                .AddHeader("C")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no string category encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestStringCategoryEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("string")
                .AddHeader("C")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no date encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestDateEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("datetime")
                .AddHeader("T")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                25,
                3,
                0, 0, 0, 0.1, null, null, null,
                "consumption", "float", "RandomDistributedScalarEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no geo encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestGeoEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("geo")
                .AddHeader("")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no coordinate encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestCoordinateEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("coord")
                .AddHeader("")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no int number encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestIntNumberEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("int")
                .AddHeader("")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no float number encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestFloatNumberEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("float")
                .AddHeader("")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests that a meaningful exception is thrown when no boolean encoder configuration was provided
         */
        [TestMethod, ExpectedException(typeof(ArgumentException))]
        public void TestBoolEncoderNotInitialized()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("foo")
                .AddHeader("bool")
                .AddHeader("")
                .Build();
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(ObservableSensor<string[]>.Create, SensorParams.Create(
                SensorParams.Keys.Obs, "", manual));
            Map<string, Map<string, object>> fieldEncodings = SetupMap(null,
                0, // n
                0, // w
                0, 0, 0, 0, null, null, null,
                "timestamp", "datetime", "DateEncoder");
            Parameters @params = Parameters.GetEncoderDefaultParameters();
            @params.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, fieldEncodings);
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            htmSensor.InitEncoder(@params);
        }

        /**
         * Tests the auto-creation of Encoders from Sensor meta data.
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.Csv")]
        public void TestInternalEncoderCreation()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                SensorParams.Create(
                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly.Csv")));


            // Cast the ValueList to the more complex type (Header)
            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            Header meta = (Header)htmSensor.GetMetaInfo();
            Assert.IsTrue(meta.GetFieldTypes().TrueForAll(
                l => l.Equals(FieldMetaType.DateTime) || l.Equals(FieldMetaType.Float)));
            Assert.IsTrue(meta.GetFieldNames().TrueForAll(
                l => l.Equals("timestamp") || l.Equals("consumption")));
            Assert.IsTrue(meta.GetFlags().TrueForAll(
                l => l.Equals(SensorFlags.T) || l.Equals(SensorFlags.B)));

            // Set the parameters on the sensor.
            // This enables it to auto-configure itself; a step which will
            // be done at the Region level.
            Encoder<object> multiEncoder = htmSensor.GetEncoder();
            Assert.IsNotNull(multiEncoder);
            Assert.IsTrue(multiEncoder is MultiEncoder);

            // Set the Local parameters on the Sensor
            htmSensor.InitEncoder(GetTestEncoderParams());
            List<EncoderTuple> encoders = multiEncoder.GetEncoders(multiEncoder);
            Assert.AreEqual(2, encoders.Count);

            // Test date specific encoder configuration
            //
            // All encoders in the MultiEncoder are accessed in a particular
            // order (the alphabetical order their corresponding fields are in),
            // so alphabetically "consumption" proceeds "timestamp"
            // so we need to ensure that the proper order is preserved (i.E. exists at index 1)
            DateEncoder dateEnc = (DateEncoder)encoders[1].GetEncoder();
            try
            {
                dateEnc.ParseEncode("7/12/10 13:10");
                dateEnc.ParseEncode("7/12/2010 13:10");
                // Should fail here due to conflict with configured format
                dateEnc.ParseEncode("--13:10 7-12-10");
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.IsInstanceOfType(e, typeof(FormatException));
                //Assert.AreEqual("Invalid format: \"13:10 7/12/10\" is malformed at \":10 7/12/10\"", e.Message);
            }

            RandomDistributedScalarEncoder rdse = (RandomDistributedScalarEncoder)encoders[0].GetEncoder();
            int[] encoding = rdse.Encode(35.3);
            Console.WriteLine("rdse: " + Arrays.ToString(encoding));

            // Now test the encoding of an input row
            Map<string, object> d = new Map<string, object>();
            d.Add("timestamp", dateEnc.Parse("7/12/10 13:10"));
            d.Add("consumption", 35.3);
            int[] output = multiEncoder.Encode(d);
            int[] expected = { 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 };
            Console.WriteLine(Arrays.ToString(expected));
            Console.WriteLine(Arrays.ToString(output));
            Assert.IsTrue(Arrays.AreEqual(expected, output));
        }

        /**
         * Test that we can query the stream for its terminal state, which {@link Stream}s
         * don't provide out of the box.
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.Csv")]
        public void TestSensorTerminalOperationDetection()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                SensorParams.Create(
                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.Csv")));

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;
            // We haven't done anything with the stream yet, so it should not be terminal
            Assert.IsFalse(htmSensor.IsTerminal());
            ((BatchedCsvStream<string[]>)htmSensor.GetInputStream()).ForEach(l => Console.WriteLine(Arrays.ToString((string[])l)));
            // Should now be terminal after operating on the stream
            Assert.IsTrue(htmSensor.IsTerminal());
        }

        /**
         * Tests mechanism by which {@link Sensor}s will input information
         * and output information ensuring that multiple streams can be created.
         */
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-small.Csv")]
        public void TestSensorMultipleStreamCreation()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(
                FileSensor.Create,
                SensorParams.Create(
                    SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-small.Csv")));

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            htmSensor.InitEncoder(GetTestEncoderParams());

            // Ensure that the HTMSensor's output stream can be retrieved more than once.
            IStream<int[]> outputStream = (IStream<int[]>) htmSensor.GetOutputStream();
            IStream<int[]> outputStream2 = (IStream<int[]>)htmSensor.GetOutputStream();
            IStream<int[]> outputStream3 = (IStream<int[]>)htmSensor.GetOutputStream();

            // Check to make sure above multiple retrieval doesn't flag the underlying stream as operated upon
            Assert.IsFalse(htmSensor.IsTerminal());
            Assert.AreEqual(17, outputStream.Count());

            //After the above we cannot request a new stream, so this will fail
            //however, the above streams that were already requested should be unaffected.
            Assert.IsTrue(htmSensor.IsTerminal(), "Terminal sensor stream expected");
            try
            {
                //@SuppressWarnings("unused")
                IStream<int[]> outputStream4 = (Stream<int[]>)htmSensor.GetOutputStream();
                Assert.Fail();
            }
            catch (Exception e)
            {
                Assert.AreEqual("Stream is already \"terminal\" (operated upon or empty)", e.Message);
            }

            //These Streams were created before operating on a stream
            Assert.AreEqual(17, outputStream2.Count());
            Assert.AreEqual(17, outputStream3.Count());

            // Verify that different streams are retrieved.
            Assert.IsFalse(outputStream.GetHashCode() == outputStream2.GetHashCode());
            Assert.IsFalse(outputStream2.GetHashCode() == outputStream3.GetHashCode());
        }

        [TestMethod, DeploymentItem("Resources\\1_100.Csv")]
        public void TestInputIntegerArray()
        {
            Sensor<FileInfo> sensor = Sensor<FileInfo>.Create(FileSensor.Create,
                SensorParams.Create(SensorParams.Keys.Path, "", ResourceLocator.Path(typeof(Resources), "1_100.Csv")));

            HTMSensor<FileInfo> htmSensor = (HTMSensor<FileInfo>)sensor;

            htmSensor.InitEncoder(GetArrayTestParams());

            // Ensure that the HTMSensor's output stream can be retrieved more than once.
            FanOutStream<int[]> outputStream = (FanOutStream<int[]>)htmSensor.GetOutputStream();
            Assert.AreEqual(884, ((int[])outputStream.First()).Length);
        }

    }
}