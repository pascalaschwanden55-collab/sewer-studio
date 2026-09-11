using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Application.UseCases.Import.Quellen;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;
using FileContentComparer = AuswertungPro.Next.Application.Common.FileContentComparer;

namespace AuswertungPro.Next.Infrastructure.Import;

/// <summary>Welche Art Begleitprotokoll der Sanierung eine Quell-PDF ist.</summary>
internal enum SanierungsprotokollArt
{
    Dichtheitspruefung,
    Aushaertung
}

/// <summary>Eine erkannte Quell-PDF samt dem Text, der die Erkennung belegt.</summary>
internal sealed record Sanierungsprotokoll(string Pfad, SanierungsprotokollArt Art, string? Text)
{
    /// <summary>Kuerzel im Zieldateinamen: <c>20260817_60248-60247_DP.pdf</c>.</summary>
    internal string Kuerzel => Art == SanierungsprotokollArt.Dichtheitspruefung ? "DP" : "AH";

    internal string Bezeichnung => Art == SanierungsprotokollArt.Dichtheitspruefung
        ? "Dichtheitspruefung"
        : "Aushaerteprotokoll";
}

/// <summary>
/// Verteilt beim Ein-Knopf-Import die Begleitprotokolle der Sanierung aus der Quelle in
/// die Haltungsordner — Dateiname &lt;JJJJMMTT&gt;_&lt;Haltung&gt;_DP.pdf beziehungsweise
/// _AH.pdf. Kandidaten sind PDFs, deren Inhalt deterministisch als Dichtheitspruefung
/// oder Aushaerteprotokoll erkannt wird; bei einem reinen Scan liefert
/// <see cref="PdfDokumenttext"/> den Text per OCR. Die KI-Zweitmeinung bleibt auf
/// DP-/Dichtheits-Ordner begrenzt, damit normale Dokumentenordner nicht breit geraten
/// werden.
///
/// Zugeordnet wird zuerst ueber die Haltungsbezeichnung, die das Dokument selbst nennt
/// (<see cref="SanierungsprotokollZuordnung"/>) — nur dieser Weg trifft eine
/// Sammelpruefung ueber mehrere Haltungen und ein Aushaerteprotokoll ohne Schachtangabe.
/// Bleibt eine Dichtheitspruefung offen, folgt unveraendert der bestehende Weg ueber das
/// Schachtpaar im Inhalt
/// (<see cref="HoldingFolderDistributor.DistributeDichtheitFiles"/>).
///
/// Kanalfernseh-, DP- und Aushaerteprotokolle liegen damit gemeinsam im
/// Haltungen_Verteilt-Ordner.
/// </summary>
public sealed class DichtheitImportDistributionService : IDichtheitImportDistributor
{
    // Ordnersegment weist auf Dichtheitspruefung hin: "DP" als eigenes Wort
    // (048473_DP_Gross) oder "Dichtheit..." im Namen.
    private static readonly Regex DpOrdnerRegex = new(
        @"(^|[_\-\s])DP($|[_\-\s])|Dichtheit",
        RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    /// <summary>Zieldateien, die dieser Verteiler selbst angelegt hat.</summary>
    private static readonly string[] ZieldateiMuster = ["*_DP*.pdf", "*_AH*.pdf"];

    public DichtheitImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        PdfKiSchiedsrichter? ki = null)
        => Distribute(project, projectFolder, sourceFolder, ki, fileStaging: null);

