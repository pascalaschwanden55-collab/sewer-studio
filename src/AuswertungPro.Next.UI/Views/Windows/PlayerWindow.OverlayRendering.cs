using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderOverlayGeometry(OverlayGeometry overlay, bool isPreview, NormalizedPoint? labelAnchor = null)
    {
        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        string tag = isPreview ? OverlayTags.Preview : OverlayTags.Manual;
        var stroke = isPreview
            ? Brushes.Lime
            : new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
        var fill = isPreview
            ? new SolidColorBrush(Color.FromArgb(50, 0x00, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(35, 0x00, 0xE5, 0xFF));
        var glowEffect = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        switch (overlay.ToolType)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
                CodingBasicOverlayRenderer.Render(
                    CodingOverlayCanvas,
                    overlay,
                    CodingNormToPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, stroke, Brushes.Transparent, glowEffect, tag));
                break;

            case OverlayToolType.Rectangle:
                CodingBasicOverlayRenderer.Render(
                    CodingOverlayCanvas,
                    overlay,
                    CodingNormToPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, stroke, fill, glowEffect, tag));
                break;

            case OverlayToolType.Point:
                CodingBasicOverlayRenderer.Render(
                    CodingOverlayCanvas,
                    overlay,
                    CodingNormToPixel,
                    new CodingBasicOverlayRenderStyle(false, stroke, Brushes.Transparent, glowEffect, tag));
                break;

            case OverlayToolType.Arc:
                if (overlay.Points.Count >= 2)
                {
                    CodingArcOverlayRenderer.Render(
                        CodingOverlayCanvas,
                        overlay.Points[0],
                        overlay.Points[1],
                        _codingOverlayService?.Calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5),
                        stroke,
                        glowEffect,
                        tag,
                        isPreview,
                        CodingNormToPixel);
                }
                break;

            case OverlayToolType.PipeBend:
                RenderPipeBendOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.LateralCircle:
                RenderLateralCircleOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ruler:
                RenderRulerOverlay(overlay, isPreview, stroke, glowEffect, tag, labelAnchor);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Level:
                RenderLevelOverlay(overlay, isPreview, glowEffect, tag);
                return; // Eigenes Label-Rendering

            case OverlayToolType.Ellipse:
                CodingBasicOverlayRenderer.Render(
                    CodingOverlayCanvas,
                    overlay,
                    CodingNormToPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, Brushes.Transparent, Brushes.Transparent, glowEffect, tag));
                break;

            case OverlayToolType.Freehand:
                CodingBasicOverlayRenderer.Render(
                    CodingOverlayCanvas,
                    overlay,
                    CodingNormToPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, Brushes.Transparent, Brushes.Transparent, glowEffect, tag));
                break;
        }

        var text = CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(overlay);
        if (!string.IsNullOrWhiteSpace(text))
        {
            var anchorNorm = labelAnchor ?? overlay.Points.LastOrDefault() ?? new NormalizedPoint(0.5, 0.5);
            var anchor = CodingNormToPixel(anchorNorm);

            CodingOverlayMeasurementLabelRenderer.Add(
                CodingOverlayCanvas,
                anchor,
                text,
                glowEffect,
                isPreview ? OverlayTags.Measure : OverlayTags.Manual);
        }
    }
}
