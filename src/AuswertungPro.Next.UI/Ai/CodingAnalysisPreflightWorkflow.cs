using System.Windows.Media;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai;

public enum CodingAnalysisPreflightWorkflowOutcome
{
    ContinueSingleModel,
    RunMultiModel,
    StopAtTerminalBoundary
}

public sealed record CodingAnalysisPreflightWorkflowRequest(
    bool DisableAnalyzeButton,
    bool UseMultiModel,
    bool HasMultiModel);

public sealed record CodingAnalysisFramePosition(
    double CaptureTimestampSeconds,
    double CurrentMeter,
    TimeSpan VideoTime);

public sealed record CodingAnalysisPreflightWorkflowActions(
    Action<bool> SetAnalyzeButtonEnabled,
    Func<CodingAnalysisFramePosition> ResolveFramePosition,
    Func<CodingAnalysisFramePosition, bool> IsAfterTerminalBoundary,
    Action ClearDetectionOverlays,
    Action ClearSamMasks,
    Action<string, Color, string?> SetCodingAiState);

public sealed record CodingAnalysisPreflightWorkflowResult(
    CodingAnalysisPreflightWorkflowOutcome Outcome,
    double CaptureTimestampSeconds);

public static class CodingAnalysisPreflightWorkflow
{
    public static CodingAnalysisPreflightWorkflowResult Execute(
        CodingAnalysisPreflightWorkflowRequest request,
        CodingAnalysisPreflightWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.DisableAnalyzeButton)
            actions.SetAnalyzeButtonEnabled(false);

        var framePosition = actions.ResolveFramePosition();
        if (actions.IsAfterTerminalBoundary(framePosition))
        {
            actions.ClearDetectionOverlays();
            actions.ClearSamMasks();
            actions.SetCodingAiState(
                "Rohrende erreicht - KI-Analyse gestoppt",
                PlayerStatusColors.Success,
                "Codierung abgeschlossen");
            return new CodingAnalysisPreflightWorkflowResult(
                CodingAnalysisPreflightWorkflowOutcome.StopAtTerminalBoundary,
                framePosition.CaptureTimestampSeconds);
        }

        var outcome = request.UseMultiModel && request.HasMultiModel
            ? CodingAnalysisPreflightWorkflowOutcome.RunMultiModel
            : CodingAnalysisPreflightWorkflowOutcome.ContinueSingleModel;
        return new CodingAnalysisPreflightWorkflowResult(outcome, framePosition.CaptureTimestampSeconds);
    }
}
