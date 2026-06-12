using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfProtocolExtractorFontEncodingTests
{
    [Fact]
    public void LooksLikeUndecodableFontEncoding_DetectsControlHeavyTextWithoutAnchors()
    {
        var text = string.Concat(Enumerable.Repeat("\u0001\u0002\u0003ABC", 40));

        Assert.True(PdfProtocolExtractor.LooksLikeUndecodableFontEncoding(text));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_KeepsNormalProtocolText()
    {
        var text = string.Join("\n", new[]
        {
            "Haltung 45346-3.45651",
            "Zustand BCD",
            "Entf. in Fließr. 0.00 m",
            "Video 00:00:16"
        });

        Assert.False(PdfProtocolExtractor.LooksLikeUndecodableFontEncoding(text));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_DoesNotFlagPlainUnknownText()
    {
        var text = string.Concat(Enumerable.Repeat("Dies ist lesbarer Text ohne VSA-Anker. ", 20));

        Assert.False(PdfProtocolExtractor.LooksLikeUndecodableFontEncoding(text));
    }
}
