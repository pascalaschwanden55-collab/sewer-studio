using System;
using System.Collections.Generic;
using AuswertungPro.Next.Domain.Ai.Vision;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Ai.Vision;

/// <summary>Request fuer den Video-Analyse-Pipeline-Workflow.</summary>
public sealed record PipelineRequest(
    string HaltungId,
    string VideoPath,
    IReadOnlyList<string> AllowedCodes,
    string? ProjectFolderAbs = null,
    string? RequestedBy = null,
    double FrameStepSeconds = 3.0,
    int DedupWindowFrames = 3
);

/// <summary>Ergebnis des Video-Analyse-Pipeline-Workflows.</summary>
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
        new(null, Array.Empty<RawVideoDetection>(),
            Array.Empty<MappedProtocolEntry>(), null,
            Array.Empty<string>(), error);
}

public sealed record PipelineStats(
    int FramesAnalyzed,
    double DurationSeconds,
    int DetectionsRaw,
    int EntriesGenerated,
    int EntriesWithHighConfidence
);

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
