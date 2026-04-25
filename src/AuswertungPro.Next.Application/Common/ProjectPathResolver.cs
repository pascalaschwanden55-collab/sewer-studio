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
    /// Loest einen Pfad zu einer Datei auf, die zwingend INNERHALB des
    /// Projektordners liegen muss (Containment-Check gegen Path-Traversal und
    /// gegen referenzierte externe Dateien aus manipulierten Projektdateien).
    ///
    /// - relative Pfade werden gegen <paramref name="projectFolder"/> aufgeloest
    /// - absolute Pfade werden NUR akzeptiert, wenn sie im Projektordner liegen
    /// - alle Pfade muessen via Path.GetFullPath aufgeloest werden, sodass ".."
    ///   nicht aus dem Projekt herausspringt
    ///
    /// Gibt null zurueck wenn Pfad ausserhalb, ungueltig oder nicht existent.
    /// </summary>
    public static string? ResolveContainedFile(string? rawPath, string? projectFolder)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(projectFolder))
            return null;

        try
        {
            var fullProjectFolder = Path.GetFullPath(projectFolder);
            // Trailing-Separator anhaengen, damit "C:\proj" nicht "C:\project_other" matcht
            var rootWithSep = fullProjectFolder.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                              + Path.DirectorySeparatorChar;

            // Forward-Slashes normalisieren (Cross-Plattform-JSON-Speicherung)
            var normalizedRaw = path.Replace('/', Path.DirectorySeparatorChar);

            string fullPath;
            if (Path.IsPathRooted(normalizedRaw))
            {
                fullPath = Path.GetFullPath(normalizedRaw);
            }
            else
            {
                fullPath = Path.GetFullPath(Path.Combine(fullProjectFolder, normalizedRaw));
            }

            // Containment: aufgeloester Pfad muss unterhalb des Projektordners liegen
            if (!fullPath.StartsWith(rootWithSep, StringComparison.OrdinalIgnoreCase))
                return null;

            return File.Exists(fullPath) ? fullPath : null;
        }
        catch
        {
            return null;
        }
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
    /// Schuetzt gegen Path-Traversal ueber "." / ".." in Haltungs-IDs aus externen
    /// Quellen (z.B. manipulierte WinCan-DB oder Import-XML).
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
        if (string.IsNullOrWhiteSpace(cleaned))
            return "UNKNOWN";

        // Path-Traversal-Schutz: "." und ".." sind gueltige Dateinamen-Zeichen,
        // aber gefaehrlich als Ordnernamen — Path.Combine wuerde daraus eine Ebene
        // ausserhalb des Zielordners machen. Auch eingebettetes ".." (z.B. "foo..bar")
        // neutralisieren. Trimming von Trailing-Dots verhindert Windows-"foo." = "foo" Aliases.
        cleaned = cleaned.TrimEnd('.', ' ');
        if (string.IsNullOrWhiteSpace(cleaned) || cleaned == "." || cleaned == "..")
            return "UNKNOWN";
        if (cleaned.Contains(".."))
            cleaned = cleaned.Replace("..", "_");
        return cleaned;
    }
}
