namespace AuswertungPro.Next.Application.Media;

/// <summary>Schneidet einen kurzen Clip aus einem Video. Das Original wird nur gelesen.</summary>
public interface IVideoClipExtractor
{
    /// <summary>
    /// Liefert den Pfad des erzeugten Clips (Temp-Datei). Strenge wie beim
    /// Bildfolgen-Extraktor: Ein ffmpeg-Fehlschlag wird mit seiner woertlichen
    /// Fehlerausgabe geworfen, und ein Lauf ohne Ergebnis ist ein Fehler,
    /// kein leerer Clip. Ein defektes Video darf nie wie eine leere Stelle
    /// aussehen.
    /// </summary>
    Task<string> CutClipAsync(
        string ffmpegPath,
        string videoPath,
        TimeSpan from,
        TimeSpan to,
        CancellationToken cancellationToken);
}
