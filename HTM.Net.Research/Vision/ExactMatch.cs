using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Runtime.InteropServices;
using HTM.Net.Util;

namespace HTM.Net.Research.Vision
{
    /// <summary>
    /// This classifier builds a list of SDRs and their associated categories.
    /// When queried for the category of an SDR it returns the first category in the list that has a matching SDR.
    /// </summary>
    public class ExactMatch
    {
        /// <summary>
        /// This classifier has just two things to keep track off:
        /// - A list of the known categories 
        /// - A list of the SDRs associated with each category
        /// </summary>
        public ExactMatch()
        {
            SDRs = new List<int[]>();
            Categories = new Map<int, List<string>>();
        }

        public void Learn(int[] inputPattern, string inputCategory, bool isSparse = false)
        {
            if (!SDRs.Any(ip => ip.SequenceEqual(inputPattern)))
            {
                SDRs.Add(inputPattern);
                Categories.Add(Categories.Count, new List<string> { inputCategory });
            }
            else
            {
                Categories[SDRs.FindIndex(ip => ip.SequenceEqual(inputPattern))].Add(inputCategory);
            }
        }

        public Tuple Infer(int[] inputPattern)
        {
            if (!SDRs.Any(ip => ip.SequenceEqual(inputPattern)))
            {
                string winner = Categories[SDRs.FindIndex(ip => ip.SequenceEqual(inputPattern))].First();
                // format return values to match KNNClassifier
                return new Tuple(winner);
            }
            return null;
        }

        private List<int[]> SDRs { get; set; }
        private Map<int, List<string>> Categories { get; set; }
    }
}