using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingTimelineInitializationWorkflowTests
{
    [Fact]
    public void Execute_throws_without_configuring_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CodingTimelineInitializationWorkflow.Execute(
                new CodingTimelineInitializationRequest(HasCodingViewModel: false),
                new CodingTimelineInitializationActions(
                    ConfigureTimeline: () => calls.Add("configure"))));

        Assert.Equal("Coding timeline requires an active coding view model.", ex.Message);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_configures_timeline_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingTimelineInitializationWorkflow.Execute(
            new CodingTimelineInitializationRequest(HasCodingViewModel: true),
            new CodingTimelineInitializationActions(
                ConfigureTimeline: () => calls.Add("configure")));

        Assert.Equal(CodingTimelineInitializationOutcome.Configured, result.Outcome);
        Assert.Equal(["configure"], calls);
    }
}
