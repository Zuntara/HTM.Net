using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Taurus.HtmEngine.Repository;
using HTM.Net.Research.Taurus.MetricCollectors;
using HTM.Net.Util;
using MathNet.Numerics;
using static HTM.Net.Parameters;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System;
using HTM.Net.Data;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Taurus.HtmEngine.Adapters;

namespace HTM.Net.Research.Taurus.HtmEngine.Runtime;

// For use with AnomalyService
public class MutableMetricDataRow
{
    public MutableMetricDataRow(double anomalyScore, string displayValue, double metricValue, double rawAnomalyScore,
        int rowid, DateTime timestamp, string uid)
    {
        AnomalyScore = anomalyScore;
        DisplayValue = displayValue;
        MetricValue = metricValue;
        RawAnomalyScore = rawAnomalyScore;
        Rowid = rowid;
        Timestamp = timestamp;
        Uid = uid;
    }

    public override string ToString()
    {
        return string.Format("{0}<uid={1}, rowid={2}, ts={3}, value={4}, raw={5}, anomlik={6}, display={7}>",
            GetType().Name, Uid, Rowid, Timestamp, MetricValue, RawAnomalyScore, AnomalyScore, DisplayValue);
    }

    public double AnomalyScore { get; set; }
    public string DisplayValue { get; set; }
    public double MetricValue { get; set; }
    public double RawAnomalyScore { get; set; }
    public long Rowid { get; set; }
    public DateTime Timestamp { get; set; }
    public string Uid { get; set; }
}

/// <summary>
/// Metric states stored in the "metric" SQL table
/// </summary>
[Flags]
public enum MetricStatus
{
    /// <summary>
    /// This is used when a metric exists but is not monitored. HTM metrics
    /// utilize this when data is sent in but the metric isn't monitored yet.
    /// </summary>
    Unmonitored = 0,
    /// <summary>
    /// This means the model has been created in the engine and there are no errors.
    /// </summary>
    Active = 1,
    /// <summary>
    /// This state is used when a model creation command has been sent to the
    /// engine but hasn't been processed yet.
    /// </summary>
    CreatePending = 2,
    /// <summary>
    /// When there is an irrecoverable error with a model it is put into this state
    /// and the message field is populated with the reason.
    /// </summary>
    Error = 4,
    /// <summary>
    /// The state is used for delayed model creation when there is a specified min
    /// and max and there isn't sufficient data to estimate the min and max with confidence.
    /// </summary>
    PendingData = 8
}

#region Exceptions

public class MetricNotActiveError : Exception
{
    public MetricNotActiveError(string message)
        : base(message)
    {

    }
}
/// <summary>
/// Raised when too many models or "instances" have been created
/// </summary>
public class ModelQuotaExceededError : Exception
{

}

/// <summary>
/// Generic exception for non-specific error while attempting to monitor a metric
/// </summary>
public class ModelMonitorRequestError : Exception
{

}
/// <summary>
/// Generic exception for non-specific error while attempting to unmonitor a metric
/// </summary>
public class ModelUnmonitorRequestError : Exception
{

}
/// <summary>
/// Generic exception for non-specific error while attempting to delete a metric
/// </summary>
public class MetricDeleteRequestError : Exception
{

}
/// <summary>
/// Specified metric was not found
/// </summary>
public class MetricNotFound : Exception
{

}

public class MetricsChangeError : Exception
{
    public MetricsChangeError(string format, params object[] args)
        : base(string.Format(format, args))
    {

    }
}

/// <summary>
/// The requested model was not found (already deleted?)
/// </summary>
public class ModelNotFound : Exception
{

}

/// <summary>
/// Generic exception for non-specific error while getting all models
/// </summary>
public class GetModelsRequestError : Exception
{

}
/// <summary>
/// Exceeded max retries without a single successful execution
/// </summary>
public class RetriesExceededError : Exception
{

}

public class MetricAlreadyMonitored : Exception
{
    public MetricAlreadyMonitored(string uid, string format, params object[] args)
        : base(string.Format(format, args))
    {
        Uid = uid;
    }

