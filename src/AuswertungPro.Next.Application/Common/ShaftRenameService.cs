using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.UseCases.Schaechte;
using ShaftRenameResult = AuswertungPro.Next.Application.Common.ShaftRenameService.ShaftRenameResult;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Geplante Schachtumbenennung mit Vorpruefung und Ruecknahme bei Dateifehlern.
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

/// <summary>Dateisystem-Implementierung der Schachtumbenennung.</summary>
public sealed class ShaftRenameFileService : IShaftRenameService
{
    private static readonly string[] PathFields = [FieldKeys.PdfPath, FieldKeys.Link, FieldKeys.PdfEigen, FieldKeys.PdfAll];
    private static readonly string[] ShaftRoots = ["Schächte_Verteilt", "Schaechte_Verteilt", "Schächte"];

    public ShaftRenameResult Rename(
        SchachtRecord record,
        string oldShaftNumber,
        string newShaftNumber,
        string? projectFilePath)
    {
        ArgumentNullException.ThrowIfNull(record);
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldShaftNumber);
        var newSan = ProjectPathResolver.SanitizePathSegment(newShaftNumber);
        if (string.Equals(oldSan, newSan, StringComparison.OrdinalIgnoreCase))
            return ShaftRenameResult.Ok(false, 0);

        var applied = new List<ShaftRenamePlan.Move>();
        ShaftProtocolPathChanges? protocolPaths = null;
        var originalFields = PathFields.ToDictionary(field => field, record.GetFieldValue);
        try
        {
            var references = originalFields.Values
                .SelectMany(value => value.Split(';', StringSplitOptions.RemoveEmptyEntries))
                .Select(path => ProjectPathResolver.EnsureWritableProjectPath(path.Trim(), projectFilePath))
                .ToArray();
            var projectRoot = ProjectFileLocator.ProjectRootFromFile(projectFilePath);
            if (string.IsNullOrWhiteSpace(projectRoot))
                return ShaftRenameResult.Ok(false, 0); // Reine Stammdatenaenderung ohne Dateiverweise.

            var folder = LocateShaftFolder(references, oldSan, projectRoot, projectFilePath);
            var plan = new ShaftRenamePlan();
            if (folder is not null)
                AddFolder(plan, folder, newSan, [oldSan, Path.GetFileName(folder)], projectFilePath);

            var photos = ProjectPathResolver.EnsureWritableProjectPath(
                Path.Combine(projectRoot, "Fotos", "Schächte", oldSan), projectFilePath);
            if (!string.Equals(photos, folder, StringComparison.OrdinalIgnoreCase) && Directory.Exists(photos))
                AddFolder(plan, photos, newSan, [oldSan], projectFilePath);

            ValidatePlan(plan, projectFilePath);
            var updatedFields = originalFields.ToDictionary(
                pair => pair.Key,
                pair => MapReferences(pair.Value, plan, projectRoot, projectFilePath));
            protocolPaths = ShaftProtocolPathChanges.Prepare(record.Protocol,
                raw => MapReferences(raw, plan, projectRoot, projectFilePath, unchangedMayBeReadOnly: true));

            foreach (var move in plan.Moves)
            {
                MoveChecked(move.Source, move.Destination, move.IsDirectory, projectFilePath);
                applied.Add(move);
            }
            protocolPaths.Apply();
            foreach (var pair in updatedFields)
            {
                if (!string.Equals(pair.Value, originalFields[pair.Key], StringComparison.Ordinal))
                    record.SetFieldValueTechnical(pair.Key, pair.Value);
            }
            return ShaftRenameResult.Ok(
                applied.Any(move => move.IsDirectory),
                updatedFields.Count(pair => !string.Equals(pair.Value, originalFields[pair.Key], StringComparison.Ordinal)));
        }
        catch (Exception ex)
        {
            var rollbackError = Rollback(applied, projectFilePath);
            protocolPaths?.Restore();
            // Bereits gesendete UI-Benachrichtigungen duerfen das Wiederherstellen nicht verhindern.
            foreach (var pair in originalFields)
            {
                if (!string.Equals(record.GetFieldValue(pair.Key), pair.Value, StringComparison.Ordinal))
                    record.Fields[pair.Key] = pair.Value;
            }
            return ShaftRenameResult.Fail(rollbackError is null ? ex.Message : $"{ex.Message} Rücknahme fehlgeschlagen: {rollbackError}");
        }
    }

    private static string? LocateShaftFolder(
        IEnumerable<string> references, string oldNumber, string projectRoot, string? projectFile)
    {
        var knownRoots = ShaftRoots.Select(name => Path.GetFullPath(Path.Combine(projectRoot, name))).ToArray();
        foreach (var reference in references)
        {
            string? matchingAncestor = null;
            var current = Path.GetDirectoryName(reference);
            while (current is not null && !string.Equals(current, Path.GetFullPath(projectRoot), StringComparison.OrdinalIgnoreCase))
            {
                var parent = Path.GetDirectoryName(current);
                if (knownRoots.Contains(parent, StringComparer.OrdinalIgnoreCase))
                {
                    current = ProjectPathResolver.EnsureWritableProjectPath(current, projectFile);
                    if (Directory.Exists(current))
                        return current;
                }
                if (string.Equals(Path.GetFileName(current), oldNumber, StringComparison.OrdinalIgnoreCase))
                    matchingAncestor = current;
                current = parent;
            }
            if (matchingAncestor is not null && Directory.Exists(matchingAncestor))
                return ProjectPathResolver.EnsureWritableProjectPath(matchingAncestor, projectFile);
        }
        foreach (var root in knownRoots)
        {
            var folder = ProjectPathResolver.EnsureWritableProjectPath(Path.Combine(root, oldNumber), projectFile);
            if (Directory.Exists(folder))
                return folder;
        }
        return null;
    }

    private static void AddFolder(
        ShaftRenamePlan plan, string folder, string newNumber, string[] aliases, string? projectFile)
    {
        folder = ProjectPathResolver.EnsureWritableProjectPath(folder, projectFile);
        var target = ProjectPathResolver.EnsureWritableProjectPath(
            Path.Combine(Path.GetDirectoryName(folder)!, newNumber), projectFile);
        var files = new List<string>();
        var directories = new List<string>();
        var pending = new Stack<string>();
        pending.Push(folder);
        while (pending.TryPop(out var current))
        {
            ProjectPathResolver.EnsureWritableProjectPath(current, projectFile);
            foreach (var entry in Directory.EnumerateFileSystemEntries(current))
            {
                var safe = ProjectPathResolver.EnsureWritableProjectPath(entry, projectFile);
                if ((File.GetAttributes(safe) & FileAttributes.Directory) == 0)
                    files.Add(safe);
                else
                {
                    directories.Add(safe);
                    pending.Push(safe);
                }
            }
        }
        plan.AddFolder(folder, target, files, directories, aliases, newNumber);
    }

    private static void ValidatePlan(ShaftRenamePlan plan, string? projectFile)
    {
        var targets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var move in plan.Moves)
        {
            ProjectPathResolver.EnsureWritableProjectPath(move.Source, projectFile);
            var target = ProjectPathResolver.EnsureWritableProjectPath(move.Destination, projectFile);
            if (!targets.Add(target) || File.Exists(target) || Directory.Exists(target))
                throw new IOException($"Ziel existiert bereits oder ist mehrfach belegt: {target}");
        }
    }

    private static string MapReferences(
        string raw, ShaftRenamePlan plan, string projectRoot, string? projectFile, bool unchangedMayBeReadOnly = false)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return raw;
        var changed = false;
        var parts = raw.Split(';', StringSplitOptions.RemoveEmptyEntries).Select(part =>
        {
            var original = unchangedMayBeReadOnly
                ? Path.GetFullPath(Path.IsPathRooted(part.Trim()) ? part.Trim() : Path.Combine(projectRoot, part.Trim()))
                : ProjectPathResolver.EnsureWritableProjectPath(part.Trim(), projectFile);
            var mapped = plan.MapPath(original);
            if (string.Equals(original, mapped, StringComparison.OrdinalIgnoreCase))
                return part;
            ProjectPathResolver.EnsureWritableProjectPath(original, projectFile);
            ProjectPathResolver.EnsureWritableProjectPath(mapped, projectFile);
            changed = true;
            return (Path.IsPathRooted(part.Trim()) ? mapped : Path.GetRelativePath(projectRoot, mapped)).Replace('\\', '/');
        }).ToArray();
        return changed ? string.Join(';', parts) : raw;
    }

    private static void MoveChecked(string source, string destination, bool directory, string? projectFile)
    {
        source = ProjectPathResolver.EnsureWritableProjectPath(source, projectFile);
        destination = ProjectPathResolver.EnsureWritableProjectPath(destination, projectFile);
        if (directory)
            Directory.Move(source, destination);
        else
            File.Move(source, destination, overwrite: false);
    }

    private static string? Rollback(IReadOnlyList<ShaftRenamePlan.Move> moves, string? projectFile)
    {
        var errors = new List<string>();
        for (var i = moves.Count - 1; i >= 0; i--)
        {
            var move = moves[i];
            try { MoveChecked(move.Destination, move.Source, move.IsDirectory, projectFile); }
            catch (Exception ex) { errors.Add($"{move.Destination}: {ex.Message}"); }
        }
        return errors.Count == 0 ? null : string.Join(" | ", errors);
    }
}