    public DichtheitImportDistributor.Result Distribute(
        Project project,
        string projectFolder,
        string sourceFolder,
        PdfKiSchiedsrichter? ki,
        IImportFileStagingSession? fileStaging)
    {
        if (fileStaging is not null
            && !Path.GetFullPath(fileStaging.ProjectRoot)
                .Equals(Path.GetFullPath(projectFolder), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "Datei-Staging und Dichtheits-Verteilung gehoeren nicht zum selben Projekt.");
        }

        var messages = new List<string>();
        var kandidaten = new List<Sanierungsprotokoll>(FindeKandidaten(sourceFolder, out var namensHinweise));
        messages.AddRange(namensHinweise);

        // R4: KI-Zweitmeinung fuer DP-Ordner-PDFs, die die deterministische
        // Typ-Erkennung NICHT sicher zuordnen konnte (nur Vorschlag, im Report gekennzeichnet).
        if (ki is not null)
        {
            foreach (var unsicher in FindeUnsichereKandidaten(sourceFolder))
            {
                var klassifikation = FrageKi(ki, unsicher);
                if (klassifikation?.Typ == PdfDokumentTyp.Dichtheitspruefung)
                {
                    kandidaten.Add(new Sanierungsprotokoll(
                        unsicher,
                        SanierungsprotokollArt.Dichtheitspruefung,
                        PdfDokumenttext.LiesKopf(unsicher, maxPages: 6)));
                    messages.Add($"Per KI als Dichtheitspruefung klassifiziert: {Path.GetFileName(unsicher)}");
                }
            }
        }

        if (kandidaten.Count == 0)
            return new DichtheitImportDistributor.Result(0, namensHinweise.Count, 0, messages);

        var zielRoot = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);

        // Idempotenz-Guard: bereits verteilte, bytegleiche DP-Protokolle
        // nicht erneut kopieren — DistributeDichtheitFiles wuerde sonst bei jedem
        // Lauf _01-Duplikate anlegen.
        // Gleiche Dateigroesse allein ist kein Inhaltsbeweis.
        var vorhandeneHashes = LeseVorhandeneDpHashes(zielRoot, fileStaging);
        var neue = new List<Sanierungsprotokoll>();
        var uebersprungen = 0;
        foreach (var kandidat in kandidaten)
        {
            string hash;
            try
            {
                hash = VerifiedImportFileCopy.ComputeSha256(kandidat.Pfad);
            }
            catch (Exception ex)
            {
                // Best effort: eine unlesbare Datei darf die restlichen DP-Kandidaten nicht blockieren.
                AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                    $"[DichtheitImport] Kandidat uebersprungen, SHA-256 nicht lesbar: {kandidat.Pfad}: {ex.Message}");
                continue;
            }

