using AuswertungPro.Next.Application.Ai.QualityGate;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEventDecisionPolicyTests
{
    [Fact]
    public void ApplyAiConfirmationDecision_sets_decision_and_quality_gate_level()
    {
        var ev = MakeEventWithAiContext();
        var gate = new QualityGateResult(0.8, TrafficLight.Yellow, new Dictionary<string, double>(), "test");

        var applied = CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
            ev,
            CodingUserDecision.Accepted,
            gate);

        Assert.True(applied);
        Assert.Equal(CodingUserDecision.Accepted, ev.AiContext!.Decision);
        Assert.Equal("Yellow", ev.AiContext.QualityGateLevel);
    }

    [Fact]
    public void ApplyAiConfirmationDecision_keeps_existing_quality_gate_level_when_gate_is_missing()
    {
        var ev = MakeEventWithAiContext();
        ev.AiContext!.QualityGateLevel = "Green";

        var applied = CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
            ev,
            CodingUserDecision.Rejected,
            gateResult: null);

        Assert.True(applied);
        Assert.Equal(CodingUserDecision.Rejected, ev.AiContext.Decision);
        Assert.Equal("Green", ev.AiContext.QualityGateLevel);
    }

    [Fact]
    public void ApplyAiConfirmationDecision_returns_false_without_ai_context()
    {
        var ev = MakeEventWithAiContext();
        ev.AiContext = null;

        var applied = CodingEventDecisionPolicy.ApplyAiConfirmationDecision(
            ev,
            CodingUserDecision.Accepted,
            gateResult: null);

        Assert.False(applied);
        Assert.Null(ev.AiContext);
    }

    [Fact]
    public void ApplyManualReviewDecision_creates_ai_context_for_manual_event()
    {
        var ev = MakeEventWithAiContext();
        ev.AiContext = null;

        CodingEventDecisionPolicy.ApplyManualReviewDecision(
            ev,
            CodingUserDecision.AcceptedWithEdit,
            "Manuell bearbeitet");

        Assert.NotNull(ev.AiContext);
        Assert.Equal("BBA", ev.AiContext!.SuggestedCode);
        Assert.Equal(1.0, ev.AiContext.Confidence);
        Assert.Equal("Manuell bearbeitet", ev.AiContext.Reason);
        Assert.Equal(CodingUserDecision.AcceptedWithEdit, ev.AiContext.Decision);
    }

    [Fact]
    public void ApplyManualReviewDecision_preserves_existing_ai_context_metadata()
    {
        var ev = MakeEventWithAiContext();
        ev.AiContext!.Reason = "KI-Vorschlag";
        ev.AiContext.Confidence = 0.42;

        CodingEventDecisionPolicy.ApplyManualReviewDecision(
            ev,
            CodingUserDecision.Rejected,
            "Manuell abgelehnt");

        Assert.Equal("KI-Vorschlag", ev.AiContext.Reason);
        Assert.Equal(0.42, ev.AiContext.Confidence);
        Assert.Equal(CodingUserDecision.Rejected, ev.AiContext.Decision);
    }

    private static CodingEvent MakeEventWithAiContext()
        => new()
        {
            Entry = new ProtocolEntry { Code = "BBA" },
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = "BBA",
                Confidence = 0.7,
                Reason = "KI-Vorschlag"
            }
        };
}
