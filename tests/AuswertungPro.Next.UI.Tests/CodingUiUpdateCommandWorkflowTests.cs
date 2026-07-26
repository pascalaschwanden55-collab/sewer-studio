using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingUiUpdateCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_and_preserves_navigation_pending_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateCommandWorkflow.Execute(
            new CodingUiUpdateCommandRequest(
                HasCodingViewModel: false,
                PropertyName: "CurrentMeter",
                NavigationPending: true),
            new CodingUiUpdateCommandActions(
                ApplyUiUpdate: (_, _) =>
                {
                    calls.Add("apply");
                    return new CodingUiUpdateResult(NavigationPending: false);
                }));

        Assert.Equal(CodingUiUpdateCommandOutcome.Skipped, result.Outcome);
        Assert.True(result.NavigationPending);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_applies_ui_update_and_returns_updated_navigation_pending_when_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingUiUpdateCommandWorkflow.Execute(
            new CodingUiUpdateCommandRequest(
                HasCodingViewModel: true,
                PropertyName: "CurrentMeter",
                NavigationPending: true),
            new CodingUiUpdateCommandActions(
                ApplyUiUpdate: (propertyName, navigationPending) =>
                {
                    calls.Add($"apply:{propertyName}:{navigationPending}");
                    return new CodingUiUpdateResult(NavigationPending: false);
                }));

        Assert.Equal(CodingUiUpdateCommandOutcome.Applied, result.Outcome);
        Assert.False(result.NavigationPending);
        Assert.Equal(["apply:CurrentMeter:True"], calls);
    }
}
