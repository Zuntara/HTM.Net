using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices.ComTypes;
using HTM.Net.Research.NAB;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class NormalizationTest
{
    private List<string> _tmpDirs = new List<string>();
    private List<string> resultsHeaders = new List<string> { "detector", "profile", "score" };

    [TestCleanup]
    public void Cleanup()
    {
        foreach (var tmpDir in _tmpDirs)
        {
            Directory.Delete(tmpDir, true);
        }
    }

    private string CreateTemporaryResultsDir()
    {
        var tmpResultsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
        Directory.CreateDirectory(tmpResultsDir);
        _tmpDirs.Add(tmpResultsDir);
        return tmpResultsDir;
    }

    [TestMethod]
    public void TestNullScoreLoading()
    {
        var testRunner = new Runner<Corpus, CorpusLabel>(null, "", null, null, null);

        // Should fail due to resultsDir/null not being a directory.
        try
        {
            testRunner.Normalize();
            Assert.Fail("Should thow IOException");
        }
        catch (IOException)
        {
            // Handle the exception
        }

        var tmpResultsDir = CreateTemporaryResultsDir();
        Directory.CreateDirectory(Path.Combine(tmpResultsDir, "null"));
        var testRunner2 = CreateRunner(tmpResultsDir, "standard");

        // Should fail due to resultsDir/null being empty.
        try
        {
            testRunner2.Normalize();
            Assert.Fail("Should thow IOException");
        }
        catch (FileNotFoundException)
        {
            // Handle the exception
        }
    }

    [TestMethod]
    public void TestResultsUpdate()
    {
        var tmpResultsDir = CreateTemporaryResultsDir();
        Directory.CreateDirectory(Path.Combine(tmpResultsDir, "null"));
        Directory.CreateDirectory(Path.Combine(tmpResultsDir, "expose"));
        var finalResults = Path.Combine(tmpResultsDir, "final_results.json");

        Assert.IsFalse(File.Exists(finalResults), "final_results.json was not created during normalization.");
        
        // Create the null detector score file
        var nullFile = Path.Combine("null", "null_standard_scores.csv");
        var nullRow = new List<string> { "null", "standard", "0.0" };
        var nullData = new List<List<string>> { resultsHeaders, nullRow };
        CreateCSV(tmpResultsDir, nullFile, nullData);

        // Create the fake results file
        var fakeFile = Path.Combine("expose", "expose_standard_scores.csv");
        var fakeRow = new List<string> { "expose", "standard", "1.0" };
        var fakeData = new List<List<string>> { resultsHeaders, fakeRow };
        CreateCSV(tmpResultsDir, fakeFile, fakeData);

        var testRunner = CreateRunner(tmpResultsDir, "standard", "expose");
        testRunner.Normalize();

        Assert.IsTrue(File.Exists(finalResults), "final_results.json was not created during normalization.");
    }

    [TestMethod]
    public void TestScoreNormalization()
    {
        var tmpResultsDir = CreateTemporaryResultsDir();
        Directory.CreateDirectory(Path.Combine(tmpResultsDir, "null"));
        Directory.CreateDirectory(Path.Combine(tmpResultsDir, "expose"));
        var finalResults = Path.Combine(tmpResultsDir, "final_results.json");

        Assert.IsFalse(File.Exists(finalResults), "final_results.json was not created during normalization.");

        // Create the null detector score file
        var nullFile = Path.Combine("null", "null_standard_scores.csv");
        var nullRow = new List<string> { "null", "standard", "-5.0" };
        var nullData = new List<List<string>> { resultsHeaders, nullRow };
        CreateCSV(tmpResultsDir, nullFile, nullData);

        // Create the fake results file
        var fakeFile = Path.Combine("expose", "expose_standard_scores.csv");
        var fakeRow = new List<string> { "expose", "standard", "2.0" };
        var fakeData = new List<List<string>> { resultsHeaders, fakeRow };
        CreateCSV(tmpResultsDir, fakeFile, fakeData);

        var testRunner = CreateRunner(tmpResultsDir, "standard", "expose");
        testRunner.Normalize();

        Assert.IsTrue(File.Exists(finalResults), "final_results.json was not created during normalization.");

        // Check that scores have been properly normalized.
        using (var finalResultsFile = File.OpenText(finalResults))
        {
            var json = finalResultsFile.ReadToEnd();
            var resultsDict = JsonConvert.DeserializeObject<Dictionary<Detector, Map<string, Map<string, double>>>>(json);
            var score = resultsDict[Detector.Expose]["standard"]["score"];

            Assert.AreEqual(70.0, score, $"Normalized score of {score} is not the expected 70.0");
        }
    }

    private void CreateCSV(string parentDir, string fileName, List<List<string>> data)
    {
        var filePath = Path.Combine(parentDir, fileName);

        using (var writer = new StreamWriter(filePath))
        {
            foreach (var line in data)
            {
                writer.WriteLine(string.Join(",", line));
            }
            writer.Flush();
        }
    }

    private Runner<Corpus, CorpusLabel> CreateRunner(string resultsDir, string profileName, string resultsName = null)
    {
        var root = Directory.GetParent(Directory.GetCurrentDirectory()).Parent.FullName;
        var labelPath = Path.GetFullPath(Path.Combine(root, "..", "NAB/test_labels/labels.json"));

        var testRunner = new Runner<Corpus, CorpusLabel>(null, resultsDir, labelPath, null, null);

        testRunner.Profiles = new Dictionary<string, Dictionary<string, CostMatrix>>
        {
            [profileName] = new Dictionary<string, CostMatrix>
            {
                ["CostMatrix"] = CostMatrix.FromDictionary(new Dictionary<string, double>
                {
                    ["tpWeight"] = 1.0
                })
            }
        };

        if (resultsName != null)
        {
            var resultsFile = $"{resultsName}_{profileName}_scores.csv";
            var resultsFilePath = Path.Combine(resultsDir, resultsName, resultsFile);
            testRunner.ResultsFiles = new List<string> { resultsFilePath };
        }

        return testRunner;
    }
}