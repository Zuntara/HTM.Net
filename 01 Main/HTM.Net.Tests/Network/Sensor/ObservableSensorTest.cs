using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using HTM.Net.Datagen;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Tests.Network.Sensor
{
    [TestClass]
    public class ObservableSensorTest
    {
        private IObservable<string> makeFileObservable()
        {
            FileInfo f = new FileInfo(ResourceLocator.Path(typeof(Resources), "rec-center-hourly.csv"));
            try
            {
                List<string> arr = new List<string>();
                using (StreamReader sr = new StreamReader(f.OpenRead()))
                {
                    string line = null;
                    while (!sr.EndOfStream)
                    {
                        arr.Add(sr.ReadLine());
                    }
                }

                //Observable <?> o = Observable.from(Files.lines(f.toPath(), Charset.forName("UTF-8")).toArray());
                return arr.ToObservable();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly.csv")]
        public void TestObservableFromFile()
        {
            object[] n = { "some name", makeFileObservable() };
            SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, n);
            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(ObservableSensor<string[]>.Create, parms);
            var inputStream = sensor.GetInputStream();
            long count = inputStream.Count();
            Assert.AreEqual(4391, count);
        }
        [TestMethod]
        public void TestOpenObservableWithExplicitEntry()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("timestamp,consumption")
                .AddHeader("datetime,float")
                .AddHeader("B")
                .Build();

            object[] n = { "some name", manual };
            SensorParams parms = SensorParams.Create(SensorParams.Keys.Obs, n);
            Sensor<ObservableSensor<string[]>> sensor =
                Sensor<ObservableSensor<string[]>>.Create(ObservableSensor<string[]>.Create, parms);

            // Test input is "sequenced" and is processed by underlying HTMSensor
            string[] expected = {
                "[0, 7/2/10 0:00, 21.2]",
                "[1, 7/2/10 1:00, 34.0]",
                "[2, 7/2/10 2:00, 40.4]",
                "[3, 7/2/10 3:00, 123.4]"
            };

            int i = 0;
            var t = new Task(() =>
            {
                sensor.GetInputStream().ForEach(l =>
                {
                    Console.WriteLine(Arrays.ToString((string[])l));
                    Assert.AreEqual(expected[i++], Arrays.ToString((string[])l));
                });
            });

            string[] entries =
            {
                "7/2/10 0:00,21.2",
                "7/2/10 1:00,34.0",
                "7/2/10 2:00,40.4",
                "7/2/10 3:00,123.4",
            };
            manual.OnNext(entries[0]);
            manual.OnNext(entries[1]);
            manual.OnNext(entries[2]);
            manual.OnNext(entries[3]);

            t.Start();
            try
            {
                t.Wait();
            }
            catch (Exception e)
            {
                Assert.Fail(e.ToString());
            }
        }

        [TestMethod, DeploymentItem("Resources\\1_100.csv")]
        public void TestReadIntegerArray()
        {
            try
            {
                string[] s = File.ReadAllLines("1_100.csv");

                int[][] ia = s.Skip(3).Select(l => Regex.Split(l, "[\\s]*\\,[\\s]*"))
                    .Select(i => i.Select(int.Parse).ToArray())
                    .ToArray();

                Assert.AreEqual(100, ia.Length);
                Assert.IsTrue(Arrays.ToString(ia[0]).Equals(
                    "[0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
                    + "1, 1, 1, 1, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 0, 0, 0, "
                    + "0, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, "
                    + "1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, "
                    + "1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, "
                    + "0, 0, 0, 0, 0, 0, 0, 0, 0, 0]"));
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        [TestMethod, DeploymentItem("Resources\\1_100.csv")]
        public void TestInputIntegerArray()
        {
            Publisher manual = Publisher.GetBuilder()
                .AddHeader("sdr_in")
                .AddHeader("sarr")
                .AddHeader("B")
                .Build();

            Sensor<ObservableSensor<string[]>> sensor = Sensor<ObservableSensor<string[]>>.Create(
                ObservableSensor<string[]>.Create, SensorParams.Create(SensorParams.Keys.Obs, new Object[] { "name", manual }));

            int i = 0;
            var t = new Task(() =>
            {
                BatchedCsvStream<string[]> iStream = (BatchedCsvStream<string[]>) sensor.GetInputStream();
                Stream<string[]> oStream = (Stream<string[]>) iStream._contentStream;
                Assert.AreEqual(2, ((string[])oStream.First()).Length);
            });

            //(t = new Thread() {
            //        int i = 0;
            //        public void run()
            //    {
            //        assertEquals(2, ((String[])sensor.getInputStream().findFirst().get()).length);
            //    }
            //}).start();

            int[][] ia = getArrayFromFile(ResourceLocator.Path(typeof(Resources), "1_100.csv"));
            manual.OnNext(Arrays.ToString(ia[0]).Trim());
            t.Start();
            try
            {
                t.Wait();
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                Assert.Fail();
            }
        }

        private int[][] getArrayFromFile(String path)
        {
            int[][] retVal = null;

            try
            {
                string[] s = File.ReadAllLines(path);

                int[][] ia = s.Skip(3).Select(l => Regex.Split(l, "[\\s]*\\,[\\s]*"))
                    .Select(i => i.Select(int.Parse).ToArray())
                    .ToArray();
                
                retVal = ia;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return retVal;
        }

    }
}