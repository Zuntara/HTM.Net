using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System;
using System.Linq;
using System.Reactive.Subjects;
using HTM.Net.Data;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming;
using MathNet.Numerics.Statistics;
using System.Reflection.PortableExecutable;

namespace HTM.Net.Research.NAB.Detectors.Numenta;

/// <summary>
/// This detector uses an HTM based anomaly detection technique.
/// </summary>
public class NumentaDetector : AnomalyRxDetector<AnomalyDetectorResult>
{
    protected CLAModelRx model;
    private readonly bool useLikelihood;
    protected AnomalyLikelihood anomalyLikelihood;
    private EncoderSetting sensorParams;
    private double? minVal, maxVal;


    public NumentaDetector(IDataFile dataSet, double probationaryPercent)
    : base(dataSet, probationaryPercent)
    {
        this.model = null;
        this.sensorParams = null;
        this.anomalyLikelihood = null;
        // Keep track of value range for spatial anomaly detection
        this.minVal = null;
        this.maxVal = null;

        // Set this to False if you want to get results based on raw scores
        // without using AnomalyLikelihood. This will give worse results, but
        // useful for checking the efficacy of AnomalyLikelihood. You will need
        // to re-optimize the thresholds when running with this setting.
        this.useLikelihood = true;
    }

    protected override string[] GetAdditionalHeaders()
    {
        return new[] { "raw_score" };
    }

    public const float SPATIAL_TOLERANCE = 0.05f;

    /// <summary>
    /// Returns a tuple (anomalyScore, rawScore).
    /// Internally to NuPIC "anomalyScore" corresponds to "likelihood_score"
    /// and "rawScore" corresponds to "anomaly_score". Sorry about that.
    /// </summary>
    /// <param name="inputData"></param>
    /// <returns></returns>
    /// <exception cref="NotImplementedException"></exception>
    protected override void HandleRecord(Dictionary<string, object> inputData)
    {
        // Send it to Numenta detector and get back the results
        this.model.Run((new Map<string, object>(inputData), inputData.Keys.ToArray()));

        //// Get the value
        //var value = (double)inputData["value"];

        //// Retrieve the anomaly score and write it to a file
        //var rawScore = (double)result.inferences[InferenceElement.AnomalyScore];

        //// Update min/max values and check if there is a spatial anomaly
        //bool spatialAnomaly = false;
        //if (this.minVal != this.maxVal)
        //{
        //    var tolerance = (this.maxVal - this.minVal) * SPATIAL_TOLERANCE;
        //    var maxExpected = this.maxVal + tolerance;
        //    var minExpected = this.minVal - tolerance;
        //    if (value > maxExpected || value < minExpected)
        //    {
        //        spatialAnomaly = true;
        //    }
        //}

        //if (this.maxVal is null || value > this.maxVal)
        //{
        //    this.maxVal = value;
        //}
        //if (this.minVal is null || value < this.minVal)
        //{
        //    this.minVal = value;
        //}

        //float finalScore;
        //if (this.useLikelihood)
        //{
        //    // Compute log(anomaly likelihood)
        //    var anomalyScore = this.anomalyLikelihood.AnomalyProbability(
        //        (double)inputData["value"], rawScore, (DateTime)inputData["timestamp"]);
        //    var logScore = AnomalyLikelihood.ComputeLogLikelihood(anomalyScore);
        //    finalScore = (float)logScore;
        //}
        //else
        //{
        //    finalScore = (float)rawScore;
        //}

        //if (spatialAnomaly)
        //{
        //    finalScore = 1.0f;
        //}

        //return new List<object>
        //{
        //    finalScore, rawScore
        //};
    }

    private void PostProcessRecord(ModelResult result)
    {
        // Get the value
        var value = (double)result.rawInput["value"];

        // Retrieve the anomaly score and write it to a file
        var rawScore = (double)result.inferences[InferenceElement.AnomalyScore];

        // Update min/max values and check if there is a spatial anomaly
        bool spatialAnomaly = false;
        if (this.minVal != this.maxVal)
        {
            var tolerance = (this.maxVal - this.minVal) * SPATIAL_TOLERANCE;
            var maxExpected = this.maxVal + tolerance;
            var minExpected = this.minVal - tolerance;
            if (value > maxExpected || value < minExpected)
            {
                spatialAnomaly = true;
            }
        }

        if (this.maxVal is null || value > this.maxVal)
        {
            this.maxVal = value;
        }
        if (this.minVal is null || value < this.minVal)
        {
            this.minVal = value;
        }

        float finalScore;
        if (this.useLikelihood)
        {
            // Compute log(anomaly likelihood)
            var anomalyScore = this.anomalyLikelihood.AnomalyProbability(
                (double)result.rawInput["value"], rawScore, (DateTime)result.rawInput["timestamp"]);
            var logScore = AnomalyLikelihood.ComputeLogLikelihood(anomalyScore);
            finalScore = (float)logScore;
        }
        else
        {
            finalScore = (float)rawScore;
        }

        if (spatialAnomaly)
        {
            finalScore = 1.0f;
        }

        RecordProcessed.OnNext(new AnomalyDetectorResult(finalScore, rawScore, result));
    }

    public override void Initialize()
    {
        double rangePadding = Math.Abs(InputMax - InputMin) * 0.2;
        ExperimentParameters modelParams = GetScalarMetricWithTimeOfDayAnomalyParams(
            metricData: new List<double>() { 0.0 },
            minVal: this.InputMin - rangePadding,
            maxVal: this.InputMax + rangePadding,
            minResolution: 0.001,
            tmImplementation: "cpp"
        );

        var adapted = SetupEncoderParams(modelParams.GetEncoderSettings());
        modelParams.SetParameterByKey(Parameters.KEY.FIELD_ENCODING_MAP, adapted.AsMap());

        model = (CLAModelRx)ModelFactory.Create(modelParams);
        model.EnableInference(new InferenceArgsDescription { predictedField = "value" });
        
        // Subscribe the handling of the records
        model.HandleRecord.Subscribe(result =>
        {
            PostProcessRecord(result);
        }, () =>
        {
            AllRecordsProcessed.OnCompleted();
        });

        var rows = new List<List<object>>();
        int i = 0;
        this.RecordProcessed.Subscribe(detectorValues =>
        {
            Console.WriteLine("Adding row");
            var inputData = detectorValues.OriginalModelResult.rawInput;
            var outputRow = inputData.Select(v => v.Value).ToList();
            outputRow.Add(detectorValues.FinalScore);
            outputRow.Add(detectorValues.RawScore);

            rows.Add(outputRow);
            i++;

            if (DataSet.Data.GetShape0() == i)
            {
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

        if (this.useLikelihood)
        {
            // Initialize the anomaly likelihood object
            var numentaLearningPeriod = (int)(Math.Floor(ProbationaryPeriod / 2.0f));
            this.anomalyLikelihood = new AnomalyLikelihood(
                claLearningPeriod: numentaLearningPeriod,
                estimationSamples: (int)ProbationaryPeriod - numentaLearningPeriod,
                windowSize: 100,
                useMovingAvg: true, isWeighted: true);
        }
    }

    public override void Run()
    {
        this.model.StartNetwork(DataSet.Data.GetShape0());

        foreach (Dictionary<string, object> inputData in DataSet.Data.IterateRows())
        {
            HandleRecord(inputData);
        }

        this.model.Complete();
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
        experimentParameters.SetParameterByKey(Parameters.KEY.COLUMN_DIMENSIONS, new int[]{2048});
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

    public void Wait()
    {
        model.WaitNetwork();
    }
}