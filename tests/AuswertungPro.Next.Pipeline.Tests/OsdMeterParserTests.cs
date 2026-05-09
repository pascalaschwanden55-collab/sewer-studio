using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Unit-Tests fuer OsdMeterParser. Spiegelt das Verhalten der
// frueheren inline-Logik in PlayerWindow.CodingMode.CodingReadOsdMeterAsync
// 1:1 — ohne Verbesserungen, ohne Verhaltensaenderungen. Slice 8a.3
// Step 1a.
public class OsdMeterParserTests
{
    private readonly OsdMeterParser _parser = new();

    // --- Happy path ---

    [Theory]
    [InlineData("7.90", 7.90)]
    [InlineData("0.00", 0.00)]
    [InlineData("0", 0.0)]
    [InlineData("14.98", 14.98)]
    [InlineData("123", 123.0)]
    [InlineData("123.45", 123.45)]
    public void TryParse_AcceptableNumber_ReturnsValue(string input, double expected)
    {
        var result = _parser.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 4);
    }

    // --- Komma-Normalisierung (deutsche Locale) ---

    [Theory]
    [InlineData("7,90", 7.90)]
    [InlineData("0,00", 0.00)]
    [InlineData("123,45", 123.45)]
    public void TryParse_CommaDecimal_NormalizesToPoint(string input, double expected)
    {
        var result = _parser.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 4);
    }

    // --- Whitespace + Trim ---

    [Theory]
    [InlineData("  7.90  ", 7.90)]
    [InlineData("\t14.98\n", 14.98)]
    public void TryParse_LeadingTrailingWhitespace_TrimsBeforeParse(string input, double expected)
    {
        var result = _parser.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 4);
    }

    // --- Eingebettete Zahl (LLM antwortet manchmal mit Praefix) ---

    [Theory]
    [InlineData("Meter: 14.98", 14.98)]
    [InlineData("OSD = 7.90 m", 7.90)]
    [InlineData("Antwort: 0.00", 0.00)]
    public void TryParse_NumberWithSurroundingText_ExtractsFirstMatch(string input, double expected)
    {
        var result = _parser.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 4);
    }

    // --- Leer / null / non-numeric ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("\t\n")]
    [InlineData("abc")]
    [InlineData("kein wert")]
    [InlineData("---")]
    public void TryParse_EmptyOrNonNumeric_ReturnsNull(string? input)
    {
        var result = _parser.TryParse(input);
        Assert.Null(result);
    }

    // --- Plausibilitaets-Range 0..500 ---

    [Theory]
    [InlineData("500", 500.0)]
    [InlineData("500.00", 500.0)]
    [InlineData("499.99", 499.99)]
    public void TryParse_BoundaryWithinRange_Accepted(string input, double expected)
    {
        var result = _parser.TryParse(input);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 4);
    }

    [Theory]
    [InlineData("501")]
    [InlineData("999")]
    [InlineData("99999")]   // Knotennummer-Format
    public void TryParse_NumberOutOfRange_ReturnsNull(string input)
    {
        var result = _parser.TryParse(input);
        Assert.Null(result);
    }

    // --- Knotennummer-Edge-Case ---
    //
    // Wenn Vision aus einem unklaren Frame gemischten Text liefert
    // ("Knoten 99999, Meter 7.90"), greift der Regex auf den ERSTEN
    // Treffer "999" (3 Ziffern aus "99999"), und der faellt durch die
    // Range-Pruefung raus. Das ist die heutige inline-Logik 1:1 — wir
    // verbessern sie in 1a NICHT, sondern dokumentieren sie nur als Test.

    [Fact]
    public void TryParse_KnotenNumberFollowedByMeter_FirstMatchOutOfRange()
    {
        var result = _parser.TryParse("Knoten 99999, Meter 7.90");
        Assert.Null(result);
    }

    // --- Regex-Greedy-Eigenheit ---
    //
    // "1234.56" → Regex matcht "123" (3 Ziffern, kein Punkt danach
    // weil "4" folgt), Range 0..500 → 123.0. Auch das ist heutiges
    // Verhalten und wird hier nur dokumentiert.

    [Fact]
    public void TryParse_FourDigitNumber_MatchesFirstThreeDigits()
    {
        var result = _parser.TryParse("1234.56");
        Assert.NotNull(result);
        Assert.Equal(123.0, result!.Value, precision: 4);
    }
}
