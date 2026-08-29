using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using ShaftRenameResult = AuswertungPro.Next.Application.Common.ShaftRenameService.ShaftRenameResult;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Atomarer Rename fuer Schachtnummer: Ordner + Dateien + Pfad-Referenzen.
/// </summary>
public static class ShaftRenameService
{
    private static readonly IShaftRenameService Default = new ShaftRenameFileService();

    public sealed record ShaftRenameResult(
        bool Success,
        string? ErrorMessage,
        bool FolderRenamed,
        int PathFieldsUpdated)
    {
        public static ShaftRenameResult Ok(bool folderRenamed, int pathFields)
            => new(true, null, folderRenamed, pathFields);

        public static ShaftRenameResult Fail(string message)
            => new(false, message, false, 0);
    }

    public static ShaftRenameResult Rename(
        SchachtRecord record,
        string oldShaftNumber,
        string newShaftNumber,
        string? projectFilePath)
        => Default.Rename(record, oldShaftNumber, newShaftNumber, projectFilePath);
}

/// <summary>Dateisystem-Implementierung der atomaren Schachtumbenennung.</summary>
public sealed class ShaftRenameFileService : IShaftRenameService
{

    public ShaftRenameResult Rename(
        SchachtRecord record,
        string oldShaftNumber,
        string newShaftNumber,
        string? projectFilePath)
    {
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldShaftNumber);
        var newSan = ProjectPathResolver.SanitizePathSegment(newShaftNumber);

        if (string.Equals(oldSan, newSan, StringComparison.OrdinalIgnoreCase))
            return ShaftRenameResult.Ok(false, 0);

