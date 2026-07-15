using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Ergebnis des Ein-Knopf-Imports.
/// </summary>
public sealed record OneClickImportResult(
    KanalExportFormat Format,
    int Found,
    int Created,
    int Updated,
    int Errors,
    int Conflicts,
    IReadOnlyList<string> Messages);

/// <summary>
/// Orchestriert den vollstaendigen Ein-Knopf-Import:
///   1. Projektstruktur sicherstellen
///   2. Restore-Point anlegen
///   3. Formatentkennung (WinCan / IKAS / KINS)
///   4. Quelldateien archivieren
///   5. Parsen (XTF oder WinCan)
///   6. SIA405-Whitelist-Anreicherung (nur IKAS)
///   7. Medien verteilen
///   8. Projekt als geaendert markieren
///
/// Jeder Schritt laeuft in einem try/catch: Fehler werden als Message gesammelt,
/// der Lauf wird nicht abgebrochen.
/// </summary>
public sealed class ProjectImportOrchestrator : IOneClickProjectImportService
{
    private readonly IXtfImportService _xtf;
    private readonly IWinCanDbImportService _winCan;
    private readonly IKinsImportService? _kins;
    private readonly IIbakImportService? _ibak;

    // R4: optionaler KI-Schiedsrichter (Qwen via Ollama) fuer unklare PDFs.
    private readonly PdfKiSchiedsrichter? _kiSchiedsrichter;

    // Task 4: optionaler name-basierter Protokoll-Verteiler (narrensicher, Dateiname-basiert).
    private readonly INameBasedProtocolDistributor? _protocolDistributor;
    private readonly IPlanPdfImporter _planPdfImporter;
    private readonly IProjectRestorePointService _projectRestorePoints;
    private readonly IImportSourceArchiver _sourceArchiver;
    private readonly IDichtheitImportDistributor _dichtheitDistributor;
    private readonly IKanalImportDistributor _kanalDistributor;
    private readonly IProjectStructureInitializer _projectStructure;
    private readonly IKanalExportDetectionService _exportDetector;
    private readonly IKinsDvdTextEnricher _kinsDvdTextEnricher;

    public ProjectImportOrchestrator(
        IXtfImportService xtf,
        IWinCanDbImportService winCan,
        IKinsImportService? kins = null,
        IIbakImportService? ibak = null,
        PdfKiSchiedsrichter? kiSchiedsrichter = null,
        INameBasedProtocolDistributor? protocolDistributor = null,
        IPlanPdfImporter? planPdfImporter = null,
        IProjectRestorePointService? projectRestorePoints = null,
        IImportSourceArchiver? sourceArchiver = null,
        IDichtheitImportDistributor? dichtheitDistributor = null,
        IKanalImportDistributor? kanalDistributor = null,
        IProjectStructureInitializer? projectStructure = null,
        IKanalExportDetectionService? exportDetector = null,
        IKinsDvdTextEnricher? kinsDvdTextEnricher = null)
    {
        _kiSchiedsrichter = kiSchiedsrichter;
        _xtf    = xtf    ?? throw new ArgumentNullException(nameof(xtf));
        _winCan = winCan ?? throw new ArgumentNullException(nameof(winCan));
        _kins   = kins;
        _ibak   = ibak;
        _protocolDistributor = protocolDistributor;
        _planPdfImporter = planPdfImporter ?? new PlanPdfImportService();
        _projectRestorePoints = projectRestorePoints ?? new ProjectRestorePointStore();
        _sourceArchiver = sourceArchiver ?? new ImportSourceArchiveService();
        _dichtheitDistributor = dichtheitDistributor ?? new DichtheitImportDistributionService();
        _kanalDistributor = kanalDistributor ?? new KanalImportDistributionService();
        _projectStructure = projectStructure ?? new ProjectStructureInitializer();
        _exportDetector = exportDetector ?? new KanalExportDetectionService();
        _kinsDvdTextEnricher = kinsDvdTextEnricher ?? Kins.KinsDvdTextEnricher.Current;
    }

