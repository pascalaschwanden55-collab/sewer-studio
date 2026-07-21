using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

internal sealed record PlayerWindowCodingModeExitControls(
    ListBox ImportEventsList,
    FrameworkElement CodingConfirmationPanel,
    FrameworkElement DetectionConfirmationPanel,
    Canvas DetectionCanvas,
    FrameworkElement DetectionOverlay,
    Popup CodingOverlayPopup,
    Canvas CodingOverlayCanvas,
    FrameworkElement CodingSidePanel,
    ColumnDefinition CodingSidePanelColumn,
    FrameworkElement CodingToolbar,
    FrameworkElement CodingTimelinePanel,
    FrameworkElement CodingCalibrationHint,
    FrameworkElement CodingMeasurementPanel,
    FrameworkElement OsdMeterBadge,
    ToggleButton LiveDetectionButton,
    TextBlock LiveDetectionStatusText,
    TextBlock ActiveToolLabel,
    ToggleButton CodingLiveAiToggle,
    TextBlock CodingAiStageText);

internal sealed record PlayerWindowCodingModeExitActions(
    Func<double, bool> CloseOpenStreckenschaeden,
    Action HideInlineDefectDetail,
    Action ResetFrameReadiness);

internal sealed record PlayerWindowCodingModeExitControllerDependencies(
    CodingRuntimeStateControllerSet RuntimeStates,
    CodingSchemaStateControllerSet SchemaStates,
    CodingOverlayStateControllerSet OverlayStates,
    CodingAiStateControllerSet AiStates,
    CodingProtocolStateControllerSet ProtocolStates,
    CodingSessionRuntime SessionRuntime,
    CodingOsdMeterController OsdMeterController,
    PlayerTimelineHost TimelineHost,
    LiveDetectionController DetectionController,
    ICodingStreckenschadenTrackingController StreckenschadenTrackingController,
    CodingBoundaryContext BoundaryContext,
    ILiveDetectionPulseController LiveDetectionPulseController,
    ICodingPipelineHealthController PipelineHealthController,
    ICodingProtocolMatchController ProtocolMatchController,
    ICodingOverlayInputVisibilityController OverlayInputVisibilityController,
    PlayerWindowCodingModeExitControls Controls,
    PlayerWindowCodingModeExitActions Actions);

internal static class PlayerWindowCodingModeExitControllerFactory
{
    internal static ICodingModeExitController Create(
        PlayerWindowCodingModeExitControllerDependencies dependencies)
    {
        Validate(dependencies);

        return new CodingModeExitController(
            new CodingModeExitControllerBindings(
                IsCodingMode: () => dependencies.RuntimeStates.ModeState.IsCodingMode,
                SetCodingMode: dependencies.RuntimeStates.ModeState.Set,
                CreateFinalizationRequest: () => CreateFinalizationRequest(dependencies),
                FinalizationActions: CreateFinalizationActions(dependencies),
                CreateTeardownRequest: () => CreateTeardownRequest(dependencies),
                TeardownActions: CreateTeardownActions(dependencies)));
    }

    private static CodingModeExitFinalizationWorkflowRequest CreateFinalizationRequest(
        PlayerWindowCodingModeExitControllerDependencies dependencies)
        => new(
            dependencies.SessionRuntime.SessionHost.EventCollection,
            dependencies.OsdMeterController.LastMeter,
            dependencies.SessionRuntime.SessionHost.EndMeter,
            dependencies.TimelineHost.DurationTimeOrZero,
            dependencies.DetectionController.PendingConfirmationFrameBytes);

    private static CodingModeExitFinalizationWorkflowActions CreateFinalizationActions(
        PlayerWindowCodingModeExitControllerDependencies dependencies)
        => new(
            CloseTrackedStreckenschaeden:
                dependencies.StreckenschadenTrackingController.CloseTracked,
            CloseOpenStreckenschaeden: dependencies.Actions.CloseOpenStreckenschaeden,
            EnsureRohrendeExists: (meter, _, frameBytes) =>
                dependencies.BoundaryContext.EnsureEnd(meter, frameBytes));

