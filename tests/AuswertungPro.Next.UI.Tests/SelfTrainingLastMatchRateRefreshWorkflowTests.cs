using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingLastMatchRateRefreshWorkflowTests
{
    [Fact]
    public async Task RunAsync_laed_history_und_wendet_letzte_match_rate_an()
    {
        var calls = new List<string>();

        await SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(
            new SelfTrainingLastMatchRateRefreshWorkflowRequest(
                LoadRunsAsync: () => Task.FromResult(new List<SelfTrainingRunSnapshot>
                {
                    Snapshot("first", 0.1, 0.2, 0.3, 0.4),
                    Snapshot("last", 0.5, 0.6, 0.7, 0.8)
                }),
                Ui: Ui(calls)));

        Assert.Equal(
            [
                "exact:0.5",
                "partial:0.6",
                "mismatch:0.7",
                "no-findings:0.8"
            ],
            calls);
    }

    [Fact]
    public async Task RunAsync_tut_nichts_wenn_history_leer_ist()
    {
        var calls = new List<string>();

        await SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(
            new SelfTrainingLastMatchRateRefreshWorkflowRequest(
                LoadRunsAsync: () => Task.FromResult(new List<SelfTrainingRunSnapshot>()),
                Ui: Ui(calls)));

        Assert.Empty(calls);
    }

    [Fact]
    public async Task RunAsync_schluckt_history_ladefehler()
    {
        var calls = new List<string>();

        await SelfTrainingLastMatchRateRefreshWorkflow.RunAsync(
            new SelfTrainingLastMatchRateRefreshWorkflowRequest(
                LoadRunsAsync: () => throw new InvalidOperationException("keine history"),
                Ui: Ui(calls)));

        Assert.Empty(calls);
    }

    private static SelfTrainingMatchRatePresentationUi Ui(List<string> calls)
        => new(
            value => calls.Add($"exact:{value}"),
            value => calls.Add($"partial:{value}"),
            value => calls.Add($"mismatch:{value}"),
            value => calls.Add($"no-findings:{value}"));

    private static SelfTrainingRunSnapshot Snapshot(
        string caseId,
        double exact,
        double partial,
        double mismatch,
        double noFindings)
        => new(
            DateTime.UtcNow,
            caseId,
            TotalEntries: 10,
            ExactPercent: exact,
            PartialPercent: partial,
            MismatchPercent: mismatch,
            NoFindingsPercent: noFindings);
}
