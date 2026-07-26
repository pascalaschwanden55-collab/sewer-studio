using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingBoundaryEventFactoryTests
{
    [Fact]
    public void CreateStart_builds_unconfirmed_bcd_event()
    {
        var videoTime = TimeSpan.FromSeconds(3);

        var draft = CodingBoundaryEventFactory.CreateStart("Rohranfang", meter: 0.0, videoTime);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCD", draft.Entry.Code);
        Assert.Equal("Rohranfang", draft.Entry.Beschreibung);
        Assert.Equal(0.0, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Equal("BCD", draft.AiContext.SuggestedCode);
        Assert.Equal(1.0, draft.AiContext.Confidence);
        Assert.Equal("Rohranfang (Vorschlag - bitte bestätigen)", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void CreateEnd_builds_unconfirmed_bce_event()
    {
        var videoTime = TimeSpan.FromSeconds(90);

        var draft = CodingBoundaryEventFactory.CreateEnd("Rohrende", meter: 15.82, videoTime);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCE", draft.Entry.Code);
        Assert.Equal("Rohrende", draft.Entry.Beschreibung);
        Assert.Equal(15.82, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Equal("BCE", draft.AiContext.SuggestedCode);
        Assert.Equal(1.0, draft.AiContext.Confidence);
        Assert.Equal("Rohrende (Vorschlag - bitte bestätigen)", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }
}
