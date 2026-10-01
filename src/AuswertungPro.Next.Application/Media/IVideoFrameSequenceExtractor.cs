namespace AuswertungPro.Next.Application.Media;

/// <summary>Ein extrahiertes Einzelbild einer Videofolge.</summary>
public sealed record VideoSequenceFrame(int Index, double TimeSeconds, string FilePath);

/// <summary>Auftrag fuer die Stapelextraktion.</summary>
public sealed record VideoFrameSequenceRequest
{
    public required string FfmpegPath { get; init; }

    public required string VideoPath { get; init; }

    /// <summary>Muss leer oder nicht vorhanden sein — Bilder frueherer Laeufe sind gesperrt.</summary>
    public required string TargetDirectory { get; init; }

    /// <summary>Abtastrate. 1,0 entspricht der Messung vom 2026-08-07.</summary>
    public double FramesPerSecond { get; init; } = 1.0;
}

/// <summary>
/// Extrahiert eine ganze Bildfolge in EINEM ffmpeg-Durchgang.
///
/// Getrennt vom bestehenden <see cref="IVideoFrameExtractor"/>, der ein Bild je
/// Aufruf holt: Fuer einen Vorabdurchlauf ueber zehn Minuten Video waeren das rund
/// 600 Prozessstarts. Beide Wege bestehen bewusst nebeneinander — den
/// Einzelbild-Weg verwenden Player und Training Studio.
/// </summary>
public interface IVideoFrameSequenceExtractor
{
    Task<IReadOnlyList<VideoSequenceFrame>> ExtractAsync(
        VideoFrameSequenceRequest request,
        CancellationToken cancellationToken);
}
