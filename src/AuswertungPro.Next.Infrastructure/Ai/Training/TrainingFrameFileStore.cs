using AuswertungPro.Next.Application.Ai.Training;
using AuswertungPro.Next.Infrastructure.Ai.KnowledgeBase;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>Dateibasierter Speicher fuer extrahierte Trainingsframes.</summary>
public sealed class TrainingFrameFileStore : ITrainingFrameStore
{
    private readonly Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> _extractFrame;

    public TrainingFrameFileStore()
        : this(VideoFrameExtractor.TryExtractFramePngAsync)
    {
    }

    public TrainingFrameFileStore(
        Func<string, string, TimeSpan, CancellationToken, Task<byte[]?>> extractFrame)
    {
        _extractFrame = extractFrame ?? throw new ArgumentNullException(nameof(extractFrame));
    }

    public string GetFramesDir(string? customDir = null)
    {
        if (!string.IsNullOrWhiteSpace(customDir))
        {
            Directory.CreateDirectory(customDir);
            return customDir;
        }

        return KnowledgeBasePaths.GetFramesDir();
    }

    public async Task<string?> ExtractAndStoreAsync(
        string ffmpegPath,
        string videoPath,
        double timeSeconds,
        string sampleId,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        var directory = GetFramesDir(framesDir);
        var outputPath = Path.Combine(directory, $"{TrainingFrameFileName.Sanitize(sampleId)}.png");
        if (File.Exists(outputPath))
            return outputPath;

        var bytes = await _extractFrame(
                ffmpegPath,
                videoPath,
                TimeSpan.FromSeconds(timeSeconds),
                ct)
            .ConfigureAwait(false);
        if (bytes is null || bytes.Length == 0)
            return null;

        await File.WriteAllBytesAsync(outputPath, bytes, ct).ConfigureAwait(false);
        return outputPath;
    }
}
