using System;
using System.Globalization;

namespace AuswertungPro.Next.Application.Media;

/// <summary>
/// Reine Logik der Stapel-Bildextraktion: ffmpeg-Argumente, Dateinamen und die
/// Abbildung Bildnummer zu Videozeit.
///
/// Der bestehende <see cref="IVideoFrameExtractor"/> holt ein Bild je Aufruf und
/// startet dafuer jedes Mal ffmpeg. Fuer einen Vorabdurchlauf ueber zehn Minuten
/// Video waeren das rund 600 Prozessstarts; ein Durchgang mit fester Abtastrate
/// erledigt dasselbe in einem. Beide Wege bestehen bewusst nebeneinander — der
/// Einzelbild-Weg wird vom Player und vom Training Studio verwendet.
/// </summary>
public static class VideoFrameSequenceLayout
{
    /// <summary>Namensmuster der Bilder; ffmpeg zaehlt ab 1.</summary>
    public const string FileNamePattern = "f%06d.jpg";

    /// <summary>
    /// Videozeit des Bildes mit dieser Nummer. Bild 1 ist der Videoanfang, nicht
    /// Sekunde 1 — ein Fehler hier verschiebt jeden Vorschlag um eine Abtastung.
    /// </summary>
    public static double TimeSecondsFor(int index, double framesPerSecond)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(index, 1);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(framesPerSecond, 0.0);
        return (index - 1) / framesPerSecond;
    }

    /// <summary>Bildnummer aus dem Dateinamen, oder null bei fremden Dateien.</summary>
    public static int? TryParseIndex(string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        var name = System.IO.Path.GetFileNameWithoutExtension(fileName);
        if (name.Length < 2 || name[0] != 'f')
            return null;

        return int.TryParse(
            name.AsSpan(1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out var index) && index >= 1
                ? index
                : null;
    }

    /// <summary>
    /// Baut die ffmpeg-Argumente. Bewusst ohne <c>-y</c>: Der Zielordner muss leer
    /// sein, damit keine Bilder eines frueheren Laufs stillschweigend
    /// weiterverwendet oder ueberschrieben werden.
    /// </summary>
    public static string BuildArguments(string videoPath, string targetDirectory, double framesPerSecond)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(videoPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetDirectory);
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(framesPerSecond, 0.0);

        // Invariante Kultur: mit deutscher Kultur entstuende "fps=0,5", was ffmpeg
        // als zwei Argumente liest.
        var rate = framesPerSecond.ToString("0.####", CultureInfo.InvariantCulture);
        var target = System.IO.Path.Combine(targetDirectory, FileNamePattern);
        return $"-v error -i \"{videoPath}\" -vf fps={rate} -q:v 3 \"{target}\"";
    }
}
