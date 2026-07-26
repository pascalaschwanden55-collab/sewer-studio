using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingEingabemarkerEventFactoryTests
{
    [Fact]
    public void CreateAccepted_builds_protocol_entry()
    {
        var videoTime = TimeSpan.FromSeconds(42);

        var draft = CodingEingabemarkerEventFactory.CreateAccepted(
            "BCD",
            "Rohranfang",
            "start",
            meter: 0.0,
            videoTime);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCD", draft.Entry.Code);
        Assert.Equal("Rohranfang", draft.Entry.Beschreibung);
        Assert.Equal(0.0, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
    }

    [Fact]
    public void CreateAccepted_marks_event_as_user_accepted_marker()
    {
        var draft = CodingEingabemarkerEventFactory.CreateAccepted(
            "BCA",
            "Anschluss",
            "anschluss",
            meter: 2.5,
            videoTime: TimeSpan.Zero);

        Assert.Equal("BCA", draft.AiContext.SuggestedCode);
        Assert.Equal(1.0, draft.AiContext.Confidence);
        Assert.Equal("Eingabemarker: anschluss", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Accepted, draft.AiContext.Decision);
    }
}
