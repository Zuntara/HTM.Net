using System;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Research.Taurus.HtmEngine.Runtime;
using HTM.Net.Research.Taurus.MetricCollectors;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using ModelParams = HTM.Net.Research.Taurus.HtmEngine.Runtime.ModelParams;

namespace HTM.Net.Research.Taurus.HtmEngine.Adapters;

/// <summary>
/// https://github.com/numenta/numenta-apps/blob/master/htmengine/htmengine/adapters/datasource/custom/__init__.py
/// </summary>
public class CustomDatasourceAdapter : IDataSourceAdapter
{
    private readonly ILog _log = LogManager.GetLogger(typeof(CustomDatasourceAdapter));
    /// <summary>
    /// Minimum records needed before creating a model; assumes 24 hours worth of 5-minute data samples
    /// </summary>
    public const int MODEL_CREATION_RECORD_THRESHOLD = (60 / 5) * 24;

    // Default metric period value to use when it's unknown
    // TODO: Should we use 0 since it's unknown "unknown" or default to 5 min?
    // Consider potential impact on web charts, htm-it-mobile
    public const int DEFAULT_METRIC_PERIOD = 300;  // 300 sec = 5 min

    public string ImportModel(ModelSpec modelSpec)
    {
        throw new NotImplementedException();
    }

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
    public string MonitorMetric(ModelSpec modelSpec)
    {
        var metricSpec = modelSpec.MetricSpec;

        string metricId;
        if (metricSpec.Uid != null)
        {
            // Via metric ID
            metricId = metricSpec.Uid;
            // Convert modelSpec to canonical form
            modelSpec = modelSpec.Clone() as ModelSpec;
            modelSpec.MetricSpec.Uid = null;
            ((CustomMetricSpec)modelSpec.MetricSpec).Metric = RepositoryFactory.Metric.GetMetric(metricId).Name;
        }
        else if (!string.IsNullOrWhiteSpace(metricSpec.Metric))
        {
            // Via metric name
            metricId = CreateMetric(metricSpec.Metric);
        }
        else
        {
            throw new InvalidOperationException(string.Format("Neither uid nor metric name present in metricSpec; modelSpec={0}", modelSpec));
        }

        var modelParams = modelSpec.ModelParams ?? new ModelParams();
        var minVal = modelParams.Min;
        var maxVal = modelParams.Max;
        var minResolution = modelParams.MinResolution;
        if (minVal.HasValue != maxVal.HasValue)
        {
            throw new InvalidOperationException(string.Format("min and max params must both be None or non-None; metric={0}; modelSpec={1}", metricId, modelSpec));
        }

        // Start monitoring
        if (!minVal.HasValue || !maxVal.HasValue)
        {
            minVal = maxVal = null;
        }

        int numDataRows = RepositoryFactory.MetricData.GetMetricDataCount(metricId);
        if (numDataRows > MODEL_CREATION_RECORD_THRESHOLD)
        {
            var mstats = GetMetricStatistics(metricId);
            _log.InfoFormat("MonitorMetric: trigger numDataRows={0}, stats={1}", numDataRows, mstats);
            minVal = mstats.Min;
            maxVal = mstats.Max;
        }

        var stats = new MetricStatistic(min: minVal, max: maxVal, minResolution: minResolution);
        _log.DebugFormat("monitorMetric: metric={0}, stats={1}", metricId, stats);

        var swarmParams = ScalarMetricUtils.GenerateSwarmParams(stats);

        StartMonitoringWithRetries(metricId, modelSpec, swarmParams);

        return metricId;
    }

    /// <summary>
    /// Start a model that is PENDING_DATA, creating the OPF/CLA model
    /// NOTE: used by MetricStreamer when model is in PENDING_DATA state and
    /// sufficient data samples are available to get statistics and complete model
    /// creation.
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    public void ActivateModel(string metricId)
    {
        var metricObj = RepositoryFactory.Metric.GetMetric(metricId);
        if (metricObj.DataSource != Datasource)
        {
            throw new InvalidOperationException(string.Format("activateModel: not an HTM metric={0}; datasource={1}",
                metricId, metricObj.DataSource));
        }
        var stats = GetMetricStatistics(metricId);
        var swarmParams = ScalarMetricUtils.GenerateSwarmParams(stats);
        ScalarMetricUtils.StartModel(metricId, swarmParams: swarmParams, logger: _log);
    }

