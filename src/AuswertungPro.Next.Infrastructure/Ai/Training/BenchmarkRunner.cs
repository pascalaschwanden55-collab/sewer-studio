// AuswertungPro – Video-Selbsttraining Phase 4
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.Application.Ai;
using Microsoft.Extensions.Logging;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Fuehrt Benchmark-Durchlaeufe durch: Alle Benchmark-Haltungen sequenziell
/// durch die Video-Pipeline analysieren und Metriken aggregieren.
/// </summary>
public sealed class BenchmarkRunner
{
    private readonly BenchmarkSetStore _setStore;
    private readonly BenchmarkMetricsStore _metricsStore;
    private readonly VideoSelfTrainingOrchestrator _orchestrator;
    private readonly Func<string, string, Task<ProtocolDocument?>> _protocolLoader;
    private readonly ILogger? _log;

    /// <param name="protocolLoader">Laedt ein Protokoll aus Pfad + SourceType. Wird von aussen injiziert.</param>
    public BenchmarkRunner(
        BenchmarkSetStore setStore,
        BenchmarkMetricsStore metricsStore,
        VideoSelfTrainingOrchestrator orchestrator,
        Func<string, string, Task<ProtocolDocument?>> protocolLoader,
        ILogger? log = null)
    {
        _setStore = setStore ?? throw new ArgumentNullException(nameof(setStore));
        _metricsStore = metricsStore ?? throw new ArgumentNullException(nameof(metricsStore));
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _protocolLoader = protocolLoader ?? throw new ArgumentNullException(nameof(protocolLoader));
        _log = log;
    }

    /// <summary>
    /// Fuehrt einen vollstaendigen Benchmark-Durchlauf durch.
    /// </summary>
    public async Task<BenchmarkRunResult> RunAsync(
        IProgress<BenchmarkProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();

        var set = await _setStore.LoadAsync(ct).ConfigureAwait(false);
        if (set.Haltungen.Count == 0)
            throw new InvalidOperationException("Benchmark-Set ist leer. Fuege zuerst Haltungen hinzu.");

        var allEntries = new List<DifferenceEntry>();
        var total = set.Haltungen.Count;

        for (int i = 0; i < total; i++)
        {
            ct.ThrowIfCancellationRequested();
            var h = set.Haltungen[i];

            progress?.Report(new BenchmarkProgress(i, total, h.HaltungId, $"Analysiere {h.HaltungId}..."));
            _log?.LogInformation("Benchmark [{I}/{Total}]: {Id}", i + 1, total, h.HaltungId);

            try
            {
                // Protokoll laden
                var protocol = await _protocolLoader(h.ProtocolSource, h.SourceType).ConfigureAwait(false);
                if (protocol is null)
                {
                    _log?.LogWarning("Benchmark: Protokoll fuer {Id} konnte nicht geladen werden", h.HaltungId);
                    continue;
                }

                var request = new VideoTrainingRequest
                {
                    VideoPath = h.VideoPath,
                    ProtocolSource = h.ProtocolSource,
                    ProtocolSourceType = h.SourceType,
                    Rohrmaterial = h.Rohrmaterial,
                    NennweiteMm = h.NennweiteMm,
                    FrameStepSeconds = 2.0,
                    MeterTolerance = MeterTolerances.Benchmark
                };

                var result = await _orchestrator.RunAsync(request, protocol, ct: ct).ConfigureAwait(false);
                allEntries.AddRange(result.Report.Entries);

                progress?.Report(new BenchmarkProgress(
                    i + 1, total, h.HaltungId,
                    $"{h.HaltungId}: F1={result.Report.F1:P0}"));
            }
            catch (Exception ex)
            {
                _log?.LogError(ex, "Benchmark fehlgeschlagen fuer {Id}", h.HaltungId);
            }
        }

        sw.Stop();

        // Aggregierte Metriken berechnen
        var report = new DifferenceReport { Entries = allEntries };
        var perCode = AggregatePerCode(allEntries);

        // Regressions-Check
        var history = await _metricsStore.LoadHistoryAsync(ct).ConfigureAwait(false);
        var regression = BenchmarkMetricsStore.CheckForRegression(
            new BenchmarkRunResult
            {
                F1 = report.F1,
                Precision = report.Precision,
                Recall = report.Recall,
                PerCodeMetrics = perCode
            },
            history,
            _log);

        var runResult = new BenchmarkRunResult
        {
            TimestampUtc = DateTime.UtcNow,
            Duration = sw.Elapsed,
            Precision = report.Precision,
            Recall = report.Recall,
            F1 = report.F1,
            TotalProtocolEntries = allEntries.Count(e =>
                e.Category is DifferenceCategory.TruePositive
                    or DifferenceCategory.FalseNegative
                    or DifferenceCategory.CodeMismatch),
            TotalDetections = allEntries.Count(e =>
                e.Category is DifferenceCategory.TruePositive
                    or DifferenceCategory.FalsePositive
                    or DifferenceCategory.CodeMismatch),
            PerCodeMetrics = perCode,
            HasRegression = regression.HasRegression,
            RegressionDetail = regression.Detail
        };

        // Ergebnis persistieren
        await _metricsStore.AppendRunAsync(runResult, ct).ConfigureAwait(false);

        // Benchmark-Set LastRunUtc aktualisieren
        set.LastRunUtc = DateTime.UtcNow;
        await _setStore.SaveAsync(set, ct).ConfigureAwait(false);

        if (regression.HasRegression)
        {
            _log?.LogWarning("REGRESSION erkannt: {Detail}", regression.Detail);
        }

        _log?.LogInformation(
            "Benchmark abgeschlossen: F1={F1:P1}, P={P:P1}, R={R:P1} in {Dur:F1}min",
            runResult.F1, runResult.Precision, runResult.Recall, sw.Elapsed.TotalMinutes);

        return runResult;
    }