    public string Uid { get; set; }
}

public class MetricAlreadyExists : Exception
{
    public MetricAlreadyExists(string uid, string message)
        : base(message)
    {
        Uid = uid;
    }
    public string Uid { get; set; }
}

#endregion

public class ModelHandler
{
    public static Metric CreateModel(CreateModelRequest modelSpec = null)
    {
        if (modelSpec == null) throw new ArgumentNullException("modelSpec");
        bool importing = false;
        if (modelSpec.DataSource == "custom")
        {
            if (modelSpec.Data != null)
            {
                importing = true;
            }
        }
        string metricId;
        try
        {
            var adapter = DataAdapterFactory.CreateDatasourceAdapter(modelSpec.DataSource);
            try
            {
                if (importing)
                {
                    metricId = adapter.ImportModel(modelSpec);
                }
                else
                {
                    metricId = adapter.MonitorMetric(modelSpec);
                }
            }
            catch (MetricAlreadyMonitored e)
            {
                metricId = e.Uid;
            }
        }
        catch (Exception e) // MetricNotSupportedError
        {
            throw;
        }
        return RepositoryFactory.Metric.GetMetric(metricId);
    }
}


#region Swarming model stuff

public class BestSingleMetricAnomalyParameters : ExperimentParameters
{
    public BestSingleMetricAnomalyParameters()
    {
        Initialize();
    }

    public void Initialize()
    {
        SetupProperties();

        // Spatial defaults
        SetParameterByKey(KEY.SP_VERBOSITY, 0);
        SetParameterByKey(KEY.GLOBAL_INHIBITION, true);
        SetParameterByKey(KEY.POTENTIAL_PCT, 0.8);
        SetParameterByKey(KEY.COLUMN_DIMENSIONS, new[] { 2048 });
        SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
        SetParameterByKey(KEY.SEED, 1956);
        SetParameterByKey(KEY.RANDOM, new XorshiftRandom((int)1956));
        SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, 0.003);
        SetParameterByKey(KEY.SYN_PERM_CONNECTED, 0.2);
        SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, 0.0005);
        SetParameterByKey(KEY.MAX_BOOST, 1.0);
        // Temporal defaults
        //SetParameterByKey(KEY.TM_VERBOSITY, 0);
        SetParameterByKey(KEY.CELLS_PER_COLUMN, 32);
        SetParameterByKey(KEY.INPUT_DIMENSIONS, new[] { 2048 });
        //SetParameterByKey(KEY.SEED_TM, 1960);
        //SetParameterByKey(KEY.RANDOM_TM, new XorshiftRandom((int)paramMap[KEY.SEED_TM]));
        SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, 20);
        // Maximum number of synapses per segment
        //  > 0 for fixed-size CLA
        // -1 for non-fixed-size CLA
        SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
        // Maximum number of segments per cell
        //  > 0 for fixed-size CLA
        // -1 for non-fixed-size CLA
        SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, 128);
        SetParameterByKey(KEY.INITIAL_PERMANENCE, 0.21);
        SetParameterByKey(KEY.PERMANENCE_INCREMENT, 0.1);
        SetParameterByKey(KEY.PERMANENCE_DECREMENT, 0.1);
        //SetParameterByKey(KEY.GLOBAL_DECAY, 0);
        //SetParameterByKey(KEY.MAX_AGE, 0);
        SetParameterByKey(KEY.MIN_THRESHOLD, 10);
        SetParameterByKey(KEY.ACTIVATION_THRESHOLD, 13);
        //SetParameterByKey(KEY.PAM_LENGTH, 3);

        // Classifier params
        SetParameterByKey(KEY.CLASSIFIER_ALPHA, 0.035828933612157998);
        SetParameterByKey(KEY.AUTO_CLASSIFY, EnableClassification);
        //SetParameterByKey(KEY.AUTO_CLASSIFY_TYPE, typeof(CLAClassifier));
        SetParameterByKey(KEY.CLASSIFIER_STEPS, new[] { 1 });

        SetParameterByKey(KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);
    }

    private void SetupProperties()
    {
        InferenceType = InferenceType.TemporalAnomaly;

        // Intermediate variables used to compute fields in modelParams and also
        // referenced from the control section.
        AggregationInfo = new AggregationSettings
        {
            days = 0,
            fields = new Map<string, object>(),
            hours = 0,
            microseconds = 0,
            milliseconds = 0,
            minutes = 0,
            months = 0,
            seconds = 0,
            weeks = 0,
            years = 0
        };

        EnableSpatialPooler = true;
        EnableClassification = false;
        EnableTemporalMemory = true;

        Control.InputRecordSchema = new[]
        {
                new FieldMetaInfo("c0", FieldMetaType.DateTime, SensorFlags.Timestamp),
                new FieldMetaInfo("c1", FieldMetaType.Float, SensorFlags.Blank)
            };
        Control.InferenceArgs = new InferenceArgsDescription
        {
            inputPredictedField = InputPredictedField.Auto,
            predictedField = "c1",
            predictionSteps = new[] { 1 }
        };


        #region Encoder setup

        SetParameterByKey(KEY.FIELD_ENCODING_MAP, new EncoderSettingsList
            {
                {
                    "c0_timeOfDay", new EncoderSetting
                    {
                        DayOfWeek = new DayOfWeekTuple(21, 9.49),
                        fieldName = "c0",
                        name = "c0",
                        type = EncoderTypes.DateEncoder
                    }
                },
                {
                    "c0_dayOfWeek", null
                },
                {
                    "c0_weekend", null
                },
                {
                    "c1", new EncoderSetting
                    {
                        fieldName = "c1",
                        name = "c1",
                        type = EncoderTypes.RandomDistributedScalarEncoder,
                        numBuckets = 130.0
                    }
                }
            });

        #endregion
    }

    public static BestSingleMetricAnomalyParameters BestSingleMetricAnomalyParams
    {
        get { return new BestSingleMetricAnomalyParameters(); }
    }
}

