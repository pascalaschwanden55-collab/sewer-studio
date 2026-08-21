using System.Security;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Prueft fremde Quelldateien komponentenweise, bevor ein Dateizugriff erfolgt.
/// So kann ein lokal aussehender Pfad nicht ueber einen Symlink oder eine Junction
/// unbemerkt auf ein anderes Laufwerk oder eine UNC-Freigabe umleiten.
/// </summary>
internal static class ImportSourcePathGuard
{
    internal static bool TryInspectFile(
        string? path,
        out string safePath,
        out bool exists,
        out string? error)
        => TryInspect(path, expectedDirectory: false, out safePath, out exists, out error);

    internal static bool TryInspectDirectory(
        string? path,
        out string safePath,
        out bool exists,
        out string? error)
        => TryInspect(path, expectedDirectory: true, out safePath, out exists, out error);

    private static bool TryInspect(
        string? path,
        bool expectedDirectory,
        out string safePath,
        out bool exists,
        out string? error)
    {
        safePath = string.Empty;
        exists = false;
        error = null;

        if (string.IsNullOrWhiteSpace(path))
        {
            error = "Quellenpfad fehlt.";
            return false;
        }

        var trimmed = path.Trim();
        if (MediaFileAllowlist.IsUnc(trimmed))
        {
            error = "UNC-Quellenpfad wird nicht gelesen.";
            return false;
        }

        try
        {
            safePath = Path.GetFullPath(trimmed);
            var root = Path.GetPathRoot(safePath);
            if (string.IsNullOrWhiteSpace(root)
                || MediaFileAllowlist.IsUnc(root)
                || IsNetworkDrive(root))
            {
                error = "UNC- oder Netzwerk-Quellenpfad wird nicht gelesen.";
                safePath = string.Empty;
                return false;
            }

            var relative = safePath[root.Length..];
            var segments = relative.Split(
                [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
                StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 0)
            {
                error = "Quellenpfad zeigt nicht auf eine Datei oder einen Unterordner.";
                return false;
            }

            var current = root;
            for (var index = 0; index < segments.Length; index++)
            {
                current = Path.Combine(current, segments[index]);
                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(current);
                }
                catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
                {
                    // Ein noch fehlender Rest kann momentan keine Verknuepfung enthalten.
                    // Der Aufrufer entscheidet anhand von exists, ob ein Fallback sinnvoll ist.
                    return true;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    error = $"Quellenpfad enthaelt eine Verknuepfung: {current}";
                    safePath = string.Empty;
                    return false;
                }

                var isDirectory = (attributes & FileAttributes.Directory) != 0;
                var isLast = index == segments.Length - 1;
                if (!isLast && !isDirectory)
                {
                    error = $"Quellenpfad enthaelt eine Datei statt eines Ordners: {current}";
                    safePath = string.Empty;
                    return false;
                }

                if (isLast && isDirectory != expectedDirectory)
                {
                    error = expectedDirectory
                        ? $"Quellenpfad ist kein Ordner: {current}"
                        : $"Quellenpfad ist keine Datei: {current}";
                    safePath = string.Empty;
                    return false;
                }
            }

            exists = true;
            return true;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or SecurityException
                                   or ArgumentException
                                   or NotSupportedException
                                   or PathTooLongException)
        {
            error = $"Quellenpfad konnte nicht sicher geprueft werden: {ex.Message}";
            safePath = string.Empty;
            exists = false;
            return false;
        }
    }

    private static bool IsNetworkDrive(string root)
    {
        if (!OperatingSystem.IsWindows())
            return false;

        try
        {
            return new DriveInfo(root).DriveType == DriveType.Network;
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or ArgumentException)
        {
            // Ein nicht sicher bestimmbares Laufwerk wird beim anschliessenden
            // komponentenweisen Attributzugriff fail-closed behandelt.
            return false;
        }
    }
}
