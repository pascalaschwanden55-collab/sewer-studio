using AuswertungPro.Next.Infrastructure.Import.Ibak;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungstests für IbakDatenTxtLineParser — prüfen das IST-Verhalten
/// aller pure-static-Methoden ohne Datei-IO oder Datenbankzugriff.
/// </summary>
public sealed class IbakDatenTxtLineParserTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // StripIbakMeta
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void StripIbakMeta_removes_marker_and_trailing_content()
    {
        var result = IbakDatenTxtLineParser.StripIbakMeta("Riss@!$ibak$!extra data");
        Assert.Equal("Riss", result);
    }

    [Fact]
    public void StripIbakMeta_returns_trimmed_text_when_no_marker()
    {
        var result = IbakDatenTxtLineParser.StripIbakMeta("  Korrosion  ");
        Assert.Equal("Korrosion", result);
    }

    [Fact]
    public void StripIbakMeta_case_insensitive_marker()
    {
        var result = IbakDatenTxtLineParser.StripIbakMeta("Text@!$IBAK$!rest");
        Assert.Equal("Text", result);
    }

    [Fact]
    public void StripIbakMeta_empty_input_returns_empty()
    {
        Assert.Equal("", IbakDatenTxtLineParser.StripIbakMeta(""));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ParseMeter
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("12.34", 12.34)]
    [InlineData("12,34", 12.34)]
    [InlineData("0.00", 0.0)]
    [InlineData("100", 100.0)]
    public void ParseMeter_parses_valid_values(string text, double expected)
    {
        var result = IbakDatenTxtLineParser.ParseMeter(text);
        Assert.NotNull(result);
        Assert.Equal(expected, result!.Value, precision: 5);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("abc")]
    public void ParseMeter_returns_null_for_invalid_input(string? text)
    {
        Assert.Null(IbakDatenTxtLineParser.ParseMeter(text!));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ParseTime
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ParseTime_parses_hh_mm_ss_format()
    {
        var result = IbakDatenTxtLineParser.ParseTime("01:23:45");
        Assert.NotNull(result);
        Assert.Equal(new TimeSpan(1, 23, 45), result!.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("not-time")]
    public void ParseTime_returns_null_for_invalid_input(string? text)
    {
        Assert.Null(IbakDatenTxtLineParser.ParseTime(text!));
    }

    // ──────────────────────────────────────────────────────────────────────────
    // ExtractRange
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ExtractRange_detects_anfang_as_start()
    {
        var (isStart, isEnd, index) = IbakDatenTxtLineParser.ExtractRange("Anfang Riss");
        Assert.True(isStart);
        Assert.False(isEnd);
        Assert.Equal("0", index);
    }

    [Fact]
    public void ExtractRange_detects_beginn_as_start()
    {
        var (isStart, isEnd, index) = IbakDatenTxtLineParser.ExtractRange("Beginn Streckenschaden");
        Assert.True(isStart);
        Assert.False(isEnd);
    }

    [Fact]
    public void ExtractRange_detects_ende()
    {
        var (isStart, isEnd, index) = IbakDatenTxtLineParser.ExtractRange("Ende Riss");
        Assert.False(isStart);
        Assert.True(isEnd);
        Assert.Equal("0", index);
    }

    [Fact]
    public void ExtractRange_extracts_parenthesized_index()
    {
        var (_, _, index) = IbakDatenTxtLineParser.ExtractRange("Anfang Riss (3)");
        Assert.Equal("3", index);
    }

    [Fact]
    public void ExtractRange_returns_no_range_for_plain_text()
    {
        var (isStart, isEnd, index) = IbakDatenTxtLineParser.ExtractRange("Normaler Befund");
        Assert.False(isStart);
        Assert.False(isEnd);
        Assert.Equal("0", index);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // BuildEntry
    // ──────────────────────────────────────────────────────────────────────────

    [Fact]
    public void BuildEntry_creates_entry_with_all_fields()
    {
        var time = new TimeSpan(0, 1, 30);
        var entry = IbakDatenTxtLineParser.BuildEntry("BAB", "Riss laengs", 12.5, "01:01:30", time);

        Assert.Equal("BAB", entry.Code);
        Assert.Equal("Riss laengs", entry.Beschreibung);
        Assert.Equal(12.5, entry.MeterStart);
        Assert.Equal(12.5, entry.MeterEnd);
        Assert.Equal("01:01:30", entry.Mpeg);
        Assert.Equal(time, entry.Zeit);
    }

    [Fact]
    public void BuildEntry_null_meter_yields_null_meterstart()
    {
        var entry = IbakDatenTxtLineParser.BuildEntry("AEC", "Header", null, null, null);

        Assert.Null(entry.MeterStart);
        Assert.Null(entry.MeterEnd);
        Assert.Null(entry.Zeit);
    }

    // ──────────────────────────────────────────────────────────────────────────
    // MapMaterial
    // ──────────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData("Polypropylen", "PP")]
    [InlineData("Polyvinylchlorid", "PVC")]
    [InlineData("pvc", "PVC")]
    [InlineData("Polyethylen", "PE")]
    [InlineData("pe", "PE")]
    [InlineData("Beton", "Beton")]
    [InlineData("Normalbeton", "Beton")]
    [InlineData("Steinzeug", "Steinzeug")]
    [InlineData("Guss", "Guss")]
    [InlineData("GFK", "GFK")]
    [InlineData("Glasfaser", "GFK")]
    [InlineData("UnbekanntesFasermaterial", "UnbekanntesFasermaterial")]
    public void MapMaterial_maps_known_and_unknown_materials(string input, string expected)
    {
        Assert.Equal(expected, IbakDatenTxtLineParser.MapMaterial(input));
    }
}
