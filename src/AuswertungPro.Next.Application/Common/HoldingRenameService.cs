using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Application.Common;

/// <summary>
/// Atomarer Rename fuer Haltungsname: Ordner + Dateien + alle Pfad-Referenzen.
/// Schlaegt der Dateisystem-Rename fehl, werden bereits umbenannte Dateien
/// zurueckgerollt und die Methode gibt Fail zurueck.
/// </summary>
public static class HoldingRenameService
{
    public sealed record HoldingRenameResult(
        bool Success,
        string? ErrorMessage,
        bool FolderRenamed,
        int PathFieldsUpdated)
    {
        public static HoldingRenameResult Ok(bool folderRenamed, int pathFields)
            => new(true, null, folderRenamed, pathFields);
        public static HoldingRenameResult Fail(string message)
            => new(false, message, false, 0);
    }

    /// <summary>
    /// Benennt die Haltung atomar um: Dateisystem-Ordner + alle Pfad-Felder im Record.
    /// ACHTUNG: Setzt NICHT das Feld "Haltungsname" selbst — das muss der Aufrufer
    /// nach erfolgreichem Rename tun.
    /// </summary>
    public static HoldingRenameResult Rename(
        HaltungRecord record,
        string oldHolding,
        string newHolding,
        string? projectFilePath)
    {
        var oldSan = ProjectPathResolver.SanitizePathSegment(oldHolding);
        var newSan = ProjectPathResolver.SanitizePathSegment(newHolding);

        if (string.Equals(oldSan, newSan, StringComparison.OrdinalIgnoreCase))
            return HoldingRenameResult.Ok(false, 0);

        // ── Phase 1: Haltungsordner lokalisieren ──────────────────────────
        var searchAliases = BuildAliases(oldSan, BuildReversedNumericHoldingAlias(oldSan));
        var folder = LocateHoldingFolder(record, searchAliases, projectFilePath);
        var oldAliases = BuildAliases(oldSan, BuildReversedNumericHoldingAlias(oldSan), Path.GetFileName(folder ?? string.Empty));
        string? targetFolder = null;
        var folderRenamed = false;

        var fotosCollision = FindSiblingHoldingFolderCollision(
            projectFilePath,
            Path.Combine("Fotos", "Haltungen"),
            oldAliases,
            newSan);
        if (!string.IsNullOrWhiteSpace(fotosCollision))
            return HoldingRenameResult.Fail($"Fotos-Zielordner existiert bereits: {fotosCollision}");

        // ── Phase 2: Dateisystem-Rename (mit Rollback) ───────────────────
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parent))
                return HoldingRenameResult.Fail($"Uebergeordneter Ordner nicht ermittelbar: {folder}");

            targetFolder = Path.Combine(parent, newSan);

            if (Directory.Exists(targetFolder))
                return HoldingRenameResult.Fail($"Zielordner existiert bereits: {targetFolder}");

            var rollbackResult = RenameFilesystemWithRollback(folder, targetFolder, oldAliases, newSan);
            if (!rollbackResult.Success)
                return HoldingRenameResult.Fail(rollbackResult.ErrorMessage!);

            folderRenamed = true;
        }

        // ── Phase 2b: Fotos-Ordner der Haltung mit umbenennen ────────────
        //    Die Fotos liegen in einem SEPARATEN Verteil-Ort (Fotos\Haltungen\<H>\), der nicht ueber
        //    den Video-Link gefunden wird. Ordner + Dateien (Haltung im Dateinamen) mitumbenennen,
        //    damit die in Phase 3 aktualisierten FotoPath-Felder auf existierende Dateien zeigen.
        var photoRenameResult = RenameSiblingHoldingFolder(
            projectFilePath,
            Path.Combine("Fotos", "Haltungen"),
            oldAliases,
            newSan,
            folder);
        if (!photoRenameResult.Success)
        {
            var rollbackMessage = string.Empty;
            if (folderRenamed
                && !string.IsNullOrWhiteSpace(folder)
                && !string.IsNullOrWhiteSpace(targetFolder)
                && Directory.Exists(targetFolder))
            {
                var rollback = RenameFilesystemWithRollback(targetFolder, folder, BuildAliases(newSan), oldSan);
                if (!rollback.Success)
                    rollbackMessage = $" Rollback Haltungsordner fehlgeschlagen: {rollback.ErrorMessage}";
            }

            return HoldingRenameResult.Fail(
                $"Fotos-Ordner konnte nicht umbenannt werden: {photoRenameResult.ErrorMessage}{rollbackMessage}");
        }

        if (photoRenameResult.FolderRenamed)
            folderRenamed = true;

        // ── Phase 3: Alle Pfad-Referenzen im Record aktualisieren ────────
        var updated = UpdateAllPaths(record, oldAliases, newSan, projectFilePath);

        return HoldingRenameResult.Ok(folderRenamed, updated);
    }

    private static string? FindSiblingHoldingFolderCollision(
        string? projectFilePath,
        string relativeParent,
        IReadOnlyCollection<string> oldAliases,
        string newSan)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                   ?? Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(root))
            return null;

        var src = oldAliases
            .Select(alias => Path.Combine(root, relativeParent, alias))
            .FirstOrDefault(Directory.Exists);
        if (!Directory.Exists(src))
            return null;

        var dest = Path.Combine(root, relativeParent, newSan);
        return Directory.Exists(dest) ? dest : null;
    }

    private sealed record SiblingRenameResult(bool Success, string? ErrorMessage, bool FolderRenamed);

    // Benennt einen parallelen, haltungsbenannten Verteil-Ordner um (z.B. Fotos\Haltungen\<H>\),
    // der NICHT ueber den Link auffindbar ist. Gegen den Projekt-ROOT aufgeloest.
    private static SiblingRenameResult RenameSiblingHoldingFolder(
        string? projectFilePath,
        string relativeParent,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        string? alreadyRenamedFolder)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return new SiblingRenameResult(true, null, false);

        var root = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                   ?? Path.GetDirectoryName(projectFilePath);
        if (string.IsNullOrWhiteSpace(root))
            return new SiblingRenameResult(true, null, false);

        var src = oldAliases
            .Select(alias => Path.Combine(root, relativeParent, alias))
            .FirstOrDefault(Directory.Exists);
        if (!Directory.Exists(src))
            return new SiblingRenameResult(true, null, false);

        // Nicht denselben Ordner doppelt behandeln, den Phase 2 bereits umbenannt hat.
        if (!string.IsNullOrWhiteSpace(alreadyRenamedFolder)
            && string.Equals(Path.GetFullPath(src), Path.GetFullPath(alreadyRenamedFolder), StringComparison.OrdinalIgnoreCase))
            return new SiblingRenameResult(true, null, false);

        var dest = Path.Combine(root, relativeParent, newSan);
        if (Directory.Exists(dest))
            return new SiblingRenameResult(false, $"Fotos-Zielordner existiert bereits: {dest}", false);

        var result = RenameFilesystemWithRollback(src, dest, oldAliases, newSan);
        return new SiblingRenameResult(result.Success, result.ErrorMessage, result.Success);
    }

    // ── Ordner-Suche ──────────────────────────────────────────────────────

    private static string? LocateHoldingFolder(
        HaltungRecord record,
        IReadOnlyCollection<string> oldAliases,
        string? projectFilePath)
    {
        var projectDir = !string.IsNullOrWhiteSpace(projectFilePath)
            ? ProjectFileLocator.ProjectRootFromFile(projectFilePath) ?? Path.GetDirectoryName(projectFilePath)
            : null;

        // 1) Ueber gespeicherte Datei-Pfade. Wenn der physische Ordner anders heisst
        // als der alte Datenwert (z.B. umgekehrte Schachtreihenfolge), zaehlt der
        // tatsaechliche Ordner unter Haltungen_Verteilt/Haltungen als Quelle.
        foreach (var raw in EnumeratePathValues(record))
        {
            var resolved = ProjectPathResolver.ResolveFilePath(raw, projectFilePath);
            var fromPath = FindHoldingFolderFromFile(resolved, oldAliases, projectDir);
            if (!string.IsNullOrWhiteSpace(fromPath))
                return fromPath;
        }

        // 2) Fallback: im Verteil-Ordner suchen (neue Struktur Haltungen_Verteilt\, alte Haltungen\).
        //    Gegen den Projekt-ROOT (nicht GetDirectoryName der projekt.json, die unter Projektdateien\
        //    liegen kann).
        if (!string.IsNullOrWhiteSpace(projectDir))
        {
            foreach (var rootName in new[] { "Haltungen_Verteilt", "Haltungen" })
            {
                var holdingsRoot = Path.Combine(projectDir, rootName);
                if (!Directory.Exists(holdingsRoot))
                    continue;

                foreach (var oldAlias in oldAliases)
                {
                    var direct = Path.Combine(holdingsRoot, oldAlias);
                    if (Directory.Exists(direct))
                        return direct;

                    try
                    {
                        var found = Directory.EnumerateDirectories(holdingsRoot, oldAlias, SearchOption.TopDirectoryOnly)
                            .FirstOrDefault();
                        if (!string.IsNullOrWhiteSpace(found))
                            return found;
                    }
                    catch { /* ignore search errors */ }
                }
            }
        }

        return null;
    }

    private static IEnumerable<string> EnumeratePathValues(HaltungRecord record)
    {
        foreach (var field in new[] { FieldKeys.Link, "Link_G", FieldKeys.PdfPath, FieldKeys.PdfEigen })
        {
            var raw = record.GetFieldValue(field);
            if (!string.IsNullOrWhiteSpace(raw))
                yield return raw;
        }

        var pdfAll = record.GetFieldValue(FieldKeys.PdfAll);
        if (string.IsNullOrWhiteSpace(pdfAll))
            yield break;

        foreach (var part in pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var trimmed = part.Trim();
            if (!string.IsNullOrWhiteSpace(trimmed))
                yield return trimmed;
        }
    }

    private static string? FindHoldingFolderFromFile(
        string? filePath,
        IReadOnlyCollection<string> oldAliases,
        string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return null;

        var dir = Path.GetDirectoryName(filePath);
        if (string.IsNullOrWhiteSpace(dir))
            return null;

        if (oldAliases.Contains(Path.GetFileName(dir), StringComparer.OrdinalIgnoreCase))
            return dir;

        var parent = Path.GetDirectoryName(dir);
        if (!string.IsNullOrWhiteSpace(parent)
            && oldAliases.Contains(Path.GetFileName(parent), StringComparer.OrdinalIgnoreCase))
            return parent;

        return FindHoldingFolderUnderKnownRoot(dir, projectDir);
    }

    private static string? FindHoldingFolderUnderKnownRoot(string directory, string? projectDir)
    {
        if (string.IsNullOrWhiteSpace(projectDir))
            return null;

        foreach (var rootName in new[] { "Haltungen_Verteilt", "Haltungen" })
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

            var holdingFolder = Path.Combine(root, firstSegment);
            if (Directory.Exists(holdingFolder))
                return holdingFolder;
        }

        return null;
    }

    // ── Dateisystem-Rename mit Rollback ───────────────────────────────────

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
            // Dateien umbenennen: die (sanitierte) Haltungsnummer als Token im Dateinamen
            // ersetzen - separator-agnostisch. Deckt beide realen Schemata ab:
            //   JJJJMMTT-<Haltung>.mp4 / ...-<Haltung>_G.mp4  (Bindestrich + _G, aktuell)
            //   JJJJMMTT_<Haltung>-g.mp4                       (Unterstrich + -g, alt)
            // Datum-Praefix, Gegeninspektions-Suffix (_G/-g) und Endung bleiben erhalten;
            // ausgetauscht wird nur die Haltungsnummer selbst.
            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder); }
            catch { files = Array.Empty<string>(); }

            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var stem = Path.GetFileNameWithoutExtension(name);
                var oldAlias = oldAliases.FirstOrDefault(alias =>
                    stem.IndexOf(alias, StringComparison.OrdinalIgnoreCase) >= 0);
                if (string.IsNullOrWhiteSpace(oldAlias))
                    continue;

                var ext = Path.GetExtension(name);
                var newStem = stem.Replace(oldAlias, newSan, StringComparison.OrdinalIgnoreCase);
                var dest = Path.Combine(folder, newStem + ext);

                if (!string.Equals(f, dest, StringComparison.OrdinalIgnoreCase))
                {
                    File.Move(f, dest);
                    renamedFiles.Add((f, dest));
                }
            }

            // Ordner verschieben
            Directory.Move(folder, targetFolder);
            folderMoved = true;

            return new FsResult(true, null);
        }
        catch (Exception ex)
        {
            // Rollback: Dateien zurueck-umbenennen
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

    // ── Pfad-Updates (in-memory) ──────────────────────────────────────────

    private static int UpdateAllPaths(
        HaltungRecord record,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        string? projectFilePath)
    {
        var count = 0;
        var projectRoot = ResolveProjectRoot(projectFilePath);

        // Link (Video)
        count += UpdateFieldPath(record, FieldKeys.Link, oldAliases, newSan);

        // Link_G (Gegeninspektions-Video)
        count += UpdateFieldPath(record, "Link_G", oldAliases, newSan);

        // PDF_Path (Original-Protokoll)
        count += UpdateFieldPath(record, FieldKeys.PdfPath, oldAliases, newSan);

        // PDF_Eigen (generiertes _E-Protokoll)
        count += UpdateFieldPath(record, FieldKeys.PdfEigen, oldAliases, newSan);

        // PDF_All (Semikolon-getrennt)
        var pdfAll = record.GetFieldValue(FieldKeys.PdfAll);
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            var parts = pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var newParts = parts.Select(p => ReplaceHoldingInPath(p.Trim(), oldAliases, newSan)).ToArray();
            var newVal = string.Join(";", newParts);
            if (!string.Equals(pdfAll, newVal, StringComparison.OrdinalIgnoreCase))
            {
                record.SetFieldValue(FieldKeys.PdfAll, newVal, FieldSource.Manual, userEdited: false);
                count++;
            }
        }

        // Protocol
        if (record.Protocol != null)
        {
            record.Protocol.HaltungId = ReplaceFirstAlias(record.Protocol.HaltungId, oldAliases, newSan) ?? newSan;
            count += UpdateRevisionPaths(record.Protocol.Original, oldAliases, newSan, projectRoot);
            count += UpdateRevisionPaths(record.Protocol.Current, oldAliases, newSan, projectRoot);
            foreach (var rev in record.Protocol.History)
                count += UpdateRevisionPaths(rev, oldAliases, newSan, projectRoot);
        }

        // VsaFindings
        if (record.VsaFindings != null)
        {
            foreach (var finding in record.VsaFindings)
            {
                if (!string.IsNullOrWhiteSpace(finding.FotoPath))
                {
                    var newPath = ReplaceHoldingPhotoPath(finding.FotoPath, oldAliases, newSan, projectRoot);
                    if (!string.Equals(finding.FotoPath, newPath, StringComparison.OrdinalIgnoreCase))
                    {
                        finding.FotoPath = newPath;
                        count++;
                    }
                }
            }
        }

        return count;
    }

    private static int UpdateFieldPath(
        HaltungRecord record,
        string fieldName,
        IReadOnlyCollection<string> oldAliases,
        string newSan)
    {
        var raw = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var updated = ReplaceHoldingInPath(raw, oldAliases, newSan);
        if (string.Equals(raw, updated, StringComparison.OrdinalIgnoreCase))
            return 0;

        record.SetFieldValue(fieldName, updated, FieldSource.Manual, userEdited: false);
        return 1;
    }

    private static int UpdateRevisionPaths(
        ProtocolRevision revision,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        string? projectRoot)
    {
        var count = 0;
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var path = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(path)) continue;

                var newPath = ReplaceHoldingPhotoPath(path, oldAliases, newSan, projectRoot);
                if (!string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    entry.FotoPaths[i] = newPath;
                    count++;
                }
            }
            count += DeduplicatePhotoPaths(entry.FotoPaths);
        }
        return count;
    }

    private static int DeduplicatePhotoPaths(IList<string> paths)
    {
        var removed = 0;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            var key = (paths[i] ?? string.Empty).Replace('\\', '/').Trim();
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (seen.Add(key))
                continue;

            paths.RemoveAt(i);
            removed++;
        }

        return removed;
    }

    private static string? ResolveProjectRoot(string? projectFilePath)
    {
        if (string.IsNullOrWhiteSpace(projectFilePath))
            return null;

        return ProjectFileLocator.ProjectRootFromFile(projectFilePath)
               ?? Path.GetDirectoryName(projectFilePath);
    }

    private static string ReplaceHoldingPhotoPath(
        string path,
        IReadOnlyCollection<string> oldAliases,
        string newSan,
        string? projectRoot)
    {
        var rewritten = ReplaceHoldingInPath(path, oldAliases, newSan);
        if (string.IsNullOrWhiteSpace(projectRoot) || !LooksLikeImagePath(rewritten))
            return rewritten;

        var fileName = Path.GetFileName(rewritten.Replace('/', Path.DirectorySeparatorChar));
        if (string.IsNullOrWhiteSpace(fileName)
            || fileName.IndexOf(newSan, StringComparison.OrdinalIgnoreCase) < 0)
            return rewritten;

        var centralAbs = Path.Combine(projectRoot, "Fotos", "Haltungen", newSan, fileName);
        if (!File.Exists(centralAbs))
            return rewritten;

        return Path.Combine("Fotos", "Haltungen", newSan, fileName).Replace('\\', '/');
    }

    private static bool LooksLikeImagePath(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".png", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".bmp", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".gif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".tif", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".tiff", StringComparison.OrdinalIgnoreCase)
               || ext.Equals(".webp", StringComparison.OrdinalIgnoreCase);
    }

    // ── Pfad-Ersetzung (delegiert an HoldingPathRewriter) ────────────────

    /// <summary>
    /// Ersetzt die Haltungsnummer in einem Pfad: zuerst die Ordner-Segmente
    /// (<see cref="HoldingPathRewriter.ReplaceHoldingInPath"/>), dann zusaetzlich als Token im
    /// Dateinamen (letzte Komponente). Notwendig, weil die Verteilung die Haltung auch in den
    /// Dateinamen einbettet (z.B. 20250310-&lt;Haltung&gt;.mp4) - sonst zeigt der Link nach dem
    /// Rename auf eine nicht existierende Datei (Ordner neu, Dateiname alt).
    /// </summary>
    internal static string ReplaceHoldingInPath(string path, string oldSan, string newSan)
        => ReplaceHoldingInPath(path, BuildAliases(oldSan, BuildReversedNumericHoldingAlias(oldSan)), newSan);

    private static string ReplaceHoldingInPath(string path, IReadOnlyCollection<string> oldAliases, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var result = path;
        foreach (var oldAlias in oldAliases)
            result = HoldingPathRewriter.ReplaceHoldingInPath(result, oldAlias, newSan);

        var sepIdx = result.LastIndexOfAny(new[] { '/', '\\' });
        var dir = sepIdx >= 0 ? result[..(sepIdx + 1)] : string.Empty;
        var file = sepIdx >= 0 ? result[(sepIdx + 1)..] : result;
        foreach (var oldAlias in oldAliases)
        {
            if (file.IndexOf(oldAlias, StringComparison.OrdinalIgnoreCase) >= 0)
                file = file.Replace(oldAlias, newSan, StringComparison.OrdinalIgnoreCase);
        }

        return dir + file;
    }

    private static string? ReplaceFirstAlias(string? value, IReadOnlyCollection<string> oldAliases, string newSan)
    {
        if (string.IsNullOrWhiteSpace(value))
            return value;

        foreach (var oldAlias in oldAliases)
        {
            if (value.IndexOf(oldAlias, StringComparison.OrdinalIgnoreCase) >= 0)
                return value.Replace(oldAlias, newSan, StringComparison.OrdinalIgnoreCase);
        }

        return value;
    }

    private static IReadOnlyCollection<string> BuildAliases(params string?[] aliases)
        => aliases
            .Where(alias => !string.IsNullOrWhiteSpace(alias))
            .Select(alias => alias!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

    private static string? BuildReversedNumericHoldingAlias(string? holding)
    {
        if (string.IsNullOrWhiteSpace(holding))
            return null;

        var parts = holding.Split('-', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !IsNumericHoldingEndpoint(parts[0]) || !IsNumericHoldingEndpoint(parts[1]))
            return null;

        return $"{parts[1]}-{parts[0]}";
    }

    private static bool IsNumericHoldingEndpoint(string value)
        => value.Any(char.IsDigit) && value.All(c => char.IsDigit(c) || c == '.');

    private static string NormalizeDirectory(string directory)
    {
        var full = Path.GetFullPath(directory);
        return full.EndsWith(Path.DirectorySeparatorChar)
            ? full
            : full + Path.DirectorySeparatorChar;
    }
}
