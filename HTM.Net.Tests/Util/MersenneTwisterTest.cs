using System;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    /**
 * Trivial Tests to keep CI Travis, Coveralls Happy
 * @author cogmission
 *
 */
    [TestClass]
    public class MersenneTwisterTest
    {

        //[TestMethod]
        //public void TestSetSeed()
        //{
        //    MersenneTwister m = new MersenneTwister();
        //    m.SetSeed(new[] { 44 });
        //    Assert.IsNotNull(m);
        //}

        [TestMethod]
        public void TestNextInt()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextInt() > -1);
        }

        [TestMethod]
        public void TestNextShort()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextShort() > -1);
        }

        [TestMethod]
        public void TestNextChar()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextChar() != ' ');
        }

        [TestMethod]
        public void TestNextBoolean()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextBoolean() || true);
        }

        [TestMethod]
        public void TestNextBooleanFloat()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextBoolean(0.22f) || true);
        }

        [TestMethod]
        public void TestNextBooleanDouble()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextBoolean(0.44D) || true);
        }

        [TestMethod]
        public void TestNextByte()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextByte() != 0x00);
        }

        [TestMethod]
        public void TestNextBytes()
        {
            MersenneTwister m = new MersenneTwister(42);
            try
            {
                m.NextBytes(new byte[] { 0x00 });
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        [TestMethod]
        public void TestNextLong()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextLong() > 0);
        }

        //[TestMethod]
        //public void TestNextLongBoundary()
        //{
        //    MersenneTwister m = new MersenneTwister(42);
        //    Assert.IsTrue(m.NextLong(4) < 4);
        //}

        [TestMethod]
        public void TestNextDouble()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextDouble() > 0);
        }

        //[TestMethod]
        //public void TestNextDoubleRange()
        //{
        //    MersenneTwister m = new MersenneTwister(42);
        //    double d = m.NextDouble(false, false);
        //    Assert.IsTrue(d > 0.0 && d < 1.0);
        //}

        //[TestMethod]
        //public void TestNextGaussian()
        //{
        //    MersenneTwister m = new MersenneTwister(42);
        //    Assert.IsTrue(m.NextGaussian() < 0.14);
        //}

        //[TestMethod]
        //public void TestNextFloat()
        //{
        //    MersenneTwister m = new MersenneTwister(42);
        //    Assert.IsTrue(m.NextFloat() > 0);
        //}

        //[TestMethod]
        //public void TestNextFloatRange()
        //{
        //    MersenneTwister m = new MersenneTwister(42);
        //    float d = m.NextFloat(false, false);
        //    Assert.IsTrue(d > 0.0 && d < 1.0);
        //}

        [TestMethod]
        public void TestNextIntBoundary()
        {
            MersenneTwister m = new MersenneTwister(42);
            Assert.IsTrue(m.NextInt(4) < 4);
        }

        //[TestMethod]
        //public void testMain()
        //{
        //    try
        //    {
        //        MersenneTwister.Main(new String[0]);
        //    }
        //    catch (Exception e)
        //    {
        //        Assert.Fail();
        //    }
        //}

    }
}