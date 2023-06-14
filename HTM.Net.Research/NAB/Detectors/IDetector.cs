using System.Reactive.Subjects;

namespace HTM.Net.Research.NAB.Detectors;

public interface IRxDetector: IDetector
{
    void Initialize();

    void Run();

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