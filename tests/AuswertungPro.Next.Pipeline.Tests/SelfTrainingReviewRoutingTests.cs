using AuswertungPro.Next.Application.Ai.Training;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class SelfTrainingReviewRoutingTests
{
    [Theory]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.Mismatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.PartialMatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.ExactMatch, TrainingSampleStatus.New, true)]
    [InlineData(MatchLevel.ExactMatch, TrainingSampleStatus.Approved, false)]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.Rejected, false)]
    [InlineData(MatchLevel.NoFindings, TrainingSampleStatus.Removed, false)]
    public void ShouldEnqueue(MatchLevel level, TrainingSampleStatus status, bool expected)
        => Assert.Equal(expected, SelfTrainingReviewRouting.ShouldEnqueue(level, status));

    [Theory]
    [InlineData(MatchLevel.NoFindings, 0.95)]
    [InlineData(MatchLevel.Mismatch, 0.90)]
    [InlineData(MatchLevel.PartialMatch, 0.60)]
    [InlineData(MatchLevel.ExactMatch, 0.30)]
    public void Priority(MatchLevel level, double expected)
        => Assert.Equal(expected, SelfTrainingReviewRouting.Priority(level), precision: 3);
}
