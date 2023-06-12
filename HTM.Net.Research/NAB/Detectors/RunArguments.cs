using System.Collections.Generic;

namespace HTM.Net.Research.NAB.Detectors;

public record RunArguments(int count, IDetector detector, string detectorName, List<object> dataFrameRow, string resultsDir, string relativePath);