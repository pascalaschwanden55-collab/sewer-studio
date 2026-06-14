using System.IO;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingDefectPreviewService
{
    public static string? BuildPreviewImagePath(CodingEvent ev, string? previewRoot = null)
    {
        var rawFramePath = ev.Entry.FotoPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(rawFramePath))
            return null;

        previewRoot ??= Path.Combine(Path.GetTempPath(), "SewerStudio", "coding_defect_previews");
        Directory.CreateDirectory(previewRoot);

        var previewPath = Path.Combine(previewRoot, $"{ev.EventId:N}_preview.png");
        var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
            rawFramePath,
            previewPath,
            BuildAnnotation(ev));

        return saved ? previewPath : rawFramePath;
    }

    private static EvidenceFrameAnnotation BuildAnnotation(CodingEvent ev)
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

    private static (double? XCenter, double? YCenter, double? Width, double? Height) ExtractBbox(OverlayGeometry? overlay)
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