    private static CodingModeExitTeardownWorkflowRequest CreateTeardownRequest(
        PlayerWindowCodingModeExitControllerDependencies dependencies)
        => new(
            HasCodingLiveAiTimers: dependencies.AiStates.LiveTimerOwner.HasController,
            HasCodingViewModel: dependencies.SessionRuntime.SessionHost.HasViewModel,
            IsLiveDetectionRunning: dependencies.DetectionController.IsDetecting);

    private static CodingModeExitTeardownWorkflowActions CreateTeardownActions(
        PlayerWindowCodingModeExitControllerDependencies dependencies)
    {
        var controls = dependencies.Controls;

        return new CodingModeExitTeardownWorkflowActions(
            StopCodingOsdTimer: dependencies.OsdMeterController.StopTimer,
            DisposeCodingOsdMeterService: dependencies.OsdMeterController.DisposeService,
            StopCodingLiveAiTimers: dependencies.AiStates.LiveTimerOwner.Stop,
            StopCodingAiPulse: dependencies.LiveDetectionPulseController.Stop,
            StopPipelineHealthMonitor: dependencies.PipelineHealthController.Stop,
            DisposeAnalysisCancellation:
                dependencies.AiStates.RuntimeOwner.Controller.DisposeAnalysisCancellation,
            ClearImportReferenceEvents: () => CodingImportReferenceStateResetter.ClearEvents(
                dependencies.ProtocolStates.ImportReferenceEvents.Events),
            ResetProtocolMatchState: () =>
            {
                dependencies.ProtocolStates.ProtocolMatchState.Reset();
            },
            UpdateProtocolMatchSummary: () => dependencies.ProtocolMatchController.UpdateSummary(
                dependencies.ProtocolStates.ProtocolMatchState.LastMatch),
            ClearImportEventsListSource: () => CodingImportReferenceControls.ClearItemsSource(
                controls.ImportEventsList),
            HideConfirmationPanels: () => CodingModeChromeControls.HideConfirmationPanels(
                controls.CodingConfirmationPanel,
                controls.DetectionConfirmationPanel),
            ClearPendingConfirmation: dependencies.ProtocolStates.PendingConfirmationState.Clear,
            ClearDetectionConfirmationBuffer:
                dependencies.DetectionController.ClearConfirmationBuffer,
            ClearDetectionOverlay: hideOverlay => DetectionOverlayCleanupController.ClearCanvas(
                controls.DetectionCanvas,
                controls.DetectionOverlay,
                hideOverlay),
            HideCodingSurface: () => CodingModeChromeControls.HideCodingSurface(
                controls.CodingOverlayPopup,
                controls.CodingOverlayCanvas,
                controls.CodingSidePanel,
                controls.CodingSidePanelColumn,
                controls.CodingToolbar,
                controls.CodingTimelinePanel,
                controls.CodingCalibrationHint,
                controls.CodingMeasurementPanel),
            HideInlineDefectDetail: dependencies.Actions.HideInlineDefectDetail,
            HideOsdBadge: () => CodingOsdBadgeControls.Hide(controls.OsdMeterBadge),
            ShowLiveDetectionEntry: isDetecting => CodingModeChromeControls.ShowLiveDetectionEntry(
                controls.LiveDetectionButton,
                controls.LiveDetectionStatusText,
                isDetecting),
            ClearActiveCodingToolName: dependencies.OverlayStates.ActiveToolNameState.Clear,
            ResetCodingIndicators: () => CodingModeChromeControls.ResetCodingIndicators(
                controls.ActiveToolLabel,
                controls.CodingLiveAiToggle,
                controls.CodingAiStageText),
            CancelCodingSchema: dependencies.SchemaStates.OverlayManagerOwner.Cancel,
            ClearCodingSchemaType: dependencies.SchemaStates.TypeState.Clear,
            DetachCodingViewModelPropertyChanged:
                dependencies.SessionRuntime.ViewModelOwner.DetachPropertyChanged,
            ClearCodingSessionReferences: () =>
            {
                dependencies.SessionRuntime.ViewModelOwner.Clear();
                dependencies.RuntimeStates.SessionRuntimeOwner.Clear();
                dependencies.RuntimeStates.OverlayRuntimeOwner.Clear();
            },
            ClearCodingCalibrationState: dependencies.OverlayStates.CalibrationState.Reset,
            ResetFrameReadiness: dependencies.Actions.ResetFrameReadiness,
            ResetCodingOverlaySuspendState:
                dependencies.OverlayInputVisibilityController.ResetSuspendState);
    }

