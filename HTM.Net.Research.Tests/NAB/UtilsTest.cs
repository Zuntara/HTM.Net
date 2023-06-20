using HTM.Net.Research.NAB;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;

namespace HTM.Net.Research.Tests.NAB;

[TestClass]
public class ThresholdsTest
{
    private string _thresholdsPath;

    [TestInitialize]
    public void SetUp()
    {
        var oldThresholds = new Dictionary<Detector, Dictionary<string, Dictionary<string, double>>>
        {
            {
                Detector.LuckyDetector,
                new Dictionary<string, Dictionary<string, double>>
                {
                    {
                        "standard",
                        new Dictionary<string, double>
                        {
                            { "score", 13.0 },
                            { "threshold", 0.7 }
                        }
                    }
                }
            },
            {
                Detector.DeepThought,
                new Dictionary<string, Dictionary<string, double>>
                {
                    {
                        "standard",
                        new Dictionary<string, double>
                        {
                            { "score", 42.0 },
                            { "threshold", 0.9 }
                        }
                    }
                }
            }
        };
        
        var root = Path.GetDirectoryName(Path.GetFullPath(typeof(ThresholdsTest).Assembly.Location));
        _thresholdsPath = Path.Combine(root, "thresholds.json");
        WriteJSON(_thresholdsPath, oldThresholds);
    }

    [TestCleanup]
    public void TearDown()
    {
        if (!string.IsNullOrEmpty(_thresholdsPath) && File.Exists(_thresholdsPath))
        {
            File.Delete(_thresholdsPath);
        }
    }

    [TestMethod]
    public void TestThresholdUpdateNewDetector()
    {
        var newThresholds = new Dictionary<Detector, Map<string, Map<string, double>>>
        {
            {
                Detector.BadDetector,
                new Map<string, Map<string, double>>
                {
                    {
                        "standard",
                        new Map<string, double>
                        {
                            { "score", -1.0 },
                            { "threshold", 0.5 }
                        }
                    }
                }
            }
        };

        Utils.UpdateThresholds(newThresholds, _thresholdsPath);

        var threshDict = ReadJSON(_thresholdsPath);

        var expectedDict = new Dictionary<Detector, Dictionary<string, Dictionary<string, double>>>
        {
            {
                Detector.LuckyDetector,
                new Dictionary<string, Dictionary<string, double>>
                {
                    {
                        "standard",
                        new Dictionary<string, double>
                        {
                            { "score", 13.0 },
                            { "threshold", 0.7 }
                        }
                    }
                }
            },
            {
                Detector.DeepThought,
                new Dictionary<string, Dictionary<string, double>>
                {
                    {
                        "standard",
                        new Dictionary<string, double>
                        {
                            { "score", 42.0 },
                            { "threshold", 0.9 }
                        }
                    }
                }
            },
            {
                Detector.BadDetector,
                new Dictionary<string, Dictionary<string, double>>
                {
                    {
                        "standard",
                        new Dictionary<string, double>
                        {
                            { "score", -1.0 },
                            { "threshold", 0.5 }
                        }
                    }
                }
            }
        };

        string jsonExepcted = JsonConvert.SerializeObject(expectedDict, Formatting.Indented);
        string jsonActual = JsonConvert.SerializeObject(threshDict, Formatting.Indented);

        Assert.AreEqual(jsonExepcted, jsonActual, "The updated threshold dict does not match the expected dict.");
    }

    [TestMethod]
    public void TestThresholdUpdateDifferentScores()
    {
        var newThresholds = new Dictionary<Detector, Map<string, Map<string, double>>>
        {
            {
                Detector.LuckyDetector,
                new Map<string, Map<string, double>>
                {
                    {
                        "standard",
                        new Map<string, double>
                        {
                            { "score", 23.0 },
                            { "threshold", 0.77 }
                        }
                    }
                }
            },
            {
                Detector.DeepThought,
                new Map<string, Map<string, double>>
                {
                    {
                        "standard",
                        new Map<string, double>
                        {
                            { "score", 32.0 },
                            { "threshold", 0.99 }
                        }
                    }
                }
            }
        };

        Utils.UpdateThresholds(newThresholds, _thresholdsPath);

        var threshDict = ReadJSON(_thresholdsPath);

        string jsonExepcted = JsonConvert.SerializeObject(newThresholds, Formatting.Indented);
        string jsonActual = JsonConvert.SerializeObject(threshDict, Formatting.Indented);

        Assert.AreEqual(jsonExepcted, jsonActual, "The updated threshold dict does not match the expected dict.");
    }

    private Dictionary<string, Dictionary<string, Dictionary<string, double>>> ReadJSON(string filePath)
    {
        var json = File.ReadAllText(filePath);
        var dataDict = JsonConvert.DeserializeObject<Dictionary<string, Dictionary<string, Dictionary<string, double>>>>(json);
        return dataDict;
    }

    private void WriteJSON(string filePath, object data)
    {
        var json = JsonConvert.SerializeObject(data, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }
}