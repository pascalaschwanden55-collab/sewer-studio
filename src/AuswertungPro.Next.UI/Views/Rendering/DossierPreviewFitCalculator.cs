using System;

namespace AuswertungPro.Next.UI.Views.Rendering;

/// <summary>Berechnet den Zoom, bei dem ein vollständiges Blatt sichtbar bleibt.</summary>
public static class DossierPreviewFitCalculator
{
    public static double Calculate(
        double viewportWidth,
        double viewportHeight,
        double pageWidth,
        double pageHeight,
        double surroundingSpace)
    {
        if (!double.IsFinite(viewportWidth)
            || !double.IsFinite(viewportHeight)
            || !double.IsFinite(pageWidth)
            || !double.IsFinite(pageHeight)
            || viewportWidth <= 0
            || viewportHeight <= 0
            || pageWidth <= 0
            || pageHeight <= 0)
        {
            return 1d;
        }

        var space = double.IsFinite(surroundingSpace)
            ? Math.Max(0, surroundingSpace)
            : 0;
        var availableWidth = Math.Max(1, viewportWidth - space);
        var availableHeight = Math.Max(1, viewportHeight - space);

        return Math.Min(availableWidth / pageWidth, availableHeight / pageHeight);
    }
}
