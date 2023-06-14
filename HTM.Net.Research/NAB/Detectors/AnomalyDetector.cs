using HTM.Net.Research.opf;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reactive.Subjects;

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

    public abstract void Run();
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