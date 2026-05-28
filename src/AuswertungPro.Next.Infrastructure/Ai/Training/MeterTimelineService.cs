using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Baut eine OSD-Meter-Zeitreihe aus einem Video auf.
/// </summary>
public sealed class MeterTimelineService
{
    private readonly AiRuntimeSettings _cfg;
    private readonly OsdMeterDetectionService? _osd;
    private readonly int _concurrency;

    public MeterTimelineService(AiRuntimeSettings cfg, OsdMeterDetectionService? osd = null, int concurrency = 1)
    {
        _cfg = cfg;
        _osd = osd;
        _concurrency = Math.Max(1, concurrency);
    }

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

    public async Task<IReadOnlyList<(double TimeSeconds, double Meter)>> BuildTimelineAsync(
        string videoPath,
        double videoDurationSeconds,
        double stepSeconds = 5.0,
        CancellationToken ct = default)
    {
        if (!_cfg.Enabled || _osd is null)
            return Array.Empty<(double, double)>();

        var ffmpeg = _cfg.FfmpegPath ?? "ffmpeg";
        var frames = new List<(int Index, FrameData Frame)>();
        var idx = 0;

        await using var stream = VideoFrameStream.Open(
            ffmpeg, videoPath, stepSeconds, videoDurationSeconds, ct);

        await foreach (var frame in stream.ReadFramesAsync(ct).ConfigureAwait(false))
        {
            ct.ThrowIfCancellationRequested();
            frames.Add((idx++, frame));
        }

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
