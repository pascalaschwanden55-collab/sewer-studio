using AuswertungPro.Next.Infrastructure.Ai.Calibration;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class AutoCalibrationServiceTests
{
    [Fact]
    public void TryAutoCalibrate_DetectsPipeDiameterFromGrayscaleFrame()
    {
        const int width = 200;
        const int height = 120;
        var pixels = Enumerable.Repeat((byte)20, width * height).ToArray();

        for (var y = 0; y < height; y++)
        {
            var row = y * width;
            for (var x = 50; x <= 150; x++)
                pixels[row + x] = 220;
        }

        var calibration = AutoCalibrationService.TryAutoCalibrate(
            new GrayscaleImageFrame(width, height, pixels),
            nominalDiameterMm: 300);

        Assert.NotNull(calibration);
        Assert.Equal(300, calibration.NominalDiameterMm);
        Assert.InRange(calibration.PipePixelDiameter, 98, 104);
        Assert.InRange(calibration.NormalizedDiameter, 0.49, 0.52);
        Assert.InRange(calibration.PipeCenter.X, 0.49, 0.51);
    }
}
