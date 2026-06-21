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
            CodingEvidenceAnnotationBuilder.Build(ev));

        return saved ? previewPath : rawFramePath;
    }
}
