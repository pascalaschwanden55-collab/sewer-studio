using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai.Coding;

public readonly record struct CodingOsdMeterState(
    double Meter,
    double? TimestampSeconds,
    string BadgeText);

public static class CodingOsdMeterStateWorkflow
{
    public static CodingOsdMeterState? FromReadResult(
        CodingOsdMeterReadResult result,
        double? frameTimestampSeconds)
    {
        if (!result.Meter.HasValue)
            return null;

        return Build(result.Meter.Value, frameTimestampSeconds);
    }

    public static CodingOsdMeterState? FromDetectionResult(LiveDetection result)
    {
        if (!CodingResultMeterReadingPolicy.TryAccept(result, out var reading))
            return null;

        return Build(reading.Meter, reading.TimestampSeconds);
    }

    private static CodingOsdMeterState Build(double meter, double? timestampSeconds)
        => new(meter, timestampSeconds, CodingOsdBadgeDisplayPolicy.BuildMeterText(meter));
}
