using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Dossiers;

/// <summary>
/// Das Ergebnis einer Umwandlung. Entweder gibt es ein Bild oder einen Grund —
/// nie beides leer.
/// </summary>
public sealed record PlanImageResult(string? ImagePath, string? Error)
{
    public bool Success => ImagePath is not null;

    public static PlanImageResult Ok(string path) => new(path, null);

    public static PlanImageResult Failed(string reason) => new(null, reason);
}

/// <summary>
/// Macht aus einer Planvorlage ein Bild, das ins Dossier eingesetzt werden kann.
///
/// Word nimmt nur PNG und JPEG. Ein Plan kommt aber meist als PDF — er wird
/// deshalb beim Auswaehlen umgewandelt, nicht erst beim Erzeugen: so sieht man
/// in der Vorschau sofort, was im Dossier stehen wird.
///
/// Die Quelldatei wird nur gelesen. Das Bild entsteht im Projekt, nie neben dem
/// Kundenoriginal.
/// </summary>
public interface IPlanImageConverter
{
    /// <summary>Wahr, wenn diese Datei umgewandelt werden muss.</summary>
    bool NeedsConversion(string? path);

    Task<PlanImageResult> ConvertAsync(
        string sourcePath, string targetFolder, CancellationToken ct = default);
}

/// <summary>
/// Passt ein bereits gewaehltes Planbild an. Gedreht wird die Datei selbst,
/// damit der Plan in Vorschau, Word und PDF gleich aussieht.
/// </summary>
public interface IPlanImageAdjuster
{
    /// <summary>
    /// Dreht um 90, 180 oder 270 Grad. Liegt das Bild nicht im Zielordner,
    /// entsteht dort eine gedrehte Kopie — ein fremdes Bild wird nie
    /// veraendert.
    /// </summary>
    PlanImageResult Rotate(string? imagePath, string targetFolder, int degrees);

    /// <summary>
    /// Schneidet den Bereich heraus. Die Angaben sind Bildpunkte des Bildes
    /// selbst, nicht der Anzeige — die Anzeige kann gezoomt sein.
    /// </summary>
    PlanImageResult Crop(
        string? imagePath, string targetFolder, int x, int y, int width, int height);
}
