using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingAutoCalibrationFrameServiceTests
{
    [Theory]
    [InlineData(null)]
    [InlineData(new byte[0])]
    public void TryAutoCalibrate_returns_null_without_loading_when_frame_bytes_are_missing(byte[]? frameBytes)
    {
        var result = CodingAutoCalibrationFrameService.TryAutoCalibrate(
            frameBytes,
            nominalDiameterMm: 300,
            _ => throw new InvalidOperationException("bitmap loader should not run"),
            (_, _) => throw new InvalidOperationException("calibration should not run"));

        Assert.Null(result);
    }

    [Fact]
    public void TryAutoCalibrate_loads_bitmap_and_passes_nominal_dn_to_calibration()
    {
        var bitmap = BitmapSource.Create(
            1,
            1,
            96,
            96,
            PixelFormats.Bgra32,
            null,
            new byte[] { 0, 0, 0, 255 },
            4);
        var calibration = new PipeCalibration { NominalDiameterMm = 400, NormalizedDiameter = 0.5 };
        byte[]? loadedBytes = null;
        int? usedDn = null;

        var result = CodingAutoCalibrationFrameService.TryAutoCalibrate(
            [1, 2, 3],
            nominalDiameterMm: 400,
            bytes =>
            {
                loadedBytes = bytes;
                return bitmap;
            },
            (frame, dn) =>
            {
                Assert.Same(bitmap, frame);
                usedDn = dn;
                return calibration;
            });

        Assert.Same(calibration, result);
        Assert.Equal([1, 2, 3], loadedBytes);
        Assert.Equal(400, usedDn);
    }
}
