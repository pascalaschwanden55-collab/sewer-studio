using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingLastMatchRatePresentationBuilderTests
{
    [Fact]
    public void Build_returns_null_when_history_is_empty()
    {
        var result = SelfTrainingLastMatchRatePresentationBuilder.Build([]);

        Assert.Null(result);
    }

    [Fact]
    public void Build_uses_last_history_snapshot_percentages()
    {
        var first = Snapshot("first", exact: 0.1, partial: 0.2, mismatch: 0.3, noFindings: 0.4);
        var last = Snapshot("last", exact: 0.5, partial: 0.6, mismatch: 0.7, noFindings: 0.8);

        var result = SelfTrainingLastMatchRatePresentationBuilder.Build([first, last]);

        Assert.NotNull(result);
        Assert.Equal(0.5, result!.ExactPercent);
        Assert.Equal(0.6, result.PartialPercent);
        Assert.Equal(0.7, result.MismatchPercent);
        Assert.Equal(0.8, result.NoFindingsPercent);
    }

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
