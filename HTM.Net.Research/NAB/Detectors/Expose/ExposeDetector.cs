using System;
using System.Collections.Generic;
using HTM.Net.Util;
using MathNet.Numerics.Distributions;
using MathNet.Numerics.LinearAlgebra;
using MathNet.Numerics.LinearAlgebra.Double;

namespace HTM.Net.Research.NAB.Detectors.Expose;

class ExposeDetector : AnomalyDetector
{
    private Matrix<double> previousExposeModel;
    private double decay;
    private int timestep;
    private RBFSampler kernel;

    public ExposeDetector(IDataFile dataSet, double probationaryPercent)
        : base(dataSet, probationaryPercent)
    {
        previousExposeModel = null;
        decay = 0.01;
        timestep = 0;
        kernel = null;
    }

    public override void Initialize()
    {
        double gamma = 0.5;
        int nComponents = 20000;
        int randomSeed = 290;

        kernel = new RBFSampler(gamma, nComponents, randomSeed);
    }

    protected override List<object> HandleRecord(Dictionary<string, object> inputData)
    {
        // Transform the input by approximating feature map of a Radial Basis
        // Function kernel using Random Kitchen Sinks approximation
        Matrix<double> inputFeature = kernel.FitTransform(Matrix<double>.Build.DenseOfArray(new double[,] { { (double)inputData["value"] } }));

        // Compute expose model as a weighted sum of new data point's feature
        // map and previous data points' kernel embedding. Influence of older data
        // points declines with the decay factor.
        Matrix<double> exposeModel;
        if (timestep == 0)
        {
            exposeModel = inputFeature;
        }
        else
        {
            exposeModel = decay * inputFeature + (1 - decay) * previousExposeModel;
        }

        // Update previous expose model
        previousExposeModel = exposeModel;

        // Compute anomaly score by calculating similarity of the new data point
        // with expose model. The similarity measure, calculated via inner
        // product, is the likelihood of the data point being normal. Resulting
        // anomaly scores are in the range of -0.02 to 1.02.
        double anomalyScore = 1 - inputFeature.Multiply(exposeModel.Transpose())[0, 0];
        timestep++;

        return new List<object> { anomalyScore };
    }
}

public class RBFSampler
{
    private double gamma;
    private int nComponents;
    private int randomState;
    private Normal normalDistribution;

    public RBFSampler(double gamma = 1.0, int nComponents = 100, int randomState = 0)
    {
        this.gamma = gamma;
        this.nComponents = nComponents;
        this.randomState = randomState;
        normalDistribution = new Normal(new XorshiftRandom(randomState));
    }

    public Matrix<double> FitTransform(Matrix<double> X)
    {
        int nSamples = X.RowCount;
        int nFeatures = X.ColumnCount;

        Matrix<double> randomWeights = GenerateRandomWeights(nFeatures, nComponents);
        Vector<double> randomOffsets = GenerateRandomOffsets(nComponents);

        Matrix<double> transformedFeatures = DenseMatrix.Create(nSamples, nComponents, 0.0);

        for (int i = 0; i < nSamples; i++)
        {
            for (int j = 0; j < nComponents; j++)
            {
                Vector<double> component = randomWeights.Column(j);
                double offset = randomOffsets[j];
                Vector<double> sample = X.Row(i);

                double dotProduct = component.DotProduct(sample);
                double transformedFeature = Math.Cos(dotProduct + offset) * Math.Sqrt(2.0 / nComponents);
                transformedFeatures[i, j] = transformedFeature;
            }
        }

        return transformedFeatures;
    }

    private Matrix<double> GenerateRandomWeights(int nFeatures, int nComponents)
    {
        Matrix<double> randomWeights = DenseMatrix.Create(nFeatures, nComponents, (i, j) => normalDistribution.Sample());
        return randomWeights;
    }

    private Vector<double> GenerateRandomOffsets(int nComponents)
    {
        Vector<double> randomOffsets = DenseVector.Create(nComponents, _ => 2 * Math.PI * normalDistribution.Sample());
        return randomOffsets;
    }
}