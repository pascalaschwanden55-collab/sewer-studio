using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class PlayerWindowClosedWorkflowTests
{
    [Fact]
    public void Execute_clears_window_state_stops_background_work_and_cleans_up_in_order()
    {
        var calls = new List<string>();

        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: true,
                HasMainWindow: false,
                IsMainWindowCurrentWindow: false,
                IsMainWindowMinimized: false),
            Actions(
                clearLastOpened: () => calls.Add("last"),
                exitCodingMode: () => calls.Add("coding"),
                stopCodingOsdTimer: () => calls.Add("osd-timer"),
                disposeCodingOsdMeterService: () => calls.Add("osd-service"),
                disposeCodingAnalysisCancellation: () => calls.Add("coding-cancel"),
                stopCodingAiPulse: () => calls.Add("pulse"),
                cancelQuickScan: () => calls.Add("quick"),
                stopLiveDetection: () => calls.Add("live"),
                stopPipelineHealthMonitor: () => calls.Add("health"),
                cleanup: () => calls.Add("cleanup")));

        Assert.Equal(
            [
                "last",
                "coding",
                "osd-timer",
                "osd-service",
                "coding-cancel",
                "pulse",
                "quick",
                "live",
                "health",
                "cleanup"
            ],
            calls);
    }

    [Fact]
    public void Execute_restores_and_activates_other_minimized_main_window_after_cleanup()
    {
        var calls = new List<string>();

        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: false,
                HasMainWindow: true,
                IsMainWindowCurrentWindow: false,
                IsMainWindowMinimized: true),
            Actions(
                cleanup: () => calls.Add("cleanup"),
                restoreMainWindow: () => calls.Add("restore"),
                activateMainWindow: () => calls.Add("activate")));

        Assert.Equal(["cleanup", "restore", "activate"], calls);
    }

    [Fact]
    public void Execute_does_not_activate_missing_or_current_main_window()
    {
        var missingMainCalls = new List<string>();
        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: false,
                HasMainWindow: false,
                IsMainWindowCurrentWindow: false,
                IsMainWindowMinimized: true),
            Actions(
                cleanup: () => missingMainCalls.Add("cleanup"),
                restoreMainWindow: () => missingMainCalls.Add("restore"),
                activateMainWindow: () => missingMainCalls.Add("activate")));

        var currentMainCalls = new List<string>();
        PlayerWindowClosedWorkflow.Execute(
            new PlayerWindowClosedWorkflowRequest(
                IsLastOpenedWindow: false,
                HasMainWindow: true,
                IsMainWindowCurrentWindow: true,
                IsMainWindowMinimized: true),
            Actions(
                cleanup: () => currentMainCalls.Add("cleanup"),
                restoreMainWindow: () => currentMainCalls.Add("restore"),
                activateMainWindow: () => currentMainCalls.Add("activate")));

        Assert.Equal(["cleanup"], missingMainCalls);
        Assert.Equal(["cleanup"], currentMainCalls);
    }

    private static PlayerWindowClosedWorkflowActions Actions(
        Action? clearLastOpened = null,
        Action? exitCodingMode = null,
        Action? stopCodingOsdTimer = null,
        Action? disposeCodingOsdMeterService = null,
        Action? disposeCodingAnalysisCancellation = null,
        Action? stopCodingAiPulse = null,
        Action? cancelQuickScan = null,
        Action? stopLiveDetection = null,
        Action? stopPipelineHealthMonitor = null,
        Action? cleanup = null,
        Action? restoreMainWindow = null,
        Action? activateMainWindow = null)
        => new(
            ClearLastOpened: clearLastOpened ?? (() => { }),
            ExitCodingMode: exitCodingMode ?? (() => { }),
            StopCodingOsdTimer: stopCodingOsdTimer ?? (() => { }),
            DisposeCodingOsdMeterService: disposeCodingOsdMeterService ?? (() => { }),
            DisposeCodingAnalysisCancellation: disposeCodingAnalysisCancellation ?? (() => { }),
            StopCodingAiPulse: stopCodingAiPulse ?? (() => { }),
            CancelQuickScan: cancelQuickScan ?? (() => { }),
            StopLiveDetection: stopLiveDetection ?? (() => { }),
            StopPipelineHealthMonitor: stopPipelineHealthMonitor ?? (() => { }),
            Cleanup: cleanup ?? (() => { }),
            RestoreMainWindow: restoreMainWindow ?? (() => { }),
            ActivateMainWindow: activateMainWindow ?? (() => { }));
}
