using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Util
{
    [TestClass]
    public class StreamTest
    {
        [TestMethod]
        public void TestStringStream_Normal()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(source);

            Assert.AreEqual("1", stream.Read());
            Assert.AreEqual("2", stream.Read());
            Assert.AreEqual("3", stream.Read());
            Assert.IsNull(stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Normal_Yielding()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));

            Assert.AreEqual("1", stream.Read());
            Assert.AreEqual("2", stream.Read());
            Assert.AreEqual("3", stream.Read());
            Assert.IsNull(stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Normal_CopyStream()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));
            IStream<string> copy1 = stream.Copy();
            IStream<string> copy2 = stream.Copy();

            Assert.AreEqual("1", copy1.Read());
            Assert.AreEqual("1", copy2.Read());

            Assert.AreEqual("2", copy1.Read());
            Assert.AreEqual("2", copy2.Read());
            Assert.AreEqual("3", copy1.Read());
            Assert.AreEqual("3", copy2.Read());
            Assert.IsNull(stream.Read());
            Assert.IsNull(copy1.Read());
            Assert.IsNull(copy2.Read());
        }

        [TestMethod]
        public void TestStringStream_Normal_Filtered()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(source);

            IStream<string> filteredStream = stream.Filter(s => s == "2");

            Assert.AreEqual("2", filteredStream.Read());
            Assert.IsNull(filteredStream.Read());

            Assert.AreEqual(null, stream.Read());    // the normal stream continues reading
        }

        [TestMethod]
        public void TestStringStream_Normal_Filtered_Yielding()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));

            IStream<string> filteredStream = stream.Filter(s => s == "2");

            Assert.AreEqual("2", filteredStream.Read());
            Assert.IsNull(filteredStream.Read());

            Assert.AreEqual(null, stream.Read());    // the normal stream continues reading
        }

        [TestMethod]
        public void TestStringStream_Normal_Map_Ideal()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(source);

            IStream<int[]> mappedStream = stream.Map(s => new int[int.Parse(s)]);

            Assert.IsTrue(new int[1].SequenceEqual(mappedStream.Read()));
            Assert.IsTrue(new int[2].SequenceEqual(mappedStream.Read()));
            Assert.IsTrue(new int[3].SequenceEqual(mappedStream.Read()));
            Assert.IsNull(mappedStream.Read());
            Assert.AreEqual(null, stream.Read());    // non mapped stream is continueing to read
        }

        [TestMethod]
        public void TestStringStream_Normal_Map_Early()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(source);

            Assert.AreEqual("1", stream.Read());

            IStream<int[]> mappedStream = stream.Map(s => new int[int.Parse(s)]);

            //Assert.IsTrue(new int[1].SequenceEqual(mappedStream.Read()));
            Assert.IsTrue(new int[2].SequenceEqual(mappedStream.Read()));
            Assert.IsTrue(new int[3].SequenceEqual(mappedStream.Read()));
            Assert.IsNull(mappedStream.Read());
            Assert.AreEqual(null, stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Fluent_Map_MoreStages()
        {
            string[] source = { "1,1", "2,1", "3,1" };

            Stream<string> stream = new Stream<string>(source);

            IStream<string[]> mappedStream1 = stream.Map(s => s.Split(','));
            IStream<int[]> mappedStream2 = mappedStream1.Map(s => new int[int.Parse(s[0])]);

            Assert.IsTrue(new int[1].SequenceEqual(mappedStream2.Read()));
            Assert.IsTrue(new int[2].SequenceEqual(mappedStream2.Read()));
            Assert.IsTrue(new int[3].SequenceEqual(mappedStream2.Read()));

            Assert.IsNull(mappedStream2.Read());
            Assert.AreEqual(null, stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Normal_Foreach()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(source);

            IStream<int[]> mappedStream = stream.Map<int[]>(s => new int[int.Parse(s)]);

            int counter = 0;

            mappedStream.ForEach(v =>
            {
                Assert.IsNotNull(v);
                counter++;
            });

            Assert.AreEqual(3, counter);
        }

        [TestMethod]
        public void TestStringStream_Observed_Replay()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            subject.OnNext("1");

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            Assert.AreEqual("1", stream.Read());

            subject.OnNext("2");

            Assert.AreEqual("2", stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Observed_Normal()
        {
            Subject<string> subject = new Subject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            subject.OnNext("1");

            Assert.AreEqual("1", stream.Read());

            subject.OnNext("2");

            Assert.AreEqual("2", stream.Read());
        }

        [TestMethod]
        public void TestStringStream_Observed_Replay_Mapped()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            IStream<int[]> mappedStream = stream.Map(s => new int[int.Parse(s)]);

            subject.OnNext("1");
            Debug.WriteLine("> Reading from mapped stream");
            Assert.IsTrue(new int[1].SequenceEqual(mappedStream.Read()));
            Debug.WriteLine("> Reading from base stream");
            Assert.AreEqual(null, stream.Read());

            subject.OnNext("2");

            Debug.WriteLine("> Reading from base stream");
            Assert.AreEqual("2", stream.Read());
            Debug.WriteLine("> Reading from mapped stream");
            Assert.AreEqual(null, mappedStream.Read());

            Assert.AreEqual(2, stream.Count());
            Assert.AreEqual(2, mappedStream.Count());
        }

        [TestMethod]
        public void TestStringStream_Observed_Replay_Mapped_Copy()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            IStream<int[]> mappedStream = stream.Map(s => new int[int.Parse(s)]);

            IStream<int[]> copyStream = mappedStream.Copy();

            subject.OnNext("1");

            Assert.IsTrue(new int[1].SequenceEqual(copyStream.Read()));

            subject.OnNext("2");

            Assert.IsTrue(new int[2].SequenceEqual(copyStream.Read()));

            Assert.AreEqual(2, copyStream.Count());
        }

        [TestMethod]
        public void TestStringStream_Yielding_Mapped_Copy()
        {
            string[] source = { "1", "2", "3", "4", "5" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));
            stream.Read();
            stream.Read();
            stream.Read();
            stream.SetOffset(3);
            // Add first mapping
            IStream<string> mappedStream1 = stream.Map(s => "map1_" + s);
            // Add second mapping
            IStream<string> mappedStream2 = mappedStream1.Map(s => "map2_" + s);
            // Add fanout
            IStream<string> output = mappedStream2.Copy();
            // read fanout and check all mappings are called
            string result = output.Read();
            Assert.AreEqual("map2_map1_4", result);
        }

        [TestMethod]
        public void TestStringStream_Observed_Mapped_Copy()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            subject.OnNext("1");
            subject.OnNext("2");
            subject.OnNext("3");
            subject.OnNext("4");
            subject.OnNext("5");

            stream.Read();
            stream.Read();
            stream.Read();
            stream.SetOffset(3);
            // Add first mapping
            IStream<string> mappedStream1 = stream.Map(s => "map1_" + s);
            // Add second mapping
            IStream<string> mappedStream2 = mappedStream1.Map(s => "map2_" + s);
            // Add fanout
            IStream<string> output = mappedStream2.Copy();
            // read fanout and check all mappings are called
            string result = output.Read();
            Assert.AreEqual("map2_map1_4", result);
        }

        [TestMethod]
        public void TestStringStream_Observed_Replay_Mapped_Copy_InterruptedSupply()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            int mappingsHit = 0;

            IStream<string> mappedStream = stream.Map(s =>
            {
                Debug.WriteLine("mapping " + s);
                mappingsHit++;
                return "m" + s;
            });

            IStream<string> copyStream = mappedStream.Copy();

            for (int i = 0; i < 100; i++)
            {
                subject.OnNext(i.ToString());
            }

            for (int i = 0; i < 50; i++)
            {
                Assert.AreEqual("m" + i, copyStream.Read());
            }

            Assert.AreEqual(50, mappingsHit);

            for (int i = 100; i < 200; i++)
            {
                subject.OnNext(i.ToString());
            }

            for (int i = 50; i < 200; i++)
            {
                Assert.AreEqual("m" + i, copyStream.Read());
            }

            Assert.AreEqual(200, mappingsHit);

            Assert.AreEqual(200, copyStream.Count());
        }

        [TestMethod]
        public void TestEndOfStream_Normal()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));

            Assert.AreEqual("1", stream.Read());
            Assert.AreEqual("2", stream.Read());
            Assert.AreEqual("3", stream.Read());
            Assert.IsNull(stream.Read());
            Assert.IsTrue(stream.EndOfStream);
        }

        [TestMethod]
        public void TestEndOfStream_Observed()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            subject.OnNext("1");
            subject.OnNext("2");
            subject.OnNext("3");
            subject.OnCompleted();

            Assert.AreEqual("1", stream.Read());
            Assert.AreEqual("2", stream.Read());
            Assert.AreEqual("3", stream.Read());
            Assert.IsNull(stream.Read());
            Assert.IsTrue(stream.EndOfStream);
        }

        [TestMethod]
        public void TestEndOfStream_Observed_Delayed()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            Assert.IsFalse(stream.EndOfStream);
            Assert.AreEqual(null, stream.Read());
            Assert.IsFalse(stream.EndOfStream);

            subject.OnNext("1");
            subject.OnNext("2");
            subject.OnNext("3");
            subject.OnCompleted();

            Assert.AreEqual("1", stream.Read());
            Assert.AreEqual("2", stream.Read());
            Assert.AreEqual("3", stream.Read());
            Assert.IsNull(stream.Read());
            Assert.IsTrue(stream.EndOfStream);
        }

        [TestMethod]
        public void TestEndOfStream_Observed_Mapped_Delayed()
        {
            ReplaySubject<string> subject = new ReplaySubject<string>();

            Stream<string> stream = new Stream<string>(subject.AsObservable());

            int mappingsHit = 0;
            IStream<string> mappedStream = stream.Map(s =>
            {
                Debug.WriteLine("mapping " + s);
                mappingsHit++;
                return "m" + s;
            });

            Assert.IsFalse(mappedStream.EndOfStream);
            Assert.AreEqual(null, mappedStream.Read());
            Assert.IsFalse(mappedStream.EndOfStream, "Stream should not be end or stream yet");

            subject.OnNext("1");
            subject.OnNext("2");
            subject.OnNext("3");
            subject.OnCompleted();

            Assert.AreEqual("m1", mappedStream.Read());
            Assert.AreEqual("m2", mappedStream.Read());
            Assert.AreEqual("m3", mappedStream.Read());
            Assert.IsNull(mappedStream.Read());
            Assert.IsTrue(mappedStream.EndOfStream);
        }

        [TestMethod]
        public void TestStreamCollection_Normal()
        {
            string[] source = { "1", "2", "3" };
            var sc = new StreamCollection<string>(0, GetDataSourceYielding(source));

            int i = 1;
            foreach (string s in sc)
            {
                Debug.WriteLine("Retrieved " + s);
                Assert.AreEqual(i++.ToString(), s);
            }
        }

        [TestMethod]
        public void TestStreamCollection_Modifying()
        {
            LinkedList<string> list = new LinkedList<string>();

            for (int i = 0; i < 10; i++)
            {
                list.AddLast(i.ToString());
            }
            var sc = new StreamCollection<string>(0, list);

            var enumerator = sc.GetEnumerator();
            int j = 0;
            for (int i = 0; i < 5; i++)
            {
                enumerator.MoveNext();
                Assert.AreEqual(j++.ToString(), enumerator.Current);
            }
            list.AddLast("10");
            enumerator.MoveNext();
            Assert.AreEqual(j++.ToString(), enumerator.Current);
        }

        [TestMethod]
        public void TestFanoutStream_Copy_Filter()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));
            IStream<string> copy1 = stream.Copy();
            IStream<string> copy2 = stream.Copy();

            var filtered = copy1.Filter(s => s == "2");

            Assert.AreEqual("2", filtered.Read());

            Assert.AreEqual("1", copy2.Read());
            Assert.AreEqual("2", copy2.Read());
            Assert.AreEqual("3", copy2.Read());

            Assert.AreEqual(null, stream.Read());
            Assert.AreEqual(null, filtered.Read());
            Assert.AreEqual(null, copy1.Read());
            Assert.AreEqual(null, copy2.Read());
        }

        [TestMethod]
        public void TestFanoutStream_Copy_Filter_Foreach()
        {
            string[] source = { "1", "2", "3" };

            Stream<string> stream = new Stream<string>(GetDataSourceYielding(source));
            IStream<string> copy1 = stream.Copy();
            IStream<string> copy2 = stream.Copy();

            int passedForeach = 0;
            copy1.Filter(s => s == "2").ForEach(s =>
            {
                passedForeach++;
            });

            Assert.AreEqual(1, passedForeach);

            Assert.AreEqual("1", copy2.Read());
            Assert.AreEqual("2", copy2.Read());
            Assert.AreEqual("3", copy2.Read());

            Assert.AreEqual(null, stream.Read());
            Assert.AreEqual(null, copy1.Read());
            Assert.AreEqual(null, copy2.Read());
        }

        private IEnumerable<string> GetDataSourceYielding(string[] source)
        {
            foreach (string s in source)
            {
                Debug.WriteLine("Returning: " + s);
                yield return s;
            }
        }
    }
}