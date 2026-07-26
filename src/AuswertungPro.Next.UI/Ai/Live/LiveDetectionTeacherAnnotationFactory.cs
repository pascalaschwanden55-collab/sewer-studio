using System.Globalization;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.UI.Ai.Live;

public static class LiveDetectionTeacherAnnotationFactory
{
    public static TeacherAnnotation CreateManualMark(
        string annotationId,
        ProtocolEntry selectedEntry,
        OverlayGeometry overlay,
        NormalizedBoundingBox boundingBox,
        string? clockPosition,
        double captureMeter,
        TimeSpan videoTimestamp,
        TrainingAnnotationResult exportResult)
        => new()
        {
            AnnotationId = annotationId,
            VsaCode = selectedEntry.Code,
            Beschreibung = selectedEntry.Beschreibung,
            MeterPosition = captureMeter,
            VideoTimestamp = videoTimestamp,
            ToolType = overlay.ToolType,
            Points = overlay.Points
                .Select(p => new NormalizedPoint(p.X, p.Y))
                .ToList(),
            BoundingBox = boundingBox,
            ClockPosition = ParseClock(clockPosition),
            FullFramePath = exportResult.FullFramePath,
            CroppedRegionPath = exportResult.CroppedRegionPath,
            YoloAnnotationPath = exportResult.YoloAnnotationPath,
            WidthMm = overlay.Q2Mm,
            HeightMm = overlay.Q1Mm
        };

    public static TeacherAnnotation CreateDetection(
        string annotationId,
        LiveFrameFinding finding,
        string code,
        NormalizedBoundingBox boundingBox,
        TimeSpan videoTimestamp,
        TrainingAnnotationResult exportResult)
        => new()
        {
            AnnotationId = annotationId,
            VsaCode = code,
            Beschreibung = finding.Label,
            MeterPosition = 0,
            VideoTimestamp = videoTimestamp,
            ToolType = OverlayToolType.None,
            Points = [],
            BoundingBox = boundingBox,
            ClockPosition = ParseClock(finding.PositionClock),
            FullFramePath = exportResult.FullFramePath,
            CroppedRegionPath = exportResult.CroppedRegionPath,
            YoloAnnotationPath = exportResult.YoloAnnotationPath,
            WidthMm = finding.WidthMm,
            HeightMm = finding.HeightMm
        };

    public static TeacherAnnotation CreateCorrectedDetection(
        string annotationId,
        LiveFrameFinding sourceFinding,
        ProtocolEntry selectedEntry,
        NormalizedBoundingBox boundingBox,
        TimeSpan videoTimestamp,
        TrainingAnnotationResult exportResult)
        => new()
        {
            AnnotationId = annotationId,
            VsaCode = selectedEntry.Code,
            Beschreibung = selectedEntry.Beschreibung,
            MeterPosition = 0,
            VideoTimestamp = videoTimestamp,
            ToolType = OverlayToolType.None,
            Points = [],
            BoundingBox = boundingBox,
            ClockPosition = ParseClock(sourceFinding.PositionClock),
            FullFramePath = exportResult.FullFramePath,
            CroppedRegionPath = exportResult.CroppedRegionPath,
            YoloAnnotationPath = exportResult.YoloAnnotationPath,
            WidthMm = sourceFinding.WidthMm,
            HeightMm = sourceFinding.HeightMm
        };

    public static TeacherAnnotation CreateImportConfirmation(
        string annotationId,
        CodingEvent importEvent,
        string fullFramePath)
        => new()
        {
            AnnotationId = annotationId,
            VsaCode = importEvent.Entry.Code,
            Beschreibung = importEvent.Entry.Beschreibung,
            MeterPosition = importEvent.MeterAtCapture,
            VideoTimestamp = importEvent.VideoTimestamp,
            ToolType = OverlayToolType.None,
            FullFramePath = fullFramePath
        };

    private static double? ParseClock(string? raw)
    {
        if (double.TryParse(raw, NumberStyles.Any, CultureInfo.InvariantCulture, out var clock))
            return clock;
        return null;
    }
}
