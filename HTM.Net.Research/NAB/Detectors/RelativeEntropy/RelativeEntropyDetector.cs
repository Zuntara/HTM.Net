using System;
using System.Collections.Generic;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.Statistics;

namespace HTM.Net.Research.NAB.Detectors.RelativeEntropy;

public class RelativeEntropyDetector : AnomalyDetector
{
    private List<double> util;
    private int N_bins;
    private int W;
    private double T;
    private int c_th;
    private int m;
    private double stepSize;
    private List<Histogram> P;
    private List<int> c;

    public RelativeEntropyDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        util = new List<double>();
        N_bins = 5;
        W = 52;
        T = ChiSquared.InvCDF(N_bins - 1, 0.99);
        c_th = 1;
        m = 0;
        stepSize = (InputMax - InputMin) / N_bins;
        P = new List<Histogram>();
        c = new List<int>();
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        double anomalyScore = 0.0;
        util.Add((double)inputData["value"]);

        if (stepSize != 0.0)
        {
            if (util.Count >= W)
            {
                List<double> util_current = util.GetRange(util.Count - W, W);
                List<double> B_current = new List<double>();
                foreach (double c in util_current)
                {
                    B_current.Add(Math.Ceiling((c - InputMin) / stepSize));
                }

                Histogram P_hat = new Histogram(B_current, N_bins, 0, N_bins);

                if (m == 0)
                {
                    P.Add(P_hat);
                    c.Add(1);
                    m = 1;
                }
                else
                {
                    int index = GetAgreementHypothesis(P_hat);

                    if (index != -1)
                    {
                        c[index]++;
                        if (c[index] <= c_th)
                        {
                            anomalyScore = 1.0;
                        }
                    }
                    else
                    {
                        anomalyScore = 1.0;
                        P.Add(P_hat);
                        c.Add(1);
                        m++;
                    }
                }
            }
        }

        return new List<object>() { anomalyScore };
    }

    private int GetAgreementHypothesis(Histogram P_hat)
    {
        int index = -1;
        double minEntropy = double.PositiveInfinity;
        for (int i = 0; i < m; i++)
        {
            double entropy = 2 * W * KL(P_hat, P[i]);
            if (entropy < T && entropy < minEntropy)
            {
                minEntropy = entropy;
                index = i;
            }
        }
        return index;
    }

    // KL divergence calculation function
    public static double KL(double[] p, double[] q)
    {
        double klDivergence = 0.0;
        for (int i = 0; i < p.Length; i++)
        {
            if (p[i] > 0.0 && q[i] > 0.0)
            {
                klDivergence += p[i] * Math.Log(p[i] / q[i]);
            }
        }
        return klDivergence;
    }

    public static double KL(Histogram p, Histogram q)
    {
        double klDivergence = 0.0;
        for (int i = 0; i < p.BucketCount; i++)
        {
            if (p[i].Count > 0.0 && q[i].Count > 0.0)
            {
                klDivergence += p[i].Count * Math.Log(p[i].Count / q[i].Count);
            }
        }
        return klDivergence;
    }
}

