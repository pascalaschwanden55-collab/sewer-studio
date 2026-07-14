using System.IO;
using System.Threading;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.UI.Ai;

public static class CodingDefectPreviewService
{
    private static ICodingDefectPreviewRenderer _current = new CodingDefectPreviewRenderer();

    internal static ICodingDefectPreviewRenderer CompatibilityService
        => Volatile.Read(ref _current);

    internal static void Use(ICodingDefectPreviewRenderer renderer)
    {
        ArgumentNullException.ThrowIfNull(renderer);
        Volatile.Write(ref _current, renderer);
    }

    public static string? BuildPreviewImagePath(CodingEvent ev, string? previewRoot = null)
        => CompatibilityService.BuildPreviewImagePath(ev, previewRoot);
}

public sealed class CodingDefectPreviewRenderer : ICodingDefectPreviewRenderer
{
    public string? BuildPreviewImagePath(CodingEvent codingEvent, string? previewRoot = null)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var rawFramePath = codingEvent.Entry.FotoPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(rawFramePath))
            return null;

        previewRoot ??= Path.Combine(Path.GetTempPath(), "SewerStudio", "coding_defect_previews");
        Directory.CreateDirectory(previewRoot);

        var previewPath = Path.Combine(previewRoot, $"{codingEvent.EventId:N}_preview.png");
        var saved = EvidenceFrameRenderer.SaveAnnotatedFrame(
            rawFramePath,
            previewPath,
            CodingEvidenceAnnotationBuilder.Build(codingEvent));

        return saved ? previewPath : rawFramePath;
    }
}
