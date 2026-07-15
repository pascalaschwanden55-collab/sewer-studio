using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeExitControllerTests
{
    [Fact]
    public void Exit_runs_finalization_before_teardown()
    {
        var calls = new List<string>();
        var controller = new CodingModeExitController(
            new CodingModeExitControllerBindings(
                IsCodingMode: () => true,
                SetCodingMode: enabled => calls.Add($"mode:{enabled}"),
                CreateFinalizationRequest: () =>
                {
                    calls.Add("finalize-request");
                    return new CodingModeExitFinalizationWorkflowRequest(
                        Events: null,
                        LastOsdMeter: null,
                        EndMeter: 0,
                        EndTime: TimeSpan.Zero,
                        AnalyzedFrameBytes: null);
                },
                FinalizationActions: FinalizationActions(),
                CreateTeardownRequest: () =>
                {
                    calls.Add("teardown-request");
                    return new CodingModeExitTeardownWorkflowRequest(
                        HasCodingLiveAiTimers: false,
                        HasCodingViewModel: false,
                        IsLiveDetectionRunning: false);
                },
                TeardownActions: TeardownActions(() => calls.Add("teardown"))));

        controller.Exit();

        Assert.Equal(
            ["mode:False", "finalize-request", "teardown-request", "teardown"],
            calls);
    }

    [Fact]
    public void Exit_restores_coding_mode_when_finalization_is_blocked()
    {
        var calls = new List<string>();
        var controller = new CodingModeExitController(
            new CodingModeExitControllerBindings(
                IsCodingMode: () => true,
                SetCodingMode: enabled => calls.Add($"mode:{enabled}"),
                CreateFinalizationRequest: () => new CodingModeExitFinalizationWorkflowRequest(
                    Events: [new CodingEvent()],
                    LastOsdMeter: 4.2,
                    EndMeter: 8.0,
                    EndTime: TimeSpan.FromSeconds(30),
                    AnalyzedFrameBytes: null),
                FinalizationActions: new CodingModeExitFinalizationWorkflowActions(
                    CloseTrackedStreckenschaeden: meter => calls.Add($"tracked:{meter}"),
                    CloseOpenStreckenschaeden: meter =>
                    {
                        calls.Add($"open:{meter}");
                        return false;
                    },
                    EnsureRohrendeExists: (_, _, _) => throw new InvalidOperationException("Rohrende darf bei blockiertem Abschluss nicht erzeugt werden.")),
                CreateTeardownRequest: () => throw new InvalidOperationException("Aufräumen darf bei blockiertem Abschluss nicht starten."),
                TeardownActions: TeardownActions()));

        controller.Exit();

        Assert.Equal(
            ["mode:False", "tracked:4.2", "open:4.2", "mode:True"],
            calls);
    }

    private static CodingModeExitFinalizationWorkflowActions FinalizationActions()
        => new(
            CloseTrackedStreckenschaeden: _ => { },
            CloseOpenStreckenschaeden: _ => true,
            EnsureRohrendeExists: (_, _, _) => { });

    private static CodingModeExitTeardownWorkflowActions TeardownActions(
        Action? stopCodingOsdTimer = null)
        => new(
            StopCodingOsdTimer: stopCodingOsdTimer ?? (() => { }),
            DisposeCodingOsdMeterService: () => { },
            StopCodingLiveAiTimers: _ => { },
            StopCodingAiPulse: () => { },
            StopPipelineHealthMonitor: () => { },
            DisposeAnalysisCancellation: () => { },
            ClearImportReferenceEvents: () => { },
            ResetProtocolMatchState: () => { },
            UpdateProtocolMatchSummary: () => { },
            ClearImportEventsListSource: () => { },
            HideConfirmationPanels: () => { },
            ClearPendingConfirmation: () => { },
            ClearDetectionConfirmationBuffer: () => { },
            ClearDetectionOverlay: _ => { },
            HideCodingSurface: () => { },
            HideInlineDefectDetail: () => { },
            HideOsdBadge: () => { },
            ShowLiveDetectionEntry: _ => { },
            ClearActiveCodingToolName: () => { },
            ResetCodingIndicators: () => { },
            CancelCodingSchema: () => { },
            ClearCodingSchemaType: () => { },
            DetachCodingViewModelPropertyChanged: () => { },
            ClearCodingSessionReferences: () => { },
            ClearCodingCalibrationState: () => { },
            ResetFrameReadiness: () => { },
            ResetCodingOverlaySuspendState: () => { });
}
