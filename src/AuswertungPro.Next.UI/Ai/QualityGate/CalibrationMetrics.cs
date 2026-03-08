using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Expected Calibration Error (ECE) computation with equal-width bins.
/// Measures how well predicted confidences match observed accuracy.
/// ECE close to 0 = well calibrated.
/// </summary>
public static class CalibrationMetrics
{
    public const int DefaultBinCount = 10;

    /// <summary>
    /// Compute ECE over predictions.
    /// Each prediction: (confidence, wasCorrect).
    /// </summary>
    public static double ComputeEce(
        IReadOnlyList<(double Confidence, bool WasCorrect)> predictions,
        int binCount = DefaultBinCount)
    {
        if (predictions.Count == 0) return 0;

        var bins = new List<(double Confidence, bool WasCorrect)>[binCount];
        for (int i = 0; i < binCount; i++)
            bins[i] = new List<(double, bool)>();

        foreach (var (conf, correct) in predictions)
        {
            var idx = Math.Min((int)(conf * binCount), binCount - 1);
            bins[idx].Add((conf, correct));
        }

        double ece = 0;
        int total = predictions.Count;

        for (int i = 0; i < binCount; i++)
        {
            var bin = bins[i];
            if (bin.Count == 0) continue;

            var avgConf = bin.Average(b => b.Confidence);
            var accuracy = (double)bin.Count(b => b.WasCorrect) / bin.Count;
            ece += (double)bin.Count / total * Math.Abs(accuracy - avgConf);
        }

        return ece;
    }

    /// <summary>
    /// Returns per-bin calibration details for visualization.
    /// </summary>
    public static IReadOnlyList<CalibrationBin> GetBinDetails(
        IReadOnlyList<(double Confidence, bool WasCorrect)> predictions,
        int binCount = DefaultBinCount)
    {
        var bins = new List<(double Confidence, bool WasCorrect)>[binCount];
        for (int i = 0; i < binCount; i++)
            bins[i] = new List<(double, bool)>();

        foreach (var (conf, correct) in predictions)
        {
            var idx = Math.Min((int)(conf * binCount), binCount - 1);
            bins[idx].Add((conf, correct));
        }

        var result = new List<CalibrationBin>(binCount);
        for (int i = 0; i < binCount; i++)
        {
            var bin = bins[i];
            result.Add(new CalibrationBin(
                BinLower: (double)i / binCount,
                BinUpper: (double)(i + 1) / binCount,
                SampleCount: bin.Count,
                MeanConfidence: bin.Count > 0 ? bin.Average(b => b.Confidence) : 0,
                Accuracy: bin.Count > 0 ? (double)bin.Count(b => b.WasCorrect) / bin.Count : 0));
        }
        return result;
    }
}

public sealed record CalibrationBin(
    double BinLower,
    double BinUpper,
    int SampleCount,
    double MeanConfidence,
    double Accuracy
)
{
    public double Gap => Math.Abs(Accuracy - MeanConfidence);
}
