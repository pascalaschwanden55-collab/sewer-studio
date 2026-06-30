using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.UI.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

public sealed class SelfTrainingReviewCandidateSelectorTests
{
    [Fact]
    public void SelectForRun_keeps_only_reviewable_samples_for_result_case()
    {
        var result = Result("H-001", exact: 1, partial: 1, mismatch: 1, noFindings: 1);
        var samples = new[]
        {
            Sample("keep-partial", "H-001", MatchLevelNames.PartialMatch, TrainingSampleStatus.New),
            Sample("keep-exact", "H-001", MatchLevelNames.ExactMatch, TrainingSampleStatus.New),
            Sample("skip-approved", "H-001", MatchLevelNames.Mismatch, TrainingSampleStatus.Approved),
            Sample("skip-other-case", "H-002", MatchLevelNames.Mismatch, TrainingSampleStatus.New),
            Sample("skip-invalid", "H-001", "ProtocolStartdata", TrainingSampleStatus.New)
        };

        var selected = SelfTrainingReviewCandidateSelector.SelectForRun(samples, result);

        Assert.Equal(new[] { "keep-partial", "keep-exact" }, selected.Select(s => s.SampleId).ToArray());
    }

    [Fact]
    public void HasReviewableMatches_returns_false_when_result_has_no_reviewable_counts()
    {
        Assert.False(SelfTrainingReviewCandidateSelector.HasReviewableMatches(
            Result("H-001", exact: 0, partial: 0, mismatch: 0, noFindings: 0)));
    }

    [Theory]
    [InlineData(1, 0, 0, 0)]
    [InlineData(0, 1, 0, 0)]
    [InlineData(0, 0, 1, 0)]
    [InlineData(0, 0, 0, 1)]
    public void HasReviewableMatches_returns_true_for_any_reviewable_count(
        int exact,
        int partial,
        int mismatch,
        int noFindings)
    {
        Assert.True(SelfTrainingReviewCandidateSelector.HasReviewableMatches(
            Result("H-001", exact, partial, mismatch, noFindings)));
    }

    private static TrainingSample Sample(
        string sampleId,
        string caseId,
        string matchLevel,
        TrainingSampleStatus status)
        => new()
        {
            SampleId = sampleId,
            CaseId = caseId,
            MatchLevel = matchLevel,
            Status = status
        };

    private static SelfTrainingResult Result(
        string caseId,
        int exact,
        int partial,
        int mismatch,
        int noFindings)
        => new(
            caseId,
            TotalEntries: exact + partial + mismatch + noFindings,
            ExactMatches: exact,
            PartialMatches: partial,
            Mismatches: mismatch,
            NoFindings: noFindings,
            OverallTechnique: null,
            Duration: TimeSpan.Zero,
            SamplesGenerated: 0);
}
