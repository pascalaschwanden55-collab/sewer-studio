using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
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
    IReadOnlyList<string> Messages)
{
    /// <summary>Haltungen, die die geprueften Quellen versprechen.</summary>
    public int ErwarteteHaltungen { get; init; }

    /// <summary>Tatsaechlich verarbeitete Haltungen, ohne Schaechte.</summary>
    public int BearbeiteteHaltungen { get; init; }

    /// <summary>Protokoll der geprueften Importquellen. Null = kein Urteil moeglich.</summary>
    public QuellenwahlErgebnis? Quellenprotokoll { get; init; }

    /// <summary>Fehler nach Schritten getrennt; Summe muss <see cref="Errors"/> entsprechen.</summary>
    public ImportFehlerbilanz Fehlerbilanz { get; init; } = ImportFehlerbilanz.Leer;

    /// <summary>Was nach dem Lauf im Projekt steht — Videos, Protokolle, Befunde.</summary>
    public ImportBestandsbilanz? Bestand { get; init; }
}

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
    private readonly IKinsDbfWhitelistEnricher _kinsDbfWhitelistEnricher;
    private readonly IKinsGesamtprotokollLocator _kinsGesamtprotokollLocator;
    private readonly IImportMediaDistributionService _mediaDistributor;

    // Derselbe Dienst wie der manuelle Befehl "Schacht Verteilen" — kein zweiter Splitter.
    private readonly IShaftDistributionService _shaftDistribution;

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
        IKinsDvdTextEnricher? kinsDvdTextEnricher = null,
        IKinsDbfWhitelistEnricher? kinsDbfWhitelistEnricher = null,
        IKinsGesamtprotokollLocator? kinsGesamtprotokollLocator = null,
        IImportMediaDistributionService? mediaDistributor = null,
        IShaftDistributionService? shaftDistribution = null)
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
        _kinsDbfWhitelistEnricher = kinsDbfWhitelistEnricher ?? Kins.KinsDbfWhitelistEnricher.Current;
        _kinsGesamtprotokollLocator = kinsGesamtprotokollLocator ?? Kins.KinsGesamtprotokollLocator.Current;
        _mediaDistributor = mediaDistributor ?? new MediaDistributionService();
        _shaftDistribution = shaftDistribution ?? new ShaftDistributionService();
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
            result.Messages)
        {
            ErwarteteHaltungen = result.ErwarteteHaltungen,
            BearbeiteteHaltungen = result.BearbeiteteHaltungen,
            Quellenprotokoll = result.Quellenprotokoll,
            Fehlerbilanz = result.Fehlerbilanz,
            Bestand = result.Bestand
        };
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
        var conflictCount = 0;
        // Fehler werden ab 2026-09-05 schrittweise gezaehlt statt als blosse Gesamtzahl.
        // Vorher verlor der Lauf Fehler unbemerkt: Die Fotoverteilung zaehlte gar nicht
        // mit, und die Kopierfehler der name-basierten Protokollverteilung wurden nie
        // gelesen. "0 Fehler" war deshalb keine Aussage ueber Vollstaendigkeit.
        var fehlerbilanz  = new ImportFehlerbilanzSammler();
        // Getrennt von found/created: nur Haltungen, ohne Schaechte. Grundlage fuer das
        // Plausibilitaetstor vor der Veroeffentlichung.
        var erwarteteHaltungen   = 0;
        var bearbeiteteHaltungen = 0;
        QuellenwahlErgebnis? quellenprotokoll = null;

        var ct = ctx?.CancellationToken ?? System.Threading.CancellationToken.None;
        void Melde(int schritt, string name, string text)
            => ctx?.Progress?.Report(new ImportProgress(ImportFortschrittText.Phase(schritt, name), 0, 0, text));
        var parseContext = ctx is null ? null : new ImportRunContext(ct,
            new Fortschritt<ImportProgress>(p => ctx.Progress?.Report(p with
            {
                Phase = ImportFortschrittText.Phase(3, "Quelldaten")
            })), ctx.Log, ctx.DryRun, ctx.CollectionLock, ctx.FileStaging);

        // Der Abbruch wird an jeder Schrittgrenze geprueft, nicht nur tief im
        // Katasterabgleich. Bis 2026-09-05 lief ein abgebrochener Ein-Knopf-Import
        // vollstaendig zu Ende und kopierte dabei Gigabyte; ein Abbruch ist kein
        // Fehler und wird deshalb weitergeworfen, statt als Fehlermeldung zu enden.
        ct.ThrowIfCancellationRequested();

        // ------------------------------------------------------------------
        // Schritt 1: Projektstruktur sicherstellen
        // ------------------------------------------------------------------
        Melde(1, "Vorbereiten", "Projektordner vorbereiten …");
        try
        {
            _projectStructure.EnsureCreated(projectFolder);
        }
        catch (Exception ex)
        {
            fehlerbilanz.Melde("Projektstruktur", $"EnsureCreated fehlgeschlagen: {ex.Message}");
            messages.Add($"EnsureCreated fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 2: Restore-Point (best-effort)
        // ------------------------------------------------------------------
        Melde(1, "Vorbereiten", "Wiederherstellungspunkt anlegen …");
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
        Melde(1, "Vorbereiten", "Format erkennen …");
        KanalExportDetection det;
        try
        {
            det = _exportDetector.Detect(sourceFolder);
        }
        catch (Exception ex)
        {
            fehlerbilanz.Melde("Formaterkennung", $"Formaterkennung fehlgeschlagen: {ex.Message}");
            messages.Add($"Formaterkennung fehlgeschlagen: {ex.Message}");
            return new OneClickImportResult(
                KanalExportFormat.Unknown, found, created, updated, fehlerbilanz.Gesamt, conflictCount, messages)
            {
                Fehlerbilanz = fehlerbilanz.Bilanz()
            };
        }

        // Bei unbekanntem oder mehrdeutigem Format sofort abbrechen
        if (det.Format == KanalExportFormat.Unknown || det.Format == KanalExportFormat.Ambiguous)
        {
            messages.Add($"Import abgebrochen: {det.Reason}");
            return new OneClickImportResult(
                det.Format, found, created, updated, fehlerbilanz.Gesamt, conflictCount, messages)
            {
                Fehlerbilanz = fehlerbilanz.Bilanz()
            };
        }

        messages.AddRange(BuildSourceDecisionMessages(det));

        // ------------------------------------------------------------------
        // Schritt 4: Quelldateien archivieren
        // ------------------------------------------------------------------
        Melde(2, "Archivieren", "Quelldateien archivieren …");
        ct.ThrowIfCancellationRequested();
        try
        {
            var archiveResult = _sourceArchiver.Archive(
                sourceFolder,
                projectFolder,
                ctx?.FileStaging);
            messages.AddRange(archiveResult.Messages);
            messages.Add(
                $"Archiviert: {archiveResult.Copied} neu, {archiveResult.Reused} wiederverwendet.");

            var archivePdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var planResult = _planPdfImporter.ImportFromArchivedPdfFolder(
                archivePdfDir,
                projectFolder,
                ctx?.FileStaging);
            messages.AddRange(planResult.Messages);
            fehlerbilanz.Melde("Plan-PDF", planResult.Errors, planResult.Messages);
            if (planResult.Copied > 0 || planResult.Reused > 0 || planResult.Errors > 0)
            {
                messages.Add(
                    $"Pläne: {planResult.Copied} neu, {planResult.Reused} wiederverwendet, " +
                    $"{planResult.Errors} Fehler.");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            fehlerbilanz.Melde("Archivierung", $"Archivierung fehlgeschlagen: {ex.Message}");
            messages.Add($"Archivierung fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 5: Parsen
        // ------------------------------------------------------------------
        Melde(3, "Quelldaten", "Quelldaten einlesen …");
        ct.ThrowIfCancellationRequested();
        try
        {
            Result<ImportStats> parseResult;

            if (det.Format == KanalExportFormat.Ikas)
            {
                parseResult = _xtf.ImportXtfFiles(new[] { det.VsaKekXtfPath! }, project, parseContext);
            }
            else if (det.Format == KanalExportFormat.Ibak)
            {
                parseResult = _ibak is not null
                    ? _ibak.ImportIbakExport(sourceFolder, project, parseContext)
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
                    parseResult = _xtf.ImportXtfFiles(new[] { det.VsaKekXtfPath }, project, parseContext);
                else if (_kins is not null)
                    parseResult = _kins.ImportKinsExport(sourceFolder, project, parseContext);
                else
                    parseResult = Result<ImportStats>.Fail(
                        "KINS_SERVICE_MISSING", "KINS ohne XTF erkannt, aber kein KINS-Importservice verfuegbar.");
            }
            else // WinCan
            {
                parseResult = _winCan.ImportWinCanExport(sourceFolder, project, parseContext);
            }

            if (parseResult.Ok && parseResult.Value is not null)
            {
                found   += parseResult.Value.Found;
                created += parseResult.Value.Created;
                updated += parseResult.Value.Updated;
                fehlerbilanz.Melde("Quelle einlesen", parseResult.Value.Errors, parseResult.Value.Messages);
                // Fuer das Plausibilitaetstor: getrennte Haltungszahlen und das
                // Quellenprotokoll bis zum Ein-Knopf-Controller durchreichen.
                erwarteteHaltungen += parseResult.Value.ErwarteteHaltungen;
                bearbeiteteHaltungen += parseResult.Value.BearbeiteteHaltungen;
                quellenprotokoll ??= parseResult.Value.Quellenprotokoll;
                messages.AddRange(parseResult.Value.Messages);
            }
            else
            {
                fehlerbilanz.Melde("Quelle einlesen",
                    $"Parse fehlgeschlagen [{parseResult.ErrorCode}]: {parseResult.ErrorMessage}");
                messages.Add(
                    $"Parse fehlgeschlagen [{parseResult.ErrorCode}]: {parseResult.ErrorMessage}");
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            fehlerbilanz.Melde("Quelle einlesen", $"Parse-Ausnahme: {ex.Message}");
            messages.Add($"Parse-Ausnahme: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 5a: Ergaenzende XTF-Quellen desselben Ordners
        // ------------------------------------------------------------------
        // Gemessen am 2026-09-05 an allen drei IBAK-Projekten:
        //
        //   Goeschenen Unterdorfstrasse: IBAK-Weg 86 Haltungen, 0 Schaechte,
        //     2 Videolinks — die danebenliegende XTF traegt 90 Haltungen,
        //     71 Schaechte, 86 Videolinks und 425 Fotos.
        //   Erstfeld Jagdmatt: gar keine XTF; der IBAK-Weg ist die einzige Quelle.
        //   Buerglen Gosmergasse: nur eine Organisationsliste ohne Fachdaten.
        //
        // Der IBAK-Weg bleibt deshalb der Hauptweg — er wird ergaenzt, nicht ersetzt.
        // Die MergeEngine entscheidet je Feld (Xtf schlaegt Legacy, Handeingaben bleiben).
        //
        // Bewusst NUR fuer IBAK: Bei WinCan und IKAS ist kein solcher Bedarf gemessen,
        // und ein zusaetzlicher XTF-Lauf koennte dort gepruefte Werte verschieben.
        if (det.Format == KanalExportFormat.Ibak)
        {
            Melde(3, "Quelldaten", "Ergänzende XTF-Quellen prüfen …");
            try
            {
                var ergaenzend = FindeErgaenzendeXtfQuellen(sourceFolder, det);
                if (ergaenzend.Count > 0)
                {
                    var vorher = project.Data.Count;
                    var vorherSchaechte = project.SchaechteData.Count;
                    var xtfErgebnis = _xtf.ImportXtfFiles(ergaenzend, project, parseContext);
                    if (xtfErgebnis.Ok && xtfErgebnis.Value is not null)
                    {
                        messages.AddRange(xtfErgebnis.Value.Messages);
                        fehlerbilanz.Melde(
                            "Ergaenzende XTF-Quelle", xtfErgebnis.Value.Errors, xtfErgebnis.Value.Messages);
                        messages.Add(
                            $"Ergaenzende XTF-Quellen: {ergaenzend.Count} gelesen, "
                            + $"{project.Data.Count - vorher} Haltungen und "
                            + $"{project.SchaechteData.Count - vorherSchaechte} Schaechte dazugekommen.");
                    }
                    else
                    {
                        fehlerbilanz.Melde("Ergaenzende XTF-Quelle",
                            $"XTF-Ergaenzung fehlgeschlagen: {xtfErgebnis.ErrorMessage}");
                        messages.Add($"Ergaenzende XTF-Quelle fehlgeschlagen: {xtfErgebnis.ErrorMessage}");
                    }
                }
            }
            catch (OperationCanceledException) { throw; }
            catch (Exception ex)
            {
                fehlerbilanz.Melde("Ergaenzende XTF-Quelle", $"XTF-Ergaenzung fehlgeschlagen: {ex.Message}");
                messages.Add($"Ergaenzende XTF-Quelle fehlgeschlagen: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // Schritt 5b: KINS-Anreicherung (Namen, Timecodes/Laenge, DBF-Stammdaten)
        // ------------------------------------------------------------------
        if (det.Format == KanalExportFormat.Kins)
        {
            Melde(3, "Quelldaten", "KINS-Angaben ergänzen …");
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
                var dbfResult = _kinsDbfWhitelistEnricher.Apply(project, sourceFolder, ctx);
                messages.AddRange(dbfResult.Messages);
                messages.Add($"KINS-DBF: {dbfResult.HaltungsfelderGesetzt} Haltungsfelder, {dbfResult.SchaechteNeu} Schaechte neu, {dbfResult.SchaechteAktualisiert} aktualisiert.");
            }
            catch (Exception ex)
            {
                fehlerbilanz.Melde("KINS-Anreicherung", $"KINS-Anreicherung fehlgeschlagen: {ex.Message}");
                messages.Add($"KINS-Anreicherung fehlgeschlagen: {ex.Message}");
            }
        }

        // ------------------------------------------------------------------
        // ------------------------------------------------------------------
        // Schritt 5c: Haltungsnummern gegen den amtlichen Kataster abgleichen
        // ------------------------------------------------------------------
        // Eine Haltungsnummer ist "Schacht oben - Schacht unten". Diese Nummer entsteht
        // bereits beim Einlesen aus Protokoll bzw. Datenbank. Liegt zusaetzlich eine
        // amtliche SIA405-Datei vor, kann die dortige Bezeichnung abweichen, wenn ein
        // Schacht spaeter umnummeriert wurde. Dann gilt der Kataster.
        //
        // Haltungen, die der Kataster nicht kennt, behalten ausdruecklich ihre Nummer aus
        // dem Protokoll. Ohne Katasterdatei aendert sich gar nichts.
        //
        // Muss VOR der Verteilung laufen (Schritt 7): die Zielordner werden nach dem
        // Haltungsnamen benannt.
        Melde(3, "Quelldaten", "Haltungsnummern mit dem Kataster abgleichen …");
        try
        {
            var katasterDateien = FindeKatasterDateien(projectFolder, det.Sia405XtfPath);
            foreach (var katasterPfad in katasterDateien)
            {
                ct.ThrowIfCancellationRequested();
                var verzeichnis = Kataster.SiaKatasterXtfReader.Lies(katasterPfad);
                if (verzeichnis.Anzahl == 0)
                    continue;

                var abgleich = Application.UseCases.Import.Kataster
                    .HaltungsnummerKatasterAbgleich.Gleiche(project, verzeichnis);
                if (abgleich.Meldungen.Count > 0)
                {
                    messages.Add($"Kataster: {Path.GetFileName(katasterPfad)} ({verzeichnis.Anzahl} Haltungen)");
                    messages.AddRange(abgleich.Meldungen);
                }
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            // Der Abgleich ist eine Zusatzpruefung und darf den Import nie stoppen.
            messages.Add($"Katasterabgleich uebersprungen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 6: SIA405-Anreicherung (nur IKAS, nur wenn Pfad bekannt)
        // ------------------------------------------------------------------
        if (det.Format == KanalExportFormat.Ikas && det.Sia405XtfPath != null)
        {
            Melde(3, "Quelldaten", "SIA405-Angaben ergänzen …");
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
        Melde(4, "Medien", "Fotos je Haltung verteilen …");
        ct.ThrowIfCancellationRequested();
        try
        {
            // 7a) Fotos zentral gruppiert (Fotos\Haltungen\) — KEINE Videos/Original-PDFs und KEINE Schacht-
            //     Kopie (Schächte kommen in 7c als seiten-gruppierte Protokolle; Videos/Protokolle in 7b).
            var mediaResult = _mediaDistributor.Distribute(new ImportMediaDistributionRequest(
                projectFolder,
                project,
                Progress: new Fortschritt<ImportMediaDistributionProgress>(p => ctx?.Progress?.Report(
                    new ImportProgress(ImportFortschrittText.Phase(4, "Medien"), p.Processed, p.Total,
                        "Fotos je Haltung verteilen …", p.CurrentFile))),
                CancellationToken: ct,
                DryRun: false,
                // CollectionLock aus dem Lauf-Kontext: die Verteilung mutiert die
                // UI-gebundenen Collections und darf nicht mit einem frischen,
                // wirkungslosen Lock-Objekt laufen.
                CollectionLock: ctx?.CollectionLock ?? new object(),
                IncludeVideos: false,
                IncludePdfs: false,
                IncludeSchacht: false,
                FileStaging: ctx?.FileStaging));
            messages.AddRange(mediaResult.Messages);
            // Bis 2026-09-05 standen diese Fehler nur im Text und fehlten in der
            // Gesamtzahl — ein Fotofehler machte den Lauf trotzdem "fehlerfrei".
            fehlerbilanz.Melde("Fotoverteilung", mediaResult.Errors, mediaResult.Messages);

            // 7b) Video + ORIGINAL-Protokoll (NUR das maßgebliche PDF, ein PDF/Haltung) flach+datumsbenannt
            //     verteilen; beide relativ verlinkt (PDF_Path = Original). Das eigene _E-Protokoll wird hier
            //     NICHT erzeugt — das macht der ProtocolRegenerationService („Protokoll neu generieren").
            //     KINS: Der Seiten-Split laeuft auf dem expliziten Gesamtprotokoll aus der Quelle
            //     (*_Protokoll.pdf) — die Auto-Wahl "groesste Archiv-PDF" traefe sonst Plaene/fremde PDFs.
            Melde(5, "Haltungsprotokolle", "Videos und Protokolle zuordnen …");
            var kinsGesamtprotokoll = det.Format == KanalExportFormat.Kins
                ? _kinsGesamtprotokollLocator.Finde(sourceFolder)
                : null;
            var archivedPdfDir = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.PdfDir);
            var recordCountBeforeDistribution = project.Data.Count;

            // Name-basierte Protokoll-Verteilung zuerst (narrensicher, Dateiname-basiert).
            // CollectionLock aus dem Lauf-Kontext mitgeben: das Anlegen neuer Schächte läuft ggf. auf
            // einem Hintergrund-Thread und mutiert die UI-gebundene SchaechteData-Collection.
            var nameBased = _protocolDistributor?.Distribute(
                project,
                projectFolder,
                archivedPdfDir,
                ctx?.CollectionLock,
                ctx?.FileStaging);
            if (nameBased is not null)
            {
                messages.Add($"Protokolle name-basiert verteilt: {nameBased.HaltungProtokolle} Haltungen, {nameBased.SchachtProtokolle} Schächte, {nameBased.SchaechteAngelegt} Schächte angelegt.");
                foreach (var nz in nameBased.NichtZugeordnet)
                    messages.Add($"Protokoll nicht zugeordnet: {nz}");

                // ProtocolDistributionReport.Meldungen sind die Kopierfehler je Datei.
                // Sie wurden bis 2026-09-05 gesammelt, aber nie gelesen: Ein Protokoll
                // konnte still verloren gehen, waehrend der Bericht "0 Fehler" meldete.
                foreach (var meldung in nameBased.Meldungen)
                    messages.Add($"Protokoll nicht kopiert: {meldung}");
                fehlerbilanz.Melde(
                    "Name-basierte Protokollverteilung",
                    nameBased.Meldungen.Count,
                    nameBased.Meldungen);
            }

            // Der Sammelprotokoll-Split laeuft IMMER, wenn es ueberhaupt ein Protokoll gibt.
            //
            // Bis 2026-09-05 schaltete ein einziger name-basierter Treffer ihn global ab:
            // Ein Ordner mit einem Einzelprotokoll fuer Haltung A und einem Sammelprotokoll
            // fuer B und C liess B und C leer. Der Schutz gegen doppelte Verknuepfungen
            // liegt jetzt dort, wo er hingehoert — eine schon versorgte Haltung behaelt in
            // KanalImportDistributionService ihren Verweis aus dem Einzelprotokoll.
            Melde(5, "Haltungsprotokolle", "Sammelprotokolle aufteilen und Videos verteilen …");
            var distResult = _kanalDistributor.Distribute(
                project, projectFolder, archivedPdfDir, sourceFolder,
                splitPdf: det.Format != KanalExportFormat.Kins || kinsGesamtprotokoll is not null,
                primaryProtocolPdf: kinsGesamtprotokoll,
                fileStaging: ctx?.FileStaging);
            messages.AddRange(distResult.Messages);
            fehlerbilanz.Melde("Video- und Protokollverteilung", distResult.Errors, distResult.Messages);
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
            Melde(5, "Haltungsprotokolle", "Dichtheitsprotokolle verteilen …");
            var dpResult = _dichtheitDistributor.Distribute(
                project,
                projectFolder,
                sourceFolder,
                _kiSchiedsrichter,
                ctx?.FileStaging);
            messages.AddRange(dpResult.Messages);
            if (dpResult.Verteilt > 0 || dpResult.NichtZugeordnet > 0 || dpResult.Uebersprungen > 0)
                messages.Add($"Dichtheitspruefung: {dpResult.Verteilt} Protokolle verteilt, {dpResult.NichtZugeordnet} nicht zugeordnet, {dpResult.Uebersprungen} bereits vorhanden.");

            // 7d) Schachtprotokolle aus dem Archiv verteilen.
            //
            // Bis 2026-09-05 blieb dieser Schritt dem manuellen Befehl „Schacht Verteilen"
            // ueberlassen — ein vollstaendiger Projektimport liess die Schaechte also leer.
            // Es laeuft derselbe Dienst wie beim manuellen Weg, dieselbe Staging-Sitzung
            // und dieselbe Verknuepfungsregel; ein zweiter Splitter entsteht nicht.
            //
            // Die Sorge dahinter bleibt gueltig und ist jetzt in der Regel abgebildet:
            // Es wird KEIN Schacht angelegt, und ein vorhandener Verweis wird nicht
            // ersetzt. Ein Haltungsprotokoll faellt beim Schacht-Parser durch und wird
            // gemeldet, nicht an beide Endschaechte gehaengt.
            Melde(6, "Schachtprotokolle", "Schachtprotokolle prüfen und verteilen …");
            var schachtMeldungen = VerteileSchachtprotokolle(
                project, projectFolder, archivedPdfDir, ctx?.FileStaging, fehlerbilanz,
                new Fortschritt<ShaftDistributionProgress>(p => ctx?.Progress?.Report(
                    new ImportProgress(ImportFortschrittText.Phase(6, "Schachtprotokolle"), p.Processed,
                        p.Total, "Schachtprotokolle prüfen und verteilen …", p.CurrentFile))));
            messages.AddRange(schachtMeldungen);

            messages.Add(
                $"Verteilung: {mediaResult.FilesCopied} Fotos/Dateien, {distResult.VideosDistributed} Videos, " +
                $"{distResult.OriginalProtocolsDistributed} Original-Protokolle, " +
                $"{mediaResult.Errors + distResult.Errors} Fehler.");
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            fehlerbilanz.Melde("Medienverteilung", $"Medienverteilung fehlgeschlagen: {ex.Message}");
            messages.Add($"Medienverteilung fehlgeschlagen: {ex.Message}");
        }

        // ------------------------------------------------------------------
        // Schritt 8: Projekt als geaendert markieren
        // ------------------------------------------------------------------
        Melde(7, "Abschliessen", "Projektdateien prüfen …");
        ctx?.CancellationToken.ThrowIfCancellationRequested();
        project.Dirty = true;

        if (found == 0 && HasDataSourceSignal(det, sourceFolder))
        {
            messages.Add(
                "WARNUNG: 0 Haltungen importiert, obwohl Datenquellen erkannt wurden. " +
                "Bitte Report pruefen; die Herstellerquelle wurde vermutlich nicht gelesen oder enthaelt ein unbekanntes Schema.");
        }

        // Bestandsaufnahme zum Schluss: was ist wirklich angekommen. Bewusst KEINE
        // erfundene Sollzahl — die Bewertung bleibt beim Menschen.
        var dateipruefung = ImportProjektdateiPruefer.Pruefe(project, projectFolder, ctx?.FileStaging);
        var bestand = dateipruefung.Bestand;
        fehlerbilanz.Melde("Abschliessende Dateiprüfung", dateipruefung.Fehler.Count, dateipruefung.Fehler);
        messages.AddRange(dateipruefung.Fehler);
        messages.AddRange(bestand.Berichtszeilen());

        return new OneClickImportResult(
            det.Format, found, created, updated, fehlerbilanz.Gesamt, conflictCount, messages)
        {
            ErwarteteHaltungen = erwarteteHaltungen,
            BearbeiteteHaltungen = bearbeiteteHaltungen,
            Quellenprotokoll = quellenprotokoll,
            Fehlerbilanz = fehlerbilanz.Bilanz(),
            Bestand = bestand
        };
    }

    /// <summary>
    /// Verteilt die Schachtprotokolle des Archivs und verknuepft sie mit den Schaechten.
    ///
    /// Verwendet denselben <see cref="IShaftDistributionService"/> wie der manuelle Weg
    /// „Schacht Verteilen" und dieselbe Verknuepfungsregel
    /// (<see cref="SchachtProtokollVerknuepfung"/>). Ein Fehler hier darf den Import
    /// nicht abbrechen — die Haltungen sind zu diesem Zeitpunkt bereits versorgt.
    /// </summary>
    private IReadOnlyList<string> VerteileSchachtprotokolle(
        Project project,
        string projectFolder,
        string archivedPdfDir,
        IImportFileStagingSession? fileStaging,
        ImportFehlerbilanzSammler fehlerbilanz,
        IProgress<ShaftDistributionProgress>? progress)
    {
        var meldungen = new List<string>();

        try
        {
            var ergebnis = _shaftDistribution.Distribute(new ShaftDistributionRequest(
                Project: project,
                DestinationFolder: Path.Combine(projectFolder, ProjectStructure.SchaechteVerteilt),
                PdfFiles: null,
                PdfSourceFolder: archivedPdfDir,
                Progress: progress,
                FileStaging: fileStaging));

            var erfolgreich = ergebnis.Items.Where(i => i.Success).ToList();
            if (erfolgreich.Count == 0 && ergebnis.Items.Count == 0)
                return meldungen;

            var verknuepfung = SchachtProtokollVerknuepfung.Verknuepfe(
                erfolgreich
                    .Where(i => !string.IsNullOrWhiteSpace(i.TargetPdfPath)
                                && !string.IsNullOrWhiteSpace(i.ShaftFolder))
                    .Select(i => (i.TargetPdfPath!, i.ShaftFolder!, i.SourcePdfPath))
                    .ToList(),
                project,
                projectFolder);

            meldungen.Add(
                $"Schachtprotokolle: {erfolgreich.Count} verteilt, "
                + $"{verknuepfung.Verknuepft} mit einem Schacht verknuepft.");
            meldungen.AddRange(verknuepfung.Meldungen);

            // Ein Protokollteil, der nicht abgelegt werden konnte, MUSS im Bericht stehen.
            // Ein reines "Parse failed" auf einer Haltungs-PDF ist dagegen erwartet und
            // wird nicht als Schachtfehler gemeldet.
            foreach (var fehlgeschlagen in ergebnis.Items.Where(i => !i.Success))
            {
                if (fehlgeschlagen.Message.StartsWith("Parse failed", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                meldungen.Add(
                    $"Schachtprotokoll {Path.GetFileName(fehlgeschlagen.SourcePdfPath)} "
                    + $"nicht verteilt: {fehlgeschlagen.Message}");
                fehlerbilanz.Melde("Schachtprotokolle", meldungen[^1]);
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            meldungen.Add($"Schachtprotokolle nicht verteilt: {ex.Message}");
            fehlerbilanz.Melde("Schachtprotokolle", meldungen[^1]);
        }

        return meldungen;
    }

    /// <summary>
    /// Die XTF-Dateien eines Ordners, die zusaetzlich zur Hauptquelle Fachdaten tragen.
    ///
    /// Es gelten dieselben Regeln wie bei der Erkennung: Nur Dateien mit Inspektions-
    /// oder Katasterinhalt, mehrere Exporte derselben Zone werden auf den
    /// inhaltsreichsten zusammengefasst, und eine bereits als Hauptquelle gelesene Datei
    /// wird nicht ein zweites Mal verarbeitet.
    ///
    /// Eine reine Organisationsliste — in Buerglen 2206 Eintraege ohne einen einzigen
    /// Fachdatensatz — faellt dabei heraus.
    /// </summary>
    private static IReadOnlyList<string> FindeErgaenzendeXtfQuellen(
        string sourceFolder,
        KanalExportDetection det)
    {
        // Nur was der Hauptweg wirklich gelesen hat, gilt als erledigt. Die Erkennung
        // FINDET eine SIA405-Datei auch im IBAK-Ordner — gelesen wird sie dort nicht.
        // Sie deshalb auszuschliessen kostete in Goeschenen Unterdorfstrasse 592
        // Bauwerke, darunter alle Normschaechte (gemessen 2026-09-05).
        var schonGelesen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var hauptwegLiestXtf = det.Format is KanalExportFormat.Ikas or KanalExportFormat.Kins;
        if (hauptwegLiestXtf)
        {
            if (!string.IsNullOrWhiteSpace(det.VsaKekXtfPath))
                schonGelesen.Add(det.VsaKekXtfPath!);
            if (!string.IsNullOrWhiteSpace(det.Sia405XtfPath))
                schonGelesen.Add(det.Sia405XtfPath!);
        }

        // Bewusst FachlicheXtfQuellen und nicht die Kandidaten-Markierung: Letztere sagt
        // "von diesem Importweg verwendet" — und der IBAK-Weg verwendet gar keine XTF.
        return det.FachlicheXtfQuellen
            .Where(pfad => !schonGelesen.Contains(pfad))
            .ToList();
    }

    /// <summary>
    /// Sammelt moegliche amtliche Katasterdateien: die vom Erkenner gefundene SIA405-Datei
    /// und alle XTF im Importordner des Projekts. Dateien ohne Haltungsobjekte (etwa die
    /// VSA-KEK-Exporte von WinCan) liefern ein leeres Verzeichnis und bleiben wirkungslos.
    /// </summary>
    private static IReadOnlyList<string> FindeKatasterDateien(string projectFolder, string? sia405AusQuelle)
    {
        var pfade = new List<string>();
        if (!string.IsNullOrWhiteSpace(sia405AusQuelle) && File.Exists(sia405AusQuelle))
            pfade.Add(sia405AusQuelle!);

        try
        {
            var xtfOrdner = ProjectStructure.ImportdateienDir(projectFolder, ProjectStructure.XtfDir);
            if (Directory.Exists(xtfOrdner))
            {
                foreach (var datei in SafeFileEnumeration.EnumerateFilesSafe(xtfOrdner, "*.xtf", recursive: false))
                {
                    if (!pfade.Contains(datei, StringComparer.OrdinalIgnoreCase))
                        pfade.Add(datei);
                }
            }
        }
        catch
        {
            // Ein unlesbarer Importordner darf den Import nicht stoppen.
        }

        return pfade;
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

    // Synchron weiterreichen: Nur der aeussere UI-Kanal wechselt den Thread.
    private sealed class Fortschritt<T>(Action<T> melden) : IProgress<T>
    {
        public void Report(T value) => melden(value);
    }

    private static bool AnyFile(string root, string pattern)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(root) || !Directory.Exists(root))
                return false;

            return SafeFileEnumeration
                .EnumerateFilesSafe(root, pattern, recursive: true)
                .Any();
        }
        catch
        {
            return false;
        }
    }
}
