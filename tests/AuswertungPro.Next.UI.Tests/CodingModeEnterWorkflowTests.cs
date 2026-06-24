using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingModeEnterWorkflowTests
{
    [Theory]
    [InlineData(true, true)]
    [InlineData(false, false)]
    public void Execute_skips_when_already_coding_or_haltung_is_missing(
        bool isCodingMode,
        bool hasHaltungRecord)
    {
        var calls = new List<string>();

        CodingModeEnterWorkflow.Execute(
            new CodingModeEnterWorkflowRequest(isCodingMode, hasHaltungRecord),
            Actions(calls));

        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_stops_after_session_start_failure_without_resetting_coding_mode()
    {
        var calls = new List<string>();

        CodingModeEnterWorkflow.Execute(
            new CodingModeEnterWorkflowRequest(IsCodingMode: false, HasHaltungRecord: true),
            Actions(
                calls,
                tryStartCodingSession: () =>
                {
                    calls.Add("try-start");
                    return false;
                }));

        Assert.Equal(
            [
                "coding:True",
                "reset-frame",
                "prepare-playback",
                "create-session",
                "apply-dn",
                "ensure-length",
                "try-start"
            ],
            calls);
    }

    [Fact]
    public void Execute_runs_complete_enter_sequence_when_session_starts()
    {
        var calls = new List<string>();

        CodingModeEnterWorkflow.Execute(
            new CodingModeEnterWorkflowRequest(IsCodingMode: false, HasHaltungRecord: true),
            Actions(calls));

        Assert.Equal(
            [
                "coding:True",
                "reset-frame",
                "prepare-playback",
                "create-session",
                "apply-dn",
                "ensure-length",
                "try-start",
                "init-import",
                "default-tool",
                "show-ui",
                "init-timeline",
                "background",
                "load-existing",
                "nav-pending:True",
                "sync-video"
            ],
            calls);
    }

    private static CodingModeEnterWorkflowActions Actions(
        List<string> calls,
        Func<bool>? tryStartCodingSession = null)
        => new(
            SetCodingMode: value => calls.Add($"coding:{value}"),
            ResetFrameReadiness: () => calls.Add("reset-frame"),
            PrepareCodingModePlayback: () => calls.Add("prepare-playback"),
            CreateCodingSessionState: () => calls.Add("create-session"),
            ApplyCodingDnCalibration: () => calls.Add("apply-dn"),
            EnsureHaltungslaenge: () => calls.Add("ensure-length"),
            TryStartCodingSession: tryStartCodingSession ?? (() =>
            {
                calls.Add("try-start");
                return true;
            }),
            InitializeCodingImportReferences: () => calls.Add("init-import"),
            ActivateDefaultCodingTool: () => calls.Add("default-tool"),
            ShowCodingModeUi: () => calls.Add("show-ui"),
            InitializeCodingTimeline: () => calls.Add("init-timeline"),
            StartCodingModeBackgroundServices: () => calls.Add("background"),
            LoadExistingProtocolEventsAsImport: () => calls.Add("load-existing"),
            SetCodingNavigationPending: value => calls.Add($"nav-pending:{value}"),
            SyncVideoToCodingMeter: () => calls.Add("sync-video"));
}
