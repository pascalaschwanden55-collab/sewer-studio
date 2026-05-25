using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Ai;

public sealed record BgraImageFrame(int Width, int Height, byte[] Pixels);

public sealed record TechniqueAssessment(
    bool OsdReadable,
    double? OsdDeltaMeters,
    string LightingQuality,
    string SharpnessQuality,
    string? CenteringQuality,
    string OverallGrade,
    double MeanLuminance,
    double LaplacianVariance);

public interface ITechniqueAssessmentService
{
    TechniqueAssessment AssessFrame(byte[] pngBytes, double? osdMeterReading, double protocolMeter);

    Task<TechniqueAssessment> AssessFrameWithVisionAsync(
        byte[] pngBytes,
        double? osdMeterReading,
        double protocolMeter,
        CancellationToken ct);
}
