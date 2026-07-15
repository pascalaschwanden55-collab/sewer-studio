using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>Kompatible Fassade; Prozess- und Dateiarbeit liegt im Instanzdienst.</summary>
public static class VideoFrameExtractor
{
    private static IVideoFrameExtractor _current =
        new VideoFrameExtractionService(ProcessOutputReader.Current);

    public static IVideoFrameExtractor Current => Volatile.Read(ref _current);

    public static void Use(IVideoFrameExtractor extractor)
        => Volatile.Write(
            ref _current,
            extractor ?? throw new ArgumentNullException(nameof(extractor)));

    /// <summary>
    /// Extrahiert ein einzelnes PNG-Frame aus einem Video bei einer Zeitposition.
    /// Benötigt ffmpeg im PATH oder als absoluter Pfad.
    /// </summary>
    public static async Task<byte[]?> TryExtractFramePngAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan at,
        CancellationToken ct)
        => await Current
            .TryExtractFramePngAsync(ffmpegPath, videoPath, at, ct)
            .ConfigureAwait(false);
}
