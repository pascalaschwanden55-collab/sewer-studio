namespace AuswertungPro.Next.UI.Player;

public readonly record struct TimelineRangeX(double StartX, double EndX, double Width);

public static class PlayerTimelineLayoutCalculator
{
    public static double CalculatePointX(double meter, double pipeLength, double offsetX, double trackWidth)
    {
        if (pipeLength <= 0 || trackWidth <= 0)
        {
            return offsetX;
        }

        var ratio = Math.Clamp(meter / pipeLength, 0.0, 1.0);
        return offsetX + ratio * trackWidth;
    }

    public static TimelineRangeX CalculateRangeX(double startMeter, double endMeter, double pipeLength, double offsetX, double trackWidth)
    {
        var startX = CalculatePointX(startMeter, pipeLength, offsetX, trackWidth);
        var endX = CalculatePointX(endMeter, pipeLength, offsetX, trackWidth);

        if (endX < startX)
        {
            (startX, endX) = (endX, startX);
        }

        return new TimelineRangeX(startX, endX, Math.Max(0, endX - startX));
    }
}
