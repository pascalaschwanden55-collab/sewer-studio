// AuswertungPro – Video-Selbsttraining Phase 4
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Zeitreihen-Speicherung fuer Benchmark-Durchlaeufe.
/// Ermoeglicht Fortschrittsmessung und Regressions-Erkennung ueber die Zeit.
/// </summary>
public sealed class BenchmarkMetricsStore
{
    private const int MaxHistoryEntries = 50;

    private static string FilePath => Path.Combine(
        KnowledgeRootProvider.GetRoot(), "benchmark_metrics.json");

    private readonly SemaphoreSlim _lock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public async Task<List<BenchmarkRunResult>> LoadHistoryAsync(CancellationToken ct = default)
    {
        var path = FilePath;
        if (!File.Exists(path)) return [];

        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
            return JsonSerializer.Deserialize<List<BenchmarkRunResult>>(json, JsonOpts) ?? [];
        }
        finally { _lock.Release(); }
    }

    public async Task AppendRunAsync(BenchmarkRunResult run, CancellationToken ct = default)
    {
        await _lock.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            var path = FilePath;
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);

            List<BenchmarkRunResult> history;
            if (File.Exists(path))
            {
                var json = await File.ReadAllTextAsync(path, ct).ConfigureAwait(false);
                history = JsonSerializer.Deserialize<List<BenchmarkRunResult>>(json, JsonOpts) ?? [];
            }
            else
            {
                history = [];
            }

            history.Add(run);

            // Max-Entries begrenzen (aelteste zuerst entfernen)
            while (history.Count > MaxHistoryEntries)
                history.RemoveAt(0);

            // Atomares Speichern
            var tmp = path + ".tmp";
            var newJson = JsonSerializer.Serialize(history, JsonOpts);
            await File.WriteAllTextAsync(tmp, newJson, ct).ConfigureAwait(false);
            File.Move(tmp, path, overwrite: true);
        }
        finally { _lock.Release(); }
    }

    /// <summary>
    /// Prueft ob der aktuelle Lauf eine Regression zeigt.
    /// Schwellen: Gesamt-F1 -5% relativ, Per-Code-F1 -10% relativ.
    /// </summary>
    public static RegressionCheck CheckForRegression(
        BenchmarkRunResult current,
        IReadOnlyList<BenchmarkRunResult> history,
        ILogger? logger = null)
    {
        if (history.Count < 2)
            return new RegressionCheck(false, 0, 0, 0, null, []);

        // Durchschnitt der letzten 3 Laeufe (oder weniger)
        var recent = history
            .OrderByDescending(r => r.TimestampUtc)
            .Take(3)
            .ToList();

        var avgF1 = recent.Average(r => r.F1);
        var avgPrecision = recent.Average(r => r.Precision);
        var avgRecall = recent.Average(r => r.Recall);

        // M11: Bei avgF1 <= 0 keine sinnvolle Regression moeglich — sichtbar machen
        if (avgF1 <= 0)
        {
            logger?.LogWarning(
                "Regressions-Check uebersprungen: historischer F1-Schnitt=0 ueber {Count} Laeufe " +
                "(neue Serie oder vollstaendiges Modell-Versagen). Aktueller F1={CurF1:F3}",
                recent.Count, current.F1);
            return new RegressionCheck(false, 0, 0, 0, null, []);
        }

        var f1Delta = current.F1 - avgF1;
        var precisionDelta = current.Precision - avgPrecision;
        var recallDelta = current.Recall - avgRecall;

        // Relative Verschlechterung pruefen
        bool hasRegression = (f1Delta / avgF1) < -0.05;

        // Per-Code Regression
        var regressedCodes = new List<string>();
        if (current.PerCodeMetrics is not null)
        {
            foreach (var code in current.PerCodeMetrics)
            {
                var historicalAvg = recent
                    .SelectMany(r => r.PerCodeMetrics ?? [])
                    .Where(c => c.VsaCodePrefix == code.VsaCodePrefix)
                    .Select(c => c.F1)
                    .DefaultIfEmpty(0)
                    .Average();

                if (historicalAvg > 0 && code.F1 < historicalAvg * 0.90)
                    regressedCodes.Add(code.VsaCodePrefix);
            }
        }

        if (regressedCodes.Count > 0)
            hasRegression = true;

        string? detail = null;
        if (hasRegression)
        {
            var parts = new List<string>();
            if (f1Delta < 0 && avgF1 > 0)
                parts.Add($"Gesamt-F1: {avgF1:P1} → {current.F1:P1} ({f1Delta:+0.0%;-0.0%})");
            if (regressedCodes.Count > 0)
                parts.Add($"Regredierte Codes: {string.Join(", ", regressedCodes)}");
            detail = string.Join(" | ", parts);
        }

        return new RegressionCheck(hasRegression, f1Delta, precisionDelta, recallDelta, detail, regressedCodes);
    }
}

/// <summary>Ergebnis eines Benchmark-Durchlaufs.</summary>
public sealed class BenchmarkRunResult
{
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public TimeSpan Duration { get; set; }
    public double Precision { get; set; }
    public double Recall { get; set; }
    public double F1 { get; set; }
    public int TotalProtocolEntries { get; set; }
    public int TotalDetections { get; set; }
    public List<CodeClassMetrics>? PerCodeMetrics { get; set; }
    public bool HasRegression { get; set; }
    public string? RegressionDetail { get; set; }
}

/// <summary>Metriken pro VSA-Code-Klasse.</summary>
public sealed class CodeClassMetrics
{
    public required string VsaCodePrefix { get; set; }
    public int TP { get; set; }
    public int FP { get; set; }
    public int FN { get; set; }
    public double Precision => (TP + FP) > 0 ? (double)TP / (TP + FP) : 0;
    public double Recall => (TP + FN) > 0 ? (double)TP / (TP + FN) : 0;
    public double F1 { get
    {
        var p = Precision;
        var r = Recall;
        return (p + r) > 0 ? 2 * p * r / (p + r) : 0;
    }}
}

/// <summary>Ergebnis der Regressions-Pruefung.</summary>
public sealed record RegressionCheck(
    bool HasRegression,
    double F1Delta,
    double PrecisionDelta,
    double RecallDelta,
    string? Detail,
    List<string> RegressedCodes);
