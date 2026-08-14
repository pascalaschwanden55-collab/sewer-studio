using System;
using AuswertungPro.Next.Infrastructure.Import.WinCan;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests fuer WinCanValueNormalizer – prueft das reale IST-Verhalten.
/// </summary>
public sealed class WinCanValueNormalizerTests
{
    // ── NormalizeNumber ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("300", "300")]
    [InlineData("300.0", "300")]
    [InlineData("12.5", "12.5")]
    [InlineData("12,5", "12.5")]
    [InlineData("1.234", "1.23")]
    [InlineData("  150  ", "150")]
    [InlineData("0", "0")]
    public void NormalizeNumber_GueltigeZahlen_KehrtKanonischeForm(string raw, string expected)
        => Assert.Equal(expected, WinCanValueNormalizer.NormalizeNumber(raw));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeNumber_LeerOderNull_GibtNull(string? raw)
        => Assert.Null(WinCanValueNormalizer.NormalizeNumber(raw));

    [Fact]
    public void NormalizeNumber_NichtParsebar_GibtRohtextTrimmed()
        => Assert.Equal("DN300", WinCanValueNormalizer.NormalizeNumber("DN300"));

    // ── NormalizeDate ────────────────────────────────────────────────────────

    [Fact]
    public void NormalizeDate_YearTextVorhanden_GibtYearText()
        => Assert.Equal("2020", WinCanValueNormalizer.NormalizeDate("2020", "01.01.2019"));

    [Fact]
    public void NormalizeDate_NurRawDate_ParsetDatum()
        => Assert.Equal("15.06.2021", WinCanValueNormalizer.NormalizeDate(null, "2021-06-15"));

    [Fact]
    public void NormalizeDate_BeideNull_GibtNull()
        => Assert.Null(WinCanValueNormalizer.NormalizeDate(null, null));

    [Fact]
    public void NormalizeDate_EuropaischesFormat_RichtigeReihenfolge()
        => Assert.Equal("03.02.2022", WinCanValueNormalizer.NormalizeDate(null, "03.02.2022"));

    // ── NormalizeUsage ───────────────────────────────────────────────────────

    [Theory]
    [InlineData("S", "Schmutzabwasser")]
    [InlineData("sw", "Schmutzabwasser")]
    [InlineData("R", "Niederschlagsabwasser")]
    [InlineData("rw", "Niederschlagsabwasser")]
    [InlineData("M", "Mischabwasser")]
    [InlineData("mw", "Mischabwasser")]
    [InlineData("Schmutzabwasser", "Schmutzabwasser")]
    [InlineData("Regenwasserkanal", "Niederschlagsabwasser")]
    [InlineData("Mischwasser", "Mischabwasser")]
    public void NormalizeUsage_BekannteKurzformenUndTexte_KehrtKanonischesLabel(string raw, string expected)
        => Assert.Equal(expected, WinCanValueNormalizer.NormalizeUsage(raw));

    [Theory]
    [InlineData("gereinigt")]
    [InlineData("ja")]
    [InlineData("nein")]
    [InlineData("-")]
    [InlineData("E")]
    [InlineData("H")]
    public void NormalizeUsage_NichtZuzuordnen_GibtNull(string raw)
        => Assert.Null(WinCanValueNormalizer.NormalizeUsage(raw));

    [Fact]
    public void NormalizeUsage_LangerUnbekannterText_GibtUnveraendertZurueck()
        => Assert.Equal("Sonderkanalisation", WinCanValueNormalizer.NormalizeUsage("Sonderkanalisation"));

    // ── NormalizeInspectionDir ───────────────────────────────────────────────

    [Fact]
    public void NormalizeInspectionDir_Code1_GibtInFliessrichtung()
        => Assert.Equal("In Fliessrichtung", WinCanValueNormalizer.NormalizeInspectionDir("1"));

    [Fact]
    public void NormalizeInspectionDir_Code2_GibtGegenFliessrichtung()
        => Assert.Equal("Gegen Fliessrichtung", WinCanValueNormalizer.NormalizeInspectionDir("2"));

    [Fact]
    public void NormalizeInspectionDir_Null_GibtNull()
        => Assert.Null(WinCanValueNormalizer.NormalizeInspectionDir(null));

    [Fact]
    public void NormalizeInspectionDir_UnbekannterCode_GibtRohtextZurueck()
        => Assert.Equal("upstream", WinCanValueNormalizer.NormalizeInspectionDir("upstream"));

    // ── NormalizeAccessible ──────────────────────────────────────────────────

