using System.Collections.Generic;
using System.IO;
using System.Security;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Safe, deterministic file enumeration for user-selected directory trees.
/// Inaccessible or transient directories are skipped instead of aborting the whole scan.
/// </summary>
public static class SafeFileEnumeration
{
    public static IEnumerable<string> EnumerateDirectoriesSafe(
        string root,
        ICollection<string>? skippedDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            yield break;

        var visited = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stack = new Stack<(string Path, bool IsRoot)>();
        stack.Push((root, true));

        while (stack.Count > 0)
        {
            var (current, isRoot) = stack.Pop();

            // Der vom Aufrufer gewaehlte Root wird normal gelesen. Verknuepfungen,
            // die erst innerhalb dieses Baums auftauchen, werden dagegen nie betreten.
            if (!isRoot && !CanEnterDirectory(current, skippedDirectories))
                continue;

            if (!TryGetDirectoryIdentity(current, out var identity))
            {
                skippedDirectories?.Add(current);
                continue;
            }

            // Zusaetzliche Sicherung gegen doppelte Aliase und Zyklen. Reparse Points
            // werden bereits oben abgewiesen; die Menge schuetzt auch vor gleichen
            // normalisierten Pfaden, die ein Dateisystem mehrfach liefert.
            if (!visited.Add(identity))
            {
                skippedDirectories?.Add(current);
                continue;
            }

            yield return current;

            string[] children;
            try
            {
                children = Directory.EnumerateDirectories(current)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                skippedDirectories?.Add(current);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                skippedDirectories?.Add(current);
                continue;
            }
            catch (IOException)
            {
                skippedDirectories?.Add(current);
                continue;
            }

            for (var i = children.Length - 1; i >= 0; i--)
                stack.Push((children[i], false));
        }
    }

    public static IEnumerable<string> EnumerateFilesSafe(
        string root,
        string searchPattern = "*",
        bool recursive = true,
        ICollection<string>? skippedDirectories = null)
    {
        if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
            yield break;

        var dirs = recursive
            ? EnumerateDirectoriesSafe(root, skippedDirectories)
            : new[] { root };

        foreach (var dir in dirs)
        {
            string[] files;
            try
            {
                files = Directory.EnumerateFiles(dir, searchPattern)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
            }
            catch (UnauthorizedAccessException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }
            catch (DirectoryNotFoundException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }
            catch (IOException)
            {
                skippedDirectories?.Add(dir);
                continue;
            }

            foreach (var file in files)
            {
                if (CanReadFileWithoutFollowingLink(file))
                    yield return file;
            }
        }
    }

    private static bool CanReadFileWithoutFollowingLink(string path)
    {
        try
        {
            return (File.GetAttributes(path) & FileAttributes.ReparsePoint) == 0;
        }
        catch (Exception ex) when (IsExpectedDirectoryFailure(ex)
                                   || ex is ArgumentException
                                       or NotSupportedException)
        {
            return false;
        }
    }

    private static bool CanEnterDirectory(
        string path,
        ICollection<string>? skippedDirectories)
    {
        try
        {
            var attributes = File.GetAttributes(path);
            var isDirectory = (attributes & FileAttributes.Directory) != 0;
            var isReparsePoint = (attributes & FileAttributes.ReparsePoint) != 0;
            if (isDirectory && !isReparsePoint)
                return true;
        }
        catch (Exception ex) when (IsExpectedDirectoryFailure(ex))
        {
            // Der einzelne Ordner wird unten sichtbar als uebersprungen gemeldet.
        }

        skippedDirectories?.Add(path);
        return false;
    }

    private static bool TryGetDirectoryIdentity(string path, out string identity)
    {
        try
        {
            identity = Path.TrimEndingDirectorySeparator(Path.GetFullPath(path));
            return true;
        }
        catch (Exception ex) when (IsExpectedPathFailure(ex))
        {
            identity = string.Empty;
            return false;
        }
    }

    private static bool IsExpectedDirectoryFailure(Exception ex)
        => ex is UnauthorizedAccessException
            or DirectoryNotFoundException
            or IOException
            or SecurityException;

    private static bool IsExpectedPathFailure(Exception ex)
        => IsExpectedDirectoryFailure(ex)
            || ex is ArgumentException
                or NotSupportedException;
}
