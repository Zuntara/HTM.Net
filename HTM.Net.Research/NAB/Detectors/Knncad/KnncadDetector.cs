using System;
using System.Collections.Generic;
using System.Linq;
using MathNet.Numerics;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace HTM.Net.Research.NAB.Detectors.Knncad;

class KnncadDetector : AnomalyDetector
{
    private List<double> _inputRowBuffer;
    private List<double[]> training;
    private List<double[]> calibration;
    private List<double> scores;
    private int recordCount;
    private double pred;
    private int k;
    private int dim;
    private Matrix<double> sigma;

    public KnncadDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        _inputRowBuffer = new List<double>();
        training = new List<double[]>();
        calibration = new List<double[]>();
        scores = new List<double>();
        recordCount = 0;
        pred = -1.0;
        k = 27;
        dim = 19;
        sigma = DenseMatrix.CreateIdentity(dim);
    }

    private double Metric(double[] a, double[] b)
    {
        var diff = Vector<double>.Build.DenseOfArray(a) - Vector<double>.Build.DenseOfArray(b);
        return diff * sigma * diff;
    }

    private double Ncm(double[] item, bool itemInArray = false)
    {
        var arr = training.Select(x => Metric(x, item)).ToList();
        arr.Sort();
        return arr.Take(k + (itemInArray ? 1 : 0)).Sum();
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        //var inputRow = new double[] { ((DateTime)inputData["timestamp"]).ToOADate(), (double)inputData["value"] };
        _inputRowBuffer.Add((double)inputData["value"]);

        recordCount++;

        if (_inputRowBuffer.Count < dim)
        {
            return new List<object>() { 0.0 };
        }
        else
        {
            var newItem = _inputRowBuffer.Skip(_inputRowBuffer.Count - dim).ToArray();
            if (recordCount < ProbationaryPeriod)
            {
                training.Add(newItem);
                return new List<object>() { 0.0 };
            }
            else
            {
                var ost = recordCount % ProbationaryPeriod;
                if (ost == 0 || ost == ProbationaryPeriod / 2)
                {
                    try
                    {
                        sigma = DenseMatrix.OfRows(training.ToArray()).TransposeThisAndMultiply(DenseMatrix.OfRows(training.ToArray())).Inverse();
                    }
                    catch (SingularUMatrixException)
                    {
                        Console.WriteLine("Singular Matrix at record " + recordCount);
                    }
                }
                if (scores.Count == 0)
                {
                    scores = training.Select(v => Ncm(v, true)).ToList();
                }

                var newScore = Ncm(newItem);
                var result = scores.Count(v => v < newScore) / (double)scores.Count;

                if (recordCount >= 2 * ProbationaryPeriod)
                {
                    training.RemoveAt(0);
                    training.Add(calibration[0]);
                    calibration.RemoveAt(0);
                }

                scores.RemoveAt(0);
                calibration.Add(newItem);
                scores.Add(newScore);

                if (pred > 0)
                {
                    pred--;
                    return new List<object>() { 0.5 };
                }
                else if (result >= 0.9965)
                {
                    pred = ProbationaryPeriod / 5.0;
                }

                return new List<object>() { result };
            }
        }
    }
}