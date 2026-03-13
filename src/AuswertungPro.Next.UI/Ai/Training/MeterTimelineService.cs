using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Baut eine OSD-Meter-Zeitreihe aus einem Video auf.
/// Nur aktiv wenn AiRuntimeConfig.Enabled = true und ein OsdMeterDetectionService vorhanden.
/// </summary>
public sealed class MeterTimelineService
{
    private readonly AiRuntimeConfig _cfg;
    private readonly OsdMeterDetectionService? _osd;
    private readonly int _concurrency;

    public MeterTimelineService(AiRuntimeConfig cfg, OsdMeterDetectionService? osd = null, int concurrency = 1)
    {
        _cfg = cfg;
        _osd = osd;
        _concurrency = Math.Max(1, concurrency);
    }

    /// <summary>
    /// Interpoliert Meterstand aus einer Timeline für einen Zeitpunkt.
    /// </summary>
    public static double? InterpolateMeter(
        IReadOnlyList<(double TimeSeconds, double Meter)> timeline,
        double timeSeconds)
    {
        if (timeline.Count == 0) return null;
        if (timeline.Count == 1) return timeline[0].Meter;

        for (var i = 0; i < timeline.Count - 1; i++)
        {
            var (t0, m0) = timeline[i];
            var (t1, m1) = timeline[i + 1];
            if (timeSeconds >= t0 && timeSeconds <= t1)
            {
                var frac = (t1 == t0) ? 0.0 : (timeSeconds - t0) / (t1 - t0);
                return m0 + frac * (m1 - m0);
            }
        }

        return timeSeconds <= timeline[0].TimeSeconds
            ? timeline[0].Meter
            : timeline[^1].Meter;
    }

    /// <summary>
    /// Baut Timeline aus dem Video: sampelt alle stepSeconds Sekunden,
    /// liest OSD-Meter (wenn enabled), gibt geglättete Zeitreihe zurück.
    /// Gibt leere Liste zurück wenn AI deaktiviert oder kein OSD-Service vorhanden.
    /// </summary>
    public async Task<IReadOnlyList<(double TimeSeconds, double Meter)>> BuildTimelineAsync(
        string videoPath,
        double videoDurationSeconds,
        double stepSeconds = 5.0,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled || _osd is null)
            return Array.Empty<(double, double)>();

        var ffmpeg = _cfg.FfmpegPath ?? "ffmpeg";

        // Collect all frames first (fast — ffmpeg decodes, no GPU needed)
        var frames = new List<(int Index, FrameData Frame)>();
        int idx = 0;

        await using var stream = VideoFrameStream.Open(
            ffmpeg, videoPath, stepSeconds, videoDurationSeconds, ct);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            frames.Add((idx++, frame));
        }

        // Parallel OSD reads — keeps GPU busy with multiple concurrent requests
        var results = new ConcurrentDictionary<int, (double Time, double? Meter)>();

        await Parallel.ForEachAsync(frames, new ParallelOptions
        {
            MaxDegreeOfParallelism = _concurrency,
            CancellationToken = ct
        }, async (item, token) =>
        {
            double? meter = null;
            if (item.Frame.PngBytes is { Length: > 0 })
            {
                var base64 = Convert.ToBase64String(item.Frame.PngBytes);
                var result = await _osd.ReadMeterAsync(base64, null, token).ConfigureAwait(false);
                if (result.Source != MeterSource.Unknown)
                    meter = result.Value;
            }
            results[item.Index] = (item.Frame.TimestampSeconds, meter);
        });

        // Reassemble in original order
        var raw = results.OrderBy(kv => kv.Key)
            .Select(kv => (kv.Value.Time, kv.Value.Meter))
            .ToList();

        return OsdMeterDetectionService.SmoothMeterTimeline(raw);
    }

    /// <summary>
    /// Protokoll-gesteuerte Timeline: Scannt nur ±windowSeconds um jede bekannte Schadenstelle.
    /// Drastisch schneller als Full-Scan (z.B. 13 Stellen × 3 Frames statt 51 Frames bei 255s Video).
    /// </summary>
    public async Task<IReadOnlyList<(double TimeSeconds, double Meter)>> BuildTargetedTimelineAsync(
        string videoPath,
        double videoDurationSeconds,
        IReadOnlyList<double> targetTimesSeconds,
        double windowSeconds = 20.0,
        double stepSeconds = 5.0,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled || _osd is null || targetTimesSeconds.Count == 0)
            return Array.Empty<(double, double)>();

        // Zeitfenster berechnen und zu Abfragezeitpunkten zusammenfuegen
        var queryTimes = new SortedSet<double>();
        foreach (var t in targetTimesSeconds)
        {
            var tStart = Math.Max(0, t - windowSeconds);
            var tEnd = Math.Min(videoDurationSeconds, t + windowSeconds);
            // Frames innerhalb des Fensters (alle stepSeconds)
            for (var s = tStart; s <= tEnd; s += stepSeconds)
                queryTimes.Add(Math.Round(s, 1));
            // Exakten Schadenszeitpunkt immer einschliessen
            queryTimes.Add(Math.Round(Math.Clamp(t, 0, videoDurationSeconds), 1));
        }

        var ffmpeg = _cfg.FfmpegPath ?? "ffmpeg";

        // Frames gezielt extrahieren (ffmpeg seek pro Zeitpunkt)
        var frames = new List<(int Index, FrameData Frame)>();
        var sortedTimes = queryTimes.ToList();
        for (var i = 0; i < sortedTimes.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var frameData = await VideoFrameStream.ExtractSingleFrameAsync(
                ffmpeg, videoPath, sortedTimes[i], ct).ConfigureAwait(false);
            if (frameData is { } fd)
                frames.Add((i, fd));
        }

        // Parallel OSD-Ablesen
        var results = new ConcurrentDictionary<int, (double Time, double? Meter)>();
        await Parallel.ForEachAsync(frames, new ParallelOptions
        {
            MaxDegreeOfParallelism = _concurrency,
            CancellationToken = ct
        }, async (item, token) =>
        {
            double? meter = null;
            if (item.Frame.PngBytes is { Length: > 0 })
            {
                var base64 = Convert.ToBase64String(item.Frame.PngBytes);
                var result = await _osd.ReadMeterAsync(base64, null, token).ConfigureAwait(false);
                if (result.Source != MeterSource.Unknown)
                    meter = result.Value;
            }
            results[item.Index] = (item.Frame.TimestampSeconds, meter);
        });

        var raw = results.OrderBy(kv => kv.Key)
            .Select(kv => (kv.Value.Time, kv.Value.Meter))
            .ToList();

        return OsdMeterDetectionService.SmoothMeterTimeline(raw);
    }
}
