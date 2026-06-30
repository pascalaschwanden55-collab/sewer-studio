using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kopiert beim Import aufgeloeste Mediendateien (Video, Fotos, PDFs) in die
/// Projektordner-Struktur und ersetzt absolute Pfade durch relative.
/// </summary>
public sealed class MediaDistributionService
{
    public sealed record CopyProgress(int Processed, int Total, string? CurrentFile);

    public sealed record CopyResult(
        int FilesCopied,
        int FilesSkipped,
        int Errors,
        IReadOnlyList<string> Messages);

    /// <summary>
    /// Kopiert alle referenzierten Mediendateien in die Haltungs-Unterordner
    /// und ersetzt die Pfade im Projekt durch relative Pfade.
    /// </summary>
    public CopyResult DistributeImportedMedia(
        string projectFolder,
        Project project,
        IProgress<CopyProgress>? progress = null,
        CancellationToken ct = default,
        bool dryRun = false,
        object? collectionLock = null)
    {
        var copied = 0;
        var skipped = 0;
        var errors = 0;
        var messages = new List<string>();
        var processed = 0;
        var records = SnapshotRecords(project, collectionLock);
        var total = records.Count;

        foreach (var record in records)
        {
            ct.ThrowIfCancellationRequested();
            var haltungsname = record.GetFieldValue("Haltungsname")?.Trim();
            if (string.IsNullOrWhiteSpace(haltungsname))
            {
                skipped++;
                processed++;
                progress?.Report(new CopyProgress(processed, total, null));
                continue;
            }

            var sanitized = SanitizePathSegment(haltungsname);
            var holdingRoot = Path.Combine(projectFolder, "Haltungen", sanitized);

            // 1) Video (Link-Feld)
            CopyFieldFile(record, "Link", holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);

            // 2) PDF_Path
            CopyFieldFile(record, "PDF_Path", holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);

            // 3) PDF_All (semikolon-getrennt)
            CopyFieldFileList(record, "PDF_All", holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);

            // 4) Protokoll-FotoPaths (Original, Current, History)
            if (record.Protocol != null)
                CopyProtocolFotos(record.Protocol, holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);

            // 5) VsaFindings FotoPath
            if (record.VsaFindings != null)
                CopyVsaFindingFotos(record.VsaFindings, holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);

            processed++;
            progress?.Report(new CopyProgress(processed, total, haltungsname));
        }

        if (!dryRun)
            project.Dirty = true;
        return new CopyResult(copied, skipped, errors, messages);
    }

