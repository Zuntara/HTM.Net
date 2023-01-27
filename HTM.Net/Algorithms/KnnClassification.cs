using System;
using MathNet.Numerics.LinearAlgebra;

namespace HTM.Net.Algorithms;

[Serializable]
public class KnnClassification : Classification<object>
{
    private KnnInferResult _knnInferResult;
    private int _storedPatterns;

    public void SetInferResult(KnnInferResult result)
    {
        _knnInferResult = result;
    }

    public int? GetWinner()
    {
        return _knnInferResult.GetWinner();
    }

    public double[] GetCategoryDistances()
    {
        return _knnInferResult.GetCategoryDistances();
    }

    public Vector<double> GetInference()
    {
        return _knnInferResult.GetInference();
    }

    public Vector<double> GetProtoDistance()
    {
        return _knnInferResult.GetProtoDistance();
    }

    public void SetNumPatterns(int numPatterns)
    {
        _storedPatterns = numPatterns;
    }

    public int GetNumPatterns()
    {
        return _storedPatterns;
    }
}
