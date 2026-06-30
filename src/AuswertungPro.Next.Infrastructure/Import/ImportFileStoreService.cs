using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kopiert Quelldateien in den Projektordner und traegt die relativen Pfade im
/// Projekt-Metadaten-Dictionary ein. Ersetzt die drei wortgleichen Store*Files-Methoden
/// aus dem ImportPageViewModel (XTF, PDF, TXT).
/// </summary>
public static class ImportFileStoreService
{
    /// <summary>
    /// Kopiert <paramref name="paths"/> in &lt;projectDir&gt;/Imports/&lt;subFolder&gt; und
    /// aktualisiert <paramref name="project"/>.Metadata[<paramref name="metadataKey"/>].
    /// </summary>
    /// <param name="project">Aktives Projekt (Metadaten werden direkt mutiert).</param>
    /// <param name="projectDir">Projektordner (Basis fuer Imports-Unterordner).</param>
    /// <param name="paths">Quelldatei-Pfade.</param>
    /// <param name="subFolder">Unterordner innerhalb Imports/ (z.B. "XTF", "PDF", "TXT").</param>
    /// <param name="metadataKey">Schluessel im Metadata-Dictionary (z.B. "XTF_StoredFiles").</param>
    /// <returns>Liste der neu kopierten relativen Pfade.</returns>
    public static List<string> StoreFiles(
        Project project,
        string projectDir,
        string[] paths,
        string subFolder,
        string metadataKey)
    {
        var targetDir = Path.Combine(projectDir, "Imports", subFolder);
        Directory.CreateDirectory(targetDir);

        var stored = new List<string>();
        foreach (var src in paths)
        {
            if (!File.Exists(src)) continue;
            var fileName = Path.GetFileName(src);
            var dest = Path.Combine(targetDir, fileName);

            if (File.Exists(dest))
            {
                var srcInfo = new FileInfo(src);
                var destInfo = new FileInfo(dest);
                if (srcInfo.Length != destInfo.Length)
                {
                    // Datei existiert mit abweichender Groesse -> Namen mit Zeitstempel versehen
                    var name = Path.GetFileNameWithoutExtension(fileName);
                    var ext = Path.GetExtension(fileName);
                    dest = Path.Combine(targetDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
                }
                else
                {
                    // Identische Datei bereits vorhanden: nur Referenz speichern
                    stored.Add(Path.GetRelativePath(projectDir, dest));
                    continue;
                }
            }

            File.Copy(src, dest, overwrite: false);
            stored.Add(Path.GetRelativePath(projectDir, dest));
        }

        if (stored.Count == 0)
            return stored;

        StoredImportFileRegistry.Save(project.Metadata, metadataKey, stored);
        return stored;
    }
}
