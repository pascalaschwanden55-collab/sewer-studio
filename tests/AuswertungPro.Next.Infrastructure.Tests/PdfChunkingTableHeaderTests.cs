using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class PdfChunkingTableHeaderTests
{
    [Fact]
    public void SplitIntoHaltungChunks_DoesNotUseDatumHeaderAsId()
    {
        var page = string.Join("\n", new[]
        {
            "Kanalfernsehprotokoll / Inspektion: 1",
            "Haltungsname:                Datum :                Wetter :               Operator :",
            " 23021-22369                22.04.2014          schoen_trocken           Manuel Joschko"
        });

        var chunks = PdfChunking.SplitIntoHaltungChunks(new[] { page }, new PdfParser());

        Assert.Single(chunks);
        Assert.Equal("23021-22369", chunks[0].DetectedId);
        Assert.False(chunks[0].IsUncertain);
    }

    [Fact]
    public void SplitIntoHaltungChunks_UsesKnotenProtocolAsAnchorForPhotoPages()
    {
        var pages = new[]
        {
            "Projektinformationen / Inspektion: 1",
            string.Join("\n", new[]
            {
                "Kanalfernsehprotokoll / Inspektion: 1",
                "Datum : 06.11.2017",
                "Knoten oben : 21405",
                "Knoten unten: 23021"
            }),
            string.Join("\n", new[]
            {
                "Kanalfernsehfotos / Inspektion: 1",
                "Haltungsbezeichnung: 21405"
            }),
            string.Join("\n", new[]
            {
                "Kanalfernsehfotos / Inspektion: 1",
                "Haltungsbezeichnung: 21405"
            }),
            string.Join("\n", new[]
            {
                "Kanalfernsehprotokoll / Inspektion: 1",
                "Datum : 06.11.2017",
                "Knoten oben : 22369",
                "Knoten unten: 23022"
            }),
            string.Join("\n", new[]
            {
                "Kanalfernsehfotos / Inspektion: 1",
                "Haltungsbezeichnung: 22369"
            })
        };

        var chunks = PdfChunking.SplitIntoHaltungChunks(pages, new PdfParser());

        Assert.Equal(3, chunks.Count);
        Assert.Null(chunks[0].DetectedId);
        Assert.Equal(new[] { 1 }, chunks[0].Pages);
        Assert.Equal("21405-23021", chunks[1].DetectedId);
        Assert.Equal(new[] { 2, 3, 4 }, chunks[1].Pages);
        Assert.Equal("22369-23022", chunks[2].DetectedId);
        Assert.Equal(new[] { 5, 6 }, chunks[2].Pages);
    }
}
