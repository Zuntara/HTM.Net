using HTM.Net.Encoders;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Encoders
{
    [TestClass]
    public class GeospatialCoordinateEncoderTest
    {
        private GeospatialCoordinateEncoder ge;
        private GeospatialCoordinateEncoder.Builder builder;

        private void SetUp()
        {
            builder = (GeospatialCoordinateEncoder.Builder) GeospatialCoordinateEncoder.GetGeobuilder()
                .Name("coordinate")
                .N(33)
                .W(3);
        }

        private void InitGe()
        {
            ge = (GeospatialCoordinateEncoder) builder.Build();
        }

        [TestMethod]
    public void TestCoordinateForPosition()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60);
            InitGe();

            double[] coords = new[] { -122.229194, 37.486782 };
            int[] coordinate = ge.CoordinateForPosition(coords[0], coords[1]);

            Assert.IsTrue(Arrays.AreEqual(new[] { -453549, 150239 }, coordinate));
        }

        [TestMethod]
    public void TestCoordinateForPositionOrigin()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            InitGe();

            double[] coords = new double[] { 0, 0 };
            int[] coordinate = ge.CoordinateForPosition(coords[0], coords[1]);

            Assert.IsTrue(Arrays.AreEqual(new[] { 0, 0 }, coordinate));
        }

        [TestMethod]
    public void TestRadiusForSpeed()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            InitGe();

            double speed = 50;//meters per second
            double radius = ge.RadiusForSpeed(speed);
            Assert.AreEqual(radius, 75, 0.1);
        }

        [TestMethod]
    public void TestRadiusForSpeed0()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            builder.N(999);
            builder.W(27);
            InitGe();

            double speed = 0;//meters per second
            double radius = ge.RadiusForSpeed(speed);
            Assert.AreEqual(radius, 3, 0.1);
        }

        [TestMethod]
    public void TestRadiusForSpeedInt()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            InitGe();

            double speed = 25;//meters per second
            double radius = ge.RadiusForSpeed(speed);
            Assert.AreEqual(radius, 38, 0.1);
        }

        [TestMethod]
    public void TestEncodeIntoArray()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            builder.N(999);
            builder.W(25);
            InitGe();

            double speed = 2.5;//meters per second

            int[] encoding1 = Encode(ge, new[] { -122.229194, 37.486782 }, speed);
            int[] encoding2 = Encode(ge, new[] { -122.229294, 37.486882 }, speed);
            int[] encoding3 = Encode(ge, new[] { -122.229294, 37.486982 }, speed);

            double overlap1 = Overlap(encoding1, encoding2);
            double overlap2 = Overlap(encoding1, encoding3);

            Assert.IsTrue(overlap1 > overlap2);
        }

        public int[] Encode(CoordinateEncoder encoder, double[] coordinate, double radius)
        {
            int[] output = new int[encoder.GetWidth()];
            encoder.EncodeIntoArray(new Tuple(coordinate[0], coordinate[1], radius), output);
            return output;
        }

        public double Overlap(int[] sdr1, int[] sdr2)
        {
            Assert.AreEqual(sdr1.Length, sdr2.Length);
            int sum = ArrayUtils.Sum(ArrayUtils.And(sdr1, sdr2));

            return sum / (double)ArrayUtils.Sum(sdr1);
        }

        [TestMethod]
    public void TestLongLatMercatorTransform()
        {
            SetUp();
            builder.Scale(30); //meters
            builder.Timestep(60); //seconds
            InitGe();

            double[] coords = new[] { -122.229194, 37.486782 };

            double[] mercatorCoords = ge.ToMercator(coords[0], coords[1]);
            Assert.AreEqual(mercatorCoords[0], -13606491.6342, 0.0001);
            Assert.AreEqual(mercatorCoords[1], 4507176.870955294, 0.0001);

            double[] longlats = ge.InverseMercator(mercatorCoords[0], mercatorCoords[1]);
            Assert.AreEqual(coords[0], longlats[0], 0.0001);
            Assert.AreEqual(coords[1], longlats[1], 0.0001);

        }
    }
}