using System;
using System.Collections.Generic;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Backup;

/// <summary>
/// Gemeinsame fail-closed Sicherheitsgrenze fuer alle schreibenden Backup-Zielpfade.
/// Lexikalische Pfadgrenzen allein reichen bei Junctions und anderen Reparse Points
/// nicht aus: deshalb werden Zielroot, Elternkette und vorhandene Zielbestandteile
/// vor jeder Mutation geprueft.
/// </summary>
internal static class BackupTargetPathGuard
{
    public static void EnsureRootIsSafe(string targetRoot)
        => EnsureRootIsSafe(targetRoot, ReadAttributes);

    internal static void EnsureRootIsSafe(
        string targetRoot,
        Func<string, FileAttributes?> readAttributes)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        var root = Normalize(targetRoot);
        var pathRoot = NormalizePathRoot(root);
        var current = root;

        while (true)
        {
            EnsureEntryIsNotReparsePoint(current, readAttributes);
            if (PathsEqual(current, pathRoot))
                return;

            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw BackupTargetBoundary.Fail(
                    $"Zielroot konnte nicht bis zum Laufwerks- oder Freigabe-Root geprueft werden: {root}");
            }

            current = Normalize(parent);
        }
    }

    public static string ResolveRelativePath(string targetRoot, string relativePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(relativePath);
        if (Path.IsPathRooted(relativePath))
            throw BackupTargetBoundary.Fail($"Zielpfad muss relativ sein: {relativePath}");

        var root = Normalize(targetRoot);
        var candidate = Normalize(Path.Combine(root, relativePath));
        EnsurePathIsSafe(root, candidate);
        return candidate;
    }

    public static void EnsurePathIsSafe(string targetRoot, string targetPath)
        => EnsurePathIsSafe(targetRoot, targetPath, ReadAttributes);

    internal static void EnsurePathIsSafe(
        string targetRoot,
        string targetPath,
        Func<string, FileAttributes?> readAttributes)
    {
        ArgumentNullException.ThrowIfNull(readAttributes);
        var root = Normalize(targetRoot);
        var candidate = Normalize(targetPath);
        if (!IsSameOrInside(root, candidate))
        {
            throw BackupTargetBoundary.Fail(
                $"Zielpfad liegt ausserhalb des Sicherungsroots: {candidate}");
        }

        EnsureRootIsSafe(root, readAttributes);
        var current = candidate;
        while (!PathsEqual(current, root))
        {
            EnsureEntryIsNotReparsePoint(current, readAttributes);
            var parent = Path.GetDirectoryName(current);
            if (string.IsNullOrWhiteSpace(parent))
            {
                throw BackupTargetBoundary.Fail(
                    $"Zielpfad konnte nicht bis zum Sicherungsroot geprueft werden: {candidate}");
            }

            current = Normalize(parent);
            if (!IsSameOrInside(root, current))
            {
                throw BackupTargetBoundary.Fail(
                    $"Zielpfad verlaesst den Sicherungsroot: {candidate}");
            }
        }
    }

    public static void EnsureTreeIsSafe(string targetRoot)
    {
        var root = Normalize(targetRoot);
        EnsureRootIsSafe(root);
        if (!Directory.Exists(root))
            return;

        var stack = new Stack<string>();
        stack.Push(root);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            string[] entries;
            try
            {
                entries = Directory.GetFileSystemEntries(current);
            }
            catch (Exception ex) when (ex is IOException
                                       or UnauthorizedAccessException
                                       or PathTooLongException
                                       or NotSupportedException)
            {
                throw BackupTargetBoundary.Fail(
                    $"Zielordner konnte nicht sicher geprueft werden: {current}",
                    ex);
            }

            foreach (var entry in entries)
            {
                EnsurePathIsSafe(root, entry);
                var attributes = ReadAttributes(entry);
                if (attributes is not null
                    && (attributes.Value & FileAttributes.Directory) != 0)
                {
                    stack.Push(entry);
                }
            }
        }
    }

    private static void EnsureEntryIsNotReparsePoint(
        string path,
        Func<string, FileAttributes?> readAttributes)
    {
        FileAttributes? attributes;
        try
        {
            attributes = readAttributes(path);
        }
        catch (Exception ex) when (ex is IOException
                                   or UnauthorizedAccessException
                                   or PathTooLongException
                                   or NotSupportedException)
        {
            throw BackupTargetBoundary.Fail(
                $"Zielpfad konnte nicht sicher geprueft werden: {path}",
                ex);
        }

        if (attributes is not null
            && (attributes.Value & FileAttributes.ReparsePoint) != 0)
        {
            throw BackupTargetBoundary.Fail(
                $"Verknuepfung im Sicherungs-Zielpfad wurde blockiert: {path}");
        }
    }

    private static FileAttributes? ReadAttributes(string path)
    {
        try
        {
            return File.GetAttributes(path);
        }
        catch (FileNotFoundException)
        {
            return null;
        }
        catch (DirectoryNotFoundException)
        {
            return null;
        }
    }

    private static bool IsSameOrInside(string root, string candidate)
    {
        if (PathsEqual(root, candidate))
            return true;

        var prefix = root.EndsWith(Path.DirectorySeparatorChar)
            || root.EndsWith(Path.AltDirectorySeparatorChar)
                ? root
                : root + Path.DirectorySeparatorChar;
        return candidate.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PathsEqual(string left, string right)
        => string.Equals(
            left.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            right.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

    private static string NormalizePathRoot(string path)
    {
        var pathRoot = Path.GetPathRoot(path);
        if (string.IsNullOrWhiteSpace(pathRoot))
            throw BackupTargetBoundary.Fail($"Zielpfad besitzt keinen Laufwerks- oder Freigabe-Root: {path}");
        return Normalize(pathRoot);
    }

    private static string Normalize(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var fullPath = Path.GetFullPath(path);
        var pathRoot = Path.GetPathRoot(fullPath);
        return string.Equals(fullPath, pathRoot, StringComparison.OrdinalIgnoreCase)
            ? fullPath
            : fullPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
    }
}
