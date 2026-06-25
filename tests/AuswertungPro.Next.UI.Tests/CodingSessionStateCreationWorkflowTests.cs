using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingSessionStateCreationWorkflowTests
{
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
