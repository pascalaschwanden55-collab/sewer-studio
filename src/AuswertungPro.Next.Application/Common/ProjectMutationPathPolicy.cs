namespace AuswertungPro.Next.Application.Common;

/// <summary>Gemeinsame Schreibgrenze; Dateiattribute liefert der aufrufende Dateidienst.</summary>
public static class ProjectMutationPathPolicy
{
    public static string EnsureSafePath(
        string projectRoot,
        string path,
        Func<string, FileAttributes?> readAttributes)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        if (string.IsNullOrWhiteSpace(projectRoot) || !Path.IsPathFullyQualified(projectRoot))
            throw new IOException("Ohne eindeutigen Projektordner dürfen keine Dateien geändert werden.");

        var root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(projectRoot));
        var full = Path.GetFullPath(path);
        var relative = Path.GetRelativePath(root, full);
        if (Path.IsPathRooted(relative) || relative == ".."
            || relative.StartsWith(".." + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new IOException($"Dateipfad liegt ausserhalb des Projektordners: {path}");

        // Auch ein Alias oberhalb des Projektroots darf nicht zu fremden Dateien fuehren.
        for (string? current = full; current is not null; current = Path.GetDirectoryName(current))
        {
            if (((readAttributes(current) ?? 0) & FileAttributes.ReparsePoint) != 0)
                throw new IOException($"Projektpfad enthält eine Verknüpfung oder Junction: {current}");
        }
        return full;
    }

    /// <summary>Importarchive bleiben unveraenderlich, auch wenn sie im Projekt liegen.</summary>
    public static void EnsureWorkingCopy(string projectRoot, string path)
    {
        var segments = Path.GetRelativePath(projectRoot, path)
            .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
        var first = segments.FirstOrDefault() ?? string.Empty;
        var archive = first.Equals("Imports", StringComparison.OrdinalIgnoreCase)
            || first.Equals("Importdateien", StringComparison.OrdinalIgnoreCase)
            || first.Equals("__RESTORE_POINTS", StringComparison.OrdinalIgnoreCase)
            || first.Equals(ProjectFileLocator.ProjektdateienDir, StringComparison.OrdinalIgnoreCase);
        if (archive)
            throw new IOException($"Die Datei liegt im geschützten Projektarchiv, nicht in einer Arbeitskopie: {path}");
    }
}
