namespace AuswertungPro.Next.Infrastructure.Import;

internal sealed class ImportFileStagingPathGuard
{
    public ImportFileStagingPathGuard(string projectRoot)
    {
        ProjectRoot = Path.GetFullPath(projectRoot);
    }

    public string ProjectRoot { get; }

    public void EnsureWithinProject(string path, string parameterName)
    {
        if (!IsWithinProject(path))
        {
            throw new ArgumentException(
                $"Dateiziel liegt ausserhalb des Projektstamms: {path}",
                parameterName);
        }
    }

    public bool IsWithinProject(string path)
    {
        var fullPath = Path.GetFullPath(path);
        var root = ProjectRoot.TrimEnd(
            Path.DirectorySeparatorChar,
            Path.AltDirectorySeparatorChar);
        return string.Equals(fullPath, root, StringComparison.OrdinalIgnoreCase)
               || fullPath.StartsWith(
                   root + Path.DirectorySeparatorChar,
                   StringComparison.OrdinalIgnoreCase);
    }

    public void EnsureNoNestedReparsePoint(string path)
    {
        var current = Path.GetFullPath(path);
        while (IsWithinProject(current)
               && !string.Equals(current, ProjectRoot, StringComparison.OrdinalIgnoreCase))
        {
            if (Directory.Exists(current))
                EnsureNotReparsePoint(current);
            current = Path.GetDirectoryName(current) ?? ProjectRoot;
        }
    }

    public static void EnsureNotReparsePoint(string path)
    {
        if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0)
            throw new IOException($"Importpfad enthaelt eine Verknuepfung oder Junction: {path}");
    }

    public static void EnsureDirectChild(string child, string parent)
    {
        var childParent = Path.GetDirectoryName(Path.GetFullPath(child));
        var fullParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(childParent, fullParent, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Unsicherer Import-Arbeitsordner wird nicht geloescht.");
    }
}
