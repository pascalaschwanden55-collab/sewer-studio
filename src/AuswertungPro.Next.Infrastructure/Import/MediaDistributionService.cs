using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Media;
using AuswertungPro.Next.Domain.Protocol;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Kopiert beim Import aufgeloeste Mediendateien (Video, Fotos, PDFs) in die
/// Projektordner-Struktur und ersetzt absolute Pfade durch relative.
/// </summary>
public sealed class MediaDistributionService : IImportMediaDistributionService
{
    public sealed record CopyProgress(int Processed, int Total, string? CurrentFile);

    public sealed record CopyResult(
        int FilesCopied,
        int FilesSkipped,
        int Errors,
        IReadOnlyList<string> Messages);

    public ImportMediaDistributionResult Distribute(ImportMediaDistributionRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        var progress = request.Progress is null
            ? null
            : new CopyProgressAdapter(request.Progress);
        var result = DistributeImportedMediaCore(
            request.ProjectFolder,
            request.Project,
            progress,
            request.CancellationToken,
            request.DryRun,
            request.CollectionLock,
            request.IncludeVideos,
            request.IncludePdfs,
            request.IncludeSchacht,
            request.FileStaging);
        return new ImportMediaDistributionResult(
            result.FilesCopied,
            result.FilesSkipped,
            result.Errors,
            result.Messages);
    }

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
        object? collectionLock = null,
        bool includeVideos = true,
        bool includePdfs = true,
        bool includeSchacht = true)
        => DistributeImportedMediaCore(
            projectFolder,
            project,
            progress,
            ct,
            dryRun,
            collectionLock,
            includeVideos,
            includePdfs,
            includeSchacht,
            fileStaging: null);

    private CopyResult DistributeImportedMediaCore(
        string projectFolder,
        Project project,
        IProgress<CopyProgress>? progress,
        CancellationToken ct,
        bool dryRun,
        object? collectionLock,
        bool includeVideos,
        bool includePdfs,
        bool includeSchacht,
        IImportFileStagingSession? fileStaging)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectFolder);
        ArgumentNullException.ThrowIfNull(project);
        if (fileStaging is not null && !SamePath(fileStaging.ProjectRoot, projectFolder))
            throw new InvalidOperationException("Datei-Staging und Medienziel gehoeren nicht zum selben Projekt.");

        Func<string, string, string> copyFile = fileStaging is null
            ? CopyFileUnique
            : (source, targetDirectory) => fileStaging.StageCopy(
                source,
                targetDirectory,
                cancellationToken: ct);
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
            var haltungsname = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            if (string.IsNullOrWhiteSpace(haltungsname))
            {
                skipped++;
                processed++;
                progress?.Report(new CopyProgress(processed, total, null));
                continue;
            }

            var sanitized = SanitizePathSegment(haltungsname);
            var holdingRoot = ProjectStructure.HaltungVerteiltDir(projectFolder, sanitized);

            // 1) Video (Link-Feld). Der manuelle Import setzt includeVideos=false,
            // damit Rohvideos erst im expliziten Verteil-Schritt ins Projekt kopiert werden.
            if (includeVideos)
                CopyFieldFile(record, FieldKeys.Link, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);

            // 2+3) PDF_Path / PDF_All. Der Ein-Knopf-Import setzt includePdfs=false, weil er das
            // eigene Protokoll (_E.pdf) generiert statt die Original-PDFs in die Haltung zu kopieren.
            if (includePdfs)
            {
                CopyFieldFile(record, FieldKeys.PdfPath, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
                CopyFieldFileList(record, FieldKeys.PdfAll, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
            }

            // 4) Protokoll-FotoPaths (Original, Current, History)
            if (record.Protocol != null)
                CopyProtocolFotos(record.Protocol, sanitized, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);

            // 5) VsaFindings FotoPath
            if (record.VsaFindings != null)
                CopyVsaFindingFotos(record.VsaFindings, sanitized, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);

            processed++;
            progress?.Report(new CopyProgress(processed, total, haltungsname));
        }

        // Schächte verteilen: Schacht-Dokumente (PDF im Link-Feld) → Schächte_Verteilt\<Schacht>\.
        // Der Ein-Knopf-Import setzt includeSchacht:false und lässt Schächte bewusst UNANGETASTET —
        // die Schacht-Verteilung macht der Anwender manuell über „Schacht Verteilen" mit dem separaten
        // Schacht-Gesamtauszug-PDF (ein 1:1-Kopieren des Link-Feldes würde sonst ein ganzes/falsches
        // Gesamt-PDF als Klumpen an jeden Schacht hängen).
        if (includeSchacht)
        {
            foreach (var schacht in SnapshotSchaechte(project, collectionLock))
            {
                ct.ThrowIfCancellationRequested();
                var schachtNr = schacht.GetFieldValue("Schachtnummer")?.Trim();
                if (string.IsNullOrWhiteSpace(schachtNr))
                    continue;
                var sanS = SanitizePathSegment(schachtNr);
                var schachtRoot = ProjectStructure.SchachtVerteiltDir(projectFolder, sanS);
                CopySchachtFieldFile(schacht, FieldKeys.Link, schachtRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
            }
        }

        if (!dryRun)
            project.Dirty = true;
        return new CopyResult(copied, skipped, errors, messages);
    }

    private static void CopyFieldFile(
        HaltungRecord record, string fieldName, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
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

        // UNC vor File.Exists ablehnen: der Zugriff wuerde SMB-Authentifizierung ausloesen (S2-3).
        if (MediaFileAllowlist.IsUnc(rawPath))
        {
            messages.Add($"{fieldName}: UNC-Pfad wird nicht uebernommen: {rawPath}");
            return;
        }

        if (!File.Exists(rawPath))
        {
            messages.Add($"{fieldName}: Datei nicht gefunden: {rawPath}");
            return;
        }

        // Nur bekannte Medientypen/Protokoll-PDFs ins Projekt kopieren (S2-1: Exfiltration beliebiger Dateien).
        if (!MediaFileAllowlist.IsImportableMediaOrPdf(rawPath))
        {
            messages.Add($"{fieldName}: Dateityp nicht erlaubt, wird nicht kopiert: {rawPath}");
            return;
        }

        try
        {
            var subfolder = GetSubfolder(Path.GetExtension(rawPath));
            var destDir = Path.Combine(holdingRoot, subfolder);
            var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(rawPath)) : copyFile(rawPath, destDir);
            if (!dryRun)
                record.SetFieldValue(fieldName,
                    ProjectPathResolver.MakeRelative(destPath, projectFolder),
                    FieldSource.Legacy, userEdited: false);
            copied++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"{fieldName}: Kopierfehler: {ex.Message}");
        }
    }

    private static void CopyFieldFileList(
        HaltungRecord record, string fieldName, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
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

            // UNC vor File.Exists ablehnen (S2-3: SMB-Authentifizierung an fremde Hosts).
            if (MediaFileAllowlist.IsUnc(trimmed))
            {
                newPaths.Add(trimmed);
                messages.Add($"{fieldName}: UNC-Pfad wird nicht uebernommen: {trimmed}");
                continue;
            }

            if (!File.Exists(trimmed))
            {
                newPaths.Add(trimmed);
                messages.Add($"{fieldName}: Datei nicht gefunden: {trimmed}");
                continue;
            }

            // S2-1: Nur bekannte Medientypen/Protokoll-PDFs ins Projekt kopieren.
            if (!MediaFileAllowlist.IsImportableMediaOrPdf(trimmed))
            {
                newPaths.Add(trimmed);
                messages.Add($"{fieldName}: Dateityp nicht erlaubt, wird nicht kopiert: {trimmed}");
                continue;
            }

            try
            {
                var subfolder = GetSubfolder(Path.GetExtension(trimmed));
                var destDir = Path.Combine(holdingRoot, subfolder);
                var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(trimmed)) : copyFile(trimmed, destDir);
                newPaths.Add(ProjectPathResolver.MakeRelative(destPath, projectFolder));
                anyChanged = true;
                copied++;
            }
            catch (OperationCanceledException)
            {
                throw;
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
        ProtocolDocument protocol, string haltungSan, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
    {
        CopyRevisionFotos(protocol.Original, haltungSan, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
        CopyRevisionFotos(protocol.Current, haltungSan, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
        foreach (var rev in protocol.History)
            CopyRevisionFotos(rev, haltungSan, holdingRoot, projectFolder, ref copied, ref errors, messages, copyFile, dryRun);
    }

    private static void CopyRevisionFotos(
        ProtocolRevision revision, string haltungSan, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
    {
        foreach (var entry in revision.Entries)
        {
            for (var i = 0; i < entry.FotoPaths.Count; i++)
            {
                var rawPath = entry.FotoPaths[i];
                if (string.IsNullOrWhiteSpace(rawPath))
                    continue;

                if (TryUseCentralHoldingPhoto(rawPath, haltungSan, projectFolder, dryRun, ref copied, ref errors, messages, copyFile, out var centralRel))
                {
                    if (!dryRun)
                        entry.FotoPaths[i] = centralRel!;
                    continue;
                }

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

                // UNC vor File.Exists ablehnen (S2-3).
                if (MediaFileAllowlist.IsUnc(rawPath))
                {
                    messages.Add($"Foto UNC-Pfad wird nicht uebernommen: {rawPath}");
                    continue;
                }

                if (!File.Exists(rawPath))
                {
                    messages.Add($"Foto nicht gefunden: {rawPath}");
                    continue;
                }

                // S2-1: Nur bekannte Medientypen ins Projekt kopieren.
                if (!MediaFileAllowlist.IsMediaFile(rawPath))
                {
                    messages.Add($"Foto Dateityp nicht erlaubt, wird nicht kopiert: {rawPath}");
                    continue;
                }

                try
                {
                    // Fotos liegen GRUPPIERT je Haltung: <Projekt>\Fotos\Haltungen\<Haltung>\
                    var destDir = ProjectStructure.FotosHaltungDir(projectFolder, haltungSan);
                    var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(rawPath)) : copyFile(rawPath, destDir);
                    if (!dryRun)
                        entry.FotoPaths[i] = ProjectPathResolver.MakeRelative(destPath, projectFolder);
                    copied++;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors++;
                    messages.Add($"Foto Kopierfehler: {ex.Message}");
                }
            }

            if (!dryRun)
                DeduplicatePhotoPaths(entry.FotoPaths);
        }
    }

    private static void CopyVsaFindingFotos(
        List<VsaFinding> findings, string haltungSan, string holdingRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
    {
        foreach (var finding in findings)
        {
            if (string.IsNullOrWhiteSpace(finding.FotoPath))
                continue;

            if (TryUseCentralHoldingPhoto(finding.FotoPath, haltungSan, projectFolder, dryRun, ref copied, ref errors, messages, copyFile, out var centralRel))
            {
                if (!dryRun)
                    finding.FotoPath = centralRel;
                continue;
            }

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

            // UNC vor File.Exists ablehnen (S2-3).
            if (MediaFileAllowlist.IsUnc(finding.FotoPath))
            {
                messages.Add($"VsaFinding Foto UNC-Pfad wird nicht uebernommen: {finding.FotoPath}");
                continue;
            }

            if (!File.Exists(finding.FotoPath))
            {
                messages.Add($"VsaFinding Foto nicht gefunden: {finding.FotoPath}");
                continue;
            }

            // S2-1: Nur bekannte Medientypen ins Projekt kopieren.
            if (!MediaFileAllowlist.IsMediaFile(finding.FotoPath))
            {
                messages.Add($"VsaFinding Foto Dateityp nicht erlaubt, wird nicht kopiert: {finding.FotoPath}");
                continue;
            }

            try
            {
                // Fotos liegen GRUPPIERT je Haltung: <Projekt>\Fotos\Haltungen\<Haltung>\
                var destDir = ProjectStructure.FotosHaltungDir(projectFolder, haltungSan);
                var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(finding.FotoPath)) : copyFile(finding.FotoPath, destDir);
                if (!dryRun)
                    finding.FotoPath = ProjectPathResolver.MakeRelative(destPath, projectFolder);
                copied++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"VsaFinding Foto Kopierfehler: {ex.Message}");
            }
        }
    }

    private static bool TryUseCentralHoldingPhoto(
        string rawPath,
        string haltungSan,
        string projectFolder,
        bool dryRun,
        ref int copied,
        ref int errors,
        List<string> messages,
        Func<string, string, string> copyFile,
        out string? relativePath)
    {
        relativePath = null;
        if (string.IsNullOrWhiteSpace(rawPath) || string.IsNullOrWhiteSpace(haltungSan))
            return false;

        // UNC-Quellen niemals anfassen (S2-3: SMB-Authentifizierung an fremde Hosts).
        if (MediaFileAllowlist.IsUnc(rawPath))
            return false;

        var normalized = rawPath.Replace('/', Path.DirectorySeparatorChar);
        var fileName = Path.GetFileName(normalized);
        if (string.IsNullOrWhiteSpace(fileName) || !MediaFileTypes.HasImageExtension(fileName))
            return false;

        if (ProjectPathResolver.IsRelative(rawPath) && !ProjectPathResolver.IsSafeRelativeProjectPath(rawPath))
            return false;

        var destDir = ProjectStructure.FotosHaltungDir(projectFolder, haltungSan);
        var preferred = Path.Combine(destDir, fileName);
        var source = ResolveExistingPhotoSource(rawPath, projectFolder);

        if (File.Exists(preferred))
        {
            if (source is not null
                && !SamePath(source, preferred)
                && !FileContentComparer.FilesEqual(source, preferred))
            {
                try
                {
                    var copiedPath = dryRun ? Path.Combine(destDir, fileName) : copyFile(source, destDir);
                    relativePath = ProjectPathResolver.MakeRelative(copiedPath, projectFolder);
                    copied++;
                    return true;
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    errors++;
                    messages.Add($"Foto Kopierfehler: {ex.Message}");
                    return false;
                }
            }

            relativePath = ProjectPathResolver.MakeRelative(preferred, projectFolder);
            return true;
        }

        if (source is null)
            return false;

        try
        {
            var destPath = dryRun ? preferred : copyFile(source, destDir);
            relativePath = ProjectPathResolver.MakeRelative(destPath, projectFolder);
            copied++;
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Foto Kopierfehler: {ex.Message}");
            return false;
        }
    }

    private static string? ResolveExistingPhotoSource(string rawPath, string projectFolder)
    {
        var normalized = rawPath.Replace('/', Path.DirectorySeparatorChar);
        if (Path.IsPathRooted(normalized))
            return File.Exists(normalized) ? normalized : null;

        if (!ProjectPathResolver.IsSafeRelativeProjectPath(rawPath))
            return null;

        var resolved = ProjectPathResolver.ResolveFilePathFromProjectFolder(rawPath, projectFolder);
        return resolved is not null && File.Exists(resolved) ? resolved : null;
    }

    private static void DeduplicatePhotoPaths(IList<string> paths)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        for (var i = paths.Count - 1; i >= 0; i--)
        {
            var key = NormalizePhotoPathKey(paths[i]);
            if (string.IsNullOrWhiteSpace(key))
                continue;
            if (!seen.Add(key))
                paths.RemoveAt(i);
        }
    }

    private static string NormalizePhotoPathKey(string path)
        => (path ?? string.Empty).Replace('\\', '/').Trim();

    private static bool SamePath(string left, string right)
    {
        try
        {
            return string.Equals(Path.GetFullPath(left), Path.GetFullPath(right), StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static IReadOnlyList<HaltungRecord> SnapshotRecords(Project project, object? collectionLock)
    {
        if (collectionLock is null)
            return project.Data.ToList();

        lock (collectionLock)
            return project.Data.ToList();
    }

    private static IReadOnlyList<SchachtRecord> SnapshotSchaechte(Project project, object? collectionLock)
    {
        if (collectionLock is null)
            return project.SchaechteData.ToList();

        lock (collectionLock)
            return project.SchaechteData.ToList();
    }

    // Wie CopyFieldFile, aber fuer SchachtRecord (eigene SetFieldValue-Signatur ohne FieldSource).
    private static void CopySchachtFieldFile(
        SchachtRecord record, string fieldName, string schachtRoot, string projectFolder,
        ref int copied, ref int errors, List<string> messages,
        Func<string, string, string> copyFile, bool dryRun = false)
    {
        var rawPath = record.GetFieldValue(fieldName)?.Trim();
        if (string.IsNullOrWhiteSpace(rawPath))
            return;

        if (ProjectPathResolver.IsRelative(rawPath))
        {
            // Bereits relativ: ok, wenn aufloesbar; sonst nur protokollieren (keine globale Schacht-Reparatur).
            if (ProjectPathResolver.IsSafeRelativeProjectPath(rawPath)
                && ProjectPathResolver.ResolveFilePathFromProjectFolder(rawPath, projectFolder) is not null)
                return;
            messages.Add($"Schacht {fieldName}: relative Datei nicht gefunden: {rawPath}");
            return;
        }

        // UNC vor File.Exists ablehnen (S2-3: SMB-Authentifizierung an fremde Hosts).
        if (MediaFileAllowlist.IsUnc(rawPath))
        {
            messages.Add($"Schacht {fieldName}: UNC-Pfad wird nicht uebernommen: {rawPath}");
            return;
        }

        if (!File.Exists(rawPath))
        {
            messages.Add($"Schacht {fieldName}: Datei nicht gefunden: {rawPath}");
            return;
        }

        // S2-1: Nur bekannte Medientypen/Protokoll-PDFs ins Projekt kopieren.
        if (!MediaFileAllowlist.IsImportableMediaOrPdf(rawPath))
        {
            messages.Add($"Schacht {fieldName}: Dateityp nicht erlaubt, wird nicht kopiert: {rawPath}");
            return;
        }

        try
        {
            var subfolder = GetSubfolder(Path.GetExtension(rawPath));
            var destDir = Path.Combine(schachtRoot, subfolder);
            var destPath = dryRun ? Path.Combine(destDir, Path.GetFileName(rawPath)) : copyFile(rawPath, destDir);
            if (!dryRun)
                record.SetFieldValue(fieldName, ProjectPathResolver.MakeRelative(destPath, projectFolder));
            copied++;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Schacht {fieldName}: Kopierfehler: {ex.Message}");
        }
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

        var haltungenRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
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
    /// Kopiert eine Datei in den Zielordner. Nur wirklich gleicher Inhalt wird
    /// wiederverwendet; eine Namenskollision erhaelt einen eindeutigen Suffix.
    /// </summary>
    private static string CopyFileUnique(string source, string destDir)
    {
        Directory.CreateDirectory(destDir);
        var fileName = Path.GetFileName(source);
        var dest = Path.Combine(destDir, fileName);

        if (File.Exists(dest))
        {
            if (FileContentComparer.FilesEqual(source, dest))
                return dest;

            var name = Path.GetFileNameWithoutExtension(fileName);
            var ext = Path.GetExtension(fileName);
            var stem = $"{name}_{DateTime.Now:yyyyMMdd_HHmmss}";
            dest = Path.Combine(destDir, stem + ext);
            var suffix = 2;
            while (File.Exists(dest))
            {
                if (FileContentComparer.FilesEqual(source, dest))
                    return dest;
                dest = Path.Combine(destDir, $"{stem}_{suffix}{ext}");
                suffix++;
            }
        }

        File.Copy(source, dest, overwrite: false);
        return dest;
    }

    private sealed class CopyProgressAdapter(
        IProgress<ImportMediaDistributionProgress> target) : IProgress<CopyProgress>
    {
        public void Report(CopyProgress value)
            => target.Report(new ImportMediaDistributionProgress(
                value.Processed,
                value.Total,
                value.CurrentFile));
    }

    /// <summary>
    /// Entfernt ungueltige Dateinamen-Zeichen aus einem Pfadsegment (Haltungsname).
    /// Delegiert an die zentrale Implementierung in ProjectPathResolver.
    /// </summary>
    public static string SanitizePathSegment(string value)
        => ProjectPathResolver.SanitizePathSegment(value);
}
