using System.IO;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.Training;

namespace AuswertungPro.Next.UI.Ai.Training;

public interface ITrainingPreviewFrameExtractor
{
    Task<string?> ExtractPreviewFrameAsync(
        TrainingCase trainingCase,
        AiRuntimeSettings settings,
        CancellationToken cancellationToken);
}

/// <summary>Erzeugt Trainingsvorschauen ueber den zentralen Frame-Speicher.</summary>
public sealed class TrainingPreviewFrameExtractionService : ITrainingPreviewFrameExtractor
{
    private readonly ITrainingFrameStore _frameStore;
    private readonly Func<string, bool> _fileExists;

    public TrainingPreviewFrameExtractionService(
        ITrainingFrameStore frameStore,
        Func<string, bool>? fileExists = null)
    {
        _frameStore = frameStore ?? throw new ArgumentNullException(nameof(frameStore));
        _fileExists = fileExists ?? File.Exists;
    }

    public async Task<string?> ExtractPreviewFrameAsync(
        TrainingCase trainingCase,
        AiRuntimeSettings settings,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(trainingCase);
        ArgumentNullException.ThrowIfNull(settings);

        if (string.IsNullOrEmpty(trainingCase.VideoPath) || !_fileExists(trainingCase.VideoPath))
            return null;

        var ffmpeg = settings.FfmpegPath ?? "ffmpeg";
        var sampleId = $"preview_{Regex.Replace(trainingCase.CaseId, @"[^\w\-]", "_")}";

        try
        {
            return await _frameStore.ExtractAndStoreAsync(
                ffmpeg,
                trainingCase.VideoPath,
                2.0,
                sampleId,
                null,
                cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            return null;
        }
    }
}

/// <summary>Kompatibilitaetsfassade fuer bestehende Aufrufer.</summary>
public static class TrainingPreviewFrameExtractor
{
    private static readonly ITrainingPreviewFrameExtractor Default =
        new TrainingPreviewFrameExtractionService(new TrainingFrameFileStore());

    public static ITrainingPreviewFrameExtractor Current => Default;

    public static Task<string?> ExtractPreviewFrameAsync(
        TrainingCase trainingCase,
        AiRuntimeSettings settings,
        CancellationToken ct) =>
        Current.ExtractPreviewFrameAsync(trainingCase, settings, ct);
}
