using System;
using System.Collections.Generic;
using HTM.Net.Model;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class GeneratorTests
    {
        /////////////////////////////////
        //       Utility Methods       //
        /////////////////////////////////
        /**
         * Returns an {@link AbstractGenerator} that runs for 30 iterations
         * 
         * @return  an {@link AbstractGenerator} that runs for 30 iterations
         */
        private Generator<int> GetGenerator()
        {
            Mock<Generator<int>> mock = new Mock<Generator<int>>();
            mock.CallBase = true;
            mock.Setup(m => m.Current).Returns(42);
            mock.Setup(m => m.MoveNext()).Returns(true);
            return mock.Object;
            //        return new Generator<int>()
            //        {
            //         /** serial version */
            //        private static final long serialVersionUID = 1L;

            //    @Override
            //        public boolean hasNext()
            //    {
            //        return true;
            //    }

            //    @Override
            //        public Integer next()
            //    {
            //        return new Integer(42);
            //    }

            //};
        }

        [TestMethod]
        public void TestInterface()
        {
            Generator<int> bg = GetGenerator();
            Assert.AreEqual(bg, bg.GetEnumerator());
            Assert.IsTrue(bg.MoveNext());
            Assert.AreEqual((int)42, (int)bg.Current);

            // Test other interface methods
            Assert.AreEqual(-1, bg.Get());
            Assert.AreEqual(-1, bg.Count);
            try
            {
                bg.Reset();
            }
            catch (Exception e)
            {
                Assert.Fail(e.Message);
            }
        }

        [TestMethod]
        public void TestOf()
        {
            List<int> l = new List<int>();
            l.Add(42);
            Generator<int> g = Generator<int>.Of(l, IntGenerator.Of(0, 1));
            Assert.IsTrue(g.MoveNext());
            Assert.AreEqual(42, (int)g.Current);
            Assert.IsFalse(g.MoveNext());
        }
    }

    [TestClass]
    public class IntGeneratorTests
    {
        /**
     * Test that iteration control is managed by the {@link AbstractStuff#exec()}
     * method, and that the execution can be precisely terminated.
     */
        [TestMethod]
        public void TestIntegerGenerator()
        {
            int i = 0;

            Generator<int> generator = IntGenerator.Of(0, 31);

            foreach (int result in generator)
            {
                Assert.AreNotEqual(result, i - 1);
                Assert.AreNotEqual(result, i + 1);
                Assert.AreEqual(result, i);
                i++;
            }

            Assert.IsTrue(i == 31);
            Assert.IsFalse(i == 32);
        }

        /**
         * Test that iteration control is managed by the {@link AbstractStuff#exec()}
         * method, and that the execution can be precisely terminated.
         */
        [TestMethod]
        public void TestIntegerGenerator_SpecifyNext()
        {
            int i = 28;

            Generator<int> generator = IntGenerator.Of(i, 31);

            generator.MoveNext();
            Assert.IsFalse(generator.Current == 29);

            generator.MoveNext();
            Assert.IsTrue(generator.Current == 29);
            generator.MoveNext();
            Assert.IsTrue(generator.Current == 30);
            Assert.IsFalse(generator.MoveNext());
        }

        [TestMethod]
        public void TestGet()
        {
            IntGenerator generator = IntGenerator.Of(0, 31);
            Assert.AreEqual(0, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(0, (int)generator.Current);
            Assert.AreEqual(1, generator.Get());
            Assert.AreEqual(1, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(1, (int)generator.Current);
            generator.MoveNext();
            Assert.AreEqual(2, (int)generator.Current);
            Assert.AreEqual(3, generator.Get());
        }

        [TestMethod]
        public void TestCount()
        {
            IntGenerator generator = IntGenerator.Of(-4, -4);
            Assert.AreEqual(0, generator.Count);
            generator.MoveNext();
            Assert.AreEqual(-4, generator.Current);
            generator.MoveNext();
            Assert.AreEqual(-4, generator.Current);
            Assert.IsFalse(generator.MoveNext());
        }

        [TestMethod]
        public void TestReset()
        {
            IntGenerator generator = IntGenerator.Of(0, 31);
            Assert.AreEqual(0, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(0, (int)generator.Current);
            Assert.AreEqual(1, generator.Get());
            Assert.AreEqual(1, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(1, (int)generator.Current);
            generator.MoveNext();
            Assert.AreEqual(2, (int)generator.Current);
            Assert.AreEqual(3, generator.Get());

            generator.Reset();
            Assert.AreEqual(0, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(0, (int)generator.Current);
            Assert.AreEqual(1, generator.Get());
            Assert.AreEqual(1, generator.Get());
            generator.MoveNext();
            Assert.AreEqual(1, (int)generator.Current);
            generator.MoveNext();
            Assert.AreEqual(2, (int)generator.Current);
            Assert.AreEqual(3, generator.Get());
        }
    }

    [TestClass]
    public class GroupByTests
    {

        [TestMethod]
        public void TestIntegerGroup()
        {
            List<int> l = Arrays.AsList(new int[] { 7, 12, 16 });
            //@SuppressWarnings("unchecked")
            List<Tuple<int, int>> expected = new List<Tuple<int, int>> {
                new Tuple<int, int>(7, 7),
                new Tuple<int, int>(12, 12),
                new Tuple<int, int>(16, 16)
            };
            GroupBy<int, int> grouper = GroupBy<int, int>.Of(l, d => d);

            int i = 0;
            int pairCount = 0;
            foreach (Tuple<int, int> p in grouper)
            {
                Assert.AreEqual(expected[i++], p);
                pairCount++;
            }

            Assert.AreEqual(3, pairCount);

            //////

            pairCount = 0;
            l = Arrays.AsList(new int[] { 2, 4, 4, 5 });

            List<Tuple<int, int>> expected2 = new List<Tuple<int, int>>
            {
                new Tuple<int, int>(2, 6),
                new Tuple<int, int>(4, 12),
                new Tuple<int, int>(4, 12),
                new Tuple<int, int>(5, 15)
            };

            grouper = GroupBy<int, int>.Of(l, @in => @in * 3);

            i = 0;
            foreach (Tuple<int, int> p in grouper)
            {
                Assert.AreEqual(expected2[i++], p);
                pairCount++;
            }

            Assert.AreEqual(4, pairCount);
        }

        [TestMethod]
        public void TestObjectGroup()
        {
            Column c0 = new Column(9, 0);
            Column c1 = new Column(9, 1);

            // Illustrates the Cell's actual index = colIndex * cellsPerColumn + indexOfCellWithinCol
            Assert.AreEqual(7, c0.GetCell(7).GetIndex());
            Assert.AreEqual(12, c1.GetCell(3).GetIndex());
            Assert.AreEqual(16, c1.GetCell(7).GetIndex());

            DistalDendrite dd0 = new DistalDendrite(c0.GetCell(7), 0, 0, 0);
            DistalDendrite dd1 = new DistalDendrite(c1.GetCell(3 /* Col 1's Cells start at 9 */), 1, 0, 1);
            DistalDendrite dd2 = new DistalDendrite(c1.GetCell(7/* Col 1's Cells start at 9 */), 2, 0, 2);

            List<DistalDendrite> l = new List<DistalDendrite> { dd0, dd1, dd2 };

            //@SuppressWarnings("unchecked")
            List<Tuple<DistalDendrite, Column>> expected = new List<Tuple<DistalDendrite, Column>>
            {
                new Tuple<DistalDendrite, Column>(dd0, c0),
                new Tuple<DistalDendrite, Column>(dd1, c1),
                new Tuple<DistalDendrite, Column>(dd2, c1)
            };

            GroupBy<DistalDendrite, Column> grouper = GroupBy<DistalDendrite, Column>.Of(l, d => d.GetParentCell().GetColumn());

            int i = 0;
            foreach (Tuple<DistalDendrite, Column> p in grouper)
            {
                Assert.AreEqual(expected[i++], p);
            }
        }
    }
}