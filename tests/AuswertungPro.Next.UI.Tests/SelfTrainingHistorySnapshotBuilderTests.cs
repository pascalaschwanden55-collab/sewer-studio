using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingHistorySnapshotBuilderTests
{
    [Fact]
    public void Build_returns_null_when_result_has_no_match_counts()
    {
        var result = Result("H-001", totalEntries: 3, exact: 0, partial: 0, mismatch: 0, noFindings: 0);

        var snapshot = SelfTrainingHistorySnapshotBuilder.Build(result, DateTime.UnixEpoch);

        Assert.Null(snapshot);
    }

    [Fact]
    public void Build_maps_metadata_and_match_percentages()
    {
        var timestamp = new DateTime(2026, 6, 29, 12, 0, 0, DateTimeKind.Utc);
        var result = Result("H-001", totalEntries: 8, exact: 2, partial: 1, mismatch: 1, noFindings: 0);

        var snapshot = SelfTrainingHistorySnapshotBuilder.Build(result, timestamp);
        Assert.NotNull(snapshot);

        Assert.Equal(timestamp, snapshot!.TimestampUtc);
        Assert.Equal("H-001", snapshot.CaseId);
        Assert.Equal(8, snapshot.TotalEntries);
        Assert.Equal(0.5, snapshot.ExactPercent);
        Assert.Equal(0.25, snapshot.PartialPercent);
        Assert.Equal(0.25, snapshot.MismatchPercent);
        Assert.Equal(0, snapshot.NoFindingsPercent);
    }

    private static SelfTrainingResult Result(
        string caseId,
        int totalEntries,
        int exact,
        int partial,
        int mismatch,
        int noFindings)
        => new(
            caseId,
            totalEntries,
            exact,
            partial,
            mismatch,
            noFindings,
            OverallTechnique: null,
            Duration: TimeSpan.Zero,
            SamplesGenerated: 0);
}
