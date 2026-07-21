using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Liest gespeicherte Importpfade zentral und unterstuetzt die alte sowie die neue
/// Lage der Projektdatei.
/// </summary>
public sealed class StoredImportFilePathResolver : IStoredImportFilePathResolver
{
    public IReadOnlyList<string> ResolveExistingFiles(
        IDictionary<string, string> metadata,
        string metadataKey,
        string? projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        ArgumentException.ThrowIfNullOrWhiteSpace(metadataKey);

        var resolvedPaths = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var storedPath in StoredImportFileRegistry.Load(metadata, metadataKey))
        {
            var resolved = TryResolveExistingFile(storedPath, projectFilePath);
            if (!string.IsNullOrWhiteSpace(resolved) && seen.Add(resolved))
                resolvedPaths.Add(resolved);
        }

        return resolvedPaths;
    }

    private static string? TryResolveExistingFile(string storedPath, string? projectFilePath)
    {
        try
        {
            // Moderne Pfade sind relativ zum echten Projektordner gespeichert.
            var resolved = ProjectPathResolver.ResolveFilePath(storedPath, projectFilePath);
            if (!string.IsNullOrWhiteSpace(resolved))
                return resolved;

            if (Path.IsPathRooted(storedPath) || string.IsNullOrWhiteSpace(projectFilePath))
                return null;

            // Fruehere Importe wurden teilweise relativ zum Ordner der projekt.json
            // gespeichert. Bei Projektdateien\projekt.json liegt dieser unterhalb des
            // echten Projektordners und bleibt deshalb ein ausdruecklicher Rueckfall.
            var legacyBaseDirectory = Path.GetDirectoryName(projectFilePath);
            return ProjectPathResolver.ResolveFilePathFromProjectFolder(
                storedPath,
                legacyBaseDirectory);
        }
        catch
        {
            // Ein ungueltiger gespeicherter Pfad darf die restlichen Quellen nicht blockieren.
            return null;
        }
    }
}
