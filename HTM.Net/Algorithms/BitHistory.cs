using System;
using System.Collections.Generic;
using System.Text;
using HTM.Net.Util;
using Newtonsoft.Json;

namespace HTM.Net.Algorithms
{
    /**
 * Stores an activationPattern bit history.
 * 
 * @author David Ray
 * @see CLAClassifier
 */
    public class BitHistory
    {
        /** Store reference to the classifier */
        [JsonIgnore]
        CLAClassifier classifier;
        /** Form our "id" */
        string id;
        /**
         * Dictionary of bucket entries. The key is the bucket index, the
         * value is the dutyCycle, which is the rolling average of the duty cycle
         */
        List<double> stats;
        /** lastUpdate is the iteration number of the last time it was updated. */
        int lastTotalUpdate = -1;

        // This determines how large one of the duty cycles must get before each of the
        // duty cycles are updated to the current iteration.
        // This must be less than float32 size since storage is float32 size
        private const int DUTY_CYCLE_UPDATE_INTERVAL = int.MaxValue;


        /**
         * Package protected constructor for serialization purposes.
         */
        BitHistory() { }

        /**
         * Constructs a new {@code BitHistory}
         * 
         * @param classifier	instance of the {@link CLAClassifier} that owns us
         * @param bitNum		activation pattern bit number this history is for,
         *                  	used only for debug messages
         * @param nSteps		number of steps of prediction this history is for, used
         *                  	only for debug messages
         */
        public BitHistory(CLAClassifier classifier, int bitNum, int nSteps)
        {
            this.classifier = classifier;
            this.id = string.Format("{0}[{1}]", bitNum, nSteps);
            this.stats = new List<double>();
        }

        /**
         * Store a new item in our history.
         * <p>
         * This gets called for a bit whenever it is active and learning is enabled
         * <p>
         * Save duty cycle by normalizing it to the same iteration as
         * the rest of the duty cycles which is lastTotalUpdate.
         * <p>
         * This is done to speed up computation in inference since all of the duty
         * cycles can now be scaled by a single number.
         * <p>
         * The duty cycle is brought up to the current iteration only at inference and
         * only when one of the duty cycles gets too large (to avoid overflow to
         * larger data type) since the ratios between the duty cycles are what is
         * important. As long as all of the duty cycles are at the same iteration
         * their ratio is the same as it would be for any other iteration, because the
         * update is simply a multiplication by a scalar that depends on the number of
         * steps between the last update of the duty cycle and the current iteration.
         * 
         * @param iteration		the learning iteration number, which is only incremented
         *             			when learning is enabled
         * @param bucketIdx		the bucket index to store
         */
        public void Store(int iteration, int bucketIdx)
        {
            // If lastTotalUpdate has not been set, set it to the current iteration.
            if (lastTotalUpdate == -1)
            {
                lastTotalUpdate = iteration;
            }

            // Get the duty cycle stored for this bucket.
            int statsLen = stats.Count - 1;
            if (bucketIdx > statsLen)
            {
                stats.AddRange(new double[bucketIdx - statsLen]);
            }

            // Update it now.
            // duty cycle n steps ago is dc{-n}
            // duty cycle for current iteration is (1-alpha)*dc{-n}*(1-alpha)**(n)+alpha
            double dc = stats[bucketIdx];

            // To get the duty cycle from n iterations ago that when updated to the
            // current iteration would equal the dc of the current iteration we simply
            // divide the duty cycle by (1-alpha)**(n). This results in the formula
            // dc'{-n} = dc{-n} + alpha/(1-alpha)**n where the apostrophe symbol is used
            // to denote that this is the new duty cycle at that iteration. This is
            // equivalent to the duty cycle dc{-n}
            double denom = Math.Pow((1.0 - classifier.Alpha), (iteration - lastTotalUpdate));

            double dcNew = 0;
            if (denom > 0) dcNew = dc + (classifier.Alpha / denom);

            // This is to prevent errors associated with infinite rescale if too large
            if (denom == 0 || dcNew > DUTY_CYCLE_UPDATE_INTERVAL)
            {
                double exp = Math.Pow((1.0 - classifier.Alpha), (iteration - lastTotalUpdate));
                double dcT = 0;
                for (int i = 0; i < stats.Count; i++)
                {
                    dcT *= exp;
                    stats[i]= dcT;
                }

                // Reset time since last update
                lastTotalUpdate = iteration;

                // Add alpha since now exponent is 0
                dc = stats[bucketIdx] + classifier.Alpha;
            }
            else {
                dc = dcNew;
            }

            stats[bucketIdx]= dc;
            if (classifier.Verbosity >= 2)
            {
                Console.WriteLine(string.Format("updated DC for{0},  bucket {1} to {2}", id, bucketIdx, dc));
            }
        }

        /**
         * Look up and return the votes for each bucketIdx for this bit.
         * 
         * @param iteration		the learning iteration number, which is only incremented
         *             			when learning is enabled
         * @param votes			array, initialized to all 0's, that should be filled
         *             			in with the votes for each bucket. The vote for bucket index N
         *             			should go into votes[N].
         */
        public void Infer(int iteration, double[] votes)
        {
            // Place the duty cycle into the votes and update the running total for
            // normalization
            double total = 0;
            for (int i = 0; i < stats.Count; i++)
            {
                double dc = stats[i];
                if (dc > 0.0)
                {
                    votes[i] = dc;
                    total += dc;
                }
            }

            // Experiment... try normalizing the votes from each bit
            if (total > 0)
            {
                double[] temp = ArrayUtils.Divide(votes, total);
                for (int i = 0; i < temp.Length; i++) votes[i] = temp[i];
            }

            if (classifier.Verbosity >= 2)
            {
                Console.WriteLine(string.Format("bucket votes for {0}:{1}", id, PFormatArray(votes)));
            }
        }

        /**
         * Return a string with pretty-print of an array using the given format
         * for each element
         * 
         * @param arr
         * @return
         */
        private string PFormatArray(double[] arr)
        {
            StringBuilder sb = new StringBuilder("[ ");
            foreach (double d in arr)
            {
                sb.Append(string.Format("{0:0.00}", d));
            }
            sb.Append(" ]");
            return sb.ToString();
        }
    }
}