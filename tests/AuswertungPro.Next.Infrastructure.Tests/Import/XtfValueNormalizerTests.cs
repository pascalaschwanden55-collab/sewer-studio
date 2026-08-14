using System;
using AuswertungPro.Next.Infrastructure.Import.Xtf;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

/// <summary>
/// Charakterisierungstests fuer XtfValueNormalizer.
/// Deckt das IST-Verhalten der Normalisierungs-Hilfsmethoden ab,
/// bevor sie aus LegacyXtfImportService extrahiert wurden.
///
/// ACHTUNG (2026-07-17): Die Material-Erwartungen wurden bewusst geaendert. Die alten Ausgaben
/// ("Kunststoff PVC", "Kunststoff PE", "Kunststoff PE-HD") standen nie in der Auswahlliste des
/// Feldes Rohrmaterial — das Programm zeigte sie darum als leer an, obwohl der Wert gespeichert
/// war (99 von 330 Haltungen ueber alle Projekte). Der Test hatte das nur festgeschrieben, nicht
/// geprueft: Er sicherte eine Extraktion ab, nicht die Richtigkeit. Die neuen Erwartungen sind
/// gegen FieldCatalog geprueft — siehe XtfMaterialNormalizerTests.
/// </summary>
public sealed class XtfValueNormalizerTests
{
    // ===================== NormalizeSiaMaterial =====================

    [Theory]
    [InlineData("Kunststoff_Hartpolyethylen", "Hartpolyethylen")]
    [InlineData("kunststoff_hartpolyethylen", "Hartpolyethylen")]
    [InlineData("Kunststoff_Polyethylen", "Polyethylen")]
    [InlineData("Kunststoff_Polyvinylchlorid", "Polyvinylchlorid")]
    [InlineData("Beton_Normalbeton", "Beton")]
    [InlineData("Beton_Stahlbeton", "Beton")]
    [InlineData("Steinzeug", "Steinzeug")]
    [InlineData("steinzeug", "Steinzeug")]
    [InlineData("", "")]
    [InlineData(null, "")]
    public void NormalizeSiaMaterial_ReturnsExpected(string? input, string expected)
    {
        var result = XtfValueNormalizer.NormalizeSiaMaterial(input!);
        Assert.Equal(expected, result);
    }

    [Fact]
    public void NormalizeSiaMaterial_UnknownCode_CapitalizesFirst()
    {
        // Unbekannte Codes: Unterstriche ersetzen, erstes Zeichen grossschreiben.
        // Beispiel bewusst gewechselt: "guss_eisen" trifft heute die Guss-Regel und ist damit
        // kein unbekannter Code mehr.
        var result = XtfValueNormalizer.NormalizeSiaMaterial("irgendwas_neues");
        Assert.Equal("Irgendwas neues", result);
    }

    [Fact]
    public void NormalizeSiaMaterial_GussEisen_MapsToSelectableGuss()
    {
        Assert.Equal("Guss", XtfValueNormalizer.NormalizeSiaMaterial("guss_eisen"));
    }

    // ===================== NormalizeNutzungsart =====================

    [Theory]
    [InlineData("Schmutzabwasser", "Schmutzabwasser")]
    [InlineData("schmutzabwasser", "Schmutzabwasser")]
    [InlineData("Regenabwasser", "Niederschlagsabwasser")]
    [InlineData("REGENABWASSER", "Niederschlagsabwasser")]
    [InlineData("Mischabwasser", "Mischabwasser")]
    [InlineData("MISCHABWASSER", "Mischabwasser")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("  unbekannt  ", "unbekannt")]
    public void NormalizeNutzungsart_ReturnsExpected(string? input, string expected)
    {
        var result = XtfValueNormalizer.NormalizeNutzungsart(input!);
        Assert.Equal(expected, result);
    }

    // ===================== NormalizeDate_yyyymmdd =====================

