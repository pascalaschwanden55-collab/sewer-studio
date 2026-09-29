using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.Projects;
using AuswertungPro.Next.Domain.Models;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>
/// Sammelstellen des Laufs, in die die Medienphase sofort schreibt. Sofort, nicht am Ende:
/// Wirft ein spaeterer Teilschritt, bleibt alles bis dahin Gemeldete und Gezaehlte erhalten.
/// </summary>
internal sealed record ImportMediaPhaseSinks(
    List<string> Messages,
    ImportFehlerbilanzSammler Fehlerbilanz,
    Action<int> HaltungenAusPdfFallback);

/// <summary>
/// Medienphase des Ein-Knopf-Imports (Anzeigeschritte 4 bis 6): Fotos, Namensprotokolle,
/// Sammelprotokolle mit Videos, Dichtheitsprotokolle, Schachtprotokolle — in dieser
/// Reihenfolge. Die gemeinsame Fehlergrenze der Phase liegt bewusst beim Aufrufer
/// (<see cref="ProjectImportOrchestrator"/>): Ein Fehler in einem Teilschritt beendet die
/// ganze Phase, ausser beim Schachtschritt, der seine Fehler selbst meldet.
/// </summary>
internal sealed class ImportMediaPhase
{
    private readonly IImportMediaDistributionService _mediaDistributor;
    private readonly INameBasedProtocolDistributor? _protocolDistributor;
    private readonly IKanalImportDistributor _kanalDistributor;
    private readonly IDichtheitImportDistributor _dichtheitDistributor;
    private readonly IShaftDistributionService _shaftDistribution;
    private readonly IKinsGesamtprotokollLocator _kinsGesamtprotokollLocator;
    private readonly PdfKiSchiedsrichter? _kiSchiedsrichter;

    public ImportMediaPhase(
        IImportMediaDistributionService mediaDistributor,
        INameBasedProtocolDistributor? protocolDistributor,
        IKanalImportDistributor kanalDistributor,
        IDichtheitImportDistributor dichtheitDistributor,
        IShaftDistributionService shaftDistribution,
        IKinsGesamtprotokollLocator kinsGesamtprotokollLocator,
        PdfKiSchiedsrichter? kiSchiedsrichter)
    {
        _mediaDistributor = mediaDistributor;
        _protocolDistributor = protocolDistributor;
        _kanalDistributor = kanalDistributor;
        _dichtheitDistributor = dichtheitDistributor;
        _shaftDistribution = shaftDistribution;
        _kinsGesamtprotokollLocator = kinsGesamtprotokollLocator;
        _kiSchiedsrichter = kiSchiedsrichter;
    }

    public void Run(
        Project project,
        string projectFolder,
        string sourceFolder,
        KanalExportFormat format,
        ImportRunContext? ctx,
        ImportMediaPhaseSinks sinks)
    {
        var messages = sinks.Messages;
        var fehlerbilanz = sinks.Fehlerbilanz;
        var fehlerVorDerPhase = fehlerbilanz.Gesamt;
        var ct = ctx?.CancellationToken ?? System.Threading.CancellationToken.None;
        void Melde(int schritt, string name, string text)
            => ctx?.Progress?.Report(new ImportProgress(ImportFortschrittText.Phase(schritt, name), 0, 0, text));

        // 7a) Fotos zentral gruppiert (Fotos\Haltungen\) — KEINE Videos/Original-PDFs und KEINE Schacht-
        //     Kopie (Schächte kommen in 7d als seiten-gruppierte Protokolle; Videos/Protokolle in 7b).
        var mediaResult = DistributePhotos(projectFolder, project, ctx, ct);
        messages.AddRange(mediaResult.Messages);
        fehlerbilanz.Melde("Fotoverteilung", mediaResult.Errors, mediaResult.Messages);

        // 7b) Video + ORIGINAL-Protokoll (NUR das maßgebliche PDF, ein PDF/Haltung) flach+datumsbenannt
        //     verteilen; beide relativ verlinkt (PDF_Path = Original). Das eigene _E-Protokoll wird hier
        //     NICHT erzeugt — das macht der ProtocolRegenerationService („Protokoll neu generieren").
        //     KINS: Der Seiten-Split laeuft auf dem expliziten Gesamtprotokoll aus der Quelle
        //     (*_Protokoll.pdf) — die Auto-Wahl "groesste Archiv-PDF" traefe sonst Plaene/fremde PDFs.
        Melde(5, "Haltungsprotokolle", "Videos und Protokolle zuordnen …");
        var kinsGesamtprotokoll = format == KanalExportFormat.Kins
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
            splitPdf: format != KanalExportFormat.Kins || kinsGesamtprotokoll is not null,
            primaryProtocolPdf: kinsGesamtprotokoll,
            fileStaging: ctx?.FileStaging);
        messages.AddRange(distResult.Messages);
        fehlerbilanz.Melde("Video- und Protokollverteilung", distResult.Errors, distResult.Messages);
        var recordsCreatedByDistribution = Math.Max(0, project.Data.Count - recordCountBeforeDistribution);
        if (recordsCreatedByDistribution > 0)
        {
            sinks.HaltungenAusPdfFallback(recordsCreatedByDistribution);
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
        if (dpResult.FehlerListe.Count > 0)
            fehlerbilanz.Melde("Dichtheitsverteilung", dpResult.FehlerListe.Count, dpResult.FehlerListe);
        if (dpResult.Verteilt > 0 || dpResult.NichtZugeordnet > 0 || dpResult.Uebersprungen > 0
            || dpResult.FehlerListe.Count > 0)
        {
            messages.Add($"Dichtheitspruefung: {dpResult.Verteilt} Protokolle verteilt, {dpResult.NichtZugeordnet} nicht zugeordnet, {dpResult.Uebersprungen} bereits vorhanden, {dpResult.FehlerListe.Count} Fehler.");
        }

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
            new SynchronerFortschritt<ShaftDistributionProgress>(p => ctx?.Progress?.Report(
                new ImportProgress(ImportFortschrittText.Phase(6, "Schachtprotokolle"), p.Processed,
                    p.Total, "Schachtprotokolle prüfen und verteilen …", p.CurrentFile))));
        messages.AddRange(schachtMeldungen);

