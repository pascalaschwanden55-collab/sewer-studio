using System.Collections.Generic;
using System.IO;

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

        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
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
                stack.Push(children[i]);
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
                yield return file;
        }
    }
}