public class ModelParams
{
    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? MinResolution { get; set; }

    public Map<string, object> AnomalyLikelihoodParams { get; set; }
    public ExperimentParameters ModelConfig { get; set; }
    public InferenceArgsDescription InferenceArgs { get; set; }
    public FieldMetaInfo[] InputSchema { get; set; }

    public static ModelParams FromDict(IDictionary<string, object> dict)
    {
        ModelParams pars = new ModelParams();
        var bu = BeanUtil.GetInstance();
        foreach (string key in dict.Keys)
        {
            bu.SetSimpleProperty(pars, key, dict[key]);
        }
        return pars;
    }

    public ModelParams Clone()
    {
        return new ModelParams
        {
            Min = Min,
            Max = Max,
            AnomalyLikelihoodParams = new Map<string, object>(AnomalyLikelihoodParams),
            MinResolution = MinResolution,
            ModelConfig = ModelConfig,
            InferenceArgs = InferenceArgs.Clone(),
            InputSchema = (FieldMetaInfo[])InputSchema.Clone()
        };
    }
}

#endregion

public class MetricStatistic
{
    public MetricStatistic(double? min, double? max, double? minResolution)
    {
        Min = min;
        Max = max;
        MinResolution = minResolution;
    }

    public double? Min { get; set; }
    public double? Max { get; set; }
    public double? MinResolution { get; set; }
}

#region Model swapper stuff

// https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/model_swapper/model_swapper_interface.py
// TODO:

/// <summary>
/// This is the interface class to connect the application layer to the Model Swapper.
/// </summary>
public class ModelSwapperInterface
{
    public void DefineModel(string modelId, ExperimentParameters args, Guid commandId)
    {
        // Sends defineModel command over the bus, for now we ignore the bus system
        // Calls the modelRunner which is the other end of the bus system.

        ModelRunner modelRunner = new ModelRunner(modelId);
        modelRunner.DefineModel(new ModelCommand
        {
            Id = commandId,
            Args = args,
            ModelId = modelId
        });
    }

