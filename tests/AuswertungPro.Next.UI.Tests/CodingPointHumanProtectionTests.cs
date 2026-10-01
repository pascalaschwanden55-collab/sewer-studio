using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPointHumanProtectionTests
{
    [Theory]
    [InlineData(CodingUserDecision.Accepted)]
    [InlineData(CodingUserDecision.AcceptedWithEdit)]
    [InlineData(CodingUserDecision.Rejected)]
    [InlineData(CodingUserDecision.Ignored)]
    public void Beide_Entscheidungspfade_markieren_auch_ausdrueckliches_Ignored(CodingUserDecision decision)
    {
        var ai = Event(); var review = Event();
        CodingEventDecisionPolicy.ApplyAiConfirmationDecision(ai, decision, null);
        CodingEventDecisionPolicy.ApplyManualReviewDecision(review, decision, "bearbeitet");
        Assert.NotNull(ai.AiContext!.HumanTouchedAtUtc);
        Assert.NotNull(review.AiContext!.HumanTouchedAtUtc);
        Assert.Equal(decision, ai.AiContext.Decision);
        Assert.Equal(decision, review.AiContext.Decision);
    }

    [Theory]
    [InlineData("edit")]
    [InlineData("photo")]
    [InlineData("delete")]
    [InlineData("button")]
    [InlineData("cancel")]
    public void Menschliche_Bearbeitungspfade_sperren_Ignored_vor_der_Aktion(string action)
    {
        var ev = Event();
        switch (action)
        {
            case "edit": CodingEventEditApplier.Apply(ev, null); break;
            case "photo": CodingEventPhotoApplier.Apply(ev, "manual.png", null); break;
            case "delete": CodingEventDeleteApplier.Apply(ev, null, new List<CodingEvent> { ev }, ev); break;
            case "button": CodingEventEditButtonCommandWorkflow.Execute(new(ev), new(selected =>
                Assert.NotNull(selected.AiContext!.HumanTouchedAtUtc))); break;
            case "cancel": CodingEventEditCommandWorkflow.Execute(new(ev), new(() => { }, selected =>
            {
                Assert.NotNull(selected.AiContext!.HumanTouchedAtUtc);
                return false;
            }, _ => throw new InvalidOperationException("Cancelled"))); break;
        }
        Assert.NotNull(ev.AiContext!.HumanTouchedAtUtc);
        Assert.Equal(CodingUserDecision.Ignored, ev.AiContext.Decision);
    }

    private static CodingEvent Event() => new() { AiContext = new() { Decision = CodingUserDecision.Ignored } };
}
