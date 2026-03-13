using System;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>Source method used to estimate uncertainty.</summary>
public enum UncertaintySource
{
    SinglePass,
    MonteCarlo,
    MaskStability,
    Ensemble
}

/// <summary>
/// Uncertainty quantification result for a single detection.
/// Epistemic = model uncertainty (reducible with more data).
/// Aleatoric = data noise (irreducible).
/// </summary>
public sealed record UncertaintyEstimate(
    double Confidence,
    double EpistemicUncertainty,
    double AleatoricUncertainty,
    double CalibratedConfidence,
    UncertaintySource Source
)
{
    /// <summary>Total uncertainty (epistemic + aleatoric).</summary>
    public double TotalUncertainty => EpistemicUncertainty + AleatoricUncertainty;

    /// <summary>Is this detection uncertain enough to warrant human review?</summary>
    public bool NeedsReview => EpistemicUncertainty >= 0.15 || (Confidence >= 0.45 && Confidence < 0.75);

    /// <summary>Create a simple single-pass estimate (no MC dropout).</summary>
    public static UncertaintyEstimate FromSinglePass(double confidence, double calibratedConfidence = -1)
    {
        if (calibratedConfidence < 0) calibratedConfidence = confidence;
        // Heuristic: epistemic ~ distance from extremes, aleatoric ~ base noise
        var epistemic = 1.0 - Math.Abs(2 * confidence - 1);
        var aleatoric = 0.05; // base noise
        return new UncertaintyEstimate(confidence, epistemic, aleatoric, calibratedConfidence, UncertaintySource.SinglePass);
    }
}
