using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai.Coding;

public sealed record CodingPipeProximityCalibration(
    double VanishX,
    double VanishY,
    double PipeRadiusNorm);

public static class CodingPipeProximityCalibrationPolicy
{
    public static CodingPipeProximityCalibration Resolve(PipeCalibration? calibration)
    {
        var vanishX = calibration?.PipeCenter.X ?? 0.5;
        var vanishY = calibration?.PipeCenter.Y ?? 0.5;
        var pipeRadius = calibration is { NormalizedDiameter: > 0 }
            ? calibration.NormalizedDiameter / 2.0
            : 0.5;

        return new CodingPipeProximityCalibration(vanishX, vanishY, pipeRadius);
    }
}
