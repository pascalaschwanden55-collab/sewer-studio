using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeExitTeardownWorkflowTests
{
    [Fact]
    public void Execute_runs_teardown_in_order_and_preserves_live_detection_state()
    {
        var calls = new List<string>();

        CodingModeExitTeardownWorkflow.Execute(
            new CodingModeExitTeardownWorkflowRequest(
                HasCodingLiveAiTimers: true,
                HasCodingViewModel: true,
                IsLiveDetectionRunning: true),
            Actions(calls));

        Assert.Equal(
            [
                "stop-osd",
                "dispose-osd",
                "stop-live-ai:True",
                "stop-pulse",
                "stop-health",
                "dispose-analysis",
                "clear-import-events",
                "reset-match",
                "update-match",
                "clear-import-list",
                "hide-confirmation",
                "clear-pending",
                "clear-buffer",
                "clear-overlay:False",
                "hide-surface",
                "hide-detail",
                "hide-osd-badge",
                "show-live:True",
                "clear-active-tool",
                "reset-indicators",
                "cancel-schema",
                "clear-schema-type",
                "detach-vm",
                "clear-session",
                "clear-calibration",
                "reset-frame",
                "reset-overlay-suspend"
            ],
            calls);
    }

    [Fact]
    public void Execute_skips_optional_timer_and_viewmodel_detach_when_absent()
    {
        var calls = new List<string>();

        CodingModeExitTeardownWorkflow.Execute(
            new CodingModeExitTeardownWorkflowRequest(
                HasCodingLiveAiTimers: false,
                HasCodingViewModel: false,
                IsLiveDetectionRunning: false),
            Actions(calls));

        Assert.Equal(
            [
                "stop-osd",
                "dispose-osd",
                "stop-pulse",
                "stop-health",
                "dispose-analysis",
                "clear-import-events",
                "reset-match",
                "update-match",
                "clear-import-list",
                "hide-confirmation",
                "clear-pending",
                "clear-buffer",
                "clear-overlay:True",
                "hide-surface",
                "hide-detail",
                "hide-osd-badge",
                "show-live:False",
                "clear-active-tool",
                "reset-indicators",
                "cancel-schema",
                "clear-schema-type",
                "clear-session",
                "clear-calibration",
                "reset-frame",
                "reset-overlay-suspend"
            ],
            calls);
    }

    private static CodingModeExitTeardownWorkflowActions Actions(List<string> calls)
        => new(
            StopCodingOsdTimer: () => calls.Add("stop-osd"),
            DisposeCodingOsdMeterService: () => calls.Add("dispose-osd"),
            StopCodingLiveAiTimers: resetButton => calls.Add($"stop-live-ai:{resetButton}"),
            StopCodingAiPulse: () => calls.Add("stop-pulse"),
            StopPipelineHealthMonitor: () => calls.Add("stop-health"),
            DisposeAnalysisCancellation: () => calls.Add("dispose-analysis"),
            ClearImportReferenceEvents: () => calls.Add("clear-import-events"),
            ResetProtocolMatchState: () => calls.Add("reset-match"),
            UpdateProtocolMatchSummary: () => calls.Add("update-match"),
            ClearImportEventsListSource: () => calls.Add("clear-import-list"),
            HideConfirmationPanels: () => calls.Add("hide-confirmation"),
            ClearPendingConfirmation: () => calls.Add("clear-pending"),
            ClearDetectionConfirmationBuffer: () => calls.Add("clear-buffer"),
            ClearDetectionOverlay: hideOverlay => calls.Add($"clear-overlay:{hideOverlay}"),
            HideCodingSurface: () => calls.Add("hide-surface"),
            HideInlineDefectDetail: () => calls.Add("hide-detail"),
            HideOsdBadge: () => calls.Add("hide-osd-badge"),
            ShowLiveDetectionEntry: isDetecting => calls.Add($"show-live:{isDetecting}"),
            ClearActiveCodingToolName: () => calls.Add("clear-active-tool"),
            ResetCodingIndicators: () => calls.Add("reset-indicators"),
            CancelCodingSchema: () => calls.Add("cancel-schema"),
            ClearCodingSchemaType: () => calls.Add("clear-schema-type"),
            DetachCodingViewModelPropertyChanged: () => calls.Add("detach-vm"),
            ClearCodingSessionReferences: () => calls.Add("clear-session"),
            ClearCodingCalibrationState: () => calls.Add("clear-calibration"),
            ResetFrameReadiness: () => calls.Add("reset-frame"),
            ResetCodingOverlaySuspendState: () => calls.Add("reset-overlay-suspend"));
}
