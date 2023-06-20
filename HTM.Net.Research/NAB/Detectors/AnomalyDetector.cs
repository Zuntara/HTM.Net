using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Research.Swarming;
using HTM.Net.Util;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;

namespace HTM.Net.Research.NAB.Detectors;

public record AnomalyDetectorResult(double FinalScore, double RawScore, ModelResult OriginalModelResult);

public abstract class BaseAnomalyDetector : IDetector
{
    protected IDataFile DataSet { get; }
    protected double ProbationaryPeriod { get; }
    protected double InputMin { get; }
    protected double InputMax { get; }

    protected BaseAnomalyDetector(IDataFile dataSet, double probationaryPercent)
    {
        DataSet = dataSet;
        ProbationaryPeriod = Utils.getProbationPeriod(probationaryPercent, dataSet.Data.GetShape0());

        InputMin = dataSet.Data["value"].Min(v => v is double d ? d : double.Parse((string)v, NumberFormatInfo.InvariantInfo));
        InputMax = dataSet.Data["value"].Max(v => v is double d ? d : double.Parse((string)v, NumberFormatInfo.InvariantInfo));
    }

    /// <summary>
    /// Do anything to initialize your detector in before calling run.
    /// 
    /// Pooling across cores forces a pickling operation when moving objects from
    ///     the main core to the pool and this may not always be possible.This function
    /// allows you to create objects within the pool itself to avoid this issue.
    /// </summary>
    public virtual void Initialize()
    {
    }

    /// <summary>
    /// Returns a list of strings.Subclasses can add in additional columns per record.
    ///
    /// This method MAY be overridden to provide the names for those columns.
    /// </summary>
    /// <returns></returns>
    protected virtual string[] GetAdditionalHeaders()
    {
        return Array.Empty<string>();
    }

    /// <summary>
    /// Gets the outputPath and all the headers needed to write the results files.
    /// </summary>
    /// <returns></returns>
    protected virtual List<string> GetHeader()
    {
        List<string> header = new() { "timestamp", "value", "anomaly_score" };
        header.AddRange(GetAdditionalHeaders());
        return header;
    }
}

public abstract class AnomalyRxDetector<TRecord> : BaseAnomalyDetector, IRxDetector
{
    protected Subject<string> DataSubject = new Subject<string>();

    protected Subject<TRecord> RecordProcessed { get; } = new Subject<TRecord>();

    public Subject<DataFrame> AllRecordsProcessed { get; } = new Subject<DataFrame>();

    protected AnomalyRxDetector(IDataFile dataSet, double probationaryPercent)
    : base(dataSet, probationaryPercent)
    {
    }

    /// <summary>
    /// Returns a list[anomalyScore, *]. It is required that the first
    ///     element of the list is the anomalyScore.The other elements may
    ///     be anything, but should correspond to the names returned by
    /// getAdditionalHeaders().
    /// 
    /// This method MUST be overridden by subclasses
    /// </summary>
    /// <param name="inputData"></param>
    protected abstract void HandleRecord(Dictionary<string, object> inputData);

    public virtual void Run()
    {
        foreach (Dictionary<string, object> inputData in DataSet.Data.IterateRows())
        {
            HandleRecord(inputData);
        }

        this.DataSubject.OnCompleted();
    }

    protected virtual EncoderSettingsList SetupEncoderParams(EncoderSettingsList encoderParams)
    {
        encoderParams["timestamp_dayOfWeek"] = encoderParams.Pop("c0_dayOfWeek");
        encoderParams["timestamp_timeOfDay"] = encoderParams.Pop("c0_timeOfDay");
        encoderParams["timestamp_timeOfDay"]["fieldName"] = "timestamp";
        encoderParams["timestamp_timeOfDay"]["name"] = "timestamp";
        encoderParams["timestamp_weekend"] = encoderParams.Pop("c0_weekend");
        encoderParams["value"] = encoderParams.Pop("c1");
        encoderParams["value"]["fieldName"] = "value";
        encoderParams["value"]["name"] = "value";

        return encoderParams;
    }

