using System;
using System.Collections.Generic;
using System.Linq;
using HTM.Net.Algorithms;
using HTM.Net.Encoders;
using HTM.Net.Model;
using HTM.Net.Network;
using HTM.Net.Network.Sensor;
using HTM.Net.Research.Swarming.Descriptions;
using log4net;

namespace HTM.Net.Research.NAB.Detectors.Numenta;

public class HtmNetDetector : AnomalyRxDetector<List<object>>
{
    private ExperimentParameters modelParams;
    private EncoderSetting sensorParams;
    protected AnomalyLikelihood anomalyLikelihood;
    private double? minVal, maxVal;

    private HTMModel model = null;


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

        model = HTMModel.Run(modelParams, DataSubject, OnRecordReceived, 0);

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
            model.GetNetwork().Halt();
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
        DataSubject.OnNext(line);
    }

    protected override EncoderSettingsList SetupEncoderParams(EncoderSettingsList encoderParams)
    {
        encoderParams = base.SetupEncoderParams(encoderParams);

        sensorParams = encoderParams["value"];

        return encoderParams;
    }

    public void Wait()
    {
        model.GetNetwork().GetTail().GetTail().GetLayerThread().Wait();
        DataSubject.OnCompleted();
    }
}

public class HTMModel
{
    protected static readonly ILog LOGGER = LogManager.GetLogger(typeof(HTMModel));

    private Network.Network network;
    private Publisher publisher;

    public HTMModel(ExperimentParameters modelParams)
    {
        LOGGER.Debug($"HTMModel({modelParams})");

        publisher = Publisher.GetBuilder()
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
                        SensorParams.Create(SensorParams.Keys.Obs, "Manual Input", publisher)))));
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
        return publisher;
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
                Console.WriteLine("Done processing data");
                if (LOGGER.IsDebugEnabled)
                {
                    model.ShowDebugInfo();
                }
            });

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
            Console.WriteLine("OnCompleted of input received, giving to publisher");
            publisher.OnComplete();
        });

        network.Start();

        return model;
    }
}