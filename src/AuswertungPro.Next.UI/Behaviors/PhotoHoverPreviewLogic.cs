using System.IO;
using AuswertungPro.Next.UI.Ai;

namespace AuswertungPro.Next.UI.Behaviors;

/// <summary>
/// Reine, UI-freie Logik fuer die Hover-Foto-Vorschau:
/// Pfadaufloesung, Blaettern mit Umlauf, Zaehlertext und 25%-Einpassung.
/// Bewusst ohne WPF-Abhaengigkeiten, damit die Kernlogik voll unit-testbar bleibt.
/// </summary>
public static class PhotoHoverPreviewLogic
{
    /// <summary>Maximaler Bildschirmanteil pro Achse (25 %).</summary>
    public const double MaxScreenFraction = 0.25;

    // Untergrenze der Vorschaubox, damit sie auf kleinen Bildschirmen nicht winzig wird.
    private const double MinBoxWidth = 160d;
    private const double MinBoxHeight = 120d;

    /// <summary>
    /// Loest FotoPaths zu existierenden Dateien auf. Absolute existierende Pfade werden direkt uebernommen,
    /// relative Pfade via <see cref="CodingPhotoDisplayPathPolicy.ResolveExistingPath"/> gegen den Projekt-Root.
    /// Whitespace-Eintraege werden verworfen, Duplikate case-insensitiv entfernt.
    /// projectRoot null/leer -> nur absolute existierende Pfade.
    /// </summary>
    public static IReadOnlyList<string> ResolveExistingPhotos(
        IEnumerable<string>? fotoPaths,
        string? projectRoot,
        Func<string, bool> fileExists)
    {
        var result = new List<string>();
        if (fotoPaths is null)
            return result;

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var raw in fotoPaths)
        {
            if (string.IsNullOrWhiteSpace(raw))
                continue;

            string? resolved;
            if (string.IsNullOrWhiteSpace(projectRoot))
            {
                // Ohne Projekt-Root koennen relative Pfade nicht aufgeloest werden -> nur absolute existierende.
                resolved = Path.IsPathRooted(raw) && fileExists(raw) ? raw : null;
            }
            else
            {
                resolved = CodingPhotoDisplayPathPolicy.ResolveExistingPath(raw, projectRoot, fileExists);
            }

            if (resolved is null)
                continue;
            if (seen.Add(resolved))
                result.Add(resolved);
        }

        return result;
    }

    /// <summary>Naechster Index mit Umlauf in beide Richtungen; count &lt;= 0 -> 0.</summary>
    public static int NextIndex(int currentIndex, int count, int delta)
    {
        if (count <= 0)
            return 0;
        return ((currentIndex + delta) % count + count) % count;
    }

    /// <summary>1-basierter Zaehlertext, z. B. index 0, count 3 -> "1/3".</summary>
    public static string CounterText(int index, int count)
        => $"{index + 1}/{count}";

    /// <summary>25 % der Bildschirmmasse (DIP) mit Untergrenze 160x120.</summary>
    public static (double MaxWidth, double MaxHeight) MaxBoxFromScreen(double screenWidth, double screenHeight)
    {
        var maxWidth = Math.Max(MinBoxWidth, screenWidth * MaxScreenFraction);
        var maxHeight = Math.Max(MinBoxHeight, screenHeight * MaxScreenFraction);
        return (maxWidth, maxHeight);
    }

    /// <summary>
    /// Seitenverhaeltnis-treues Einpassen in die Box. Bilder kleiner als die Box werden NICHT
    /// hochskaliert. Ungueltige Masse (&lt;= 0) liefern (0,0).
    /// </summary>
    public static (double Width, double Height) FitPreserveAspect(
        double imageWidth, double imageHeight, double maxWidth, double maxHeight)
    {
        if (imageWidth <= 0 || imageHeight <= 0 || maxWidth <= 0 || maxHeight <= 0)
            return (0d, 0d);

        // Nur verkleinern (Faktor <= 1), damit kleine Fotos nicht unscharf hochgezogen werden.
        var scale = Math.Min(1d, Math.Min(maxWidth / imageWidth, maxHeight / imageHeight));
        return (imageWidth * scale, imageHeight * scale);
    }
}
