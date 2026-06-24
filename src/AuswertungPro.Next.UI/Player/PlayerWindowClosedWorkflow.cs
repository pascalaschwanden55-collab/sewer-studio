namespace AuswertungPro.Next.UI.Player;

public sealed record PlayerWindowClosedWorkflowRequest(
    bool IsLastOpenedWindow,
    bool HasMainWindow,
    bool IsMainWindowCurrentWindow,
    bool IsMainWindowMinimized);

public sealed record PlayerWindowClosedWorkflowActions(
    Action ClearLastOpened,
    Action ExitCodingMode,
    Action StopCodingOsdTimer,
    Action DisposeCodingOsdMeterService,
    Action DisposeCodingAnalysisCancellation,
    Action StopCodingAiPulse,
    Action CancelQuickScan,
    Action StopLiveDetection,
    Action StopPipelineHealthMonitor,
    Action Cleanup,
    Action RestoreMainWindow,
    Action ActivateMainWindow);

public static class PlayerWindowClosedWorkflow
{
    public static void Execute(
        PlayerWindowClosedWorkflowRequest request,
        PlayerWindowClosedWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsLastOpenedWindow)
            actions.ClearLastOpened();

        actions.ExitCodingMode();
        actions.StopCodingOsdTimer();
        actions.DisposeCodingOsdMeterService();
        actions.DisposeCodingAnalysisCancellation();
        actions.StopCodingAiPulse();
        actions.CancelQuickScan();
        actions.StopLiveDetection();
        actions.StopPipelineHealthMonitor();
        actions.Cleanup();

        if (!request.HasMainWindow || request.IsMainWindowCurrentWindow)
            return;

        if (request.IsMainWindowMinimized)
            actions.RestoreMainWindow();

        actions.ActivateMainWindow();
    }
}
