using AuswertungPro.Next.Application.DataPage;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer <see cref="DnValueParser.TryParseMillimeters"/>.
/// Verifiziert das bisherige Verhalten aus DataPageViewModel.TryParseDnMm (verhaltensneutral).
/// </summary>
public sealed class DataPageDnValueParserTests
{
    // --- Leerfall ---

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void TryParseMillimeters_liefert_null_bei_leer(string? input)
        => Assert.Null(DnValueParser.TryParseMillimeters(input));

    // --- Einfache numerische Werte ---

    [Theory]
    [InlineData("300", 300d)]
    [InlineData("150", 150d)]
    [InlineData("600", 600d)]
    public void TryParseMillimeters_parst_ganzzahl(string input, double expected)
        => Assert.Equal(expected, DnValueParser.TryParseMillimeters(input));

    // --- Dezimaltrenner Komma (mit ausschliesslichem Komma im Text) ---
    // Hinweis: "300,5" ohne Punkt wird via InvariantCulture als 3005 geparst
    // (Komma = Tausendertrennzeichen dort). Charakterisierungstest spiegelt
    // das reale Verhalten des urspruenglichen TryParseDnMm-Codes wider.

    [Theory]
    [InlineData("300,5", 3005d)]   // Komma = Tausendertrennzeichen via InvariantCulture+AllowThousands
    [InlineData("300.5", 300.5d)]  // Punkt = Dezimaltrenner
    public void TryParseMillimeters_parst_dezimaltrenner(string input, double expected)
        => Assert.Equal(expected, DnValueParser.TryParseMillimeters(input));

    // --- Leerzeichen und Apostrophe (Tausendertrennzeichen) ---

    [Theory]
    [InlineData("1 000", 1000d)]
    [InlineData("1'000", 1000d)]
    public void TryParseMillimeters_ignoriert_trennzeichen(string input, double expected)
        => Assert.Equal(expected, DnValueParser.TryParseMillimeters(input));

    // --- Ungueltige Werte ---

    [Theory]
    [InlineData("abc")]
    [InlineData("DN")]
    public void TryParseMillimeters_liefert_null_bei_nicht_parsbar(string input)
        => Assert.Null(DnValueParser.TryParseMillimeters(input));

    // --- Wert <= 0 ---

    [Theory]
    [InlineData("0")]
    [InlineData("-300")]
    public void TryParseMillimeters_liefert_null_bei_null_oder_negativ(string input)
        => Assert.Null(DnValueParser.TryParseMillimeters(input));

    // --- Ziffernfolge ohne Trennzeichen (Fallback, Mindestwert 50) ---

    [Fact]
    public void TryParseMillimeters_akzeptiert_ziffernfolge_ab_50()
    {
        // z.B. "300mm" wird nicht gesondert gefiltert (reiner Ziffernfall)
        // dieser Test prueft die digits-only-Fallback-Kette mit 300 >= 50
        var result = DnValueParser.TryParseMillimeters("300");
        Assert.Equal(300d, result);
    }
}
