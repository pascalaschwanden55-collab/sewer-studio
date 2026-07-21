using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai;

public interface IVideoAnalysisPipelineService
{
    Task<PipelineResult> RunAsync(
        PipelineRequest request,
        IProgress<PipelineProgress>? progress = null,
        CancellationToken ct = default);
}

public sealed record PipelineRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null,
    double FrameStepSeconds = 3.0,
    int DedupWindowFrames = 3,
    // Echte Haltungslaenge aus den Stammdaten (Haltungslaenge_m). null = unbekannt,
    // dann gilt die 50m-Annahme der Meter-Schaetzung.
    double? ReachLengthM = null);

public sealed record PipelineResult(
    ProtocolDocument? Document,
    IReadOnlyList<RawVideoDetection> Detections,
    IReadOnlyList<MappedProtocolEntry> MappedEntries,
    PipelineStats? Stats,
    IReadOnlyList<string> Warnings,
    string? Error,
    TelemetrySummary? Telemetry = null)
{
    public bool IsSuccess => Error is null;

    public static PipelineResult Failed(string error) =>
        new(null, Array.Empty<RawVideoDetection>(), Array.Empty<MappedProtocolEntry>(), null, Array.Empty<string>(), error);
}

public sealed record PipelineStats(
    int FramesAnalyzed,
    double DurationSeconds,
    int DetectionsRaw,
    int EntriesGenerated,
    int EntriesWithHighConfidence);

public sealed record PipelineProgress(
    PipelinePhase Phase,
    double PercentInPhase,
    string Status,
    int? FramesDone = null,
    int? FramesTotal = null,
    int? ItemsDone = null,
    int? ItemsTotal = null,
    byte[]? FramePreviewPng = null,
    IReadOnlyList<LiveFrameFinding>? LiveFindings = null);

public enum PipelinePhase
{
    VideoAnalysis,
    MultiModelDetection,
    CodeMapping,
    Done
}

public sealed record VideoAnalysisResult(
    string VideoPath,
    double DurationSeconds,
    int FramesAnalyzed,
    IReadOnlyList<RawVideoDetection> Detections,
    string? Error,
    TelemetrySummary? Telemetry = null,
    // Degraded=true: Lauf ist durchgelaufen, aber wichtige Frames scheiterten (z. B. Sidecar-
    // Ausfall mitten im Video). Das Ergebnis ist unvollstaendig und darf NICHT wie ein sauberes
    // Rohr behandelt werden; die UI weist es aus. IsSuccess bleibt true (Ergebnis ist nutzbar).
    bool Degraded = false,
    string? DegradedReason = null)
{
    public bool IsSuccess => Error is null;

    public static VideoAnalysisResult Failed(string error) =>
        new(string.Empty, 0, 0, Array.Empty<RawVideoDetection>(), error);
}

public sealed record VideoAnalysisProgress(
    int FramesDone,
    int FramesTotal,
    string Status,
    byte[]? FramePreviewPng = null,
    IReadOnlyList<LiveFrameFinding>? LiveFindings = null)
{
    public double Percent => FramesTotal > 0 ? (double)FramesDone / FramesTotal * 100.0 : 0;
}

public sealed record RawVideoDetection(
    string FindingLabel,
    double MeterStart,
    double MeterEnd,
    string Severity,
    string? VsaCodeHint = null,
    string? PositionClock = null,
    int? ExtentPercent = null,
    int? HeightMm = null,
    int? WidthMm = null,
    int? IntrusionPercent = null,
    int? CrossSectionReductionPercent = null,
    int? DiameterReductionMm = null,
    EvidenceVector? Evidence = null,
    string? MeterSource = null,
    bool IsMeterEstimated = false)
{
    public string Code => VsaCodeHint ?? string.Empty;
    public string Label => FindingLabel;

    // ACHTUNG: abgeleitet aus dem SCHADENSGRAD (high/mid/low), keine Modell-Sicherheit.
    // Nur fuer Anzeige/Overlay-Zwecke — darf NIE als Freigabe-Signal dienen
    // (Fehlerpruefung 11.07., Kritisch 3; die Freigabe nutzt MappedProtocolEntry.Confidence
    // = QualityGate-Composite). Abloesung durch echte Pipeline-Werte: Stufe 2.
    public double Confidence => Severity?.ToLowerInvariant() switch
    {
        "high" => 0.90,
        "mid" => 0.70,
        "low" => 0.50,
        _ => 0.60
    };
}

public sealed record FullProtocolGenerationRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null);

public sealed record FullProtocolGenerationResult(
    ProtocolDocument? Document,
    IReadOnlyList<MappedProtocolEntry> MappedEntries,
    string? Error,
    IReadOnlyList<string> Warnings)
{
    public bool IsSuccess => Error is null;

    public static FullProtocolGenerationResult Failed(string error) =>
        new(null, Array.Empty<MappedProtocolEntry>(), error, Array.Empty<string>());
}

public sealed record MappedProtocolEntry(
    RawVideoDetection Detection,
    string? SuggestedCode,
    double Confidence,
    string? Reason,
    IReadOnlyList<string> Warnings,
    QualityGateResult? QualityGateResult = null,
    UncertaintyEstimate? Uncertainty = null,
    // Strukturiertes Urteil der zentralen Freigabe-Regel (Fehlerpruefung 11.07.,
    // Kritisch 2): 3 Outcomes + Grund, fuer Anzeige und Auswahl VOR der Uebernahme.
    AiDecision? Freigabe = null,
    // Stabile Kennung, die der Protokolleintrag uebernimmt — verbindet die
    // Fenster-Auswahl mit dem Dokument (Uebernahme-Filter).
    Guid EntryId = default,
    // Laufzeit-Identitaeten fuer die spaetere Erklaerbarkeit des KI-Urteils.
    string? VisionModel = null,
    string? TextModel = null,
    string? QualityGateVersion = null);

public sealed record CodeMappingProgress(int Done, int Total, string Status)
{
    public double Percent => Total > 0 ? (double)Done / Total * 100.0 : 0;
}

public sealed record FrameTiming(
    int FrameIndex,
    double TimestampSec,
    long ExtractionMs,
    long YoloMs,
    long DinoMs,
    long SamMs,
    long QwenMs,
    long TotalMs,
    bool Skipped);

public sealed record TelemetrySummary(
    int TotalFrames,
    int SkippedFrames,
    PhaseStat Extraction,
    PhaseStat Yolo,
    PhaseStat Dino,
    PhaseStat Sam,
    PhaseStat Qwen,
    PhaseStat Total,
    long WallClockMs);

public sealed record PhaseStat(
    double MeanMs,
    double MedianMs,
    double P95Ms,
    long TotalMs);
