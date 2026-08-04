using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using AuswertungPro.Next.Application.Ai.Workbench;

namespace AuswertungPro.Next.UI.Ai.Pipeline;

/// <summary>
/// Zeichnet die SAM-Maske im Pruefplatz deutlich sichtbar als Flaeche und Kontur.
/// </summary>
internal static class TrainingStudioMaskOverlayRenderer
{
    internal sealed record RenderResult(bool Rendered, string? ErrorMessage);

    public static RenderResult Render(
        Canvas canvas,
        WorkbenchSegmentation segmentation,
        Rect imageArea,
        bool isValidForGold = true)
    {
        if (string.IsNullOrWhiteSpace(segmentation.MaskRle))
            return new RenderResult(false, null);

        if (segmentation.MaskImageWidth <= 0
            || segmentation.MaskImageHeight <= 0
            || imageArea.Width <= 0
            || imageArea.Height <= 0)
        {
            return new RenderResult(false, "Segmentierung ist vorhanden, aber ihre Bildgroesse ist ungueltig.");
        }

        try
        {
            var mask = SamMaskRenderer.DecodeRle(
                segmentation.MaskRle,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight);
            if (!HasForeground(mask))
                return new RenderResult(false, "SAM hat eine leere Maske geliefert. Bitte die Box neu ziehen.");

            var transform = new TranslateTransform(imageArea.X, imageArea.Y);
            var fillGeometry = SamMaskRenderer.ExtractFillGeometry(
                mask,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                imageArea.Width,
                imageArea.Height);
            var overlayColor = isValidForGold
                ? Color.FromRgb(0, 255, 80)
                : Colors.Orange;
            canvas.Children.Add(new Path
            {
                Data = fillGeometry,
                Fill = new SolidColorBrush(Color.FromArgb(
                    72,
                    overlayColor.R,
                    overlayColor.G,
                    overlayColor.B)),
                IsHitTestVisible = false,
                RenderTransform = transform,
            });

            var contourGeometry = SamMaskRenderer.ExtractContourGeometry(
                mask,
                segmentation.MaskImageWidth,
                segmentation.MaskImageHeight,
                imageArea.Width,
                imageArea.Height);
            canvas.Children.Add(new Path
            {
                Data = contourGeometry,
                Stroke = new SolidColorBrush(overlayColor),
                StrokeThickness = 3,
                IsHitTestVisible = false,
                RenderTransform = transform,
            });

            return new RenderResult(true, null);
        }
        catch
        {
            return new RenderResult(false, "Segmentierung wurde erstellt, kann aber nicht angezeigt werden.");
        }
    }

    private static bool HasForeground(bool[,] mask)
    {
        for (var row = 0; row < mask.GetLength(0); row++)
        {
            for (var column = 0; column < mask.GetLength(1); column++)
            {
                if (mask[row, column])
                    return true;
            }
        }

        return false;
    }
}
