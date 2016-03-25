using System;
using System.Collections.Generic;
using System.IO;
using HTM.Net.Datagen;
using HTM.Net.Network.Sensor;
using HTM.Net.Tests.Properties;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Tests.Network.Sensor
{
    /**
 * Tests {@link Header} condition flag configuration and 
 * state management.
 * 
 * @author David Ray
 * @see SensorFlags
 * @see FieldMetaType
 * @see Header
 */
    [TestClass]
    public class HeaderTest
    {
        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4reset.csv")]
        public void TestHeader()
        {
            object[] n = { "some name", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4reset.csv") };

            Sensor<FileInfo> sensor2 = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, n));

            Header header = new Header(sensor2.GetMetaInfo());
            Assert.AreEqual("[T, B, R]", Arrays.ToString(header.GetFlags().ToArray()));
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4period.csv"), DeploymentItem("Resources\\rec-center-hourly-4seqReset.csv")]
        public void TestProcessSequence()
        {
            Header header = new Header(GetTestHeaderOff());
            List<string[]> lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4period.Csv"));

            foreach (string[] line in lines)
            {
                header.Process(line);
                Assert.IsFalse(header.IsReset());
                Assert.IsTrue(header.IsLearn());
            }

            header = new Header(GetTestHeaderSeq());
            lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4seqReset.Csv"));
            int idx = 0;
            foreach (string[] line in lines)
            {
                string[] shifted = new string[line.Length + 1];
                Array.Copy(line, 0, shifted, 1, line.Length);
                shifted[0] = idx.ToString();

                header.Process(shifted);

                if (idx > 0 && idx % 24 == 0)
                {
                    Assert.IsTrue(header.IsReset());
                }
                else {
                    Assert.IsFalse(header.IsReset());
                }
                idx++;
            }
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4period-cat.csv")]
        public void TestProcessCategories()
        {
            object[] n = { "some name", ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4period-cat.csv") };

            Sensor<FileInfo> sensor2 = Sensor<FileInfo>.Create(
                FileSensor.Create,
                    SensorParams.Create(SensorParams.Keys.Path, n));

            Header header = new Header(sensor2.GetMetaInfo());
            Assert.AreEqual("[T, B, C]", Arrays.ToString(header.GetFlags().ToArray()));
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4period.csv"), DeploymentItem("Resources\\rec-center-hourly-4reset.csv")]
        public void TestProcessReset()
        {
            Header header = new Header(GetTestHeaderOff());
            List<string[]> lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4period.csv"));

            foreach (string[] line in lines)
            {
                header.Process(line);
                Assert.IsFalse(header.IsReset());
                Assert.IsTrue(header.IsLearn());
            }

            header = new Header(GetTestHeaderReset());
            lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4reset.csv"));
            int idx = 0;
            foreach (string[] line in lines)
            {
                string[] shifted = new string[line.Length + 1];
                Array.Copy(line, 0, shifted, 1, line.Length);
                shifted[0] = idx.ToString();

                header.Process(shifted);

                if (line[2].Equals("1"))
                {
                    Assert.IsTrue(header.IsReset());
                }
                else {
                    Assert.IsFalse(header.IsReset());
                }
                idx++;
            }
        }

        [TestMethod, DeploymentItem("Resources\\rec-center-hourly-4period.csv"), DeploymentItem("Resources\\rec-center-hourly-4learn.csv")]
        public void TestProcessLearn()
        {
            Header header = new Header(GetTestHeaderOff());
            List<string[]> lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4period.csv"));

            foreach (string[] line in lines)
            {
                header.Process(line);
                Assert.IsFalse(header.IsReset());
                Assert.IsTrue(header.IsLearn());
            }

            header = new Header(GetTestHeaderLearn());
            lines = GetLines(ResourceLocator.Path(typeof(Resources), "rec-center-hourly-4learn.csv"));

            int idx = 0;
            foreach (string[] line in lines)
            {
                string[] shifted = new string[line.Length + 1];
                Array.Copy(line, 0, shifted, 1, line.Length);
                shifted[0] = idx.ToString();

                if (idx == 72)
                {
                    idx = 72;
                }

                header.Process(shifted);

                if (line[2].Equals("1"))
                {
                    Assert.IsTrue(header.IsLearn());
                }
                else {
                    Assert.IsFalse(header.IsLearn());
                }
                idx++;
            }
        }

        private List<string[]> GetLines(string path)
        {
            List<string[]> retVal = new List<string[]>();
            FileInfo f = new FileInfo(path);
            StreamReader buf = null;
            try
            {
                buf = new StreamReader(f.Open(FileMode.Open));
                string line = null;
                int headerCount = 0;
                while ((line = buf.ReadLine()) != null)
                {
                    if (headerCount++ < 3) continue;
                    retVal.Add(line.Split(new[] {","}, StringSplitOptions.None));
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                try
                {
                    buf.Close();
                }
                catch (Exception ignore) { }
            }

            return retVal;
        }

        private IValueList GetTestHeaderOff()
        {
            Tuple[] ta = new Tuple[] {
                    new Tuple("timestamp", "consumption"),
                    new Tuple("datetime", "float"),
                    new Tuple("T"),
                };
            return new MockValueList
            {
                GetRowFunc = row => ta[row],
                SizeFunc = () => ta.Length
            };
            //return new ValueList() {


            //    public Tuple getRow(int row)
            //    {
            //        return ta[row];
            //    }

            //    public int size()
            //    {
            //        return ta.Length;
            //    }
            //};
        }

        public class MockValueList : IValueList
        {
            public System.Func<int, Tuple> GetRowFunc { get; set; }
            public Func<int> SizeFunc { get; set; }
            public Func<bool> IsLearnFunc { get; set; }
            public Func<bool> IsResetFunc { get; set; }
            public Func<List<FieldMetaType>> GetFieldTypesFunc { get; set; }

            public Tuple GetRow(int row)
            {
                return GetRowFunc?.Invoke(row);
            }

            public int Size()
            {
                return SizeFunc?.Invoke() ?? 0;
            }

            public bool IsLearn()
            {
                return IsLearnFunc?.Invoke() ?? false;
            }

            public bool IsReset()
            {
                return IsResetFunc?.Invoke() ?? false;
            }

            public List<FieldMetaType> GetFieldTypes()
            {
                return GetFieldTypesFunc?.Invoke();
            }
        }


        private IValueList GetTestHeaderSeq()
        {
            Tuple[] ta = {
                new Tuple("timestamp", "consumption"),
                new Tuple("datetime", "float"),
                new Tuple("T", "B", "S"),
            };
            return new MockValueList
            {
                GetRowFunc = row => ta[row],
                SizeFunc = () => ta.Length
            };
        }

        private IValueList GetTestHeaderReset()
        {
            Tuple[] ta = new Tuple[] {
                new Tuple("timestamp", "consumption"),
                new Tuple("datetime", "float"),
                new Tuple("T", "B", "R"),
            };
            return new MockValueList
            {
                GetRowFunc = row => ta[row],
                SizeFunc = () => ta.Length
            };
        }

        private IValueList GetTestHeaderLearn()
        {
            Tuple[] ta = new Tuple[] {
                new Tuple("timestamp", "consumption"),
                new Tuple("datetime", "float"),
                new Tuple("T", "B", "L"),
            };
            return new MockValueList
            {
                GetRowFunc = row => ta[row],
                SizeFunc = () => ta.Length
            };
        }

        //    public static void main(String[] args)
        //{
        //    File f = new File("/Users/cogmission/git/htm.java/src/test/resources/rec-center-hourly-4period.Csv");
        //    File fout = new File("/Users/cogmission/git/htm.java/src/test/resources/rec-center-hourly-4temp.Csv");
        //    BufferedReader buf = null;
        //    PrintWriter p = null;
        //    try
        //    {
        //        buf = new BufferedReader(new FileReader(f));
        //        p = new PrintWriter(new FileWriter(fout));
        //        String line = null;
        //        int counter = 0;
        //        int headerCount = 0;
        //        while ((line = buf.readLine()) != null)
        //        {
        //            if (headerCount++ > 2)
        //            {
        //                if (counter < 24)
        //                {
        //                    line = line.Concat(",.");
        //                    counter++;
        //                }
        //                else {
        //                    line = line.Concat(",?");
        //                    counter = 0;
        //                }
        //            }
        //            p.Println(line);
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        e.PrintStackTrace();
        //    }
        //    finally
        //    {
        //        try
        //        {
        //            buf.Close();
        //            p.flush();
        //            p.Close();
        //        }
        //        catch (Exception ignore) { }
        //    }
        //}
    }
}