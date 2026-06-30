namespace AuswertungPro.Next.UI.Controls;

internal readonly record struct TimelineScaleTick(
    string Text,
    double Left,
    bool AlignRight);

internal static class TimelineScaleCalculator
{
    public const double FallbackCanvasWidth = 400d;

    public static double EffectiveCanvasWidth(double actualWidth)
        => actualWidth > 0 ? actualWidth : FallbackCanvasWidth;

    public static double ChooseInterval(double totalLength)
    {
        if (totalLength <= 0)
            return 0;

        return totalLength switch
        {
            <= 10 => 2,
            <= 25 => 5,
            <= 50 => 10,
            <= 100 => 20,
            <= 250 => 50,
            _ => 100
        };
    }

    public static double MeterToX(double meter, double totalLength, double canvasWidth)
    {
        if (totalLength <= 0)
            return 0;

        var width = EffectiveCanvasWidth(canvasWidth);
        return Math.Clamp(meter / totalLength, 0, 1) * width;
    }

    public static double? XToMeter(double x, double totalLength, double canvasWidth)
    {
        if (totalLength <= 0 || canvasWidth <= 0)
            return null;

        return Math.Clamp((x / canvasWidth) * totalLength, 0, totalLength);
    }

    public static IReadOnlyList<TimelineScaleTick> BuildTicks(double totalLength, double canvasWidth)
    {
        if (totalLength <= 0)
            return [];

        var width = EffectiveCanvasWidth(canvasWidth);
        var interval = ChooseInterval(totalLength);
        if (interval <= 0)
            return [];

        var ticks = new List<TimelineScaleTick>();
        for (double meter = 0; meter <= totalLength; meter += interval)
        {
            if (Math.Abs(meter - totalLength) < 0.01 || meter + interval > totalLength)
            {
                ticks.Add(new TimelineScaleTick($"{totalLength:F1}m", Left: 0, AlignRight: true));
                continue;
            }

            var x = (meter / totalLength) * width;
            ticks.Add(new TimelineScaleTick($"{meter:F0}m", Math.Max(0, x - 8), AlignRight: false));
        }

        ticks.Insert(0, new TimelineScaleTick("0m", Left: 0, AlignRight: false));
        return ticks;
    }
}
