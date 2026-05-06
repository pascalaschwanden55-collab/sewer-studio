using System;
using AuswertungPro.Next.Domain.Ai.Training;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Kuriert Schwachstellen aus vorhandenen Benchmarks und abgelehnten Samples.
/// Die Klasse veraendert keine Trainingsdaten, sondern erzeugt eine priorisierte
/// Review-Liste fuer gezieltes Nachtraining.
/// </summary>
public sealed class WeakSpotCurator
{
    private readonly BenchmarkMetricsStore _metricsStore;
    private readonly double _weakF1Threshold;

    public WeakSpotCurator(
        BenchmarkMetricsStore? metricsStore = null,
        double weakF1Threshold = 0.65)
    {
        _metricsStore = metricsStore ?? new BenchmarkMetricsStore();
        _weakF1Threshold = Math.Clamp(weakF1Threshold, 0.0, 1.0);
    }

    public async Task<WeakSpotReport> BuildAsync(CancellationToken ct = default)
    {
        var historyTask = _metricsStore.LoadHistoryAsync(ct);
        var samplesTask = TrainingSamplesStore.LoadAsync();

        await Task.WhenAll(historyTask, samplesTask).ConfigureAwait(false);

        var history = historyTask.Result
            .OrderBy(r => r.TimestampUtc)
            .ToList();
        var samples = samplesTask.Result;

        var latest = history.LastOrDefault();
        var items = new List<WeakSpotItem>();

        if (latest?.PerCodeMetrics is { Count: > 0 } metrics)
        {
            foreach (var metric in metrics)
            {
                ct.ThrowIfCancellationRequested();

                var support = metric.TP + metric.FN;
                var hasError = metric.FP > 0 || metric.FN > 0;
                if (!hasError && metric.F1 >= _weakF1Threshold)
                    continue;

                if (metric.F1 >= _weakF1Threshold && support >= 3)
                    continue;

                var rejectionCount = CountRejectedForCode(samples, metric.VsaCodePrefix);
                var priority = ScoreBenchmarkWeakness(metric, support, rejectionCount);
                var reason = metric.F1 < _weakF1Threshold
                    ? $"Benchmark-F1 unter {_weakF1Threshold:P0}"
                    : "Benchmark-Fehler bei niedriger Stuetzung";

                items.Add(new WeakSpotItem
                {
                    Source = "Benchmark",
                    ExpectedCode = metric.VsaCodePrefix,
                    ConfusedWithCode = "",
                    Priority = priority,
                    Count = support,
                    RejectedCount = rejectionCount,
                    F1 = metric.F1,
                    Precision = metric.Precision,
                    Recall = metric.Recall,
                    FalsePositives = metric.FP,
                    FalseNegatives = metric.FN,
                    Reason = reason,
                    SuggestedAction = BuildBenchmarkAction(metric, support, rejectionCount)
                });
            }
        }

        AddRejectedSampleWeakSpots(samples, items, ct);
        AddMismatchWeakSpots(samples, items, ct);

        var ordered = items
            .GroupBy(i => i.Key, StringComparer.OrdinalIgnoreCase)
            .Select(g => Merge(g.ToList()))
            .OrderByDescending(i => i.Priority)
            .ThenBy(i => i.ExpectedCode, StringComparer.OrdinalIgnoreCase)
            .Take(100)
            .ToList();

        var summary = BuildSummary(history.Count, samples.Count, ordered);
        return new WeakSpotReport(summary, ordered);
    }

    private static void AddRejectedSampleWeakSpots(
        IReadOnlyList<TrainingSample> samples,
        List<WeakSpotItem> items,
        CancellationToken ct)
    {
        var rejectedGroups = samples
            .Where(s => s.Status == TrainingSampleStatus.Rejected)
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => new
            {
                Expected = NormalizeCode(s.Code),
                Confused = NormalizeCode(s.KiCode ?? "")
            });