    protected ExperimentParameters GetScalarMetricWithTimeOfDayAnomalyParams(
        List<double> metricData,
        double? minVal = null,
        double? maxVal = null,
        double? minResolution = null,
        string tmImplementation = "cpp")
    {
        // Default values
        if (minResolution == null)
        {
            minResolution = 0.001;
        }

        // Compute min and/or max from the data if not specified
        if (minVal == null || maxVal == null)
        {
            (double compMinVal, double compMaxVal) = RangeGen(metricData);
            if (minVal == null)
            {
                minVal = compMinVal;
            }

            if (maxVal == null)
            {
                maxVal = compMaxVal;
            }
        }

        // Handle the corner case where the incoming min and max are the same
        if (minVal == maxVal)
        {
            maxVal = minVal + 1;
        }

        // Load model parameters and update encoder params
        string paramFileRelativePath;
        ExperimentParameters paramSet;
        if (tmImplementation == "cpp")
        {
            paramFileRelativePath = Path.Combine(
                "anomaly_params_random_encoder",
                "best_single_metric_anomaly_params_cpp.json");
            paramSet = LoadSingleMetricAnomalyParams();
            FixupRandomEncoderParams(paramSet, minVal.Value, maxVal.Value, minResolution.Value);

            return paramSet;
        }
        else if (tmImplementation == "tm_cpp")
        {
            paramFileRelativePath = Path.Combine(
                "anomaly_params_random_encoder",
                "best_single_metric_anomaly_params_tm_cpp.json");
            paramSet = LoadSingleMetricAnomalyParamsTm();
            FixupRandomEncoderParams(paramSet, minVal.Value, maxVal.Value, minResolution.Value);
            return paramSet;
        }
        else
        {
            throw new InvalidOleVariantTypeException("Invalid string for tmImplementation. Try cpp or tm_cpp");
        }

        string json;
        using (Stream stream = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream(paramFileRelativePath))
        using (StreamReader reader = new StreamReader(stream))
        {
            json = reader.ReadToEnd();
        }

        paramSet = JsonConvert.DeserializeObject<ExperimentParameters>(json);
        FixupRandomEncoderParams(paramSet, minVal.Value, maxVal.Value, minResolution.Value);

        return paramSet;
    }

    private ExperimentParameters LoadSingleMetricAnomalyParams()
    {
        ExperimentParameters experimentParameters = ExperimentParameters.Default();

        experimentParameters.AggregationInfo = new AggregationSettings();
        experimentParameters.Model = "CLA-RX";
        experimentParameters.PredictAheadTime = null;
        experimentParameters.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, new Map<string, Map<string, object>>());
        experimentParameters.InferenceType = InferenceType.TemporalAnomaly;

