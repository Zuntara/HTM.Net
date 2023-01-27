using System.Collections.Generic;

namespace HTM.Net.Algorithms
{
    public interface IClassifier
    {
        /// <summary>
        /// Process one input sample.
        /// This method is called by outer loop code outside the nupic-engine. We
        /// use this instead of the nupic engine compute() because our inputs and
        /// outputs aren't fixed size vectors of reals.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="recordNum">Record number of this input pattern. Record numbers should
        /// normally increase sequentially by 1 each time unless there
        /// are missing records in the dataset. Knowing this information
        /// insures that we don't get confused by missing records.</param>
        /// <param name="classification">Map of the classification information:
        /// bucketIdx: index of the encoder bucket
        /// actValue:  actual value going into the encoder</param>
        /// <param name="patternNZ">list of the active indices from the output below</param>
        /// <param name="learn">if true, learn this sample</param>
        /// <param name="infer">if true, perform inference</param>
        /// <returns>dict containing inference results, there is one entry for each
        /// step in steps, where the key is the number of steps, and
        /// the value is an array containing the relative likelihood for
        /// each bucketIdx starting from bucketIdx 0.
        /// 
        /// There is also an entry containing the average actual value to
        /// use for each bucket. The key is 'actualValues'.
        /// 
        /// for example:
        /// {	
        /// 	1 :             [0.1, 0.3, 0.2, 0.7],
        /// 	4 :             [0.2, 0.4, 0.3, 0.5],
        /// 	'actualValues': [1.5, 3,5, 5,5, 7.6],
        /// }
        /// </returns>
        IClassification<T> Compute<T>(int recordNum, IDictionary<string, object> classification, int[] patternNonZero,
            bool learn, bool infer);

        int[] Steps { get; }
    }
}