using System.Collections.Generic;
using System;
using System.Linq;
using HTM.Net.Research.Taurus.MetricCollectors;

namespace HTM.Net.Research.Taurus.HtmEngine.Adapters;

public interface IDataSourceAdapter
{
    string ImportModel(ModelSpec modelSpec);
    /// <summary>
    /// Start monitoring a metric; perform model creation logic specific to custom metrics.
    /// Start the model if possible: this will happen if modelParams includes both
    /// "min" and "max" or there is enough data to estimate them.
    /// </summary>
    /// <param name="modelSpec">model specification for HTM model; per ``model_spec_schema.json`` 
    /// with the ``metricSpec`` property per ``custom_metric_spec_schema.json``</param>
    /// <remarks>
    /// 1st variant: `uid` is the unique id of an existing metric;
    /// # TODO: it would be preferable to punt on this variant, and refer
    /// #  to custom metric by name in modelSpec for consistency with
    /// # import/export. Web GUI uses metric name; some tests use this variant,
    /// # though.
    /// {
    ///     "datasource": "custom",
    ///     "metricSpec": {
    ///         "uid": "4a833e2294494b4fbc5004e03bad45b6",
    ///         "unit": "Count",  # optional
    ///         "resource": "prod.web1",  # optional
    ///         "userInfo": {"symbol": "<TICKER_SYMBOL>"} # optional
    ///     },
    ///     # Optional model params
    ///     "modelParams": {
    ///         "min": min-value,  # optional
    ///         "max": max-value  # optional
    ///     }
    /// }
    ///  ::
    ///  2nd variant: `metric` is the unique name of the metric; a new custom
    ///  metric row will be created with this name, if it doesn't exit
    ///    {
    ///      "datasource": "custom",
    ///      "metricSpec": {
    ///        "metric": "prod.web.14.memory",
    ///        "unit": "Count",  # optional
    ///        "resource": "prod.web1",  # optional
    ///        "userInfo": {"symbol": "<TICKER_SYMBOL>"} # optional
    ///      },
    ///      # Optional model params
    ///      "modelParams": {
    ///        "min": min-value,  # optional
    ///        "max": max-value  # optional
    ///      }
    ///    }
    /// </remarks>
    /// <returns>datasource-specific unique model identifier</returns>
    string MonitorMetric(ModelSpec modelSpec);
    /// <summary>
    /// Unmonitor a metric
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    void UnmonitorMetric(string metricId);

    /// <summary>
    /// Start a model that is PENDING_DATA, creating the OPF/CLA model
    /// NOTE: used by MetricStreamer when model is in PENDING_DATA state and
    /// sufficient data samples are available to get statistics and complete model
    /// creation.
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    void ActivateModel(string metricId);
    string Datasource { get; }
    /// <summary>
    /// Create scalar HTM metric if it doesn't exist
    /// </summary>
    /// <param name="metricId"></param>
    /// <returns></returns>
    string CreateMetric(string metricId);
}

public static class DataAdapterFactory
{
    private static List<IDataSourceAdapter> _adapterRegistry = new List<IDataSourceAdapter>();
    /// <summary>
    /// Factory for Datasource adapters
    /// </summary>
    /// <param name="dataSource">datasource (e.g., "cloudwatch", "custom", ...)</param>
    /// <returns></returns>
    public static IDataSourceAdapter CreateDatasourceAdapter(string dataSource)
    {
        var adapter = _adapterRegistry.SingleOrDefault(a => a.Datasource.Equals(dataSource, StringComparison.InvariantCultureIgnoreCase));
        if (adapter == null && dataSource == "custom")
        {
            return new CustomDatasourceAdapter();
        }
        if (adapter == null)
        {
            throw new InvalidOperationException("Adapter not found");
        }
        return adapter;
    }

    public static void RegisterDatasourceAdapter(IDataSourceAdapter clientCls)
    {
        if (!_adapterRegistry.Contains(clientCls))
        {
            _adapterRegistry.Add(clientCls);
        }
    }

    public static void ClearRegistrations()
    {
        _adapterRegistry.Clear();
    }
}