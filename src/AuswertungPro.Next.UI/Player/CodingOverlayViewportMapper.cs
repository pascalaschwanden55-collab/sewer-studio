using System.Windows;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayViewportMapper
{
    public static Rect GetContentRect(double canvasWidth, double canvasHeight, double videoAspect)
    {
        if (canvasWidth <= 0 || canvasHeight <= 0 || videoAspect <= 0)
            return new Rect(0, 0, Math.Max(0, canvasWidth), Math.Max(0, canvasHeight));

        var canvasAspect = canvasWidth / canvasHeight;
        if (videoAspect > canvasAspect)
        {
            var contentHeight = canvasWidth / videoAspect;
            return new Rect(0, (canvasHeight - contentHeight) / 2.0, canvasWidth, contentHeight);
        }

        var contentWidth = canvasHeight * videoAspect;
        return new Rect((canvasWidth - contentWidth) / 2.0, 0, contentWidth, canvasHeight);
    }

    public static NormalizedPoint PixelToNorm(Point pixel, Rect contentRect)
    {
        if (contentRect.Width <= 0 || contentRect.Height <= 0)
            return new NormalizedPoint(0.5, 0.5);

        return new NormalizedPoint(
            (pixel.X - contentRect.X) / contentRect.Width,
            (pixel.Y - contentRect.Y) / contentRect.Height);
    }

    public static Point NormToPixel(NormalizedPoint norm, Rect contentRect)
        => new(
            contentRect.X + norm.X * contentRect.Width,
            contentRect.Y + norm.Y * contentRect.Height);
}
