using System.IO;

namespace AuswertungPro.Next.UI.Mapping;

/// <summary>
/// Findet den Basisordner der Offline-Hintergrundkarten (mit den Unterordnern "satellit"/"av").
/// Toleriert einen veralteten gespeicherten Pfad: fruehere Versionen speicherten
/// "...\basemap_tiles\uri" in der settings.json, die Kacheln liegen aber unter
/// "...\basemap_tiles\{satellit,av}". Der Resolver prueft daher den Pfad selbst UND seinen
/// Elternordner. So laedt die Satellit-/AV-Karte auch ohne die settings.json von Hand zu
/// aendern. Reine Ordner-Pruefung -> gut testbar, keine God-Class.
/// </summary>
public static class OfflineBasemapBaseResolver
{
    public static string? Resolve(string? configuredPath)
    {
        if (string.IsNullOrWhiteSpace(configuredPath))
            return configuredPath;

        if (HatKartenordner(configuredPath))
            return configuredPath;

        var parent = Path.GetDirectoryName(
            configuredPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
        if (!string.IsNullOrWhiteSpace(parent) && HatKartenordner(parent))
            return parent;

        return configuredPath; // nichts gefunden -> unveraendert (Verhalten wie bisher)
    }

    private static bool HatKartenordner(string basePath)
        => Directory.Exists(Path.Combine(basePath, KarteBasemapLayerFactory.SatellitSubfolder))
        || Directory.Exists(Path.Combine(basePath, KarteBasemapLayerFactory.AvSubfolder));
}