            if (vorhandeneHashes.Contains(hash))
                uebersprungen++;
            else
                neue.Add(kandidat);
        }

        if (neue.Count == 0)
            return new DichtheitImportDistributor.Result(0, namensHinweise.Count, uebersprungen, messages);

        // Zuerst der genaue Weg ueber die Haltungsbezeichnung des Dokuments. Er ist der
        // einzige, der eine Sammelpruefung ueber mehrere Haltungen richtig ablegt und
        // der ein Aushaerteprotokoll ueberhaupt zuordnen kann — dort steht kein Schacht.
        var (perBezeichnung, offen, offeneGruende) = VerteileUeberBezeichnung(
            neue, project, projectFolder, zielRoot, fileStaging, messages);

        if (offen.Count == 0)
        {
            return new DichtheitImportDistributor.Result(
                perBezeichnung, namensHinweise.Count, uebersprungen, messages);
        }

        // Rueckfall: der bestehende Weg ueber das Schachtpaar im Dokument (KIT-Bauinspekt
        // und andere Pruefberichte). Ein Aushaerteprotokoll kennt er nicht.
        var (fuerSchachtpaar, ohneWeg) = TeileNachWeg(offen);
        foreach (var protokoll in ohneWeg)
        {
            messages.Add(
                $"{protokoll.Bezeichnung} nicht zugeordnet: {Path.GetFileName(protokoll.Pfad)} — "
                + "das Dokument nennt keine Haltung dieses Projekts.");

            // Der genaue Grund gehoert dazu: "H73 steht nur im Dateinamen, nicht im
            // Dokument" sagt Pascal, wo er nachsehen muss.
            if (offeneGruende.TryGetValue(protokoll.Pfad, out var gruende))
                messages.AddRange(gruende);
        }

        if (fuerSchachtpaar.Count == 0)
        {
            return new DichtheitImportDistributor.Result(
                perBezeichnung, ohneWeg.Count + namensHinweise.Count, uebersprungen, messages);
        }

        var results = DistributeCandidates(
            fuerSchachtpaar.Select(p => p.Pfad).ToList(),
            zielRoot,
            project,
            fileStaging);

        var verteilt = results.Count(r => r.Success);
        var nichtZugeordnet = 0;
        foreach (var r in results.Where(r => !r.Success))
        {
            // R4: Wenn der Inhalt-Parser das Schachtpaar nicht fand, darf die KI
            // einen Vorschlag machen — Zuordnung wird im Report als "per KI" gekennzeichnet.
            if (ki is not null && !string.IsNullOrWhiteSpace(r.SourcePdfPath)
                && VerteilePerKi(
                    ki,
                    r.SourcePdfPath!,
                    projectFolder,
                    zielRoot,
                    messages,
                    fileStaging))
            {
                verteilt++;
                continue;
            }

            nichtZugeordnet++;
            messages.Add($"DP nicht zugeordnet: {Path.GetFileName(r.SourcePdfPath ?? "?")} — {r.Message}");
        }

        return new DichtheitImportDistributor.Result(
            verteilt + perBezeichnung,
            nichtZugeordnet + ohneWeg.Count + namensHinweise.Count,
            uebersprungen,
            messages);
    }

    /// <summary>
    /// Legt jedes Protokoll in die Haltungsordner, die sein Dokument selbst nennt.
    /// Liefert die Zahl der abgelegten Dateien und alles, was ueber diesen Weg offen blieb.
    /// </summary>
    private static (int Verteilt,
                    IReadOnlyList<Sanierungsprotokoll> Offen,
                    IReadOnlyDictionary<string, IReadOnlyList<string>> Gruende)
        VerteileUeberBezeichnung(
        IReadOnlyList<Sanierungsprotokoll> protokolle,
        Project project,
        string projectFolder,
        string zielRoot,
        IImportFileStagingSession? fileStaging,
        List<string> messages)
    {
        var haltungen = project.Data
            .Select(r => new SanierungsprotokollHaltung(
                r.GetFieldValue(FieldKeys.HoldingName)?.Trim() ?? "",
                r.ImportBezeichnung))
            .Where(h => !string.IsNullOrWhiteSpace(h.Haltung))
            .ToList();

        var verteilt = 0;
        var offen = new List<Sanierungsprotokoll>();
        var gruende = new Dictionary<string, IReadOnlyList<string>>(StringComparer.OrdinalIgnoreCase);

        foreach (var protokoll in protokolle)
        {
            var befund = SanierungsprotokollZuordnung.Ordne(protokoll.Pfad, protokoll.Text, haltungen);
            if (befund.Ziele.Count == 0)
            {
                offen.Add(protokoll);
                gruende[protokoll.Pfad] = befund.Hinweise;
                continue;
            }

            // Ein Stolperstein bei einer Haltung darf die uebrigen nicht mitreissen.
            messages.AddRange(befund.Hinweise);

            var stamp = Datumsstempel(protokoll.Text);
            var abgelegt = new List<string>();
            foreach (var ziel in befund.Ziele)
            {
                var pfad = LegeInHaltungsordner(
                    protokoll, ziel.Haltung, stamp, projectFolder, zielRoot, fileStaging, messages);
                if (pfad is null)
                    continue;

                verteilt++;
                abgelegt.Add(ziel.Haltung);
            }

            if (abgelegt.Count == 0)
            {
                offen.Add(protokoll);
                gruende[protokoll.Pfad] = befund.Hinweise;
                continue;
            }

            messages.Add(abgelegt.Count == 1
                ? $"{protokoll.Bezeichnung} {Path.GetFileName(protokoll.Pfad)} → {abgelegt[0]}"
                : $"{protokoll.Bezeichnung} {Path.GetFileName(protokoll.Pfad)} deckt "
                  + $"{abgelegt.Count} Haltungen ab → {string.Join(", ", abgelegt)}");
        }

        return (verteilt, offen, gruende);
    }

    /// <summary>
    /// Nur eine Dichtheitspruefung kann ueber das Schachtpaar im Dokument gehen. Ein
    /// Aushaerteprotokoll nennt keinen Schacht — dort wuerde der alte Weg aus einer
    /// beliebigen Nummer einen Ordner bauen (in Buerglen aus der Chargen-Nr. des Liners).
    /// </summary>
    private static (IReadOnlyList<Sanierungsprotokoll> Schachtpaar, IReadOnlyList<Sanierungsprotokoll> Ohne)
        TeileNachWeg(IReadOnlyList<Sanierungsprotokoll> offen)
    {
        var schachtpaar = offen
            .Where(p => p.Art == SanierungsprotokollArt.Dichtheitspruefung)
            .ToList();
        var ohne = offen
            .Where(p => p.Art != SanierungsprotokollArt.Dichtheitspruefung)
            .ToList();
        return (schachtpaar, ohne);
    }

    private static string Datumsstempel(string? text)
        => HoldingDistribution.HoldingTextParser.TryFindInspectionDate(text ?? "")
               ?.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture)
           ?? "00000000";

    /// <summary>
    /// Legt eine Quelldatei als <c>&lt;JJJJMMTT&gt;_&lt;Haltung&gt;_&lt;DP|AH&gt;.pdf</c> im
    /// Haltungsordner ab. Liegt dort bereits eine inhaltsgleiche Datei, wird sie
    /// wiederverwendet statt ein zweites Mal kopiert; ein abweichender Bestand bekommt
    /// einen freien Namen. Kundenoriginale werden nur gelesen.
    /// </summary>
    private static string? LegeInHaltungsordner(
        Sanierungsprotokoll protokoll,
        string haltung,
        string stamp,
        string projectFolder,
        string zielRoot,
        IImportFileStagingSession? fileStaging,
        List<string> messages)
    {
        try
        {
            var san = AuswertungPro.Next.Application.Common.ProjectPathResolver
                .SanitizePathSegment(haltung);
            var dir = Path.Combine(zielRoot, san);
            var zielname = $"{stamp}_{san}_{protokoll.Kuerzel}.pdf";

            var vorhanden = FindeGleicheDatei(dir, protokoll.Pfad, fileStaging);
            if (vorhanden is not null)
                return vorhanden;

            if (fileStaging is not null)
                return fileStaging.StageCopyAs(protokoll.Pfad, dir, zielname);

            var writePaths = new ProjectWritePathGuard(projectFolder);
            dir = writePaths.EnsureSafeDirectoryTarget(dir);
            Directory.CreateDirectory(dir);
            var ziel = writePaths.EnsureSafeFileTarget(
                KanalImportDistributor.UniquePath(Path.Combine(dir, zielname)));
            writePaths.EnsureSafeFileTarget(ziel);
            File.Copy(protokoll.Pfad, ziel, overwrite: false);
            return ziel;
        }
        catch (Exception ex)
        {
            messages.Add(
                $"{protokoll.Bezeichnung} {Path.GetFileName(protokoll.Pfad)} → {haltung}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Sucht im Haltungsordner eine inhaltsgleiche PDF. Byteweiser Vergleich; Name,
    /// Groesse oder Zeitstempel allein sind kein Beleg fuer Gleichheit. Jeder Lesefehler
    /// ergibt <c>null</c> — dann wird kopiert.
    /// </summary>
    private static string? FindeGleicheDatei(
        string zielOrdner,
        string quelle,
        IImportFileStagingSession? fileStaging)
    {
        try
        {
            IEnumerable<(string Ziel, string Lesepfad)> kandidaten =
                fileStaging is null
                    ? (Directory.Exists(zielOrdner)
                        ? Directory.EnumerateFiles(zielOrdner, "*.pdf").Select(p => (p, p))
                        : Array.Empty<(string, string)>())
                    : fileStaging
                        .EnumerateReadableFiles(zielOrdner, "*.pdf", SearchOption.TopDirectoryOnly)
                        .Select(f => (f.TargetPath, f.ReadPath));

            foreach (var (ziel, lesepfad) in kandidaten.OrderBy(k => k.Ziel, StringComparer.OrdinalIgnoreCase))
            {
                if (File.Exists(lesepfad) && FileContentComparer.FilesEqual(lesepfad, quelle))
                    return ziel;
            }
        }
        catch (Exception ex)
        {
            AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                $"[DichtheitImport] Dublettenpruefung im Zielordner uebersprungen: {zielOrdner}: {ex.Message}");
        }

        return null;
    }

    private static IReadOnlyList<HoldingFolderDistributor.DistributionResult> DistributeCandidates(
        IReadOnlyList<string> candidates,
        string logicalDestinationRoot,
        Project project,
        IImportFileStagingSession? fileStaging)
    {
        if (fileStaging is null)
        {
            return HoldingFolderDistributor.DistributeDichtheitFiles(
                candidates,
                logicalDestinationRoot,
                project: project);
        }

        using var output = new StagedDistributionOutput();
        var temporaryResults = HoldingFolderDistributor.DistributeDichtheitFiles(
            candidates,
            output.OutputRoot,
            project: project);
        output.StageAll(fileStaging, logicalDestinationRoot);
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
    /// Kapselt den asynchronen KI-Aufruf auf einem freien Thread mit hartem Timeout.
    /// Dadurch kann ein aufrufender UI-Kontext nicht durch eine dort eingeplante
    /// Fortsetzung blockiert werden; Ollama-Ausfaelle stoppen den Import nie.
    /// </summary>
    private static PdfKiKlassifikation? FrageKi(PdfKiSchiedsrichter ki, string pdfPath)
    {
        try
        {
            using var cts = new System.Threading.CancellationTokenSource(TimeSpan.FromSeconds(25));
            return System.Threading.Tasks.Task.Run(
                    async () => await ki.KlassifiziereAsync(pdfPath, cts.Token)
                        .WaitAsync(cts.Token)
                        .ConfigureAwait(false),
                    cts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>Legt ein per KI zugeordnetes DP-Protokoll unter dem erkannten Schachtpaar ab.</summary>
    private static bool VerteilePerKi(
        PdfKiSchiedsrichter ki,
        string pdfPath,
        string projectFolder,
        string zielRoot,
        List<string> messages,
        IImportFileStagingSession? fileStaging)
    {
        var k = FrageKi(ki, pdfPath);
        if (k is null
            || string.IsNullOrWhiteSpace(k.SchachtVon)
            || string.IsNullOrWhiteSpace(k.SchachtBis))
            return false;

        try
        {
            var haltung = AuswertungPro.Next.Application.Common.ProjectPathResolver
                .SanitizePathSegment($"{k.SchachtVon}-{k.SchachtBis}");
            var stamp = "00000000";
            if (DateTime.TryParseExact(k.Datum ?? "", "dd.MM.yyyy",
                    System.Globalization.CultureInfo.InvariantCulture,
                    System.Globalization.DateTimeStyles.None, out var datum))
                stamp = datum.ToString("yyyyMMdd", System.Globalization.CultureInfo.InvariantCulture);

            var dir = Path.Combine(zielRoot, haltung);
            var ziel = Path.Combine(dir, $"{stamp}_{haltung}_DP.pdf");
            if (fileStaging is not null)
            {
                ziel = fileStaging.StageCopyAs(
                    pdfPath,
                    dir,
                    Path.GetFileName(ziel));
            }
            else
            {
                var writePaths = new ProjectWritePathGuard(projectFolder);
                writePaths.EnsureSafeDirectoryTarget(projectFolder);
                zielRoot = writePaths.EnsureSafeDirectoryTarget(zielRoot);
                dir = writePaths.EnsureSafeDirectoryTarget(dir);
                ziel = writePaths.EnsureSafeFileTarget(ziel);

                if (!(File.Exists(ziel) && FileContentComparer.FilesEqual(ziel, pdfPath)))
                {
                    ziel = writePaths.EnsureSafeFileTarget(
                        KanalImportDistributor.UniquePath(ziel));
                    Directory.CreateDirectory(dir);
                    dir = writePaths.EnsureSafeDirectoryTarget(dir);
                    writePaths.EnsureSafeDirectoryTarget(dir);
                    writePaths.EnsureSafeFileTarget(ziel);
                    File.Copy(pdfPath, ziel, overwrite: false);
                }
            }

            messages.Add($"DP per KI zugeordnet: {Path.GetFileName(pdfPath)} → {haltung}");
            return true;
        }
        catch (Exception ex)
        {
            messages.Add($"DP-KI-Zuordnung fehlgeschlagen ({Path.GetFileName(pdfPath)}): {ex.Message}");
            return false;
        }
    }

    /// <summary>DP-Ordner-PDFs, deren Typ die deterministische Erkennung NICHT bestimmen konnte.</summary>
    internal IReadOnlyList<string> FindeUnsichereKandidaten(string sourceFolder)
    {
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return Array.Empty<string>();

        try
        {
            return SafeFileEnumeration.EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                .Where(p => LiegtInDpOrdner(p, sourceFolder))
                .Where(p => PdfDokumentTypErkennung.ErkenneDatei(p) == PdfDokumentTyp.Unbekannt)
                .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    /// <summary>Sicher erkannte Begleitprotokolle der Quelle (rekursiv).</summary>
    internal IReadOnlyList<Sanierungsprotokoll> FindeKandidaten(string sourceFolder)
        => FindeKandidaten(sourceFolder, out _);

    /// <param name="hinweise">
    /// Dateien, deren Name auf ein Begleitprotokoll hinweist, deren Inhalt das aber nicht
    /// bestaetigt. Sie gehoeren in den Importbericht.
    /// </param>
    internal IReadOnlyList<Sanierungsprotokoll> FindeKandidaten(
        string sourceFolder, out IReadOnlyList<string> hinweise)
    {
        hinweise = Array.Empty<string>();
        if (string.IsNullOrWhiteSpace(sourceFolder) || !Directory.Exists(sourceFolder))
            return Array.Empty<Sanierungsprotokoll>();

        try
        {
            var gefunden = new List<Sanierungsprotokoll>();
            var gemeldet = new List<string>();
            foreach (var pfad in SafeFileEnumeration
                         .EnumerateFilesSafe(sourceFolder, "*.pdf", recursive: true)
                         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
            {
                var protokoll = Beurteile(pfad, sourceFolder, out var hinweis);
                if (protokoll is not null)
                    gefunden.Add(protokoll);
                else if (hinweis is not null)
                    gemeldet.Add(hinweis);
            }

            hinweise = gemeldet;
            return gefunden;
        }
        catch
        {
            return Array.Empty<Sanierungsprotokoll>();
        }
    }

    private static bool IstDichtheitsKandidat(string pdfPath, string sourceFolder)
        => Beurteile(pdfPath, sourceFolder) is not null;

    /// <summary>
    /// Beurteilt eine Quell-PDF: Ist sie ein Begleitprotokoll der Sanierung, und welcher
    /// Text belegt das?
    ///
    /// Der Text kommt aus <see cref="PdfDokumenttext"/> — bei einem reinen Scan also per
    /// OCR. Bis 2026-09-09 wurde nur die Textebene gelesen; die zehn eingescannten
    /// Dichtheitspruefungen aus Buerglen wurden deshalb nie als Kandidat erkannt.
    /// </summary>
    private static Sanierungsprotokoll? Beurteile(string pdfPath, string sourceFolder)
        => Beurteile(pdfPath, sourceFolder, out _);

    private static Sanierungsprotokoll? Beurteile(
        string pdfPath, string sourceFolder, out string? hinweis)
    {
        hinweis = null;
        var text = PdfDokumenttext.LiesKopf(pdfPath, maxPages: 6);
        var typMitDateiname = PdfDokumentTypErkennung.ErkenneText(text, Path.GetFileName(pdfPath));

        var art = typMitDateiname switch
        {
            PdfDokumentTyp.Dichtheitspruefung => SanierungsprotokollArt.Dichtheitspruefung,
            PdfDokumentTyp.Aushaerteprotokoll => SanierungsprotokollArt.Aushaertung,
            _ => (SanierungsprotokollArt?)null
        };

        if (art is null)
            return null;

        if (LiegtInDpOrdner(pdfPath, sourceFolder))
            return new Sanierungsprotokoll(pdfPath, art.Value, text);

        // In neutralen Ordnern wie "Dokumente" reicht der Dateiname nicht aus;
        // der Inhalt muss die Art selbst belegen.
        var typOhneDateiname = PdfDokumentTypErkennung.ErkenneText(text, fileName: null);
        if (typMitDateiname == typOhneDateiname)
            return new Sanierungsprotokoll(pdfPath, art.Value, text);

        // Der Name sagt das eine, der Inhalt bestaetigt es nicht. Das darf nicht still
        // enden: In Buerglen verlas das OCR die Titelzeile dreier Aushaerteprotokolle,
        // und sie verschwanden ohne eine einzige Zeile im Bericht.
        hinweis =
            $"{Path.GetFileName(pdfPath)}: Der Dateiname weist auf ein Begleitprotokoll hin, "
            + "der lesbare Inhalt bestaetigt das aber nicht — nicht verteilt.";
        return null;
    }

    private static bool LiegtInDpOrdner(string pdfPath, string sourceFolder)
    {
        var dir = Path.GetDirectoryName(pdfPath);
        while (!string.IsNullOrEmpty(dir)
               && dir.StartsWith(sourceFolder, StringComparison.OrdinalIgnoreCase)
               && !string.Equals(dir, sourceFolder, StringComparison.OrdinalIgnoreCase))
        {
            if (DpOrdnerRegex.IsMatch(Path.GetFileName(dir)))
                return true;
            dir = Path.GetDirectoryName(dir);
        }

        return false;
    }

    private static HashSet<string> LeseVorhandeneDpHashes(
        string zielRoot,
        IImportFileStagingSession? fileStaging)
    {
        var hashes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        try
        {
            // Beide Kuerzel: eine bereits abgelegte Aushaertung darf beim naechsten Lauf
            // ebenso wenig ein zweites Mal kopiert werden wie eine Dichtheitspruefung.
            var files = ZieldateiMuster
                .SelectMany(muster => fileStaging is null
                    ? Directory.Exists(zielRoot)
                        ? SafeFileEnumeration.EnumerateFilesSafe(
                            zielRoot,
                            muster,
                            recursive: true)
                            .Select(path => new ImportReadableFile(path, path))
                            .ToList()
                        : []
                    : fileStaging.EnumerateReadableFiles(
                        zielRoot,
                        muster,
                        SearchOption.AllDirectories))
                .DistinctBy(f => f.TargetPath, StringComparer.OrdinalIgnoreCase)
                .ToList();
            foreach (var file in files)
            {
                try
                {
                    hashes.Add(VerifiedImportFileCopy.ComputeSha256(file.ReadPath));
                }
                catch (Exception ex)
                {
                    // Best effort: defekte Ziel-Dateien verhindern nur die Duplikat-Erkennung fuer diese Datei.
                    AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                        $"[DichtheitImport] Vorhandenes DP-Protokoll ohne lesbare SHA-256 ignoriert: {file.TargetPath}: {ex.Message}");
                }
            }
        }
        catch (Exception ex)
        {
            // Best effort: wenn der Zielbaum nicht lesbar ist, laeuft der Import ohne Idempotenz-Guard weiter.
            AuswertungPro.Next.Application.Common.BestEffort.ReportWarning(
                $"[DichtheitImport] Vorhandene DP-Pruefsummen konnten nicht gelesen werden: {zielRoot}: {ex.Message}");
        }

        return hashes;
    }
}
