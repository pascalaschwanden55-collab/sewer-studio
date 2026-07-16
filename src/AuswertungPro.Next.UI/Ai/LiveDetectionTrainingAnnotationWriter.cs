using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai.Teacher;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai;

public interface ILiveDetectionTrainingAnnotationWriter
{
    Task<TeacherAnnotation> SaveAcceptedAsync(
        byte[] frameBytes,
        LiveFrameFinding finding,
        TimeSpan videoTimestamp,
        CancellationToken ct = default);

    Task<TeacherAnnotation> SaveCorrectedAsync(
        byte[] frameBytes,
        LiveFrameFinding sourceFinding,
        ProtocolEntry selectedEntry,
        TimeSpan videoTimestamp,
        CancellationToken ct = default);

    Task<TeacherAnnotation?> SaveManualMarkAsync(
        byte[] frameBytes,
        ProtocolEntry selectedEntry,
        OverlayGeometry overlay,
        string? clockPosition,
        double captureMeter,
        TimeSpan videoTimestamp,
        CancellationToken ct = default);
}

public sealed class LiveDetectionTrainingAnnotationWriter : ILiveDetectionTrainingAnnotationWriter
{
    private readonly LiveDetectionTrainingFrameExporter _frameExporter;
    private readonly LiveDetectionTrainingExportPlanner _exportPlanner;
    private readonly Func<string> _annotationIdFactory;
    private readonly Func<TeacherAnnotation, Task> _appendAsync;

    public LiveDetectionTrainingAnnotationWriter(
        LiveDetectionTrainingFrameExporter frameExporter,
        Func<string>? annotationIdFactory = null,
        Func<TeacherAnnotation, Task>? appendAsync = null,
        LiveDetectionTrainingExportPlanner? exportPlanner = null)
    {
        _frameExporter = frameExporter ?? throw new ArgumentNullException(nameof(frameExporter));
        _exportPlanner = exportPlanner
            ?? new LiveDetectionTrainingExportPlanner(InfraTeacher.VsaYoloClassMap.Current);
        _annotationIdFactory = annotationIdFactory ?? LiveDetectionTrainingExportPlanner.CreateAnnotationId;
        _appendAsync = appendAsync ?? (annotation => InfraTeacher.TeacherAnnotationStore.AppendAsync(annotation));
    }

    public static LiveDetectionTrainingAnnotationWriter CreateDefault(
        ITeacherAnnotationStore? annotationStore = null,
        IVsaYoloClassMapStore? yoloClasses = null)
    {
        var annotations = annotationStore ?? InfraTeacher.TeacherAnnotationStore.Current;
        var classMap = yoloClasses ?? InfraTeacher.VsaYoloClassMap.Current;
        return new LiveDetectionTrainingAnnotationWriter(
            new LiveDetectionTrainingFrameExporter(
                TrainingAnnotationExportServiceFactory.Create(annotations)),
            appendAsync: annotation => annotations.AppendAsync(annotation),
            exportPlanner: new LiveDetectionTrainingExportPlanner(classMap));
    }

    public async Task<TeacherAnnotation> SaveAcceptedAsync(
        byte[] frameBytes,
        LiveFrameFinding finding,
        TimeSpan videoTimestamp,
        CancellationToken ct = default)
    {
        var annotationId = _annotationIdFactory();
        var exportPlan = _exportPlanner.PlanAccepted(finding, annotationId);
        var exportResult = await _frameExporter.ExportAsync(
            frameBytes,
            exportPlan.BoundingBox,
            exportPlan.Code,
            exportPlan.ClassId,
            exportPlan.BaseName,
            annotationId,
            ct);

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateDetection(
            annotationId,
            finding,
            exportPlan.Code,
            exportPlan.BoundingBox,
            videoTimestamp,
            exportResult);
        await _appendAsync(annotation);
        return annotation;
    }

    public async Task<TeacherAnnotation> SaveCorrectedAsync(
        byte[] frameBytes,
        LiveFrameFinding sourceFinding,
        ProtocolEntry selectedEntry,
        TimeSpan videoTimestamp,
        CancellationToken ct = default)
    {
        var annotationId = _annotationIdFactory();
        var exportPlan = _exportPlanner.PlanCorrected(sourceFinding, selectedEntry.Code, annotationId);
        var exportResult = await _frameExporter.ExportAsync(
            frameBytes,
            exportPlan.BoundingBox,
            exportPlan.Code,
            exportPlan.ClassId,
            exportPlan.BaseName,
            annotationId,
            ct);

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateCorrectedDetection(
            annotationId,
            sourceFinding,
            selectedEntry,
            exportPlan.BoundingBox,
            videoTimestamp,
            exportResult);
        await _appendAsync(annotation);
        return annotation;
    }

    public async Task<TeacherAnnotation?> SaveManualMarkAsync(
        byte[] frameBytes,
        ProtocolEntry selectedEntry,
        OverlayGeometry overlay,
        string? clockPosition,
        double captureMeter,
        TimeSpan videoTimestamp,
        CancellationToken ct = default)
    {
        var boundingBox = LiveDetectionGeometryMapper.BBoxFromOverlay(overlay);
        if (boundingBox.Width < 0.01 || boundingBox.Height < 0.01)
            return null;

        var annotationId = _annotationIdFactory();
        var exportResult = await _frameExporter.ExportAsync(
            frameBytes,
            boundingBox,
            selectedEntry.Code,
            _exportPlanner.GetClassId(selectedEntry.Code),
            $"mark_{annotationId}",
            annotationId,
            ct);

        var annotation = LiveDetectionTeacherAnnotationFactory.CreateManualMark(
            annotationId,
            selectedEntry,
            overlay,
            boundingBox,
            clockPosition,
            captureMeter,
            videoTimestamp,
            exportResult);
        await _appendAsync(annotation);
        return annotation;
    }
}
