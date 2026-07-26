using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingAiHealthMonitorCreationRequest(
    CodingAiRuntime Runtime,
    Func<bool> AiEnabled,
    Func<bool> QwenAvailable);

public sealed record CodingAiHealthMonitorCreationActions(
    Func<IVisionPipelineClient, Func<bool>, Func<bool>, IPipelineHealthMonitor> CreateHealthMonitor);

public static class CodingAiHealthMonitorCreationWorkflow
{
    public static IPipelineHealthMonitor Create(
        CodingAiRuntime runtime,
        Func<bool> aiEnabled,
        Func<bool> qwenAvailable)
        => Create(
            new CodingAiHealthMonitorCreationRequest(runtime, aiEnabled, qwenAvailable),
            new CodingAiHealthMonitorCreationActions(
                CodingAiRuntimeFactory.CreateHealthMonitor));

    public static IPipelineHealthMonitor Create(
        CodingAiHealthMonitorCreationRequest request,
        CodingAiHealthMonitorCreationActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Runtime);
        ArgumentNullException.ThrowIfNull(request.Runtime.VisionClient);
        ArgumentNullException.ThrowIfNull(request.AiEnabled);
        ArgumentNullException.ThrowIfNull(request.QwenAvailable);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateHealthMonitor);

        return actions.CreateHealthMonitor(
            request.Runtime.VisionClient,
            request.AiEnabled,
            request.QwenAvailable);
    }
}
