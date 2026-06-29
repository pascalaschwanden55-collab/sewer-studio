using AuswertungPro.Next.Application.Protocol;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer ProtocolEntryInputNormalizer (IST-Verhalten).
/// Deckt alle oeffentlichen Methoden ab.
/// </summary>
public sealed class ProtocolEntryInputNormalizerTests
{
    // ── NormalizeCode ─────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeCode_leerstring_gibt_leer_zurueck()
        => Assert.Equal(string.Empty, ProtocolEntryInputNormalizer.NormalizeCode(string.Empty));

    [Fact]
    public void NormalizeCode_nur_leerzeichen_gibt_leer_zurueck()
        => Assert.Equal(string.Empty, ProtocolEntryInputNormalizer.NormalizeCode("   "));

    [Theory]
    [InlineData("bab",   "BAB")]
    [InlineData("Bab",   "BAB")]
    [InlineData("BAB",   "BAB")]
    [InlineData("ba b",  "BAB")]  // Leerzeichen entfernt
    [InlineData(" BAB ", "BAB")]  // Rand-Leerzeichen entfernt
    public void NormalizeCode_normiert_auf_grossbuchstaben_ohne_leerzeichen(string input, string expected)
        => Assert.Equal(expected, ProtocolEntryInputNormalizer.NormalizeCode(input));

    // ── TryParseOptionalDouble ────────────────────────────────────────────────

