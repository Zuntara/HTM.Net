using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Threading;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Taurus.HtmEngine.Adapters;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Util;
using log4net;
using Newtonsoft.Json;
using Tuple = HTM.Net.Util.Tuple;

namespace HTM.Net.Research.Taurus.HtmEngine.runtime
{
    /// <summary>
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/runtime/scalar_metric_utils.py
    /// </summary>
    public class ScalarMetricUtils
    {
        /// <summary>
        /// Generate parameters for creating a model
        /// </summary>
        /// <param name="stats">dict with "min", "max" and optional "minResolution"; values must be integer, float or null.</param>
        /// <returns>if either minVal or maxVal is None, returns None; otherwise returns
        /// swarmParams object that is suitable for passing to startMonitoring and
        /// startModel</returns>
        public static BestSingleMetricAnomalyParamsDescription GenerateSwarmParams(MetricStatistic stats)
        {
            var minVal = stats.Min;
            var maxVal = stats.Max;
            var minResolution = stats.MinResolution;
            if (minVal == null || maxVal == null)
            {
                return null;
            }
            // Create possible swarm parameters based on metric data
            BestSingleMetricAnomalyParamsDescription swarmParams =
                GetScalarMetricWithTimeOfDayAnomalyParams(metricData: new[] { 0 }, minVal: minVal, maxVal: maxVal, minResolution: minResolution);

            // 
            swarmParams.inputRecordSchema = new Map<string, Tuple<FieldMetaType, SensorFlags>>
            {
                {"c0", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.DateTime, SensorFlags.Timestamp )} ,
                {"c1", new Tuple<FieldMetaType, SensorFlags>(FieldMetaType.Float, SensorFlags.Blank ) },
            };
            /*
             swarmParams["inputRecordSchema"] = (
                fieldmeta.FieldMetaInfo("c0", fieldmeta.FieldMetaType.datetime,
                                        fieldmeta.FieldMetaSpecial.timestamp),
                fieldmeta.FieldMetaInfo("c1", fieldmeta.FieldMetaType.float,
                                        fieldmeta.FieldMetaSpecial.none),
              )
             */
            return swarmParams;
        }

        // https://github.com/numenta/nupic/blob/a5a7f52e39e30c5356c561547fc6ac3ffd99588c/src/nupic/frameworks/opf/common_models/cluster_params.py

        /// <summary>
        /// Return a dict that can be used to create an anomaly model via OPF's ModelFactory.
        /// </summary>
        /// <param name="metricData">numpy array of metric data. Used to calculate minVal and maxVal if either is unspecified</param>
        /// <param name="minVal">Minimum value of metric. Used to set up encoders. If null will be derived from metricData.</param>
        /// <param name="maxVal">Maximum value of metric. Used to set up input encoders. If null will be derived from metricData</param>
        /// <param name="minResolution">minimum resolution of metric. Used to set up encoders.If None, will use default value of 0.001.</param>
        /// <returns>
        /// a dict containing "modelConfig" and "inferenceArgs" top-level properties.
        /// The value of the "modelConfig" property is for passing to the OPF `ModelFactory.create()` method as the `modelConfig` parameter.
        /// The "inferenceArgs" property is for passing to the resulting model's `enableInference()` method as the inferenceArgs parameter.
        /// 
        /// NOTE: the timestamp field corresponds to input "c0"; 
        /// the predicted field corresponds to input "c1".
        /// </returns>
        /// <remarks>
        /// Example:
        ///    from nupic.frameworks.opf.modelfactory import ModelFactory
        /// 
        ///    from nupic.frameworks.opf.common_models.cluster_params import (
        /// 
        ///      getScalarMetricWithTimeOfDayAnomalyParams)
        ///  params = getScalarMetricWithTimeOfDayAnomalyParams(
        ///    metricData=[0],
        ///    minVal= 0.0,
        ///    maxVal= 100.0)
        ///  model = ModelFactory.create(modelConfig=params["modelConfig"])
        ///  model.enableLearning()
        ///  model.enableInference(params["inferenceArgs"])
        /// </remarks>
        private static BestSingleMetricAnomalyParamsDescription GetScalarMetricWithTimeOfDayAnomalyParams(int[] metricData, double? minVal, double? maxVal, double? minResolution)
        {
            // Default values
            if (!minResolution.HasValue) minResolution = 0.001;
            // Compute min and/or max from the data if not specified
            if (!minVal.HasValue || !maxVal.HasValue)
            {
                var rangeGen = RangeGen(metricData);
                if (!minVal.HasValue)
                {
                    minVal = (double?)rangeGen.Get(0);
                }
                if (!maxVal.HasValue)
                {
                    maxVal = (double?)rangeGen.Get(1);
                }
            }
            //  Handle the corner case where the incoming min and max are the same
            if (minVal == maxVal)
            {
                maxVal = minVal + 1;
            }
            // Load model parameters and update encoder params
            var paramSet = BestSingleMetricAnomalyParamsDescription.BestSingleMetricAnomalyParams;
            FixupRandomEncoderParams(paramSet, minVal, maxVal, minResolution);
            return paramSet;
        }

