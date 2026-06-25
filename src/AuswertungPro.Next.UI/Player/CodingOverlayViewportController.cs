namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayViewportController
{
    public static void Update(
        double videoWidth,
        double videoHeight,
        double canvasWidth,
        double canvasHeight,
        Action<double> setCanvasWidth,
        Action<double> setCanvasHeight)
    {
        ArgumentNullException.ThrowIfNull(setCanvasWidth);
        ArgumentNullException.ThrowIfNull(setCanvasHeight);

        var update = CodingOverlayViewportSizePolicy.Build(
            videoWidth,
            videoHeight,
            canvasWidth,
            canvasHeight);

        if (!update.IsValid)
            return;

        if (update.Width.HasValue)
            setCanvasWidth(update.Width.Value);

        if (update.Height.HasValue)
            setCanvasHeight(update.Height.Value);
    }
}