    [Fact]
    public void TryParseOptionalDouble_leer_ergibt_null_und_true()
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble(string.Empty, out var value);
        Assert.True(ok);
        Assert.Null(value);
    }

    [Fact]
    public void TryParseOptionalDouble_leerzeichen_ergibt_null_und_true()
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble("   ", out var value);
        Assert.True(ok);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("1.5",  1.5)]
    [InlineData("1,5",  1.5)]   // Komma als Dezimaltrenner
    [InlineData("0",    0.0)]
    [InlineData("123",  123.0)]
    [InlineData("-5.5", -5.5)]
    public void TryParseOptionalDouble_gueltige_werte_werden_geparst(string input, double expected)
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble(input, out var value);
        Assert.True(ok);
        Assert.Equal(expected, value);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("1.2.3")]
    [InlineData("--5")]
    public void TryParseOptionalDouble_ungueltige_werte_ergeben_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalDouble(input, out _);
        Assert.False(ok);
    }

    // ── TryParseOptionalTimeSpan ──────────────────────────────────────────────

    [Fact]
    public void TryParseOptionalTimeSpan_leer_ergibt_null_und_true()
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(string.Empty, out var value);
        Assert.True(ok);
        Assert.Null(value);
    }

    [Theory]
    [InlineData("01:30",    0, 1,  30)]   // mm:ss -> 1m 30s
    [InlineData("00:00:05", 0, 0,  5)]    // hh:mm:ss -> 0h 0m 5s
    [InlineData("1:02:03",  1, 2,  3)]    // h:mm:ss -> 1h 2m 3s
    public void TryParseOptionalTimeSpan_gueltige_formate_werden_geparst(
        string input, int expectedHours, int expectedMinutes, int expectedSeconds)
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(input, out var value);
        Assert.True(ok);
        Assert.NotNull(value);
        Assert.Equal(expectedHours,   value!.Value.Hours);
        Assert.Equal(expectedMinutes, value.Value.Minutes);
        Assert.Equal(expectedSeconds, value.Value.Seconds);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("99:99:99")]
    public void TryParseOptionalTimeSpan_ungueltiges_format_ergibt_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryParseOptionalTimeSpan(input, out _);
        Assert.False(ok);
    }

    // ── TryParseTimeFallback ──────────────────────────────────────────────────

    [Fact]
    public void TryParseTimeFallback_gueltiger_wert_gibt_timespan_zurueck()
    {
        var result = ProtocolEntryInputNormalizer.TryParseTimeFallback("01:30");
        Assert.NotNull(result);
    }

    [Fact]
    public void TryParseTimeFallback_ungueltiger_wert_gibt_null_zurueck()
    {
        var result = ProtocolEntryInputNormalizer.TryParseTimeFallback("xyz");
        Assert.Null(result);
    }

    // ── TryNormalizeClockPosition ─────────────────────────────────────────────

    [Fact]
    public void TryNormalizeClockPosition_leer_ist_kein_fehler_hasValue_false()
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeClockPosition(null, out _, out var hasValue);
        Assert.True(ok);
        Assert.False(hasValue);
    }

    [Theory]
    [InlineData("0",  "00")]
    [InlineData("6",  "06")]
    [InlineData("12", "12")]
    public void TryNormalizeClockPosition_gueltige_werte_werden_normiert(string input, string expected)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeClockPosition(input, out var normalized, out var hasValue);
        Assert.True(ok);
        Assert.True(hasValue);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("13")]   // ueber 12
    [InlineData("-1")]   // unter 0
    [InlineData("abc")]  // kein Integer
    public void TryNormalizeClockPosition_ungueltiger_wert_ergibt_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeClockPosition(input, out _, out _);
        Assert.False(ok);
    }

    // ── TryNormalizeStrecke ───────────────────────────────────────────────────

    [Fact]
    public void TryNormalizeStrecke_leer_ist_kein_fehler()
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeStrecke(null, out _, out var hasValue);
        Assert.True(ok);
        Assert.False(hasValue);
    }

    [Theory]
    [InlineData("A",  "A1")]   // einbuchstabig wird zu A1
    [InlineData("B",  "B1")]
    [InlineData("C",  "C1")]
    [InlineData("A1", "A1")]   // bereits normiert
    [InlineData("b2", "B2")]   // Klein-> Gross
    [InlineData("C12","C12")]  // mehrstellig
    public void TryNormalizeStrecke_gueltige_werte_werden_normiert(string input, string expected)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeStrecke(input, out var normalized, out _);
        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("D")]    // nicht A/B/C
    [InlineData("1A")]   // Ziffer vorne
    [InlineData("X99")]  // unbekannter Buchstabe
    public void TryNormalizeStrecke_ungueltiger_wert_ergibt_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeStrecke(input, out _, out _);
        Assert.False(ok);
    }

    // ── TryNormalizeEz ────────────────────────────────────────────────────────

    [Fact]
    public void TryNormalizeEz_leer_ist_kein_fehler()
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeEz(null, out _, out var hasValue);
        Assert.True(ok);
        Assert.False(hasValue);
    }

    [Theory]
    [InlineData("0",   "EZ0")]
    [InlineData("4",   "EZ4")]
    [InlineData("EZ2", "EZ2")]
    [InlineData("ez3", "EZ3")]  // Kleinbuchstaben werden normiert
    public void TryNormalizeEz_gueltige_werte_werden_normiert(string input, string expected)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeEz(input, out var normalized, out _);
        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("5")]   // ueber 4
    [InlineData("-1")]  // unter 0
    [InlineData("abc")] // kein Integer
    public void TryNormalizeEz_ungueltiger_wert_ergibt_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeEz(input, out _, out _);
        Assert.False(ok);
    }

    // ── TryNormalizeSchachtbereich ────────────────────────────────────────────

    [Fact]
    public void TryNormalizeSchachtbereich_leer_ist_kein_fehler()
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(null, out _, out var hasValue);
        Assert.True(ok);
        Assert.False(hasValue);
    }

    [Theory]
    [InlineData("A", "A")]
    [InlineData("b", "B")]  // Kleinbuchstabe normiert
    [InlineData("D", "D")]
    [InlineData("F", "F")]
    [InlineData("H", "H")]
    [InlineData("I", "I")]
    [InlineData("J", "J")]
    public void TryNormalizeSchachtbereich_erlaubte_werte_werden_akzeptiert(string input, string expected)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(input, out var normalized, out _);
        Assert.True(ok);
        Assert.Equal(expected, normalized);
    }

    [Theory]
    [InlineData("C")]   // nicht im Werteset
    [InlineData("E")]
    [InlineData("AB")]  // mehrere Buchstaben
    public void TryNormalizeSchachtbereich_verbotener_wert_ergibt_false(string input)
    {
        var ok = ProtocolEntryInputNormalizer.TryNormalizeSchachtbereich(input, out _, out _);
        Assert.False(ok);
    }

    // ── FormatTime ────────────────────────────────────────────────────────────

    [Fact]
    public void FormatTime_unter_einer_stunde_nutzt_mm_ss_format()
    {
        var ts = new TimeSpan(0, 5, 30);
        Assert.Equal("05:30", ProtocolEntryInputNormalizer.FormatTime(ts));
    }

    [Fact]
    public void FormatTime_eine_stunde_und_mehr_nutzt_hh_mm_ss_format()
    {
        var ts = new TimeSpan(1, 5, 30);
        Assert.Equal("01:05:30", ProtocolEntryInputNormalizer.FormatTime(ts));
    }

    [Fact]
    public void FormatTime_genau_null_nutzt_mm_ss()
    {
        var ts = TimeSpan.Zero;
        Assert.Equal("00:00", ProtocolEntryInputNormalizer.FormatTime(ts));
    }
}
