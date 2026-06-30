using System.IO;

namespace AuswertungPro.Next.Application.DataPage;

/// <summary>
/// Reine Hilfsklasse fuer die Aufloesung des initialen Video-Ordners
/// mit 3-stufiger Fallback-Kette:
/// LastVideoSourceFolder → LastVideoFolder → Verzeichnis der Projektdatei.
/// Aus <c>DataPageViewModel</c> extrahiert (verhaltensneutral); taucht 3x mit
/// identischem Muster in EnsureVideoPath / RelinkVideo / EnsureProtocolPath auf.
/// </summary>
public static class VideoFolderFallbackResolver
{
    /// <summary>
    /// Ermittelt den initialen Ordner fuer Video-Datei-Dialoge.
    /// Gibt den ersten nicht-leeren Wert der Fallback-Kette zurueck,
    /// oder <c>null</c> wenn alle Quellen leer sind.
    /// </summary>
    /// <param name="lastVideoSourceFolder">Zuletzt verwendeter Video-Quell-Ordner.</param>
    /// <param name="lastVideoFolder">Legacy-Video-Ordner-Einstellung.</param>
    /// <param name="lastProjectPath">Pfad zur aktuell geoeffneten Projektdatei.</param>
    public static string? Resolve(
        string? lastVideoSourceFolder,
        string? lastVideoFolder,
        string? lastProjectPath)
    {
        if (!string.IsNullOrWhiteSpace(lastVideoSourceFolder))
            return lastVideoSourceFolder;

        if (!string.IsNullOrWhiteSpace(lastVideoFolder))
            return lastVideoFolder;

        if (lastProjectPath is null)
            return null;

        return Path.GetDirectoryName(lastProjectPath);
    }
}
