namespace AuswertungPro.Next.UI.DataPage;

public sealed record DataPageGridZoomResult(bool Handled, double NextZoom);

public static class DataPageGridZoomController
{
    private const double Step = 0.05d;
    private const double MinZoom = 0.5d;
    private const double MaxZoom = 2.0d;
    private const double Epsilon = 0.001d;

    public static DataPageGridZoomResult Resolve(
        double currentZoom,
        int wheelDelta,
        bool hasControlModifier)
    {
        if (!hasControlModifier)
            return new DataPageGridZoomResult(false, currentZoom);

        var delta = wheelDelta > 0 ? Step : -Step;
        var next = Math.Clamp(currentZoom + delta, MinZoom, MaxZoom);
        var handled = Math.Abs(next - currentZoom) >= Epsilon;
        return new DataPageGridZoomResult(handled, next);
    }
}