        foreach (var group in rejectedGroups)
        {
            ct.ThrowIfCancellationRequested();
            var list = group.ToList();
            var count = list.Count;
            var hasFrames = list.Count(s => !string.IsNullOrWhiteSpace(s.FramePath));
            var confused = group.Key.Confused;

            items.Add(new WeakSpotItem
            {
                Source = "Rejected",
                ExpectedCode = group.Key.Expected,
                ConfusedWithCode = confused,
                Priority = 70 + Math.Min(50, count * 8) + Math.Min(20, hasFrames * 2),
                Count = count,
                RejectedCount = count,
                F1 = null,
                Precision = null,
                Recall = null,
                FalsePositives = 0,
                FalseNegatives = 0,
                Reason = string.IsNullOrWhiteSpace(confused)
                    ? "Manuell abgelehnte Samples"
                    : $"Manuell abgelehnt: KI tendiert zu {confused}",
                SuggestedAction = hasFrames > 0
                    ? $"Review-Queue mit {Math.Min(20, hasFrames)} Frame-Beispielen pruefen und korrekt annotieren."
                    : "Abgelehnte protocol-only Samples pruefen; nur eindeutige Korrekturen in die KB uebernehmen."
            });
        }
    }

    private static void AddMismatchWeakSpots(
        IReadOnlyList<TrainingSample> samples,
        List<WeakSpotItem> items,
        CancellationToken ct)
    {
        var mismatchGroups = samples
            .Where(s => s.Status == TrainingSampleStatus.New)
            .Where(s => s.MatchLevel is MatchLevelNames.Mismatch or MatchLevelNames.PartialMatch)
            .Where(s => !string.IsNullOrWhiteSpace(s.Code))
            .GroupBy(s => new
            {
                Expected = NormalizeCode(s.Code),
                Confused = NormalizeCode(s.KiCode ?? "")
            });

        foreach (var group in mismatchGroups)
        {
            ct.ThrowIfCancellationRequested();
            var list = group.ToList();
            var count = list.Count;
            var confused = group.Key.Confused;

            items.Add(new WeakSpotItem
            {
                Source = "Review",
                ExpectedCode = group.Key.Expected,
                ConfusedWithCode = confused,
                Priority = 45 + Math.Min(45, count * 6),
                Count = count,
                RejectedCount = 0,
                F1 = null,
                Precision = null,
                Recall = null,
                FalsePositives = 0,
                FalseNegatives = 0,
                Reason = string.IsNullOrWhiteSpace(confused)
                    ? "Offene Partial/Mismatch-Samples"
                    : $"Offene Verwechslung mit {confused}",
                SuggestedAction = "Als Hard-Negatives priorisiert reviewen; echte Treffer approven, Fehlgriffe korrigieren oder rejecten."
            });
        }
    }

    private static double ScoreBenchmarkWeakness(CodeClassMetrics metric, int support, int rejectedCount)
    {
        var f1Penalty = (1.0 - metric.F1) * 100.0;
        var errorPenalty = metric.FN * 8.0 + metric.FP * 5.0;
        var rejectionPenalty = Math.Min(30, rejectedCount * 4);
        var lowSupportPenalty = support is > 0 and < 3 ? 10 : 0;
        return Math.Round(f1Penalty + errorPenalty + rejectionPenalty + lowSupportPenalty, 1);
    }

    private static string BuildBenchmarkAction(CodeClassMetrics metric, int support, int rejectedCount)
    {
        if (metric.FN > metric.FP)
            return $"Mindestens {Math.Max(5, metric.FN * 3)} positive Beispiele fuer {metric.VsaCodePrefix} suchen; uebersehene Treffer zuerst.";

        if (metric.FP > metric.FN)
            return $"Hard-Negatives gegen {metric.VsaCodePrefix} kuratieren; falsch-positive Frames markieren.";

        if (rejectedCount > 0)
            return $"Rejected-Samples fuer {metric.VsaCodePrefix} pruefen und klare Korrekturen als Teacher-Annotation speichern.";

        return support < 3
            ? $"Mehr gepruefte Beispiele sammeln; Benchmark-Stuetzung fuer {metric.VsaCodePrefix} ist niedrig."
            : $"Verwechslungen fuer {metric.VsaCodePrefix} in Review Queue und Eval-Ergebnissen pruefen.";
    }

    private static int CountRejectedForCode(IEnumerable<TrainingSample> samples, string code)
        => samples.Count(s => s.Status == TrainingSampleStatus.Rejected
            && string.Equals(NormalizeCode(s.Code), NormalizeCode(code), StringComparison.OrdinalIgnoreCase));

    private static WeakSpotItem Merge(IReadOnlyList<WeakSpotItem> items)
    {
        if (items.Count == 1)
            return items[0];

        var first = items[0];
        return first with
        {
            Source = string.Join("+", items.Select(i => i.Source).Distinct(StringComparer.OrdinalIgnoreCase)),
            Priority = Math.Round(items.Sum(i => i.Priority), 1),
            Count = items.Sum(i => i.Count),
            RejectedCount = items.Sum(i => i.RejectedCount),
            FalsePositives = items.Sum(i => i.FalsePositives),
            FalseNegatives = items.Sum(i => i.FalseNegatives),
            Reason = string.Join(" | ", items.Select(i => i.Reason).Distinct()),
            SuggestedAction = string.Join(" | ", items.Select(i => i.SuggestedAction).Distinct())
        };
    }

    private static string BuildSummary(int benchmarkRuns, int sampleCount, IReadOnlyList<WeakSpotItem> items)
    {
        if (items.Count == 0)
        {
            return benchmarkRuns == 0 && sampleCount == 0
                ? "Noch keine Benchmark- oder Sample-Daten vorhanden."
                : "Keine priorisierten Schwachstellen gefunden.";
        }

        var benchmarkItems = items.Count(i => i.Source.Contains("Benchmark", StringComparison.OrdinalIgnoreCase));
        var rejectedItems = items.Count(i => i.Source.Contains("Rejected", StringComparison.OrdinalIgnoreCase));
        var reviewItems = items.Count(i => i.Source.Contains("Review", StringComparison.OrdinalIgnoreCase));
        return $"{items.Count} Schwachstellen: {benchmarkItems} Benchmark, {rejectedItems} Rejected, {reviewItems} offene Review.";
    }

    private static string NormalizeCode(string? code)
        => string.IsNullOrWhiteSpace(code)
            ? ""
            : code.Trim().Split('.')[0].ToUpperInvariant();
}

public sealed record WeakSpotReport(
    string Summary,
    IReadOnlyList<WeakSpotItem> Items);

public sealed record WeakSpotItem
{
    public string Source { get; init; } = "";
    public string ExpectedCode { get; init; } = "";
    public string ConfusedWithCode { get; init; } = "";
    public double Priority { get; init; }
    public int Count { get; init; }
    public int RejectedCount { get; init; }
    public double? F1 { get; init; }
    public double? Precision { get; init; }
    public double? Recall { get; init; }
    public int FalsePositives { get; init; }
    public int FalseNegatives { get; init; }
    public string Reason { get; init; } = "";
    public string SuggestedAction { get; init; } = "";

    public string Key => $"{ExpectedCode}|{ConfusedWithCode}";
    public string ConfusionLabel => string.IsNullOrWhiteSpace(ConfusedWithCode)
        ? ExpectedCode
        : $"{ExpectedCode} -> {ConfusedWithCode}";
}
