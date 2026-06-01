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

    [Fact]
    public void KbDisagreement_VetoesAutoAccept_EvenWhenAutoAllowed()
    {
        // Selbst bei ExactMatch + Flag aus: KB-Widerspruch -> immer Review.
        var d = SelfTrainingAutoAcceptPolicy.Decide(
            MatchLevel.ExactMatch, requireHumanReview: false, KbCheckResult.KbDisagreement);

        Assert.Equal(TrainingSampleStatus.New, d.Status);
        Assert.Equal(KbIndexState.None, d.KbIndexState);
        Assert.True(d.RouteToReview);
        Assert.Equal(SelfTrainingAutoAcceptPolicy.KbDisagreementReason, d.Reason);
    }

    [Fact]
    public void KbAgreement_DoesNotOverride_RequireHumanReview()
    {
        // KbAgreement ist nur ein Kandidaten-Signal; RequireHumanReview bleibt staerker.
        var d = SelfTrainingAutoAcceptPolicy.Decide(
            MatchLevel.ExactMatch, requireHumanReview: true, KbCheckResult.KbAgreement);

        Assert.Equal(TrainingSampleStatus.New, d.Status);
        Assert.Equal(KbIndexState.None, d.KbIndexState);
        Assert.Equal(SelfTrainingAutoAcceptPolicy.HumanReviewRequiredReason, d.Reason);
    }

    [Theory]
    [InlineData(KbCheckResult.KbAgreement)]
    [InlineData(KbCheckResult.KbNoSignal)]
    public void NonDisagreement_WithFlagOff_CleanExact_StillApproves(KbCheckResult kb)
    {
        // Ohne Flag und ohne KB-Widerspruch bleibt das S2-Verhalten (Auto-Approve bei ExactMatch).
        var d = SelfTrainingAutoAcceptPolicy.Decide(
            MatchLevel.ExactMatch, requireHumanReview: false, kb);

        Assert.Equal(TrainingSampleStatus.Approved, d.Status);
        Assert.Equal(KbIndexState.Pending, d.KbIndexState);
        Assert.False(d.RouteToReview);
    }
}
