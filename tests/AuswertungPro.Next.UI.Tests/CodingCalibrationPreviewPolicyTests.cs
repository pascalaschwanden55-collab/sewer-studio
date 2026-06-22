using System.Windows;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingCalibrationPreviewPolicyTests
{
    [Fact]
    public void Build_keeps_line_points_for_rendering()
    {
        var state = CodingCalibrationPreviewPolicy.Build(
            new Point(20, 40),
            new Point(120, 90));

        Assert.Equal(20, state.Start.X);
        Assert.Equal(40, state.Start.Y);
        Assert.Equal(120, state.End.X);
        Assert.Equal(90, state.End.Y);
    }

    [Fact]
    public void Build_formats_reference_line_pixel_length()
    {
        var state = CodingCalibrationPreviewPolicy.Build(
            new Point(20, 40),
            new Point(23, 44));

        Assert.Equal(5, state.PixelLength);
        Assert.Equal("Referenzlinie: 5 px", state.HintText);
    }
}
