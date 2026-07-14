namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Prüft den konfigurierten ffmpeg-Pfad des Selbsttrainings und liefert einen sicheren Rückfall.
/// </summary>
public interface ITrainingFfmpegPathResolver
{
    string Resolve(string? ffmpegPath);
}