    public List<ModelInferenceResult> SubmitRequests(string modelId, List<ModelInputRow> input)
    {
        List<ModelInferenceResult> results = new List<ModelInferenceResult>();
        ModelRunner modelRunner = new ModelRunner(modelId);
        foreach (ModelInputRow row in input)
        {
            results.Add(modelRunner.ProcessInputRow(row, null));
        }
        return results;
    }

    public void DeleteModel(string modelId, Guid commandId)
    {
        ModelRunner modelRunner = new ModelRunner(modelId);
        modelRunner.DeleteModel(new ModelCommand
        {
            ModelId = modelId,
            Id = commandId
        });
    }
}

public class ModelInputRow
{
    public long RowId { get; set; }
    public List<string> Data { get; set; }

    public ModelInputRow(long rowId, List<string> data)
    {
        RowId = rowId;
        Data = data;
    }
}

public class ModelRunner
{
    private readonly string _modelId;
    public CheckPointManager _checkpointMgr;
    private opf.Model _model;
    private bool _hasCheckpoint;
    private InputRowEncoder _inputRowEncoder;

    public ModelRunner(string modelId)
    {
        _modelId = modelId;
        _checkpointMgr = new CheckPointManager();
        _inputRowEncoder = null;
    }

    /// <summary>
    /// Handle the "defineModel" command
    /// </summary>
    /// <param name="command">ModelCommand instance for the "defineModel" command</param>
    public void DefineModel(ModelCommand command)
    {
        // Save the model to persistent storage (the parameters)

        ModelDefinition newModelDefinition = new ModelDefinition
        {
            ModelParams = new ModelParams
            {
                ModelConfig = command.Args,
                InferenceArgs = command.Args.Control.InferenceArgs,
                InputSchema = command.Args.Control.InputRecordSchema
            }
        };

        _checkpointMgr.Define(modelId: _modelId, definition: newModelDefinition);
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="row">ModelInputRow instance</param>
    /// <param name="currentRunInputSamples">a list; the input row's data will be appended
    /// to this list if the row is processed successfully</param>
    /// <returns>a ModelInferenceResult instance</returns>
    public ModelInferenceResult ProcessInputRow(ModelInputRow row, List<object> currentRunInputSamples)
    {
        if (_model == null)
        {
            LoadModel();
        }
        // Convert a flat input row into a format that is consumable by an OPF model
        _inputRowEncoder.AppendRecord(row.Data);
        var inputRecord = _inputRowEncoder.GetNextRecordDict();
        // Infer
        ModelResult r = _model.run(inputRecord);

        currentRunInputSamples?.Add(row.Data);

        return new ModelInferenceResult(commandId: null, rowId: row.RowId, status: 0, anomalyScore: (double)r.inferences[InferenceElement.AnomalyScore]);
    }

    /// <summary>
    /// Load the model and construct the input row encoder. On success,
    /// the loaded model may be accessed via the `model` attribute
    /// </summary>
    private void LoadModel()
    {
        if (_model != null) return;

        ModelDefinition modelDefinition = null;
        try
        {
            _model = _checkpointMgr.Load(_modelId);
            _hasCheckpoint = true;
        }
        catch (ModelNotFound)
        {
            // So, we didn't have a checkpoint... try to create our model from model
            // definition params
            _hasCheckpoint = false;

            modelDefinition = _checkpointMgr.LoadModelDefinition(_modelId);

            var modelParams = modelDefinition.ModelParams;

            // TODO: when creating the model from params, do we need to call
            // its model.setFieldStatistics() method? And where will the
            // fieldStats come from, anyway?

            //ModelParams modelParams = modelDefinition.modelParams;
            //_model = ModelFactory.Create(modelConfig: modelParams.ModelConfig);
            _model = null;// let it crash, look at line above, description mechanism has been changed
            _model.enableLearning();
            _model.enableInference(modelParams.InferenceArgs);
        }

        // Construct the object for converting a flat input row into a format
        // that is consumable by an OPF model
        if (modelDefinition == null)
        {
            modelDefinition = _checkpointMgr.LoadModelDefinition(_modelId);
        }

        var inputSchema = modelDefinition.inputRecordSchema;

        FieldMetaInfo[] inputFieldsMeta = inputSchema;
        _inputRowEncoder = new InputRowEncoder(inputFieldsMeta);

        // TODO: check https://github.com/numenta/numenta-apps/blob/9d1f35b6e6da31a05bf364cda227a4d6c48e7f9d/htmengine/htmengine/model_swapper/model_runner.py
        // that we need some extra lines

    }
    /// <summary>
    /// Handle the "deleteModel" command
    /// </summary>
    /// <param name="command">ModelCommand instance for the "deleteModel" command</param>
    public ModelInferenceResult DeleteModel(ModelCommand command)
    {
        _checkpointMgr.Remove(modelId: _modelId);

        return new ModelInferenceResult(commandId: command.Id, status: 0);
    }
}



internal class InputRowEncoder
{
    private FieldMetaInfo[] _fieldMeta;
    private List<string> _fieldNames;
    private List<string> _row;
    private ModelRecordEncoder _modelRecordEncoder;

