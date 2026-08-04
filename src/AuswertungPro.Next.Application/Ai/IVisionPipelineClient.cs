using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

/// <summary>
/// Abstraktion des HTTP-Clients zum Python-FastAPI-Vision-Sidecar.
/// Ermoeglicht Testbarkeit von MultiModelAnalysisService ohne echten Sidecar.
/// </summary>
public interface IVisionPipelineClient
{
    Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default);
    Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default);
    Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default);
    Task<BccTestYoloResponse> DetectBccTestYoloAsync(
        YoloRequest request,
        CancellationToken ct = default)
        => Task.FromException<BccTestYoloResponse>(
            new NotSupportedException("Dieser Vision-Client unterstützt den getrennten BCC-Modelltest nicht."));
    Task<BccTestYoloResponse> DetectBccTestYoloAsync(
        BccTestYoloRequest request,
        CancellationToken ct = default)
        => Task.FromException<BccTestYoloResponse>(
            new NotSupportedException(
                "Dieser Vision-Client unterstuetzt die exakte Anheftung eines BCC-Kandidaten nicht."));
    Task<BccTestCandidatesResponse> GetBccTestCandidatesAsync(
        CancellationToken ct = default)
        => Task.FromResult(new BccTestCandidatesResponse(
            Available: false,
            Error: "Dieser Vision-Client unterstuetzt keine BCC-Kandidatenliste.",
            Candidates: []));
    Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default);
    Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default);
    Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default);
    Task<TrainingExportPlanResponseDto> ExportPlannedTrainingAsync(
        TrainingExportPlanRequestDto request,
        CancellationToken ct = default)
        => Task.FromException<TrainingExportPlanResponseDto>(
            new NotSupportedException("Dieser Vision-Client unterstuetzt den plan-gesteuerten Export v2 nicht."));
}
