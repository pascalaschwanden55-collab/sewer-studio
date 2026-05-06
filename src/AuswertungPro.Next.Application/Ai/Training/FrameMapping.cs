using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Application.Ai.Training;

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
