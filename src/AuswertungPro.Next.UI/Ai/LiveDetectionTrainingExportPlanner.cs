using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Teacher;
using AuswertungPro.Next.Domain.Models;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionTrainingExportPlan(
    string Code,
    int ClassId,
    string BaseName,
    NormalizedBoundingBox BoundingBox);

public sealed class LiveDetectionTrainingExportPlanner
{
    private readonly IVsaYoloClassMapStore _classMap;

    public LiveDetectionTrainingExportPlanner(IVsaYoloClassMapStore classMap)
    {
        _classMap = classMap ?? throw new ArgumentNullException(nameof(classMap));
    }

    public LiveDetectionTrainingExportPlan PlanAccepted(
        LiveFrameFinding finding,
        string annotationId)
    {
        var code = finding.VsaCodeHint ?? finding.Label;
        return Build(finding, code, $"det_{annotationId}");
    }

    public LiveDetectionTrainingExportPlan PlanCorrected(
        LiveFrameFinding sourceFinding,
        string selectedCode,
        string annotationId)
        => Build(sourceFinding, selectedCode, $"det_corr_{annotationId}");

    public int GetClassId(string code)
        => _classMap.GetClassId(code);

    public int GetOrAddClassId(string code)
        => _classMap.GetOrAddClassId(code);

    [Obsolete("Klassenkarte direkt ueber den Konstruktor uebergeben.")]
    public static LiveDetectionTrainingExportPlan BuildAccepted(
        LiveFrameFinding finding,
        string annotationId)
        => new LiveDetectionTrainingExportPlanner(InfraTeacher.VsaYoloClassMap.Current)
            .PlanAccepted(finding, annotationId);

    [Obsolete("Klassenkarte direkt ueber den Konstruktor uebergeben.")]
    public static LiveDetectionTrainingExportPlan BuildCorrected(
        LiveFrameFinding sourceFinding,
        string selectedCode,
        string annotationId)
        => new LiveDetectionTrainingExportPlanner(InfraTeacher.VsaYoloClassMap.Current)
            .PlanCorrected(sourceFinding, selectedCode, annotationId);

    public static string CreateAnnotationId()
        => Guid.NewGuid().ToString("N")[..12];

    private LiveDetectionTrainingExportPlan Build(
        LiveFrameFinding finding,
        string code,
        string baseName)
        => new(
            code,
            _classMap.GetOrAddClassId(code),
            baseName,
            LiveDetectionGeometryMapper.BBoxFromClockPosition(finding));
}
