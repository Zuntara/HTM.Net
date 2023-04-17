using System;
using System.Collections.Generic;
using System.Linq;

using HTM.Net.Algorithms;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;

namespace HTM.Net.Research.Tests.Examples.Sine;

public interface INetworkBuilder
{
    INetworkBuilder AddRegion(string name, Action<IRegionBuilder> regionBuilder, bool connectToPrev = false);

    Network.Network Build();
}

public interface IRegionBuilder
{
    IRegionBuilder AddLayer(string name, LayerMask layerBuilder,
        bool connectToPrev = false, bool autoClassify = false, ISensor sensor = null, Parameters p = null);
}

public class NetworkBuilder : INetworkBuilder
{
    private string Name { get; }
    private Parameters Parameters { get; }

    private List<Region> _regions = new List<Region>();
    // from, to
    private Dictionary<Region, Region> _regionConnections = new Dictionary<Region, Region>();

    public static INetworkBuilder Create(string name, Parameters p)
    {
        return new NetworkBuilder(name, p);
    }

    private NetworkBuilder(string name, Parameters p)
    {
        Name = name;
        Parameters = p;
    }

    public INetworkBuilder AddRegion(string name, Action<IRegionBuilder> regionBuilder, bool connectToPrev = false)
    {
        RegionBuilder builder = new RegionBuilder(name, Parameters);
        regionBuilder(builder);

        var region = builder.Build();

        if (connectToPrev)
        {
            var prevRegion = _regions.Last();
            _regionConnections.Add(region, prevRegion);
        }
            
        _regions.Add(region);

        return this;
    }

    public Network.Network Build()
    {
        var network = Network.Network.Create(Name, Parameters);
        foreach (var region in _regions)
        {
            network.Add(region);
        }

        foreach (var connection in _regionConnections)
        {
            network.Connect(connection.Value.GetName(), connection.Key.GetName());
        }

        return network;
    }

    public class RegionBuilder : IRegionBuilder
    {
        private string Name { get; }
        private Parameters GlobalParameters { get; }

        private List<ILayer> _layers = new List<ILayer>();
        // from, to
        private Dictionary<ILayer, ILayer> _layerConnections = new Dictionary<ILayer, ILayer>();

        public RegionBuilder(string name, Parameters globalParameters)
        {
            Name = name;
            GlobalParameters = globalParameters;
        }

        public IRegionBuilder AddLayer(string name, LayerMask mask, bool connectToPrev = false, bool autoClassify = false, ISensor sensor = null,
            Parameters p = null)
        {
            var layer = Network.Network.CreateLayer(name, p ?? GlobalParameters);

            if (autoClassify)
            {
                layer.AlterParameter(Parameters.KEY.AUTO_CLASSIFY, true);
            }

            if (mask.HasFlag(LayerMask.AnomalyComputer))
            {
                layer.Add(Anomaly.Create());
            }
            if (mask.HasFlag(LayerMask.TemporalMemory))
            {
                layer.Add(new TemporalMemory());
            }
            if (mask.HasFlag(LayerMask.SpatialPooler))
            {
                layer.Add(new Algorithms.SpatialPooler());
            }

            if (sensor != null)
            {
                layer.Add(sensor);
            }

            if (connectToPrev)
            {
                var prevLayer = _layers.Last();
                _layerConnections.Add(layer, prevLayer);
            }

            _layers.Add(layer);

            return this;
        }

        public Region Build()
        {
            var region = Network.Network.CreateRegion(Name);

            foreach (var layer in _layers)
            {
                region.Add(layer);
            }

            foreach (var connection in _layerConnections)
            {
                region.Connect(connection.Value.GetName(), connection.Key.GetName());
            }

            return region;
        }
    }
}