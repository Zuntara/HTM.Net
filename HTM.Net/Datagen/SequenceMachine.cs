using System.Collections.Generic;

namespace HTM.Net.Datagen
{
    public class SequenceMachine
    {
        private PatternMachine patternMachine;

        public readonly static HashSet<int> NONE = new HashSet<int>();

        public SequenceMachine(PatternMachine pMachine)
        {
            patternMachine = pMachine;
        }

        /// <summary>
        /// Generate a sequence from a list of numbers.
        /// </summary>
        /// <param name="numbers"></param>
        /// <returns></returns>
        public List<HashSet<int>> generateFromNumbers(List<int> numbers)
        {
            List<HashSet<int>> sequence = new List<HashSet<int>>();
            foreach (int i in numbers)
            {
                if (i == -1)
                {
                    sequence.Add(NONE);
                }
                else {
                    HashSet<int> pattern = patternMachine.get(i);
                    sequence.Add(pattern);
                }
            }

            return sequence;
        }

        /**
         * Add spatial noise to each pattern in the sequence.
         * 
         * @param sequence      List of patterns
         * @param amount        Amount of spatial noise
         * @return  Sequence with noise added to each non-empty pattern
         */
        public List<HashSet<int>> addSpatialNoise(List<HashSet<int>> sequence, double amount)
        {
            List<HashSet<int>> newSequence = new List<HashSet<int>>();

            for (int index = 0; index < sequence.Count; index++)
            {
                HashSet<int> pattern = sequence[index];
                if (!pattern.SetEquals(NONE))
                {
                    pattern = patternMachine.addNoise(pattern, amount);
                }
                newSequence.Add(pattern);
            }

            return newSequence;
        }

        /**
         * Pretty print a sequence.
         * 
         * @param sequence      the sequence of numbers to print
         * @param verbosity     the extent of output chatter
         * @return
         */
        public string prettyPrintSequence(List<HashSet<int>> sequence, int verbosity)
        {
            string text = "";

            for (int i = 0; i < sequence.Count; i++)
            {
                HashSet<int> pattern = sequence[i];
                if (pattern == NONE)
                {
                    text += "<reset>";
                    if (i < sequence.Count - 1)
                    {
                        text += "\n";
                    }
                }
                else {
                    text += patternMachine.prettyPrintPattern(pattern, verbosity);
                }
            }
            return text;
        }
    }
}