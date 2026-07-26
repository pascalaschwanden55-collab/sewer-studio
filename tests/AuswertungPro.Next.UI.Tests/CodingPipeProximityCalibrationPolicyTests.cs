using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Tests;

public sealed class CodingPipeProximityCalibrationPolicyTests
{
    [Fact]
    public void Resolve_uses_default_center_and_radius_without_calibration()
    {
        var state = CodingPipeProximityCalibrationPolicy.Resolve(null);

        Assert.Equal(0.5, state.VanishX);
        Assert.Equal(0.5, state.VanishY);
        Assert.Equal(0.5, state.PipeRadiusNorm);
    }

    [Fact]
    public void Resolve_uses_calibrated_center_and_half_normalized_diameter()
    {
        var calibration = new PipeCalibration
        {
            PipeCenter = new NormalizedPoint(0.42, 0.61),
            NormalizedDiameter = 0.7
        };

        var state = CodingPipeProximityCalibrationPolicy.Resolve(calibration);

        Assert.Equal(0.42, state.VanishX);
        Assert.Equal(0.61, state.VanishY);
        Assert.Equal(0.35, state.PipeRadiusNorm);
    }

    [Fact]
    public void Resolve_keeps_center_but_uses_default_radius_for_invalid_diameter()
    {
        var calibration = new PipeCalibration
        {
            PipeCenter = new NormalizedPoint(0.42, 0.61),
            NormalizedDiameter = 0
        };

        var state = CodingPipeProximityCalibrationPolicy.Resolve(calibration);

        Assert.Equal(0.42, state.VanishX);
        Assert.Equal(0.61, state.VanishY);
        Assert.Equal(0.5, state.PipeRadiusNorm);
    }
}
