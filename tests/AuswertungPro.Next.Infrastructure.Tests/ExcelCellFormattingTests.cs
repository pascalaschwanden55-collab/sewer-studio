using AuswertungPro.Next.Infrastructure.Export.Excel;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer ExcelCellFormatting (reine string/double-Logik, kein ClosedXML).
/// </summary>
public sealed class ExcelCellFormattingTests
{
    // ----- TryParseExcelNumber -----

    [Theory]
    [InlineData("1234.56", 1234.56)]
    [InlineData("1'234.56", 1234.56)]   // CH-Tausender-Apostroph
    [InlineData("1.234,56", 1234.56)]   // DE-Format
    [InlineData("1234,56", 1234.56)]    // DE ohne Tausender
    [InlineData("0", 0.0)]
    [InlineData("42", 42.0)]
    [InlineData("-3.14", -3.14)]
    [InlineData("+100", 100.0)]
    [InlineData("1 234.56", 1234.56)] // geschuetztes Leerzeichen als Tausendertrenner
    public void TryParseExcelNumber_GueltigeZahlen(string input, double expected)
    {
        var ok = ExcelCellFormatting.TryParseExcelNumber(input, out var result);

        Assert.True(ok, $"Parsing von '{input}' soll erfolgreich sein");
        Assert.Equal(expected, result, 3);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    [InlineData("12.34.56")]
    public void TryParseExcelNumber_UngueltigeEingaben(string? input)
    {
        var ok = ExcelCellFormatting.TryParseExcelNumber(input, out _);

        Assert.False(ok, $"Parsing von '{input ?? "(null)"}' soll fehlschlagen");
    }

    // ----- NormalizeHeader -----

    [Theory]
    [InlineData("Haltungsname", "haltungsname")]
    [InlineData("Haltungslänge m", "haltungslaenge m")]
    [InlineData("HaltungslÃ¤nge m", "haltungslaenge m")]  // Mojibake fuer ä
    [InlineData("Ãœbersicht", "uebersicht")]              // Mojibake fuer Ü
    [InlineData("Ã–ffnung", "oeffnung")]                  // Mojibake fuer Ö
    [InlineData("straße", "strasse")]
    [InlineData("STRASSE", "strasse")]
    [InlineData("  DN mm  ", "dn mm")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void NormalizeHeader_NormiertUmlauteUndMojibake(string? input, string expected)
    {
        var result = ExcelCellFormatting.NormalizeHeader(input);

        Assert.Equal(expected, result);
    }
}
