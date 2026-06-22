using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStreckenschadenEventFactoryTests
{
    [Fact]
    public void CreateOpen_builds_open_streckenschaden_entry()
    {
        var videoTime = TimeSpan.FromSeconds(18);

        var draft = CodingStreckenschadenEventFactory.CreateOpen(
            "BAJ",
            "Laengsriss",
            startMeter: 6.7,
            videoTime);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BAJ", draft.Entry.Code);
        Assert.Equal("Laengsriss", draft.Entry.Beschreibung);
        Assert.Equal(6.7, draft.Entry.MeterStart);
        Assert.Null(draft.Entry.MeterEnd);
        Assert.True(draft.Entry.IsStreckenschaden);
        Assert.Equal(videoTime, draft.Entry.Zeit);
    }

    [Fact]
    public void CreateOpen_builds_unconfirmed_ai_context()
    {
        var draft = CodingStreckenschadenEventFactory.CreateOpen(
            "BAG",
            label: null,
            startMeter: 1.0,
            videoTime: TimeSpan.Zero);

        Assert.Equal("BAG", draft.Entry.Beschreibung);
        Assert.Equal("BAG", draft.AiContext.SuggestedCode);
        Assert.Equal(0.0, draft.AiContext.Confidence);
        Assert.Equal("Streckenschaden-Anfang (automatisch) - noch offen", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void CloseStart_marks_start_entry_and_builds_end_marker()
    {
        var codeMeta = new ProtocolEntryCodeMeta { Code = "BAJ" };
        var start = new ProtocolEntry
        {
            Code = "BAJ",
            Beschreibung = "Laengsriss",
            MeterStart = 2.0,
            Source = ProtocolEntrySource.Ai,
            CodeMeta = codeMeta
        };

        var endEntry = CodingStreckenschadenEventFactory.CloseStart(start, endMeter: 8.5);

        Assert.True(start.IsStreckenschaden);
        Assert.Equal(8.5, start.MeterEnd);
        Assert.Equal("BAJ", endEntry.Code);
        Assert.Equal("Laengsriss (Ende)", endEntry.Beschreibung);
        Assert.Equal(8.5, endEntry.MeterStart);
        Assert.True(endEntry.IsStreckenschaden);
        Assert.Equal(ProtocolEntrySource.Ai, endEntry.Source);
        Assert.Same(codeMeta, endEntry.CodeMeta);
    }
}
