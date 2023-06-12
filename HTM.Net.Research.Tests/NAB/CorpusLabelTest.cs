using System.Collections.Generic;
using System.IO;
using System;
using System.Collections;
using System.Globalization;
using System.Linq;
using HTM.Net.Research.NAB;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static log4net.Appender.RollingFileAppender;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class CorpusLabelTest
{
    [TestMethod]
    public void TestGetProbationPeriod()
    {
        int[] fileLengths = { 1000, 4032, 5000, 15000 };
        int[] expectedIndices = { 150, 604, 750, 750 };

        foreach ((int length, int idx)  in fileLengths.Zip(expectedIndices))
        {
            int probationIndex = Utils.getProbationPeriod(0.15f, length);
            Assert.AreEqual(probationIndex, idx, $"Expected probation index of {idx} got {probationIndex}.");
        }
    }

    private string _tempDir;
    private string _tempCorpusPath;
    private string _tempCorpusLabelPath;

    [TestInitialize]
    public void SetUp()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "test", Guid.NewGuid().ToString());
        _tempCorpusPath = Path.Combine(_tempDir, "data");
        _tempCorpusLabelPath = Path.Combine(_tempDir, "labels", "label.json");

        Directory.CreateDirectory(_tempCorpusPath);
        Directory.CreateDirectory(Path.GetDirectoryName(_tempCorpusLabelPath));
    }

    [TestCleanup]
    public void TearDown()
    {
        Directory.Delete(_tempDir, true);
    }

    [TestMethod]
    public void TestWindowTimestampsNotInDataFileThrowsError()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2014-01-01"), null, 1) }
        });

        var windows = new List<List<string>>
        {
            new List<string> { "2015-01-01", "2015-01-01" }
        };

        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { "test_data_file.csv", data } });
        TestUtils.WriteCorpusLabel(_tempCorpusLabelPath, new Dictionary<string, List<List<string>>> { { "test_data_file.csv", windows } });

        var corpus = new Corpus(_tempCorpusPath);

        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            var corpusLabel = new CorpusLabel(_tempCorpusLabelPath, corpus);
        });
    }

    [TestMethod]
    public void TestWindowTimestampsNonChronologicalThrowsError()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2014-01-01"), TimeSpan.FromMinutes(5), 10) }
        });

        var windows = new List<List<string>>
        {
            new List<string> { DateTime.Parse("2014-01-01 00:45").ToString("s"), DateTime.Parse("2014-01-01 00:00").ToString("s") },
            new List<string> { DateTime.Parse("2014-01-01 10:15").ToString("s"), DateTime.Parse("2014-01-01 11:15").ToString("s") }
        };

        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { "test_data_file.csv", data } });
        TestUtils.WriteCorpusLabel(_tempCorpusLabelPath, new Dictionary<string, List<List<string>>> { { "test_data_file.csv", windows } });

        var corpus = new Corpus(_tempCorpusPath);

        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            var corpusLabel = new CorpusLabel(_tempCorpusLabelPath, corpus);
        });
    }

    [TestMethod]
    public void TestRowsLabeledAnomalousWithinAWindow()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2014-01-01"), TimeSpan.FromMinutes(5), 10) }
        });

        var windows = new List<List<string>>
        {
            new List<string> { DateTime.Parse("2014-01-01 00:15:00").ToString("s"), DateTime.Parse("2014-01-01 00:30:00").ToString("s") }
        };

        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { "test_data_file.csv", data } });
        TestUtils.WriteCorpusLabel(_tempCorpusLabelPath, new Dictionary<string, List<List<string>>> { { "test_data_file.csv", windows } });

        var corpus = new Corpus(_tempCorpusPath);
        var corpusLabel = new CorpusLabel(_tempCorpusLabelPath, corpus);

        foreach (var kvp in corpusLabel.Labels)
        {
            var relativePath = kvp.Key;
            var lab = kvp.Value;
            var windows1 = corpusLabel.Windows[relativePath];

            foreach (var row in lab.Where("label", l => (int)l == 1).IterateRows())
            {
                Assert.IsTrue(windows1.All(w => w.start <= DateTime.Parse((string)row["timestamp"]) && DateTime.Parse((string)row["timestamp"]) <= w.end),
                    $"The label at {row["timestamp"]} of file {relativePath} is not within a label window");
            }
        }
    }

    [TestMethod]
    public void TestNonexistentDatafileForLabelsThrowsError()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2014-01-01"), TimeSpan.FromMinutes(5), 10) }
        });

        var windows = new List<List<string>>
        {
            new List<string> { DateTime.Parse("2014-01-01 00:15").ToString("s"), DateTime.Parse("2014-01-01 00:30").ToString("s") }
        };

        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { "test_data_file.csv", data } });
        TestUtils.WriteCorpusLabel(_tempCorpusLabelPath, new Dictionary<string, List<List<string>>>
        {
            { "test_data_file.csv", windows },
            { "non_existent_data_file.csv", windows }
        });

        var corpus = new Corpus(_tempCorpusPath);

        Assert.ThrowsException<KeyNotFoundException>(() =>
        {
            var corpusLabel = new CorpusLabel(_tempCorpusLabelPath, corpus);
        });
    }

    [TestMethod]
    public void TestGetLabels()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2014-01-01"), TimeSpan.FromMinutes(5), 10) }
        });

        var windows = new List<List<string>>
        {
            new List<string> { DateTime.Parse("2014-01-01 00:00").ToString(DateTimeFormatInfo.InvariantInfo), DateTime.Parse("2014-01-01 00:10").ToString(DateTimeFormatInfo.InvariantInfo) },
            new List<string> { DateTime.Parse("2014-01-01 00:10").ToString(DateTimeFormatInfo.InvariantInfo), DateTime.Parse("2014-01-01 00:15").ToString(DateTimeFormatInfo.InvariantInfo) }
        };

        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { "test_data_file.csv", data } });
        TestUtils.WriteCorpusLabel(_tempCorpusLabelPath, new Dictionary<string, List<List<string>>> { { "test_data_file.csv", windows } });

        var corpus = new Corpus(_tempCorpusPath);
        var corpusLabel = new CorpusLabel(_tempCorpusLabelPath, corpus);

        foreach (var kvp in corpusLabel.Labels)
        {
            var relativePath = kvp.Key;
            var l = kvp.Value;
            var windows1 = corpusLabel.Windows[relativePath];

            foreach (var row in corpusLabel.Labels["test_data_file.csv"].IterateRows())
            {
                var t = DateTime.Parse((string)row["timestamp"]);
                var lab = (int)row["label"];
                foreach (var w in windows1)
                {
                    if (w.start <= t && t <= w.end)
                    {
                        Assert.AreEqual(1, lab, $"Incorrect label value for timestamp {t}");
                    }
                }
            }
        }
    }

    [TestMethod]
    public void TestRedundantTimestampsRaiseException()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2015-01-01"), TimeSpan.FromDays(1), 365) }
        });
        var dataFileName = "test_data_file.csv";
        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { dataFileName, data } });

        var labels = new List<string>
        {
            DateTime.Parse("2015-12-25 00:00:00").ToString("s"),
            DateTime.Parse("2015-12-26 00:00:00").ToString("s"),
            DateTime.Parse("2015-12-31 00:00:00").ToString("s"),
        };
        var labelsDir = _tempCorpusLabelPath.Replace("\\label.json", "\\raw\\label.json");
        TestUtils.WriteCorpusLabel(labelsDir, new Dictionary<string, List<string>> { { dataFileName, labels } });

        var corpus = new Corpus(_tempCorpusPath);
        var labDir = labelsDir.Replace("\\label.json", "");
        var labelCombiner = new LabelCombiner(labDir, corpus, 0.5f, 0.10f, 0.15f, 0);

        Assert.ThrowsException<InvalidOperationException>(() =>
        {
            labelCombiner.Combine();
        });
    }

    [TestMethod]
    public void TestBucketMerge()
    {
        var data = new DataFrame(new Dictionary<string, IList>
        {
            { "timestamp", TestUtils.GenerateTimestamps(DateTime.Parse("2015-12-01"), TimeSpan.FromDays(1), 31) }
        });
        var dataFileName = "test_data_file.csv";
        TestUtils.WriteCorpus(_tempCorpusPath, new Dictionary<string, DataFrame> { { dataFileName, data } });

        var rawLabels = new List<List<string>>
        {
            new List<string> { DateTime.Parse("2015-12-24 00:00:00").ToString("s"), DateTime.Parse("2015-12-31 00:00:00").ToString("s") },
            new List<string> { DateTime.Parse("2015-12-01 00:00:00").ToString("s"), DateTime.Parse("2015-12-25 00:00:00").ToString("s"), DateTime.Parse("2015-12-31 00:00:00").ToString("s") },
            new List<string> { DateTime.Parse("2015-12-25 00:00:00").ToString("s") }
        };

        string labelsPath = null;
        int i;
        for (i = 0; i < rawLabels.Count; i++)
        {
            labelsPath = _tempCorpusLabelPath.Replace(
                Path.DirectorySeparatorChar + "label.json", Path.DirectorySeparatorChar + "raw" + Path.DirectorySeparatorChar + $"label{i}.json");
            TestUtils.WriteCorpusLabel(labelsPath, new Dictionary<string, List<string>> { { dataFileName, rawLabels[i] } });
        }
        var labelsDir = labelsPath.Replace(Path.DirectorySeparatorChar + $"label{i-1}.json", "");

        var corpus = new Corpus(_tempCorpusPath);
        var labelCombiner = new LabelCombiner(labelsDir, corpus, 0.5f, 0.10f, 0.15f, 0);
        labelCombiner.GetRawLabels();
        var (labelTimestamps, _) = labelCombiner.CombineLabels();

        var expectedLabels = new List<string> { "2015-12-25T00:00:00", "2015-12-31T00:00:00" };
        CollectionAssert.AreEqual(expectedLabels, labelTimestamps[dataFileName],
            "The combined labels did not bucket and merge as expected.");
    }
}