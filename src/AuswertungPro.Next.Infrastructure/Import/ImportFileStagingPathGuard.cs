using AuswertungPro.Next.Application.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

internal sealed class ImportFileStagingPathGuard
{
    public ImportFileStagingPathGuard(string projectRoot)
    {
        ProjectRoot = Path.GetFullPath(projectRoot);
    }

    public string ProjectRoot { get; }

    /// <summary>
    /// Gemeinsame Pfadgrenze fuer Staging, direkte Projektschreibwege und Journal.
    /// Sie prueft neben der lexikalischen Projektgrenze auch den Projektroot selbst,
    /// alle vorhandenen Elternordner und ein bereits vorhandenes Endziel.
    /// </summary>
    public string EnsureSafeProjectPath(string path, string parameterName)
    {
        var fullPath = Path.GetFullPath(path);
        EnsureWithinProject(fullPath, parameterName);
        EnsureProjectRootIsSafe();
        EnsureNoNestedReparsePoint(fullPath);
        EnsureExistingTargetIsNotReparsePoint(fullPath);
        return fullPath;
    }

    public void EnsureProjectRootIsSafe()
        => EnsureExistingTargetIsNotReparsePoint(ProjectRoot);

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

    // Gemeinsame Verknuepfungspruefung (Deepscan A5), Regel Streng: ein fehlender oder unlesbarer
    // Eintrag wirft seine urspruengliche Ausnahme (EnsureExistingTargetIsNotReparsePoint faengt "fehlt").
    public static void EnsureNotReparsePoint(string path)
    {
        var befund = VerknuepfungsSchutz.PruefeEintrag(path, VerknuepfungsRegel.Streng);
        if (befund.Fehler is not null)
            System.Runtime.ExceptionServices.ExceptionDispatchInfo.Throw(befund.Fehler);
        if (befund.Befund == VerknuepfungsBefund.Verknuepfung)
            throw new IOException($"Importpfad enthält eine Verknüpfung oder Junction: {path}");
    }

    public static void EnsureDirectChild(string child, string parent)
    {
        var childParent = Path.GetDirectoryName(Path.GetFullPath(child));
        var fullParent = Path.GetFullPath(parent)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (!string.Equals(childParent, fullParent, StringComparison.OrdinalIgnoreCase))
            throw new IOException("Unsicherer Import-Arbeitsordner wird nicht gelöscht.");
    }

    private static void EnsureExistingTargetIsNotReparsePoint(string path)
    {
        try
        {
            EnsureNotReparsePoint(path);
        }
        catch (FileNotFoundException)
        {
            // Das Ziel darf vor dem Schreiben noch fehlen.
        }
        catch (DirectoryNotFoundException)
        {
            // Fehlende Unterordner werden erst nach der Elternpruefung angelegt.
        }
    }
}
