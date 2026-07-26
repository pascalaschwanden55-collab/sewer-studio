using System.Windows;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingManualCalibrationPolicyTests
{
    [Fact]
    public void Build_creates_manual_pipe_calibration_from_reference_line()
    {
        var result = CodingManualCalibrationPolicy.Build(
            start: new NormalizedPoint(0.2, 0.4),
            end: new NormalizedPoint(0.8, 0.4),
            startPixel: new Point(20, 40),
            endPixel: new Point(620, 40),
            nominalDiameterMm: 300);

        Assert.True(result.IsValid);
        Assert.NotNull(result.Calibration);
        var calibration = result.Calibration!;
        Assert.Equal(300, calibration.NominalDiameterMm);
        Assert.Equal(600, calibration.PipePixelDiameter);
        Assert.Equal(0.6, calibration.NormalizedDiameter, precision: 3);
        Assert.Equal(0.5, calibration.PipeCenter.X, precision: 3);
        Assert.Equal(0.4, calibration.PipeCenter.Y, precision: 3);
        Assert.True(calibration.WasManuallyCalibrated);
        Assert.Equal(CalibrationSource.Manual, calibration.Source);
        Assert.StartsWith("Kalibriert:", result.StatusText);
        Assert.Equal("Kalibriert! DN 300mm = 600px", result.HintText);
    }

    [Fact]
    public void Build_rejects_too_short_reference_line()
    {
        var result = CodingManualCalibrationPolicy.Build(
            start: new NormalizedPoint(0.2, 0.4),
            end: new NormalizedPoint(0.21, 0.4),
            startPixel: new Point(20, 40),
            endPixel: new Point(25, 40),
            nominalDiameterMm: 300);

        Assert.False(result.IsValid);
        Assert.Null(result.Calibration);
        Assert.Equal("Linie zu kurz - bitte nochmal", result.HintText);
    }
}
