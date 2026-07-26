using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;
using AuswertungPro.Next.UI.Ai.Coding;

namespace AuswertungPro.Next.UI.Player;

public static class CodingAiOverlayRenderer
{
    public static int Render(
        Canvas canvas,
        IEnumerable<CodingEvent> events,
        double canvasWidth,
        double canvasHeight,
        NormalizedPoint pipeCenter,
        Func<NormalizedPoint, Point> toPixel)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(pipeCenter);
        ArgumentNullException.ThrowIfNull(toPixel);

        ClearExistingAiOverlays(canvas);

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return 0;

        var aiGlow = CreateAiGlowEffect();
        var rendered = 0;

        foreach (var ev in events)
        {
            if (ev.Overlay is null || ev.AiContext is null)
                continue;

            if (RenderEvent(canvas, ev, canvasWidth, canvasHeight, pipeCenter, toPixel, aiGlow))
                rendered++;
        }

        return rendered;
    }

    private static void ClearExistingAiOverlays(Canvas canvas)
    {
        var toRemove = canvas.Children.OfType<FrameworkElement>()
            .Where(e => CodingOverlayCleanupPolicy.ShouldRemoveAiOverlayTag(e.Tag))
            .ToList();
        foreach (var element in toRemove)
            canvas.Children.Remove(element);
    }

    private static DropShadowEffect CreateAiGlowEffect()
        => new()
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

    private static bool RenderEvent(
        Canvas canvas,
        CodingEvent ev,
        double canvasWidth,
        double canvasHeight,
        NormalizedPoint pipeCenter,
        Func<NormalizedPoint, Point> toPixel,
        DropShadowEffect aiGlow)
    {
        var geo = ev.Overlay!;
        var strokeColor = CodingAiOverlayDisplayPolicy.StrokeColor(ev.AiContext!.Decision);
        Brush stroke = new SolidColorBrush(strokeColor);
        var primitiveStyle = new CodingAiPrimitiveOverlayRenderStyle(stroke, aiGlow, OverlayTags.AiOverlay);
        var rectangleStyle = new CodingAiRectangleOverlayRenderStyle(stroke, strokeColor, aiGlow, OverlayTags.AiOverlay);

        return geo.ToolType switch
        {
            OverlayToolType.Line or OverlayToolType.Stretch or OverlayToolType.Point
                => CodingAiPrimitiveOverlayRenderer.Render(canvas, geo, canvasWidth, canvasHeight, primitiveStyle),

            OverlayToolType.Rectangle
                => CodingAiRectangleOverlayRenderer.Render(
                    canvas,
                    geo,
                    canvasWidth,
                    canvasHeight,
                    ev.Entry.Code,
                    ev.AiContext.Confidence,
                    rectangleStyle),

            OverlayToolType.Arc when geo.Points.Count >= 2
                => CodingArcOverlayRenderer.Render(
                    canvas,
                    geo.Points[0],
                    geo.Points[1],
                    pipeCenter,
                    stroke,
                    aiGlow,
                    OverlayTags.AiOverlay,
                    dashed: true,
                    toPixel),

            OverlayToolType.PipeBend
                => CodingPipeBendOverlayRenderer.Render(
                    canvas,
                    geo,
                    isPreview: true,
                    aiGlow,
                    OverlayTags.AiOverlay,
                    OverlayTags.Measure,
                    toPixel),

            OverlayToolType.LateralCircle
                => CodingLateralCircleOverlayRenderer.Render(
                    canvas,
                    geo,
                    isPreview: true,
                    aiGlow,
                    OverlayTags.AiOverlay,
                    OverlayTags.Measure,
                    toPixel),

            OverlayToolType.Ruler
                => CodingRulerOverlayRenderer.Render(
                    canvas,
                    geo,
                    isPreview: true,
                    aiGlow,
                    OverlayTags.AiOverlay,
                    OverlayTags.Measure,
                    toPixel,
                    labelAnchor: null),

            _ => false
        };
    }
}