    private static void CopyFieldFile(
        HaltungRecord record, string fieldName, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages, bool dryRun = false)
    {
        var rawPath = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        // Relativer Pfad: pruefen ob Datei existiert, sonst reparieren
        if (ProjectPathResolver.IsRelative(rawPath))
        {
            if (!ProjectPathResolver.IsSafeRelativeProjectPath(rawPath))
            {
                messages.Add($"{fieldName}: Unsicherer relativer Pfad: {rawPath}");
                return;
            }

            var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(rawPath, projectFolder);
            if (resolved is not null)
                return; // Alles OK, Datei existiert

            // Datei nicht gefunden - nach Dateiname in Haltungen-Ordner suchen
            var fileName = Path.GetFileName(rawPath);
            var found = SearchFileInHaltungen(projectFolder, holdingRoot, fileName, messages, fieldName);
            if (found != null)
            {
                var newRelative = ProjectPathResolver.MakeRelative(found, projectFolder);
                if (!dryRun)
                    record.SetFieldValue(fieldName, newRelative, FieldSource.Legacy, userEdited: false);
                messages.Add($"{fieldName}: {(dryRun ? "Wuerde reparieren" : "Repariert")}: {rawPath} -> {newRelative}");
                copied++;
            }
            else
            {
                messages.Add($"{fieldName}: Relative Datei nicht gefunden: {rawPath}");
            }
            return;
        }

        if (!File.Exists(rawPath))
        {
            messages.Add($"{fieldName}: Datei nicht gefunden: {rawPath}");
            return;
        }

        try
        {
            var subfolder = GetSubfolder(Path.GetExtension(rawPath));
            var destDir = Path.Combine(holdingRoot, subfolder);
            if (!dryRun) Directory.CreateDirectory(destDir);
            var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(rawPath)) : CopyFileUnique(rawPath, destDir);
            if (!dryRun)
                record.SetFieldValue(fieldName,
                    ProjectPathResolver.MakeRelative(destPath, projectFolder),
                    FieldSource.Legacy, userEdited: false);
            copied++;
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"{fieldName}: Kopierfehler: {ex.Message}");
        }
    }

    private static void CopyFieldFileList(
        HaltungRecord record, string fieldName, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages, bool dryRun = false)
    {
        var raw = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(raw))
            return;

        var paths = raw.Split(';', StringSplitOptions.RemoveEmptyEntries);
        var newPaths = new List<string>();
        var anyChanged = false;

        foreach (var p in paths)
        {
            var trimmed = p.Trim();

            // Relativer Pfad: pruefen ob Datei existiert, sonst reparieren
            if (ProjectPathResolver.IsRelative(trimmed))
            {
                if (!ProjectPathResolver.IsSafeRelativeProjectPath(trimmed))
                {
                    newPaths.Add(trimmed);
                    messages.Add($"{fieldName}: Unsicherer relativer Pfad: {trimmed}");
                    continue;
                }

                var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(trimmed, projectFolder);
                if (resolved is not null)
                {
                    newPaths.Add(trimmed);
                    continue;
                }

                // Datei nicht gefunden - suchen
                var fn = Path.GetFileName(trimmed);
                var found = SearchFileInHaltungen(projectFolder, holdingRoot, fn, messages, fieldName);
                if (found != null)
                {
                    var newRel = ProjectPathResolver.MakeRelative(found, projectFolder);
                    newPaths.Add(dryRun ? trimmed : newRel);
                    anyChanged = true;
                    copied++;
                    messages.Add($"{fieldName}: {(dryRun ? "Wuerde reparieren" : "Repariert")}: {trimmed} -> {newRel}");
                }
                else
                {
                    newPaths.Add(trimmed);
                    messages.Add($"{fieldName}: Relative Datei nicht gefunden: {trimmed}");
                }
                continue;
            }

            if (!File.Exists(trimmed))
            {
                newPaths.Add(trimmed);
                messages.Add($"{fieldName}: Datei nicht gefunden: {trimmed}");
                continue;
            }

            try
            {
                var subfolder = GetSubfolder(Path.GetExtension(trimmed));
                var destDir = Path.Combine(holdingRoot, subfolder);
                if (!dryRun) Directory.CreateDirectory(destDir);
                var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(trimmed)) : CopyFileUnique(trimmed, destDir);
                newPaths.Add(ProjectPathResolver.MakeRelative(destPath, projectFolder));
                anyChanged = true;
                copied++;
            }
            catch (Exception ex)
            {
                newPaths.Add(trimmed);
                errors++;
                messages.Add($"{fieldName}: Kopierfehler: {ex.Message}");
            }
        }

        if (anyChanged && !dryRun)
            record.SetFieldValue(fieldName, string.Join(";", newPaths), FieldSource.Legacy, userEdited: false);
    }

    private static void CopyProtocolFotos(
        ProtocolDocument protocol, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages, bool dryRun = false)
    {
        CopyRevisionFotos(protocol.Original, holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);
        CopyRevisionFotos(protocol.Current, holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);
        foreach (var rev in protocol.History)
            CopyRevisionFotos(rev, holdingRoot, projectFolder, ref copied, ref errors, messages, dryRun);
    }

    private static void CopyRevisionFotos(
        ProtocolRevision revision, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages, bool dryRun = false)
    {
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var rawPath = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                // Relativer Pfad: pruefen ob Datei existiert, sonst reparieren
                if (ProjectPathResolver.IsRelative(rawPath))
                {
                    if (!ProjectPathResolver.IsSafeRelativeProjectPath(rawPath))
                    {
                        messages.Add($"Foto unsicherer relativer Pfad: {rawPath}");
                        continue;
                    }

                    var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(rawPath, projectFolder);
                    if (resolved is not null)
                        continue; // OK

                    var fn = Path.GetFileName(rawPath);
                    var found = SearchFileInHaltungen(projectFolder, holdingRoot, fn, messages, "Foto");
                    if (found != null)
                    {
                        var newRel = ProjectPathResolver.MakeRelative(found, projectFolder);
                        if (!dryRun)
                            entry.FotoPaths[i] = newRel;
                        copied++;
                        messages.Add($"Foto {(dryRun ? "wuerde repariert" : "repariert")}: {rawPath} -> {newRel}");
                    }
                    else
                    {
                        messages.Add($"Foto nicht gefunden: {rawPath}");
                    }
                    continue;
                }

                if (!File.Exists(rawPath))
                {
                    messages.Add($"Foto nicht gefunden: {rawPath}");
                    continue;
                }

                try
                {
                    // Fotos liegen ZENTRAL im Projekt (<Projekt>\Fotos\), nicht je verteilter Haltung.
                    // Dateinamen tragen die Haltung (z.B. H_<Haltung>_NNN.jpg) -> kollisionssicher zentral.
                    var destDir = Path.Combine(projectFolder, "Fotos");
                    if (!dryRun) Directory.CreateDirectory(destDir);
                    var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(rawPath)) : CopyFileUnique(rawPath, destDir);
                    if (!dryRun)
                        entry.FotoPaths[i] = ProjectPathResolver.MakeRelative(destPath, projectFolder);
                    copied++;
                }
                catch (Exception ex)
                {
                    errors++;
                    messages.Add($"Foto Kopierfehler: {ex.Message}");
                }
            }
        }
    }

    private static void CopyVsaFindingFotos(
        List<VsaFinding> findings, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages, bool dryRun = false)
    {
        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.FotoPath))
                continue;

            // Relativer Pfad: pruefen ob Datei existiert, sonst reparieren
            if (ProjectPathResolver.IsRelative(finding.FotoPath))
            {
                if (!ProjectPathResolver.IsSafeRelativeProjectPath(finding.FotoPath))
                {
                    messages.Add($"VsaFinding Foto unsicherer relativer Pfad: {finding.FotoPath}");
                    continue;
                }

                var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(finding.FotoPath, projectFolder);
                if (resolved is not null)
                    continue; // OK

                var fn = Path.GetFileName(finding.FotoPath);
                var found = SearchFileInHaltungen(projectFolder, holdingRoot, fn, messages, "VsaFinding Foto");
                if (found != null)
                {
                    var newRel = ProjectPathResolver.MakeRelative(found, projectFolder);
                    if (!dryRun)
                        finding.FotoPath = newRel;
                    copied++;
                    messages.Add($"VsaFinding Foto {(dryRun ? "wuerde repariert" : "repariert")}: {fn} -> {newRel}");
                }
                else
                {
                    messages.Add($"VsaFinding Foto nicht gefunden: {finding.FotoPath}");
                }
                continue;
            }

            if (!File.Exists(finding.FotoPath))
            {
                messages.Add($"VsaFinding Foto nicht gefunden: {finding.FotoPath}");
                continue;
            }

            try
            {
                var destDir = Path.Combine(holdingRoot, "Fotos");
                if (!dryRun) Directory.CreateDirectory(destDir);
                var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(finding.FotoPath)) : CopyFileUnique(finding.FotoPath, destDir);
                if (!dryRun)
                    finding.FotoPath = ProjectPathResolver.MakeRelative(destPath, projectFolder);
                copied++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"VsaFinding Foto Kopierfehler: {ex.Message}");
            }
        }
    }

    private static IReadOnlyList<HaltungRecord> SnapshotRecords(Project project, object? collectionLock)
    {
        if (collectionLock is null)
            return project.Data.ToList();

        lock (collectionLock)
            return project.Data.ToList();
    }

    /// <summary>
    /// Repariert relative Medienpfade vorsichtig: zuerst in der eigenen Haltung suchen;
    /// global nur verwenden, wenn genau ein Treffer existiert.
    /// </summary>
    private static string? SearchFileInHaltungen(
        string projectFolder,
        string holdingRoot,
        string fileName,
        List<string> messages,
        string context)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return null;

        if (Directory.Exists(holdingRoot))
        {
            var ownMatches = FindMatchingFiles(holdingRoot, fileName, max: 2);
            if (ownMatches.Count == 1)
                return ownMatches[0];
            if (ownMatches.Count > 1)
            {
                messages.Add($"{context}: Mehrere Treffer in eigener Haltung fuer {fileName} - Pfad nicht automatisch repariert.");
                return null;
            }
        }

        var haltungenRoot = Path.Combine(projectFolder, "Haltungen");
        if (!Directory.Exists(haltungenRoot))
            return null;

        var globalMatches = FindMatchingFiles(haltungenRoot, fileName, max: 2);
        if (globalMatches.Count == 1)
            return globalMatches[0];
        if (globalMatches.Count > 1)
        {
            messages.Add($"{context}: Mehrere globale Treffer fuer {fileName} - Pfad nicht automatisch repariert.");
            return null;
        }

        return null;
    }

    private static List<string> FindMatchingFiles(string root, string fileName, int max)
    {
        try
        {
            return AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                .EnumerateFilesSafe(root, fileName, recursive: true)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .Take(max)
                .ToList();
        }
        catch
        {
            return new List<string>();
        }
    }

    /// <summary>
    /// Bestimmt den Unterordner anhand der Dateiendung.
    /// </summary>
    public static string GetSubfolder(string ext)
    {
        if (MediaFileTypes.HasVideoExtension(ext)) return "Video";
        if (MediaFileTypes.HasImageExtension(ext)) return "Fotos";
        return "PDF";
    }

    /// <summary>
    /// Kopiert eine Datei in den Zielordner. Bei Namenskollision mit unterschiedlicher
    /// Groesse wird ein Timestamp-Suffix angehaengt. Bei gleicher Groesse wird die
    /// bestehende Datei wiederverwendet.
    /// </summary>
    private static string CopyFileUnique(string source, string destDir)
    {
        var fileName = Path.GetFileName(source);
        var dest = Path.Combine(destDir, fileName);

        if (File.Exists(dest))
        {
            var srcInfo = new FileInfo(source);
            var destInfo = new FileInfo(dest);
            if (srcInfo.Length == destInfo.Length)
                return dest; // Gleiche Datei, wiederverwenden

            // Unterschiedlicher Inhalt: Timestamp-Suffix
            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            dest = Path.Combine(destDir, $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}{ext}");
        }

        File.Copy(source, dest, overwrite: false);
        return dest;
    }

    /// <summary>
    /// Entfernt ungueltige Dateinamen-Zeichen aus einem Pfadsegment (Haltungsname).
    /// Delegiert an die zentrale Implementierung in ProjectPathResolver.
    /// </summary>
    public static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);
}
