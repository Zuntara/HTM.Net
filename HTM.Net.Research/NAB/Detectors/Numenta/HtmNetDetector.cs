using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json.Nodes;
using HTM.Net.Algorithms;
using HTM.Net.Data;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using log4net;
using log4net.Repository.Hierarchy;
using MathNet.Numerics.Statistics;
using Newtonsoft.Json;
using static HTM.Net.Parameters;
using static HTM.Net.Research.NAB.Scorer;

namespace HTM.Net.Research.NAB.Detectors.Numenta;

public class HtmNetDetector : AnomalyRxDetector<List<object>>
{
    private ExperimentParameters modelParams;
    private EncoderSetting sensorParams;
    protected AnomalyLikelihood anomalyLikelihood;
    private double? minVal, maxVal;

    private HTMModel model = null;
    private Subject<string> subject = new Subject<string>();


    private const double SPATIAL_TOLERANCE = 0.05;

    public HtmNetDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        
    }

    protected override string[] GetAdditionalHeaders()
    {
        return new string[] { "raw_score" };
    }

    public override void Initialize()
    {
        var rangePadding = Math.Abs(InputMax - InputMin) * 0.2;
        modelParams = GetScalarMetricWithTimeOfDayAnomalyParams(new List<double>() { 0 },
            InputMin - rangePadding,
            InputMax + rangePadding,
            0.001,
            "tm_cpp");

        var adapted = SetupEncoderParams(modelParams.GetEncoderSettings());
        modelParams.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, adapted.AsMap());

        var numentaLearningPeriod = (int)Math.Floor(ProbationaryPeriod / 2.0);
        anomalyLikelihood = new AnomalyLikelihood(
            useMovingAvg: true, windowSize: 100, isWeighted: true,
            claLearningPeriod: numentaLearningPeriod,
            estimationSamples: (int)(ProbationaryPeriod - numentaLearningPeriod),
            reestimationPeriod: 100);

        model = HTMModel.Run(modelParams, subject, OnRecordReceived, 0);

        var rows = new List<List<object>>();
        int i = 0;
        this.RecordProcessed.Subscribe(detectorValues =>
        {
            // We get the resulting scores from the detector
            Console.WriteLine("Adding row");
            var inputData = (Dictionary<string, object>)detectorValues[0];
            var outputRow = inputData.Select(v => v.Value).ToList();
            outputRow.Add(detectorValues[1]);
            outputRow.Add(detectorValues[2]);

            rows.Add(outputRow);
            i++;

            if (DataSet.Data.GetShape0() == i)
            {
                // Make sure we will finish up
                RecordProcessed.OnCompleted();
            }
        }, () =>
        {
            Console.WriteLine($"Completed {rows.Count} records");
            var headers = GetHeader();
            DataFrame frame = new DataFrame();
            frame.Populate(rows, headers);
            AllRecordsProcessed.OnNext(frame);
            AllRecordsProcessed.OnCompleted();
        });
    }

    private void OnRecordReceived(int index, double score, Dictionary<string,object> inputData)
    {
        var value = (double)inputData["value"];
        var rawScore = score;

        var spatialAnomaly = false;
        if (minVal != maxVal)
        {
            var tolerance = (maxVal - minVal) * SPATIAL_TOLERANCE;
            var maxExpected = maxVal + tolerance;
            var minExpected = minVal - tolerance;
            if (value > maxExpected || value < minExpected)
            {
                spatialAnomaly = true;
            }
        }
        if (maxVal == null || value > maxVal)
        {
            maxVal = value;
        }
        if (minVal == null || value < minVal)
        {
            minVal = value;
        }

        var anomalyScore = anomalyLikelihood.AnomalyProbability(value, rawScore, (DateTime)inputData["timestamp"]);
        var logScore = AnomalyLikelihood.ComputeLogLikelihood(anomalyScore);
        if (spatialAnomaly)
        {
            logScore = 1.0;
        }

        RecordProcessed.OnNext(new List<object> { inputData, logScore, rawScore });
    }

    protected override void HandleRecord(Dictionary<string, object> inputData)
    {
        var line = $"{inputData["timestamp"]},{inputData["value"]}\n";
        //model.StandardInput.WriteLine(line);
        subject.OnNext(line);

        //var value = Convert.ToDouble(inputData["value"]);

        //var result = model.StandardOutput.ReadLine();
        //var rawScore = Convert.ToDouble(result);

        //var spatialAnomaly = false;
        //if (minVal != maxVal)
        //{
        //    var tolerance = (maxVal - minVal) * SPATIAL_TOLERANCE;
        //    var maxExpected = maxVal + tolerance;
        //    var minExpected = minVal - tolerance;
        //    if (value > maxExpected || value < minExpected)
        //    {
        //        spatialAnomaly = true;
        //    }
        //}
        //if (maxVal == null || value > maxVal)
        //{
        //    maxVal = value;
        //}
        //if (minVal == null || value < minVal)
        //{
        //    minVal = value;
        //}

        //var anomalyScore = anomalyLikelihood.AnomalyProbability(value, rawScore, (DateTime)inputData["timestamp"]);
        //var logScore = AnomalyLikelihood.ComputeLogLikelihood(anomalyScore);
        //if (spatialAnomaly)
        //{
        //    logScore = 1.0;
        //}

        //return new List<object> { logScore, rawScore };
    }

    public override void Run()
    {
        foreach (Dictionary<string, object> inputData in DataSet.Data.IterateRows())
        {
            HandleRecord(inputData);
        }

        this.subject.OnCompleted();
    }

    public void Wait()
    {
        model.GetNetwork().GetTail().GetTail().GetLayerThread().Wait();
    }

    protected EncoderSettingsList SetupEncoderParams(EncoderSettingsList encoderParams)
    {
        encoderParams["timestamp_dayOfWeek"] = encoderParams.Pop("c0_dayOfWeek");
        encoderParams["timestamp_timeOfDay"] = encoderParams.Pop("c0_timeOfDay");
        encoderParams["timestamp_timeOfDay"]["fieldName"] = "timestamp";
        encoderParams["timestamp_timeOfDay"]["name"] = "timestamp";
        encoderParams["timestamp_weekend"] = encoderParams.Pop("c0_weekend");
        encoderParams["value"] = encoderParams.Pop("c1");
        encoderParams["value"]["fieldName"] = "value";
        encoderParams["value"]["name"] = "value";

        sensorParams = encoderParams["value"];

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

public class HTMModel
{
    protected static readonly ILog LOGGER = LogManager.GetLogger(typeof(HTMModel));

    private Network.Network network;
    private PublisherSupplier supplier;

    public HTMModel(ExperimentParameters modelParams)
    {
        LOGGER.Debug($"HTMModel({modelParams})");

        supplier = PublisherSupplier.GetBuilder()
            .AddHeader("timestamp,value")
            .AddHeader("datetime,float")
            .AddHeader("T,B")
            .Build();

        Parameters parameters = GetModelParameters(modelParams);

        LOGGER.Info("RUNNING WITH NO EXPLICIT P_RADIUS SET");

        network = Network.Network.Create("NAB Network", parameters)
            .Add(Network.Network.CreateRegion("NAB Region")
                .Add(Network.Network.CreateLayer("NAB Layer", parameters)
                    .Add(Anomaly.Create())
                    .Add(new TemporalMemory())
                    .Add(new SpatialPooler())
                    .Add(Sensor<ObservableSensor<string[]>>.Create(ObservableSensor<string[]>.Create,
                        SensorParams.Create(SensorParams.Keys.Obs, "Manual Input", supplier)))));
    }

    public Map<string, Map<string, object>> GetFieldEncodingMap(ExperimentParameters modelParams)
    {
        Map<string, Map<string, object>> fieldEncodings = new Map<string, Map<string, object>>();
        string fieldName;
        Map<string, object> fieldMap;
        var encoders = modelParams.GetEncoderSettings();
        LOGGER.Debug($"GetFieldEncodingMap({encoders})");
        foreach (var node in encoders.Values)
        {
            if (node == null)
                continue;

            fieldName = node.fieldName;
            if (!fieldEncodings.TryGetValue(fieldName, out fieldMap))
            {
                fieldMap = new Map<string, object>();
                fieldMap.Add("fieldName", fieldName);
                fieldEncodings.Add(fieldName, fieldMap);
            }
            fieldMap.Add("encoderType", node.encoderType);
            if (node.TimeOfDay != null)
            {
                var timeOfDay = node.TimeOfDay;
                fieldMap.Add("fieldType", "datetime");
                fieldMap.Add(KEY.DATEFIELD_PATTERN.GetFieldName(), "YYYY-MM-dd HH:mm:ss");
                fieldMap.Add(KEY.DATEFIELD_TOFD.GetFieldName(),
                    new TimeOfDayTuple(timeOfDay.BitsToUse, timeOfDay.Radius));
            }
            else
            {
                fieldMap.Add("fieldType", "float");
            }
            if (node.resolution != null)
            {
                fieldMap.Add("resolution", node.resolution);
            }
        }
        LOGGER.Debug($"GetFieldEncodingMap => {fieldEncodings}");
        return fieldEncodings;
    }

    public Parameters GetSpatialPoolerParams(ExperimentParameters modelParams)
    {
        Parameters p = Parameters.GetSpatialDefaultParameters();
        var spParams = modelParams;
        LOGGER.Debug($"GetSpatialPoolerParams({spParams})");
        if (spParams.Has(KEY.COLUMN_DIMENSIONS))
        {
            p.SetParameterByKey(KEY.COLUMN_DIMENSIONS, spParams.GetParameterByKey(KEY.COLUMN_DIMENSIONS));
        }
        if (spParams.Has(KEY.MAX_BOOST))
        {
            p.SetParameterByKey(KEY.MAX_BOOST, spParams.GetParameterByKey(KEY.MAX_BOOST));
        }
        if (spParams.Has(KEY.SYN_PERM_INACTIVE_DEC))
        {
            p.SetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC, spParams.GetParameterByKey(KEY.SYN_PERM_INACTIVE_DEC));
        }
        if (spParams.Has(KEY.SYN_PERM_CONNECTED))
        {
            p.SetParameterByKey(KEY.SYN_PERM_CONNECTED, spParams.GetParameterByKey(KEY.SYN_PERM_CONNECTED));
        }
        if (spParams.Has(KEY.SYN_PERM_ACTIVE_INC))
        {
            p.SetParameterByKey(KEY.SYN_PERM_ACTIVE_INC, spParams.GetParameterByKey(KEY.SYN_PERM_ACTIVE_INC));
        }
        if (spParams.Has(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA))
        {
            p.SetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA, spParams.GetParameterByKey(KEY.NUM_ACTIVE_COLUMNS_PER_INH_AREA));
        }
        if (spParams.Has(KEY.GLOBAL_INHIBITION))
        {
            p.SetParameterByKey(KEY.GLOBAL_INHIBITION, spParams.GetParameterByKey(KEY.GLOBAL_INHIBITION));
        }
        if (spParams.Has(KEY.POTENTIAL_PCT))
        {
            p.SetParameterByKey(KEY.POTENTIAL_PCT, spParams.GetParameterByKey(KEY.POTENTIAL_PCT));
        }
        if (spParams.Has(KEY.INPUT_DIMENSIONS))
        {
            p.SetParameterByKey(KEY.INPUT_DIMENSIONS, spParams.GetParameterByKey(KEY.INPUT_DIMENSIONS));
        }
        LOGGER.Debug($"GetSpatialPoolerParams => {p}");
        return p;
    }

    public Parameters GetTemporalMemoryParams(ExperimentParameters modelParams)
    {
        Parameters p = Parameters.GetTemporalDefaultParameters();
        ExperimentParameters tpParams = modelParams;
        LOGGER.Debug($"GetTemporalMemoryParams({tpParams})");
        if (tpParams.Has(KEY.COLUMN_DIMENSIONS))
        {
            p.SetParameterByKey(KEY.COLUMN_DIMENSIONS, tpParams.GetParameterByKey(KEY.COLUMN_DIMENSIONS));
        }
        if (tpParams.Has(KEY.ACTIVATION_THRESHOLD))
        {
            p.SetParameterByKey(KEY.ACTIVATION_THRESHOLD, tpParams.GetParameterByKey(KEY.ACTIVATION_THRESHOLD));
        }
        if (tpParams.Has(KEY.CELLS_PER_COLUMN))
        {
            p.SetParameterByKey(KEY.CELLS_PER_COLUMN, tpParams.GetParameterByKey(KEY.CELLS_PER_COLUMN));
        }
        if (tpParams.Has(KEY.PERMANENCE_INCREMENT))
        {
            p.SetParameterByKey(KEY.PERMANENCE_INCREMENT, tpParams.GetParameterByKey(KEY.PERMANENCE_INCREMENT));
        }
        if (tpParams.Has(KEY.MIN_THRESHOLD))
        {
            p.SetParameterByKey(KEY.MIN_THRESHOLD, tpParams.GetParameterByKey(KEY.MIN_THRESHOLD));
        }
        if (tpParams.Has(KEY.INITIAL_PERMANENCE))
        {
            p.SetParameterByKey(KEY.INITIAL_PERMANENCE, tpParams.GetParameterByKey(KEY.INITIAL_PERMANENCE));
        }
        if (tpParams.Has(KEY.MAX_SEGMENTS_PER_CELL))
        {
            p.SetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL, tpParams.GetParameterByKey(KEY.MAX_SEGMENTS_PER_CELL));
        }
        if (tpParams.Has(KEY.MAX_SYNAPSES_PER_SEGMENT))
        {
            p.SetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT, tpParams.GetParameterByKey(KEY.MAX_SYNAPSES_PER_SEGMENT));
        }
        if (tpParams.Has(KEY.PERMANENCE_DECREMENT))
        {
            p.SetParameterByKey(KEY.PERMANENCE_DECREMENT, tpParams.GetParameterByKey(KEY.PERMANENCE_DECREMENT));
        }
        if (tpParams.Has(KEY.PREDICTED_SEGMENT_DECREMENT))
        {
            p.SetParameterByKey(KEY.PREDICTED_SEGMENT_DECREMENT, tpParams.GetParameterByKey(KEY.PREDICTED_SEGMENT_DECREMENT));
        }
        if (tpParams.Has(KEY.MAX_NEW_SYNAPSE_COUNT))
        {
            p.SetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT, tpParams.GetParameterByKey(KEY.MAX_NEW_SYNAPSE_COUNT));
        }

        LOGGER.Debug($"GetTemporalMemoryParams => {p}");
        return p;
    }

    public Parameters GetSensorParams(ExperimentParameters modelParams)
    {
        ExperimentParameters sensorParams = modelParams;
        LOGGER.Debug($"GetSensorParams({sensorParams})");
       var fieldEncodings = GetFieldEncodingMap(sensorParams);
        Parameters p = Parameters.Empty();
        p.SetParameterByKey(KEY.CLIP_INPUT, true);
        p.SetParameterByKey(KEY.FIELD_ENCODING_MAP, fieldEncodings);

        LOGGER.Debug($"GetSensorParams => {p}");
        return p;
    }

    public Parameters GetModelParameters(ExperimentParameters parameters)
    {
        ExperimentParameters modelParams = parameters;
        LOGGER.Debug($"GetModelParameters({modelParams})");
        //Parameters p = Parameters.GetAllDefaultParameters()
        //    .Union(GetSpatialPoolerParams(modelParams))
        //    .Union(GetTemporalMemoryParams(modelParams))
        //    .Union(GetSensorParams(modelParams));
        Parameters p = parameters;


        // TODO https://github.com/numenta/htm.java/issues/482
        // if (spParams.has("seed")) {
        //     p.Set(KEY.SEED, spParams.Get("seed").AsInt());
        // }
        //p.SetParameterByKey(KEY.RANDOM, new UniversalRandom(42));
        // Setting the random above is done as a work-around to this.
        //p.Set(KEY.SEED, 42);

        LOGGER.Debug($"GetModelParameters => {p}");
        return p;
    }

    public Publisher GetPublisher()
    {
        return supplier.Get();
    }

    public Network.Network GetNetwork()
    {
        return network;
    }

    public void ShowDebugInfo()
    {
        Region region = network.GetHead();
        ILayer layer = region.Lookup("NAB Layer");
        Connections connections = layer.GetConnections();
        double[] cycles = connections.GetActiveDutyCycles();
        int spActive = 0;
        for (int i = 0; i < cycles.Length; i++)
        {
            if (cycles[i] > 0)
            {
                spActive++;
            }
        }
        LOGGER.Debug($"SP ActiveDutyCycles: {spActive}");
    }

    public static HTMModel Run(ExperimentParameters parameters, IObservable<string> inputs, Action<int, double, Dictionary<string,object>> resultCallback, int headerSkipLines=0)
    {
        HTMModel model = new HTMModel(parameters);

        Network.Network network = model.GetNetwork();
        network.Observe().Subscribe(i =>
            {
                double score = i.GetAnomalyScore();
                int record = i.GetRecordNum();
                var input = i.GetClassifierInput()
                    .ToDictionary(ci => ci.Key, ci => ci.Value["inputValue"]);

                LOGGER.Debug($"Anomaly score: {score} for record {record}");

                resultCallback(record, score, input);
            }, e =>
            {
                LOGGER.Error("Error processing data", e);
            },
            () =>
            {
                LOGGER.Debug("Done processing data");
                if (LOGGER.IsDebugEnabled)
                {
                    model.ShowDebugInfo();
                }
            });

        network.Start();

        // Pipe data to network
        Publisher publisher = model.GetPublisher();

        int skip = headerSkipLines;
        inputs.Subscribe(line =>
        {
            if (skip > 0)
            {
                skip--;
                return;
            }

            publisher.OnNext(line);
        }, () =>
        {
            publisher.OnComplete();
        });

        return model;
    }
}