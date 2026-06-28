using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Import.Common;

/// <summary>
/// Gemeinsamer In-Memory-Dateiindex fuer Mediendateien.
/// Baut einen Dictionary (Dateiname -> Liste absoluter Pfade) aus einer bereits
/// enumertierten Dateiliste auf und loest einzelne Dateinamen eindeutig auf.
///
/// IO (SafeFileEnumeration, GetMediaRoots) liegt IMMER beim Aufrufer,
/// damit die jeweils unterschiedlichen Scan-Wurzeln und Extension-Sets
/// verhaltensneutral erhalten bleiben.
/// </summary>
internal static class MediaFileIndex
{
    /// <summary>
    /// Baut den Index auf: Dateiname (case-insensitive) -> Liste aller absoluten Pfade.
    /// Nur Dateien, deren Extension in <paramref name="extensions"/> enthalten ist,
    /// werden aufgenommen.
    /// </summary>
    /// <param name="files">Bereits enumerierte absolute Dateipfade (IO liegt beim Aufrufer).</param>
    /// <param name="extensions">
    /// Zugelassene Erweiterungen (inkl. Punkt, z.B. ".mp4"), case-insensitiv verglichen.
    /// </param>
    /// <returns>Dictionary: Dateiname (OrdinalIgnoreCase) -> Liste absoluter Pfade.</returns>
    public static Dictionary<string, List<string>> Build(
        IEnumerable<string> files,
        HashSet<string> extensions)
    {
        var dict = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var file in files)
        {
            var ext = Path.GetExtension(file);
            if (!extensions.Contains(ext))
                continue;

            var name = Path.GetFileName(file);
            if (!dict.TryGetValue(name, out var list))
            {
                list = new List<string>();
                dict[name] = list;
            }
            list.Add(file);
        }

        return dict;
    }

    /// <summary>
    /// Loest einen Dateinamen im Index auf: gibt den absoluten Pfad zurueck,
    /// wenn der Dateiname genau einmal im Index vorhanden ist.
    /// Bei Mehrdeutigkeit (mehrere Pfade) oder fehlendem Eintrag wird null zurueckgegeben.
    /// </summary>
    /// <param name="index">Datei-Index aus <see cref="Build"/>.</param>
    /// <param name="fileName">Dateiname (ohne Pfad).</param>
    /// <returns>Absoluter Pfad oder null bei Fehltreffer / Mehrdeutigkeit.</returns>
    public static string? ResolveSingle(Dictionary<string, List<string>> index, string fileName)
    {
        if (!index.TryGetValue(fileName, out var list) || list.Count == 0)
            return null;

        if (list.Count == 1)
            return list[0];

        return null;
    }
}
