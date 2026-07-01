using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Domain.Models;

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
///   3. Formatentkennung (WinCan / IKAS)
///   4. Quelldateien archivieren
///   5. Parsen (XTF oder WinCan)
///   6. SIA405-Whitelist-Anreicherung (nur IKAS)
///   7. Medien verteilen
///   8. Projekt als geaendert markieren
///
/// Jeder Schritt laeuft in einem try/catch: Fehler werden als Message gesammelt,
/// der Lauf wird nicht abgebrochen.
/// </summary>
public sealed class ProjectImportOrchestrator
{
    private readonly IXtfImportService _xtf;
    private readonly IWinCanDbImportService _winCan;

    public ProjectImportOrchestrator(IXtfImportService xtf, IWinCanDbImportService winCan)
    {
        _xtf    = xtf    ?? throw new ArgumentNullException(nameof(xtf));
        _winCan = winCan ?? throw new ArgumentNullException(nameof(winCan));
    }

    /// <summary>
    /// Fuehrt den vollstaendigen Ein-Knopf-Import durch.
    /// </summary>
    /// <param name="sourceFolder">Quellordner des Kanalfernsehen-Exports.</param>
    /// <param name="projectFolder">Projektstammordner (wird angelegt falls nicht vorhanden).</param>
    /// <param name="project">Offenes Projekt-Objekt.</param>
    /// <param name="ctx">Optionaler Lauf-Kontext (CancellationToken, Log, …).</param>
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
            ProjectStructure.EnsureCreated(projectFolder);
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
            var projektJson = Path.Combine(projectFolder, "projekt.json");
            if (File.Exists(projektJson))
            {
                var zeitstempel = DateTime.Now.ToString("yyyyMMdd_HHmmss");
                var restoreDir  = Path.Combine(
                    projectFolder,
                    ProjectStructure.RestorePoints,
                    "projekt",
                    zeitstempel);
                Directory.CreateDirectory(restoreDir);
                File.Copy(projektJson, Path.Combine(restoreDir, "projekt.json"), overwrite: false);
                messages.Add($"Restore-Point angelegt: {restoreDir}");
            }
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
            det = KanalExportDetector.Detect(sourceFolder);
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

        // ------------------------------------------------------------------
        // Schritt 4: Quelldateien archivieren
        // ------------------------------------------------------------------
        try
        {
            var archiveResult = ImportSourceArchiver.Archive(sourceFolder, projectFolder);
            messages.AddRange(archiveResult.Messages);
            messages.Add(
                $"Archiviert: {archiveResult.Copied} neu, {archiveResult.Reused} wiederverwendet.");
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
            var archivedPdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var distResult = KanalImportDistributor.Distribute(project, projectFolder, archivedPdfDir, sourceFolder);
            messages.AddRange(distResult.Messages);
            errors += distResult.Errors;

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

        return new OneClickImportResult(
            det.Format, found, created, updated, errors, conflictCount, messages);
    }
}
