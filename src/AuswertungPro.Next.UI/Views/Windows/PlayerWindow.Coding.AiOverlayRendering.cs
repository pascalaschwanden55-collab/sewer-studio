using System;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    // --- KI-Overlays rendern (orange, gestrichelt) ---

    private void RenderAiOverlays()
    {
        if (_codingVm == null) return;

        // Bestehende KI-Overlays entfernen (Tags beginnen mit "ai_")
        var toRemove = CodingOverlayCanvas.Children.OfType<FrameworkElement>()
            .Where(e => CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag(e.Tag))
            .ToList();
        foreach (var el in toRemove)
            CodingOverlayCanvas.Children.Remove(el);

        var aiGlow = new System.Windows.Media.Effects.DropShadowEffect
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

        double w = CodingOverlayCanvas.ActualWidth;
        double h = CodingOverlayCanvas.ActualHeight;
        if (w <= 0 || h <= 0) return;

        foreach (var ev in _codingVm.Events)
        {
            if (ev.Overlay == null || ev.AiContext == null) continue;
            var geo = ev.Overlay;

            var strokeColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext.Decision);
            Brush stroke = new SolidColorBrush(strokeColor);
            var primitiveStyle = new CodingAiPrimitiveOverlayRenderStyle(stroke, aiGlow, OverlayTags.AiOverlay);
            var rectangleStyle = new CodingAiRectangleOverlayRenderStyle(stroke, strokeColor, aiGlow, OverlayTags.AiOverlay);

            switch (geo.ToolType)
            {
                case OverlayToolType.Line:
                case OverlayToolType.Stretch:
                    CodingAiPrimitiveOverlayRenderer.Render(CodingOverlayCanvas, geo, w, h, primitiveStyle);
                    break;

                case OverlayToolType.Rectangle:
                    CodingAiRectangleOverlayRenderer.Render(
                        CodingOverlayCanvas,
                        geo,
                        w,
                        h,
                        ev.Entry.Code,
                        ev.AiContext.Confidence,
                        rectangleStyle);
                    break;

                case OverlayToolType.Point:
                    CodingAiPrimitiveOverlayRenderer.Render(CodingOverlayCanvas, geo, w, h, primitiveStyle);
                    break;

                case OverlayToolType.Arc:
                    if (geo.Points.Count >= 2)
                    {
                        CodingArcOverlayRenderer.Render(
                            CodingOverlayCanvas,
                            geo.Points[0],
                            geo.Points[1],
                            _codingOverlayService?.Calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5),
                            stroke,
                            aiGlow,
                            OverlayTags.AiOverlay,
                            dashed: true,
                            CodingNormToPixel);
                    }
                    break;

                case OverlayToolType.PipeBend:
                    RenderPipeBendOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;

                case OverlayToolType.LateralCircle:
                    RenderLateralCircleOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;

                case OverlayToolType.Ruler:
                    RenderRulerOverlay(geo, true, stroke, aiGlow, OverlayTags.AiOverlay, null);
                    break;
            }
        }
    }
}
