using System;

namespace AuswertungPro.Next.Application.Ai.Pipeline;

/// <summary>Ein Yellow/Red-Frame in der Eskalations-Queue.</summary>
public sealed record EscalationItem
{
    public string? FrameBase64 { get; init; }
    public string? FrameId { get; init; }
    public string? VideoPath { get; init; }
    public string? HaltungName { get; init; }
    public double MeterPosition { get; init; }
    public string? EscalationReason { get; init; }
    public string? FirstResultJson { get; init; }
    public DateTimeOffset EnqueuedAt { get; init; } = DateTimeOffset.UtcNow;
}

/// <summary>
/// Einzelne YOLO-Detektion aus einem Frame — Eingabe fuer den DetectionAggregator.
/// </summary>
public sealed record FrameDetection
{
    /// <summary>YOLO-Klassen-ID (0-9).</summary>
    public required int YoloClassId { get; init; }

    /// <summary>YOLO-Klassenname, z.B. "crack", "root".</summary>
    public required string YoloClassName { get; init; }

    /// <summary>YOLO-Konfidenz (0.0 – 1.0).</summary>
    public required double Confidence { get; init; }

    /// <summary>Zeitpunkt im Video in Sekunden.</summary>
    public required double TimeSeconds { get; init; }

    /// <summary>Meterstand der Kamera zum Zeitpunkt der Detektion.</summary>
    public required double Meter { get; init; }

    /// <summary>Pfad zum extrahierten Frame-PNG.</summary>
    public required string FramePath { get; init; }

    /// <summary>Normalisierte Bounding-Box (x1, y1, x2, y2), optional.</summary>
    public double[]? Bbox { get; init; }
}

/// <summary>
/// Aggregiertes Detektionsereignis — repraesentiert EINEN Schaden,
/// verdichtet aus mehreren Frame-Detektionen durch den DetectionAggregator.
/// </summary>
public sealed record DetectionEvent
{
    /// <summary>YOLO-Klassen-ID (0-9).</summary>
    public required int YoloClassId { get; init; }

    /// <summary>YOLO-Klassenname, z.B. "crack", "root".</summary>
    public required string YoloClassName { get; init; }

    /// <summary>Hoechste YOLO-Konfidenz ueber alle Frames hinweg.</summary>
    public required double PeakConfidence { get; init; }

    /// <summary>Pfad zum Frame-PNG mit der hoechsten Konfidenz.</summary>
    public required string PeakFramePath { get; init; }

    /// <summary>Zeitpunkt im Video des Peak-Frames in Sekunden.</summary>
    public required double PeakTimeSeconds { get; init; }

    /// <summary>Meterstand am Beginn der Detektion.</summary>
    public required double MeterStart { get; init; }

    /// <summary>Meterstand am Ende der Detektion (= MeterStart bei Punktschaden).</summary>
    public required double MeterEnd { get; init; }

    /// <summary>Anzahl Frames, in denen der Schaden sichtbar war.</summary>
    public required int FrameCount { get; init; }

    /// <summary>Normalisierte Bounding-Box des Peak-Frames (x1, y1, x2, y2), optional.</summary>
    public double[]? PeakBbox { get; init; }

    /// <summary>True, wenn Qwen diesen Schaden bereits klassifiziert hat.</summary>
    public bool IsClassified { get; set; }

    /// <summary>VSA-Code nach Qwen-Klassifikation, z.B. "BAB-A".</summary>
    public string? VsaCode { get; set; }

    /// <summary>Schweregrad 1-5 nach Klassifikation.</summary>
    public int? Severity { get; set; }

    /// <summary>Uhrlage nach Klassifikation, z.B. "3:00-6:00".</summary>
    public string? ClockPosition { get; set; }
}
