using System;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingPipelineHealthChangeWorkflowOutcome
{
    Ignored,
    Dispatched,
    Applied
}

public sealed record CodingPipelineHealthChangeWorkflowRequest(
    bool IsClosing,
    bool DispatcherHasShutdownStarted,
    bool HasDispatcherAccess);

public sealed record CodingPipelineHealthChangeWorkflowActions(
    Func<bool> ShouldApply,
    Action<Action> DispatchToUi,
    Action ApplyPipelineHealth);

public sealed record CodingPipelineHealthChangeWorkflowResult(
    CodingPipelineHealthChangeWorkflowOutcome Outcome);

public static class CodingPipelineHealthChangeWorkflow
{
    public static CodingPipelineHealthChangeWorkflowResult Execute(
        CodingPipelineHealthChangeWorkflowRequest request,
        CodingPipelineHealthChangeWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsClosing || request.DispatcherHasShutdownStarted)
            return Result(CodingPipelineHealthChangeWorkflowOutcome.Ignored);

        if (!request.HasDispatcherAccess)
        {
            actions.DispatchToUi(() =>
            {
                if (actions.ShouldApply())
                    actions.ApplyPipelineHealth();
            });
            return Result(CodingPipelineHealthChangeWorkflowOutcome.Dispatched);
        }

        if (!actions.ShouldApply())
            return Result(CodingPipelineHealthChangeWorkflowOutcome.Ignored);

        actions.ApplyPipelineHealth();
        return Result(CodingPipelineHealthChangeWorkflowOutcome.Applied);
    }

    private static CodingPipelineHealthChangeWorkflowResult Result(
        CodingPipelineHealthChangeWorkflowOutcome outcome)
        => new(outcome);
}
