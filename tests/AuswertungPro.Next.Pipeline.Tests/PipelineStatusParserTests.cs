using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="PipelineStatusParser"/>.
/// Decken alle Regex-Pfade und Grenzfaelle der Status-String-Extraktion ab.
/// </summary>
public sealed class PipelineStatusParserTests
{
    // ── TryExtractMeter ──────────────────────────────────────────────────────

    [Theory]
    [InlineData("Frame 10/200 @ 12.5m", "12.5 m")]
    [InlineData("@ 0.0m", "0.0 m")]
    [InlineData("Analyse @ 100m abgeschlossen", "100.0 m")]
    [InlineData("@ 3,7m (Komma)", "3.7 m")]       // Komma als Dezimaltrenner
    [InlineData("@12.0m ohne Leerzeichen", "12.0 m")]
    public void TryExtractMeter_GueltigeEingabe_GibtFormattiertenMeter(string status, string expected)
    {
        var result = PipelineStatusParser.TryExtractMeter(status);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("Frame 5/10 kein Meterstand")]
    [InlineData("Kein @ Zeichen vorhanden")]
    public void TryExtractMeter_UngueltigeEingabe_GibtNull(string? status)
    {
        var result = PipelineStatusParser.TryExtractMeter(status);
        Assert.Null(result);
    }

    // ── TryExtractFindingCount ───────────────────────────────────────────────

    [Theory]
    [InlineData("5 Befunde erkannt", 5)]
    [InlineData("Verarbeitet: 42 Befunde", 42)]
    [InlineData("1 befunde (Kleinschreibung)", 1)]   // IgnoreCase
    [InlineData("0 Befunde", 0)]
    public void TryExtractFindingCount_GueltigeEingabe_GibtAnzahl(string status, int expected)
    {
        var result = PipelineStatusParser.TryExtractFindingCount(status);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Keine Erkennungen vorhanden")]
    [InlineData("@ 10m Frame")]
    public void TryExtractFindingCount_UngueltigeEingabe_GibtNull(string? status)
    {
        var result = PipelineStatusParser.TryExtractFindingCount(status);
        Assert.Null(result);
    }

    // ── TryExtractYoloTotalFrames ─────────────────────────────────────────────

    [Theory]
    [InlineData("YOLO: 38 gesamt", 38)]
    [InlineData("Analyse: 100 gesamt abgeschlossen", 100)]
    [InlineData("0 gesamt", 0)]
    public void TryExtractYoloTotalFrames_GueltigeEingabe_GibtFrameanzahl(string status, int expected)
    {
        var result = PipelineStatusParser.TryExtractYoloTotalFrames(status);
        Assert.Equal(expected, result);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("38 frames (kein 'gesamt')")]
    public void TryExtractYoloTotalFrames_UngueltigeEingabe_GibtNull(string? status)
    {
        var result = PipelineStatusParser.TryExtractYoloTotalFrames(status);
        Assert.Null(result);
    }
}
