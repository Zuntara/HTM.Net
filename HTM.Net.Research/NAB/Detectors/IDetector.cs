namespace HTM.Net.Research.NAB.Detectors;

public interface IDetector
{
    void Initialize();

    DataFrame Run();
}