    /// <summary>
    /// Unmonitor a metric
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    public void UnmonitorMetric(string metricId)
    {
        RepositoryFactory.Metric.DeleteModel(metricId);

        ModelSwapperUtils.DeleteHtmModel(metricId);

        _log.InfoFormat("HTM Metric unmonitored: metric={0}", metricId);
    }

    public string Datasource { get { return "custom"; } }

    /// <summary>
    /// Perform the start-monitoring operation atomically/reliably
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    /// <param name="modelSpec">same as `modelSpec`` from `monitorMetric`</param>
    /// <param name="swarmParams">object returned by scalar_metric_utils.generateSwarmParams()</param>
    private void StartMonitoringWithRetries(string metricId, ModelSpec modelSpec, ExperimentParameters swarmParams)
    {
        var metricObj = RepositoryFactory.Metric.GetMetric(metricId);
        if (metricObj.DataSource != "custom")
        {
            throw new InvalidOperationException("Not an HTM metric");
        }
        if (metricObj.Status != MetricStatus.Unmonitored)
        {
            _log.InfoFormat("MonitorMetric: already monitored; metric={0}", metricObj);
            throw new MetricAlreadyMonitored(metricId, "Custom metric={0} is already monitored by model={1}", metricObj.Name, metricObj);
        }
        // Save model specification in metric row
        var update = new Map<string, object>
        {
            {
                "parameters",
                JsonConvert.SerializeObject(modelSpec, new JsonSerializerSettings
                {
                    TypeNameHandling = TypeNameHandling.All
                })
            }
        };
        var instanceName = GetInstanceNameForModelSpec(modelSpec);
        if (instanceName != null)
        {
            update["server"] = instanceName;
        }
        RepositoryFactory.Metric.UpdateMetricColumns(metricId, update);

        var modelStarted = ScalarMetricUtils.StartMonitoring(metricId, swarmParams, logger: _log);
        if (modelStarted)
        {
            ScalarMetricUtils.SendBacklogDataToModel(metricId, logger: _log);
        }
    }

    /// <summary>
    /// Get canonical instance name from a model spec
    /// </summary>
    /// <param name="modelSpec">Datasource-specific model specification</param>
    /// <returns>Canonical instance name</returns>
    private string GetInstanceNameForModelSpec(ModelSpec modelSpec)
    {
        return modelSpec.MetricSpec.Resource;
    }

    /// <summary>
    /// Get metric data statistics
    /// </summary>
    /// <param name="metricId">unique identifier of the metric row</param>
    /// <returns>a dictionary with the metric's statistics  {"min": <min-value>, "max": <max-value>}</returns>
    private MetricStatistic GetMetricStatistics(string metricId)
    {
        MetricStatistic stats = RepositoryFactory.MetricData.GetMetricStats(metricId);

        var minVal = stats.Min;
        var maxVal = stats.Max;
        if (maxVal < minVal)
        {
            _log.WarnFormat("Max encoder value ({0}) is not greater than min ({1}).",
                maxVal, minVal);
            maxVal = minVal + 1;
        }

        // Add a 20% buffer on both ends of the range
        // (borrowed from legacy custom adapter)
        var buff = (maxVal - minVal) * 0.2;
        minVal -= buff;
        maxVal += buff;

        _log.DebugFormat("getMetricStatistics for metric={0}: minVal={1},maxVal={2}", metricId, minVal, maxVal);

        MetricStatistic stat = new MetricStatistic(minVal, maxVal, null);
        return stat;
    }

    /// <summary>
    /// Create scalar HTM metric if it doesn't exist
    /// NOTE: this method is specific to HTM Metrics, where metric creation happens separately from model creation.
    /// </summary>
    /// <param name="metricName">name of the HTM metric</param>
    /// <returns>unique metric identifier</returns>
    public string CreateMetric(string metricName)
    {
        var resource = MakeDefaultResourceName(metricName);

        Metric metricObj = RepositoryFactory.Metric.GetCustomMetricByName(metricName);

        if (metricObj != null)
        {
            throw new MetricAlreadyExists(metricObj.Uid, "Custom metric with matching name already exists");
        }

        Metric metricDict = RepositoryFactory.Metric.AddMetric(name: metricName, description: "Custom metric " + metricName, server: resource, location: "", pollInterval: DEFAULT_METRIC_PERIOD, status: MetricStatus.Unmonitored, datasource: "custom");

        return metricDict.Uid;
    }

    /// <summary>
    /// Construct the default resource name for a given metric
    /// </summary>
    /// <param name="metricName">unique name of the metric</param>
    /// <returns></returns>
    private static string MakeDefaultResourceName(string metricName)
    {
        return metricName;
    }

}