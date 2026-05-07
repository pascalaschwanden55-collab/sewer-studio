using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>
/// Collects per-frame timing data and produces aggregate statistics.
/// </summary>
public sealed class PipelineTelemetry
{
    private readonly object _lock = new();
    private readonly List<FrameTiming> _frames = new();
    private readonly System.Diagnostics.Stopwatch _wallClock = System.Diagnostics.Stopwatch.StartNew();

    public IReadOnlyList<FrameTiming> Frames { get { lock (_lock) { return _frames.ToList(); } } }

    public void RecordFrame(FrameTiming timing) { lock (_lock) { _frames.Add(timing); } }

    /// <summary>
    /// Sprint 2 (2026-05-07): Optionaler zusaetzlicher Persister
    /// (typisch: SQLite-Telemetry-Store fuer Drift-Auswertung). Wird NACH
    /// dem JSONL-Schreiben aufgerufen. Eigene Exceptions werden geschluckt
    /// (Debug-Log) — die JSONL-Persistierung bleibt der zuverlaessige Pfad.
    /// </summary>
    public Func<string, TelemetrySummary, CancellationToken, Task>? AdditionalPersister { get; set; }

    /// <summary>
    /// Audit 2026-05-06 Top-10: Pipeline-Telemetry-Persistierung. Schreibt
    /// die aktuelle Summary als JSONL-Zeile in
    /// <c>%LOCALAPPDATA%/SewerStudio/logs/pipeline_telemetry.jsonl</c> (oder
    /// einen vom Aufrufer angegebenen Pfad). Nicht-blockierend dank
    /// SemaphoreSlim, damit parallele Pipeline-Laeufe sich nicht ueberschreiben.
    ///
    /// Schema pro Zeile:
    ///   { "ts": "...", "label": "...", "totalFrames": 0, "skippedFrames": 0,
    ///     "wallClockMs": 0, "yolo": { "meanMs": ..., ... }, ... }
    /// </summary>
    public async Task PersistSummaryAsync(
        string label,
        string? customPath = null,
        CancellationToken ct = default)
    {
        var summary = GetSummary();
        var path = customPath ?? GetDefaultLogPath();
        var dir = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

        var entry = new
        {
            ts = DateTime.UtcNow.ToString("O"),
            label,
            totalFrames = summary.TotalFrames,
            skippedFrames = summary.SkippedFrames,
            wallClockMs = summary.WallClockMs,
            extraction = summary.Extraction,
            yolo = summary.Yolo,
            dino = summary.Dino,
            sam = summary.Sam,
            qwen = summary.Qwen,
            total = summary.Total,
        };
        var json = JsonSerializer.Serialize(entry);

        await _persistGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            await File.AppendAllTextAsync(path, json + Environment.NewLine, ct).ConfigureAwait(false);
        }
        finally
        {
            _persistGate.Release();
        }

        // Sprint 2: zusaetzlicher Persister (z.B. SQLite). Failures schlucken,
        // weil JSONL-Pfad bereits erfolgreich war.
        if (AdditionalPersister is not null)
        {
            try
            {
                await AdditionalPersister(label, summary, ct).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PipelineTelemetry] AdditionalPersister failed: {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    private static readonly SemaphoreSlim _persistGate = new(1, 1);

    private static string GetDefaultLogPath() => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SewerStudio", "logs", "pipeline_telemetry.jsonl");

    public TelemetrySummary GetSummary()
    {
        lock (_lock)
        {
            if (_wallClock.IsRunning) _wallClock.Stop();

            var active = _frames.Where(f => !f.Skipped).ToList();
            var skipped = _frames.Count(f => f.Skipped);

            return new TelemetrySummary(
                TotalFrames: _frames.Count,
                SkippedFrames: skipped,
                Extraction: ComputePhase(active, f => f.ExtractionMs),
                Yolo: ComputePhase(active, f => f.YoloMs),
                Dino: ComputePhase(active, f => f.DinoMs),
                Sam: ComputePhase(active, f => f.SamMs),
                Qwen: ComputePhase(active, f => f.QwenMs),
                Total: ComputePhase(active, f => f.TotalMs),
                WallClockMs: _wallClock.ElapsedMilliseconds);
        }
    }

    private static PhaseStat ComputePhase(
        IReadOnlyList<FrameTiming> frames,
        Func<FrameTiming, long> selector)
    {
        if (frames.Count == 0)
            return new PhaseStat(0, 0, 0, 0);

        var values = frames.Select(selector).ToArray();
        Array.Sort(values);

        var total = values.Sum();
        var mean = (double)total / values.Length;
        var median = Percentile(values, 0.50);
        var p95 = Percentile(values, 0.95);

        return new PhaseStat(
            MeanMs: Math.Round(mean, 1),
            MedianMs: Math.Round(median, 1),
            P95Ms: Math.Round(p95, 1),
            TotalMs: total);
    }

    private static double Percentile(long[] sorted, double p)
    {
        if (sorted.Length == 0) return 0;
        if (sorted.Length == 1) return sorted[0];

        var index = p * (sorted.Length - 1);
        var lower = (int)Math.Floor(index);
        var upper = Math.Min(lower + 1, sorted.Length - 1);
        var frac = index - lower;
        return sorted[lower] + frac * (sorted[upper] - sorted[lower]);
    }
}
