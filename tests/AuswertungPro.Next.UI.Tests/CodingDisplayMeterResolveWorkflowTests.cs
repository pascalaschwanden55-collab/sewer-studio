using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingDisplayMeterResolveWorkflowTests
{
    [Fact]
    public void Execute_returns_zero_without_resolving_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingDisplayMeterResolveWorkflow.Execute(
            new CodingDisplayMeterResolveRequest(HasCodingViewModel: false),
            new CodingDisplayMeterResolveActions(
                ResolveDisplayMeter: () =>
                {
                    calls.Add("resolve");
                    return 12.5;
                }));

        Assert.Equal(CodingDisplayMeterResolveOutcome.NoCodingContext, result.Outcome);
        Assert.Equal(0, result.DisplayMeter);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_returns_resolved_display_meter_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingDisplayMeterResolveWorkflow.Execute(
            new CodingDisplayMeterResolveRequest(HasCodingViewModel: true),
            new CodingDisplayMeterResolveActions(
                ResolveDisplayMeter: () =>
                {
                    calls.Add("resolve");
                    return 12.5;
                }));

        Assert.Equal(CodingDisplayMeterResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(12.5, result.DisplayMeter);
        Assert.Equal(["resolve"], calls);
    }
}
