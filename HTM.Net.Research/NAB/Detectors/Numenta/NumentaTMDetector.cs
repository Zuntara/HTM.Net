using System;
using System.Collections.Generic;
using HTM.Net.Algorithms;
using HTM.Net.Research.opf;
using HTM.Net.Research.Swarming.Descriptions;

namespace HTM.Net.Research.NAB.Detectors.Numenta;

public class NumentaTMDetector : NumentaDetector
{
    public NumentaTMDetector(IDataFile dataSet, float probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
    }

    public override void Initialize()
    {
        double rangePadding = Math.Abs(InputMax - InputMin) * 0.2;
        ExperimentParameters modelParams = (ExperimentParameters)GetScalarMetricWithTimeOfDayAnomalyParams(
            metricData: new List<double>() { 0.0 },
            minVal: this.InputMin - rangePadding,
            maxVal: this.InputMax + rangePadding,
            minResolution: 0.001,
            tmImplementation: "tm_cpp"
        );

        SetupEncoderParams(modelParams.GetEncoderSettings());

        model = (CLAModelRx)ModelFactory.Create(modelParams);
        model.EnableInference(new InferenceArgsDescription { predictedField = "value" });

        // Initialize the anomaly likelihood object
        var numentaLearningPeriod = (int)(Math.Floor(ProbationaryPeriod / 2.0f));
        this.anomalyLikelihood = new AnomalyLikelihood(
            claLearningPeriod: numentaLearningPeriod,
            estimationSamples: (int)ProbationaryPeriod - numentaLearningPeriod,
            windowSize: 100,
            useMovingAvg: true, isWeighted: true);
    }
}