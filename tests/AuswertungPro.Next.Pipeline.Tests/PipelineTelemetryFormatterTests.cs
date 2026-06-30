using AuswertungPro.Next.Application.Ai;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Charakterisierungs-Tests fuer <see cref="PipelineTelemetryFormatter"/>.
/// </summary>
public sealed class PipelineTelemetryFormatterTests
{
    private static PhaseStat LeereStat() => new(0, 0, 0, 0);
    private static PhaseStat AktiveStat(double mean, double p95) => new(mean, mean, p95, (long)(mean * 10));

    [Fact]
    public void Format_Null_GibtLeerstring()
    {
        Assert.Equal("", PipelineTelemetryFormatter.Format(null));
    }

    [Fact]
    public void Format_MinimaleTelemetrie_EnthaltWallUndFrames()
    {
        var t = new TelemetrySummary(
            TotalFrames: 50,
            SkippedFrames: 5,
            Extraction: LeereStat(),
            Yolo: LeereStat(),
            Dino: LeereStat(),
            Sam: LeereStat(),
            Qwen: LeereStat(),
            Total: LeereStat(),
            WallClockMs: 3000);

        var result = PipelineTelemetryFormatter.Format(t);
        Assert.Contains("Wall: 3.0s", result);
        Assert.Contains("Frames: 50 (5 skipped)", result);
    }

    [Fact]
    public void Format_MitYoloDinoSam_EnthaltAlle()
    {
        var t = new TelemetrySummary(
            TotalFrames: 100,
            SkippedFrames: 0,
            Extraction: AktiveStat(10, 20),
            Yolo: AktiveStat(50, 90),
            Dino: AktiveStat(107, 150),
            Sam: AktiveStat(30, 60),
            Qwen: LeereStat(),
            Total: AktiveStat(200, 300),
            WallClockMs: 12000);

        var result = PipelineTelemetryFormatter.Format(t);
        Assert.Contains("YOLO:", result);
        Assert.Contains("DINO:", result);
        Assert.Contains("SAM:", result);
        Assert.DoesNotContain("Vision:", result);  // Qwen deaktiviert
        Assert.Contains("Total/Frame:", result);
    }

    [Fact]
    public void Format_MitQwen_EnthaltVision()
    {
        var t = new TelemetrySummary(
            TotalFrames: 10,
            SkippedFrames: 0,
            Extraction: LeereStat(),
            Yolo: LeereStat(),
            Dino: LeereStat(),
            Sam: LeereStat(),
            Qwen: AktiveStat(250, 400),
            Total: LeereStat(),
            WallClockMs: 5000);

        var result = PipelineTelemetryFormatter.Format(t);
        Assert.Contains("Vision:", result);
    }

    [Fact]
    public void Format_LeereStat_ZeigtDash()
    {
        var t = new TelemetrySummary(
            TotalFrames: 0,
            SkippedFrames: 0,
            Extraction: LeereStat(),
            Yolo: LeereStat(),
            Dino: LeereStat(),
            Sam: LeereStat(),
            Qwen: LeereStat(),
            Total: LeereStat(),
            WallClockMs: 0);

        var result = PipelineTelemetryFormatter.Format(t);
        Assert.Contains("Extraction: —", result);
        Assert.Contains("Total/Frame: —", result);
    }

    [Fact]
    public void Format_TrennerIstPipe()
    {
        var t = new TelemetrySummary(
            TotalFrames: 5,
            SkippedFrames: 0,
            Extraction: LeereStat(),
            Yolo: LeereStat(),
            Dino: LeereStat(),
            Sam: LeereStat(),
            Qwen: LeereStat(),
            Total: LeereStat(),
            WallClockMs: 1000);

        var result = PipelineTelemetryFormatter.Format(t);
        Assert.Contains("  |  ", result);
    }
}
