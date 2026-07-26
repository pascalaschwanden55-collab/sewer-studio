using System.Windows.Media;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Ai.Coding;

public enum CodingLiveAiToggleWorkflowOutcome
{
    Started,
    Stopped
}

public sealed record CodingLiveAiToggleWorkflowRequest(
    bool IsChecked,
    string? ModelName);

public sealed record CodingLiveAiToggleWorkflowActions(
    Action StartTimers,
    Action<bool> StopTimers,
    Action<string, Color, string?> SetCodingAiState);

public static class CodingLiveAiToggleWorkflow
{
    public static CodingLiveAiToggleWorkflowOutcome Execute(
        CodingLiveAiToggleWorkflowRequest request,
        CodingLiveAiToggleWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        var compactModelName = LiveDetectionDisplayPolicy.CompactModelName(request.ModelName);
        if (request.IsChecked)
        {
            actions.StartTimers();
            var status = CodingLiveAiButtonDisplayPolicy.BuildStatus(
                isActive: true,
                compactModelName);
            actions.SetCodingAiState(status.StatusText, PlayerStatusColors.Success, status.DetailText);
            return CodingLiveAiToggleWorkflowOutcome.Started;
        }

        actions.StopTimers(true);
        var stoppedStatus = CodingLiveAiButtonDisplayPolicy.BuildStatus(
            isActive: false,
            compactModelName);
        actions.SetCodingAiState(stoppedStatus.StatusText, PlayerStatusColors.Success, stoppedStatus.DetailText);
        return CodingLiveAiToggleWorkflowOutcome.Stopped;
    }
}
