namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingModeEnterWorkflowRequest(
    bool IsCodingMode,
    bool HasHaltungRecord);

public sealed record CodingModeEnterWorkflowActions(
    Action<bool> SetCodingMode,
    Action ResetFrameReadiness,
    Action PrepareCodingModePlayback,
    Action CreateCodingSessionState,
    Action ApplyCodingDnCalibration,
    Action EnsureHaltungslaenge,
    Func<bool> TryStartCodingSession,
    Action InitializeCodingImportReferences,
    Action ActivateDefaultCodingTool,
    Action ShowCodingModeUi,
    Action InitializeCodingTimeline,
    Action StartCodingModeBackgroundServices,
    Action LoadExistingProtocolEventsAsImport,
    Action<bool> SetCodingNavigationPending,
    Action SyncVideoToCodingMeter);

public static class CodingModeEnterWorkflow
{
    public static void Execute(
        CodingModeEnterWorkflowRequest request,
        CodingModeEnterWorkflowActions actions)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(actions);

        if (request.IsCodingMode || !request.HasHaltungRecord)
            return;

        actions.SetCodingMode(true);
        actions.ResetFrameReadiness();

        actions.PrepareCodingModePlayback();
        actions.CreateCodingSessionState();
        actions.ApplyCodingDnCalibration();
        actions.EnsureHaltungslaenge();

        if (!actions.TryStartCodingSession())
            return;

        actions.InitializeCodingImportReferences();
        actions.ActivateDefaultCodingTool();
        actions.ShowCodingModeUi();

        actions.InitializeCodingTimeline();
        actions.StartCodingModeBackgroundServices();

        actions.LoadExistingProtocolEventsAsImport();

        actions.SetCodingNavigationPending(true);
        actions.SyncVideoToCodingMeter();
    }
}
