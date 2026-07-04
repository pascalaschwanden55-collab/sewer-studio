using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingLastMatchRateRefreshRequestFactoryTests
{
    [Fact]
    public void CreateWithDefaults_verdrahtet_history_store_default_und_ui()
    {
        var ui = new SelfTrainingMatchRatePresentationUi(
            _ => { },
            _ => { },
            _ => { },
            _ => { });

        var request = SelfTrainingLastMatchRateRefreshRequestFactory.CreateWithDefaults(
            new SelfTrainingLastMatchRateRefreshDefaultRequestFactoryRequest(ui));

        Assert.Same(ui, request.Ui);
        Assert.NotNull(request.LoadRunsAsync);
    }

    [Fact]
    public async Task Create_verdrahtet_last_match_rate_refresh_request()
    {
        var calls = new List<string>();
        var runs = new List<SelfTrainingRunSnapshot>
        {
            new(
                DateTime.UtcNow,
                "case-1",
                TotalEntries: 1,
                ExactPercent: 0.1,
                PartialPercent: 0.2,
                MismatchPercent: 0.3,
                NoFindingsPercent: 0.4)
        };
        var ui = new SelfTrainingMatchRatePresentationUi(
            value => calls.Add("exact:" + value),
            value => calls.Add("partial:" + value),
            value => calls.Add("mismatch:" + value),
            value => calls.Add("no-findings:" + value));

        var request = SelfTrainingLastMatchRateRefreshRequestFactory.Create(
            new SelfTrainingLastMatchRateRefreshRequestFactoryRequest(
                LoadRunsAsync: () =>
                {
                    calls.Add("load-runs");
                    return Task.FromResult(runs);
                },
                Ui: ui));

        Assert.Same(runs, await request.LoadRunsAsync());
        Assert.Same(ui, request.Ui);
        request.Ui.SetExactPercent(0.5);

        Assert.Equal(["load-runs", "exact:0.5"], calls);
    }
}