    /// <summary>
    /// Fuehrt den vollstaendigen Ein-Knopf-Import durch.
    /// </summary>
    /// <param name="sourceFolder">Quellordner des Kanalfernsehen-Exports.</param>
    /// <param name="projectFolder">Projektstammordner (wird angelegt falls nicht vorhanden).</param>
    /// <param name="project">Offenes Projekt-Objekt.</param>
    /// <param name="ctx">Optionaler Lauf-Kontext (CancellationToken, Log, …).</param>
    OneClickProjectImportResult IOneClickProjectImportService.Import(
        string sourceFolder,
        string projectFolder,
        Project project,
        ImportRunContext? context)
    {
        var result = Import(sourceFolder, projectFolder, project, context);
        return new OneClickProjectImportResult(
            result.Format switch
            {
                KanalExportFormat.Ikas => OneClickProjectImportFormat.Ikas,
                KanalExportFormat.Ibak => OneClickProjectImportFormat.Ibak,
                KanalExportFormat.WinCan => OneClickProjectImportFormat.WinCan,
                KanalExportFormat.Ambiguous => OneClickProjectImportFormat.Ambiguous,
                KanalExportFormat.Kins => OneClickProjectImportFormat.Kins,
                _ => OneClickProjectImportFormat.Unknown
            },
            result.Found,
            result.Created,
            result.Updated,
            result.Errors,
            result.Conflicts,
            result.Messages);
    }

