using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStatisticsUpdateCommandWorkflowTests
{
    [Fact]
    public void Execute_skips_refresh_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingStatisticsUpdateCommandWorkflow.Execute(
            new CodingStatisticsUpdateCommandRequest(HasCodingViewModel: false),
            new CodingStatisticsUpdateCommandActions(
                RefreshStatistics: () => calls.Add("refresh")));

        Assert.Equal(CodingStatisticsUpdateCommandOutcome.Skipped, result.Outcome);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_refreshes_statistics_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingStatisticsUpdateCommandWorkflow.Execute(
            new CodingStatisticsUpdateCommandRequest(HasCodingViewModel: true),
            new CodingStatisticsUpdateCommandActions(
                RefreshStatistics: () => calls.Add("refresh")));

        Assert.Equal(CodingStatisticsUpdateCommandOutcome.Refreshed, result.Outcome);
        Assert.Equal(["refresh"], calls);
    }
}
