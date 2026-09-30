using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Veraenderlicher Zustand genau eines Laufs von <see cref="MultiModelAnalysisService.AnalyzeAsync"/>.
/// Vorher lief dieser Zustand als lokale Variablen und drei Dienstfelder durch den Ablauf; jetzt
/// hat er einen Ort und eine klare Lebensdauer: Er entsteht zu Beginn eines Laufs und endet mit
/// dem Ergebnis. Der Dienst selbst haelt dadurch zwischen zwei Laeufen keinen Laufzustand mehr
/// (Code-Voting, Qwen-Vorbefund und Neustart-Budget wurden vorher je Lauf zurueckgesetzt).
/// Enthaelt keine Entscheidungen, nur Werte.
/// </summary>
internal sealed class MultiModelLaufZustand
{
    /// <summary>Folge-Frames mit Sidecar-Transportfehler bis zum Abbruch; gleiche Grenze fuer die Qwen-Notiz.</summary>
    public const int SidecarOutageLimit = 8;

    public MultiModelLaufZustand(
        string videoPath,
        double duration,
        int totalFrames,
        int pipeDiameterMm,
        TemporalFindingDeduplicator deduplicator,
        IProgress<VideoAnalysisProgress>? progress)
    {
        VideoPath = videoPath;
        Duration = duration;
        TotalFrames = totalFrames;
        PipeDiameterMm = pipeDiameterMm;
        Deduplicator = deduplicator;
        Progress = progress;
    }

    // ── Eingaben des Laufs (unveraenderlich) ──
    public string VideoPath { get; }
    public double Duration { get; }
    public int TotalFrames { get; }

    /// <summary>Haltungs-DN vor globaler Vorgabe; 0 = unbekannt (keine erfundenen Masse).</summary>
    public int PipeDiameterMm { get; }

    public IProgress<VideoAnalysisProgress>? Progress { get; }

    /// <summary>Kennung fuer den Stufen-Trace (reine Sichtbarkeit).</summary>
    public string RunId { get; set; } = string.Empty;

    // ── Befunde und Zusammenfuehrung ──
    public List<RawVideoDetection> Detections { get; } = new();
    public TemporalFindingDeduplicator Deduplicator { get; }

    /// <summary>Temporal-Voting gegen Einzelbild-Ausreisser; Fenster gilt pro Lauf.</summary>
    public ITemporalCodeVotingService CodeVoting { get; } = new TemporalCodeVotingService();

    /// <summary>Qwen-Schritt dieses Laufs (haelt den Vorbefund-Kontext); null = ohne Qwen.</summary>
    public MultiModelQwenSchritt? Qwen { get; set; }

    // ── Fortschritt und Wiederaufnahme ──
    public int FrameIndex { get; set; }
    public int SkippedFrames { get; set; }
    public double LastMeter { get; set; }

    /// <summary>Zuletzt aus dem Checkpoint-Journal uebernommener Frame (0 = frischer Lauf).</summary>
    public int ResumedFrames { get; set; }

    public PipelineTelemetry Telemetry { get; } = new();

    // ── Detektor-Qualifikation (kann sich waehrend des Laufs verschlechtern) ──
    public bool DetectorQualified { get; set; }
    public bool? EffectiveDetectorQualified { get; set; }
    public string? DetectorQualificationReason { get; set; }
    public bool YoloFallbackWarned { get; set; }

    // ── Fehlerzaehler, Ausfallschutz und Vollstaendigkeit ──
    public SidecarOutageGuard OutageGuard { get; } = new(SidecarOutageLimit);
    public QwenOutageTracker QwenOutage { get; } = new(SidecarOutageLimit);
    public MultiModelRunCompleteness Completeness { get; } = new();
    public bool SidecarOutage { get; set; }

    /// <summary>Erste VRAM-Mangel-Meldung des Laufs (Degraded-Grund mit VRAM-Zahlen).</summary>
    public string? VramInsufficientMessage { get; set; }

    /// <summary>Neustart-Budget: genau ein kontrollierter Sidecar-Neustart pro Lauf.</summary>
    public bool SidecarRestartAttempted { get; set; }
}
