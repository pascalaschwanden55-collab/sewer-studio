namespace AuswertungPro.Next.UI.Ai;

public interface IVideoAnalysisPipelineService
{
    Task<PipelineResult> RunAsync(PipelineRequest request, IProgress<PipelineProgress>? progress = null, CancellationToken ct = default);
}
