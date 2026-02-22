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
}

