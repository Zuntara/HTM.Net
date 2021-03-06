﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace HTM.Net.Datagen
{
    /**
 * Generates test data for use with KNNClassifierIntegrationTest for
 * PCAKNN short.
 * 
 * @author cogmission
 */
    public class PCAKNNData
    {

        private static readonly List<object> PcaShortList;

        static PCAKNNData()
        {
            PcaShortList = GenerateForPcaknnShort();
        }

        private readonly KNNDataArray _trainData;
        private readonly KNNDataArray _testData;

        public PCAKNNData()
        {
            _trainData = new KNNDataArray(
                (double[][])PcaShortList[0], (int[])PcaShortList[1]);
            _testData = new KNNDataArray(
                (double[][])PcaShortList[2], (int[])PcaShortList[3]);
        }

        public static List<object> GenerateForPcaknnShort()
        {
            string[] files =
            {
                "train_pcaknnshort_data.txt",
                "train_pcaknnshort_class.txt",
                "test_pcaknnshort_data.txt",
                "test_pcaknnshort_class.txt"
            };

            List<object> retVal = new List<object>();
            bool isClass = false;
            foreach (string file in files)
            {
                string path = file;
                isClass = path.IndexOf("class", StringComparison.Ordinal) != -1;

                string[] l = null;
                try
                {
                    l = File.ReadAllLines(Path.GetFullPath(path), Encoding.UTF8);
                }
                catch (IOException e)
                {
                    Console.WriteLine(e);
                }

                if (isClass)
                {
                    int[] outer = new int[l.Length];
                    int i = 0;
                    foreach (string line in l)
                    {
                        outer[i++] = (int)double.Parse(line.Trim(), NumberFormatInfo.InvariantInfo);
                    }

                    retVal.Add(outer);
                }
                else {
                    double[][] outer = new double[l.Length][];
                    int i = 0;
                    foreach (string line in l)
                    {
                        string[] la = Regex.Split(line, "[\\s]+");// line.split("[\\s]+");
                        double[] dla = new double[la.Length];
                        int j = 0;
                        foreach (string s in la)
                        {
                            dla[j++] = double.Parse(s, NumberFormatInfo.InvariantInfo);
                        }
                        outer[i++] = dla;
                    }

                    retVal.Add(outer);
                }
            }

            return retVal;
        }

        /**
         * Returns a list of data arrays generated by the Python code
         * and saved in a file for testing exact calculations.
         * @return  list of data arrays
         */
        public KNNDataArray[] GetPcaKNNShortData()
        {
            return new KNNDataArray[] { _trainData, _testData };
        }

        public static void main(string[] args)
        {
            new PCAKNNData();
        }
    }
}