using System.Windows.Media;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Player;

namespace AuswertungPro.Next.UI.Views.Windows;

public partial class PlayerWindow
{
    private void RenderLineOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        CodingBasicOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            CodingNormToPixel,
            new CodingBasicOverlayRenderStyle(isPreview, stroke, Brushes.Transparent, glowEffect, tag));
    }

    private void RenderRectangleOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        Brush stroke,
        Brush fill,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        CodingBasicOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            CodingNormToPixel,
            new CodingBasicOverlayRenderStyle(isPreview, stroke, fill, glowEffect, tag));
    }

    private void RenderPointOverlay(
        OverlayGeometry overlay,
        Brush stroke,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        CodingBasicOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            CodingNormToPixel,
            new CodingBasicOverlayRenderStyle(false, stroke, Brushes.Transparent, glowEffect, tag));
    }

    private void RenderEllipseOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        CodingBasicOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            CodingNormToPixel,
            new CodingBasicOverlayRenderStyle(isPreview, Brushes.Transparent, Brushes.Transparent, glowEffect, tag));
    }

    private void RenderFreehandOverlay(
        OverlayGeometry overlay,
        bool isPreview,
        System.Windows.Media.Effects.DropShadowEffect glowEffect,
        string tag)
    {
        CodingBasicOverlayRenderer.Render(
            CodingOverlayCanvas,
            overlay,
            CodingNormToPixel,
            new CodingBasicOverlayRenderStyle(isPreview, Brushes.Transparent, Brushes.Transparent, glowEffect, tag));
    }
}
