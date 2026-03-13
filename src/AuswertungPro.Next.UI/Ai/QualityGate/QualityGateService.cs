using System;
using System.Collections.Generic;
using System.Linq;

namespace AuswertungPro.Next.UI.Ai.QualityGate;

/// <summary>
/// Weighted-average fusion of evidence signals into a composite confidence score.
/// Nullable signals are skipped and weights renormalized over available signals.
/// Thresholds: Green >= 0.75, Yellow >= 0.45, else Red.
/// </summary>
public sealed class QualityGateService
{
    public const double GreenThreshold = 0.75;
    public const double YellowThreshold = 0.45;

    private readonly Dictionary<string, CategoryWeights> _categoryWeights = new(StringComparer.OrdinalIgnoreCase);

    public QualityGateService() { }

    public QualityGateService(IEnumerable<CategoryWeights> weights)
    {
        foreach (var w in weights)
            _categoryWeights[w.Category] = w;
    }

    public void SetWeights(CategoryWeights weights) =>
        _categoryWeights[weights.Category] = weights;

    public QualityGateResult Evaluate(EvidenceVector evidence)
    {
        var category = evidence.DamageCategory ?? "default";
        if (!_categoryWeights.TryGetValue(category, out var weights))
        {
            if (!_categoryWeights.TryGetValue("default", out weights))
                weights = CategoryWeights.Default();
        }

        var signals = new List<(string Name, double Value, double Weight)>();
        TryAdd(signals, nameof(EvidenceVector.YoloConf), evidence.YoloConf, weights.WYolo);
        TryAdd(signals, nameof(EvidenceVector.DinoConf), evidence.DinoConf, weights.WDino);
        TryAdd(signals, nameof(EvidenceVector.SamMaskStability), evidence.SamMaskStability, weights.WSam);
        TryAdd(signals, nameof(EvidenceVector.QwenVisionConf), evidence.QwenVisionConf, weights.WQwen);
        TryAdd(signals, nameof(EvidenceVector.LlmCodeConf), evidence.LlmCodeConf, weights.WLlm);
        TryAdd(signals, nameof(EvidenceVector.KbSimilarity), evidence.KbSimilarity, weights.WKb);
        if (evidence.KbCodeAgreement.HasValue)
            signals.Add((nameof(EvidenceVector.KbCodeAgreement), evidence.KbCodeAgreement.Value ? 1.0 : 0.0, weights.WKbAgreement));
        TryAdd(signals, nameof(EvidenceVector.PlausibilityScore), evidence.PlausibilityScore, weights.WPlausibility);

        if (signals.Count == 0)
        {
            return new QualityGateResult(0.0, TrafficLight.Red,
                new Dictionary<string, double>(),
                "Keine Signale verfügbar.");
        }

        // Renormalize weights over available signals
        var totalWeight = signals.Sum(s => s.Weight);
        if (totalWeight <= 0) totalWeight = signals.Count; // fallback to equal

        var composite = signals.Sum(s => s.Value * s.Weight) / totalWeight;
        composite = Math.Clamp(composite, 0.0, 1.0);

        var weightsUsed = new Dictionary<string, double>(signals.Count);
        foreach (var s in signals)
            weightsUsed[s.Name] = s.Weight / totalWeight;

        var trafficLight = composite >= GreenThreshold ? TrafficLight.Green
            : composite >= YellowThreshold ? TrafficLight.Yellow
            : TrafficLight.Red;

        var explanation = $"Composite={composite:F3} ({signals.Count} signals, category={category}): " +
            string.Join(", ", signals.Select(s => $"{s.Name}={s.Value:F2}×{s.Weight / totalWeight:F2}"));

        return new QualityGateResult(composite, trafficLight, weightsUsed, explanation);
    }

    private static void TryAdd(List<(string, double, double)> list, string name, double? value, double weight)
    {
        if (value.HasValue)
            list.Add((name, Math.Clamp(value.Value, 0.0, 1.0), weight));
    }
}
