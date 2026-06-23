using System.IO;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Ai;

public sealed class LiveDetectionTrainingFrameExporter
{
    private readonly ITrainingAnnotationExportService _exportService;
    private readonly Func<string, string> _tempFramePathFactory;

    public LiveDetectionTrainingFrameExporter(
        ITrainingAnnotationExportService exportService,
        Func<string, string>? tempFramePathFactory = null)
    {
        _exportService = exportService;
        _tempFramePathFactory = tempFramePathFactory ?? DefaultTempFramePath;
    }

    public async Task<TrainingAnnotationResult> ExportAsync(
        byte[] frameBytes,
        NormalizedBoundingBox bbox,
        string code,
        int classId,
        string baseName,
        string annotationId,
        CancellationToken ct = default)
    {
        var tempFrame = _tempFramePathFactory(annotationId);
        var directory = Path.GetDirectoryName(tempFrame);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);

        await File.WriteAllBytesAsync(tempFrame, frameBytes, ct);

        try
        {
            return await _exportService.ExportAsync(tempFrame, bbox, code, classId, baseName, ct);
        }
        finally
        {
            BestEffort.Try(() => File.Delete(tempFrame), "Detection-Training: Temp-Frame loeschen");
        }
    }

    private static string DefaultTempFramePath(string annotationId)
        => Path.Combine(Path.GetTempPath(), $"sewer_studio_det_{annotationId}.png");
}
