using System.Windows;
using AuswertungPro.Next.Application.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Reine Koordinatenabbildung zwischen dem sichtbaren Bild, Mauspositionen und
/// normierten Trainingsboxen. Beruecksichtigt freie Raender bei Uniform-Stretch.
/// </summary>
internal static class TrainingStudioImageGeometryMapper
{
    public static Rect GetDisplayedImageRect(
        Size viewportSize,
        Size sourceSize,
        Point viewportOrigin)
    {
        if (viewportSize.Width <= 0
            || viewportSize.Height <= 0
            || sourceSize.Width <= 0
            || sourceSize.Height <= 0)
        {
            return Rect.Empty;
        }

        var scale = Math.Min(
            viewportSize.Width / sourceSize.Width,
            viewportSize.Height / sourceSize.Height);
        var width = sourceSize.Width * scale;
        var height = sourceSize.Height * scale;

        return new Rect(
            viewportOrigin.X + (viewportSize.Width - width) / 2,
            viewportOrigin.Y + (viewportSize.Height - height) / 2,
            width,
            height);
    }

    public static bool TryCreateNormalizedBox(
        Rect imageArea,
        Point dragStart,
        Point dragEnd,
        out BoundingBox box)
    {
        box = default;
        if (imageArea.IsEmpty
            || imageArea.Width <= 0
            || imageArea.Height <= 0
            || !imageArea.Contains(dragStart))
        {
            return false;
        }

        var end = ClampToImage(imageArea, dragEnd);
        var x1 = (Math.Min(dragStart.X, end.X) - imageArea.X) / imageArea.Width;
        var y1 = (Math.Min(dragStart.Y, end.Y) - imageArea.Y) / imageArea.Height;
        var x2 = (Math.Max(dragStart.X, end.X) - imageArea.X) / imageArea.Width;
        var y2 = (Math.Max(dragStart.Y, end.Y) - imageArea.Y) / imageArea.Height;

        var width = x2 - x1;
        var height = y2 - y1;
        if (width < 0.01 || height < 0.01)
            return false;

        return BoundingBox.TryCreate(
            (x1 + x2) / 2,
            (y1 + y2) / 2,
            width,
            height,
            out box);
    }

    public static Point ClampToImage(Rect imageArea, Point point)
    {
        if (imageArea.IsEmpty || imageArea.Width <= 0 || imageArea.Height <= 0)
            return point;

        return new Point(
            Math.Clamp(point.X, imageArea.Left, imageArea.Right),
            Math.Clamp(point.Y, imageArea.Top, imageArea.Bottom));
    }

    public static Rect ToCanvasRect(Rect imageArea, BoundingBox box)
        => new(
            imageArea.X + (box.XCenter - box.Width / 2) * imageArea.Width,
            imageArea.Y + (box.YCenter - box.Height / 2) * imageArea.Height,
            box.Width * imageArea.Width,
            box.Height * imageArea.Height);
}
