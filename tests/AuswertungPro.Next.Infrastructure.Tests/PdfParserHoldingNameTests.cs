using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfParserHoldingNameTests
{
    [Fact]
    public void ParseFields_TableHeaderWithDatum_DetectsCorrectHaltungsname()
    {
        var text = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko",
            "Schacht oben: 23021",
            "Schacht unten: 22369"
        });

        var parser = new PdfParser();
        var fields = parser.ParseFields(text);

        Assert.True(fields.TryGetValue("Haltungsname", out var id));
        Assert.Equal("23021-22369", id);
    }

    [Fact]
    public void ParseFields_PhantomHoldingNameWithRepeatedDigitRuns_IsRejected()
    {
        const string phantom = "29120-000000044444449999999";
        var text = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            $"Haltungsname: {phantom}",
            "Datum: 30.06.2025",
            "Nutzungsart: Mischwasser"
        });

        var parser = new PdfParser();
        var fields = parser.ParseFields(text);

        Assert.False(fields.TryGetValue("Haltungsname", out var id), id);
    }

    [Fact]
    public void GetHaltungKeyFromChunk_PhantomHoldingNameWithRepeatedDigitRuns_IsRejected()
    {
        const string phantom = "29120-000000044444449999999";
        var text = string.Join("\n", new[]
        {
            $"Haltungsname: {phantom}",
            "Datum: 30.06.2025",
            "Nutzungsart: Mischwasser"
        });

        var key = PdfChunking.GetHaltungKeyFromChunk(text, new PdfParser());

        Assert.Null(key);
    }

    [Fact]
    public void TryExtractHoldingIdFromFileName_DatedPdfNamePrefersEmbeddedDashPair()
    {
        var id = LegacyPdfImportService.TryExtractHoldingIdFromFileName(
            @"D:\Haltungen\29120-03.27666\20250630_29120-03.27666.pdf");

        Assert.Equal("29120-03.27666", id);
    }
}

