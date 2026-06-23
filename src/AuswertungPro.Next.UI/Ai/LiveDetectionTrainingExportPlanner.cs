using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using InfraTeacher = AuswertungPro.Next.Infrastructure.Ai.Teacher;

namespace AuswertungPro.Next.UI.Ai;

public sealed record LiveDetectionTrainingExportPlan(
    string Code,
    int ClassId,
    string BaseName,
    NormalizedBoundingBox BoundingBox);

public static class LiveDetectionTrainingExportPlanner
{
    public static LiveDetectionTrainingExportPlan BuildAccepted(
        LiveFrameFinding finding,
        string annotationId)
    {
        var code = finding.VsaCodeHint ?? finding.Label;
        return Build(finding, code, $"det_{annotationId}");
    }

    public static LiveDetectionTrainingExportPlan BuildCorrected(
        LiveFrameFinding sourceFinding,
        string selectedCode,
        string annotationId)
        => Build(sourceFinding, selectedCode, $"det_corr_{annotationId}");

    public static string CreateAnnotationId()
        => Guid.NewGuid().ToString("N")[..12];

    private static LiveDetectionTrainingExportPlan Build(
        LiveFrameFinding finding,
        string code,
        string baseName)
        => new(
            code,
            InfraTeacher.VsaYoloClassMap.GetClassId(code),
            baseName,
            LiveDetectionGeometryMapper.BBoxFromClockPosition(finding));
}
