using System;
using System.Collections.Generic;

namespace AuswertungPro.Next.Application.Ai.Annotation;

/// <summary>
/// Eingabe fuer PreviewMaskAsync und CommitAsync. Die UI hat den Frame
/// bereits nach <see cref="FramePath"/> geschrieben (typisch ein temp-Pfad,
/// der bei Commit nach KI_BRAIN/frames/&lt;CaseId&gt;/&lt;SampleId&gt;.png finalisiert wird).
/// </summary>
public sealed record AnnotationRequest(
    string CaseId,
    string Code,
    double ProtocolMeterstand,
    double SuggestedFrameTimeSeconds,
    double ActualFrameTimeSeconds,
    int VideoFrameIndex,
    string FramePath,
    int FrameWidth,
    int FrameHeight,
    BoundingBoxNormalized Box);

/// <summary>
/// Bounding-Box im YOLO-Format (Center + Size, normalisiert 0..1).
/// </summary>
public sealed record BoundingBoxNormalized(
    double XCenter,
    double YCenter,
    double Width,
    double Height);

/// <summary>
/// SAM-Maske als RLE + vorberechneter Polygon-String. Bei Slice 1 ist
/// <see cref="PolygonJson"/> die Quelle fuer den YOLO-seg-Label.
/// </summary>
public sealed record MaskPreview(
    string SamMaskRle,
    string SamMaskEncoding,
    string PolygonJson,
    int MaskWidth,
    int MaskHeight,
    int MaskAreaPixels,
    double SamConfidence,
    TimeSpan SamLatency,
    IReadOnlyList<string>? Warnings);

/// <summary>
/// Ergebnis eines CommitAsync-Aufrufs. <see cref="IsSuccess"/> entspricht
/// <see cref="StorePersisted"/> — KB und YOLO sind separate Status-Felder
/// (Best-Effort, Store-First-Semantik).
/// </summary>
public sealed record CommitResult(
    bool IsSuccess,
    string SampleId,
    string? FramePath,
    string? LabelPath,
    bool StorePersisted,
    bool KbIndexed,
    bool YoloWritten,
    string? Error,
    IReadOnlyList<string>? Warnings);
