using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

public sealed class CodingFeedbackDecisionMapperTests
{
    [Fact]
    public void TryCreate_ForAcceptedAiEvent_ReturnsFeedbackDecision()
    {
        var ev = MakeAiEvent("BBA", "BBA", CodingUserDecision.Accepted);
        ev.Overlay = new OverlayGeometry
        {
            ClockFrom = 6.5,
            Q1Mm = 12.4,
            Q2Mm = 3.2,
            FillPercent = 18.6
        };

        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, "H-500");

        Assert.NotNull(decision);
        Assert.Equal("H-500", decision.CaseId);
        Assert.Equal("BBA", decision.SuggestedCode);
        Assert.Equal("BBA", decision.FinalCode);
        Assert.True(decision.Accepted);
        Assert.Equal(2.4, decision.MeterStart);
        Assert.Equal(2.4, decision.MeterEnd);
        Assert.Equal("KI-Vorschlag", decision.Label);
        Assert.Equal("mittel", decision.Severity);
        Assert.Equal(0.72, decision.Confidence);
        Assert.Equal("Testgrund", decision.Reason);
        Assert.Equal("6.5", decision.PositionClock);
        Assert.Equal(12, decision.HeightMm);
        Assert.Equal(3, decision.WidthMm);
        Assert.Equal(19, decision.CrossSectionReductionPercent);
    }

    [Fact]
    public void TryCreate_ForAcceptedWithEdit_UsesEditedFinalCode()
    {
        var ev = MakeAiEvent("BBA", "BAA", CodingUserDecision.AcceptedWithEdit);

        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, "H-501");

        Assert.NotNull(decision);
        Assert.Equal("BBA", decision.SuggestedCode);
        Assert.Equal("BAA", decision.FinalCode);
        Assert.True(decision.Accepted);
    }

    [Fact]
    public void TryCreate_ForRejectedAiEvent_KeepsSuggestedCodeAndEmptyFinalCode()
    {
        var ev = MakeAiEvent("BBA", "BBA", CodingUserDecision.Rejected);

        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, "H-502");

        Assert.NotNull(decision);
        Assert.Equal("BBA", decision.SuggestedCode);
        Assert.Equal("", decision.FinalCode);
        Assert.False(decision.Accepted);
    }

    [Fact]
    public void TryCreate_ForManualEvent_ReturnsNull()
    {
        var ev = new CodingEvent
        {
            Entry = new ProtocolEntry
            {
                Code = "BBA",
                Beschreibung = "Manuell",
                MeterStart = 1.2,
                Source = ProtocolEntrySource.Manual
            },
            MeterAtCapture = 1.2
        };

        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, "H-503");

        Assert.Null(decision);
    }

    [Fact]
    public void TryCreate_WithoutSuggestedCode_ReturnsNull()
    {
        var ev = MakeAiEvent(null, "BBA", CodingUserDecision.Accepted);

        var decision = CodingFeedbackDecisionMapper.TryCreate(ev, "H-504");

        Assert.Null(decision);
    }

    private static CodingEvent MakeAiEvent(
        string? suggestedCode,
        string finalCode,
        CodingUserDecision decision)
        => new()
        {
            Entry = new ProtocolEntry
            {
                Code = finalCode,
                Beschreibung = "KI-Vorschlag",
                MeterStart = 2.4,
                Source = ProtocolEntrySource.Ai,
                CodeMeta = new ProtocolEntryCodeMeta { Severity = "mittel" }
            },
            MeterAtCapture = 2.4,
            AiContext = new CodingEventAiContext
            {
                SuggestedCode = suggestedCode,
                Confidence = 0.72,
                Reason = "Testgrund",
                Decision = decision
            }
        };
}
