using AuswertungPro.Next.Domain.Protocol;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingProtocolEntryCopierTests
{
    [Fact]
    public void CopyValues_copies_protocol_fields_and_clones_photo_list()
    {
        var meta = new ProtocolEntryCodeMeta { Code = "BAB" };
        meta.Parameters["p"] = "v";
        var ai = new ProtocolEntryAiMeta { SuggestedCode = "BAB", Confidence = 0.91 };
        var source = new ProtocolEntry
        {
            Code = "BAB",
            Beschreibung = "Riss",
            MeterStart = 1.2,
            MeterEnd = 3.4,
            IsStreckenschaden = true,
            Mpeg = "film.mp4",
            Zeit = TimeSpan.FromSeconds(12),
            Source = ProtocolEntrySource.Ai,
            CodeMeta = meta,
            Ai = ai,
            FotoPaths = ["a.png"]
        };
        var target = new ProtocolEntry { Code = "OLD", FotoPaths = ["old.png"] };

        CodingProtocolEntryCopier.CopyValues(source, target);
        source.FotoPaths.Add("late.png");

        Assert.Equal("BAB", target.Code);
        Assert.Equal("Riss", target.Beschreibung);
        Assert.Equal(1.2, target.MeterStart);
        Assert.Equal(3.4, target.MeterEnd);
        Assert.True(target.IsStreckenschaden);
        Assert.Equal("film.mp4", target.Mpeg);
        Assert.Equal(TimeSpan.FromSeconds(12), target.Zeit);
        Assert.Equal(ProtocolEntrySource.Ai, target.Source);
        Assert.Same(meta, target.CodeMeta);
        Assert.Same(ai, target.Ai);
        Assert.Equal(["a.png"], target.FotoPaths);
    }

    [Fact]
    public void CopyEditableValues_keeps_identity_and_ai_fields()
    {
        var source = new ProtocolEntry
        {
            Code = "BCA",
            Beschreibung = "Anschluss",
            MeterStart = 2.5,
            MeterEnd = 2.7,
            IsStreckenschaden = true,
            Mpeg = "source-film.mp4",
            Zeit = TimeSpan.FromSeconds(23),
            Source = ProtocolEntrySource.Manual,
            CodeMeta = new ProtocolEntryCodeMeta { Code = "BCA" },
            Ai = new ProtocolEntryAiMeta { SuggestedCode = "BCA" },
            FotoPaths = ["new.png"]
        };
        var originalAi = new ProtocolEntryAiMeta { SuggestedCode = "OLD" };
        var target = new ProtocolEntry
        {
            Code = "OLD",
            Mpeg = "target-film.mp4",
            Source = ProtocolEntrySource.Ai,
            Ai = originalAi,
            FotoPaths = ["old.png"]
        };

        CodingProtocolEntryCopier.CopyEditableValues(source, target);
        source.FotoPaths.Add("late.png");

        Assert.Equal("BCA", target.Code);
        Assert.Equal("Anschluss", target.Beschreibung);
        Assert.Equal(2.5, target.MeterStart);
        Assert.Equal(2.7, target.MeterEnd);
        Assert.True(target.IsStreckenschaden);
        Assert.Equal(TimeSpan.FromSeconds(23), target.Zeit);
        Assert.Equal(["new.png"], target.FotoPaths);
        Assert.Equal("target-film.mp4", target.Mpeg);
        Assert.Equal(ProtocolEntrySource.Ai, target.Source);
        Assert.Same(originalAi, target.Ai);
    }
}
