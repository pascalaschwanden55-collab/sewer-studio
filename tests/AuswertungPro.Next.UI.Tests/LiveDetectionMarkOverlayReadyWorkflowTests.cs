using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Live;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class LiveDetectionMarkOverlayReadyWorkflowTests
{
    [Fact]
    public void Execute_with_state_request_creates_state_and_applies_services_when_not_ready()
    {
        var calls = new List<string>();
        CodingSessionStateComponents? capturedState = null;

        var result = LiveDetectionMarkOverlayReadyWorkflow.Execute(
            new LiveDetectionMarkOverlayReadyStateRequest(
                HasOverlayService: false,
                HasViewModel: true,
                VideoPath: @"C:\videos\manual-mark.mp4",
                Settings: null,
                ExistingSessionService: null,
                ExistingOverlayService: null),
            new LiveDetectionMarkOverlayReadyApplyActions(
                SetSessionService: service =>
                {
                    calls.Add("session");
                    capturedState = capturedState is null
                        ? new CodingSessionStateComponents(service, null!, null!)
                        : capturedState with { SessionService = service };
                },
                SetOverlayService: service =>
                {
                    calls.Add("overlay");
                    capturedState = capturedState is null
                        ? new CodingSessionStateComponents(null!, service, null!)
                        : capturedState with { OverlayService = service };
                },
                SetViewModel: viewModel =>
                {
                    calls.Add("view-model");
                    capturedState = capturedState is null
                        ? new CodingSessionStateComponents(null!, null!, viewModel)
                        : capturedState with { ViewModel = viewModel };
                }));

        Assert.Equal(LiveDetectionMarkOverlayReadyOutcome.Created, result.Outcome);
        Assert.True(result.Created);
        Assert.Equal(["session", "overlay", "view-model"], calls);
        Assert.NotNull(capturedState);
        Assert.NotNull(capturedState.SessionService);
        Assert.NotNull(capturedState.OverlayService);
        Assert.NotNull(capturedState.ViewModel);
        Assert.Equal(@"C:\videos\manual-mark.mp4", capturedState.ViewModel.VideoPath);
    }

    [Fact]
    public void Execute_skips_when_overlay_and_view_model_are_ready()
    {
        var result = LiveDetectionMarkOverlayReadyWorkflow.Execute(
            new LiveDetectionMarkOverlayReadyRequest(
                HasOverlayService: true,
                HasViewModel: true),
            Actions(_ => throw new InvalidOperationException("No action should run.")));

        Assert.Equal(LiveDetectionMarkOverlayReadyOutcome.AlreadyReady, result.Outcome);
        Assert.False(result.Created);
    }

    [Fact]
    public void Execute_creates_state_and_applies_services_and_view_model_when_not_ready()
    {
        var calls = new List<string>();
        var state = new CodingSessionStateComponents(null!, null!, null!);

        var result = LiveDetectionMarkOverlayReadyWorkflow.Execute(
            new LiveDetectionMarkOverlayReadyRequest(
                HasOverlayService: false,
                HasViewModel: true),
            Actions(
                calls.Add,
                createState: () =>
                {
                    calls.Add("create");
                    return state;
                }));

        Assert.Equal(LiveDetectionMarkOverlayReadyOutcome.Created, result.Outcome);
        Assert.True(result.Created);
        Assert.Equal(["create", "session", "overlay", "view-model"], calls);
    }

    private static LiveDetectionMarkOverlayReadyActions Actions(
        Action<string> calls,
        Func<CodingSessionStateComponents>? createState = null)
        => new(
            CreateState: createState ?? (() => new CodingSessionStateComponents(null!, null!, null!)),
            SetSessionService: _ => calls("session"),
            SetOverlayService: _ => calls("overlay"),
            SetViewModel: _ => calls("view-model"));
}
