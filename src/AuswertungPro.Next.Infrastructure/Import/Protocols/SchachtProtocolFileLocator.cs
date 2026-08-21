using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Protocols;

/// <summary>
/// Gemeinsame Suche nach der Protokoll-PDF genau eines Schachts. Ersetzt die
/// frueher starre Aufloesung, die nur exakte relative Pfade akzeptierte und
/// deshalb bei absoluten Verknuepfungen oder umbenannten Dateien nichts fand.
/// </summary>
public sealed class SchachtProtocolFileLocator : ISchachtProtocolFileLocator
{
    private static readonly string[] KnownSchachtFolders =
    {
        "Schächte_Verteilt",
        "Schaechte_Verteilt",
        "Schächte_1.15",
        "Schaechte_1.15"
    };

    public SchachtProtocolFileMatch? Locate(
        string projektOrdner,
        string? gespeicherterPfad,
        string? linkPfad,
        string? schachtnummer)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projektOrdner);

        foreach (var rawPath in new[] { gespeicherterPfad, linkPfad })
        {
            var resolved = ResolveStoredPath(projektOrdner, rawPath);
            if (resolved is not null)
                return new SchachtProtocolFileMatch(resolved, SchachtProtocolFileOrigin.Verknuepfung);
        }

        var folderMatch = FindInShaftFolder(projektOrdner, schachtnummer);
        return folderMatch is null
            ? null
            : new SchachtProtocolFileMatch(folderMatch, SchachtProtocolFileOrigin.Schachtordner);
    }

    /// <summary>
    /// Sucht ausschliesslich im Ordner dieses einen Schachts (inklusive dessen
    /// Unterordnern, z.B. <c>PDF\</c>). Fremde Schachtordner bleiben tabu.
    /// </summary>
    internal static string? FindInShaftFolder(string projektOrdner, string? schachtnummer)
    {
        if (string.IsNullOrWhiteSpace(schachtnummer))
            return null;

        var safeNumber = ProjectPathResolver.SanitizePathSegment(schachtnummer);
        if (safeNumber == "UNKNOWN")
            return null;

        foreach (var baseName in KnownSchachtFolders)
        {
            var match = FindBestPdf(Path.Combine(projektOrdner, baseName, safeNumber), safeNumber);
            if (match is not null)
                return match;
        }

        // Projektnamen koennen abweichende Zusaetze tragen. Deshalb weitere
        // Schacht-Hauptordner kontrolliert pruefen, ohne ausserhalb des Projekts zu suchen.
        try
        {
            foreach (var directory in Directory.EnumerateDirectories(
                         projektOrdner,
                         "*",
                         SearchOption.TopDirectoryOnly))
            {
                var name = Path.GetFileName(directory);
                if (!name.Contains("Schächt", StringComparison.OrdinalIgnoreCase)
                    && !name.Contains("Schaech", StringComparison.OrdinalIgnoreCase))
                    continue;

                var match = FindBestPdf(Path.Combine(directory, safeNumber), safeNumber);
                if (match is not null)
                    return match;
            }
        }
        catch
        {
            // Die direkten Pfade wurden bereits versucht. Ein unlesbarer Zusatzordner
            // darf die Suche fuer diesen Schacht nicht abbrechen.
        }

        return null;
    }

    internal static string? ResolveStoredPath(string projektOrdner, string? rawPath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (Path.IsPathRooted(path))
                return File.Exists(path) && IsPdf(path) ? Path.GetFullPath(path) : null;

            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(path, projektOrdner);
            return resolved is not null && IsPdf(resolved) ? resolved : null;
        }
        catch
        {
            return null;
        }
    }

    private static string? FindBestPdf(string numberFolder, string schachtnummer)
    {
        if (!Directory.Exists(numberFolder))
            return null;

        try
        {
            return SafeFileEnumeration
                .EnumerateFilesSafe(numberFolder, "*.pdf", recursive: true)
                .OrderByDescending(path => Path.GetFileNameWithoutExtension(path)
                    .Contains(schachtnummer, StringComparison.OrdinalIgnoreCase))
                .ThenByDescending(File.GetLastWriteTimeUtc)
                .ThenBy(path => path, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
        }
        catch
        {
            return null;
        }
    }

    private static bool IsPdf(string path)
        => string.Equals(Path.GetExtension(path), ".pdf", StringComparison.OrdinalIgnoreCase);
}
