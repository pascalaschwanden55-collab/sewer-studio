using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Regel-Tests fuer die zentrale KI-Freigabe. AutoAccept braucht neben den
/// Gate-Werten einen unabhaengigen, bestaetigenden Datenbank-Abgleich.
/// </summary>
public sealed class StandardAiDecisionPolicyTests
{
    private static AiDecision Decide(AiDecisionSignals s) => StandardAiDecisionPolicy.Default.Decide(s);

    [Fact]
    public void GleicherGateWertAlsSicherheitUndAmpel_OhneKbBeleg_BleibtReview()
    {
        var d = Decide(new AiDecisionSignals(0.94, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
        Assert.Contains("Datenbank-Abgleich", d.Reason);
    }

    [Fact] // Der Kern-Fix: hohe Sicherheit, aber Ampel NICHT gruen -> nie AutoAccept.
    public void HoheSicherheit_AberGelbeAmpel_Review()
    {
        var d = Decide(new AiDecisionSignals(0.99, TrafficLight.Yellow));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Ampel fehlt (Live ohne angereichertes Gate) -> kein zweiter Beleg -> Review.
    public void AmpelFehlt_Review()
    {
        var d = Decide(new AiDecisionSignals(0.99, QualityGate: null));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Rote Ampel -> Reject, egal wie hoch die Sicherheit.
    public void RoteAmpel_Reject()
    {
        var d = Decide(new AiDecisionSignals(0.99, TrafficLight.Red));
        Assert.Equal(AiDecisionOutcome.Reject, d.Outcome);
    }

    [Fact] // Sicherheit unter Reject-Schwelle -> Reject.
    public void SehrNiedrigeSicherheit_Reject()
    {
        var d = Decide(new AiDecisionSignals(0.40, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.Reject, d.Outcome);
    }

    [Fact] // Zwischenzone (0.60..0.92) -> Review.
    public void MittlereSicherheit_Review()
    {
        var d = Decide(new AiDecisionSignals(0.80, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Video-Fall: volle Belegkette -> AutoAccept.
    public void VideoVolleKette_AutoAccept()
    {
        var signals = new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.05);
        var d = Decide(signals);

        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
        Assert.Equal(AiDecisionReasonCode.EvidenceConfirmed, d.ReasonCode);
        Assert.Equal(StandardAiDecisionPolicy.PolicyVersion, d.PolicyVersion);
        Assert.Equal(signals, d.Signals);
        Assert.Equal(StandardAiDecisionPolicy.CurrentThresholds, d.Thresholds);
    }

    [Fact] // Video: vorhandener KB-Beleg widerspricht -> Review trotz hoher Sicherheit + gruener Ampel.
    public void VideoKbWiderspruch_Review()
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: false, EpistemicUncertainty: 0.05));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    [Fact] // Video: vorhandene Unsicherheit zu hoch -> Review.
    public void VideoHoheUnsicherheit_Review()
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.30));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
    }

    // ── Ungueltige Zahlen (Review 11.07., Befund 3): NaN laesst alle Vergleiche
    //    fehlschlagen — ohne Guard wuerde NaN mit gruener Ampel + KB-Beleg AutoAccept
    //    erreichen. Solche Werte sind Datenfehler und muessen hart auf Reject gehen. ──

    [Theory]
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(double.NegativeInfinity)]
    [InlineData(1.5)]
    [InlineData(-0.1)]
    public void UngueltigeSicherheit_IstImmerReject(double confidence)
    {
        var d = Decide(new AiDecisionSignals(confidence, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.05));
        Assert.Equal(AiDecisionOutcome.Reject, d.Outcome);
        Assert.Contains("ngueltig", d.Reason); // "Ungueltige Sicherheit ..."
    }

    [Theory] // Unsicherheit "vorhanden, aber unbrauchbar" (auch negativ!) -> nie AutoAccept.
    [InlineData(double.NaN)]
    [InlineData(double.PositiveInfinity)]
    [InlineData(-0.05)]
    [InlineData(1.2)]
    public void UnbrauchbareUnsicherheit_Review(double uncertainty)
    {
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: uncertainty));
        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
        Assert.Equal(AiDecisionReasonCode.InvalidUncertainty, d.ReasonCode);
    }

    [Fact]
    public void ExakteRejectGrenze_IstReviewNichtReject()
    {
        var d = Decide(new AiDecisionSignals(
            StandardAiDecisionPolicy.RejectConfidence,
            TrafficLight.Green,
            KbAgreement: true));

        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
        Assert.Equal(AiDecisionReasonCode.ConfidenceBelowAutoAccept, d.ReasonCode);
    }

    [Fact]
    public void ExakteAutoAcceptGrenze_OhneUnsicherheit_IstAutoAccept()
    {
        var d = Decide(new AiDecisionSignals(
            StandardAiDecisionPolicy.AutoAcceptConfidence,
            TrafficLight.Green,
            KbAgreement: true,
            EpistemicUncertainty: null));

        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
    }

    [Fact]
    public void ExakteUnsicherheitsGrenze_IstReview()
    {
        var d = Decide(new AiDecisionSignals(
            0.95,
            TrafficLight.Green,
            KbAgreement: true,
            EpistemicUncertainty: StandardAiDecisionPolicy.MaxEpistemicUncertainty));

        Assert.Equal(AiDecisionOutcome.Review, d.Outcome);
        Assert.Equal(AiDecisionReasonCode.UncertaintyTooHigh, d.ReasonCode);
    }
}