        var folder = LocateShaftFolder(record, oldSan, projectFilePath);
        var oldAliases = BuildAliases(oldSan, Path.GetFileName(folder ?? string.Empty));
        var folderRenamed = false;

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parent))
                return ShaftRenameResult.Fail($"Uebergeordneter Ordner nicht ermittelbar: {folder}");

            var targetFolder = Path.Combine(parent, newSan);
            var sameFolder = string.Equals(
                Path.GetFullPath(folder),
                Path.GetFullPath(targetFolder),
                StringComparison.OrdinalIgnoreCase);

            if (!sameFolder && Directory.Exists(targetFolder))
                return ShaftRenameResult.Fail($"Zielordner existiert bereits: {targetFolder}");

            var result = RenameFilesystemWithRollback(folder, targetFolder, oldAliases, newSan);
            if (!result.Success)
                return ShaftRenameResult.Fail(result.ErrorMessage!);

            folderRenamed = !sameFolder;
        }

        if (RenameSiblingShaftFolder(projectFilePath, Path.Combine("Fotos", "Sch\u00e4chte"), oldSan, newSan, folder))
            folderRenamed = true;

        var updated = UpdateAllPaths(record, oldAliases, newSan);
        return ShaftRenameResult.Ok(folderRenamed, updated);
    }

    private static string? LocateShaftFolder(SchachtRecord record, string oldSan, string? projectFilePath)
    {
        var projectDir = ResolveProjectRoot(projectFilePath);

        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.Link, FieldKeys.PdfEigen })
        {
            var resolved = ProjectPathResolver.ResolveFilePath(record.GetFieldValue(field), projectFilePath);
            var folder = FindShaftFolderFromFile(resolved, oldSan, projectDir);
            if (!string.IsNullOrWhiteSpace(folder))
                return folder;
        }

        if (string.IsNullOrWhiteSpace(projectDir))
            return null;

        foreach (var rootName in new[] { "Sch\u00e4chte_Verteilt", "Schaechte_Verteilt", "Sch\u00e4chte" })
        {
            var shaftsRoot = Path.Combine(projectDir, rootName);
            if (!Directory.Exists(shaftsRoot))
                continue;

            var direct = Path.Combine(shaftsRoot, oldSan);
            if (Directory.Exists(direct))
                return direct;

            try
            {
                var found = Directory.EnumerateDirectories(shaftsRoot, oldSan, SearchOption.TopDirectoryOnly)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(found))
                    return found;
            }
            catch
            {
                // Suchfehler sollen den Rename nicht global abbrechen.
            }
        }

        return null;
    }

    private static string? FindShaftFolderFromFile(string? filePath, string oldSan, string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        if (string.Equals(Path.GetFileName(dir), oldSan, StringComparison.OrdinalIgnoreCase))
            return dir;

        var parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrWhiteSpace(parent)
            && string.Equals(Path.GetFileName(parent), oldSan, StringComparison.OrdinalIgnoreCase))
            return parent;

        var fromKnownRoot = FindShaftFolderUnderKnownRoot(dir, projectDir);
        if (!string.IsNullOrWhiteSpace(fromKnownRoot))
            return fromKnownRoot;

        return null;
    }

    private static string? FindShaftFolderUnderKnownRoot(string directory, string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            return null;

        foreach (var rootName in new[] { "Sch\u00e4chte_Verteilt", "Schaechte_Verteilt", "Sch\u00e4chte" })
        {
            var root = Path.Combine(projectDir, rootName);
            if (!Directory.Exists(root))
                continue;

            var rootFull = NormalizeDirectory(root);
            var dirFull = Path.GetFullPath(directory);
            if (!dirFull.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                continue;

            var relative = Path.GetRelativePath(root, dirFull);
            if (relative.StartsWith("..", StringComparison.Ordinal) || Path.IsPathRooted(relative))
                continue;

            var firstSegment = relative
                .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
            if (string.IsNullOrWhiteSpace(firstSegment))
                continue;

            var shaftFolder = Path.Combine(root, firstSegment);
            if (Directory.Exists(shaftFolder))
                return shaftFolder;
        }

        return null;
    }

    private static bool RenameSiblingShaftFolder(
        string? projectFilePath,
        string relativeParent,
        string oldSan,
        string newSan,
        string? alreadyRenamedFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return false;

        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                   ?? Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(root))
            return false;

        var src = Path.Combine(root, relativeParent, oldSan);
        if (!Directory.Exists(src))
            return false;

        if (!string.IsNullOrWhiteSpace(alreadyRenamedFolder)
            && string.Equals(Path.GetFullPath(src), Path.GetFullPath(alreadyRenamedFolder), StringComparison.OrdinalIgnoreCase))
            return false;

        var dest = Path.Combine(root, relativeParent, newSan);
        if (Directory.Exists(dest))
            return false;

        var result = RenameFilesystemWithRollback(src, dest, BuildAliases(oldSan), newSan);
        return result.Success;
    }

    private sealed record FsResult(bool Success, string? ErrorMessage);

    private static FsResult RenameFilesystemWithRollback(
        string folder,
        string targetFolder,
        IReadOnlyCollection<string> oldAliases,
        string newSan)
    {
        var renamedFiles = new List<(string OldPath, string NewPath)>();
        var folderMoved = false;

        try
        {
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder); }
            catch { files = Array.Empty<string>(); }

            foreach (var file in files)
            {
                var name = Path.GetFileName(file);
                if (string.IsNullOrWhiteSpace(name))
                    continue;

                var renamed = BuildRenamedFileName(name, oldAliases, newSan);
                var dest = Path.Combine(folder, renamed);
                if (string.Equals(file, dest, StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Move(file, dest);
                renamedFiles.Add((file, dest));
            }

            if (!string.Equals(Path.GetFullPath(folder), Path.GetFullPath(targetFolder), StringComparison.OrdinalIgnoreCase))
            {
                Directory.Move(folder, targetFolder);
                folderMoved = true;
            }

            return new FsResult(true, null);
        }
        catch (Exception ex)
        {
            if (!folderMoved)
            {
                for (var i = renamedFiles.Count - 1; i >= 0; i--)
                {
                    try { File.Move(renamedFiles[i].NewPath, renamedFiles[i].OldPath); }
                    catch { /* best-effort rollback */ }
                }
            }

            return new FsResult(false, ex.Message);
        }
    }

    private static int UpdateAllPaths(SchachtRecord record, IReadOnlyCollection<string> oldAliases, string newSan)
    {
        var count = 0;
        count += UpdateFieldPath(record, FieldKeys.PdfPath, oldAliases, newSan);
        count += UpdateFieldPath(record, FieldKeys.Link, oldAliases, newSan);
        count += UpdateFieldPath(record, FieldKeys.PdfEigen, oldAliases, newSan);

        var pdfAll = record.GetFieldValue(FieldKeys.PdfAll);
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            var parts = pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var newParts = parts.Select(p => ReplaceShaftInPath(p.Trim(), oldAliases, newSan)).ToArray();
            var newValue = string.Join(";", newParts);
            if (!string.Equals(pdfAll, newValue, StringComparison.OrdinalIgnoreCase))
            {
                record.SetFieldValue(FieldKeys.PdfAll, newValue, FieldSource.Manual, userEdited: true);
                count++;
            }
        }

        return count;
    }

    private static int UpdateFieldPath(
        SchachtRecord record,
        string fieldName,
        IReadOnlyCollection<string> oldAliases,
        string newSan)
    {
        var raw = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var updated = ReplaceShaftInPath(raw, oldAliases, newSan);
        if (string.Equals(raw, updated, StringComparison.OrdinalIgnoreCase))
            return 0;

        record.SetFieldValue(fieldName, updated, FieldSource.Manual, userEdited: true);
        return 1;
    }

    internal static string ReplaceShaftInPath(string path, string oldSan, string newSan)
        => ReplaceShaftInPath(path, BuildAliases(oldSan), newSan);

    private static string ReplaceShaftInPath(string path, IReadOnlyCollection<string> oldAliases, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/');
        var segmentChanged = false;
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (TryReplaceAlias(parts[i], oldAliases, newSan, out var replacement))
            {
                parts[i] = replacement;
                segmentChanged = true;
            }
        }

        var result = string.Join('/', parts);
        var sepIdx = result.LastIndexOf('/');
        var dir = sepIdx >= 0 ? result[..(sepIdx + 1)] : string.Empty;
        var file = sepIdx >= 0 ? result[(sepIdx + 1)..] : result;
        var renamedFile = BuildRenamedFileName(file, oldAliases, newSan, allowDateSuffixFallback: segmentChanged);

        return dir + renamedFile;
    }

    private static string BuildRenamedFileName(
        string fileName,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        bool allowDateSuffixFallback = true)
    {
        foreach (var oldAlias in oldAliases)
        {
            if (fileName.IndexOf(oldAlias, StringComparison.OrdinalIgnoreCase) >= 0)
                return fileName.Replace(oldAlias, newSan, StringComparison.OrdinalIgnoreCase);
        }

        if (!allowDateSuffixFallback)
            return fileName;

        var extension = Path.GetExtension(fileName);
        if (!extension.Equals(".pdf", StringComparison.OrdinalIgnoreCase))
            return fileName;

        var stem = Path.GetFileNameWithoutExtension(fileName);
        var underscore = stem.LastIndexOf('_');
        if (underscore <= 0 || underscore >= stem.Length - 1)
            return fileName;

        return stem[..(underscore + 1)] + newSan + extension;
    }

    private static bool TryReplaceAlias(
        string value,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        out string replacement)
    {
        foreach (var oldAlias in oldAliases)
        {
            if (string.Equals(value, oldAlias, StringComparison.OrdinalIgnoreCase))
            {
                replacement = newSan;
                return true;
            }
        }

        replacement = value;
        return false;
    }

    private static IReadOnlyCollection<string> BuildAliases(params string?[] aliases)
        => aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? ResolveProjectRoot(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        return ProjectFileLocator.ProjectRootFromFile(projectFilePath)
               ?? Path.GetDirectoryName(projectFilePath);
    }

    private static string NormalizeDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        return full.EndsWith(Path.DirectorySeparatorChar)
            ? full
            : full + Path.DirectorySeparatorChar;
    }
}
