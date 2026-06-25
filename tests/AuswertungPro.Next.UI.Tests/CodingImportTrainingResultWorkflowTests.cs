using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingImportTrainingResultWorkflowTests
{
    [Fact]
    public void Execute_returns_rejected_without_badge_actions()
    {
        var result = CodingImportTrainingResultWorkflow.Execute(
            new CodingProtocolImportTrainingResult(
                Accepted: false,
                Badge: default),
            new CodingImportTrainingResultActions(
                ShowBadge: _ => throw new InvalidOperationException("Badge should not be shown."),
                ScheduleHideBadge: _ => throw new InvalidOperationException("Hide should not be scheduled.")));

        Assert.Equal(CodingImportTrainingResultOutcome.Rejected, result.Outcome);
        Assert.False(result.Accepted);
    }

    [Fact]
    public void Execute_shows_badge_and_schedules_hide_for_accepted_result()
    {
        var calls = new List<string>();
        var delay = TimeSpan.FromSeconds(3);
        var badge = new CodingImportConfirmationBadgeState("? BAB @ 1.2m bestaetigt", delay);

        var result = CodingImportTrainingResultWorkflow.Execute(
            new CodingProtocolImportTrainingResult(Accepted: true, badge),
            new CodingImportTrainingResultActions(
                ShowBadge: text => calls.Add($"show:{text}"),
                ScheduleHideBadge: hideDelay => calls.Add($"hide:{hideDelay.TotalSeconds}")));

        Assert.Equal(CodingImportTrainingResultOutcome.Accepted, result.Outcome);
        Assert.True(result.Accepted);
        Assert.Equal(["show:? BAB @ 1.2m bestaetigt", "hide:3"], calls);
    }
}
