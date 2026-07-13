using MathNet.Numerics.Distributions;

namespace ToggleMesh.API.Features.Analytics.Services;

public class BayesianMathService
{
    public double CalculateProbabilityBBeatsA(
        long exposuresA, long conversionsA, 
        long exposuresB, long conversionsB, 
        int iterations = 100000)
    {
        double priorAlpha = 1.0;
        double priorBeta = 1.0;

        double alphaA = Math.Max(priorAlpha + conversionsA, 1e-9);
        double betaA = Math.Max(priorBeta + (exposuresA - conversionsA), 1e-9);

        double alphaB = Math.Max(priorAlpha + conversionsB, 1e-9);
        double betaB = Math.Max(priorBeta + (exposuresB - conversionsB), 1e-9);

        var distA = new Beta(alphaA, betaA);
        var distB = new Beta(alphaB, betaB);

        int bBeatsA = 0;

        for (int i = 0; i < iterations; i++)
        {
            double sampleA = distA.Sample();
            double sampleB = distB.Sample();

            if (sampleB > sampleA)
                bBeatsA++;
        }

        return (double)bBeatsA / iterations;
    }

    public double[] CalculateDirichletProbabilities(
        long[] exposures, 
        long[] conversions, 
        int iterations = 10000)
    {
        int n = exposures.Length;
        if (n == 0) return [];
        if (n == 1) return [1.0];

        double priorAlpha = 1.0;
        double priorBeta = 1.0;

        var dists = new Beta[n];

        for (int i = 0; i < n; i++)
        {
            double alpha = Math.Max(priorAlpha + conversions[i], 1e-9);
            double beta = Math.Max(priorBeta + (exposures[i] - conversions[i]), 1e-9);
            dists[i] = new Beta(alpha, beta);
        }

        var wins = new int[n];

        for (int i = 0; i < iterations; i++)
        {
            int bestIdx = 0;
            double maxSample = -1;

            for (int j = 0; j < n; j++)
            {
                double sample = dists[j].Sample();
                if (sample > maxSample)
                {
                    maxSample = sample;
                    bestIdx = j;
                }
            }

            wins[bestIdx]++;
        }

        var probs = new double[n];
        for (int i = 0; i < n; i++)
        {
            probs[i] = (double)wins[i] / iterations;
        }

        return probs;
    }

    public double[] CalculateDirichletProbabilities_Revenue(
        long[] exposures, double[] values, double[] sumSquareds,
        int iterations = 10000)
    {
        int n = exposures.Length;
        if (n == 0) return [];
        if (n == 1) return [1.0];

        var means = new double[n];
        var vars = new double[n];

        for (int i = 0; i < n; i++)
        {
            if (exposures[i] < 2)
            {
                means[i] = 0;
                vars[i] = 1e-9;
                continue;
            }

            double mean = values[i] / exposures[i];
            double variance = (sumSquareds[i] / exposures[i]) - (mean * mean);
            
            means[i] = mean;
            vars[i] = Math.Max(variance / exposures[i], 1e-9);
        }

        var dists = new Normal[n];
        for (int i = 0; i < n; i++)
        {
            dists[i] = new Normal(means[i], Math.Sqrt(vars[i]));
        }

        var wins = new int[n];
        for (int i = 0; i < iterations; i++)
        {
            int bestIdx = 0;
            double maxSample = double.MinValue;

            for (int j = 0; j < n; j++)
            {
                double sample = dists[j].Sample();
                if (sample > maxSample)
                {
                    maxSample = sample;
                    bestIdx = j;
                }
            }

            wins[bestIdx]++;
        }

        var probs = new double[n];
        for (int i = 0; i < n; i++)
        {
            probs[i] = (double)wins[i] / iterations;
        }

        return probs;
    }

    public double CalculateExpectedUplift(
        long exposuresA, long conversionsA, 
        long exposuresB, long conversionsB)
    {
        if (exposuresA == 0 || exposuresB == 0) return 0;

        double crA = (double)conversionsA / exposuresA;
        double crB = (double)conversionsB / exposuresB;

        if (crA == 0) return crB > 0 ? 1.0 : 0;

        return (crB - crA) / crA;
    }

    public double CalculateProbabilityBBeatsA_Revenue(
        long exposuresA, double valueA, double sumSquaredA,
        long exposuresB, double valueB, double sumSquaredB)
    {
        if (exposuresA < 2 || exposuresB < 2) return 0.5;

        double meanA = valueA / exposuresA;
        double meanB = valueB / exposuresB;

        double varA = (sumSquaredA / exposuresA) - (meanA * meanA);
        double varB = (sumSquaredB / exposuresB) - (meanB * meanB);

        if (varA <= 0) varA = 1e-9;
        if (varB <= 0) varB = 1e-9;

        double meanDiff = meanB - meanA;
        double varDiff = (varA / exposuresA) + (varB / exposuresB);

        double z = meanDiff / Math.Sqrt(varDiff);

        return Normal.CDF(0, 1, z);
    }

    public double CalculateExpectedUplift_Revenue(
        long exposuresA, double valueA,
        long exposuresB, double valueB)
    {
        if (exposuresA == 0 || exposuresB == 0) return 0;

        double arpuA = valueA / exposuresA;
        double arpuB = valueB / exposuresB;

        if (arpuA == 0) return arpuB > 0 ? 1.0 : 0;

        return (arpuB - arpuA) / arpuA;
    }
}
