using System;
using AuswertungPro.Next.Infrastructure.Import.Kins;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="KinsTextLineParser"/>.
/// Testen das exakte IST-Verhalten der extrahierten Methoden.
/// </summary>
public sealed class KinsTextLineParserTests
{
    // --- TryParseHeaderLine ---

    [Fact]
    public void TryParseHeaderLine_ReturnsFalse_WhenLineIsEmpty()
    {
        var result = KinsTextLineParser.TryParseHeaderLine(string.Empty, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseHeaderLine_ReturnsFalse_WhenNoAtDateiMarker()
    {
        var result = KinsTextLineParser.TryParseHeaderLine("Schmutzwasser 23654 -> 23038 UV 450", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseHeaderLine_ReturnsFalse_WhenNoArrow()
    {
        var result = KinsTextLineParser.TryParseHeaderLine("Schmutzwasser 23654 23038 UV 450 @Datei=A001.MPG", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseHeaderLine_ParsesFullHeader_WithMaterialAndDiameter()
    {
        const string line = "Schmutzwasser 23654 -> 23038 UV 450 @Datei=A001.MPG";

        var ok = KinsTextLineParser.TryParseHeaderLine(line, out var header);

        Assert.True(ok);
        Assert.Equal("Schmutzwasser", header.Usage);
        Assert.Equal("23654", header.From);
        Assert.Equal("23038", header.To);
        Assert.Equal("UV", header.Material);
        Assert.Equal("450", header.Diameter);
        Assert.Equal("A001.MPG", header.VideoFile);
    }

    [Fact]
    public void TryParseHeaderLine_ParsesHeaderWithoutDiameter()
    {
        const string line = "SW 10001 -> 10002 Beton @Datei=video.avi";

        var ok = KinsTextLineParser.TryParseHeaderLine(line, out var header);

        Assert.True(ok);
        Assert.Equal("SW", header.Usage);
        Assert.Equal("10001", header.From);
        Assert.Equal("10002", header.To);
        Assert.Equal("Beton", header.Material);
        Assert.Null(header.Diameter);
        Assert.Equal("video.avi", header.VideoFile);
    }

    [Fact]
    public void TryParseHeaderLine_ParsesHeaderWithoutMaterialNorDiameter()
    {
        // Nur Usage + Von -> Nach + Datei
        const string line = "SW 10001 -> 10002 @Datei=video.avi";

        var ok = KinsTextLineParser.TryParseHeaderLine(line, out var header);

        Assert.True(ok);
        Assert.Equal("SW", header.Usage);
        Assert.Equal("10001", header.From);
        Assert.Equal("10002", header.To);
        Assert.Equal(string.Empty, header.Material);
        Assert.Null(header.Diameter);
        Assert.Equal("video.avi", header.VideoFile);
    }

    [Fact]
    public void TryParseHeaderLine_ReturnsFalse_WhenVideoFileIsEmpty()
    {
        const string line = "Schmutzwasser 23654 -> 23038 UV 450 @Datei=";

        var ok = KinsTextLineParser.TryParseHeaderLine(line, out _);

        Assert.False(ok);
    }

    // --- TryParseObservationLine ---

    [Fact]
    public void TryParseObservationLine_ReturnsFalse_WhenLineIsEmpty()
    {
        var result = KinsTextLineParser.TryParseObservationLine(string.Empty, out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseObservationLine_ReturnsFalse_WhenNoMeterPrefix()
    {
        var result = KinsTextLineParser.TryParseObservationLine("Rohranfang @Pos=0:00:00", out _);
        Assert.False(result);
    }

    [Fact]
    public void TryParseObservationLine_ParsesMeterAndDescription()
    {
        const string line = "   18.3m Rohrende  @Pos=0:02:23";

        var ok = KinsTextLineParser.TryParseObservationLine(line, out var entry);

        Assert.True(ok);
        Assert.Equal(18.3, entry.MeterStart);
        Assert.Equal(18.3, entry.MeterEnd);
        Assert.Equal("Rohrende", entry.Beschreibung);
        Assert.False(entry.IsStreckenschaden);
    }

    [Fact]
    public void TryParseObservationLine_ParsesTimeStampFromPos()
    {
        const string line = "   0.0m Rohranfang  @Pos=0:00:00";

        var ok = KinsTextLineParser.TryParseObservationLine(line, out var entry);

        Assert.True(ok);
        Assert.Equal(TimeSpan.Zero, entry.Zeit);
        Assert.Equal("0:00:00", entry.Mpeg);
    }

    [Fact]
    public void TryParseObservationLine_HandlesMeterWithCommaDecimal()
    {
        // Deutschen Dezimaltrenner (Komma) akzeptieren
        const string line = "12,5m Riss";

        var ok = KinsTextLineParser.TryParseObservationLine(line, out var entry);

        Assert.True(ok);
        Assert.Equal(12.5, entry.MeterStart);
    }

    [Fact]
    public void TryParseObservationLine_SetsNullZeit_WhenNoPosGroup()
    {
        const string line = "   5.0m Ablagerung";

        var ok = KinsTextLineParser.TryParseObservationLine(line, out var entry);

        Assert.True(ok);
        Assert.Null(entry.Zeit);
        Assert.Null(entry.Mpeg);
    }

    // --- ParseKinsTime ---

    [Fact]
    public void ParseKinsTime_ReturnsNull_WhenTextIsEmpty()
    {
        var result = KinsTextLineParser.ParseKinsTime(string.Empty);
        Assert.Null(result);
    }

    [Fact]
    public void ParseKinsTime_ParsesHhMmSsFormat()
    {
        var result = KinsTextLineParser.ParseKinsTime("01:23:45");
        Assert.Equal(new TimeSpan(1, 23, 45), result);
    }

    [Fact]
    public void ParseKinsTime_ParsesHMmSsFormat()
    {
        var result = KinsTextLineParser.ParseKinsTime("0:02:23");
        Assert.Equal(new TimeSpan(0, 2, 23), result);
    }

    [Fact]
    public void ParseKinsTime_ReturnsNull_WhenTextIsNotParseable()
    {
        var result = KinsTextLineParser.ParseKinsTime("abc");
        Assert.Null(result);
    }

    // --- Tokenize ---

    [Fact]
    public void Tokenize_SplitsOnWhitespace()
    {
        var tokens = KinsTextLineParser.Tokenize("  Schmutzwasser  23654  ");
        Assert.Equal(new[] { "Schmutzwasser", "23654" }, tokens);
    }

    [Fact]
    public void Tokenize_ReturnsEmptyArray_WhenInputIsEmpty()
    {
        var tokens = KinsTextLineParser.Tokenize(string.Empty);
        Assert.Empty(tokens);
    }

    [Fact]
    public void Tokenize_ReturnsEmptyArray_WhenInputIsNull()
    {
        var tokens = KinsTextLineParser.Tokenize(null!);
        Assert.Empty(tokens);
    }
}
