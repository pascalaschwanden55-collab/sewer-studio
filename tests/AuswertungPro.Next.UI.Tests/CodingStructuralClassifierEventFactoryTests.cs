using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingStructuralClassifierEventFactoryTests
{
    [Fact]
    public void Create_builds_protocol_entry_and_ai_context()
    {
        var videoTime = TimeSpan.FromSeconds(7);

        var draft = CodingStructuralClassifierEventFactory.Create(
            "BCA",
            "Anschluss",
            "Anschluss",
            classifierConfidence: 0.91,
            meter: 3.4,
            videoTime,
            meterFromOsd: true);

        Assert.Equal(ProtocolEntrySource.Ai, draft.Entry.Source);
        Assert.Equal("BCA", draft.Entry.Code);
        Assert.Equal("Anschluss", draft.Entry.Beschreibung);
        Assert.Equal(3.4, draft.Entry.MeterStart);
        Assert.Equal(videoTime, draft.Entry.Zeit);
        Assert.Null(draft.Entry.CodeMeta);
        Assert.Equal("BCA", draft.AiContext.SuggestedCode);
        Assert.Equal(0.91, draft.AiContext.Confidence);
        Assert.Equal("Anschluss (Klassifikator, ohne DINO/SAM-Box)", draft.AiContext.Reason);
        Assert.Equal(CodingUserDecision.Ignored, draft.AiContext.Decision);
    }

    [Fact]
    public void Create_marks_estimated_meter_when_meter_is_not_from_osd()
    {
        var draft = CodingStructuralClassifierEventFactory.Create(
            "BCC",
            "Bogen",
            "Bogen",
            classifierConfidence: null,
            meter: 9.0,
            videoTime: TimeSpan.Zero,
            meterFromOsd: false);

        Assert.Equal(0.0, draft.AiContext.Confidence);
        Assert.Equal("geschaetzt", draft.Entry.CodeMeta!.Parameters["vsa.meter.quelle"]);
    }
}
