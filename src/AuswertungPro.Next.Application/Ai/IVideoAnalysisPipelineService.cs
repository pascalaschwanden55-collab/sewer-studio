using System;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai.Vision;

namespace AuswertungPro.Next.Application.Ai;

public interface IVideoAnalysisPipelineService
{
    Task<PipelineResult> RunAsync(PipelineRequest request, IProgress<PipelineProgress>? progress = null, CancellationToken ct = default);
}
