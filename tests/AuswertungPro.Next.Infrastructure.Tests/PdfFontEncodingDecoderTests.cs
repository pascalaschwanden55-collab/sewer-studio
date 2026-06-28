// Charakterisierungs-Tests fuer PdfFontEncodingDecoder
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfFontEncodingDecoderTests
{
    // ── LooksLikeUndecodableFontEncoding ────────────────────────────────────

    [Fact]
    public void LooksLikeUndecodableFontEncoding_ErkenntSteuerzeichenSchwerlastText()
    {
        // Viele Steuerzeichen (>= 25 %) ohne bekannte Textanker => true
        var text = string.Concat(Enumerable.Repeat("ABC", 40));

        Assert.True(PdfFontEncodingDecoder.LooksLikeUndecodableFontEncoding(text));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_AkzeptiertNormalenProtokolltext()
    {
        var text = string.Join("\n", new[]
        {
            "Haltung 45346-3.45651",
            "Zustand BCD",
            "Entf. in Fließr. 0.00 m",
            "Video 00:00:16"
        });

        Assert.False(PdfFontEncodingDecoder.LooksLikeUndecodableFontEncoding(text));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_FlaggtKeinenLesbaren_NichtAnkerText()
    {
        var text = string.Concat(Enumerable.Repeat("Dies ist lesbarer Text ohne VSA-Anker. ", 20));

        Assert.False(PdfFontEncodingDecoder.LooksLikeUndecodableFontEncoding(text));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_GibtFalseZurueck_BeiNull()
    {
        Assert.False(PdfFontEncodingDecoder.LooksLikeUndecodableFontEncoding(null));
    }

    [Fact]
    public void LooksLikeUndecodableFontEncoding_GibtFalseZurueck_BeiKurzText()
    {
        // Weniger als 80 Nicht-Leerzeichen => immer false
        var text = string.Concat(Enumerable.Repeat("", 20));

        Assert.False(PdfFontEncodingDecoder.LooksLikeUndecodableFontEncoding(text));
    }

    // ── CountWordMatches ────────────────────────────────────────────────────

    [Fact]
    public void CountWordMatches_ZaehltTrefferCaseInsensitive()
    {
        var count = PdfFontEncodingDecoder.CountWordMatches(
            "Leitung ABC Video def Foto ghi",
            new[] { "Leitung", "Video", "Foto", "Schacht" });

        Assert.Equal(3, count);
    }

    [Fact]
    public void CountWordMatches_GibtNull_BeiLeeremText()
    {
        var count = PdfFontEncodingDecoder.CountWordMatches("", new[] { "Leitung" });
        Assert.Equal(0, count);
    }

    // ── ShiftAllChars ───────────────────────────────────────────────────────

    [Fact]
    public void ShiftAllChars_VerschiebtNurDruckbareZeichen()
    {
        // Leerzeichen, Tab, CR, LF bleiben unveraendert; andere Zeichen werden um Offset verschoben
        var result = PdfFontEncodingDecoder.ShiftAllChars("A B\tC\r\nD", 1);

        Assert.Equal("B C\tD\r\nE", result);
    }

    [Fact]
    public void ShiftAllChars_ShiftNull_GibtUnveraendertZurueck()
    {
        var result = PdfFontEncodingDecoder.ShiftAllChars("Haltung", 0);
        Assert.Equal("Haltung", result);
    }

    // ── IsSuspiciousDecodedChar ─────────────────────────────────────────────

    [Fact]
    public void IsSuspiciousDecodedChar_ErkenntSteuerzeichen()
    {
        Assert.True(PdfFontEncodingDecoder.IsSuspiciousDecodedChar(''));
    }

    [Fact]
    public void IsSuspiciousDecodedChar_AkzeptiertNormaleBuchstaben()
    {
        Assert.False(PdfFontEncodingDecoder.IsSuspiciousDecodedChar('A'));
        Assert.False(PdfFontEncodingDecoder.IsSuspiciousDecodedChar('Z'));
        Assert.False(PdfFontEncodingDecoder.IsSuspiciousDecodedChar('ä'));
    }
}
