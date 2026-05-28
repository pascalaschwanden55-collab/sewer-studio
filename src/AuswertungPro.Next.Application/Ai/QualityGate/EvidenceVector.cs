using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.QualityGate;

/// <summary>
/// Nullable signal vector collecting evidence from all pipeline stages.
/// Null means the signal is not available.
/// </summary>
public sealed record EvidenceVector(
    double? YoloConf = null,
    double? DinoConf = null,
    double? SamMaskStability = null,
    double? QwenVisionConf = null,
    double? LlmCodeConf = null,
    double? KbSimilarity = null,
    bool? KbCodeAgreement = null,
    double? PlausibilityScore = null,
    string? DamageCategory = null,
    int? FrameCount = null)
{
    public IReadOnlyList<string> AvailableSignals()
    {
        var list = new List<string>(10);
        if (YoloConf.HasValue) list.Add(nameof(YoloConf));
        if (DinoConf.HasValue) list.Add(nameof(DinoConf));
        if (SamMaskStability.HasValue) list.Add(nameof(SamMaskStability));
        if (QwenVisionConf.HasValue) list.Add(nameof(QwenVisionConf));
        if (LlmCodeConf.HasValue) list.Add(nameof(LlmCodeConf));
        if (KbSimilarity.HasValue) list.Add(nameof(KbSimilarity));
        if (KbCodeAgreement.HasValue) list.Add(nameof(KbCodeAgreement));
        if (PlausibilityScore.HasValue) list.Add(nameof(PlausibilityScore));
        return list;
    }

    public int SignalCount => AvailableSignals().Count;
}

public enum TrafficLight
{
    Green,
    Yellow,
    Red
}

public sealed record QualityGateResult(
    double CompositeConfidence,
    TrafficLight TrafficLight,
    IReadOnlyDictionary<string, double> WeightsUsed,
    string Explanation)
{
    public bool IsGreen => TrafficLight == TrafficLight.Green;
    public bool IsYellow => TrafficLight == TrafficLight.Yellow;
    public bool IsRed => TrafficLight == TrafficLight.Red;
}
