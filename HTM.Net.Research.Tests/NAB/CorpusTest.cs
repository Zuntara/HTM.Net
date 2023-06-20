using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HTM.Net.Research.NAB;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class CorpusTest
{
    private static string _root;
    private static string _corpusSource;
    private ICorpus _corpus;

    [ClassInitialize]
    public static void SetUpClass(TestContext testContext)
    {
        int depth = 2;
        _root = Utils.Recur(
            p => Path.GetDirectoryName(p), 
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testContext.DeploymentDirectory), testContext.TestDir)),
            depth);
        _corpusSource = Path.Combine(_root, "HTM.Net.Research.Tests", "NAB", "test_data");
    }

    [TestInitialize]
    public void SetUp()
    {
        _corpus = new Corpus(_corpusSource);
    }

    [TestMethod]
    public void TestGetDataFiles()
    {
        // Test the getDataFiles() function, specifically check if Corpus.dataFiles
        // is a dictionary containing DataFile objects containing DataFrame
        // objects to represent the underlying data.
        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsInstanceOfType(df, typeof(DataFile));
            Assert.IsInstanceOfType(df.Data, typeof(DataFrame));
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value" })));
        }
    }

    [TestMethod]
    public void TestAddColumn()
    {
        //Test the addColumn() function, specifically check if a new column named
        //"test" is added.
        Dictionary<string, List<object>> columnData = new Dictionary<string, List<object>>();
        foreach (var item in _corpus.DataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            int rows = df.Data.GetShape0();

            columnData[relativePath] = Enumerable.Repeat<object>(0, rows).ToList();
        }

        _corpus.AddColumn("test", columnData, write: false);

        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value", "test" })));
        }
    }

    [TestMethod]
    public void TestRemoveColumn()
    {
        // Test the removeColumn() function, specifically check if an added column
        // named "test" is removed.
        Dictionary<string, List<object>> columnData = new Dictionary<string, List<object>>();
        foreach (var item in _corpus.DataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            int rows = df.Data.GetShape0();

            columnData[relativePath] = Enumerable.Repeat<object>(0, rows).ToList();
        }

        _corpus.AddColumn("test", columnData, write: false);

        _corpus.RemoveColumn("test", write: false);

        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value" })));
        }
    }

    [TestMethod]
    public void CheckLeastSquares()
    {
        bool result1 = Research.NAB.Detectors.Skyline.Algorithms.LeastSquares(new List<(DateTime timestamp, double value)>
        {
            (DateTime.Now.Date.AddDays(0), -1),
            (DateTime.Now.Date.AddDays(1), 0.2),
            (DateTime.Now.Date.AddDays(2), 0.9),
            (DateTime.Now.Date.AddDays(3), 2.1),
        }, true);

        Assert.AreEqual(true, result1);
    }

    [TestMethod]
    public void TestCopy()
    {
        // Test the copy() function, specifically check if it copies the whole Corpus
        // to another directory and that the copied Corpus is the exact same as the
        // original.
        string copyLocation = Path.Combine(@"C:\temp", "testABC");
        try
        {
            var copyCorpus = _corpus.Copy(copyLocation);

            foreach (var relativePaths in _corpus.DataFiles.Keys.Zip(copyCorpus.DataFiles.Keys))
            {
                CollectionAssert.Contains(copyCorpus.DataFiles.Keys.ToList(), relativePaths.First);
                CollectionAssert.Contains(_corpus.DataFiles.Keys.ToList(), relativePaths.Second);

                Assert.IsTrue((_corpus.DataFiles[relativePaths.First].Data == copyCorpus.DataFiles[relativePaths.Second].Data));
            }
        }
        finally
        {
            Directory.Delete(copyLocation, true);
        }
    }

    /*[TestMethod]
    public void TestAddDataSet()
    {
        // Test the addDataSet() function, specifically check if it adds a new
        // data file in the correct location in directory and into the dataFiles
        // attribute.
        string copyLocation = Path.Combine(Path.GetTempPath(), "test");
        nab.Corpus.Corpus copyCorpus = Corpus.Copy(copyLocation);

        foreach (var item in Corpus.dataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            string newPath = relativePath + "_copy";
            copyCorpus.AddDataSet(newPath, df.Copy());

            Assert.IsTrue((copyCorpus.dataFiles[newPath].data == df.data).all());
        }

        Directory.Delete(copyLocation, true);
    }

    [TestMethod]
    public void TestGetDataSubset()
    {
        // Test the getDataSubset() function, specifically check if it returns only
        // dataFiles with relativePaths that contain the query given.
        string query1 = "realAWSCloudwatch";
        var subset1 = Corpus.GetDataSubset(query1);

        Assert.AreEqual(subset1.Count, 2);
        foreach (var relativePath in subset1.Keys.ToList())
        {
            StringAssert.Contains(relativePath, query1);
        }

        string query2 = "artificialWithAnomaly";
        var subset2 = Corpus.GetDataSubset(query2);

        Assert.AreEqual(subset2.Count, 1);

        foreach (var relativePath in subset2.Keys.ToList())
        {
            StringAssert.Contains(relativePath, query2);
        }
    }*/
}

