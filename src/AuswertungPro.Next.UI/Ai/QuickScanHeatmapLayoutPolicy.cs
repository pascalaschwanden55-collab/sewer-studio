using AuswertungPro.Next.Application.Ai;

namespace AuswertungPro.Next.UI.Ai;

public sealed record QuickScanHeatmapSegmentLayout(double Left, double Width);

public static class QuickScanHeatmapLayoutPolicy
{
    public const double FrameStepSeconds = 5.0;
    private const double MinSegmentWidth = 2.0;

    public static QuickScanHeatmapSegmentLayout CalculateSegmentLayout(
        double timestampSeconds,
        double videoDurationSeconds,
        double trackOffsetX,
        double trackWidth)
    {
        if (videoDurationSeconds <= 0 || trackWidth <= 0)
            return new QuickScanHeatmapSegmentLayout(trackOffsetX, 0);

        var width = Math.Max(MinSegmentWidth, FrameStepSeconds / videoDurationSeconds * trackWidth);
        var ratio = Math.Clamp(timestampSeconds / videoDurationSeconds, 0.0, 1.0);
        return new QuickScanHeatmapSegmentLayout(trackOffsetX + ratio * trackWidth, width);
    }

    public static double EstimateDuration(IEnumerable<QuickScanSegment> segments)
    {
        var duration = 0.0;
        foreach (var segment in segments)
            duration = Math.Max(duration, segment.TimestampSeconds + FrameStepSeconds);
        return duration;
    }
}
