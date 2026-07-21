using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kompatibilitaetsfassade fuer alte Aufrufer. Neue Aufrufer verwenden
/// IStoredImportFileService ueber den zentralen ServiceProvider.
/// </summary>
public static class ImportFileStoreService
{
    private static readonly StoredImportFileService DefaultService = new();

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
        ArgumentNullException.ThrowIfNull(project);

        var result = DefaultService.StoreInProjectDirectory(
            projectDir,
            project.Metadata,
            subFolder,
            metadataKey,
            paths);
        return new List<string>(result.StoredRelativePaths);
    }
}
