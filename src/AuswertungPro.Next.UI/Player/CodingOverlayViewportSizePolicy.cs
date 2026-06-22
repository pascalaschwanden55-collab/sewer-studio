namespace AuswertungPro.Next.UI.Player;

public readonly record struct CodingOverlayViewportSizeUpdate(
    bool IsValid,
    double? Width,
    double? Height);

public static class CodingOverlayViewportSizePolicy
{
    private const double ResizeTolerance = 0.5;

    public static CodingOverlayViewportSizeUpdate Build(
        double videoWidth,
        double videoHeight,
        double canvasWidth,
        double canvasHeight)
    {
        if (!IsUsableVideoDimension(videoWidth) || !IsUsableVideoDimension(videoHeight))
            return new CodingOverlayViewportSizeUpdate(false, null, null);

        return new CodingOverlayViewportSizeUpdate(
            IsValid: true,
            Width: Math.Abs(canvasWidth - videoWidth) > ResizeTolerance ? videoWidth : null,
            Height: Math.Abs(canvasHeight - videoHeight) > ResizeTolerance ? videoHeight : null);
    }

    private static bool IsUsableVideoDimension(double value)
        => !double.IsNaN(value)
           && !double.IsInfinity(value)
           && value > 1;
}
