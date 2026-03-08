using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Nullable signal vector collecting evidence from all pipeline stages.
/// Null = signal not available (skipped in fusion).
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
    int? FrameCount = null
)
{
    /// <summary>Returns only the populated signal names.</summary>
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

    /// <summary>Number of non-null numeric signals available.</summary>
    public int SignalCount => AvailableSignals().Count;
}

/// <summary>Traffic light classification for detection quality.</summary>
public enum TrafficLight
{
    Green,
    Yellow,
    Red
}

/// <summary>Result of QualityGate evaluation.</summary>
public sealed record QualityGateResult(
    double CompositeConfidence,
    TrafficLight TrafficLight,
    IReadOnlyDictionary<string, double> WeightsUsed,
    string Explanation
)
{
    public bool IsGreen => TrafficLight == TrafficLight.Green;
    public bool IsYellow => TrafficLight == TrafficLight.Yellow;
    public bool IsRed => TrafficLight == TrafficLight.Red;
}
