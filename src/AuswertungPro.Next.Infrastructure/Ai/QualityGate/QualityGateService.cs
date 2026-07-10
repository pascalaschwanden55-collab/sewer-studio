using System;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Infrastructure.Ai.QualityGate;

/// <summary>
/// Weighted-average fusion of evidence signals into a composite confidence score.
/// Persisted learned weights are distributed process-wide. Existing service instances
/// pick up a newly activated snapshot before their next evaluation.
/// </summary>
public sealed class QualityGateService
{
    public const double GreenThreshold = 0.75;
    public const double YellowThreshold = 0.45;
    public const int MinSignalsForGreen = 2;

    private static readonly object ProcessSync = new();
    private static ProcessWeightSnapshot _processSnapshot = ProcessWeightSnapshot.Empty;
    private static long _processRevision;

    private readonly object _instanceSync = new();
    private readonly Dictionary<string, CategoryWeights> _categoryWeights = new(StringComparer.OrdinalIgnoreCase);
    private long _appliedProcessRevision = -1;

    public QualityGateService()
    {
        EnsureProcessWeightsApplied();
    }

    public QualityGateService(IEnumerable<CategoryWeights> weights)
        : this()
    {
        ArgumentNullException.ThrowIfNull(weights);
        foreach (var weight in weights)
            SetWeights(weight);
    }

    /// <summary>Currently active learned-weight version for diagnostics and audit trails.</summary>
    public static string ActiveProcessWeightVersion
    {
        get
        {
            lock (ProcessSync)
                return _processSnapshot.Version;
        }
    }

    /// <summary>
    /// Atomically activates a complete learned-weight snapshot for the current process.
    /// Existing and future gate instances adopt it without restart.
    /// </summary>
    public static void ConfigureProcessWeights(
        IEnumerable<CategoryWeights> weights,
        string? version = null)
    {
        ArgumentNullException.ThrowIfNull(weights);

        var snapshot = weights
            .Where(IsUsable)
            .GroupBy(w => w.Category, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(w => w.UpdatedUtc).First())
            .ToDictionary(w => w.Category, w => w, StringComparer.OrdinalIgnoreCase);

        lock (ProcessSync)
        {
            _processSnapshot = new ProcessWeightSnapshot(
                string.IsNullOrWhiteSpace(version)
                    ? QualityGateWeightSnapshot.DefaultVersion
                    : version!,
                snapshot);
            _processRevision++;
        }
    }

    public void SetWeights(CategoryWeights weights)
    {
        ArgumentNullException.ThrowIfNull(weights);
        if (!IsUsable(weights))
            throw new ArgumentException("CategoryWeights enthaelt keine gueltige Kategorie oder Gewichtssumme.", nameof(weights));

        lock (_instanceSync)
            _categoryWeights[weights.Category] = weights;
    }

    public QualityGateResult Evaluate(EvidenceVector evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        EnsureProcessWeightsApplied();

        var category = evidence.DamageCategory ?? "default";
        CategoryWeights weights;
        lock (_instanceSync)
        {
            if (!_categoryWeights.TryGetValue(category, out weights!))
            {
                if (!_categoryWeights.TryGetValue("default", out weights!))
                    weights = CategoryWeights.Default();
            }
        }

        var signals = new List<(string Name, double Value, double Weight)>();
        TryAdd(signals, nameof(EvidenceVector.YoloConf), evidence.YoloConf, weights.WYolo);
        TryAdd(signals, nameof(EvidenceVector.DinoConf), evidence.DinoConf, weights.WDino);
        TryAdd(signals, nameof(EvidenceVector.SamMaskStability), evidence.SamMaskStability, weights.WSam);
        TryAdd(signals, nameof(EvidenceVector.QwenVisionConf), evidence.QwenVisionConf, weights.WQwen);
        TryAdd(signals, nameof(EvidenceVector.LlmCodeConf), evidence.LlmCodeConf, weights.WLlm);
        TryAdd(signals, nameof(EvidenceVector.KbSimilarity), evidence.KbSimilarity, weights.WKb);
        if (evidence.KbCodeAgreement.HasValue)
        {
            signals.Add((
                nameof(EvidenceVector.KbCodeAgreement),
                evidence.KbCodeAgreement.Value ? 1.0 : 0.0,
                weights.WKbAgreement));
        }
        TryAdd(signals, nameof(EvidenceVector.PlausibilityScore), evidence.PlausibilityScore, weights.WPlausibility);

        if (signals.Count == 0)
        {
            return new QualityGateResult(
                0.0,
                TrafficLight.Red,
                new Dictionary<string, double>(),
                $"Keine Signale verfuegbar. WeightVersion={ActiveProcessWeightVersion}.");
        }

        var totalWeight = signals.Sum(s => s.Weight);
        if (totalWeight <= 0)
            totalWeight = signals.Count;

        var composite = signals.Sum(s => s.Value * s.Weight) / totalWeight;
        composite = Math.Clamp(composite, 0.0, 1.0);

        var weightsUsed = new Dictionary<string, double>(signals.Count);
        foreach (var signal in signals)
            weightsUsed[signal.Name] = signal.Weight / totalWeight;

        var trafficLight = composite >= GreenThreshold ? TrafficLight.Green
            : composite >= YellowThreshold ? TrafficLight.Yellow
            : TrafficLight.Red;

        var cappedSingleSignal = trafficLight == TrafficLight.Green && signals.Count < MinSignalsForGreen;
        if (cappedSingleSignal)
            trafficLight = TrafficLight.Yellow;

        var explanation =
            $"Composite={composite:F3} ({signals.Count} signals, category={category}, weights={ActiveProcessWeightVersion}): " +
            string.Join(", ", signals.Select(s => $"{s.Name}={s.Value:F2}×{s.Weight / totalWeight:F2}")) +
            (cappedSingleSignal
                ? $" — auf Gelb begrenzt: nur {signals.Count} Evidenzsignal vorhanden, Green erst ab {MinSignalsForGreen}."
                : string.Empty);

        return new QualityGateResult(composite, trafficLight, weightsUsed, explanation);
    }

    private void EnsureProcessWeightsApplied()
    {
        ProcessWeightSnapshot snapshot;
        long revision;
        lock (ProcessSync)
        {
            revision = _processRevision;
            if (_appliedProcessRevision == revision)
                return;
            snapshot = _processSnapshot;
        }

        lock (_instanceSync)
        {
            foreach (var pair in snapshot.Weights)
                _categoryWeights[pair.Key] = pair.Value;
            _appliedProcessRevision = revision;
        }
    }

    private static bool IsUsable(CategoryWeights weights)
    {
        if (weights is null || string.IsNullOrWhiteSpace(weights.Category))
            return false;
        return weights.ToArray().Any(w => w > 0 && double.IsFinite(w));
    }

    private static void TryAdd(
        List<(string Name, double Value, double Weight)> list,
        string name,
        double? value,
        double weight)
    {
        if (value.HasValue)
            list.Add((name, Math.Clamp(value.Value, 0.0, 1.0), weight));
    }

    private sealed record ProcessWeightSnapshot(
        string Version,
        IReadOnlyDictionary<string, CategoryWeights> Weights)
    {
        public static ProcessWeightSnapshot Empty { get; } = new(
            QualityGateWeightSnapshot.DefaultVersion,
            new Dictionary<string, CategoryWeights>(StringComparer.OrdinalIgnoreCase));
    }
}
