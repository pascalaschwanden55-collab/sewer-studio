using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Infrastructure.Ai.Pipeline;

/// <summary>
/// Abstraktion des HTTP-Clients zum Python-FastAPI-Vision-Sidecar.
/// Ermoeglicht Testbarkeit von MultiModelAnalysisService ohne echten Sidecar.
/// </summary>
public interface IVisionPipelineClient
{
    Task<SidecarHealthResponse?> HealthCheckAsync(CancellationToken ct = default);
    Task<PipelineHealthCheckResult> CheckHealthDetailedAsync(CancellationToken ct = default);
    Task<YoloResponse> DetectYoloAsync(YoloRequest request, CancellationToken ct = default);
    Task<DinoResponse> DetectDinoAsync(DinoRequest request, CancellationToken ct = default);
    Task<SamResponse> SegmentSamAsync(SamRequest request, CancellationToken ct = default);
    Task<YoloClassifyResponse> ClassifyYoloAsync(YoloClassifyRequest request, CancellationToken ct = default);
    Task<TrainingExportResponseDto> ExportTrainingAsync(TrainingExportRequestDto request, CancellationToken ct = default);
}
