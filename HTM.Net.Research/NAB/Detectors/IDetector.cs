using System.Reactive.Subjects;

namespace HTM.Net.Research.NAB.Detectors;

public interface IRxDetector: IDetector
{
    void Initialize();

    void Run();

    /// <summary>
    /// Triggered when all the records are processed
    /// </summary>
    Subject<DataFrame> AllRecordsProcessed { get; }
}

public interface IDirectDetector : IDetector
{
    DataFrame Run();
}

public interface IDetector
{
    void Initialize();
}