    private static void Validate(PlayerWindowCodingModeExitControllerDependencies dependencies)
    {
        ArgumentNullException.ThrowIfNull(dependencies);
        ArgumentNullException.ThrowIfNull(dependencies.RuntimeStates);
        ArgumentNullException.ThrowIfNull(dependencies.SchemaStates);
        ArgumentNullException.ThrowIfNull(dependencies.OverlayStates);
        ArgumentNullException.ThrowIfNull(dependencies.AiStates);
        ArgumentNullException.ThrowIfNull(dependencies.ProtocolStates);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime.ViewModelOwner);
        ArgumentNullException.ThrowIfNull(dependencies.SessionRuntime.SessionHost);
        ArgumentNullException.ThrowIfNull(dependencies.OsdMeterController);
        ArgumentNullException.ThrowIfNull(dependencies.TimelineHost);
        ArgumentNullException.ThrowIfNull(dependencies.DetectionController);
        ArgumentNullException.ThrowIfNull(dependencies.StreckenschadenTrackingController);
        ArgumentNullException.ThrowIfNull(dependencies.BoundaryContext);
        ArgumentNullException.ThrowIfNull(dependencies.LiveDetectionPulseController);
        ArgumentNullException.ThrowIfNull(dependencies.PipelineHealthController);
        ArgumentNullException.ThrowIfNull(dependencies.ProtocolMatchController);
        ArgumentNullException.ThrowIfNull(dependencies.OverlayInputVisibilityController);
        ValidateControls(dependencies.Controls);
        ValidateActions(dependencies.Actions);
    }

    private static void ValidateControls(PlayerWindowCodingModeExitControls controls)
    {
        ArgumentNullException.ThrowIfNull(controls);
        ArgumentNullException.ThrowIfNull(controls.ImportEventsList);
        ArgumentNullException.ThrowIfNull(controls.CodingConfirmationPanel);
        ArgumentNullException.ThrowIfNull(controls.DetectionConfirmationPanel);
        ArgumentNullException.ThrowIfNull(controls.DetectionCanvas);
        ArgumentNullException.ThrowIfNull(controls.DetectionOverlay);
        ArgumentNullException.ThrowIfNull(controls.CodingOverlayPopup);
        ArgumentNullException.ThrowIfNull(controls.CodingOverlayCanvas);
        ArgumentNullException.ThrowIfNull(controls.CodingSidePanel);
        ArgumentNullException.ThrowIfNull(controls.CodingSidePanelColumn);
        ArgumentNullException.ThrowIfNull(controls.CodingToolbar);
        ArgumentNullException.ThrowIfNull(controls.CodingTimelinePanel);
        ArgumentNullException.ThrowIfNull(controls.CodingCalibrationHint);
        ArgumentNullException.ThrowIfNull(controls.CodingMeasurementPanel);
        ArgumentNullException.ThrowIfNull(controls.OsdMeterBadge);
        ArgumentNullException.ThrowIfNull(controls.LiveDetectionButton);
        ArgumentNullException.ThrowIfNull(controls.LiveDetectionStatusText);
        ArgumentNullException.ThrowIfNull(controls.ActiveToolLabel);
        ArgumentNullException.ThrowIfNull(controls.CodingLiveAiToggle);
        ArgumentNullException.ThrowIfNull(controls.CodingAiStageText);
    }

    private static void ValidateActions(PlayerWindowCodingModeExitActions actions)
    {
        ArgumentNullException.ThrowIfNull(actions);
        ArgumentNullException.ThrowIfNull(actions.CloseOpenStreckenschaeden);
        ArgumentNullException.ThrowIfNull(actions.HideInlineDefectDetail);
        ArgumentNullException.ThrowIfNull(actions.ResetFrameReadiness);
    }
}