        /// <summary>
        /// Return reasonable min/max values to use given the data.
        /// </summary>
        /// <returns></returns>
        private static Tuple RangeGen(int[] data, int std = 1)
        {
            double average = data.Average();
            double sumOfSquaresOfDifferences = data.Select(val => (val - average) * (val - average)).Sum();
            double dataStd = Math.Sqrt(sumOfSquaresOfDifferences / data.Length);

            if (Math.Abs(dataStd) < double.Epsilon)
            {
                dataStd = 1;
            }
            double minval = ArrayUtils.Min(data) - std * dataStd;
            double maxval = ArrayUtils.Max(data) + std * dataStd;
            return new Tuple(minval, maxval);
        }

        /// <summary>
        /// Given model params, figure out the correct parameters for the RandomDistributed encoder.
        /// Modifies params in place.
        /// </summary>
        /// <param name="paramSet"></param>
        /// <param name="minVal"></param>
        /// <param name="maxVal"></param>
        /// <param name="minResolution"></param>
        private static void FixupRandomEncoderParams(BestSingleMetricAnomalyParamsDescription paramSet, double? minVal, double? maxVal,
            double? minResolution)
        {
            Map<string, Map<string, object>> encodersDict = paramSet.modelConfig.modelParams.sensorParams.encoders;
            foreach (var encoder in encodersDict.Values)
            {
                if (encoder != null)
                {
                    if (encoder["type"] == "RandomDistributedScalarEncoder")
                    {
                        var resolution = Math.Max(minResolution.Value,
                            (maxVal.Value - minVal.Value) / (double)encoder["numBuckets"]);
                        encoder.Remove("numBuckets");
                        encodersDict["c1"]["resolution"] = resolution;
                    }
                }
            }
        }
        /// <summary>
        /// Start monitoring an UNMONITORED metric.
        /// NOTE: typically called either inside a transaction and/or with locked tables
        /// Starts the CLA model if provided non-None swarmParams; otherwise defers model
        /// creation to a later time and places the metric in MetricStatus.PENDING_DATA state.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="swarmParams">swarmParams generated via scalar_metric_utils.generateSwarmParams() or None.</param>
        /// <param name="logger">logger object</param>
        /// <returns> True if model was started; False if not</returns>
        public static bool StartMonitoring(string metricId, BestSingleMetricAnomalyParamsDescription swarmParams, ILog logger)
        {
            bool modelStarted = false;
            DateTime startTime = DateTime.Now;
            Metric metricObj = RepositoryFactory.Metric.GetMetric(metricId);
            Debug.Assert(metricObj.Status == MetricStatus.Unmonitored);

            if (swarmParams != null)
            {
                // We have swarmParams, so start the model
                modelStarted = StartModelHelper(metricObj, swarmParams, logger);
            }
            else
            {
                // Put the metric into the PENDING_DATA state until enough data arrives for
                // stats
                MetricStatus refStatus = metricObj.Status;
                RepositoryFactory.Metric.SetMetricStatus(metricId, MetricStatus.PendingData, refStatus: refStatus);
                // refresh
                var metricStatus = RepositoryFactory.Metric.GetMetric(metricId).Status;
                if (metricStatus == MetricStatus.PendingData)
                {
                    logger.InfoFormat(
                        "startMonitoring: promoted metric to model in PENDING_DATA; metric={0}; duration={1}",
                        metricId, DateTime.Now - startTime);
                }
                else
                {
                    throw new InvalidOperationException("metric status change failed");
                }
            }
            return modelStarted;
        }
        /// <summary>
        /// Start the model
        /// </summary>
        /// <param name="metricObj">metric, freshly-loaded</param>
        /// <param name="swarmParams">non-None swarmParams generated via GenerateSwarmParams().</param>
        /// <param name="logger">logger object</param>
        /// <returns>True if model was started; False if not</returns>
        private static bool StartModelHelper(Metric metricObj, IDescription swarmParams, ILog logger)
        {
            if (swarmParams == null)
                throw new ArgumentNullException("swarmParams", "startModel: 'swarmParams' must be non-None: metric=" + metricObj.Uid);

            string metricName = metricObj.Name;

            if (!new[] { MetricStatus.Unmonitored, MetricStatus.PendingData, }.Contains(metricObj.Status))
            {
                if (new[] { MetricStatus.CreatePending, MetricStatus.Active, }.Contains(metricObj.Status))
                {
                    return false;
                }
                logger.ErrorFormat("Unexpected metric status; metric={0}", metricObj.Uid);
                throw new InvalidOperationException(string.Format("startModel: unexpected metric status; metric={0}", metricObj.Uid));
            }

            var startTime = DateTime.Now;

            // Save swarm parameters and update metric status
            MetricStatus refStatus = metricObj.Status;
            RepositoryFactory.Metric.UpdateMetricColumnsForRefStatus(metricObj.Uid, refStatus,
                new Map<string, object>
                {
                    {"status", MetricStatus.CreatePending},
                    {"modelParams", JsonConvert.SerializeObject(swarmParams)}
                });

            metricObj = RepositoryFactory.Metric.GetMetric(metricObj.Uid);

            if (metricObj.Status != MetricStatus.CreatePending)
            {
                throw new MetricsChangeError("startModel: unable to start model={0}; metric status morphed from {1} to {2}",
                    metricObj.Uid, refStatus, metricObj.Status);
            }

            // Request to create the CLA Model
            try
            {
                ModelSwapperUtils.CreateHtmModel(metricObj.Uid, swarmParams);
            }
            catch (Exception e)
            {
                logger.ErrorFormat("startModel: createHTMModel failed. -> {0}", e);
                RepositoryFactory.Metric.SetMetricStatus(metricObj.Uid, MetricStatus.Error, message: e.ToString());
                throw;
            }

            logger.InfoFormat("startModel: started model uid={0}, name={1}; duration={2}",
                metricObj.Uid, metricName, DateTime.Now - startTime);

            return true;
        }

