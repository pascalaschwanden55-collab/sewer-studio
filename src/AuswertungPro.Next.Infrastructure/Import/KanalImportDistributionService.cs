using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import.Common;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Verteilt beim Ein-Knopf-Import je Haltung Video + ORIGINAL-Protokoll — mit der gleichen Logik wie
/// „Haltung Verteilen" (<see cref="HoldingFolderDistributor"/>):
///
///  1. Das Original-Protokoll-PDF (aus <c>Importdateien\PDF</c>) wird pro Haltung gesplittet und flach +
///     datumsbenannt nach <c>Haltungen_Verteilt\&lt;H&gt;\JJJJMMTT_&lt;H&gt;.pdf</c> gelegt; dabei wird auch das
///     Video gematcht (über <c>record.Link</c> aus dem XTF bzw. OSD). Das Original-Protokoll wird als
///     <c>PDF_Path</c> (RELATIV) verlinkt — das ist das Menü „Haltungsprotokoll Original (PDF) öffnen".
///  2. Alle projekt-internen absoluten Pfade (Link/PDF_Path) werden relativiert, damit „Video Play" und
///     „Protokoll öffnen" die verteilte Kopie nutzen.
///  3. Fallback: für Haltungen, deren Video der PDF-getriebene Lauf NICHT verteilt hat (z. B. wenn kein
///     Original-PDF existiert oder die Protokollseite nicht erkannt wurde), wird das Video eigenständig
///     flach + datumsbenannt kopiert. So bleibt die Video-Verteilung zuverlässig, auch wenn der PDF-Split
///     einzelne Haltungen verpasst.
///
/// Das programm-EIGENE Protokoll (Suffix <c>_E</c>) wird hier bewusst NICHT erzeugt — das übernimmt der
/// <see cref="ProtocolRegenerationService"/> am Ende der Bearbeitung („Protokoll neu generieren").
/// </summary>
public sealed class KanalImportDistributionService : IKanalImportDistributor
{
    /// <summary>
    /// Verteilt Video + Original-Protokoll je Haltung (siehe Klassen-Doku).
    /// </summary>
    /// <param name="project">Offenes Projekt (Records werden verlinkt).</param>
    /// <param name="projectFolder">Absoluter Projektstammordner.</param>
    /// <param name="archivedPdfDir">Ordner mit dem/den archivierten Original-Protokoll-PDF(s) (i. d. R. Importdateien\PDF).</param>
    /// <param name="sourceVideoDir">Quellordner, in dem die Videos liegen (Export-Root, rekursiv).</param>
    /// <param name="splitPdf">false = Gesamt-PDF-Split ueberspringen; Relativierung + Video-Fallback laufen trotzdem.</param>
    /// <param name="primaryProtocolPdf">Explizites Gesamtprotokoll fuer den Split (KINS: *_Protokoll.pdf aus der
    /// Quelle). Null = automatische Wahl aus dem Archivordner (SelectPrimaryProtocolPdf).</param>
    public KanalImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        bool splitPdf = true,
        string? primaryProtocolPdf = null)
        => Distribute(
            project,
            projectFolder,
            archivedPdfDir,
            sourceVideoDir,
            splitPdf,
            primaryProtocolPdf,
            fileStaging: null);

    public KanalImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        bool splitPdf,
        string? primaryProtocolPdf,
        IImportFileStagingSession? fileStaging)
    {
        if (fileStaging is not null
            && !Path.GetFullPath(fileStaging.ProjectRoot)
                .Equals(Path.GetFullPath(projectFolder), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Datei-Staging und Kanal-Verteilung gehoeren nicht zum selben Projekt.");
        }

        var messages = new List<string>();
        int videos = 0, origs = 0, errors = 0;
        var destRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);

        // 1) Original-Protokoll pro Haltung splitten + Video matchen (die echte „Haltung Verteilen"-Logik).
        //    HoldingFolderDistributor liefert bei leerem PDF-Ordner ein Fehlerergebnis (Success=false) und
        //    fasst dann kein Video an — die Videos übernimmt in diesem Fall der Fallback in Schritt 3.
        if (splitPdf)
        {
            try
            {
                // NUR das maßgebliche Inspektionsprotokoll splitten (nicht alle PDFs im Ordner) —
                // sonst entstehen aus mehreren Gesamt-PDFs Duplikate (<H>.pdf + <H>_01.pdf).
                var results = DistributeOriginalProtocol(
                    project,
                    projectFolder,
                    archivedPdfDir,
                    sourceVideoDir,
                    destRoot,
                    primaryProtocolPdf,
                    fileStaging);

                foreach (var r in results)
                {
                    if (!r.Success)
                        continue;
                    if (!string.IsNullOrWhiteSpace(r.DestVideoPath))
                        videos++;
                    if (string.IsNullOrWhiteSpace(r.DestPdfPath) || string.IsNullOrWhiteSpace(r.HoldingFolder))
                        continue;

                    // Zuordnung Ergebnis -> Record über den sanitisierten Ordnernamen (wie in Export/Schacht).
                    var folderName = Path.GetFileName(
                        r.HoldingFolder!.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));
                    var record = FindRecordBySanitizedHaltung(project, folderName);
                    if (record is null)
                    {
                        record = CreateRecordFromDistributedFolder(project, folderName);
                        messages.Add($"Haltung aus Original-Protokoll angelegt: {folderName}");
                    }

                    // PDF_Path = verteiltes ORIGINAL-Protokoll (Menü „Haltungsprotokoll Original öffnen").
                    record.SetFieldValue(FieldKeys.PdfPath, r.DestPdfPath!, FieldSource.Legacy, userEdited: false);
                    origs++;
                }
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Original-Protokoll-Verteilung: {ex.Message}");
            }
        }

        // 2) Projekt-interne absolute Pfade relativieren (HoldingFolderDistributor setzt Link absolut).
        foreach (var record in project.Data)
        {
            RelativizeIfInProject(record, FieldKeys.Link, projectFolder);
            RelativizeIfInProject(record, "Link_G", projectFolder);   // Gegeninspektions-Video
            RelativizeIfInProject(record, FieldKeys.PdfPath, projectFolder);
        }

        // 3) Fallback-Video: Haltungen, deren Link noch auf die absolute QUELLE zeigt (nicht verteilt),
        //    bekommen ihr Video flach + datumsbenannt in den Haltungsordner + relativen Link.
        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var link = record.GetFieldValue(FieldKeys.Link)?.Trim();
            if (string.IsNullOrWhiteSpace(link) || ProjectPathResolver.IsRelative(link))
                continue;

            if (!TryInspectExistingSourceFile(
                    link,
                    out var safeLink,
                    out var sourceError))
            {
                if (!string.IsNullOrWhiteSpace(sourceError))
                    messages.Add($"Video {haltung}: {sourceError}");
                continue;
            }

            // S2-1: Nur bekannte Medientypen/Protokoll-PDFs ins Projekt kopieren.
            if (!MediaFileAllowlist.IsImportableMediaOrPdf(safeLink))
            {
                messages.Add($"Video {haltung}: Dateityp nicht erlaubt, wird nicht kopiert: {link}");
                continue;
            }

            try
            {
                if (!TryInspectExistingSourceFile(
                        safeLink,
                        out safeLink,
                        out sourceError))
                {
                    throw new IOException(sourceError ?? "Quelldatei fehlt.");
                }

                var san = ProjectPathResolver.SanitizePathSegment(haltung);
                var dir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
                var stamp = ResolveDateStamp(record);
                var ext = Path.GetExtension(safeLink);
                string dest;
                if (fileStaging is null)
                {
                    var writePathGuard = new ProjectWritePathGuard(projectFolder);
                    dir = writePathGuard.EnsureSafeDirectoryTarget(dir);
                    Directory.CreateDirectory(dir);
                    dest = writePathGuard.EnsureSafeFileTarget(
                        UniquePath(Path.Combine(dir, $"{stamp}_{san}{ext}")));
                    writePathGuard.EnsureSafeFileTarget(dest);
                    File.Copy(safeLink, dest, overwrite: false);
                }
                else
                {
                    dest = fileStaging.StageCopyAs(
                        safeLink,
                        dir,
                        $"{stamp}_{san}{ext}");
                }
                record.SetFieldValue(FieldKeys.Link, ProjectPathResolver.MakeRelative(dest, projectFolder), FieldSource.Legacy, userEdited: false);
                videos++;
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"Video {haltung}: {ex.Message}");
            }
        }

        return new KanalImportDistributor.Result(videos, origs, errors, messages);
    }

    private IReadOnlyList<HoldingFolderDistributor.DistributionResult> DistributeOriginalProtocol(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        string sourceVideoDir,
        string logicalDestinationRoot,
        string? primaryProtocolPdf,
        IImportFileStagingSession? fileStaging)
    {
        var primary = ResolvePrimaryProtocol(
            primaryProtocolPdf,
            archivedPdfDir,
            fileStaging);
        if (primary is null || !TryInspectReadableFile(primary, out primary))
            return Array.Empty<HoldingFolderDistributor.DistributionResult>();

        if (fileStaging is null)
        {
            new ProjectWritePathGuard(projectFolder)
                .EnsureSafeDirectoryTarget(logicalDestinationRoot);
            return HoldingFolderDistributor.DistributeFiles(
                pdfFiles: [primary.ReadPath],
                videoSourceFolder: sourceVideoDir,
                destGemeindeFolder: logicalDestinationRoot,
                project: project);
        }

        using var output = new StagedDistributionOutput();
        var readablePdf = output.CreateReadableCopy(
            primary.ReadPath,
            Path.GetFileName(primary.TargetPath));
        var temporaryResults = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: [readablePdf],
            videoSourceFolder: sourceVideoDir,
            destGemeindeFolder: output.OutputRoot,
            project: project);
        output.StageAll(fileStaging, logicalDestinationRoot);
        RemapTemporaryProjectPaths(project, projectFolder, output, logicalDestinationRoot);

        return temporaryResults
            .Select(result => result with
            {
                DestPdfPath = output.MapPath(result.DestPdfPath, logicalDestinationRoot),
                DestVideoPath = output.MapPath(result.DestVideoPath, logicalDestinationRoot),
                InfoPath = output.MapPath(result.InfoPath, logicalDestinationRoot),
                HoldingFolder = output.MapPath(result.HoldingFolder, logicalDestinationRoot)
            })
            .ToList();
    }

    private ImportReadableFile? ResolvePrimaryProtocol(
        string? primaryProtocolPdf,
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging)
    {
        if (!string.IsNullOrWhiteSpace(primaryProtocolPdf))
        {
            var direct = new ImportReadableFile(primaryProtocolPdf, primaryProtocolPdf);
            if (TryInspectReadableFile(direct, out direct))
                return direct;

            if (fileStaging is not null)
            {
                try
                {
                    var readable = fileStaging.ResolveReadPath(primaryProtocolPdf);
                    var staged = new ImportReadableFile(primaryProtocolPdf, readable);
                    if (TryInspectReadableFile(staged, out staged))
                        return staged;
                }
                catch (ArgumentException)
                {
                    // Explizite KINS-Quellen duerfen ausserhalb des Projekts liegen.
                }
            }
        }

        return SelectPrimaryProtocolPdfReadable(archivedPdfDir, fileStaging);
    }

    private static void RemapTemporaryProjectPaths(
        Project project,
        string projectFolder,
        StagedDistributionOutput output,
        string logicalDestinationRoot)
    {
        foreach (var record in project.Data)
        {
            foreach (var field in new[] { FieldKeys.Link, "Link_G", FieldKeys.PdfPath })
            {
                var current = record.GetFieldValue(field)?.Trim();
                if (string.IsNullOrWhiteSpace(current) || ProjectPathResolver.IsRelative(current))
                    continue;

                var mapped = output.MapPath(current, logicalDestinationRoot);
                if (string.IsNullOrWhiteSpace(mapped)
                    || string.Equals(mapped, current, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                record.SetFieldValue(
                    field,
                    ProjectPathResolver.MakeRelative(mapped, projectFolder),
                    FieldSource.Legacy,
                    userEdited: false);
            }
        }
    }

    // Wählt aus dem Archiv-PDF-Ordner das MASSGEBLICHE Inspektionsprotokoll: das größte PDF, das kein
    // Duplikat-Suffix (<basis>_&lt;n&gt;.pdf, wenn <basis>.pdf existiert) ist. So gewinnt das Basis-Protokoll
    // gegen Zweit-Export (_1) und den kleineren Plan. Liefert null, wenn keine PDFs vorhanden.
    internal string? SelectPrimaryProtocolPdf(string archivedPdfDir)
        => SelectPrimaryProtocolPdfReadable(archivedPdfDir, fileStaging: null)?.ReadPath;

    private ImportReadableFile? SelectPrimaryProtocolPdfReadable(
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging)
    {
        if (!ImportSourcePathGuard.TryInspectDirectory(
                archivedPdfDir,
                out var safeArchivedPdfDir,
                out var archiveExists,
                out _))
        {
            return null;
        }

        IReadOnlyList<ImportReadableFile> candidates;
        if (fileStaging is null)
        {
            candidates = archiveExists
                ? Directory.EnumerateFiles(
                        safeArchivedPdfDir,
                        "*.pdf",
                        SearchOption.TopDirectoryOnly)
                    .Select(path => new ImportReadableFile(path, path))
                    .ToList()
                : [];
        }
        else
        {
            candidates = fileStaging.EnumerateReadableFiles(
                safeArchivedPdfDir,
                "*.pdf",
                SearchOption.TopDirectoryOnly);
        }

        var pdfs = candidates
            .Select(file => TryInspectReadableFile(file, out var safeFile) ? safeFile : null)
            .Where(file => file is not null)
            .Select(file => file!)
            .Where(p => !Path.GetFileName(p.TargetPath).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();
        if (pdfs.Count == 0)
            return null;

        var stems = new HashSet<string>(
            pdfs.Select(p => Path.GetFileNameWithoutExtension(p.TargetPath)), StringComparer.OrdinalIgnoreCase);

        // Ein PDF ist ein Duplikat-Variant, wenn sein Stamm = <basis>_<ziffern> und <basis>.pdf existiert.
        bool IsDuplicateVariant(ImportReadableFile file)
        {
            var stem = Path.GetFileNameWithoutExtension(file.TargetPath);
            var us = stem.LastIndexOf('_');
            if (us <= 0 || us == stem.Length - 1)
                return false;
            var suffix = stem[(us + 1)..];
            if (!suffix.All(char.IsDigit))
                return false;
            var basis = stem[..us];
            return stems.Contains(basis);
        }

        return pdfs
            .Select(p =>
            {
                var text = PdfDokumentTypErkennung.ReadPdfTextPrefix(p.ReadPath, maxPages: 6);
                var typ = PdfDokumentTypErkennung.ErkenneText(text, Path.GetFileName(p.TargetPath));
                return new
                {
                    File = p,
                    Score = ScoreProtocolCandidate(typ, IsDuplicateVariant(p)),
                    Length = SafeLength(p.ReadPath)
                };
            })
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.Length)
            .ThenBy(p => Path.GetFileName(p.File.TargetPath), StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault()
            ?.File;
    }

    private static int ScoreProtocolCandidate(PdfDokumentTyp typ, bool isDuplicateVariant)
    {
        var score = typ switch
        {
            PdfDokumentTyp.TvProtokoll => 100,
            PdfDokumentTyp.PlanSituation => -1000,
            PdfDokumentTyp.Dichtheitspruefung => -900,
            PdfDokumentTyp.Deckblatt => -800,
            _ => 0
        };
        if (isDuplicateVariant)
            score -= 10;
        return score;
    }

    private static long SafeLength(string path)
    {
        try { return new FileInfo(path).Length; }
        catch { return 0L; }
    }

    private static bool TryInspectReadableFile(
        ImportReadableFile file,
        out ImportReadableFile safeFile)
    {
        safeFile = file;
        if (!string.Equals(
                Path.GetExtension(file.TargetPath),
                ".pdf",
                StringComparison.OrdinalIgnoreCase)
            || !TryInspectExistingSourceFile(
                file.ReadPath,
                out var safeReadPath,
                out _))
        {
            return false;
        }

        safeFile = file with { ReadPath = safeReadPath };
        return true;
    }

    private static bool TryInspectExistingSourceFile(
        string path,
        out string safePath,
        out string? error)
    {
        if (!ImportSourcePathGuard.TryInspectFile(
                path,
                out safePath,
                out var exists,
                out error))
        {
            return false;
        }

        if (exists)
            return true;

        safePath = string.Empty;
        error = null;
        return false;
    }

    // Record über den sanitisierten Haltungsnamen (== Ordnername) finden.
    private static HaltungRecord? FindRecordBySanitizedHaltung(Project project, string sanitizedFolderName)
    {
        if (string.IsNullOrWhiteSpace(sanitizedFolderName))
            return null;

        var wanted = HoldingKeyNormalizer.NormalizeIbak(sanitizedFolderName);
        return project.Data.FirstOrDefault(x =>
        {
            var raw = (x.GetFieldValue(FieldKeys.HoldingName) ?? "").Trim();
            var sanitized = ProjectPathResolver.SanitizePathSegment(raw);
            return string.Equals(HoldingKeyNormalizer.NormalizeIbak(sanitized), wanted, StringComparison.OrdinalIgnoreCase);
        });
    }

    private static HaltungRecord CreateRecordFromDistributedFolder(Project project, string sanitizedFolderName)
    {
        // CreateNewRecord vergibt die fortlaufende NR (einheitlich zu WinCan/KINS/IBAK; frueher
        // legte dieser Weg per new HaltungRecord ohne NR an). Der Namens-Duplikatschutz liegt hier
        // beim Aufrufer (FindRecordBySanitizedHaltung), daher bewusst project.Data.Add statt AddRecord.
        var record = project.CreateNewRecord();
        record.SetFieldValue(FieldKeys.HoldingName, sanitizedFolderName, FieldSource.Legacy, userEdited: false);
        project.Data.Add(record);
        project.ModifiedAtUtc = DateTime.UtcNow;
        project.Dirty = true;
        return record;
    }

    // Relativiert ein Pfadfeld NUR, wenn es absolut ist UND unter dem Projektordner liegt (verteilte
    // Kopie). Absolute Quellpfade (außerhalb des Projekts) bleiben unverändert, damit der Fallback greift.
    private static void RelativizeIfInProject(HaltungRecord record, string field, string projectFolder)
    {
        var val = record.GetFieldValue(field)?.Trim();
        if (string.IsNullOrWhiteSpace(val) || ProjectPathResolver.IsRelative(val))
            return;

        string full, root;
        try
        {
            full = Path.GetFullPath(val);
            root = Path.GetFullPath(projectFolder);
        }
        catch
        {
            return;
        }

        if (!IsUnderDirectory(full, root))
            return;

        record.SetFieldValue(field, ProjectPathResolver.MakeRelative(full, projectFolder), FieldSource.Legacy, userEdited: false);
    }

    private static bool IsUnderDirectory(string path, string directory)
    {
        var fullPath = Path.GetFullPath(path);
        var fullDirectory = Path.GetFullPath(directory)
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(fullPath, fullDirectory, StringComparison.OrdinalIgnoreCase))
            return true;

        return fullPath.StartsWith(
            fullDirectory + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase);
    }

    // JJJJMMTT aus Datum_Jahr (verschiedene Formate), sonst aus verteilten Medienpfaden.
    // Die Regel selbst liegt in ImportDateStampResolver, damit der Protokoll-Verteiler
    // denselben Stempel bildet wie der Videoweg.
    internal string ResolveDateStamp(HaltungRecord record)
        => Common.ImportDateStampResolver.Resolve(
            record.GetFieldValue("Datum_Jahr"),
            record.GetFieldValue(FieldKeys.PdfPath),
            record.GetFieldValue(FieldKeys.PdfEigen),
            record.GetFieldValue(FieldKeys.Link));

    internal string UniquePath(string path)
    {
        if (!File.Exists(path))
            return path;

        var dir = Path.GetDirectoryName(path) ?? "";
        var stem = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        for (var i = 1; i < 1000; i++)
        {
            var cand = Path.Combine(dir, $"{stem}_{i}{ext}");
            if (!File.Exists(cand))
                return cand;
        }
        return path;
    }
}
