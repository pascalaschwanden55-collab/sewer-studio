using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.QualityGate;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Regel-Tests fuer die zentrale KI-Freigabe (Audit Fix 3). Kontextabhaengig streng:
/// alle vorhandenen Belege muessen passen, Pflicht sind hohe Sicherheit + gruene Ampel.
/// </summary>
public sealed class StandardAiDecisionPolicyTests
{
    private static AiDecision Decide(AiDecisionSignals s) => StandardAiDecisionPolicy.Default.Decide(s);

    [Fact] // Live-Fall: zwei Belege (Sicherheit + Ampel) reichen fuer Gruen.
    public void LiveZweiBelege_HoheSicherheitUndGrueneAmpel_AutoAccept()
    {
        var d = Decide(new AiDecisionSignals(0.94, TrafficLight.Green));
        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
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
        var d = Decide(new AiDecisionSignals(0.95, TrafficLight.Green, KbAgreement: true, EpistemicUncertainty: 0.05));
        Assert.Equal(AiDecisionOutcome.AutoAccept, d.Outcome);
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
}
