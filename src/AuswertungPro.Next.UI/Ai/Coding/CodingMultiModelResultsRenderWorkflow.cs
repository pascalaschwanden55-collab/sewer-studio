using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Ai.Pipeline;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingMultiModelResultsRenderWorkflowOutcome
{
    NoSamResponse,
    NoVisibleMasks,
    RenderedMasks
}

public sealed record CodingMultiModelResultsRenderRequest(
    SingleFrameResult Result,
    IReadOnlyList<SegmentedFinding> Segmented);

public sealed record CodingMultiModelResultsRenderActions(
    Action ClearMasks,
    Action<double> SetVideoAspect,
    Func<IReadOnlyList<SegmentedFinding>, IReadOnlyList<SamMaskRenderer.MaskRenderCandidate>> BuildVisibleMaskRenderCandidates,
    Action<IReadOnlyList<SamMaskRenderer.MaskRenderCandidate>, SamResponse> RenderCandidates,
    Action ShowReferenceDn);

public sealed record CodingMultiModelResultsRenderWorkflowResult(
    CodingMultiModelResultsRenderWorkflowOutcome Outcome,
    int VisibleMaskCount)
{
    public bool RenderedMasks => Outcome == CodingMultiModelResultsRenderWorkflowOutcome.RenderedMasks;
}

public static class CodingMultiModelResultsRenderWorkflow
{
    public static CodingMultiModelResultsRenderWorkflowResult Execute(
        CodingMultiModelResultsRenderRequest request,
        CodingMultiModelResultsRenderActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        actions.ClearMasks();

        var samResponse = request.Result.SamResponse;
        if (samResponse == null)
        {
            actions.ShowReferenceDn();
            return Result(CodingMultiModelResultsRenderWorkflowOutcome.NoSamResponse, 0);
        }

        if (samResponse.ImageWidth > 0 && samResponse.ImageHeight > 0)
            actions.SetVideoAspect((double)samResponse.ImageWidth / samResponse.ImageHeight);

        var candidates = actions.BuildVisibleMaskRenderCandidates(request.Segmented);
        if (candidates.Count > 0)
        {
            actions.RenderCandidates(candidates, samResponse);
            actions.ShowReferenceDn();
            return Result(CodingMultiModelResultsRenderWorkflowOutcome.RenderedMasks, candidates.Count);
        }

        actions.ShowReferenceDn();
        return Result(CodingMultiModelResultsRenderWorkflowOutcome.NoVisibleMasks, 0);
    }

    private static CodingMultiModelResultsRenderWorkflowResult Result(
        CodingMultiModelResultsRenderWorkflowOutcome outcome,
        int visibleMaskCount)
        => new(outcome, visibleMaskCount);
}
