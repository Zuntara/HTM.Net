using System.Collections.Generic;

namespace HTM.Net.Algorithms
{
    public interface IClassifier
    {
        Classification<T> Compute<T>(int recordNum, IDictionary<string, object> classification, int[] patternNonZero,
            bool learn, bool infer);

        int Verbosity { get; }

        double Alpha { get; set; }

        int[] Steps { get; }
    }
}