using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Vision;

/// <summary>
/// Phase 5.3 vorbereitend: Pure DTOs der Video-Analyse-Pipeline.
/// Vorher in <c>UI.Ai.VideoFullAnalysisService.cs</c> + <c>UI.Ai.FullProtocolGenerationService.cs</c>
/// + <c>UI.Ai.Pipeline.PipelineTelemetry.cs</c>.
/// </summary>
public sealed record VideoAnalysisResult(
    string VideoPath,
    double DurationSeconds,
    int FramesAnalyzed,
    IReadOnlyList<RawVideoDetection> Detections,
    string? Error,
    TelemetrySummary? Telemetry = null)
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

public sealed record LiveFrameFinding(
    string Label,
    int Severity,
    string? PositionClock,
    int? ExtentPercent,
    string? VsaCodeHint = null,
    int? HeightMm = null,
    int? WidthMm = null,
    int? IntrusionPercent = null,
    int? CrossSectionReductionPercent = null,
    int? DiameterReductionMm = null,
    double? BboxX1 = null,
    double? BboxY1 = null,
    double? BboxX2 = null,
    double? BboxY2 = null);

public sealed record RawVideoDetection(
    string FindingLabel,
    double MeterStart,
    double MeterEnd,
    string Severity,
    string? VsaCodeHint = null,   // direkt aus EnhancedVisionAnalysisService
    string? PositionClock = null, // Uhrlage (1-12 oder "12:00")
    int? ExtentPercent = null,    // Umfangsausdehnung in Prozent
    int? HeightMm = null,
    int? WidthMm = null,
    int? IntrusionPercent = null,
    int? CrossSectionReductionPercent = null,
    int? DiameterReductionMm = null,
    EvidenceVector? Evidence = null,
    // V4.2: Persistierter Frame-Pfad fuer Review-Queue-Anzeige.
    string? FramePath = null,
    double? BboxX1 = null,
    double? BboxY1 = null,
    double? BboxX2 = null,
    double? BboxY2 = null
)
{
    // Für UI-Bindings / Mapping
    public string Code => VsaCodeHint ?? "";
    public string Label => FindingLabel;

    // Simple Heuristik (Severity kommt i.d.R. als "high/mid/low")
    public double Confidence => Severity?.ToLowerInvariant() switch
    {
        "high" => 0.90,
        "mid"  => 0.70,
        "low"  => 0.50,
        _      => 0.60
    };
}

public sealed record MappedProtocolEntry(
    RawVideoDetection Detection,
    string? SuggestedCode,
    double Confidence,
    string? Reason,
    IReadOnlyList<string> Warnings,
    QualityGateResult? QualityGateResult = null,
    UncertaintyEstimate? Uncertainty = null
);

public sealed record CodeMappingProgress(int Done, int Total, string Status)
{
    public double Percent => Total > 0 ? (double)Done / Total * 100.0 : 0;
}

// ── Telemetry (Pure Records — Service-Klasse PipelineTelemetry bleibt in UI) ──

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

// ── Full Protocol Generation (Detection -> ProtocolDocument) ──

public sealed record FullProtocolGenerationRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null
);

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

// ── YOLO-Training-Export ──

public sealed record TrainingExportResult(
    bool IsSuccess,
    string? Error,
    int TotalSamples,
    int TrainCount,
    int ValCount
);

// ── Live-Detection (Player-Codiermodus) ──

/// <summary>
/// Ergebnis der Live-Frame-Analyse im Codiermodus.
/// </summary>
public sealed record LiveDetection(
    double TimestampSeconds,
    IReadOnlyList<LiveFrameFinding> Findings,
    double? MeterReading,
    string? Error);

// ── Knick-Erkennung (BAG via Fluchtpunkt-Tracking) ──

// ── OSD-Meter-Auslesen (Vision/OCR/Linear) ──

public sealed record MeterReadResult(double Value, MeterSource Source);

public enum MeterSource
{
    OsdVision,      // Ollama Vision hat OSD direkt gelesen
    OcrText,        // OCR-Engine hat Text aus Bild gelesen
    LinearEstimate, // Lineare Schaetzung aus Zeit/Dauer
    Unknown
}

// ── Batch-Pipeline (YOLO Batch -> DINO+SAM -> Qwen ×6 parallel) ──

/// <summary>Ergebnis einer Batch-Pipeline-Analyse.</summary>
public sealed record BatchPipelineResult(
    List<BatchFrameAnalysis> Analyses,
    TimeSpan Duration,
    int TotalFrames,
    int RelevantFrames,
    int SkippedFrames);

/// <summary>Einzelne Frame-Analyse aus der Batch-Pipeline.</summary>
public sealed record BatchFrameAnalysis(
    int FrameIndex,
    double TimestampSeconds,
    EnhancedFrameAnalysis QwenResult,
    int YoloDetectionCount,
    // V4.2: Persistierter Frame-Pfad (PNG), gesetzt wenn Findings vorhanden und frameOutputDir gegeben.
    string? FramePath = null);

/// <summary>Fortschritt der Batch-Pipeline.</summary>
public sealed record BatchPipelineProgress(
    int Done,
    int Total,
    string Status);

/// <summary>
/// Ein einzelner Protokoll-Eintrag, auf den sich die Pipeline fokussieren soll.
/// </summary>
public sealed record ProtocolTarget(
    string VsaCode,
    double MeterStart,
    double MeterEnd,
    string? Description = null,
    string? ClockPosition = null);

/// <summary>
/// V4.2 Phase 2.3: Ergebnis einer gerichteten Verifikation durch Qwen.
/// "Ist Code X bei Meter Y im Frame sichtbar?" — Ja/Nein + Schweregrad.
/// </summary>
public sealed record DamageVerification(
    bool Visible,
    int? Severity,
    double Confidence,
    string? Notes)
{
    /// <summary>V4.2 Nachbesserung B: Pipeline-Version fuer Ursachenanalyse.</summary>
    public string PipelineVersion { get; init; } = PipelineVersions.Pipeline;

    /// <summary>V4.2 Nachbesserung B: Prompt-Version (VerifyPrompt).</summary>
    public string PromptVersion { get; init; } = PipelineVersions.VerifyPrompt;
}

/// <summary>
/// Erkannter Knick (BAG) an einer Rohrverbindung.
/// </summary>
public sealed record KnickFinding(
    /// <summary>Winkel in Grad (ab 10° = Knick-Schaden).</summary>
    double AngleDeg,
    /// <summary>Meterstand im Video.</summary>
    double MeterPosition,
    /// <summary>Frame-Index wo der Knick erkannt wurde.</summary>
    int FrameIndex,
    /// <summary>Richtung: positiv = nach rechts/oben, negativ = nach links/unten.</summary>
    double DirectionDeg,
    /// <summary>Konfidenz der Erkennung (0..1).</summary>
    double Confidence,
    /// <summary>Muffe erkannt (stuetzt die Knick-Hypothese).</summary>
    bool JointDetected
);