        // Die Fehlerzahl kommt aus der Fehlerbilanz dieser Phase, nicht nur aus Foto und Kanal:
        // sonst stand «0 Fehler» direkt unter einem gemeldeten Schacht- oder Protokollfehler.
        messages.Add(
            $"Verteilung: {mediaResult.FilesCopied} Fotos/Dateien, {distResult.VideosDistributed} Videos, " +
            $"{distResult.OriginalProtocolsDistributed} Original-Protokolle, " +
            $"{fehlerbilanz.Gesamt - fehlerVorDerPhase} Fehler.");
    }

    /// <summary>
    /// Hält den Fotoauftrag einschliesslich UI-CollectionLock und Fortschritt
    /// getrennt von der Bilanzierung.
    /// </summary>
    private ImportMediaDistributionResult DistributePhotos(
        string projectFolder,
        Project project,
        ImportRunContext? ctx,
        System.Threading.CancellationToken ct)
    {
        return _mediaDistributor.Distribute(new ImportMediaDistributionRequest(
            projectFolder,
            project,
            Progress: new SynchronerFortschritt<ImportMediaDistributionProgress>(p => ctx?.Progress?.Report(
                new ImportProgress(ImportFortschrittText.Phase(4, "Medien"), p.Processed, p.Total,
                    "Fotos je Haltung verteilen …", p.CurrentFile))),
            CancellationToken: ct,
            DryRun: false,
            // Die Verteilung mutiert UI-gebundene Collections: vorhandenen Lauf-Lock verwenden.
            CollectionLock: ctx?.CollectionLock ?? new object(),
            IncludeVideos: false,
            IncludePdfs: false,
            IncludeSchacht: false,
            FileStaging: ctx?.FileStaging));
    }

    internal enum SchachtFehlerArt
    {
        /// <summary>Weder Schachtnummer noch Datum: gehoert nicht in die Schachtverteilung.</summary>
        KeinSchachtprotokoll,
        /// <summary>Ohne Textebene (Scan): kann ein Schacht- oder Haltungsprotokoll sein.</summary>
        Hinweis,
        Fehler
    }

    /// <summary>
    /// Ordnet ein nicht verteiltes Schachtergebnis ein. Vorher wurde jedes «Parse failed»
    /// verworfen; ein gescanntes oder datumsloses Schachtprotokoll verschwand dadurch spurlos.
    /// Eindeutig fremde Textdokumente (Haltungsprotokolle) sortiert die Schachtverteilung schon
    /// vorher aus (ShaftPdfRelevance).
    /// </summary>
    internal static SchachtFehlerArt EinordnenSchachtFehler(string meldung)
    {
        if (!meldung.StartsWith("Parse failed", StringComparison.OrdinalIgnoreCase))
            return SchachtFehlerArt.Fehler;
        if (meldung.Contains("Textebene", StringComparison.OrdinalIgnoreCase))
            return SchachtFehlerArt.Hinweis;
        if (meldung.Contains("Schachtnummer und Datum nicht gefunden", StringComparison.OrdinalIgnoreCase))
            return SchachtFehlerArt.KeinSchachtprotokoll;
        // «Datum nicht gefunden» heisst: Schachtnummer erkannt, also ein Schachtprotokoll mit Problem.
        if (meldung.Contains("Datum nicht gefunden", StringComparison.OrdinalIgnoreCase))
            return SchachtFehlerArt.Fehler;
        return SchachtFehlerArt.KeinSchachtprotokoll;
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
                + $"{verknuepfung.Verknuepft} mit einem Schacht verknüpft.");
            meldungen.AddRange(verknuepfung.Meldungen);

            // Ein Protokollteil, der nicht abgelegt werden konnte, MUSS im Bericht stehen.
            // Ein reines "Parse failed" auf einer Haltungs-PDF ist dagegen erwartet und
            // wird nicht als Schachtfehler gemeldet.
            foreach (var fehlgeschlagen in ergebnis.Items.Where(i => !i.Success))
            {
                var art = EinordnenSchachtFehler(fehlgeschlagen.Message);
                if (art == SchachtFehlerArt.KeinSchachtprotokoll)
                    continue;

                if (art == SchachtFehlerArt.Hinweis)
                {
                    meldungen.Add(
                        $"Hinweis: {Path.GetFileName(fehlgeschlagen.SourcePdfPath)} hat keine lesbare Textebene "
                        + "und wurde nicht als Schachtprotokoll verteilt – bitte prüfen, ob es eines ist.");
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
}
