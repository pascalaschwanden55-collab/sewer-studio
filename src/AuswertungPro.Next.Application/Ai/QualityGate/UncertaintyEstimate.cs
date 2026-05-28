namespace AuswertungPro.Next.Application.Ai.QualityGate;

public enum UncertaintySource
{
    SinglePass,
    MonteCarlo,
    MaskStability,
    Ensemble
}

public sealed record UncertaintyEstimate(
    double Confidence,
    double EpistemicUncertainty,
    double AleatoricUncertainty,
    double CalibratedConfidence,
    UncertaintySource Source)
{
    public double TotalUncertainty => EpistemicUncertainty + AleatoricUncertainty;

    public bool NeedsReview =>
        EpistemicUncertainty >= 0.15 || (Confidence >= 0.45 && Confidence < 0.75);

    public static UncertaintyEstimate FromSinglePass(double confidence, double calibratedConfidence = -1)
    {
        if (calibratedConfidence < 0) calibratedConfidence = confidence;

        var epistemic = 1.0 - System.Math.Abs(2 * confidence - 1);
        const double aleatoric = 0.05;
        return new UncertaintyEstimate(
            confidence,
            epistemic,
            aleatoric,
            calibratedConfidence,
            UncertaintySource.SinglePass);
    }
}