    public OneClickImportResult Import(
        string sourceFolder,
        string projectFolder,
        Project project,
        ImportRunContext? ctx = null)
    {
        var messages      = new List<string>();
        var found         = 0;
        var created       = 0;
        var updated       = 0;
        var errors        = 0;
        var conflictCount = 0;

        var ct = ctx?.CancellationToken ?? System.Threading.CancellationToken.None;

        // ------------------------------------------------------------------
        // Schritt 1: Projektstruktur sicherstellen
        // ------------------------------------------------------------------
        try
        {
            _projectStructure.EnsureCreated(projectFolder);
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"EnsureCreated fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 2: Restore-Point (best-effort)
        // ------------------------------------------------------------------
        try
        {
            // Bugfix AP-02: Neue Projekte legen projekt.json unter Projektdateien\ ab,
            // Alt-Projekte direkt im Root. ProjectFileLocator findet beide Faelle — der
            // frueher hartkodierte Root-Pfad uebersprang das Sicherheitsnetz bei neuen Projekten.
            var restorePoint = _projectRestorePoints.TryCreateForProjectFolder(projectFolder);
            messages.Add(restorePoint.Message);
        }
        catch (Exception ex)
        {
            messages.Add($"Restore-Point fehlgeschlagen (nicht kritisch): {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 3: Formatentkennung
        // ------------------------------------------------------------------
        KanalExportDetection det;
        try
        {
            det = _exportDetector.Detect(sourceFolder);
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Formaterkennung fehlgeschlagen: {ex.Message}");
            return new OneClickImportResult(
                KanalExportFormat.Unknown, found, created, updated, errors, conflictCount, messages);
        }

        // Bei unbekanntem oder mehrdeutigem Format sofort abbrechen
        if (det.Format == KanalExportFormat.Unknown || det.Format == KanalExportFormat.Ambiguous)
        {
            messages.Add($"Import abgebrochen: {det.Reason}");
            return new OneClickImportResult(
                det.Format, found, created, updated, errors, conflictCount, messages);
        }

        messages.AddRange(BuildSourceDecisionMessages(det));

        // ------------------------------------------------------------------
        // Schritt 4: Quelldateien archivieren
        // ------------------------------------------------------------------
        try
        {
            var archiveResult = _sourceArchiver.Archive(sourceFolder, projectFolder);
            messages.AddRange(archiveResult.Messages);
            messages.Add(
                $"Archiviert: {archiveResult.Copied} neu, {archiveResult.Reused} wiederverwendet.");

            var archivePdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var planResult = _planPdfImporter.ImportFromArchivedPdfFolder(archivePdfDir, projectFolder);
            messages.AddRange(planResult.Messages);
            errors += planResult.Errors;
            if (planResult.Copied > 0 || planResult.Reused > 0 || planResult.Errors > 0)
            {
                messages.Add(
                    $"Pläne: {planResult.Copied} neu, {planResult.Reused} wiederverwendet, " +
                    $"{planResult.Errors} Fehler.");
            }
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Archivierung fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 5: Parsen
        // ------------------------------------------------------------------
        try
        {
            Result<ImportStats> parseResult;

            if (det.Format == KanalExportFormat.Ikas)
            {
                parseResult = _xtf.ImportXtfFiles(new[] { det.VsaKekXtfPath! }, project, ctx);
            }
            else if (det.Format == KanalExportFormat.Ibak)
            {
                parseResult = _ibak is not null
                    ? _ibak.ImportIbakExport(sourceFolder, project, ctx)
                    : Result<ImportStats>.Success(new ImportStats(
                        Found: 0,
                        Created: 0,
                        Updated: 0,
                        Errors: 0,
                        Uncertain: 0,
                        Messages: new[] { "IBAK/KIAS erkannt; IBAK-Daten.txt-Importer nicht konfiguriert, nur PDF-Fallback." }));
            }
            else if (det.Format == KanalExportFormat.Kins)
            {
                // KINS: massgebliche Quelle ist das VSAKEK-XTF (wie IKAS);
                // alte DVDs ohne XTF laufen ueber den kiDVDaten.txt-Import.
                if (det.VsaKekXtfPath is not null)
                    parseResult = _xtf.ImportXtfFiles(new[] { det.VsaKekXtfPath }, project, ctx);
                else if (_kins is not null)
                    parseResult = _kins.ImportKinsExport(sourceFolder, project, ctx);
                else
                    parseResult = Result<ImportStats>.Fail(
                        "KINS_SERVICE_MISSING", "KINS ohne XTF erkannt, aber kein KINS-Importservice verfuegbar.");
            }
            else // WinCan
            {
                parseResult = _winCan.ImportWinCanExport(sourceFolder, project, ctx);
            }

            if (parseResult.Ok && parseResult.Value is not null)
            {
                found   += parseResult.Value.Found;
                created += parseResult.Value.Created;
                updated += parseResult.Value.Updated;
                errors  += parseResult.Value.Errors;
                messages.AddRange(parseResult.Value.Messages);
            }
            else
            {
                errors++;
                messages.Add(
                    $"Parse fehlgeschlagen [{parseResult.ErrorCode}]: {parseResult.ErrorMessage}");
            }
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Parse-Ausnahme: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 5b: KINS-Anreicherung (Namen, Timecodes/Laenge, DBF-Stammdaten)
        // ------------------------------------------------------------------
        if (det.Format == KanalExportFormat.Kins)
        {
            try
            {
                // 1. Numerische XTF-Bezeichnungen → "{Schacht_oben}-{Schacht_unten}"
                //    (merkt die Bezeichnung, raeumt Re-Import-Duplikate ab)
                var nameResult = Kins.KinsHoldingNameNormalizer.Apply(project, ctx);
                messages.AddRange(nameResult.Messages);
                if (nameResult.Umbenannt > 0 || nameResult.DuplikateEntfernt > 0)
                    messages.Add($"KINS-Namen: {nameResult.Umbenannt} normalisiert, {nameResult.DuplikateEntfernt} Re-Import-Duplikate entfernt.");

                // 2. kiDVDaten.txt: Video-Timecodes je Beobachtung + inspizierte Laenge + Datum
                if (det.KinsDataTxtPath is not null)
                {
                    var txtResult = _kinsDvdTextEnricher.Apply(project, det.KinsDataTxtPath);
                    messages.AddRange(txtResult.Messages);
                    messages.Add($"KINS-TXT: {txtResult.TimecodesGesetzt} Timecodes, {txtResult.LaengenGesetzt} Laengen, {txtResult.DatumGesetzt} Daten gesetzt.");
                }

                // 3. FoxPro-DBF: Schachtliste + Whitelist fuer leere Stammdaten
                var dbfResult = Kins.KinsDbfWhitelistEnricher.Apply(project, sourceFolder, ctx);
                messages.AddRange(dbfResult.Messages);
                messages.Add($"KINS-DBF: {dbfResult.HaltungsfelderGesetzt} Haltungsfelder, {dbfResult.SchaechteNeu} Schaechte neu, {dbfResult.SchaechteAktualisiert} aktualisiert.");
            }
            catch (Exception ex)
            {
                errors++;
                messages.Add($"KINS-Anreicherung fehlgeschlagen: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Schritt 6: SIA405-Anreicherung (nur IKAS, nur wenn Pfad bekannt)
        // ------------------------------------------------------------------
        if (det.Format == KanalExportFormat.Ikas && det.Sia405XtfPath != null)
        {
            try
            {
                // SIA405-XTF in ein temporaeres Projekt importieren
                var tmp             = new Project();
                var sia405Result    = _xtf.ImportXtfFiles(new[] { det.Sia405XtfPath }, tmp, null);

                if (sia405Result.Ok && sia405Result.Value is not null)
                {
                    // Whitelist-Map aufbauen: Haltungsname -> (Feldname -> Wert)
                    var sia405ByHaltung =
                        new Dictionary<string, IReadOnlyDictionary<string, string>>(
                            StringComparer.OrdinalIgnoreCase);

                    foreach (var rec in tmp.Data)
                    {
                        var haltungsname = rec.GetFieldValue("Haltungsname");
                        if (string.IsNullOrWhiteSpace(haltungsname))
                            continue;

                        var felder = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                        foreach (var feld in Sia405WhitelistEnricher.Whitelist)
                        {
                            var wert = rec.GetFieldValue(feld);
                            if (!string.IsNullOrEmpty(wert))
                                felder[feld] = wert;
                        }

                        if (felder.Count > 0)
                            sia405ByHaltung.TryAdd(haltungsname, felder);
                    }

                    // Anreicherung anwenden
                    var enrichResult = Sia405WhitelistEnricher.Apply(project, sia405ByHaltung);
                    conflictCount += enrichResult.Conflicts.Count;
                    messages.AddRange(enrichResult.Conflicts);
                    messages.Add(
                        $"SIA405-Anreicherung: {enrichResult.Filled} Felder gefuellt, " +
                        $"{enrichResult.Conflicts.Count} Konflikte.");
                }
                else
                {
                    messages.Add(
                        $"SIA405-Import fehlgeschlagen [{sia405Result.ErrorCode}]: {sia405Result.ErrorMessage}");
                }
            }
            catch (Exception ex)
            {
                messages.Add($"SIA405-Anreicherung fehlgeschlagen (nicht kritisch): {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Schritt 7: Medien verteilen
        // ------------------------------------------------------------------
        try
        {
            // 7a) Fotos zentral gruppiert (Fotos\Haltungen\) — KEINE Videos/Original-PDFs und KEINE Schacht-
            //     Kopie (Schächte kommen in 7c als seiten-gruppierte Protokolle; Videos/Protokolle in 7b).
            var mediaResult = new MediaDistributionService()
                .DistributeImportedMedia(
                    projectFolder,
                    project,
                    progress: null,
                    ct: ct,
                    dryRun: false,
                    collectionLock: new object(),
                    includeVideos: false,
                    includePdfs: false,
                    includeSchacht: false);
            messages.AddRange(mediaResult.Messages);

            // 7b) Video + ORIGINAL-Protokoll (NUR das maßgebliche PDF, ein PDF/Haltung) flach+datumsbenannt
            //     verteilen; beide relativ verlinkt (PDF_Path = Original). Das eigene _E-Protokoll wird hier
            //     NICHT erzeugt — das macht der ProtocolRegenerationService („Protokoll neu generieren").
            //     KINS: Der Seiten-Split laeuft auf dem expliziten Gesamtprotokoll aus der Quelle
            //     (*_Protokoll.pdf) — die Auto-Wahl "groesste Archiv-PDF" traefe sonst Plaene/fremde PDFs.
            var kinsGesamtprotokoll = det.Format == KanalExportFormat.Kins
                ? Kins.KinsGesamtprotokollLocator.Finde(sourceFolder)
                : null;
            var archivedPdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var recordCountBeforeDistribution = project.Data.Count;

            // Name-basierte Protokoll-Verteilung zuerst (narrensicher, Dateiname-basiert).
            // CollectionLock aus dem Lauf-Kontext mitgeben: das Anlegen neuer Schächte läuft ggf. auf
            // einem Hintergrund-Thread und mutiert die UI-gebundene SchaechteData-Collection.
            var nameBased = _protocolDistributor?.Distribute(project, projectFolder, archivedPdfDir, ctx?.CollectionLock);
            // Nur HALTUNG-Treffer duerfen den inhaltsbasierten Gesamtprotokoll-Split unterdruecken —
            // der Split verteilt HALTUNGS-Protokolle. Ein reiner Schacht-Treffer darf ihn NICHT abschalten.
            var nameBasedHaltungHits = nameBased?.HaltungProtokolle ?? 0;
            if (nameBased is not null)
            {
                messages.Add($"Protokolle name-basiert verteilt: {nameBased.HaltungProtokolle} Haltungen, {nameBased.SchachtProtokolle} Schächte, {nameBased.SchaechteAngelegt} Schächte angelegt.");
                foreach (var nz in nameBased.NichtZugeordnet)
                    messages.Add($"Protokoll nicht zugeordnet: {nz}");
            }

            var distResult = _kanalDistributor.Distribute(
                project, projectFolder, archivedPdfDir, sourceFolder,
                splitPdf: nameBasedHaltungHits == 0 && (det.Format != KanalExportFormat.Kins || kinsGesamtprotokoll is not null),
                primaryProtocolPdf: kinsGesamtprotokoll);
            messages.AddRange(distResult.Messages);
            errors += distResult.Errors;
            var recordsCreatedByDistribution = Math.Max(0, project.Data.Count - recordCountBeforeDistribution);
            if (recordsCreatedByDistribution > 0)
            {
                found += recordsCreatedByDistribution;
                created += recordsCreatedByDistribution;
                messages.Add($"PDF-Fallback: {recordsCreatedByDistribution} Haltungen aus Original-Protokollen angelegt.");
            }

            // 7c) Dichtheitspruefungsprotokolle (DP) aus der Quelle je Haltung verteilen
            //     (<JJJJMMTT>_<H>_DP.pdf) — Kanalfernseh- UND DP-Protokolle liegen damit
            //     gemeinsam im Haltungen_Verteilt-Ordner. Sicher erkannte DP-PDFs
            //     duerfen auch in neutralen Dokumente-Ordnern liegen; die KI-Zweitmeinung
            //     bleibt auf DP-/Dichtheits-Ordner begrenzt.
            var dpResult = _dichtheitDistributor.Distribute(
                project,
                projectFolder,
                sourceFolder,
                _kiSchiedsrichter);
            messages.AddRange(dpResult.Messages);
            if (dpResult.Verteilt > 0 || dpResult.NichtZugeordnet > 0 || dpResult.Uebersprungen > 0)
                messages.Add($"Dichtheitspruefung: {dpResult.Verteilt} Protokolle verteilt, {dpResult.NichtZugeordnet} nicht zugeordnet, {dpResult.Uebersprungen} bereits vorhanden.");

            // HINWEIS: Schächte verteilt der Import bewusst NICHT (includeSchacht:false oben) — das macht der
            // Anwender manuell über „Schacht Verteilen" mit dem separaten Schacht-Gesamtauszug-PDF, damit kein
            // falsches/ganzes PDF automatisch an die Schächte gehängt wird.

            messages.Add(
                $"Verteilung: {mediaResult.FilesCopied} Fotos/Dateien, {distResult.VideosDistributed} Videos, " +
                $"{distResult.OriginalProtocolsDistributed} Original-Protokolle, " +
                $"{mediaResult.Errors + distResult.Errors} Fehler.");
        }
        catch (Exception ex)
        {
            errors++;
            messages.Add($"Medienverteilung fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 8: Projekt als geaendert markieren
        // ------------------------------------------------------------------
        project.Dirty = true;

        if (found == 0 && HasDataSourceSignal(det, sourceFolder))
        {
            messages.Add(
                "WARNUNG: 0 Haltungen importiert, obwohl Datenquellen erkannt wurden. " +
                "Bitte Report pruefen; die Herstellerquelle wurde vermutlich nicht gelesen oder enthaelt ein unbekanntes Schema.");
        }

        return new OneClickImportResult(
            det.Format, found, created, updated, errors, conflictCount, messages);
    }

    private static IReadOnlyList<string> BuildSourceDecisionMessages(KanalExportDetection det)
    {
        var messages = new List<string>
        {
            $"Erkanntes Format: {det.Format} - {det.Reason}"
        };

        switch (det.Format)
        {
            case KanalExportFormat.WinCan:
                messages.Add($"Hauptquelle: WinCan .db3 ({Path.GetFileName(det.Db3Path ?? "")}).");
                messages.Add("PDF/TXT/XTF: archiviert; nicht als Stammdaten-Hauptquelle gelesen.");
                break;

            case KanalExportFormat.Ikas:
                messages.Add($"Hauptquelle: IKAS VSA_KEK-XTF ({Path.GetFileName(det.VsaKekXtfPath ?? "")}).");
                messages.Add(det.Sia405XtfPath is null
                    ? "SIA405: nicht vorhanden."
                    : $"SIA405: Whitelist-Anreicherung aus {Path.GetFileName(det.Sia405XtfPath)}.");
                messages.Add("FDB/Daten.txt/PDF: archiviert; PDF nur fuer Plan-Import und Protokoll-Verteilung.");
                break;

            case KanalExportFormat.Ibak:
                messages.Add("Hauptquelle: IBAK/KIAS Daten.txt (Arizona.fdb/PDF werden archiviert und ergaenzend genutzt, falls Service es unterstuetzt).");
                messages.Add("PDF: archiviert; TV-Protokoll nur fuer Verteilung, Plan-PDF nur fuer den Ordner Plaene.");
                break;

            case KanalExportFormat.Kins:
                if (!string.IsNullOrWhiteSpace(det.VsaKekXtfPath))
                    messages.Add($"Hauptquelle: KINS VSA_KEK-XTF ({Path.GetFileName(det.VsaKekXtfPath)}).");
                else
                    messages.Add($"Hauptquelle: KINS kiDVDaten.txt ({Path.GetFileName(det.KinsDataTxtPath ?? "")}).");
                messages.Add("KINS-Zusatzquellen: kiDVDaten.txt/DBF nur fuer Timecodes, Laengen, Schaechte und Whitelist-Felder.");
                break;
        }

        return messages;
    }

    private static bool HasDataSourceSignal(KanalExportDetection det, string sourceFolder)
        => !string.IsNullOrWhiteSpace(det.Db3Path)
           || !string.IsNullOrWhiteSpace(det.VsaKekXtfPath)
           || !string.IsNullOrWhiteSpace(det.KinsDataTxtPath)
           || AnyFile(sourceFolder, "Daten.txt")
           || AnyFile(sourceFolder, "*.fdb")
           || AnyFile(sourceFolder, "*.xtf");

    private static bool AnyFile(string root, string pattern)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return false;

            var opts = new EnumerationOptions
            {
                RecurseSubdirectories = true,
                IgnoreInaccessible = true,
                MatchCasing = MatchCasing.CaseInsensitive
            };
            return Directory.EnumerateFiles(root, pattern, opts).Any();
        }
        catch
        {
            return false;
        }
    }
}
