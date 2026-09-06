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

                // Ein Protokoll, das nicht in seinen Haltungsordner kam, MUSS im Bericht stehen.
                // Bis 2026-09-04 wurde jedes Fehlerergebnis hier still uebersprungen: In Goeschenen
                // meldete der Lauf "0 Original-Protokolle, 0 Fehler", obwohl alle 239 fehlten.
                var nichtVerteilt = new List<string>();

                foreach (var r in results)
                {
                    if (!r.Success)
                    {
                        nichtVerteilt.Add(r.Message);
                        continue;
                    }
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
                    //
                    // Ein bereits gesetzter Verweis wird NICHT ersetzt: Er stammt aus der
                    // name-basierten Verteilung, also aus einem eindeutig benannten
                    // Einzelprotokoll. Eine Seite aus einem Sammelprotokoll darf ihn nicht
                    // ueberholen — sonst entschiede die Reihenfolge, welche Datei gilt.
                    var vorhanden = record.GetFieldValue(FieldKeys.PdfPath)?.Trim();
                    if (!string.IsNullOrWhiteSpace(vorhanden)
                        && !string.Equals(vorhanden, r.DestPdfPath, StringComparison.OrdinalIgnoreCase))
                    {
                        messages.Add(
                            $"Protokoll {folderName}: bereits aus einem Einzelprotokoll versorgt; "
                            + $"die Seite aus dem Sammelprotokoll liegt zusaetzlich im Ordner "
                            + $"({Path.GetFileName(r.DestPdfPath)}).");
                        continue;
                    }

                    record.SetFieldValue(FieldKeys.PdfPath, r.DestPdfPath!, FieldSource.Legacy, userEdited: false);
                    origs++;
                }

                if (nichtVerteilt.Count > 0)
                {
                    // Die ersten Gruende ausschreiben, den Rest zaehlen — ein Sammelprotokoll kann
                    // hunderte Haltungen enthalten, und der Bericht soll lesbar bleiben.
                    const int SichtbareGruende = 10;
                    errors += nichtVerteilt.Count;
                    messages.Add($"Haltungsprotokolle nicht verteilt: {nichtVerteilt.Count}");
                    foreach (var grund in nichtVerteilt.Take(SichtbareGruende))
                        messages.Add($"  nicht verteilt: {grund}");
                    if (nichtVerteilt.Count > SichtbareGruende)
                        messages.Add($"  ... und {nichtVerteilt.Count - SichtbareGruende} weitere");
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

        // 3) Fallback-Video: Haltungen, deren Videoverweis noch auf die absolute QUELLE
        //    zeigt (nicht verteilt), bekommen ihr Video flach + datumsbenannt in den
        //    Haltungsordner und einen relativen Link.
        //
        //    Bis 2026-09-05 lief nur das Feld "Link" durch diesen Weg. Das
        //    Gegeninspektionsvideo blieb als absoluter Pfad auf die Kundenquelle stehen —
        //    der Bericht meldete trotzdem "1 Video, 0 Fehler". In einer Kopie des fertigen
        //    Projekts war es damit nicht mehr abspielbar.
        foreach (var record in project.Data.ToList())
        {
            var haltung = record.GetFieldValue(FieldKeys.HoldingName)?.Trim();
            if (string.IsNullOrWhiteSpace(haltung))
                continue;

            var verteiltePfade = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (feld, namenszusatz) in VideoFelder)
            {
                var quelle = record.GetFieldValue(feld);
                switch (VerteileQuellvideo(
                    record, haltung!, feld, namenszusatz, projectFolder, fileStaging, messages))
                {
                    case VideoVerteilung.Verteilt: videos++; break;
                    case VideoVerteilung.Fehler: errors++; break;
                }
                if (!string.IsNullOrWhiteSpace(quelle))
                    verteiltePfade[quelle] = record.GetFieldValue(feld) ?? quelle;
            }

            // Weitere Untersuchungen dürfen nicht verschwinden, nur weil ihre
            // Gegenrolle offen ist. Die Verweise bleiben an ihrer Protokollrevision.
            if (record.Protocol is { } protokoll)
            {
                var revisionen = new[] { protokoll.Original, protokoll.Current }.Concat(protokoll.History);
                foreach (var revision in revisionen)
                {
                    if (revision.ImportVideoPaths is null) continue;
                    for (var i = 0; i < revision.ImportVideoPaths.Count; i++)
                    {
                        var quelle = revision.ImportVideoPaths[i];
                        if (verteiltePfade.TryGetValue(quelle, out var bekannt))
                        {
                            revision.ImportVideoPaths[i] = bekannt;
                            continue;
                        }
                        // Derselbe Kopierweg wie Link/Link_G, ohne die aktive
                        // Untersuchung oder ihre Felder dafür auszutauschen.
                        var datei = new HaltungRecord();
                        datei.SetFieldValue(FieldKeys.Link, quelle, FieldSource.Legacy, false);
                        var zusatz = "-aufnahme-" + Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
                            System.Text.Encoding.UTF8.GetBytes(Path.GetFileName(quelle))))[..12];
                        switch (VerteileQuellvideo(datei, haltung!, FieldKeys.Link, zusatz, projectFolder, fileStaging, messages))
                        {
                            case VideoVerteilung.Verteilt: videos++; break;
                            case VideoVerteilung.Fehler: errors++; break;
                        }
                        var ziel = datei.GetFieldValue(FieldKeys.Link) ?? quelle;
                        revision.ImportVideoPaths[i] = ziel;
                        verteiltePfade[quelle] = ziel;
                    }
                }
            }
        }

        return new KanalImportDistributor.Result(videos, origs, errors, messages);
    }

    /// <summary>
    /// Die Videofelder einer Haltung samt Zusatz im Zieldateinamen. Beide laufen ueber
    /// denselben abgesicherten Kopierweg; der Zusatz "-g" ist die bestehende
    /// Namenskonvention der Verteilung und bleibt unveraendert.
    /// </summary>
    /// <summary>Ergebnis je Videofeld — ein Fehler beim einen darf das andere nicht mitreissen.</summary>
    private enum VideoVerteilung { NichtsZuTun, Verteilt, Fehler }

    private static readonly (string Feld, string Namenszusatz)[] VideoFelder =
    [
        (FieldKeys.Link, ""),
        ("Link_G", "-g")
    ];

    /// <summary>
    /// Kopiert ein noch auf die Quelle zeigendes Video ins Projekt und macht den Link
    /// relativ. Liefert <c>true</c>, wenn dabei wirklich ein Video verteilt wurde.
    ///
    /// Ein Fehler bei EINEM Feld darf das andere nicht mitreissen: Wenn das Hauptvideo
    /// ankommt und die Gegenkopie scheitert, bleibt das Hauptvideo verteilt und der
    /// Fehler steht im Bericht.
    /// </summary>
    private VideoVerteilung VerteileQuellvideo(
        HaltungRecord record,
        string haltung,
        string feld,
        string namenszusatz,
        string projectFolder,
        IImportFileStagingSession? fileStaging,
        List<string> messages)
    {
        var link = record.GetFieldValue(feld)?.Trim();
        if (string.IsNullOrWhiteSpace(link))
            return VideoVerteilung.NichtsZuTun;

        if (ProjectPathResolver.IsRelative(link))
        {
            if (ImportProjektdateiPruefer.IstLesbar(link, projectFolder, fileStaging, out var grund))
                return VideoVerteilung.NichtsZuTun;
            messages.Add($"Video {haltung}{namenszusatz}: Projektdatei nicht lesbar: {link} — {grund}");
            return VideoVerteilung.Fehler;
        }

        if (!TryInspectExistingSourceFile(link, out var safeLink, out var sourceError))
        {
            // Eine schlicht fehlende Quelldatei liefert keinen Fehlertext. Sie darf
            // trotzdem nicht still verschwinden: Der Verweis stand im Projekt, die Datei
            // ist nicht da — das muss im Bericht stehen (Audit 2026-09-05).
            messages.Add(string.IsNullOrWhiteSpace(sourceError)
                ? $"Video {haltung}{namenszusatz}: Quelldatei nicht vorhanden: {link}"
                : $"Video {haltung}{namenszusatz}: {sourceError}");
            return VideoVerteilung.Fehler;
        }

        // S2-1: Nur bekannte Medientypen/Protokoll-PDFs ins Projekt kopieren.
        if (!MediaFileAllowlist.IsImportableMediaOrPdf(safeLink))
        {
            messages.Add($"Video {haltung}{namenszusatz}: Dateityp nicht erlaubt, wird nicht kopiert: {link}");
            return VideoVerteilung.Fehler;
        }

        try
        {
            if (!TryInspectExistingSourceFile(safeLink, out safeLink, out sourceError))
                throw new IOException(sourceError ?? "Quelldatei fehlt.");

            var san = ProjectPathResolver.SanitizePathSegment(haltung);
            var dir = ProjectStructure.HaltungVerteiltDir(projectFolder, san);
            var stamp = ResolveDateStamp(record);
            var ext = Path.GetExtension(safeLink);
            var zielname = $"{stamp}_{san}{namenszusatz}{ext}";
            string dest;
            var wiederverwendet = false;
            if (fileStaging is null)
            {
                var writePathGuard = new ProjectWritePathGuard(projectFolder);
                dir = writePathGuard.EnsureSafeDirectoryTarget(dir);
                Directory.CreateDirectory(dir);

                // Liegt am Zielort schon dieselbe Datei, wird sie wiederverwendet statt ein
                // zweites Mal kopiert. Ohne diese Pruefung legte jeder erneute Import
                // desselben Ordners eine weitere Kopie "..._1.mpg" an und verbog den Link
                // darauf (gemessen 2026-09-05). Ein abweichender Bestand bekommt weiterhin
                // einen freien Namen — dieselbe Regel wie in der Dichtheitsverteilung und
                // im Staging-Weg, die beide schon so arbeiten.
                var wunsch = writePathGuard.EnsureSafeFileTarget(Path.Combine(dir, zielname));
                if (File.Exists(wunsch) && FileContentComparer.FilesEqual(wunsch, safeLink))
                {
                    dest = wunsch;
                    wiederverwendet = true;
                }
                else
                {
                    dest = writePathGuard.EnsureSafeFileTarget(UniquePath(wunsch));
                    writePathGuard.EnsureSafeFileTarget(dest);
                    File.Copy(safeLink, dest, overwrite: false);
                }
            }
            else
            {
                dest = fileStaging.StageCopyAs(safeLink, dir, zielname);
            }

            // Der Link wird erst nach erfolgreicher Kopie gesetzt: Ein Verweis auf eine
            // Datei, die nie ankam, waere schlimmer als der alte Quellpfad.
            record.SetFieldValue(
                feld,
                ProjectPathResolver.MakeRelative(dest, projectFolder),
                FieldSource.Legacy,
                userEdited: false);

            // Nur eine wirklich angelegte Kopie zaehlt als verteiltes Video. Der Link wurde
            // auch im Wiederverwendungsfall wieder auf den Projektpfad gesetzt — das ist
            // eine Reparatur, keine Verteilung.
            return wiederverwendet ? VideoVerteilung.NichtsZuTun : VideoVerteilung.Verteilt;
        }
        catch (Exception ex)
        {
            messages.Add($"Video {haltung}{namenszusatz}: {ex.Message}");
            return VideoVerteilung.Fehler;
        }
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
        // Alle Inspektionsprotokolle des Archivs, nicht nur eines. Ein Ordner kann ein
        // Einzelprotokoll fuer Haltung A und ein Sammelprotokoll fuer B und C enthalten —
        // bis 2026-09-05 wurde genau eines davon gesplittet und der Rest blieb liegen.
        // Plaene, Deckblaetter und Dichtheitsprotokolle sind ueber die Bewertung in
        // ScoreProtocolCandidate weiterhin ausgeschlossen.
        var protokolle = ResolveProtocolPdfs(primaryProtocolPdf, archivedPdfDir, fileStaging);
        if (protokolle.Count == 0)
            return Array.Empty<HoldingFolderDistributor.DistributionResult>();

        if (fileStaging is null)
        {
            new ProjectWritePathGuard(projectFolder)
                .EnsureSafeDirectoryTarget(logicalDestinationRoot);
            return HoldingFolderDistributor.DistributeFiles(
                pdfFiles: protokolle.Select(p => p.ReadPath).ToList(),
                videoSourceFolder: sourceVideoDir,
                destGemeindeFolder: logicalDestinationRoot,
                project: project);
        }

        using var output = new StagedDistributionOutput();
        var readablePdfs = protokolle
            .Select(p => output.CreateReadableCopy(p.ReadPath, Path.GetFileName(p.TargetPath)))
            .ToList();
        var temporaryResults = HoldingFolderDistributor.DistributeFiles(
            pdfFiles: readablePdfs,
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

    /// <summary>
    /// Alle Inspektionsprotokolle, die gesplittet werden sollen — in begruendeter
    /// Reihenfolge (bestbewertetes zuerst). Ist ein Protokoll ausdruecklich vorgegeben
    /// (KINS-Gesamtprotokoll), gilt nur dieses.
    /// </summary>
    private IReadOnlyList<ImportReadableFile> ResolveProtocolPdfs(
        string? primaryProtocolPdf,
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging)
    {
        var vorgegeben = ResolvePrimaryProtocol(
            primaryProtocolPdf, archivedPdfDir, fileStaging, nurVorgabe: true);
        if (vorgegeben is not null)
            return [vorgegeben];

        return SelectProtocolPdfsReadable(archivedPdfDir, fileStaging);
    }

    private ImportReadableFile? ResolvePrimaryProtocol(
        string? primaryProtocolPdf,
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging,
        bool nurVorgabe = false)
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

        return nurVorgabe
            ? null
            : SelectProtocolPdfsReadable(archivedPdfDir, fileStaging).FirstOrDefault();
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
        => SelectProtocolPdfsReadable(archivedPdfDir, fileStaging: null).FirstOrDefault()?.ReadPath;

    private IReadOnlyList<ImportReadableFile> SelectProtocolPdfsReadable(
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging)
    {
        if (!ImportSourcePathGuard.TryInspectDirectory(
                archivedPdfDir,
                out var safeArchivedPdfDir,
                out var archiveExists,
                out _))
        {
            return [];
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
            return [];

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
            // Plaene, Deckblaetter und Dichtheitsprotokolle tragen eine negative Bewertung
            // und sind damit ausgeschlossen — sie sind keine TV-Originale.
            .Where(p => p.Score >= 0)
            .OrderByDescending(p => p.Score)
            .ThenByDescending(p => p.Length)
            .ThenBy(p => Path.GetFileName(p.File.TargetPath), StringComparer.OrdinalIgnoreCase)
            .Select(p => p.File)
            .ToList();
    }

    private static int ScoreProtocolCandidate(PdfDokumentTyp typ, bool isDuplicateVariant)
    {
        var score = typ switch
        {
            PdfDokumentTyp.TvProtokoll => 100,
            PdfDokumentTyp.PlanSituation => -1000,
            PdfDokumentTyp.Dichtheitspruefung => -900,
            PdfDokumentTyp.Deckblatt => -800,
            PdfDokumentTyp.Schachtprotokoll => -1000,
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
