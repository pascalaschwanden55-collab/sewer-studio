namespace AuswertungPro.Next.Application.Media;

/// <summary>Extrahiert ein einzelnes PNG-Bild aus einem Video.</summary>
public interface IVideoFrameExtractor
{
    Task<byte[]?> TryExtractFramePngAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan at,
        CancellationToken cancellationToken);
}
