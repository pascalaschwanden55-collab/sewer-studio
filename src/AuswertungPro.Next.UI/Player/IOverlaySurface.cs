using System.Windows.Controls;

namespace AuswertungPro.Next.UI.Player;

public interface IOverlaySurface
{
    Canvas Canvas { get; }
    double Width { get; }
    double Height { get; }
    void ClearTransient(bool clearManualOverlay);
}

public sealed class CanvasOverlaySurface : IOverlaySurface
{
    private readonly Canvas _canvas;

    public CanvasOverlaySurface(Canvas canvas)
        => _canvas = canvas ?? throw new ArgumentNullException(nameof(canvas));

    public Canvas Canvas => _canvas;
    public double Width => _canvas.ActualWidth;
    public double Height => _canvas.ActualHeight;

    public void ClearTransient(bool clearManualOverlay)
        => CodingOverlayCanvasCleaner.ClearTransient(_canvas, clearManualOverlay);
}
