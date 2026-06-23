using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Effects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Player;

public static class CodingOverlayGeometryRenderer
{
    public static bool Render(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        NormalizedPoint? labelAnchor,
        Func<NormalizedPoint, Point> toPixel,
        PipeCalibration? calibration,
        double canvasWidth,
        double canvasHeight)
    {
        ArgumentNullException.ThrowIfNull(canvas);
        ArgumentNullException.ThrowIfNull(overlay);
        ArgumentNullException.ThrowIfNull(toPixel);

        if (canvasWidth <= 0 || canvasHeight <= 0)
            return false;

        var tag = isPreview ? OverlayTags.Preview : OverlayTags.Manual;
        var stroke = isPreview
            ? Brushes.Lime
            : new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
        var fill = isPreview
            ? new SolidColorBrush(Color.FromArgb(50, 0x00, 0xFF, 0xFF))
            : new SolidColorBrush(Color.FromArgb(35, 0x00, 0xE5, 0xFF));
        var glowEffect = CreateGlowEffect();

        if (!RenderShape(canvas, overlay, isPreview, labelAnchor, toPixel, calibration, canvasWidth, canvasHeight, tag, stroke, fill, glowEffect))
            return false;

        AddMeasurementLabel(canvas, overlay, isPreview, labelAnchor, toPixel, glowEffect);
        return true;
    }

    private static DropShadowEffect CreateGlowEffect()
        => new()
        {
            Color = Colors.Black,
            BlurRadius = 6,
            ShadowDepth = 0,
            Opacity = 0.9
        };

    private static bool RenderShape(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        NormalizedPoint? labelAnchor,
        Func<NormalizedPoint, Point> toPixel,
        PipeCalibration? calibration,
        double canvasWidth,
        double canvasHeight,
        string tag,
        Brush stroke,
        Brush fill,
        DropShadowEffect glowEffect)
    {
        switch (overlay.ToolType)
        {
            case OverlayToolType.Line:
            case OverlayToolType.Stretch:
                return CodingBasicOverlayRenderer.Render(
                    canvas,
                    overlay,
                    toPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, stroke, Brushes.Transparent, glowEffect, tag));

            case OverlayToolType.Rectangle:
                return CodingBasicOverlayRenderer.Render(
                    canvas,
                    overlay,
                    toPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, stroke, fill, glowEffect, tag));

            case OverlayToolType.Point:
                return CodingBasicOverlayRenderer.Render(
                    canvas,
                    overlay,
                    toPixel,
                    new CodingBasicOverlayRenderStyle(false, stroke, Brushes.Transparent, glowEffect, tag));

            case OverlayToolType.Arc:
                return overlay.Points.Count >= 2
                       && CodingArcOverlayRenderer.Render(
                           canvas,
                           overlay.Points[0],
                           overlay.Points[1],
                           calibration?.PipeCenter ?? new NormalizedPoint(0.5, 0.5),
                           stroke,
                           glowEffect,
                           tag,
                           isPreview,
                           toPixel);

            case OverlayToolType.PipeBend:
                return CodingPipeBendOverlayRenderer.Render(
                    canvas,
                    overlay,
                    isPreview,
                    glowEffect,
                    tag,
                    isPreview ? OverlayTags.Measure : OverlayTags.Manual,
                    toPixel);

            case OverlayToolType.LateralCircle:
                return CodingLateralCircleOverlayRenderer.Render(
                    canvas,
                    overlay,
                    isPreview,
                    glowEffect,
                    tag,
                    isPreview ? OverlayTags.Measure : OverlayTags.Manual,
                    toPixel);

            case OverlayToolType.Ruler:
                return CodingRulerOverlayRenderer.Render(
                    canvas,
                    overlay,
                    isPreview,
                    glowEffect,
                    tag,
                    isPreview ? OverlayTags.Measure : OverlayTags.Manual,
                    toPixel,
                    labelAnchor);

            case OverlayToolType.Level:
                return CodingLevelOverlayRenderer.Render(
                    canvas,
                    overlay,
                    isPreview,
                    glowEffect,
                    tag,
                    toPixel,
                    calibration,
                    canvasWidth,
                    canvasHeight);

            case OverlayToolType.Ellipse:
            case OverlayToolType.Freehand:
                return CodingBasicOverlayRenderer.Render(
                    canvas,
                    overlay,
                    toPixel,
                    new CodingBasicOverlayRenderStyle(isPreview, Brushes.Transparent, Brushes.Transparent, glowEffect, tag));

            default:
                return false;
        }
    }

    private static void AddMeasurementLabel(
        Canvas canvas,
        OverlayGeometry overlay,
        bool isPreview,
        NormalizedPoint? labelAnchor,
        Func<NormalizedPoint, Point> toPixel,
        Effect glowEffect)
    {
        if (overlay.ToolType is OverlayToolType.PipeBend
            or OverlayToolType.LateralCircle
            or OverlayToolType.Ruler
            or OverlayToolType.Level)
        {
            return;
        }

        var text = CodingOverlayMeasurementFormatter.BuildOverlayMeasurementText(overlay);
        if (string.IsNullOrWhiteSpace(text))
            return;

        var anchorNorm = labelAnchor ?? overlay.Points.LastOrDefault() ?? new NormalizedPoint(0.5, 0.5);
        CodingOverlayMeasurementLabelRenderer.Add(
            canvas,
            toPixel(anchorNorm),
            text,
            glowEffect,
            isPreview ? OverlayTags.Measure : OverlayTags.Manual);
    }
}
