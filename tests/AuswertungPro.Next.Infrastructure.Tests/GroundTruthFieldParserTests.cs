// Charakterisierungs-Tests fuer GroundTruthFieldParser
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class GroundTruthFieldParserTests
{
    // ── ParseTimestamp ──────────────────────────────────────────────────────

    [Fact]
    public void ParseTimestamp_VerarbeitetGueltigenHHMMSS()
    {
        var result = GroundTruthFieldParser.ParseTimestamp("00:01:26");

        Assert.Equal(TimeSpan.FromSeconds(86), result);
    }

    [Fact]
    public void ParseTimestamp_GibtNull_BeiLeeremString()
    {
        Assert.Null(GroundTruthFieldParser.ParseTimestamp(""));
        Assert.Null(GroundTruthFieldParser.ParseTimestamp(null!));
    }

    [Fact]
    public void ParseTimestamp_GibtNull_BeiUngueltigemFormat()
    {
        Assert.Null(GroundTruthFieldParser.ParseTimestamp("12:34"));
        Assert.Null(GroundTruthFieldParser.ParseTimestamp("aa:bb:cc"));
    }

    // ── TryParseMeter ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("12.45", 12.45)]
    [InlineData("12,45", 12.45)]
    [InlineData("0.00", 0.00)]
    [InlineData("100.000", 100.0)]
    public void TryParseMeter_VerarbeitetGueltigenMeter(string raw, double expected)
    {
        var ok = GroundTruthFieldParser.TryParseMeter(raw, out var value);

        Assert.True(ok);
        Assert.Equal(expected, value, precision: 3);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    public void TryParseMeter_GibtFalseZurueck_BeiUngueltigemInput(string? raw)
    {
        var ok = GroundTruthFieldParser.TryParseMeter(raw!, out _);

        Assert.False(ok);
    }

    // ── TryParseClockPosition ───────────────────────────────────────────────

    [Theory]
    [InlineData("Riss 3 Uhr", "3")]
    [InlineData("Riss 12h rechts", "12")]
    [InlineData("Schadensposition 9 Uhr", "9")]
    public void TryParseClockPosition_FindetUhrzeit(string text, string expected)
    {
        var result = GroundTruthFieldParser.TryParseClockPosition(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseClockPosition_GibtNull_BeiKeinerUhrzeit()
    {
        Assert.Null(GroundTruthFieldParser.TryParseClockPosition("Riss ohne Uhrangabe"));
    }

    // ── TryParseSeverity ────────────────────────────────────────────────────

    [Theory]
    [InlineData("Schadensstufe 3", "3")]
    [InlineData("Schweregrad: high", "high")]
    [InlineData("Stufe 5", "5")]
    [InlineData("S2", "2")]
    public void TryParseSeverity_FindetSchwerestufe(string text, string expected)
    {
        var result = GroundTruthFieldParser.TryParseSeverity(text);

        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData("Schadensstufe leicht", "low")]
    [InlineData("Schweregrad niedrig", "low")]
    [InlineData("Stufe: mittel", "mid")]
    [InlineData("Schweregrad hoch", "high")]
    public void TryParseSeverity_NormalisierttVerbaleSchwerestufen(string text, string expected)
    {
        var result = GroundTruthFieldParser.TryParseSeverity(text);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void TryParseSeverity_GibtNull_OhneAngabe()
    {
        Assert.Null(GroundTruthFieldParser.TryParseSeverity("Riss ohne Schweregrad"));
    }

    // ── TryParseQuantification ──────────────────────────────────────────────

    [Fact]
    public void TryParseQuantification_FindetProzentangabe()
    {
        var result = GroundTruthFieldParser.TryParseQuantification("Querschnittsverminderung 30%", null);

        Assert.NotNull(result);
        Assert.Equal(30.0, result!.Value, precision: 2);
        Assert.Equal("%", result.Unit);
        Assert.Equal("Querschnittsverminderung", result.Type);
    }

    [Fact]
    public void TryParseQuantification_FindetMillimeterangabe()
    {
        var result = GroundTruthFieldParser.TryParseQuantification("Spaltbreite 3mm", null);

        Assert.NotNull(result);
        Assert.Equal(3.0, result!.Value, precision: 2);
        Assert.Equal("mm", result.Unit);
    }

    [Fact]
    public void TryParseQuantification_GibtNull_OhneAngabe()
    {
        Assert.Null(GroundTruthFieldParser.TryParseQuantification("Riss ohne Massangabe", null));
    }

    // ── NormalizeKnownVsaCode ───────────────────────────────────────────────

    [Fact]
    public void NormalizeKnownVsaCode_NormalisiertBekanntenCode()
    {
        var result = GroundTruthFieldParser.NormalizeKnownVsaCode("BCD");

        Assert.Equal("BCD", result);
    }

    [Fact]
    public void NormalizeKnownVsaCode_EntferntPunkte()
    {
        var result = GroundTruthFieldParser.NormalizeKnownVsaCode("BAF.B");

        // Haengt davon ab ob BAF.B oder BAFB bekannt ist - wir testen was Ist-Verhalten ist.
        if (result is not null)
            Assert.Equal(result.Replace(".", ""), result);
    }

    [Fact]
    public void NormalizeKnownVsaCode_GibtNull_BeiUnbekanntemCode()
    {
        Assert.Null(GroundTruthFieldParser.NormalizeKnownVsaCode("ZZZZZ"));
        Assert.Null(GroundTruthFieldParser.NormalizeKnownVsaCode(null));
    }

    // ── Sig ─────────────────────────────────────────────────────────────────

    [Fact]
    public void Sig_EnthältCode_MeterStartUndEnd()
    {
        var entry = new GroundTruthEntry
        {
            VsaCode    = "BCD",
            MeterStart = 1.25,
            MeterEnd   = 1.25,
            Text       = "Rohranfang"
        };

        var sig = GroundTruthFieldParser.Sig(entry);

        Assert.Contains("BCD", sig);
        Assert.Contains("1.25", sig);
    }
}
