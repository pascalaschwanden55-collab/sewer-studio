using AuswertungPro.Next.Application.Media;

namespace AuswertungPro.Next.Infrastructure.Ai;

/// <summary>Kompatible Fassade; Prozess- und Dateiarbeit liegt im Instanzdienst.</summary>
public static class VideoFrameExtractor
{
    private static readonly IVideoFrameExtractor Default =
        new VideoFrameExtractionService(ProcessOutputReader.Current);

    public static IVideoFrameExtractor Current => Default;

    [Obsolete("Globaler Austausch wurde entfernt. Den Dienst per Konstruktor uebergeben.")]
    public static void Use(IVideoFrameExtractor extractor) =>
        throw new NotSupportedException(
            "Der globale Video-Frame-Extraktor kann nicht mehr ausgetauscht werden. " +
            "IVideoFrameExtractor bitte per Konstruktor uebergeben.");

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
