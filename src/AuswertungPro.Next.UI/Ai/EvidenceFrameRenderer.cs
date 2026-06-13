using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai;

public sealed record EvidenceFrameAnnotation(
    string Code,
    double? Confidence,
    double? BboxXCenter,
    double? BboxYCenter,
    double? BboxWidth,
    double? BboxHeight)
{
    public bool HasBbox =>
        BboxXCenter.HasValue
        && BboxYCenter.HasValue
        && BboxWidth.HasValue
        && BboxHeight.HasValue
        && BboxWidth.Value > 0
        && BboxHeight.Value > 0;
}

public static class EvidenceFrameRenderer
{
    public static bool SaveAnnotatedFrame(
        string sourceImagePath,
        string outputImagePath,
        EvidenceFrameAnnotation annotation)
    {
        ArgumentNullException.ThrowIfNull(annotation);

        if (string.IsNullOrWhiteSpace(sourceImagePath) ||
            string.IsNullOrWhiteSpace(outputImagePath) ||
            !File.Exists(sourceImagePath))
        {
            return false;
        }

        var image = LoadBitmap(sourceImagePath);
        if (image.PixelWidth <= 0 || image.PixelHeight <= 0)
            return false;

        var visual = new DrawingVisual();
        var width = image.PixelWidth;
        var height = image.PixelHeight;

        using (var dc = visual.RenderOpen())
        {
            dc.DrawImage(image, new Rect(0, 0, width, height));

            var labelAnchor = new Point(10, 10);
            if (TryBuildBbox(annotation, width, height, out var box))
            {
                var stroke = new Pen(new SolidColorBrush(Color.FromRgb(0, 220, 80)), 4);
                stroke.Freeze();
                var fill = new SolidColorBrush(Color.FromArgb(42, 0, 220, 80));
                fill.Freeze();
                dc.DrawRectangle(fill, stroke, box);
                labelAnchor = new Point(box.Left, Math.Max(4, box.Top - 28));
            }

            DrawLabel(dc, BuildLabel(annotation), labelAnchor, width);
        }

        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);

        var directory = Path.GetDirectoryName(outputImagePath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(outputImagePath);
        encoder.Save(stream);
        return true;
    }

    private static BitmapImage LoadBitmap(string path)
    {
        var image = new BitmapImage();
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.UriSource = new Uri(path, UriKind.Absolute);
        image.EndInit();
        image.Freeze();
        return image;
    }

    private static bool TryBuildBbox(EvidenceFrameAnnotation annotation, int imageWidth, int imageHeight, out Rect box)
    {
        box = Rect.Empty;
        if (!annotation.HasBbox)
            return false;

        var xCenter = Clamp01(annotation.BboxXCenter!.Value) * imageWidth;
        var yCenter = Clamp01(annotation.BboxYCenter!.Value) * imageHeight;
        var width = Clamp01(annotation.BboxWidth!.Value) * imageWidth;
        var height = Clamp01(annotation.BboxHeight!.Value) * imageHeight;
        if (width < 2 || height < 2)
            return false;

        var left = Math.Clamp(xCenter - width / 2, 0, imageWidth - 1);
        var top = Math.Clamp(yCenter - height / 2, 0, imageHeight - 1);
        var right = Math.Clamp(xCenter + width / 2, left + 1, imageWidth);
        var bottom = Math.Clamp(yCenter + height / 2, top + 1, imageHeight);

        box = new Rect(left, top, right - left, bottom - top);
        return true;
    }

    private static void DrawLabel(DrawingContext dc, string label, Point anchor, int imageWidth)
    {
        var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);
        var text = new FormattedText(
            label,
            CultureInfo.InvariantCulture,
            FlowDirection.LeftToRight,
            typeface,
            15,
            Brushes.White,
            pixelsPerDip: 1.0)
        {
            MaxTextWidth = Math.Max(32, imageWidth - 16),
            MaxLineCount = 1,
            Trimming = TextTrimming.CharacterEllipsis
        };

        var x = Math.Clamp(anchor.X, 4, Math.Max(4, imageWidth - text.Width - 12));
        var y = Math.Max(4, anchor.Y);
        var background = new SolidColorBrush(Color.FromArgb(210, 12, 22, 18));
        background.Freeze();
        dc.DrawRoundedRectangle(
            background,
            null,
            new Rect(x - 5, y - 4, text.Width + 10, text.Height + 8),
            3,
            3);
        dc.DrawText(text, new Point(x, y));
    }

    private static string BuildLabel(EvidenceFrameAnnotation annotation)
    {
        var code = string.IsNullOrWhiteSpace(annotation.Code) ? "Befund" : annotation.Code.Trim();
        return annotation.Confidence.HasValue
            ? string.Format(CultureInfo.InvariantCulture, "{0} {1:P0}", code, annotation.Confidence.Value)
            : code;
    }

    private static double Clamp01(double value)
        => Math.Min(1, Math.Max(0, value));
}
