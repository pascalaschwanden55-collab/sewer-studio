using System;
using System.Collections.Generic;
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

    public MeterTimelineService(AiRuntimeConfig cfg, OsdMeterDetectionService? osd = null)
    {
        _cfg = cfg;
        _osd = osd;
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
        var raw = new List<(double TimeSeconds, double? Meter)>();

        for (var t = 0.0; t < videoDurationSeconds; t += stepSeconds)
        {
            ct.ThrowIfCancellationRequested();

            var bytes = await VideoFrameExtractor.TryExtractFramePngAsync(
                ffmpeg, videoPath, TimeSpan.FromSeconds(t), ct)
                .ConfigureAwait(false);

            double? meter = null;
            if (bytes is not null)
            {
                var base64 = Convert.ToBase64String(bytes);
                var result = await _osd.ReadMeterAsync(base64, null, ct).ConfigureAwait(false);
                if (result.Source != MeterSource.Unknown)
                    meter = result.Value;
            }

            raw.Add((t, meter));
        }

        return OsdMeterDetectionService.SmoothMeterTimeline(raw);
    }
}