    public InputRowEncoder(FieldMetaInfo[] inputFieldsMeta)
    {
        _fieldMeta = inputFieldsMeta;
        _fieldNames = inputFieldsMeta.Select(m => m.name).ToList();
        _row = null;
    }

    public void AppendRecord(List<string> record)
    {
        Debug.Assert(_row == null);

        _row = record;
    }

    public List<string> GetFieldNames()
    {
        return _fieldNames;
    }

    public FieldMetaInfo[] GetFields()
    {
        return _fieldMeta;
    }

    public List<string> GetNextRecord(bool useCache = true)
    {
        Debug.Assert(_row != null);
        var row = _row;
        _row = null;
        return row;
    }

    public Tuple<Map<string, object>, string[]> GetNextRecordDict()
    {
        var values = GetNextRecord();
        if (values == null) return null;
        if (!values.Any()) return new Tuple<Map<string, object>, string[]>(new Map<string, object>(), new string[0]);
        if (_modelRecordEncoder == null)
        {
            _modelRecordEncoder = new ModelRecordEncoder(fields: GetFields(),
                aggregationPeriod: GetAggregationMonthAndSeconds());
        }
        return _modelRecordEncoder.Encode(values);
    }

    private TimeSpan? GetAggregationMonthAndSeconds()
    {
        return null;
    }
}

/// <summary>
/// Encodes metric data input rows  for consumption by OPF models. See the `ModelRecordEncoder.encode` method for more details.
/// </summary>
internal class ModelRecordEncoder
{
    private FieldMetaInfo[] _fields;
    private TimeSpan? _aggregationPeriod;
    private int _sequenceId;
    private List<string> _fieldNames;
    private int? _timestampFieldIndex;

    public ModelRecordEncoder(FieldMetaInfo[] fields, TimeSpan? aggregationPeriod = null)
    {
        if (fields == null || !fields.Any())
        {
            throw new ArgumentNullException("fields", "fields arg must be non-empty");
        }
        _fields = fields;
        _aggregationPeriod = aggregationPeriod;
        _sequenceId = -1;
        _fieldNames = fields.Select(m => m.name).ToList();

        _timestampFieldIndex = GetFieldIndexBySpecial(fields, SensorFlags.Timestamp);
    }

    private int? GetFieldIndexBySpecial(FieldMetaInfo[] fields, SensorFlags sensorFlags)
    {
        return fields.Select(t => t.special).ToList().IndexOf(sensorFlags);
    }