    /// <summary>Aggregiert Metriken pro VSA-Code-Praefixgruppe (3 Zeichen).</summary>
    internal static List<CodeClassMetrics> AggregatePerCode(List<DifferenceEntry> entries)
    {
        var groups = new Dictionary<string, (int TP, int FP, int FN)>(StringComparer.OrdinalIgnoreCase);

        foreach (var e in entries)
        {
            var code = e.ProtocolEntry?.VsaCode ?? e.KiDetection?.VsaCode ?? e.KiDetection?.Label;
            if (string.IsNullOrEmpty(code) || code.Length < 3) continue;

            var prefix = code[..3].ToUpperInvariant();
            if (!groups.ContainsKey(prefix))
                groups[prefix] = (0, 0, 0);

            var (tp, fp, fn) = groups[prefix];
            switch (e.Category)
            {
                case DifferenceCategory.TruePositive:
                    groups[prefix] = (tp + 1, fp, fn);
                    break;
                case DifferenceCategory.FalsePositive:
                    groups[prefix] = (tp, fp + 1, fn);
                    break;
                case DifferenceCategory.FalseNegative:
                    groups[prefix] = (tp, fp, fn + 1);
                    break;
                case DifferenceCategory.CodeMismatch:
                    // FN fuer den Protokoll-Code (der richtige Code wurde uebersehen)
                    var protCode = e.ProtocolEntry?.VsaCode;
                    if (!string.IsNullOrEmpty(protCode) && protCode.Length >= 3)
                    {
                        var protPrefix = protCode[..3].ToUpperInvariant();
                        if (!groups.ContainsKey(protPrefix))
                            groups[protPrefix] = (0, 0, 0);
                        var (ptp, pfp, pfn) = groups[protPrefix];
                        groups[protPrefix] = (ptp, pfp, pfn + 1);
                    }
                    // FP fuer den KI-Code (die KI hat den falschen Code gemeldet)
                    var kiCode = e.KiDetection?.VsaCode ?? e.KiDetection?.Label;
                    if (!string.IsNullOrEmpty(kiCode) && kiCode.Length >= 3)
                    {
                        var kiPrefix = kiCode[..3].ToUpperInvariant();
                        if (!groups.ContainsKey(kiPrefix))
                            groups[kiPrefix] = (0, 0, 0);
                        var (ktp, kfp, kfn) = groups[kiPrefix];
                        groups[kiPrefix] = (ktp, kfp + 1, kfn);
                    }
                    continue; // Skip das default groups[prefix] Update
            }
        }

        return groups
            .Select(g => new CodeClassMetrics
            {
                VsaCodePrefix = g.Key,
                TP = g.Value.TP,
                FP = g.Value.FP,
                FN = g.Value.FN
            })
            .OrderByDescending(c => c.TP + c.FP + c.FN) // Haeufigste zuerst
            .ToList();
    }
}

/// <summary>Fortschrittsmeldung waehrend des Benchmark-Durchlaufs.</summary>
public sealed record BenchmarkProgress(
    int CurrentHaltung,
    int TotalHaltungen,
    string HaltungId,
    string Status);
