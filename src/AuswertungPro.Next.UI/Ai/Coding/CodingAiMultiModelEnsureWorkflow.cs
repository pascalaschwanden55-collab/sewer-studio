using System;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingAiMultiModelEnsureActions(
    Func<IVisionPipelineClient, PipelineConfig?, SingleFrameMultiModelService> CreateMultiModelService);

public static class CodingAiMultiModelEnsureWorkflow
{
    public static SingleFrameMultiModelService? Ensure(CodingAiController controller)
        => Ensure(
            controller,
            new CodingAiMultiModelEnsureActions(CodingAiRuntimeFactory.CreateMultiModelService));

    public static SingleFrameMultiModelService? Ensure(
        CodingAiController controller,
        CodingAiMultiModelEnsureActions actions)
    {
        ArgumentNullException.ThrowIfNull(controller);
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CreateMultiModelService);

        return controller.EnsureMultiModel(actions.CreateMultiModelService);
    }
}