    /// <summary>
    /// Encodes the given input row as a dict, with the
    /// keys being the field names.This also adds in some meta fields:
    /// '_category': The value from the category field(if any)
    /// '_reset': True if the reset field was True(if any)
    /// '_sequenceId': the value from the sequenceId field(if any)
    /// </summary>
    /// <param name="inputRow">sequence of values corresponding to a single input metric data row</param>
    /// <returns></returns>
    public Tuple<Map<string, object>, string[]> Encode(List<string> inputRow)
    {
        var result = new Map<string, object>(ArrayUtils.Zip(_fieldNames, inputRow).ToDictionary(k => k.Get(0) as string, v => v.Get(1)));

        // TODO add the special field handling (category etc)
        if (_timestampFieldIndex.HasValue && _timestampFieldIndex >= 0)
        {
            result["_timestamp"] = inputRow[_timestampFieldIndex.Value];
            // Compute the record index based on timestamp
            result["_timestampRecordIdx"] = ComputeTimestampRecordIdx(inputRow[_timestampFieldIndex.Value]);
        }
        else
        {
            result["_timestamp"] = null;
        }

        result["_category"] = null;
        result["_reset"] = 0;
        result["_sequenceId"] = null;
        return new Tuple<Map<string, object>, string[]>(result, inputRow.ToArray());
    }

    private string ComputeTimestampRecordIdx(string recordTs)
    {
        if (_aggregationPeriod == null)
            return null;
        throw new NotImplementedException("check this");
    }
}

public class ModelDefinition
{
    public FieldMetaInfo[] inputRecordSchema;
    public ModelParams ModelParams { get; set; }

}

public class ModelCommand
{
    public Guid Id { get; set; }
    public string ModelId { get; set; }

    public ExperimentParameters Args { get; set; }
}

//public class ModelCommandArgs
//{
//    public ConfigModelDescription modelConfig { get; set; }
//    public InferenceArgsDescription inferenceArgs { get; set; }
//    public FieldMetaInfo[] inputRecordSchema { get; set; }
//}

public class ModelInferenceResult
{
    private double? anomalyScore;
    private long? rowId;
    private int? status;
    private Guid? commandId;

    public ModelInferenceResult(Guid? commandId = null, long? rowId = null, int? status = null, double? anomalyScore = null)
    {
        this.commandId = commandId;
        this.rowId = rowId;
        this.status = status;
        this.anomalyScore = anomalyScore;
    }
}

public class ModelSwapperUtils
{
    /// <summary>
    /// Dispatch command to create HTM model
    /// </summary>
    /// <param name="modelId"> unique identifier of the metric row</param>
    /// <param name="params">model params for creating a scalar model per ModelSwapper interface</param>
    public static void CreateHtmModel(string modelId, ExperimentParameters @params)
    {
        ModelSwapperInterface modelSwapper = new ModelSwapperInterface();
        modelSwapper.DefineModel(modelId: modelId, args: @params, commandId: Guid.NewGuid());
    }

    public static void DeleteHtmModel(string modelId)
    {
        ModelSwapperInterface modelSwapper = new ModelSwapperInterface();
        modelSwapper.DeleteModel(modelId: modelId, commandId: Guid.NewGuid());
    }
}

public class CheckPointManager
{
    private static Dictionary<string, ModelDefinition> _storedDefinitions = new Dictionary<string, ModelDefinition>();

    /// <summary>
    /// Retrieve a model instance from checkpoint.
    /// </summary>
    /// <param name="modelId">unique model ID</param>
    /// <returns>an OPF model instance</returns>
    public opf.Model Load(string modelId)
    {
        throw new ModelNotFound();
    }

    public ModelDefinition LoadModelDefinition(string modelId)
    {
        var definition = _storedDefinitions
            .Where(sd => sd.Key == modelId)
            .Select(sd => sd.Value)
            .FirstOrDefault();

        return definition;
    }

    public void Define(string modelId, ModelDefinition definition)
    {
        if (!_storedDefinitions.ContainsKey(modelId))
            _storedDefinitions.Add(modelId, definition);
    }

    /// <summary>
    /// Remove the model entry with the given model ID from storage
    /// </summary>
    /// <param name="modelId">model ID to remove</param>
    public void Remove(string modelId)
    {
        _storedDefinitions.Remove(modelId);
    }
}

#endregion