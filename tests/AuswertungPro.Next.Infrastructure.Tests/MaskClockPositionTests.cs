using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;

namespace AuswertungPro.Next.Infrastructure.Tests;

/// <summary>
/// Uhrlage gegen die kalibrierte Rohrmitte statt Bildmitte. Bei zentrierter
/// Kamera identisch zum Fallback; bei verschobener Rohrmitte korrekt anders;
/// ohne Kalibrierung bleibt das bisherige (Bildmitte-)Verhalten erhalten.
/// </summary>
public class MaskClockPositionTests
{
    private static PipeCalibration Calib(double cx, double cy) =>
        new() { PipeCenter = new NormalizedPoint(cx, cy) };

    [Fact]
    public void Clock_CenteredCalibration_MatchesImageCenterFallback()
    {
        var withCalib = MaskQuantificationService.ComputeClockPosition(70, 50, 100, 100, Calib(0.5, 0.5));
        var fallback = MaskQuantificationService.ComputeClockPosition(70, 50, 100, 100);

        Assert.Equal(fallback, withCalib);
        Assert.Equal("3:00", withCalib);
    }

    [Fact]
    public void Clock_ShiftedCenter_ChangesResult()
    {
        // Centroid (70,50) liegt bei verschobener Rohrmitte (0.7,0.2) direkt
        // UNTER dem Zentrum -> 6:00 (gegen Bildmitte waere es 3:00).
        var shifted = MaskQuantificationService.ComputeClockPosition(70, 50, 100, 100, Calib(0.7, 0.2));

        Assert.Equal("6:00", shifted);
    }

    [Fact]
    public void Clock_NoCalibration_FallsBackToImageCenter()
    {
        Assert.Equal("3:00", MaskQuantificationService.ComputeClockPosition(70, 50, 100, 100, null));
    }

    [Fact]
    public void Clock_InvalidImage_ReturnsNull()
    {
        Assert.Null(MaskQuantificationService.ComputeClockPosition(70, 50, 0, 100, Calib(0.5, 0.5)));
    }
}