[TestClass]
public class VirtualCorpusTest
{
    private static string _root;
    private static string _corpusSource;
    private VirtualCorpus _corpus;

    // for convenience, we'll use the same data-path for all tests
    [ClassInitialize]
    public static void SetUpClass(TestContext testContext)
    {
        int depth = 2;
        _root = Utils.Recur(
            p => Path.GetDirectoryName(p),
            Path.GetFullPath(Path.Combine(Path.GetDirectoryName(testContext.DeploymentDirectory), testContext.TestDir)),
            depth);
        _corpusSource = Path.Combine(_root, "HTM.Net.Research.Tests", "NAB", "test_data");
    }

    [TestInitialize]
    public void SetUp()
    {
        // Create the virtual datafiles

        var filePaths = Directory.GetFiles(_corpusSource, "*.csv", SearchOption.AllDirectories);
        var dataSets = filePaths
            .Where(f => f.EndsWith(".csv"))
            .Select(path => new DataFile(path))
            .ToList();

        var dataFiles = dataSets.Select(d =>
            new VirtualDataFile(d.FileName, d.Data.IterateColumns().ToDictionary(k => k.Key, v => v.Value)))
            .ToArray();

        _corpus = new VirtualCorpus("dataSource", dataFiles);
    }

    [TestMethod]
    public void TestGetDataFiles()
    {
        // Test the getDataFiles() function, specifically check if Corpus.dataFiles
        // is a dictionary containing DataFile objects containing DataFrame
        // objects to represent the underlying data.
        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsInstanceOfType(df, typeof(VirtualDataFile));
            Assert.IsInstanceOfType(df.Data, typeof(DataFrame));
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value" })));
        }
    }

    [TestMethod]
    public void TestAddColumn()
    {
        //Test the addColumn() function, specifically check if a new column named
        //"test" is added.
        Dictionary<string, List<object>> columnData = new Dictionary<string, List<object>>();
        foreach (var item in _corpus.DataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            int rows = df.Data.GetShape0();

            columnData[relativePath] = Enumerable.Repeat<object>(0, rows).ToList();
        }

        _corpus.AddColumn("test", columnData, write: false);

        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value", "test" })));
        }
    }

    [TestMethod]
    public void TestRemoveColumn()
    {
        // Test the removeColumn() function, specifically check if an added column
        // named "test" is removed.
        Dictionary<string, List<object>> columnData = new Dictionary<string, List<object>>();
        foreach (var item in _corpus.DataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            int rows = df.Data.GetShape0();

            columnData[relativePath] = Enumerable.Repeat<object>(0, rows).ToList();
        }

        _corpus.AddColumn("test", columnData, write: false);

        _corpus.RemoveColumn("test", write: false);

        foreach (var df in _corpus.DataFiles.Values.ToList())
        {
            Assert.IsTrue(new HashSet<string>(df.Data.ColumnNames).SetEquals(new HashSet<string>(new[] { "timestamp", "value" })));
        }
    }

    [TestMethod]
    public void TestCopy()
    {
        // Test the copy() function, specifically check if it copies the whole Corpus
        // to another directory and that the copied Corpus is the exact same as the
        // original.
        string copyLocation = Path.Combine(Path.GetTempPath(), "test");
        
        ICorpus copyCorpus = _corpus.Copy(copyLocation);

        foreach (var relativePath in _corpus.DataFiles.Keys.ToList())
        {
            CollectionAssert.Contains(copyCorpus.DataFiles.Keys.ToList(), relativePath);

            Assert.IsTrue((_corpus.DataFiles[relativePath].Data == copyCorpus.DataFiles[relativePath].Data));
        }
    }

    /*[TestMethod]
    public void TestAddDataSet()
    {
        // Test the addDataSet() function, specifically check if it adds a new
        // data file in the correct location in directory and into the dataFiles
        // attribute.
        string copyLocation = Path.Combine(Path.GetTempPath(), "test");
        nab.Corpus.Corpus copyCorpus = Corpus.Copy(copyLocation);

        foreach (var item in Corpus.dataFiles)
        {
            var relativePath = item.Key;
            var df = item.Value;
            string newPath = relativePath + "_copy";
            copyCorpus.AddDataSet(newPath, df.Copy());

            Assert.IsTrue((copyCorpus.dataFiles[newPath].data == df.data).all());
        }

        Directory.Delete(copyLocation, true);
    }

    [TestMethod]
    public void TestGetDataSubset()
    {
        // Test the getDataSubset() function, specifically check if it returns only
        // dataFiles with relativePaths that contain the query given.
        string query1 = "realAWSCloudwatch";
        var subset1 = Corpus.GetDataSubset(query1);

        Assert.AreEqual(subset1.Count, 2);
        foreach (var relativePath in subset1.Keys.ToList())
        {
            StringAssert.Contains(relativePath, query1);
        }

        string query2 = "artificialWithAnomaly";
        var subset2 = Corpus.GetDataSubset(query2);

        Assert.AreEqual(subset2.Count, 1);

        foreach (var relativePath in subset2.Keys.ToList())
        {
            StringAssert.Contains(relativePath, query2);
        }
    }*/
}