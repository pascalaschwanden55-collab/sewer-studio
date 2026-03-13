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

        // Relativer Pfad: gegen Projektordner aufloesen
        if (!Path.IsPathRooted(path) && !string.IsNullOrWhiteSpace(projectFilePath))
        {
            var baseDir = Path.GetDirectoryName(projectFilePath);
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                // Path-Traversal-Schutz: aufgeloester Pfad muss im Projektordner bleiben
                var normalizedBase = Path.GetFullPath(baseDir) + Path.DirectorySeparatorChar;
                if (combined.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase)
                    && File.Exists(combined))
                    return combined;
            }
        }

        return null;
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
            if (!string.IsNullOrWhiteSpace(baseDir))
            {
                var combined = Path.GetFullPath(Path.Combine(baseDir, path));
                // Path-Traversal-Schutz: aufgeloester Pfad muss im Projektordner bleiben
                var normalizedBase = Path.GetFullPath(baseDir) + Path.DirectorySeparatorChar;
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
    /// Prueft, ob ein Pfad relativ ist (nicht gerootet).
    /// </summary>
    public static bool IsRelative(string? path)
        => !string.IsNullOrWhiteSpace(path) && !Path.IsPathRooted(path);

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
        return string.IsNullOrWhiteSpace(cleaned) ? "UNKNOWN" : cleaned;
    }
}
