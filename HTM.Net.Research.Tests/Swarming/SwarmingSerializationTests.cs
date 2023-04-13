using System;
using System.Collections.Generic;
using System.ComponentModel;
using HTM.Net.Algorithms;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Swarming.HyperSearch.Variables;
using HTM.Net.Util;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Newtonsoft.Json;
using static HTM.Net.Algorithms.Anomaly;

namespace HTM.Net.Research.Tests.Swarming;

[TestClass]
public class SwarmingSerializationTests
{
    [TestMethod]
    public void TestExperimentParametersSerialization()
    {
        ExperimentParameters ep = ExperimentParameters.Default();
        ep.SetNumActiveColumnsPerInhArea(101);
        ep.SetColumnDimensions(new []{ 1024 });
        ep.SetInputDimensions(new []{ 512 });

        Console.WriteLine();
        Console.WriteLine(ep);
        Console.WriteLine();

        var str = JsonConvert.SerializeObject(ep, Formatting.Indented);

        Console.WriteLine($"{str}");

        var ep2 = JsonConvert.DeserializeObject<ExperimentParameters>(str);

        Assert.IsNotNull(ep2);

        var s1 = ep.ToString().Trim();
        var s2 = ep2.ToString().Trim();

        Assert.AreEqual(ep, ep2, "Expected equal parameters");
        Assert.AreEqual(s1.Length, s2.Length, "Expected equal length");
    }

    [TestMethod]
    public void TestKeySerialization()
    {
        Parameters.KEY key = Parameters.KEY.INPUT_DIMENSIONS;

        var str = JsonConvert.SerializeObject(key);

        Console.WriteLine($"KEY: {str}");

        var key2 = JsonConvert.DeserializeObject<Parameters.KEY>(str);

        Assert.IsNotNull(key2);
        Assert.AreEqual(key, key2, "Expected equal keys");
    }

    [TestMethod]
    public void TestKeyMapSerialization()
    {
        Parameters.ParametersMap map = new Parameters.ParametersMap();

        Parameters.KEY key1 = Parameters.KEY.INPUT_DIMENSIONS;
        Parameters.KEY key2 = Parameters.KEY.CELLS_PER_COLUMN;

        map.Add(key1, new[] { 10, 20 });
        map.Add(key2, 6);

        Console.WriteLine($"Before serialize: {map}");

        var str = JsonConvert.SerializeObject(map);

        Console.WriteLine($"KEY map: {str}");

        var map2 = JsonConvert.DeserializeObject<Parameters.ParametersMap>(str);

        Console.WriteLine($"After serialize: {map2}");

        Assert.IsNotNull(map2);
        Assert.AreEqual(map, map2, "Expected equal keys");
    }

    [TestMethod]
    public void TestPermuteIntSerialization()
    {
        PermuteInt pInt = new PermuteInt(1, 10, 2, 1.01, 1.02, 1.03);

        Console.WriteLine($"Before serialize: {pInt}");
        var str = JsonConvert.SerializeObject(pInt, Formatting.Indented);

        Console.WriteLine(str);

        var pIntAfter = JsonConvert.DeserializeObject<PermuteInt>(str);
        Console.WriteLine($"After serialize: {pIntAfter}");
        Assert.IsNotNull(pIntAfter);

        Assert.AreEqual(pInt.ToString(), pIntAfter.ToString(), "Expected equal structures");
    }

    [TestMethod]
    public void TestPermuteIntInMapSerialization()
    {
        Parameters.ParametersMap map = new Parameters.ParametersMap();

        PermuteInt pInt = new PermuteInt(1, 10, 2, 1.01, 1.02, 1.03);

        map.Add(Parameters.KEY.MIN_THRESHOLD, pInt);

        Console.WriteLine($"Before serialize: {map}");
        var str = JsonConvert.SerializeObject(map, Formatting.Indented);

        Console.WriteLine(str);

        var mapAfter = JsonConvert.DeserializeObject<Parameters.ParametersMap>(str);
        Console.WriteLine($"After serialize: {mapAfter}");
        Assert.IsNotNull(mapAfter);

        Assert.AreEqual(map, mapAfter, "Expected equal structures");
    }

    [TestMethod]
    public void TestAnomalyParamsSerialization()
    {
        Statistic distribution = new Statistic(12, 10, 2);
        AveragedAnomalyRecordList records = new AveragedAnomalyRecordList(
            new List<Sample>()
            {
                new Sample(DateTime.Now, 10, 0)
            }, new List<double>{ 10.0 }, 0);
        double[] likelihoods = new double[records.AveragedRecords.Count];
        likelihoods[0] = 0.5;
        int averagingWindow = 10;
        int len = likelihoods.Length;

        AnomalyLikelihood.AnomalyParams @params = new AnomalyLikelihood.AnomalyParams(
            distribution,
            new MovingAverage(records.HistoricalValues, records.Total, averagingWindow),
            len > 0
                ? Arrays.CopyOfRange(likelihoods, len - Math.Min(averagingWindow, len), len)
                : Array.Empty<double>());

        var str = JsonConvert.SerializeObject(@params, Formatting.Indented);
        Console.WriteLine(str);

        var mapAfter = JsonConvert.DeserializeObject<AnomalyLikelihood.AnomalyParams>(str);
        Console.WriteLine($"After serialize: {mapAfter}");
        Assert.IsNotNull(mapAfter);

        Assert.AreEqual(@params, mapAfter, "Expected equal structures");
    }
}