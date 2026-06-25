using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEndMeterResolveWorkflowTests
{
    [Fact]
    public void Execute_returns_null_without_resolving_when_coding_view_model_is_missing()
    {
        var calls = new List<string>();

        var result = CodingEndMeterResolveWorkflow.Execute(
            new CodingEndMeterResolveRequest(HasCodingViewModel: false),
            new CodingEndMeterResolveActions(
                ResolveEndMeter: () =>
                {
                    calls.Add("resolve");
                    return 42.5;
                }));

        Assert.Equal(CodingEndMeterResolveOutcome.NoCodingContext, result.Outcome);
        Assert.Null(result.EndMeter);
        Assert.Empty(calls);
    }

    [Fact]
    public void Execute_returns_resolved_end_meter_when_coding_view_model_exists()
    {
        var calls = new List<string>();

        var result = CodingEndMeterResolveWorkflow.Execute(
            new CodingEndMeterResolveRequest(HasCodingViewModel: true),
            new CodingEndMeterResolveActions(
                ResolveEndMeter: () =>
                {
                    calls.Add("resolve");
                    return 42.5;
                }));

        Assert.Equal(CodingEndMeterResolveOutcome.Resolved, result.Outcome);
        Assert.Equal(42.5, result.EndMeter);
        Assert.Equal(["resolve"], calls);
    }
}
