using System;
using System.Collections.Generic;

namespace HTM.Net.Research.Generators
{
    public class SineGenerator
    {
        public static IEnumerable<double> GenerateSineWave(double samplingRate, int nrOfSamples, 
            double amplitude, double frequencyInHz)
        {
            for (int i = 0; i < nrOfSamples; i++)
            {
                double timeInSeconds = i /samplingRate;
                double sample = amplitude*Math.Sin(2*Math.PI*frequencyInHz*timeInSeconds);
                yield return sample;
            }
        }
    }
}