using System;
using System.IO;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Zentraler Helper fuer die Aufloesung von relativen/absoluten Pfaden im Projektkontext.
/// Neue Projekte speichern relative Pfade (portabel). Alte Projekte mit absoluten Pfaden
/// werden weiterhin unterstuetzt.
/// </summary>
public static class ProjectPathResolver
{
    /// <summary>
    /// Loest einen Pfad auf, der relativ (zum Projektordner) oder absolut sein kann.
    /// Gibt den absoluten Pfad zurueck, wenn die Datei existiert, sonst null.
    /// </summary>
    public static string? ResolveFilePath(string? rawPath, string? projectFilePath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        // Absoluter Pfad: direkt pruefen
        if (Path.IsPathRooted(path) && File.Exists(path))
            return path;

        // Relativer Pfad: gegen den Projekt-ROOT aufloesen. Seit die projekt.json unter
        // Projektdateien\ liegen kann, ist GetDirectoryName(projekt.json) NICHT der Root; relative
        // Medienpfade (Haltungen_Verteilt\...) sind aber relativ zum Root gespeichert.
        // ProjectRootFromFile ist rueckwaertskompatibel (liegt die Datei im Root, ist es dessen Ordner).
        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(projectFilePath))
        {
            var baseDir = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                          ?? Path.GetDirectoryName(projectFilePath);
            return ResolveFilePathFromProjectFolder(path, baseDir);
        }

        return null;
    }

    public static string? ResolveFilePathFromProjectFolder(string? rawPath, string? projectFolder)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(projectFolder))
            return null;

        if (!IsSafeRelativeProjectPath(path))
            return null;

        try
        {
            var combined = Path.GetFullPath(Path.Combine(projectFolder, path));
            var normalizedBase = NormalizeDirectoryForContainment(projectFolder);
            if (!combined.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
                return null;

            return File.Exists(combined) ? combined : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Loest einen Ordner-Pfad auf (relativ oder absolut).
    /// </summary>
    public static string? ResolveDirectoryPath(string? rawPath, string? projectFilePath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (Path.IsPathRooted(path) && Directory.Exists(path))
            return path;

        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(projectFilePath))
        {
            var baseDir = Path.GetDirectoryName(projectFilePath);
            if (!string.IsNullOrWhiteSpace(baseDir) && IsSafeRelativeProjectPath(path))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                // Path-Traversal-Schutz: aufgeloester Pfad muss im Projektordner bleiben
                var normalizedBase = NormalizeDirectoryForContainment(baseDir);
                if (combined.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
                    && Directory.Exists(combined))
                    return combined;
            }
        }

        return null;
    }

    /// <summary>
    /// Wandelt einen absoluten Pfad in einen relativen Pfad (zum Projektordner) um.
    /// Verwendet Forward-Slashes fuer plattformunabhaengige JSON-Speicherung.
    /// </summary>
    public static string MakeRelative(string absolutePath, string projectFolder)
    {
        try
        {
            var relative = Path.GetRelativePath(projectFolder, absolutePath);
            return relative.Replace('\\', '/');
        }
        catch
        {
            return absolutePath;
        }
    }

    /// <summary>
    /// Wandelt einen absoluten Pfad nur dann um, wenn er wirklich innerhalb des
    /// Projektordners liegt. Externe Quelldateien bleiben absolut verknuepft.
    /// </summary>
    public static string MakeRelativeIfInsideProject(string? path, string? projectFolder)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || string.IsNullOrWhiteSpace(projectFolder))
            return trimmed ?? string.Empty;
        if (!Path.IsPathRooted(trimmed))
            return trimmed.Replace('\\', '/');

        try
        {
            var fullPath = Path.GetFullPath(trimmed);
            var normalizedRoot = NormalizeDirectoryForContainment(projectFolder);
            if (!fullPath.StartsWith(normalizedRoot, StringComparison.OrdinalIgnoreCase))
                return trimmed;

            return MakeRelative(fullPath, projectFolder);
        }
        catch
        {
            return trimmed;
        }
    }

    /// <summary>
    /// Prueft, ob ein Pfad relativ ist (nicht gerootet).
    /// </summary>
    public static bool IsRelative(string? path)
        => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path);

    public static bool IsSafeRelativeProjectPath(string? path)
    {
        var trimmed = path?.Trim();
        if (string.IsNullOrWhiteSpace(trimmed) || Path.IsPathRooted(trimmed))
            return false;

        var parts = trimmed
            .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
            .Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);

        return parts.Length > 0
            && parts.All(part => part is not "." and not "..");
    }

    /// <summary>
    /// Entfernt ungueltige Dateinamen-Zeichen aus einem Pfadsegment (z.B. Haltungsname).
    /// Gibt "UNKNOWN" zurueck wenn der Wert null/leer ist.
    /// </summary>
    public static string SanitizePathSegment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "UNKNOWN";

        var invalid = Path.GetInvalidFileNameChars();
        var sb = new System.Text.StringBuilder(value.Length);
        foreach (var ch in value)
        {
            if (invalid.Contains(ch))
                sb.Append('_');
            else
                sb.Append(ch);
        }
        var cleaned = sb.ToString().Trim();

        // Punkt-Segmente abfangen: "." / ".." (oder nur Punkte) wuerden sonst
        // ueber Path.Combine aus dem Zielordner ausbrechen. Auch fuehrende/
        // abschliessende Punkte sind unter Windows problematisch.
        cleaned = cleaned.Trim().Trim('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned))
            return "UNKNOWN";

        return cleaned;
    }

    private static string NormalizeDirectoryForContainment(string directory)
    {
        var normalized = Path.GetFullPath(directory);
        return normalized.EndsWith(Path.DirectorySeparatorChar)
            ? normalized
            : normalized + Path.DirectorySeparatorChar;
    }
}
