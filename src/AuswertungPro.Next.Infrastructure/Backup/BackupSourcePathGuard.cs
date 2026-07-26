namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Fail-closed Schutz fuer Quellen einer Sicherung. Eine Quellwurzel oder Quelldatei
/// darf selbst keine Junction/kein Symlink sein; sonst koennte fremder Inhalt kopiert
/// oder ein textuell unerkannter Quelle-Ziel-Kreis aufgebaut werden.
/// </summary>
internal static class BackupSourcePathGuard
{
    public static void EnsureDirectoryRootIsSafe(string sourceRoot)
        => EnsureDirectoryRootIsSafe(sourceRoot, File.GetAttributes);

    internal static void EnsureDirectoryRootIsSafe(
        string sourceRoot,
        Func<string, FileAttributes> readAttributes)
    {
        var attributes = ReadAttributes(sourceRoot, readAttributes);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"Verknuepfung als Sicherungs-Quellroot wurde blockiert: {sourceRoot}");
        }

        if ((attributes & FileAttributes.Directory) == 0)
            throw new InvalidDataException($"Sicherungs-Quellroot ist kein Ordner: {sourceRoot}");
    }

    public static void EnsureFileIsSafe(string sourcePath)
    {
        var attributes = ReadAttributes(sourcePath, File.GetAttributes);
        if ((attributes & FileAttributes.ReparsePoint) != 0)
        {
            throw new InvalidDataException(
                $"Verknuepfung als Sicherungs-Quelldatei wurde blockiert: {sourcePath}");
        }

        if ((attributes & FileAttributes.Directory) != 0)
            throw new InvalidDataException($"Sicherungs-Quelldatei ist ein Ordner: {sourcePath}");
    }

    private static FileAttributes ReadAttributes(
        string path,
        Func<string, FileAttributes> readAttributes)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(readAttributes);
        try
        {
            return readAttributes(path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            throw;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or NotSupportedException)
        {
            throw new InvalidDataException(
                $"Sicherungsquelle konnte nicht sicher geprueft werden: {path}",
                ex);
        }
    }
}
