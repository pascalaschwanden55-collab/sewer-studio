using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
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
        var folder = LocateHoldingFolder(record, oldSan, projectFilePath);
        string? targetFolder = null;
        var folderRenamed = false;

        // ── Phase 2: Dateisystem-Rename (mit Rollback) ───────────────────
        if (!string.IsNullOrWhiteSpace(folder) && Directory.Exists(folder))
        {
            var parent = Path.GetDirectoryName(folder);
            if (string.IsNullOrWhiteSpace(parent))
                return HoldingRenameResult.Fail($"Uebergeordneter Ordner nicht ermittelbar: {folder}");

            targetFolder = Path.Combine(parent, newSan);

            if (Directory.Exists(targetFolder))
                return HoldingRenameResult.Fail($"Zielordner existiert bereits: {targetFolder}");

            var rollbackResult = RenameFilesystemWithRollback(folder, targetFolder, oldSan, newSan);
            if (!rollbackResult.Success)
                return HoldingRenameResult.Fail(rollbackResult.ErrorMessage!);

            folderRenamed = true;
        }

        // ── Phase 3: Alle Pfad-Referenzen im Record aktualisieren ────────
        var updated = UpdateAllPaths(record, oldSan, newSan);

        return HoldingRenameResult.Ok(folderRenamed, updated);
    }

    // ── Ordner-Suche ──────────────────────────────────────────────────────

    private static string? LocateHoldingFolder(HaltungRecord record, string oldSan, string? projectFilePath)
    {
        // 1) Ueber Link-Feld
        var link = record.GetFieldValue("Link")?.Trim();
        if (!string.IsNullOrWhiteSpace(link))
        {
            var resolved = ProjectPathResolver.ResolveFilePath(link, projectFilePath);
            if (!string.IsNullOrWhiteSpace(resolved))
            {
                var dir = Path.GetDirectoryName(resolved);
                // Link zeigt oft in Video/-Unterordner -> eine Ebene hoch pruefen
                if (!string.IsNullOrWhiteSpace(dir))
                {
                    var dirName = Path.GetFileName(dir);
                    if (string.Equals(dirName, oldSan, StringComparison.OrdinalIgnoreCase))
                        return dir;
                    var parent = Path.GetDirectoryName(dir);
                    if (!string.IsNullOrWhiteSpace(parent)
                        && string.Equals(Path.GetFileName(parent), oldSan, StringComparison.OrdinalIgnoreCase))
                        return parent;
                }
            }
        }

        // 2) Fallback: im Haltungen-Ordner suchen
        if (!string.IsNullOrWhiteSpace(projectFilePath))
        {
            var projectDir = Path.GetDirectoryName(projectFilePath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                var holdingsRoot = Path.Combine(projectDir, "Haltungen");
                if (Directory.Exists(holdingsRoot))
                {
                    var direct = Path.Combine(holdingsRoot, oldSan);
                    if (Directory.Exists(direct))
                        return direct;

                    try
                    {
                        return Directory.EnumerateDirectories(holdingsRoot, oldSan, SearchOption.TopDirectoryOnly)
                            .FirstOrDefault();
                    }
                    catch { /* ignore search errors */ }
                }
            }
        }

        return null;
    }

    // ── Dateisystem-Rename mit Rollback ───────────────────────────────────

    private sealed record FsResult(bool Success, string? ErrorMessage);

    private static FsResult RenameFilesystemWithRollback(
        string folder, string targetFolder, string oldSan, string newSan)
    {
        var renamedFiles = new List<(string OldPath, string NewPath)>();
        var folderMoved = false;

        try
        {
            // Dateien umbenennen (Muster: YYYYMMDD_HALTUNGSNAME...)
            var pattern = new Regex(
                @"^(?<d>\d{8})_" + Regex.Escape(oldSan) + @"(?<g>-g)?(?<rest>.*)$",
                RegexOptions.IgnoreCase);

            IEnumerable<string> files;
            try { files = Directory.EnumerateFiles(folder); }
            catch { files = Array.Empty<string>(); }

            foreach (var f in files)
            {
                var name = Path.GetFileName(f);
                if (string.IsNullOrWhiteSpace(name)) continue;

                var m = pattern.Match(Path.GetFileNameWithoutExtension(name));
                if (!m.Success) continue;

                var ext = Path.GetExtension(name);
                var date = m.Groups["d"].Value;
                var g = m.Groups["g"].Value;
                var rest = m.Groups["rest"].Value;
                var newName = $"{date}_{newSan}{g}{rest}{ext}";
                var dest = Path.Combine(folder, newName);

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

    private static int UpdateAllPaths(HaltungRecord record, string oldSan, string newSan)
    {
        var count = 0;

        // Link (Video)
        count += UpdateFieldPath(record, "Link", oldSan, newSan);

        // PDF_Path
        count += UpdateFieldPath(record, "PDF_Path", oldSan, newSan);

        // PDF_All (Semikolon-getrennt)
        var pdfAll = record.GetFieldValue("PDF_All");
        if (!string.IsNullOrWhiteSpace(pdfAll))
        {
            var parts = pdfAll.Split(';', StringSplitOptions.RemoveEmptyEntries);
            var newParts = parts.Select(p => ReplaceHoldingInPath(p.Trim(), oldSan, newSan)).ToArray();
            var newVal = string.Join(";", newParts);
            if (!string.Equals(pdfAll, newVal, StringComparison.OrdinalIgnoreCase))
            {
                record.SetFieldValue("PDF_All", newVal, FieldSource.Manual, userEdited: false);
                count++;
            }
        }

        // Protocol
        if (record.Protocol != null)
        {
            record.Protocol.HaltungId = record.Protocol.HaltungId?.Replace(oldSan, newSan) ?? newSan;
            count += UpdateRevisionPaths(record.Protocol.Original, oldSan, newSan);
            count += UpdateRevisionPaths(record.Protocol.Current, oldSan, newSan);
            foreach (var rev in record.Protocol.History)
                count += UpdateRevisionPaths(rev, oldSan, newSan);
        }

        // VsaFindings
        if (record.VsaFindings != null)
        {
            foreach (var finding in record.VsaFindings)
            {
                if (!string.IsNullOrWhiteSpace(finding.FotoPath))
                {
                    var newPath = ReplaceHoldingInPath(finding.FotoPath, oldSan, newSan);
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

    private static int UpdateFieldPath(HaltungRecord record, string fieldName, string oldSan, string newSan)
    {
        var raw = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return 0;

        var updated = ReplaceHoldingInPath(raw, oldSan, newSan);
        if (string.Equals(raw, updated, StringComparison.OrdinalIgnoreCase))
            return 0;

        record.SetFieldValue(fieldName, updated, FieldSource.Manual, userEdited: false);
        return 1;
    }

    private static int UpdateRevisionPaths(ProtocolRevision revision, string oldSan, string newSan)
    {
        var count = 0;
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var path = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(path)) continue;

                var newPath = ReplaceHoldingInPath(path, oldSan, newSan);
                if (!string.Equals(path, newPath, StringComparison.OrdinalIgnoreCase))
                {
                    entry.FotoPaths[i] = newPath;
                    count++;
                }
            }
        }
        return count;
    }

    // ── Pfad-Ersetzung ───────────────────────────────────────────────────

    /// <summary>
    /// Ersetzt das Haltungsname-Segment in einem Pfad.
    /// Funktioniert mit Backslash, Forward-Slash, am Anfang und am Ende.
    /// </summary>
    internal static string ReplaceHoldingInPath(string path, string oldSan, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var result = path;

        // Mitte: \OLD\ und /OLD/
        result = result.Replace("\\" + oldSan + "\\", "\\" + newSan + "\\", StringComparison.OrdinalIgnoreCase);
        result = result.Replace("/" + oldSan + "/", "/" + newSan + "/", StringComparison.OrdinalIgnoreCase);

        // Ende: \OLD oder /OLD (ohne Trailing-Separator)
        if (result.EndsWith("\\" + oldSan, StringComparison.OrdinalIgnoreCase))
            result = result[..^oldSan.Length] + newSan;
        else if (result.EndsWith("/" + oldSan, StringComparison.OrdinalIgnoreCase))
            result = result[..^oldSan.Length] + newSan;

        // Anfang: OLD\ oder OLD/ (relative Pfade)
        if (result.StartsWith(oldSan + "\\", StringComparison.OrdinalIgnoreCase))
            result = newSan + result[oldSan.Length..];
        else if (result.StartsWith(oldSan + "/", StringComparison.OrdinalIgnoreCase))
            result = newSan + result[oldSan.Length..];

        return result;
    }
}
