using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Platt Scaling: learns a temperature T on validation data such that
/// Scale(conf) = Sigmoid(Logit(conf) / T) minimizes Negative Log-Likelihood.
/// Grid search over T in [0.1, 5.0].
/// </summary>
public sealed class TemperatureScaler
{
    public double Temperature { get; private set; } = 1.0;

    /// <summary>Apply temperature scaling to a raw confidence.</summary>
    public double Scale(double confidence)
    {
        confidence = Math.Clamp(confidence, 1e-7, 1.0 - 1e-7);
        var logit = Math.Log(confidence / (1.0 - confidence));
        var scaled = logit / Temperature;
        return Sigmoid(scaled);
    }

    /// <summary>
    /// Fit temperature on validation data using grid search.
    /// Each sample: (predictedConfidence, wasCorrect).
    /// </summary>
    public void Fit(IReadOnlyList<(double Confidence, bool WasCorrect)> validationData)
    {
        if (validationData.Count < 5) return;

        double bestT = 1.0;
        double bestNll = double.MaxValue;

        // Grid search: 0.1 to 5.0, step 0.05
        for (double t = 0.1; t <= 5.0; t += 0.05)
        {
            var nll = ComputeNll(validationData, t);
            if (nll < bestNll)
            {
                bestNll = nll;
                bestT = t;
            }
        }

        // Fine search around best: ±0.05, step 0.005
        for (double t = Math.Max(0.05, bestT - 0.05); t <= bestT + 0.05; t += 0.005)
        {
            var nll = ComputeNll(validationData, t);
            if (nll < bestNll)
            {
                bestNll = nll;
                bestT = t;
            }
        }

        Temperature = bestT;
    }

    private static double ComputeNll(IReadOnlyList<(double Confidence, bool WasCorrect)> data, double T)
    {
        const double eps = 1e-7;
        double totalNll = 0;

        foreach (var (conf, correct) in data)
        {
            var c = Math.Clamp(conf, eps, 1.0 - eps);
            var logit = Math.Log(c / (1.0 - c));
            var scaled = Sigmoid(logit / T);
            scaled = Math.Clamp(scaled, eps, 1.0 - eps);

            totalNll += correct
                ? -Math.Log(scaled)
                : -Math.Log(1.0 - scaled);
        }

        return totalNll / data.Count;
    }

    private static double Sigmoid(double x) => 1.0 / (1.0 + Math.Exp(-x));
}
