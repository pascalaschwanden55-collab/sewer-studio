using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionStateCreationWorkflowTests
{
    [Fact]
    public void Execute_with_request_creates_state_and_applies_services()
    {
        var calls = new List<string>();
        CodingSessionStateComponents? capturedState = null;

        var result = CodingSessionStateCreationWorkflow.Execute(
            new CodingSessionStateCreationRequest(
                VideoPath: @"C:\videos\haltung.mp4",
                Settings: null),
            new CodingSessionStateCreationApplyActions(
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
                CancelSchema: () => calls.Add("cancel-schema"),
                ClearSchemaType: () => calls.Add("clear-schema-type"),
                SetViewModel: (viewModel, observePropertyChanged) =>
                {
                    calls.Add($"view-model:{observePropertyChanged}");
                    capturedState = capturedState is null
                        ? new CodingSessionStateComponents(null!, null!, viewModel)
                        : capturedState with { ViewModel = viewModel };
                }));

        Assert.Equal(CodingSessionStateCreationWorkflowOutcome.Created, result.Outcome);
        Assert.Equal(
            ["session", "overlay", "cancel-schema", "clear-schema-type", "view-model:True"],
            calls);
        Assert.NotNull(capturedState);
        Assert.NotNull(capturedState.SessionService);
        Assert.NotNull(capturedState.OverlayService);
        Assert.NotNull(capturedState.ViewModel);
        Assert.Equal(@"C:\videos\haltung.mp4", capturedState.ViewModel.VideoPath);
    }

    [Fact]
    public void Execute_creates_state_applies_services_clears_schema_and_sets_view_model()
    {
        var calls = new List<string>();
        var state = new CodingSessionStateComponents(null!, null!, null!);

        var result = CodingSessionStateCreationWorkflow.Execute(
            new CodingSessionStateCreationWorkflowActions(
                CreateState: () =>
                {
                    calls.Add("create");
                    return state;
                },
                SetSessionService: _ => calls.Add("session"),
                SetOverlayService: _ => calls.Add("overlay"),
                CancelSchema: () => calls.Add("cancel-schema"),
                ClearSchemaType: () => calls.Add("clear-schema-type"),
                SetViewModel: (_, observePropertyChanged) => calls.Add($"view-model:{observePropertyChanged}")));

        Assert.Equal(CodingSessionStateCreationWorkflowOutcome.Created, result.Outcome);
        Assert.True(result.Created);
        Assert.Equal(
            ["create", "session", "overlay", "cancel-schema", "clear-schema-type", "view-model:True"],
            calls);
    }
}
