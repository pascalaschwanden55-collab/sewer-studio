using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Tests.Export;

/// <summary>
/// Formelzellen in CSV-Ausgaben (Gesamtaudit 2026-08-14, Prio 2).
/// Excel fuehrt eine Zelle aus, die mit = + - @ beginnt. Unsere Exporte enthalten
/// Freitext aus Kundenprotokollen.
/// </summary>
public sealed class CsvCellTests
{
    [Theory]
    [InlineData("=1+1")]
    [InlineData("=HYPERLINK(\"http://boese\";\"klicken\")")]
    [InlineData("+42")]
    [InlineData("@SUM(A1)")]
    [InlineData("\tTabulator")]
    public void Formelanfaenge_werden_entschaerft(string wert)
    {
        var ergebnis = CsvCell.Neutralize(wert);

        Assert.StartsWith("'", ergebnis);
        // Der sichtbare Inhalt bleibt vollstaendig erhalten
        Assert.EndsWith(wert, ergebnis);
    }

    [Theory]
    [InlineData("-12,5")]
    [InlineData("-3")]
    [InlineData("-0.75")]
    public void Negative_Zahlen_bleiben_Zahlen(string wert)
        => Assert.Equal(wert, CsvCell.Neutralize(wert));

    [Theory]
    [InlineData("-SUMME(A1)")]
    [InlineData("-cmd|' /c calc'!A1")]
    public void Ein_Minus_vor_Text_ist_eine_Formel(string wert)
        => Assert.StartsWith("'", CsvCell.Neutralize(wert));

    [Theory]
    [InlineData("BAB Riss laengs")]
    [InlineData("300")]
    [InlineData("")]
    [InlineData("Haltung 1-2")]
    public void Normale_Werte_bleiben_unveraendert(string wert)
        => Assert.Equal(wert, CsvCell.Neutralize(wert));

    [Fact]
    public void Trennzeichen_werden_weiterhin_maskiert()
    {
        Assert.Equal("\"a;b\"", CsvCell.Escape("a;b"));
        Assert.Equal("\"sagt \"\"hallo\"\"\"", CsvCell.Escape("sagt \"hallo\""));
        Assert.Equal("\"zwei\nZeilen\"", CsvCell.Escape("zwei\nZeilen"));
    }

    [Fact]
    public void Formel_und_Trennzeichen_zusammen()
    {
        // Erst entschaerfen, dann maskieren - beides muss greifen.
        var ergebnis = CsvCell.Escape("=A1;B2");

        Assert.Equal("\"'=A1;B2\"", ergebnis);
    }

    [Fact]
    public void Komma_als_Trennzeichen_wird_beruecksichtigt()
    {
        Assert.Equal("\"a,b\"", CsvCell.Escape("a,b", separator: ','));
        Assert.Equal("a;b", CsvCell.Escape("a;b", separator: ','));
    }

    [Fact]
    public void Null_wird_zur_leeren_Zelle()
    {
        Assert.Equal(string.Empty, CsvCell.Escape(null));
        Assert.Equal(string.Empty, CsvCell.Neutralize(null));
    }
}