    [Theory]
    [InlineData("1", "offen")]
    [InlineData("true", "offen")]
    [InlineData("ja", "offen")]
    [InlineData("yes", "offen")]
    [InlineData("0", "abgeschlossen")]
    [InlineData("false", "abgeschlossen")]
    [InlineData("nein", "abgeschlossen")]
    [InlineData("no", "abgeschlossen")]
    public void NormalizeAccessible_BekannteFlags_GibtKlarenText(string raw, string expected)
        => Assert.Equal(expected, WinCanValueNormalizer.NormalizeAccessible(raw));

    [Fact]
    public void NormalizeAccessible_Null_GibtNull()
        => Assert.Null(WinCanValueNormalizer.NormalizeAccessible(null));

    // ── ParseSqliteDate ──────────────────────────────────────────────────────

    [Fact]
    public void ParseSqliteDate_UnixMillisFormat_ParsetKorrekt()
    {
        var expected = DateTimeOffset.FromUnixTimeMilliseconds(1609459200000L).DateTime;
        var result = WinCanValueNormalizer.ParseSqliteDate("Date(1609459200000)");
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ParseSqliteDate_Iso8601Format_ParsetKorrekt()
    {
        var result = WinCanValueNormalizer.ParseSqliteDate("2021-06-15");
        Assert.Equal(new DateTime(2021, 6, 15), result);
    }

    [Fact]
    public void ParseSqliteDate_EuropaischesFormat_ParsetOhneTagMonatVerwechslung()
    {
        var result = WinCanValueNormalizer.ParseSqliteDate("03.02.2022");
        Assert.Equal(new DateTime(2022, 2, 3), result);
    }

    [Fact]
    public void ParseSqliteDate_Null_GibtNull()
        => Assert.Null(WinCanValueNormalizer.ParseSqliteDate(null));

    [Fact]
    public void ParseSqliteDate_Leerstring_GibtNull()
        => Assert.Null(WinCanValueNormalizer.ParseSqliteDate(""));

    // ── ExtractQuantValue ────────────────────────────────────────────────────

    [Fact]
    public void ExtractQuantValue_Prozent_ExtrahiertWert()
        => Assert.Equal("25", WinCanValueNormalizer.ExtractQuantValue("Verformung 25%"));

    [Fact]
    public void ExtractQuantValue_ProzentMitKomma_NormalistertAufPunkt()
        => Assert.Equal("10.5", WinCanValueNormalizer.ExtractQuantValue("Verformung 10,5%"));

    [Fact]
    public void ExtractQuantValue_Grad_ExtrahiertWert()
        => Assert.Equal("45", WinCanValueNormalizer.ExtractQuantValue("Knick 45°"));

    [Fact]
    public void ExtractQuantValue_Millimeter_ExtrahiertWert()
        => Assert.Equal("2", WinCanValueNormalizer.ExtractQuantValue("Riss 2mm"));

    [Fact]
    public void ExtractQuantValue_KeinWert_GibtNull()
        => Assert.Null(WinCanValueNormalizer.ExtractQuantValue("Keine Angabe"));

    // ── ParseTimeSpan ────────────────────────────────────────────────────────

    [Fact]
    public void ParseTimeSpan_HhMmSsFormat_ParsetKorrekt()
        => Assert.Equal(new TimeSpan(0, 1, 23, 45), WinCanValueNormalizer.ParseTimeSpan("01:23:45"));

    [Fact]
    public void ParseTimeSpan_MmSsFormat_ParsetKorrekt()
        => Assert.Equal(new TimeSpan(0, 0, 1, 30), WinCanValueNormalizer.ParseTimeSpan("01:30"));

    [Fact]
    public void ParseTimeSpan_Null_GibtNull()
        => Assert.Null(WinCanValueNormalizer.ParseTimeSpan(null));

    [Fact]
    public void ParseTimeSpan_Leerstring_GibtNull()
        => Assert.Null(WinCanValueNormalizer.ParseTimeSpan(""));

    // ── IsImage / IsVideo ────────────────────────────────────────────────────

    [Theory]
    [InlineData("JPG", true)]
    [InlineData("jpeg", true)]
    [InlineData("PNG", true)]
    [InlineData("BMP", true)]
    [InlineData("MP4", false)]
    [InlineData(null, false)]
    public void IsImage_VariantenUndNull_GibtErwartetesErgebnis(string? type, bool expected)
        => Assert.Equal(expected, WinCanValueNormalizer.IsImage(type));

    [Theory]
    [InlineData("MP4", true)]
    [InlineData("mpg", true)]
    [InlineData("AVI", true)]
    [InlineData("MOV", true)]
    [InlineData("JPG", false)]
    [InlineData(null, false)]
    public void IsVideo_VariantenUndNull_GibtErwartetesErgebnis(string? type, bool expected)
        => Assert.Equal(expected, WinCanValueNormalizer.IsVideo(type));
}
