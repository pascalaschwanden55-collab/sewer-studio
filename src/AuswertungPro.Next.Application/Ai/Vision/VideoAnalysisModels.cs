using System;
using System.Collections.Generic;
using AuswertungPro.Next.Application.Ai.QualityGate;

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