        /// <summary>
        /// Send backlog data to OPF/CLA model. Do not call this before starting the  model.
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="logger">logger object</param>
        public static void SendBacklogDataToModel(string metricId, ILog logger)
        {
            List<ModelInputRow> backlogData = RepositoryFactory.MetricData.GetMetricData(metricId)
                .Select(md => new ModelInputRow(rowId: md.Rowid,
                    data: new List<string>
                    {
                        md.Timestamp.ToString("G"),
                        md.MetricValue.ToString(NumberFormatInfo.InvariantInfo)
                    }))
                .ToList();

            if (backlogData != null && backlogData.Any())
            {
                ModelSwapperInterface modelSwapper = new ModelSwapperInterface();

                ModelDataFeeder.SendInputRowsToModel(modelId: metricId, inputRows: backlogData, batchSize: 100,
                    modelSwapper: modelSwapper, logger: logger, profiling: logger.IsDebugEnabled);
            }

            logger.InfoFormat("sendBacklogDataToModel: sent {0} backlog data rows to model={1}",
                backlogData.Count, metricId);
        }

        /// <summary>
        /// Start the model atomically/reliably and send data backlog, if any
        /// </summary>
        /// <param name="metricId">unique identifier of the metric row</param>
        /// <param name="swarmParams">non-None swarmParams generated via scalar_metric_utils.generateSwarmParams().</param>
        /// <param name="logger"> logger object</param>
        /// <returns>True if model was started; False if not</returns>
        public static bool StartModel(string metricId, IDescription swarmParams, ILog logger)
        {
            if (string.IsNullOrWhiteSpace(metricId)) throw new ArgumentNullException(nameof(metricId));
            if (swarmParams == null) throw new ArgumentNullException(nameof(swarmParams));

            var metricObj = RepositoryFactory.Metric.GetMetric(metricId);
            var modelStarted = StartModelHelper(metricObj, swarmParams, logger);
            if (modelStarted)
            {
                SendBacklogDataToModel(metricId, logger);
            }
            return modelStarted;
        }
    }

    public class MetricDataSample
    {
        public DateTime Timestamp { get; set; }
        public double MetricValue { get; set; }
        public long? RowId { get; set; }
    }

    public class MetricStreamer
    {
        public const int TAIL_INPUT_TIMESTAMP_GC_INTERVAL_SEC = 7 * 24 * 60 * 60;
        private readonly ILog _log = LogManager.GetLogger(typeof(MetricStreamer));
        private bool _profiling;
        private int _metricDataOutputChunkSize;
        private Map<string, DateTime?> _tailInputMetricDataTimestamps;
        private TimeSpan _lastTailInputMetricDataTimestampsGCTime;

        public MetricStreamer()
        {
            _profiling = _log.IsDebugEnabled;
            _metricDataOutputChunkSize = 200;
            // from config chunk_size
            // Cache of latest metric_data timestamps for each metric; used for filtering
            // out duplicate/re-delivered input metric data so it won't be saved again
            // in the metric_data table. Each key is a metric id and the corresponding
            // value is a datetime.datetime timestamp of the last metric_data stored by
            // us in metric_data table.
            _tailInputMetricDataTimestamps = new Map<string, DateTime?>();
            // Last garbage-collection time; seconds since unix epoch (time.time())
            _lastTailInputMetricDataTimestampsGCTime = TimeSpan.Zero;
        }

        /// <summary>
        ///  Filter out metric data samples that are out of order or have duplicate timestamps.
        /// </summary>
        /// <param name="data">A sequence of data samples; each data sample is a pair:  (datetime.datetime, float)</param>
        /// <param name="metricId">unique metric id</param>
        /// <param name="lastDataRowId">last metric data row identifier for metric with given metric id</param>
        /// <returns>a (possibly empty) sequence of metric data samples that passed the scrubbing.</returns>
        private List<MetricDataSample> ScrubDataSamples(List<MetricDataSample> data, string metricId, long lastDataRowId)
        {
            List<MetricDataSample> passingSamples = new List<MetricDataSample>();
            List<DateTime> rejectedDataTimestamps = new List<DateTime>();
            DateTime? prevSampleTimestamp = GetTailMetricRowTimestamp(metricId, lastDataRowId);

            foreach (MetricDataSample sample in data)
            {
                DateTime timestamp = sample.Timestamp;
                double metricValue = sample.MetricValue;
                // Filter out those whose timestamp is not newer than previous sampale's
                if (prevSampleTimestamp != null && timestamp < prevSampleTimestamp)
                {
                    // Reject it; this could be the result of an unordered sample feed or
                    // concurrent feeds of samples for the same metric
                    // TODO: unit-test
                    rejectedDataTimestamps.Add(timestamp);
                    _log.ErrorFormat("Rejected input sample older than previous ts={0} ({1}): metric={2}; rejectedTs={3} ({4}); rejectedValue={5}",
                       prevSampleTimestamp, EpochFromNaiveUTCDatetime(prevSampleTimestamp), metricId,
                       timestamp, EpochFromNaiveUTCDatetime(timestamp), metricValue);
                }
                else if (timestamp == prevSampleTimestamp)
                {
                    // Reject it; this could be the result of guaranteed delivery via message
                    // publish retry following transient connection loss with the message bus
                    _log.ErrorFormat("Rejected input sample with duplicate ts={0} ({1}): metric={2}; rejectedValue={3}",
                        prevSampleTimestamp, EpochFromNaiveUTCDatetime(prevSampleTimestamp), metricId,
                        metricValue);
                    rejectedDataTimestamps.Add(timestamp);
                }
                else
                {
                    passingSamples.Add(sample);
                    prevSampleTimestamp = timestamp;
                }
            }

            if (rejectedDataTimestamps.Any())
            {
                // TODO: unit-test
                _log.ErrorFormat("Rejected input rows: metric={0}; numRejected={1}; rejectedRange=[{2}..{3}]",
                    metricId, rejectedDataTimestamps.Count,
                      rejectedDataTimestamps.Min(), rejectedDataTimestamps.Max());
            }
            return passingSamples;
        }

        private object EpochFromNaiveUTCDatetime(DateTime? dt)
        {
            return (dt.GetValueOrDefault() - new DateTime(0, DateTimeKind.Utc)).TotalSeconds;
        }

        /// <summary>
        /// TODO: unit-test
        /// </summary>
        /// <param name="metricId">unique metric id</param>
        /// <param name="lastDataRowId">last metric data row identifier for metric with given metric id</param>
        /// <returns>
        /// timestamp of the last metric data row that *we* stored in
        /// metric_data table for the given metric id, or None if none have been
        /// stored
        /// </returns>
        private DateTime? GetTailMetricRowTimestamp(string metricId, long lastDataRowId)
        {
            if ((DateTime.Now - (DateTime.Now - _lastTailInputMetricDataTimestampsGCTime)).TotalSeconds >
                TAIL_INPUT_TIMESTAMP_GC_INTERVAL_SEC)
            {
                // Garbage-collect our cache
                // TODO: unit test
                _tailInputMetricDataTimestamps.Clear();
                _lastTailInputMetricDataTimestampsGCTime = DateTime.Now.TimeOfDay;
                _log.Info("Garbage-collected tailInputMetricDataTimestamps cache");
            }

            DateTime? timestamp = null;
            timestamp = _tailInputMetricDataTimestamps.Get(metricId, null);
            if (timestamp == null)
            {
                var rows = RepositoryFactory.MetricData.GetMetricData(metricId, rowId: lastDataRowId);
                if (rows.Count > 0 && rows.Any())
                {
                    timestamp = rows.First().Timestamp;
                    _tailInputMetricDataTimestamps[metricId] = timestamp;
                }
            }
            return timestamp;
        }

        /// <summary>
        /// Store the given metric data samples in metric_data table
        /// </summary>
        /// <param name="data">A sequence of data samples; each data sample is a pair: (datetime.datetime, float)</param>
        /// <param name="metricId">unique metric id</param>
        /// <returns>a (possibly empty) tuple of ModelInputRow objects corresponding to the samples 
        /// that were stored; ordered by rowid.</returns>
        private List<ModelInputRow> StoreDataSamples(List<MetricDataSample> data, string metricId)
        {
            if (data != null && data.Any())
            {
                // repository.AddMetricData expects samples as pairs of (value, timestamp)
                var rowData = data.Select(d => new Tuple<DateTime, double>(d.Timestamp, d.MetricValue)).ToList();
                // Save new metric data in metric table
                var rows = RepositoryFactory.MetricData.AddMetricData(metricId, rowData);
                // Update tail metric data timestamp cache for metrics stored by us
                _tailInputMetricDataTimestamps[metricId] = rows.Last().Timestamp;
                // Add newly-stored records to batch for sending to CLA model
                var modelInputRows = ArrayUtils.Zip(data, rows)
                    .Select(item => new ModelInputRow((long)item.Get(3), new List<string>
                    {
                        ((DateTime)item.Get(0)).ToString("G"),
                        ((double)item.Get(1)).ToString(NumberFormatInfo.InvariantInfo)
                    }))
                    .ToList();
                return modelInputRows;
            }
            _log.Warn("StoreDataSamples called with empty data");
            return new List<ModelInputRow>();
        }

        /// <summary>
        /// Store the data samples in metric_data table if needed, and stream the
        /// data samples to the model associated with the metric if the metric is
        /// monitored.
        /// 
        /// If the metric is in PENDING_DATA state: if there are now enough data samples
        /// to start a model, start it and stream it the entire backlog of data samples;
        /// if there are still not enough data samples, suppress streaming of the data
        /// samples.
        /// </summary>
        /// <param name="data">A sequence of data samples; each data sample is a three-tuple:
        ///           (datetime.datetime, float, None)
        ///              OR
        ///           (datetime.datetime, float, rowid)</param>
        /// <param name="metricId">unique id of the HTM metric</param>
        /// <param name="modelSwapper">ModelSwapper object for sending data to models</param>
        public void StreamMetricData(List<MetricDataSample> data, string metricId, ModelSwapperInterface modelSwapper)
        {
            if (data == null)
            {
                _log.WarnFormat("Empty input metric data batch for metric={0}", metricId);
                return;
            }

            Func<Tuple<List<ModelInputRow>, string, MetricStatus>> storeDataWithRetries = () =>
            {
                List<ModelInputRow> modelInputRowsResult;
                var metricObj = RepositoryFactory.Metric.GetMetric(metricId);

                if (metricObj.Status != MetricStatus.Unmonitored &&
                    metricObj.Status != MetricStatus.Active &&
                    metricObj.Status != MetricStatus.PendingData &&
                    metricObj.Status != MetricStatus.CreatePending)
                {
                    _log.ErrorFormat("Can't stream: metric={0} has unexpected status={1}",
                        metricId, metricObj.Status);
                    modelInputRowsResult = null;
                }
                else
                {
                    // TODO: unit test
                    var passingSamples = ScrubDataSamples(data, metricId, metricObj.LastRowId);
                    if (passingSamples != null)
                    {
                        modelInputRowsResult = StoreDataSamples(passingSamples, metricId);
                    }
                    else
                    {
                        modelInputRowsResult = new List<ModelInputRow>();
                    }
                }
                return new Tuple<List<ModelInputRow>, string, MetricStatus>(modelInputRowsResult, metricObj.DataSource, metricObj.Status);
            };

            var resultTuple = storeDataWithRetries();
            var modelInputRows = resultTuple.Item1;
            var datasource = resultTuple.Item2;
            var metricStatus = resultTuple.Item3;

            if (modelInputRows == null)
            {
                // Metric was in state not suitable for streaming
                return;
            }
            if (!modelInputRows.Any())
            {
                _log.ErrorFormat("No records to stream to model={0}", metricId);
                return;
            }

            if (metricStatus == MetricStatus.Unmonitored)
            {
                // Metric was not monitored during storage, so we're done
                // self._log.info("Status of metric=%s is UNMONITORED; not forwarding "
                //               "%d rows: rowids[%s..%s]; data=[%s..%s]",
                // metricID, len(modelInputRows),
                // modelInputRows[0].rowID, modelInputRows[-1].rowID,
                // modelInputRows[0].data, modelInputRows[-1].data)
                return;
            }

            long? lastDataRowId = modelInputRows.Last().RowId;
            // Check models that are waiting for activation upon sufficient data
            if (metricStatus == MetricStatus.PendingData)
            {
                if (lastDataRowId >= CustomDatasourceAdapter.MODEL_CREATION_RECORD_THRESHOLD)
                {
                    try
                    {
                        DataAdapterFactory.CreateDatasourceAdapter(datasource).ActivateModel(metricId);
                    }
                    catch (Exception ex)
                    {
                        _log.ErrorFormat("Couldn't start model={0}: {1}", metricId, ex);
                    }
                }
                return; // todo: must this be here?
            }

            // Stream data if model is activated
            // TODO: unit test
            if (metricStatus == MetricStatus.Active || metricStatus == MetricStatus.PendingData)
            {
                SendInputRowsToModel(inputRows: modelInputRows, metricId: metricId, modelSwapper: modelSwapper);
                _log.DebugFormat("Streamed numRecords={0} to model={1}", modelInputRows.Count, metricId);
            }
        }

        /// <summary>
        /// Send input rows to CLA model for processing
        /// </summary>
        /// <param name="inputRows"> sequence of model_swapper_interface.ModelInputRow objects</param>
        /// <param name="metricId"></param>
        /// <param name="modelSwapper"></param>
        private void SendInputRowsToModel(List<ModelInputRow> inputRows, string metricId,
            ModelSwapperInterface modelSwapper)
        {
            ModelDataFeeder.SendInputRowsToModel(modelId: metricId, inputRows: inputRows, batchSize: _metricDataOutputChunkSize,
                modelSwapper: modelSwapper, logger: _log, profiling: _profiling);
        }
    }

    public class ModelDataFeeder
    {
        /// <summary>
        /// Send input rows to CLA model for processing
        /// </summary>
        /// <param name="modelId">unique identifier of the model</param>
        /// <param name="inputRows">sequence of model_swapper_interface.ModelInputRow objects</param>
        /// <param name="batchSize">max number of data records per input batch</param>
        /// <param name="modelSwapper">model_swapper_interface.ModelSwapperInterface object</param>
        /// <param name="logger">logger object</param>
        /// <param name="profiling">True if profiling is enabled</param>
        public static void SendInputRowsToModel(string modelId, List<ModelInputRow> inputRows, int batchSize,
            ModelSwapperInterface modelSwapper, ILog logger, bool profiling)
        {
            logger.DebugFormat("Streaming numRecords={0} to model={1}", inputRows.Count, modelId);

            // Stream data to HTM model in batches
            // TODO: now it stream everything, chunk it
            //foreach (var batch in inputRows)
            var batch = inputRows;
            {
                DateTime startTime = DateTime.Now;

                var batchId = modelSwapper.SubmitRequests(modelId, batch);

                if (profiling)
                {
                    var headTS = batch.First().Data.First();
                    var tailTS = batch.Last().Data.First();
                    logger.InfoFormat(
                        "{{TAG:STRM.DATA.TO_MODEL.DONE}} Submitted batch={0} to model={1}; numRows={2}; rows=[{3}]; ts=[{4}]; duration={5}s",
                        batchId, modelId, batch.Count,
                        string.Format("{0}..{1}", batch.First().RowId, batch.Last().RowId),
                        string.Format("{0}..{1}", headTS, tailTS), DateTime.Now - startTime);
                }
            }
        }
    }

    /// <summary>
    /// Listens on a UDP or TCP port for metric data to write to a queue. > to be replaced by WCF service
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/runtime/metric_listener.py
    /// </summary>
    public class MetricListener
    {
        /// <summary>
        /// Max number of data samples per batch
        /// </summary>
        public const int MAX_BATCH_SIZE = 200;

        // Forwards data in batches to the metric storer
    }

    public class MetricMessage
    {
        public string MetricName { get; set; }
        public double Value { get; set; }
        public DateTime Timestamp { get; set; }
    }

    /// <summary>
    /// This process is designed work in parallel with metric_listener for accepting
    /// metrics and adding them to the database.
    /// 
    /// TODO: Get just the values for metrics from the database that we need instead
    /// of the entire rows.We might only need the `uid`.
    /// 
    /// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/runtime/metric_storer.py
    /// </summary>
    public class MetricStorer
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(MetricStorer));
        private IDictionary<string, List<object>> _customMetrics;
        private bool _profiling;
        private bool _stopProcessing;
        public ConcurrentQueue<MetricMessage> Queue { get; set; }

        public const int MAX_MESSAGES_PER_BATCH = 200;
        public const int POLL_DELAY_SEC = 1;
        public const int CACHED_METRICS_TO_KEEP = 10000;
        public const int MAX_CACHED_METRICS = 15000;

        public MetricStorer()
        {
            Queue = new ConcurrentQueue<MetricMessage>();
            _profiling = _log.IsDebugEnabled;
        }

        public void RunServer()
        {
            DateTime now = DateTime.Now;
            _customMetrics = RepositoryFactory.Metric.GetCustomMetrics().ToDictionary(k => k.Name, v => new List<object>
            {
                v, now
            });
            var metricStreamer = new MetricStreamer();
            var modelSwapper = new ModelSwapperInterface();
            var messageRxTimes = new List<DateTime>();
            var messages = new List<MetricMessage>();

            while (!_stopProcessing)
            {
                // server loop
                MetricMessage message = null;
                if (!Queue.IsEmpty)
                {
                    Queue.TryDequeue(out message);
                    messages.Add(message);
                    if (_profiling)
                    {
                        messageRxTimes.Add(DateTime.Now);
                    }
                }
                if (message == null || messages.Count > MAX_MESSAGES_PER_BATCH)
                {
                    if (messages.Count > MAX_MESSAGES_PER_BATCH)
                    {
                        HandleBatch(messages, messageRxTimes, metricStreamer, modelSwapper);
                        messages.Clear();
                        messageRxTimes.Clear();
                    }
                    else
                    {
                        Thread.Sleep(POLL_DELAY_SEC * 1000);
                    }
                }
            }
        }

        /// <summary>
        /// Process a batch of messages from the queue.
        /// 
        /// This parses the message contents as JSON and uses the 'protocol' field to
        /// determine how to parse the 'data' in the message.The data is added to the
        /// database and sent through the metric streamer.
        /// 
        /// The Metric objects are cached in gCustomMetrics to minimize database
        /// lookups.
        /// </summary>
        /// <param name="messages">a list of queue messages to process</param>
        /// <param name="messageRxTimes">optional sequence of message-receive times (from time.time()) if profiling corresponding to the messages in `messages` arg, else empty list</param>
        /// <param name="metricStreamer"></param>
        /// <param name="modelSwapper"></param>
        private void HandleBatch(List<MetricMessage> messages, List<DateTime> messageRxTimes,
            MetricStreamer metricStreamer, ModelSwapperInterface modelSwapper)
        {
            Dictionary<string, List<MetricDataSample>> data = new Dictionary<string, List<MetricDataSample>>();
            int i = 0;
            foreach (var element in ArrayUtils.Zip(messages, messageRxTimes))
            {
                MetricMessage m = (MetricMessage)element.Get(0);
                DateTime rxTime = (DateTime)element.Get(1);

                MetricDataSample sample = new MetricDataSample();
                sample.Timestamp = m.Timestamp;
                sample.MetricValue = m.Value;
                sample.RowId = i++;

                if (data.ContainsKey(m.MetricName))
                {
                    data[m.MetricName].Add(sample);
                }
                else
                {
                    data.Add(m.MetricName, new List<MetricDataSample> {sample});
                }
            }
            AddMetricData(data, metricStreamer, modelSwapper);
        }
        /// <summary>
        /// Send metric data for each metric to the metric streamer
        /// </summary>
        /// <param name="data"></param>
        /// <param name="metricStreamer"></param>
        /// <param name="modelSwapper"></param>
        private void AddMetricData(Dictionary<string, List<MetricDataSample>> data, MetricStreamer metricStreamer, ModelSwapperInterface modelSwapper)
        {
            // For each metric, create the metric if it doesn't exist and add the data
            foreach (var metric in data)
            {
                if (!_customMetrics.ContainsKey(metric.Key))
                {
                    AddMetric(metric.Key);
                }
                else
                {
                    _customMetrics[metric.Key][1] = DateTime.Now;
                }
                // Add the data
                var metricData = metric.Value.Select(ds => new MetricDataSample {MetricValue = ds.MetricValue, Timestamp = ds.Timestamp}).ToList();
                metricStreamer.StreamMetricData(metricData, ((Metric)_customMetrics[metric.Key][0]).Uid, modelSwapper);
            }
        }

        /// <summary>
        /// Add the new metric to the database.
        /// </summary>
        /// <param name="metricName"></param>
        private void AddMetric(string metricName)
        {
            string metricId = null;
            if (_customMetrics.ContainsKey(metricName))
            {
                metricId = ((Metric) _customMetrics[metricName][0]).Uid;
                _customMetrics[metricName][0] = RepositoryFactory.Metric.GetMetric(metricId);
                return;
            }
            metricId = DataAdapterFactory.CreateDatasourceAdapter("custom").CreateMetric(metricName);
            var metric = RepositoryFactory.Metric.GetMetric(metricId);
            _customMetrics[metricName] = new List<object> {metric, DateTime.Now};
            //TrimMetricCache(); // TODO
        }

        public void StopServer()
        {
            _stopProcessing = true;
        }
    }

    /// <summary>
    /// Anomaly Service for processing CLA model results, calculating Anomaly
    ///     Likelihood scores, and updating the associated metric data records
    ///     Records are processed in batches from
    /// ``ModelSwapperInterface().consumeResults()`` and the associated
    /// ``MetricData`` rows are updated with the results of applying
    /// ``AnomalyLikelihoodHelper().updateModelAnomalyScores()`` and finally the
    /// results are packaged up as as objects complient with
    /// ``model_inference_results_msg_schema.json`` and published to the model
    /// results exchange, as identified by the ``results_exchange_name``
    /// configuration directive from the ``metric_streamer`` section of
    /// ``config``.
    /// Other services may be subscribed to the model results fanout exchange for
    /// subsequent(and parallel) processing.For example,
    /// ``htmengine.runtime.notification_service.NotificationService`` is one example
    /// of a use-case for that exchange.Consumers must deserialize inbound messages
    /// with ``AnomalyService.deserializeModelResult()``.
    /// </summary>
    public class AnomalyService
    {
        private readonly ILog _log = LogManager.GetLogger(typeof(AnomalyService));
        private bool _profiling;
        private int _statisticsSampleSize;
        private string _modelResultsExchange;
        private AnomalyLikelihoodHelper _likelihoodHelper;

        public AnomalyService()
        {
            _profiling = _log.IsDebugEnabled;
            _modelResultsExchange = ""; // from config
            _statisticsSampleSize = 10; // from config
            _likelihoodHelper = new AnomalyLikelihoodHelper(_log);

        }
    }
}