using AuswertungPro.Next.Infrastructure.Ai;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer PrimaryDamageLineParser.
/// Sichert das IST-Verhalten aller vier unterstuetzten Import-Formate ab,
/// bevor die Methoden aus CodingSessionService extrahiert werden.
/// </summary>
public sealed class PrimaryDamageLineParserTests
{
    // ─── ParsePrimaryDamageLine ───────────────────────────────────────────

    [Theory]
    [InlineData("BCD @0.00m (Rohranfang)", "BCD", 0.00, "Rohranfang")]
    [InlineData("BAB @5.30m (Riss laengs)", "BAB", 5.30, "Riss laengs")]
    [InlineData("BCE @45.80m (Rohrende)", "BCE", 45.80, "Rohrende")]
    public void ParseLine_Format1_Pdf_CodeAtMeterInParens(string line, string code, double meter, string desc)
    {
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine(line);
        Assert.NotNull(result);
        Assert.Equal(code, result!.Value.Code);
        Assert.Equal(meter, result.Value.Meter, precision: 2);
        Assert.Equal(desc, result.Value.Description);
    }

    [Fact]
    public void ParseLine_Format1_WithOperatorCode_ExtractsCorrectCode()
    {
        // "A01 BAFCE @0.00m (Beschreibung)" — Operator-Code vor dem VSA-Code
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine("A01 BAFCE @0.00m (Beschreibung)");
        Assert.NotNull(result);
        Assert.Equal("BAFCE", result!.Value.Code);
        Assert.Equal(0.00, result.Value.Meter, precision: 2);
        Assert.Equal("Beschreibung", result.Value.Description);
    }

    [Fact]
    public void ParseLine_Format1_MissingParens_FallsBackToCodeAsDescription()
    {
        // Keine Klammern -> Beschreibung faellt auf Code zurueck
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine("BCD @0.00m");
        Assert.NotNull(result);
        Assert.Equal("BCD", result!.Value.Code);
        Assert.Equal("BCD", result.Value.Description);
    }

    [Theory]
    [InlineData("0.00m BCD Rohranfang", "BCD", 0.00, "Rohranfang")]
    [InlineData("2.24m BCCBA Bogen nach rechts", "BCCBA", 2.24, "Bogen nach rechts")]
    [InlineData("12.50m BAB Riss laengs", "BAB", 12.50, "Riss laengs")]
    public void ParseLine_Format2_Xtf_MeterCodeDesc(string line, string code, double meter, string desc)
    {
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine(line);
        Assert.NotNull(result);
        Assert.Equal(code, result!.Value.Code);
        Assert.Equal(meter, result.Value.Meter, precision: 2);
        Assert.Equal(desc, result.Value.Description);
    }

    [Fact]
    public void ParseLine_Format2_QualifierStripped()
    {
        // Q1=15 am Ende muss entfernt werden
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine("2.24m BCCBA Bogen (Details) Q1=15");
        Assert.NotNull(result);
        Assert.Equal("BCCBA", result!.Value.Code);
        Assert.Equal("Bogen (Details)", result.Value.Description);
    }

    [Theory]
    [InlineData("0.00  BCD  Rohranfang", "BCD", 0.00, "Rohranfang")]
    [InlineData("5.50  BAF  Oberflaechenschaden", "BAF", 5.50, "Oberflaechenschaden")]
    public void ParseLine_Format3_AltPdfInternal_MeterCodeDesc(string line, string code, double meter, string desc)
    {
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine(line);
        Assert.NotNull(result);
        Assert.Equal(code, result!.Value.Code);
        Assert.Equal(meter, result.Value.Meter, precision: 2);
        Assert.Contains(desc, result!.Value.Description);
    }

    [Fact]
    public void ParseLine_Format3_TimestampStripped()
    {
        // "0.00  BCD  Rohranfang  00:00:00" — Timestamp muss entfernt werden
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine("0.00  BCD  Rohranfang  00:00:00");
        Assert.NotNull(result);
        Assert.Equal("BCD", result!.Value.Code);
        Assert.Equal("Rohranfang", result.Value.Description);
    }

    [Fact]
    public void ParseLine_Format4_NoMeter_DefaultsMeterZero()
    {
        // Nur "CODE Beschreibung" ohne Meter -> Meter 0
        var result = PrimaryDamageLineParser.ParsePrimaryDamageLine("BCA Seitlicher Anschluss");
        Assert.NotNull(result);
        Assert.Equal("BCA", result!.Value.Code);
        Assert.Equal(0.0, result.Value.Meter, precision: 2);
        Assert.Contains("Anschluss", result.Value.Description);
    }

    [Fact]
    public void ParseLine_EmptyOrWhitespace_ReturnsNull()
    {
        Assert.Null(PrimaryDamageLineParser.ParsePrimaryDamageLine(""));
        Assert.Null(PrimaryDamageLineParser.ParsePrimaryDamageLine("   "));
    }

    [Fact]
    public void ParseLine_GarbageLine_ReturnsNull()
    {
        // Vollstaendiger Unsinn -> kein Match
        Assert.Null(PrimaryDamageLineParser.ParsePrimaryDamageLine("?!@#$%^&*()"));
    }

    // ─── TryParseMeterValue ───────────────────────────────────────────────

    [Theory]
    [InlineData("0.00", 0.00)]
    [InlineData("12.50", 12.50)]
    [InlineData("12,50", 12.50)]   // Komma als Dezimaltrennzeichen
    [InlineData("45", 45.0)]
    public void TryParseMeterValue_VariousFormats_ReturnsCorrect(string raw, double expected)
    {
        Assert.Equal(expected, PrimaryDamageLineParser.TryParseMeterValue(raw), precision: 2);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("abc")]
    public void TryParseMeterValue_InvalidInput_ReturnsZero(string raw)
    {
        Assert.Equal(0.0, PrimaryDamageLineParser.TryParseMeterValue(raw), precision: 2);
    }

    // ─── CleanDescription ─────────────────────────────────────────────────

    [Fact]
    public void CleanDescription_RemovesTimestamp()
    {
        Assert.Equal("Rohranfang", PrimaryDamageLineParser.CleanDescription("Rohranfang  00:00:00"));
    }

    [Fact]
    public void CleanDescription_RemovesQualifierSuffix()
    {
        Assert.Equal("Bogen", PrimaryDamageLineParser.CleanDescription("Bogen Q1=15%"));
    }

    [Fact]
    public void CleanDescription_EmptyInput_ReturnsEmpty()
    {
        Assert.Equal("", PrimaryDamageLineParser.CleanDescription(""));
        Assert.Equal("", PrimaryDamageLineParser.CleanDescription("   "));
    }

    [Fact]
    public void CleanDescription_NoNoise_ReturnsUnchanged()
    {
        Assert.Equal("Riss laengs", PrimaryDamageLineParser.CleanDescription("Riss laengs"));
    }
}
