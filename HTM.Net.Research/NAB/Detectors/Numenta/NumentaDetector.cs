using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;
using HTM.Net.Util;
using System.Collections.Generic;
using System;
using System.Linq;

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

    public void Wait()
    {
        model.WaitNetwork();
    }
}