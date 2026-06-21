using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingEvidenceAnnotationBuilder
{
    public static EvidenceFrameAnnotation Build(CodingEvent ev)
    {
        var (xCenter, yCenter, width, height) = ExtractBbox(ev.Overlay);
        return new EvidenceFrameAnnotation(
            ev.Entry.Code,
            ev.AiContext?.Confidence,
            xCenter,
            yCenter,
            width,
            height,
            ev.AiContext?.SamMaskRle,
            ev.AiContext?.SamMaskImageWidth,
            ev.AiContext?.SamMaskImageHeight);
    }

    public static (double? XCenter, double? YCenter, double? Width, double? Height) ExtractBbox(OverlayGeometry? overlay)
    {
        if (overlay?.Points == null || overlay.Points.Count < 2)
            return (null, null, null, null);

        var minX = overlay.Points.Min(p => p.X);
        var minY = overlay.Points.Min(p => p.Y);
        var maxX = overlay.Points.Max(p => p.X);
        var maxY = overlay.Points.Max(p => p.Y);
        var width = maxX - minX;
        var height = maxY - minY;
        if (width <= 0 || height <= 0)
            return (null, null, null, null);

        return (minX + width / 2.0, minY + height / 2.0, width, height);
    }
}
