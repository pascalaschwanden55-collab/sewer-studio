using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.PhotoMeasurement;

internal interface IPhotoMeasurementOverlayExporter
{
    string? Export(
        BitmapSource? photo,
        Visual overlay,
        Rect renderedImageRect,
        string photoPath);
}

/// <summary>
/// Rendert das sichtbare Mess-Overlay synchron in Original-Pixelgroesse und
/// speichert es als abgeleitete PNG-Datei neben dem Quellfoto.
/// </summary>
internal sealed class PhotoMeasurementOverlayExporter : IPhotoMeasurementOverlayExporter
{
    public string? Export(
        BitmapSource? photo,
        Visual overlay,
        Rect renderedImageRect,
        string photoPath)
    {
        if (photo is null || renderedImageRect.Width <= 0 || renderedImageRect.Height <= 0)
            return null;

        var outputWidth = photo.PixelWidth;
        var outputHeight = photo.PixelHeight;
        if (outputWidth <= 0 || outputHeight <= 0)
            return null;

        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(photoPath);

        var visual = new DrawingVisual();
        using (var drawing = visual.RenderOpen())
        {
            drawing.DrawImage(photo, new Rect(0, 0, outputWidth, outputHeight));
            var overlayBrush = new VisualBrush(overlay)
            {
                Viewbox = renderedImageRect,
                ViewboxUnits = BrushMappingMode.Absolute,
                Stretch = Stretch.Fill
            };
            drawing.DrawRectangle(
                overlayBrush,
                null,
                new Rect(0, 0, outputWidth, outputHeight));
        }

        var rendered = new RenderTargetBitmap(
            outputWidth,
            outputHeight,
            96,
            96,
            PixelFormats.Pbgra32);
        rendered.Render(visual);

        var outputPath = Path.ChangeExtension(photoPath, null) + "_overlay.png";
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(rendered));
        using var stream = File.Create(outputPath);
        encoder.Save(stream);
        return outputPath;
    }
}
