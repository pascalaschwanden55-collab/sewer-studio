using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// S2b Review-Only-Schalter: bei RequireHumanReview darf NICHTS automatisch Gold/KB werden —
/// auch ein sauberer ExactMatch bleibt Kandidat und geht in die ReviewQueue. Deterministisch.
/// </summary>
public sealed class SelfTrainingAutoAcceptPolicyTests
{
    [Fact]
    public void RequireHumanReview_HoldsBackCleanExactMatch_RoutesToReview_WithReason()
    {
        var d = SelfTrainingAutoAcceptPolicy.Decide(MatchLevel.ExactMatch, requireHumanReview: true);

        Assert.Equal(TrainingSampleStatus.New, d.Status);          // kein Auto-Approve
        Assert.Equal(KbIndexState.None, d.KbIndexState);           // kein Auto-Index
        Assert.True(d.RouteToReview);
        Assert.Equal(SelfTrainingAutoAcceptPolicy.HumanReviewRequiredReason, d.Reason);
        Assert.Equal("HumanReviewRequired", d.Reason);
    }

    [Fact]
    public void AutoAcceptAllowed_CleanExactMatch_ApprovesAndQueuesForIndex()
    {
        var d = SelfTrainingAutoAcceptPolicy.Decide(MatchLevel.ExactMatch, requireHumanReview: false);

        Assert.Equal(TrainingSampleStatus.Approved, d.Status);     // S2-Verhalten
        Assert.Equal(KbIndexState.Pending, d.KbIndexState);
        Assert.False(d.RouteToReview);
        Assert.Null(d.Reason);
    }

    [Theory]
    [InlineData(MatchLevel.PartialMatch)]
    [InlineData(MatchLevel.Mismatch)]
    [InlineData(MatchLevel.NoFindings)]
    public void NonExactMatch_NeverAutoGold_AlwaysReview(MatchLevel level)
    {
        foreach (var requireReview in new[] { true, false })
        {
            var d = SelfTrainingAutoAcceptPolicy.Decide(level, requireReview);
            Assert.Equal(TrainingSampleStatus.New, d.Status);
            Assert.Equal(KbIndexState.None, d.KbIndexState);
            Assert.True(d.RouteToReview);
        }
    }
}
