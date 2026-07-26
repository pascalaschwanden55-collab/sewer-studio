using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Ai.Evidence;

namespace AuswertungPro.Next.UI.Ai.Coding;

public static class CodingDefectPreviewService
{
    private static readonly ICodingDefectPreviewRenderer Default = new CodingDefectPreviewRenderer();

    internal static ICodingDefectPreviewRenderer CompatibilityService
        => Default;

    public static string? BuildPreviewImagePath(CodingEvent ev, string? previewRoot = null)
        => CompatibilityService.BuildPreviewImagePath(ev, previewRoot);
}

public sealed class CodingDefectPreviewRenderer : ICodingDefectPreviewRenderer
{
    private readonly IEvidenceFrameRenderer _frameRenderer;

    public CodingDefectPreviewRenderer()
        : this(new EvidenceFrameImageRenderer())
    {
    }

    public CodingDefectPreviewRenderer(IEvidenceFrameRenderer frameRenderer)
    {
        _frameRenderer = frameRenderer ?? throw new ArgumentNullException(nameof(frameRenderer));
    }

    public string? BuildPreviewImagePath(CodingEvent codingEvent, string? previewRoot = null)
    {
        ArgumentNullException.ThrowIfNull(codingEvent);

        var rawFramePath = codingEvent.Entry.FotoPaths.FirstOrDefault(File.Exists);
        if (string.IsNullOrWhiteSpace(rawFramePath))
            return null;

        previewRoot ??= Path.Combine(Path.GetTempPath(), "SewerStudio", "coding_defect_previews");
        Directory.CreateDirectory(previewRoot);

        var previewPath = Path.Combine(previewRoot, $"{codingEvent.EventId:N}_preview.png");
        var saved = _frameRenderer.SaveAnnotatedFrame(
            rawFramePath,
            previewPath,
            CodingEvidenceAnnotationBuilder.Build(codingEvent));

        return saved ? previewPath : rawFramePath;
    }
}
