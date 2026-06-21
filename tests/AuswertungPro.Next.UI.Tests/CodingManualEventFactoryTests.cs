using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualEventFactoryTests
{
    [Fact]
    public void CreateUnconfirmed_builds_manual_protocol_entry_and_ai_context()
    {
        var videoTime = TimeSpan.FromSeconds(12);

        var draft = CodingManualEventFactory.CreateUnconfirmed(
            "BCA",
            "Anschluss",
            meter: 4.2,
            videoTime,
            overlay: null);

        Assert.Equal(ProtocolEntrySource.Manual, draft.Entry.Source);
        Assert.Equal("BCA", draft.Entry.Code);
        Assert.Equal("Anschluss", draft.Entry.Beschreibung);
        Assert.Equal(4.2, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Null(draft.Entry.CodeMeta);
        Assert.Equal("BCA", draft.AiContext.SuggestedCode);
        Assert.Equal(1.0, draft.AiContext.Confidence);
        Assert.Equal("Manuell codiert - bitte bestätigen", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void CreateUnconfirmed_applies_overlay_quantification()
    {
        var overlay = new OverlayGeometry
        {
            ToolType = OverlayToolType.Level,
            Points =
            [
                new NormalizedPoint(0.1, 0.1),
                new NormalizedPoint(0.9, 0.1),
                new NormalizedPoint(0.9, 0.4)
            ],
            ClockFrom = 3,
            ClockTo = 5,
            Q1Mm = 12.34,
            Q2Mm = 8.7,
            FillPercent = 42.2
        };

        var draft = CodingManualEventFactory.CreateUnconfirmed(
            "BDD",
            "Wasserstand",
            meter: 1.0,
            videoTime: TimeSpan.Zero,
            overlay);

        Assert.Equal("3.0", draft.Entry.CodeMeta!.Parameters["vsa.uhr.von"]);
        Assert.Equal("5.0", draft.Entry.CodeMeta.Parameters["vsa.uhr.bis"]);
        Assert.Equal("12.3", draft.Entry.CodeMeta.Parameters["vsa.q1"]);
        Assert.Equal("8.7", draft.Entry.CodeMeta.Parameters["vsa.q2"]);
        Assert.Equal("42.2", draft.Entry.CodeMeta.Parameters["vsa.querschnitt.prozent"]);
    }
}
