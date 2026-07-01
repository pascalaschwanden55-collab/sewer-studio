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

        // 2) Fallback: im Verteil-Ordner suchen (neue Struktur Haltungen_Verteilt\, alte Haltungen\).
        //    Gegen den Projekt-ROOT (nicht GetDirectoryName der projekt.json, die unter Projektdateien\
        //    liegen kann).
        if (!string.IsNullOrWhiteSpace(projectFilePath))
        {
            var projectDir = ProjectFileLocator.ProjectRootFromFile(projectFilePath)
                             ?? Path.GetDirectoryName(projectFilePath);
            if (!string.IsNullOrWhiteSpace(projectDir))
            {
                foreach (var rootName in new[] { "Haltungen_Verteilt", "Haltungen" })
                {
                    var holdingsRoot = Path.Combine(projectDir, rootName);
                    if (!Directory.Exists(holdingsRoot))
                        continue;

                    var direct = Path.Combine(holdingsRoot, oldSan);
                    if (Directory.Exists(direct))
                        return direct;

                    try
                    {
                        var found = Directory.EnumerateDirectories(holdingsRoot, oldSan, SearchOption.TopDirectoryOnly)
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

    // ── Dateisystem-Rename mit Rollback ───────────────────────────────────

    private sealed record FsResult(bool Success, string? ErrorMessage);

    private static FsResult RenameFilesystemWithRollback(
        string folder, string targetFolder, string oldSan, string newSan)
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
                if (stem.IndexOf(oldSan, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;

                var ext = Path.GetExtension(name);
                var newStem = stem.Replace(oldSan, newSan, StringComparison.OrdinalIgnoreCase);
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

    private static int UpdateAllPaths(HaltungRecord record, string oldSan, string newSan)
    {
        var count = 0;

        // Link (Video)
        count += UpdateFieldPath(record, "Link", oldSan, newSan);

        // PDF_Path (Original-Protokoll)
        count += UpdateFieldPath(record, "PDF_Path", oldSan, newSan);

        // PDF_Eigen (generiertes _E-Protokoll)
        count += UpdateFieldPath(record, "PDF_Eigen", oldSan, newSan);

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

    // ── Pfad-Ersetzung (delegiert an HoldingPathRewriter) ────────────────

    /// <summary>
    /// Ersetzt die Haltungsnummer in einem Pfad: zuerst die Ordner-Segmente
    /// (<see cref="HoldingPathRewriter.ReplaceHoldingInPath"/>), dann zusaetzlich als Token im
    /// Dateinamen (letzte Komponente). Notwendig, weil die Verteilung die Haltung auch in den
    /// Dateinamen einbettet (z.B. 20250310-&lt;Haltung&gt;.mp4) - sonst zeigt der Link nach dem
    /// Rename auf eine nicht existierende Datei (Ordner neu, Dateiname alt).
    /// </summary>
    internal static string ReplaceHoldingInPath(string path, string oldSan, string newSan)
    {
        if (string.IsNullOrWhiteSpace(path))
            return path;

        var result = HoldingPathRewriter.ReplaceHoldingInPath(path, oldSan, newSan);

        var sepIdx = result.LastIndexOfAny(new[] { '/', '\\' });
        var dir = sepIdx >= 0 ? result[..(sepIdx + 1)] : string.Empty;
        var file = sepIdx >= 0 ? result[(sepIdx + 1)..] : result;
        if (file.IndexOf(oldSan, StringComparison.OrdinalIgnoreCase) >= 0)
            file = file.Replace(oldSan, newSan, StringComparison.OrdinalIgnoreCase);

        return dir + file;
    }
}
