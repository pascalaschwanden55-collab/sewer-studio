using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.UI.Views;

/// <summary>
/// Loest Foto-/Videopfade eines Protokolleintrags zu anzeigbaren Dateien auf.
///
/// Grenze seit dem Gesamtaudit 2026-08-14 (Prio 2): Vorher genuegte es, dass eine
/// Datei existiert — jeder absolute Pfad und jeder relative Pfad mit <c>..</c> wurde
/// angezeigt. Eine fremde oder manipulierte Projektdatei konnte SewerStudio damit dazu
/// bringen, beliebige lokale Dateien zu oeffnen.
///
/// Jetzt gilt:
/// * Relative Pfade duerfen den Projektordner nicht verlassen (kein <c>..</c>-Ausbruch).
/// * Absolute Pfade muessen im Projektordner oder in einer ausdruecklich erlaubten
///   Wurzel liegen (Projektwurzel und die zuletzt genutzten Projektordner) — externe
///   Kundenmedien bleiben damit sichtbar, beliebige Systempfade nicht.
/// * Zusaetzlich muss die Endung eine Mediendatei sein.
/// * Verknuepfungen werden nicht gefolgt.
/// </summary>
internal sealed class ProtocolEntryEditorMediaPathResolver
{
    /// <summary>Erlaubte Medienendungen. Alles andere wird nicht angezeigt.</summary>
    private static readonly string[] AllowedExtensions =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp", ".tif", ".tiff",
        ".mp4", ".avi", ".mkv", ".mov", ".mpg", ".mpeg", ".wmv", ".webm",
        ".pdf"
    };

    private readonly string? _projectFolder;
    private readonly Func<string?> _currentProjectPath;
    private readonly Func<string, bool> _fileExists;
    private readonly IReadOnlyList<string> _additionalAllowedRoots;

    internal ProtocolEntryEditorMediaPathResolver(
        string? projectFolder,
        Func<string?> currentProjectPath,
        Func<string, bool>? fileExists = null,
        IReadOnlyList<string>? additionalAllowedRoots = null)
    {
        ArgumentNullException.ThrowIfNull(currentProjectPath);

        _projectFolder = projectFolder;
        _currentProjectPath = currentProjectPath;
        _fileExists = fileExists ?? File.Exists;
        _additionalAllowedRoots = additionalAllowedRoots ?? Array.Empty<string>();
    }

    internal string ResolveProjectFolder()
    {
        if (!string.IsNullOrWhiteSpace(_projectFolder))
            return _projectFolder;

        var fromSettings = _currentProjectPath();
        if (!string.IsNullOrWhiteSpace(fromSettings))
        {
            var directory = ProjectFileLocator.ProjectRootFromFile(fromSettings)
                            ?? Path.GetDirectoryName(fromSettings);
            if (!string.IsNullOrWhiteSpace(directory))
                return directory;
        }

        return AppDomain.CurrentDomain.BaseDirectory;
    }

    internal string? ResolveExistingPath(string? rawPath)
    {
        var path = rawPath?.Trim();
        if (string.IsNullOrWhiteSpace(path))
            return null;

        if (!HasAllowedExtension(path))
            return null;

        var baseDirectory = ResolveProjectFolder();

        if (Path.IsPathRooted(path))
        {
            // Absoluter Pfad: nur innerhalb erlaubter Wurzeln.
            if (!IsWithinAllowedRoot(path, baseDirectory))
                return null;

            return _fileExists(path) && !IsReparsePoint(path) ? path : null;
        }

        if (string.IsNullOrWhiteSpace(baseDirectory))
            return null;

        // Ein syntaktisch ungueltiger Pfad bleibt bewusst eine Ausnahme: ein solcher
        // Eintrag ist ein Datenfehler und soll nicht stillschweigend verschwinden.
        var combined = Path.GetFullPath(Path.Combine(baseDirectory, path));

        // Kein Ausbruch aus dem Projektordner ueber "..".
        if (!IsInside(baseDirectory, combined))
            return null;

        return _fileExists(combined) && !IsReparsePoint(combined) ? combined : null;
    }

    internal IReadOnlyList<string> ResolveImagePaths(IReadOnlyList<string> rawPaths)
    {
        var result = new List<string>();
        foreach (var rawPath in rawPaths)
        {
            var path = ResolveExistingPath(rawPath);
            if (string.IsNullOrWhiteSpace(path))
                continue;
            if (!result.Contains(path, StringComparer.OrdinalIgnoreCase))
                result.Add(path);
        }

        return result;
    }

    private static bool HasAllowedExtension(string path)
    {
        var extension = Path.GetExtension(path);
        return !string.IsNullOrEmpty(extension)
               && AllowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
    }

    private bool IsWithinAllowedRoot(string absolutePath, string projectFolder)
    {
        if (!string.IsNullOrWhiteSpace(projectFolder) && IsInside(projectFolder, absolutePath))
            return true;

        foreach (var root in _additionalAllowedRoots)
        {
            if (!string.IsNullOrWhiteSpace(root) && IsInside(root, absolutePath))
                return true;
        }

        return false;
    }

    private static bool IsInside(string root, string candidate)
    {
        try
        {
            var normalizedRoot = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
            var relative = Path.GetRelativePath(normalizedRoot, Path.GetFullPath(candidate));
            return !relative.StartsWith("..", StringComparison.Ordinal)
                   && !Path.IsPathRooted(relative);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    /// <summary>
    /// Eine Verknuepfung koennte auf eine ganz andere Datei zeigen und die
    /// Wurzelpruefung damit umgehen.
    /// </summary>
    private static bool IsReparsePoint(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            return true;
        }
    }
}