    [Theory]
    [InlineData("20250103", "03.01.2025")]
    [InlineData("20140422", "22.04.2014")]
    [InlineData("19991231", "31.12.1999")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("2025-01-03", "2025-01-03")] // nicht yyyymmdd-Format -> unveraendert zurueck
    [InlineData("  20250103  ", "03.01.2025")] // Leerzeichen werden getrimmt
    public void NormalizeDate_yyyymmdd_ReturnsExpected(string? input, string expected)
    {
        var result = XtfValueNormalizer.NormalizeDate_yyyymmdd(input);
        Assert.Equal(expected, result);
    }

    // ===================== TryParseDouble =====================

    [Theory]
    [InlineData("22.5", true, 22.5)]
    [InlineData("22,5", true, 22.5)]
    [InlineData("0", true, 0.0)]
    [InlineData("-1.23", true, -1.23)]
    [InlineData("abc123def", true, 123.0)] // Regex-Fallback extrahiert Zahl
    [InlineData("", false, 0.0)]
    [InlineData(null, false, 0.0)]
    [InlineData("abc", false, 0.0)]
    public void TryParseDouble_ReturnsExpected(string? input, bool expectedSuccess, double expectedValue)
    {
        var success = XtfValueNormalizer.TryParseDouble(input, out var value);
        Assert.Equal(expectedSuccess, success);
        if (expectedSuccess)
            Assert.Equal(expectedValue, value, precision: 6);
    }

    // ===================== NormalizeCode =====================

    [Theory]
    [InlineData("BAB", "BAB")]
    [InlineData("bab", "BAB")]
    [InlineData("BA-B", "BAB")]
    [InlineData("BA B", "BAB")]
    [InlineData("", "")]
    [InlineData(null, "")]
    [InlineData("  BCD  ", "BCD")]
    public void NormalizeCode_ReturnsExpected(string? input, string expected)
    {
        var result = XtfValueNormalizer.NormalizeCode(input);
        Assert.Equal(expected, result);
    }

    // ===================== GetCodeSimilarityRank =====================

    [Theory]
    [InlineData("BAB", "BAB", 0)]   // exakt gleich
    [InlineData("BAB", "bab", 0)]   // case-insensitiv
    [InlineData("BAB", "BAB1", 1)]  // Praefix-Match
    [InlineData("BAB1", "BAB", 1)]  // Praefix-Match umgekehrt
    [InlineData("BAB", "BCD", 2)]   // keine Uebereinstimmung
    [InlineData("", "BAB", 2)]      // leer
    [InlineData("BAB", "", 2)]      // leer
    public void GetCodeSimilarityRank_ReturnsExpected(string left, string right, int expected)
    {
        var result = XtfValueNormalizer.GetCodeSimilarityRank(left, right);
        Assert.Equal(expected, result);
    }

    // ===================== ParseMpegTime =====================

    [Theory]
    [InlineData("01:23:45", 1, 23, 45, 0)]
    [InlineData("23:45", 0, 23, 45, 0)]
    [InlineData("1:02:03", 1, 2, 3, 0)]
    [InlineData("5:06", 0, 5, 6, 0)]
    [InlineData("01:23:45.123", 1, 23, 45, 123)]
    [InlineData("", 0, 0, 0, -1)]   // ergibt null
    [InlineData(null, 0, 0, 0, -1)] // ergibt null
    [InlineData("abc", 0, 0, 0, -1)] // kein Zeitformat -> null
    public void ParseMpegTime_ReturnsExpected(string? input, int h, int m, int s, int ms)
    {
        var result = XtfValueNormalizer.ParseMpegTime(input);
        if (ms == -1)
        {
            Assert.Null(result);
        }
        else
        {
            Assert.NotNull(result);
            Assert.Equal(h, result!.Value.Hours);
            Assert.Equal(m, result.Value.Minutes);
            Assert.Equal(s, result.Value.Seconds);
            Assert.Equal(ms, result.Value.Milliseconds);
        }
    }
}
