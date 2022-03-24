using System;
using System.Linq;
using System.Text;

using HTM.Net.Util;

using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util;

[TestClass]
public class UniversalRandomTest
{
    [TestMethod]
    public void TestRandom()
    {
        UniversalRandom random = new UniversalRandom(42);

        long s = 2858730232218250L;
        long e = (s >> 35);
        Assert.AreEqual(83200, e);

        int x = random.NextInt(50);
        //System.out.println("x = " + x);
        Assert.AreEqual(0, x);

        x = random.NextInt(50);
        //System.out.println("x = " + x);
        Assert.AreEqual(26, x);

        x = random.NextInt(50);
        //System.out.println("x = " + x);
        Assert.AreEqual(14, x);

        x = random.NextInt(50);
        //System.out.println("x = " + x);
        Assert.AreEqual(15, x);

        x = random.NextInt(50);
        //System.out.println("x = " + x);
        Assert.AreEqual(38, x);

        int[] expecteds = { 47, 13, 9, 15, 31, 6, 3, 0, 21, 45 };
        for (int i = 0; i < 10; i++)
        {
            int o = random.NextInt(50);
            Assert.AreEqual(expecteds[i], o);
        }

        double[] exp = {
            0.945,
            0.2426,
            0.5214,
            0.0815,
            0.0988,
            0.5497,
            0.4013,
            0.4559,
            0.5415,
            0.2381
        };
        random = new UniversalRandom(42);
        for (int i = 0; i < 10; i++)
        {
            double o = random.NextDouble();
            Assert.AreEqual(exp[i], o, 0.0001);
        }
    }

    [TestMethod]
    public void TestMain()
    {
        StringBuilder builder = new StringBuilder();
        UniversalRandom.Main(builder);

        string[] lines = builder.ToString().Split(Environment.NewLine);

        lines.ToList().ForEach(Console.WriteLine);

        string[] expected = {
            "e = 83200",
            "x = 0",
            "x = 26",
            "x = 14",
            "x = 15",
            "x = 38",
            "x = 47",
            "x = 13",
            "x = 9",
            "x = 15",
            "x = 31",
            "x = 6",
            "x = 3",
            "x = 0",
            "x = 21",
            "x = 45",
            "d = 0.945",
            "d = 0.2426",
            "d = 0.5214",
            "d = 0.0815",
            "d = 0.0988",
            "d = 0.5497",
            "d = 0.4013",
            "d = 0.4559",
            "d = 0.5415",
            "d = 0.2381"
        };

        Enumerable.Range(0, expected.Length).ToList().ForEach(i => Assert.AreEqual(lines[i], expected[i]));
    }
}