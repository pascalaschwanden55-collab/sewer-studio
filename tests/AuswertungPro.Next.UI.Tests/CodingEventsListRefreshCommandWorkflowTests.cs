using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventsListRefreshCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_colorize_when_refresh_returns_false()
    {
        var calls = new List<string>();

        var result = CodingEventsListRefreshCommandWorkflow.Execute(
            new CodingEventsListRefreshCommandActions(
                RefreshListAndStatistics: () =>
                {
                    calls.Add("refresh:false");
                    return false;
                },
                ScheduleColorize: () => calls.Add("schedule-colorize")));

        Assert.Equal(CodingEventsListRefreshCommandOutcome.Skipped, result.Outcome);
        Assert.Equal(["refresh:false"], calls);
    }

    [Fact]
    public void Execute_schedules_colorize_after_successful_refresh()
    {
        var calls = new List<string>();

        var result = CodingEventsListRefreshCommandWorkflow.Execute(
            new CodingEventsListRefreshCommandActions(
                RefreshListAndStatistics: () =>
                {
                    calls.Add("refresh:true");
                    return true;
                },
                ScheduleColorize: () => calls.Add("schedule-colorize")));

        Assert.Equal(CodingEventsListRefreshCommandOutcome.Refreshed, result.Outcome);
        Assert.Equal(["refresh:true", "schedule-colorize"], calls);
    }
}
