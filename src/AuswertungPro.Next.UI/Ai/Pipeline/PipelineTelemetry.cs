using System;
using AuswertungPro.Next.Application.Ai.Vision;
using System.Collections.Generic;
using System.Linq;
using AuswertungPro.Next.Domain.Ai.Vision;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

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
        } // lock
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

// Phase 5.3 vorbereitend: FrameTiming, TelemetrySummary, PhaseStat
// nach Domain/Ai/Vision/VideoAnalysisModels.cs.
