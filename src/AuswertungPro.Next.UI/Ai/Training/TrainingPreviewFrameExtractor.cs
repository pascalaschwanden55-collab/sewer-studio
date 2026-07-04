using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public static class TrainingPreviewFrameExtractor
{
    public static async Task<string?> ExtractPreviewFrameAsync(
        TrainingCase trainingCase,
        AiRuntimeSettings settings,
        CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrEmpty(trainingCase.VideoPath) || !File.Exists(trainingCase.VideoPath))
            return null;

        var ffmpeg = settings.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(trainingCase.CaseId, @"[^\w\-]", "_")}";

        try
        {
            return await FrameStore.ExtractAndStoreAsync(
                ffmpeg,
                trainingCase.VideoPath,
                2.0,
                sampleId,
                null,
                ct).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}
