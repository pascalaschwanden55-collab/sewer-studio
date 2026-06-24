namespace AuswertungPro.Next.UI.Ai;

public sealed record CodingModeExitTeardownWorkflowRequest(
    bool HasCodingLiveAiTimers,
    bool HasCodingViewModel,
    bool IsLiveDetectionRunning);

public sealed record CodingModeExitTeardownWorkflowActions(
    Action StopCodingOsdTimer,
    Action DisposeCodingOsdMeterService,
    Action<bool> StopCodingLiveAiTimers,
    Action StopCodingAiPulse,
    Action StopPipelineHealthMonitor,
    Action DisposeAnalysisCancellation,
    Action ClearImportReferenceEvents,
    Action ResetProtocolMatchState,
    Action UpdateProtocolMatchSummary,
    Action ClearImportEventsListSource,
    Action HideConfirmationPanels,
    Action ClearPendingConfirmation,
    Action ClearDetectionConfirmationBuffer,
    Action<bool> ClearDetectionOverlay,
    Action HideCodingSurface,
    Action HideInlineDefectDetail,
    Action HideOsdBadge,
    Action<bool> ShowLiveDetectionEntry,
    Action ClearActiveCodingToolName,
    Action ResetCodingIndicators,
    Action CancelCodingSchema,
    Action ClearCodingSchemaType,
    Action DetachCodingViewModelPropertyChanged,
    Action ClearCodingSessionReferences,
    Action ClearCodingCalibrationState,
    Action ResetFrameReadiness,
    Action ResetCodingOverlaySuspendState);

public static class CodingModeExitTeardownWorkflow
{
    public static void Execute(
        CodingModeExitTeardownWorkflowRequest request,
        CodingModeExitTeardownWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        actions.StopCodingOsdTimer();
        actions.DisposeCodingOsdMeterService();

        if (request.HasCodingLiveAiTimers)
            actions.StopCodingLiveAiTimers(true);

        actions.StopCodingAiPulse();
        actions.StopPipelineHealthMonitor();
        actions.DisposeAnalysisCancellation();

        actions.ClearImportReferenceEvents();
        actions.ResetProtocolMatchState();
        actions.UpdateProtocolMatchSummary();
        actions.ClearImportEventsListSource();

        actions.HideConfirmationPanels();
        actions.ClearPendingConfirmation();
        actions.ClearDetectionConfirmationBuffer();
        actions.ClearDetectionOverlay(!request.IsLiveDetectionRunning);

        actions.HideCodingSurface();
        actions.HideInlineDefectDetail();
        actions.HideOsdBadge();
        actions.ShowLiveDetectionEntry(request.IsLiveDetectionRunning);

        actions.ClearActiveCodingToolName();
        actions.ResetCodingIndicators();

        actions.CancelCodingSchema();
        actions.ClearCodingSchemaType();

        if (request.HasCodingViewModel)
            actions.DetachCodingViewModelPropertyChanged();

        actions.ClearCodingSessionReferences();
        actions.ClearCodingCalibrationState();
        actions.ResetFrameReadiness();
        actions.ResetCodingOverlaySuspendState();
    }
}
