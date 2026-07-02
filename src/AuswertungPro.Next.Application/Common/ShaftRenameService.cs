using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Atomarer Rename fuer Schachtnummer: Ordner + Dateien + Pfad-Referenzen.
/// </summary>
public static class ShaftRenameService
{
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
    {
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldShaftNumber);
        var newSan = ProjectPathResolver.SanitizePathSegment(newShaftNumber);

        if (string.Equals(oldSan, newSan, StringComparison.OrdinalIgnoreCase))
            return ShaftRenameResult.Ok(false, 0);

        var folder = LocateShaftFolder(record, oldSan, projectFilePath);
        var folderRenamed = false;

        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parent))
                return ShaftRenameResult.Fail($"Uebergeordneter Ordner nicht ermittelbar: {folder}");

            var targetFolder = Path.Combine(parent, newSan);
            if (Directory.Exists(targetFolder))
                return ShaftRenameResult.Fail($"Zielordner existiert bereits: {targetFolder}");

            var result = RenameFilesystemWithRollback(folder, targetFolder, oldSan, newSan);
            if (!result.Success)
                return ShaftRenameResult.Fail(result.ErrorMessage!);

            folderRenamed = true;
        }

        if (RenameSiblingShaftFolder(projectFilePath, Path.Combine("Fotos", "Sch\u00e4chte"), oldSan, newSan, folder))
            folderRenamed = true;

        var updated = UpdateAllPaths(record, oldSan, newSan);
        return ShaftRenameResult.Ok(folderRenamed, updated);
    }

    private static string? LocateShaftFolder(SchachtRecord record, string oldSan, string? projectFilePath)
    {
        foreach (var field in new[] { FieldKeys.PdfPath, FieldKeys.Link, FieldKeys.PdfEigen })
        {
            var resolved = ProjectPathResolver.ResolveFilePath(record.GetFieldValue(field), projectFilePath);
            var folder = FindShaftFolderFromFile(resolved, oldSan);
            if (!string.IsNullOrWhiteSpace(folder))
                return folder;
        }

        if (string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        var projectDir = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                         ?? Path.GetDirectoryName(projectFilePath);
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

    private static string? FindShaftFolderFromFile(string? filePath, string oldSan)
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

        var result = RenameFilesystemWithRollback(src, dest, oldSan, newSan);
        return result.Success;
    }

    private sealed record FsResult(bool Success, string? ErrorMessage);

    private static FsResult RenameFilesystemWithRollback(string folder, string targetFolder, string oldSan, string newSan)
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
                if (string.IsNullOrWhiteSpace(name)
                    || name.IndexOf(oldSan, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var dest = Path.Combine(
                    folder,
                    name.Replace(oldSan, newSan, StringComparison.OrdinalIgnoreCase));
                if (string.Equals(file, dest, StringComparison.OrdinalIgnoreCase))
                    continue;

                File.Move(file, dest);
                renamedFiles.Add((file, dest));
            }

            Directory.Move(folder, targetFolder);
            folderMoved = true;
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

    private static int UpdateAllPaths(SchachtRecord record, string oldSan, string newSan)
    {
        var count = 0;
        count += UpdateFieldPath(record, FieldKeys.PdfPath, oldSan, newSan);
        count += UpdateFieldPath(record, FieldKeys.Link, oldSan, newSan);
        count += UpdateFieldPath(record, FieldKeys.PdfEigen, oldSan, newSan);

        var pdfAll = record.GetFieldValue(FieldKeys.PdfAll);
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            var parts = pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var newParts = parts.Select(p => ReplaceShaftInPath(p.Trim(), oldSan, newSan)).ToArray();
            var newValue = string.Join(";", newParts);
            if (!string.Equals(pdfAll, newValue, StringComparison.OrdinalIgnoreCase))
            {
                record.SetFieldValue(FieldKeys.PdfAll, newValue);
                count++;
            }
        }

        return count;
    }

    private static int UpdateFieldPath(SchachtRecord record, string fieldName, string oldSan, string newSan)
    {
        var raw = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var updated = ReplaceShaftInPath(raw, oldSan, newSan);
        if (string.Equals(raw, updated, StringComparison.OrdinalIgnoreCase))
            return 0;

        record.SetFieldValue(fieldName, updated);
        return 1;
    }

    internal static string ReplaceShaftInPath(string path, string oldSan, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var normalized = path.Replace('\\', '/');
        var parts = normalized.Split('/');
        for (var i = 0; i < parts.Length; i++)
        {
            if (string.Equals(parts[i], oldSan, StringComparison.OrdinalIgnoreCase))
                parts[i] = newSan;
        }

        var result = string.Join('/', parts);
        var sepIdx = result.LastIndexOf('/');
        var dir = sepIdx >= 0 ? result[..(sepIdx + 1)] : string.Empty;
        var file = sepIdx >= 0 ? result[(sepIdx + 1)..] : result;
        if (file.IndexOf(oldSan, StringComparison.OrdinalIgnoreCase) >= 0)
            file = file.Replace(oldSan, newSan, StringComparison.OrdinalIgnoreCase);

        return dir + file;
    }
}