        EncoderSettingsList encoders = experimentParameters.GetEncoderSettings();
        encoders["c0_timeOfDay"] = new EncoderSetting
        {
            encoderType = EncoderTypes.DateEncoder,
            type = EncoderTypes.DateEncoder,
            TimeOfDay = new TimeOfDayTuple(21, 9.49),
            Weekend = new WeekendTuple(0, 1.0),
            fieldName = "c0",
            name = "c0"
        };
        encoders["c1"] = new EncoderSetting
        {
            encoderType = EncoderTypes.RandomDistributedScalarEncoder,
            type = EncoderTypes.RandomDistributedScalarEncoder,
            fieldName = "c1",
            name = "c1",
            numBuckets = 130,
        };
        experimentParameters.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, encoders.AsMap());
        experimentParameters.SensorAutoReset = null;

        experimentParameters.EnableSpatialPooler = true;
        experimentParameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.8);
        experimentParameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
        experimentParameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);
        experimentParameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
        experimentParameters.SetParameterByKey(Parameters.KEY.SEED, 1956);
        experimentParameters.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 0);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.003);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.2);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.0005);

        experimentParameters.TrainSPNetOnlyIfRequested = false;
        experimentParameters.EnableTemporalMemory = true;
        experimentParameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 20);
        experimentParameters.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
        experimentParameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.24);
        experimentParameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 2048 });
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_SEGMENTS_PER_CELL, 128);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_SYNAPSES_PER_SEGMENT, 32);
        experimentParameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 13);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 31);
        experimentParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.008);
        experimentParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.04);
        experimentParameters.SetParameterByKey(Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.001);
        experimentParameters.SetParameterByKey(Parameters.KEY.TM_ANOMALY_MODE, AnomalyMode.Raw);

        experimentParameters.EnableClassification = false;
        experimentParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, 0.035828933612157998);
        experimentParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_STEPS, new[] { 1 });

        experimentParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

        experimentParameters.Control = new ExperimentControl
        {
            InputRecordSchema = new FieldMetaInfo[]
            {
                new FieldMetaInfo("timestamp", FieldMetaType.DateTime, SensorFlags.T),
                new FieldMetaInfo("value", FieldMetaType.Float, SensorFlags.L),
            }
        };
        return experimentParameters;
    }

    private ExperimentParameters LoadSingleMetricAnomalyParamsTm()
    {
        ExperimentParameters experimentParameters = ExperimentParameters.Default();

        experimentParameters.AggregationInfo = new AggregationSettings();
        experimentParameters.Model = "CLA-RX";
        experimentParameters.PredictAheadTime = null;
        experimentParameters.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, new Map<string, Map<string, object>>());
        experimentParameters.InferenceType = InferenceType.TemporalAnomaly;

        EncoderSettingsList encoders = experimentParameters.GetEncoderSettings();
        encoders["c0_timeOfDay"] = new EncoderSetting
        {
            encoderType = EncoderTypes.DateEncoder,
            type = EncoderTypes.DateEncoder,
            TimeOfDay = new TimeOfDayTuple(21, 9.49),
            Weekend = new WeekendTuple(0, 1.0),
            fieldName = "c0",
            name = "c0"
        };
        encoders["c1"] = new EncoderSetting
        {
            encoderType = EncoderTypes.RandomDistributedScalarEncoder,
            type = EncoderTypes.RandomDistributedScalarEncoder,
            fieldName = "c1",
            name = "c1",
            numBuckets = 130,
        };
        experimentParameters.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, encoders.AsMap());
        experimentParameters.SensorAutoReset = null;

        experimentParameters.EnableSpatialPooler = true;
        experimentParameters.SetParameterByKey(Parameters.KEY.POTENTIAL_PCT, 0.8);
        experimentParameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[] { 2048 });
        experimentParameters.SetParameterByKey(Parameters.KEY.GLOBAL_INHIBITION, true);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_BOOST, 1.0);
        experimentParameters.SetParameterByKey(Parameters.KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, 40.0);
        experimentParameters.SetParameterByKey(Parameters.KEY.SEED, 1956);
        experimentParameters.SetParameterByKey(Parameters.KEY.SP_VERBOSITY, 0);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_ACTIVE_INC, 0.003);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_CONNECTED, 0.2);
        experimentParameters.SetParameterByKey(Parameters.KEY.SYN_PERM_INACTIVE_DEC, 0.0005);

        experimentParameters.TrainSPNetOnlyIfRequested = false;
        experimentParameters.EnableTemporalMemory = true;
        experimentParameters.SetParameterByKey(Parameters.KEY.ACTIVATION_THRESHOLD, 20);
        experimentParameters.SetParameterByKey(Parameters.KEY.CELLS_PER_COLUMN, 32);
        experimentParameters.SetParameterByKey(Parameters.KEY.INITIAL_PERMANENCE, 0.24);
        experimentParameters.SetParameterByKey(Parameters.KEY.INPUT_DIMENSIONS, new int[] { 2048 });
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_SEGMENTS_PER_CELL, 128);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_SYNAPSES_PER_SEGMENT, 128);
        experimentParameters.SetParameterByKey(Parameters.KEY.MIN_THRESHOLD, 13);
        experimentParameters.SetParameterByKey(Parameters.KEY.MAX_NEW_SYNAPSE_COUNT, 31);
        experimentParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_DECREMENT, 0.008);
        experimentParameters.SetParameterByKey(Parameters.KEY.PERMANENCE_INCREMENT, 0.04);
        experimentParameters.SetParameterByKey(Parameters.KEY.PREDICTED_SEGMENT_DECREMENT, 0.001);
        experimentParameters.SetParameterByKey(Parameters.KEY.TM_ANOMALY_MODE, AnomalyMode.Raw);

        experimentParameters.EnableClassification = false;
        experimentParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_ALPHA, 0.035828933612157998);
        experimentParameters.SetParameterByKey(Parameters.KEY.CLASSIFIER_STEPS, new[] { 1 });

        experimentParameters.SetParameterByKey(Parameters.KEY.ANOMALY_KEY_MODE, Anomaly.Mode.PURE);

        experimentParameters.Control = new ExperimentControl
        {
            InputRecordSchema = new FieldMetaInfo[]
            {
                new FieldMetaInfo("timestamp", FieldMetaType.DateTime, SensorFlags.T),
                new FieldMetaInfo("value", FieldMetaType.Float, SensorFlags.L),
            }
        };
        return experimentParameters;
    }

    private (double, double) RangeGen(List<double> data, double std = 1)
    {
        double dataStd = data.StandardDeviation();
        if (dataStd == 0)
        {
            dataStd = 1;
        }
        double minVal = data.Min() - std * dataStd;
        double maxVal = data.Max() + std * dataStd;
        return (minVal, maxVal);
    }

    private void FixupRandomEncoderParams(ExperimentParameters paramSet, double minVal, double maxVal, double minResolution)
    {
        var encodersDict = (paramSet).GetEncoderSettings();

        foreach (var encoder in encodersDict.Values)
        {
            if (encoder != null)
            {
                if (encoder.encoderType == EncoderTypes.RandomDistributedScalarEncoder)
                {
                    double resolution = Math.Max(minResolution, (maxVal - minVal) / encoder.numBuckets.GetValueOrDefault());
                    ((EncoderSetting)encodersDict["c1"]).resolution = resolution;
                }
            }
        }

        paramSet.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, encodersDict.AsMap());
    }
}

public abstract class AnomalyDetector : BaseAnomalyDetector, IDirectDetector
{
    protected AnomalyDetector(IDataFile dataSet, double probationaryPercent)
    : base(dataSet, probationaryPercent)
    {
    }

    /// <summary>
    /// Returns a list[anomalyScore, *]. It is required that the first
    ///     element of the list is the anomalyScore.The other elements may
    ///     be anything, but should correspond to the names returned by
    /// getAdditionalHeaders().
    /// 
    /// This method MUST be overridden by subclasses
    /// </summary>
    /// <param name="inputData"></param>
    /// <returns></returns>
    protected abstract List<object> HandleRecord(Dictionary<string, object> inputData);

    public virtual DataFrame Run()
    {
        var headers = GetHeader();

        var rows = new List<List<object>>();

        int i = 0;
        foreach (Dictionary<string, object> inputData in DataSet.Data.IterateRows())
        {
            var detectorValues = HandleRecord(inputData);

            var outputRow = inputData.Select(v => v.Value).ToList();
            outputRow.AddRange(detectorValues);

            rows.Add(outputRow);
            i++;
        }

        DataFrame frame = new DataFrame();
        frame.Populate(rows, headers);
        return frame;
    }
}