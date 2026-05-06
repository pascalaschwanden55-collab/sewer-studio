// AuswertungPro – Video-Selbsttraining Phase 1
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.UI.Ai.Training.Models;
using Microsoft.Extensions.Logging;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>Quelle der Meter-zu-Frame-Zuordnung.</summary>
public enum MeterMappingSource
{
    /// <summary>OSD-Timeline (Meterstand aus Video-Overlay).</summary>
    OSD,
    /// <summary>Zeitstempel aus dem Protokoll (z.B. HH:MM:SS in WinCan).</summary>
    ProtocolTimestamp,
    /// <summary>Lineare Interpolation (Fallback: Meter/Haltungslaenge * Dauer).</summary>
    Linear
}

/// <summary>Zuordnung eines GroundTruthEntry zu einem Video-Frame.</summary>
public sealed record FrameMapping(
    GroundTruthEntry Entry,
    double TimeSeconds,
    string? FramePath,
    MeterMappingSource Source);

/// <summary>
/// Ordnet jedem Protokolleintrag das passende Video-Frame zu.
/// Nutzt die OSD-Timeline (bevorzugt), Protokoll-Timestamps (Fallback) oder
/// lineare Interpolation (letzter Fallback).
/// </summary>
public sealed class MeterToFrameResolver
{
    private readonly MeterTimelineService _timeline;
    private readonly ILogger? _log;

    public MeterToFrameResolver(MeterTimelineService timeline, ILogger? log = null)
    {
        _timeline = timeline ?? throw new ArgumentNullException(nameof(timeline));
        _log = log;
    }

    /// <summary>
    /// Loest alle GroundTruthEntries zu Frame-Zuordnungen auf.
    /// Frames werden als PNG in <paramref name="frameOutputDir"/> gespeichert.
    /// </summary>
    public async Task<List<FrameMapping>> ResolveAllAsync(
        string videoPath,
        double videoDurationSeconds,
        double inspektionslaengeMeter,
        List<GroundTruthEntry> entries,
        string frameOutputDir,
        double centeringOffsetMeter = 0.0,
        CancellationToken ct = default)
    {
        if (entries.Count == 0) return [];

        // 1. OSD-Timeline bauen (versuchen)
        IReadOnlyList<(double TimeSeconds, double Meter)> timeline;
        try
        {
            timeline = await _timeline.BuildTimelineAsync(
                videoPath, videoDurationSeconds, stepSeconds: 5.0, ct).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _log?.LogWarning(ex, "OSD-Timeline konnte nicht gebaut werden, nutze Fallbacks");
            timeline = Array.Empty<(double, double)>();
        }

        Directory.CreateDirectory(frameOutputDir);

        var ffmpegPath = FfmpegLocator.ResolveFfmpeg();
        var result = new List<FrameMapping>(entries.Count);

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            // Ziel-Meter ist die Mitte des Schadens (bei Streckenschaden)
            // + Zentrierungs-Offset: Kamera muss leicht weiter sein, damit das
            // Ereignis vertikal zentriert im Bild steht (nicht am oberen Rand)
            var targetMeter = (entry.IsStreckenschaden
                ? (entry.MeterStart + entry.MeterEnd) / 2.0
                : entry.MeterStart) + centeringOffsetMeter;

            var (timeSeconds, source) = ResolveTimeForMeter(
                targetMeter, timeline, entry.Zeit,
                inspektionslaengeMeter, videoDurationSeconds);

            // Frame extrahieren
            string? framePath = null;
            if (timeSeconds >= 0 && timeSeconds <= videoDurationSeconds)
            {
                var ts = TimeSpan.FromSeconds(timeSeconds);
                var frameBytes = await VideoFrameExtractor.TryExtractFramePngAsync(
                    ffmpegPath, videoPath, ts, ct).ConfigureAwait(false);

                if (frameBytes is not null && frameBytes.Length > 0)
                {
                    var fileName = $"gt_{targetMeter:F1}m_{timeSeconds:F1}s.png";
                    framePath = Path.Combine(frameOutputDir, fileName);
                    await File.WriteAllBytesAsync(framePath, frameBytes, ct).ConfigureAwait(false);
                }
            }

            result.Add(new FrameMapping(entry, timeSeconds, framePath, source));
        }

        return result;
    }

    /// <summary>
    /// Bestimmt den Video-Zeitpunkt fuer einen gegebenen Meterstand.
    /// Fallback-Kaskade: OSD → Protokoll-Timestamp → Linear.
    /// </summary>
    internal static (double TimeSeconds, MeterMappingSource Source) ResolveTimeForMeter(
        double targetMeter,
        IReadOnlyList<(double TimeSeconds, double Meter)> timeline,
        TimeSpan? protocolTimestamp,
        double inspektionslaengeMeter,
        double videoDurationSeconds)
    {
        // Strategie 1: OSD-Timeline (Reverse-Lookup — Meter → Zeit)
        if (timeline.Count >= 2)
        {
            var resolved = ReverseLookupMeter(timeline, targetMeter);
            if (resolved.HasValue)
                return (resolved.Value, MeterMappingSource.OSD);
        }

        // Strategie 2: Protokoll-Timestamp (direkt aus WinCan/IBAK)
        if (protocolTimestamp.HasValue && protocolTimestamp.Value.TotalSeconds > 0)
            return (protocolTimestamp.Value.TotalSeconds, MeterMappingSource.ProtocolTimestamp);

        // Strategie 3: Lineare Interpolation
        if (inspektionslaengeMeter > 0 && videoDurationSeconds > 0)
        {
            var ratio = Math.Clamp(targetMeter / inspektionslaengeMeter, 0, 1);
            return (ratio * videoDurationSeconds, MeterMappingSource.Linear);
        }

        // Absoluter Fallback
        return (0, MeterMappingSource.Linear);
    }

    /// <summary>
    /// Invertiert die OSD-Timeline: Fuer einen Ziel-Meter den naechsten Zeitpunkt finden.
    /// Lineare Interpolation zwischen den zwei umgebenden Stuetzpunkten.
    /// </summary>
    private static double? ReverseLookupMeter(
        IReadOnlyList<(double TimeSeconds, double Meter)> timeline,
        double targetMeter)
    {
        // Timeline muss nach Meter aufsteigend sortiert sein (normalerweise der Fall)
        // Finde die zwei Punkte die den Ziel-Meter umschliessen

        // Sonderfall: Ziel liegt vor dem ersten Punkt
        if (targetMeter <= timeline[0].Meter)
            return timeline[0].TimeSeconds;

        // Sonderfall: Ziel liegt nach dem letzten Punkt
        if (targetMeter >= timeline[^1].Meter)
            return timeline[^1].TimeSeconds;

        for (int i = 0; i < timeline.Count - 1; i++)
        {
            var (t1, m1) = timeline[i];
            var (t2, m2) = timeline[i + 1];

            // Meter muss monoton steigen fuer Interpolation
            if (m2 <= m1) continue;

            if (targetMeter >= m1 && targetMeter <= m2)
            {
                // Lineare Interpolation
                var ratio = (targetMeter - m1) / (m2 - m1);
                return t1 + ratio * (t2 - t1);
            }
        }

        // Kein passendes Segment gefunden — naechsten Punkt suchen
        var closest = timeline
            .OrderBy(p => Math.Abs(p.Meter - targetMeter))
            .First();

        // Nur zurueckgeben wenn die Abweichung unter 5m liegt (OSD-Genauigkeit bei langen Haltungen)
        if (Math.Abs(closest.Meter - targetMeter) < 5.0)
            return closest.TimeSeconds;

        return null;
    }
}
