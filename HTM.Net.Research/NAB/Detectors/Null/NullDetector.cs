using System.Collections.Generic;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace HTM.Net.Research.NAB.Detectors.Null;

/// <summary>
/// This detector establishes a baseline score by recording a constant value for all data points.
/// </summary>
public class NullDetector : AnomalyDetector
{
    public NullDetector(IDataFile dataSet, float probationaryPercent) : base(dataSet, probationaryPercent)
    {
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        return new List<object> { 0.5 };
    }
}