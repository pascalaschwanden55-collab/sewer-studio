namespace AuswertungPro.Next.Application.Import;

/// <summary>
/// Loest in Projekt-Metadaten gespeicherte Importdateien zu vorhandenen Dateien auf.
/// </summary>
public interface IStoredImportFilePathResolver
{
    IReadOnlyList<string> ResolveExistingFiles(
        IDictionary<string, string> metadata,
        string metadataKey,
        string? projectFilePath);
}
