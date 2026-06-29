using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AuswertungPro.Next.Infrastructure.Ai.Training;

/// <summary>
/// Liest die Video-Dauer aus der ffmpeg-Stderr-Ausgabe heraus.
/// Reine statische Klasse ohne I/O oder Prozessstart.
/// </summary>
internal static class FfmpegDurationParser
{
    // Muster: "Duration: HH:MM:SS.s"
    private static readonly Regex DurationPattern =
        new(@"Duration:\s*(\d+):(\d{2}):(\d{2}\.?\d*)", RegexOptions.Compiled);

    /// <summary>
    /// Parst die Videodauer aus der ffmpeg-Stderr-Ausgabe.
    /// Gibt 0 zurück, wenn kein gültiges Muster gefunden wird.
    /// </summary>
    /// <param name="stderr">Vollständiger stderr-Text von ffmpeg -i &lt;video&gt;.</param>
    /// <returns>Dauer in Sekunden, oder 0 wenn nicht parsebar.</returns>
    public static double Parse(string stderr)
    {
        if (string.IsNullOrEmpty(stderr))
            return 0;

        var m = DurationPattern.Match(stderr);
        if (!m.Success)
            return 0;

        if (!int.TryParse(m.Groups[1].Value, out var hh))
            return 0;
        if (!int.TryParse(m.Groups[2].Value, out var mm))
            return 0;
        if (!double.TryParse(m.Groups[3].Value, NumberStyles.Float,
                CultureInfo.InvariantCulture, out var ss))
            return 0;

        return hh * 3600 + mm * 60 + ss;
    }
}
