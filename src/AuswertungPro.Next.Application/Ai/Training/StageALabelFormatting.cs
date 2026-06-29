using System.Globalization;

namespace AuswertungPro.Next.Application.Ai.Training;

/// <summary>
/// Reine Hilfsklasse fuer YOLO-Label-Formatierung und Dateinamen-Bereinigung.
/// Enthaelt keine IO-Abhaengigkeiten.
/// </summary>
public static class StageALabelFormatting
{
    /// <summary>
    /// Baut eine YOLO-Label-Zeile fuer das gegebene Sample.
    /// Wenn keine BoundingBox vorhanden ist, wird eine Standard-Box (0.5 0.5 0.8 0.8) verwendet.
    /// </summary>
    public static string BuildYoloLabelLine(int classId, TrainingSample sample)
    {
        var (xc, yc, w, h) = sample.HasBbox
            ? (
                Clamp01(sample.BboxXCenter!.Value),
                Clamp01(sample.BboxYCenter!.Value),
                Clamp01(sample.BboxWidth!.Value),
                Clamp01(sample.BboxHeight!.Value))
            : (0.5, 0.5, 0.8, 0.8);

        return string.Format(
            CultureInfo.InvariantCulture,
            "{0} {1:F6} {2:F6} {3:F6} {4:F6}",
            classId,
            xc,
            yc,
            w,
            h);
    }

    /// <summary>
    /// Klemmt einen Wert auf den Bereich [0, 1].
    /// </summary>
    public static double Clamp01(double value)
        => Math.Min(1, Math.Max(0, value));

    /// <summary>
    /// Ersetzt ungueltiger Dateinamen-Zeichen durch Unterstriche.
    /// Gibt eine neue GUID zurueck, wenn der bereinigte Name leer waere.
    /// </summary>
    public static string SanitizeFileName(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Guid.NewGuid().ToString("N");

        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var chars = value
            .Select(ch => invalid.Contains(ch) ? '_' : ch)
            .ToArray();
        var sanitized = new string(chars).Trim();
        return string.IsNullOrWhiteSpace(sanitized)
            ? Guid.NewGuid().ToString("N")
            : sanitized;
    }
}
