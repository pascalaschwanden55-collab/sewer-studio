using System.Windows.Media;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingBoundaryClassifierResultWorkflowOutcome
{
    NotHandled,
    PossibleEndAhead,
    BoundaryHandled
}

public sealed record CodingBoundaryClassifierResultWorkflowRequest(
    SingleFrameResult Result,
    double Meter,
    double EndMeter,
    TimeSpan VideoTime,
    int ExistingEventCount,
    byte[]? AnalyzedFrameBytes);

public sealed record CodingBoundaryClassifierResultWorkflowActions(
    Func<string, string?> LookupVsaLabel,
    Action<string> Trace,
    Action ClearDetectionOverlays,
    Action ClearMasks,
    Action<string, string> ShowPossibleBoundary,
    Action<string, string> ShowBoundary,
    Func<double, TimeSpan, byte[]?, Task<bool>> EnsureStartExistsAsync,
    Action<double> CloseTrackedStretchDamages,
    Action<double, TimeSpan, byte[]?> EnsureEndExists,
    Func<int> GetCurrentEventCount,
    Action<string, Color, string?> SetAiState);

public sealed record CodingBoundaryClassifierResultWorkflowResult(
    CodingBoundaryClassifierResultWorkflowOutcome Outcome)
{
    public bool Handled => Outcome != CodingBoundaryClassifierResultWorkflowOutcome.NotHandled;
}

public static class CodingBoundaryClassifierResultWorkflow
{
    public static bool CanHandle(SingleFrameResult result)
    {
        ArgumentNullException.ThrowIfNull(result);

        return CodingClassifierDisplayPolicy.IsBoundaryClassifierCode(result.ClassifierCode);
    }

    public static async Task<CodingBoundaryClassifierResultWorkflowResult> ExecuteAsync(
        CodingBoundaryClassifierResultWorkflowRequest request,
        CodingBoundaryClassifierResultWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(request.Result);
        ArgumentNullException.ThrowIfNull(actions);

        var code = request.Result.ClassifierCode;
        if (!CanHandle(request.Result))
            return NotHandled();

        var boundaryCode = code!;

        if (boundaryCode == "BCE"
            && !CodingDedupPolicy.IsBoundaryEndCodePlausible(boundaryCode, request.Meter, request.EndMeter))
        {
            var possibleLabel = CodingClassifierDisplayPolicy.ResolveBoundaryLabel(
                boundaryCode,
                actions.LookupVsaLabel(boundaryCode));

            actions.Trace(
                $"[Boundary] BCE bei {request.Meter:F2}m verworfen (Haltungsende ~{request.EndMeter:F2}m, noch zu weit) - weiteranalysieren");
            actions.SetAiState(
                CodingClassifierDisplayPolicy.PossibleBoundaryEndStatus,
                PlayerStatusColors.Warning,
                CodingClassifierDisplayPolicy.PossibleBoundaryEndDetail);
            actions.ClearDetectionOverlays();
            actions.ClearMasks();
            actions.ShowPossibleBoundary(boundaryCode, possibleLabel);

            return new CodingBoundaryClassifierResultWorkflowResult(
                CodingBoundaryClassifierResultWorkflowOutcome.PossibleEndAhead);
        }

        var anyAdded = false;

        if (boundaryCode == "BCD")
        {
            anyAdded = await actions.EnsureStartExistsAsync(
                request.Meter,
                request.VideoTime,
                request.AnalyzedFrameBytes);
        }
        else
        {
            actions.CloseTrackedStretchDamages(request.Meter);
            actions.EnsureEndExists(
                request.EndMeter,
                request.VideoTime,
                request.AnalyzedFrameBytes);
            actions.ClearDetectionOverlays();
            actions.ClearMasks();
        }

        var label = CodingClassifierDisplayPolicy.ResolveBoundaryLabel(
            boundaryCode,
            actions.LookupVsaLabel(boundaryCode));
        var added = anyAdded || actions.GetCurrentEventCount() > request.ExistingEventCount;
        var statusText = CodingClassifierDisplayPolicy.BuildDetectedStatusText(label, added);

        actions.SetAiState(
            statusText,
            PlayerStatusColors.Success,
            CodingClassifierDisplayPolicy.BuildClassifierDetail(request.Result.ClassifierConfidence));
        actions.ShowBoundary(boundaryCode, label);

        return new CodingBoundaryClassifierResultWorkflowResult(
            CodingBoundaryClassifierResultWorkflowOutcome.BoundaryHandled);
    }

    private static CodingBoundaryClassifierResultWorkflowResult NotHandled()
        => new(CodingBoundaryClassifierResultWorkflowOutcome.NotHandled);
}
