using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Map;
using Distributor = AuswertungPro.Next.Infrastructure.HoldingFolderDistributor;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Verteilt Dichtheitspruefungsprotokolle (DP) in die Haltungsordner-Struktur.
/// Aus HoldingFolderDistributor ausgelagert, damit die Verteilerklasse nicht weiter waechst.
///
/// Drei Eigenheiten dieses Wegs (18.09.2026):
/// - Eine Pegel-Dichtheitspruefung an einem Behaelter hat gar keine Haltung und geht
///   ueber <see cref="BehaelterPruefungParser"/> in einen eigenen Bauwerksordner.
/// - Sammelberichte werden seitenweise gesplittet; ein Fehler auf einer Seite darf die
///   uebrigen Seiten NICHT mitreissen.
/// - Nicht im Kataster bekannte Haltungen landen im Sammelordner "keine_Zuordnung".
/// </summary>
internal static class DichtheitDistributionController
{
    internal static IReadOnlyList<Distributor.DistributionResult> Verteile(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<Distributor.DistributionProgress>? progress,
        IHaltungCadastreResolver? cadastre = null,
        DistributionTargetConfig? directoryConfig = null)
    {
        var results = new List<Distributor.DistributionResult>();
        var processed = 0;

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var writePaths = new DistributionWritePathGuard(destGemeindeFolder);
                var pages = DistributionPdfAssignmentController.ReadPages(pdfPath);

                // Pegel-Dichtheitspruefung an einem Behaelter? Dann gibt es gar keine Haltung.
                // Frueher lief so ein Protokoll in die Haltungserkennung und erzeugte dort
                // erfundene Haltungen aus Fusszeile und Masstabelle (18.09.2026).
                var behaelter = BehaelterPruefungParser.Erkenne(
                    string.Join("\n\n", pages.Select(p => p.Text)));
                if (behaelter is not null)
                {
                    results.Add(VerteileBehaelterpruefung(
                        pdfPath, behaelter, destGemeindeFolder, directoryConfig, project,
                        writePaths, moveInsteadOfCopy, overwrite));
                    continue;
                }

                // Multi-Seiten-Erkennung: Jede Seite einzeln auf Haltungspaar pruefen.
                // KIT Bauinspekt PDFs haben pro Seite eine andere Haltung/Schacht.
                // Kontrollinformations-Seiten (Messdaten) gehoeren zur vorherigen Pruefseite.
                var pageResults = DistributionPdfAssignmentController.ExtractDichtheitPerPage(
                    pages,
                    project,
                    destGemeindeFolder,
                    cadastre);

                // Multi-Split nur wenn VERSCHIEDENE Haltungen erkannt wurden.
                // PDFs mit mehreren Seiten aber gleicher Haltung (z.B. Pruefbericht + Anhang)
                // werden als Ganzes behandelt.
                var distinctHaltungen = pageResults
                    .Where(pr => !string.IsNullOrWhiteSpace(pr.HaltungId))
                    .Select(pr => pr.HaltungId!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .Count();

                if (distinctHaltungen > 1)
                {
                    // Multi-Haltungs-PDF: seitenweise splitten und verteilen
                    foreach (var pr in pageResults)
                    {
                        if (string.IsNullOrWhiteSpace(pr.HaltungId))
                        {
                            results.Add(new Distributor.DistributionResult(false,
                                $"Seite {pr.MainPage}: Haltung nicht erkannt",
                                pdfPath, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked));
                            continue;
                        }

                        try
                        {
                            results.Add(VerteileDichtheitSeite(
                                pdfPath, pr, destGemeindeFolder, directoryConfig, project,
                                cadastre, writePaths, overwrite));
                        }
                        catch (Exception ex)
                        {
                            // Ein Fehler auf EINER Seite darf die uebrigen Seiten nicht mitreissen.
                            // Vorher lag der catch nur um die ganze Datei: ein nicht anlegbarer
                            // Ordner beendete damit die Verteilung aller weiteren Haltungen
                            // desselben Sammelberichts (18.09.2026, acht KIT-PDFs ohne Erfolg).
                            results.Add(new Distributor.DistributionResult(false,
                                $"Seite {pr.MainPage}: {ex.Message}",
                                pdfPath, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked));
                        }
                    }
                }
                else
                {
                    // Single-Haltung oder Fallback: gesamtes PDF einer Haltung zuordnen
                    var pdfText = string.Join("\n\n", pages.Select(p => p.Text));
                    string? haltungId = pageResults.Count == 1 ? pageResults[0].HaltungId : null;

                    // Bestehende Fallback-Kette wenn seitenweise Extraktion nichts ergab
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var (shaftA, shaftB) = DichtheitShaftParser.TryExtractShafts(pdfText);
                        if (!string.IsNullOrWhiteSpace(shaftA) && !string.IsNullOrWhiteSpace(shaftB))
                            haltungId = DistributionPdfAssignmentController.ResolveHoldingOrder(
                                shaftA,
                                shaftB,
                                project,
                                destGemeindeFolder);
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var parsed = Distributor.ParsePdfWithOcrFallback(pages);
                        if (parsed.Success && !string.IsNullOrWhiteSpace(parsed.Haltung))
                            haltungId = parsed.Haltung;
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                        haltungId = ShaftCandidateScanner.TryExtractFromShafts(pdfText);

                    // Letzter Rettungsanker: amtlicher Kataster-Abgleich (universell, formatunabhaengig).
                    if (string.IsNullOrWhiteSpace(haltungId) && cadastre is not null)
                        haltungId = DistributionPdfAssignmentController.ResolveViaCadastre(
                            pdfText,
                            cadastre);

                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        results.Add(new Distributor.DistributionResult(false,
                            "Haltung nicht erkannt (oberer/unterer Schacht nicht gefunden)",
                            pdfPath, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var date = HoldingTextParser.TryFindInspectionDate(pdfText);
                    if (date is null
                        && directoryConfig is not null
                        && pageResults.Count > 0
                        && DateTime.TryParseExact(
                            pageResults[0].DateStamp,
                            "yyyyMMdd",
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.None,
                            out var pageDate))
                    {
                        date = pageDate;
                    }
                    var dateStamp = date?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

                    var haltung = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(haltungId));
                    // Nicht im Kataster bekannte Haltungen in den Sammelordner "keine_Zuordnung"
                    // umlenken (reguläre Ablage-Logik, nur eine Ebene tiefer).
                    var destRoot = DistributionPdfAssignmentController.ResolveDistributionRoot(
                        destGemeindeFolder,
                        haltungId,
                        cadastre);
                    var treeContext = new DistributionPatternContext(
                        Datum: date,
                        Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
                        Haltung: haltung);
                    var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
                        destRoot,
                        directoryConfig,
                        treeContext,
                        "{Haltung}");
                    holdingFolder = writePaths.EnsureDirectoryTarget(holdingFolder);
                    Directory.CreateDirectory(holdingFolder);

                    var destPdfName = $"{dateStamp}_{haltung}_DP.pdf";
                    var destPath = DistributionTargetReuse.Lege(
                        writePaths, holdingFolder, Path.Combine(holdingFolder, destPdfName),
                        pdfPath, moveInsteadOfCopy, overwrite).Pfad;

                    results.Add(new Distributor.DistributionResult(true, $"OK -> {haltung}",
                        pdfPath, null, destPath, null, null, holdingFolder, Distributor.VideoMatchStatus.NotChecked));
                }
            }
            catch (Exception ex)
            {
                results.Add(new Distributor.DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new Distributor.DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        return results;
    }

    /// <summary>
    /// Legt die Seite(n) EINER erkannten Haltung eines Dichtheits-Sammelberichts ab.
    /// Ausgelagert, damit der Aufrufer jede Seite einzeln absichern kann.
    /// </summary>
    private static Distributor.DistributionResult VerteileDichtheitSeite(
        string pdfPath,
        DichtheitPageAssignment pr,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        Project? project,
        IHaltungCadastreResolver? cadastre,
        DistributionWritePathGuard writePaths,
        bool overwrite)
    {
        var haltung = ProjectPathResolver.SanitizePathSegment(HoldingIdNormalizer.NormalizeHaltungId(pr.HaltungId!));
        // Nicht im Kataster bekannte Haltungen in den Sammelordner "keine_Zuordnung"
        // umlenken (reguläre Ablage-Logik, nur eine Ebene tiefer).
        var destRoot = DistributionPdfAssignmentController.ResolveDistributionRoot(
            destGemeindeFolder,
            pr.HaltungId,
            cadastre);
        var hasTreeDate = DateTime.TryParseExact(
            pr.DateStamp,
            "yyyyMMdd",
            CultureInfo.InvariantCulture,
            DateTimeStyles.None,
            out var treeDate);
        var treeContext = new DistributionPatternContext(
            Datum: hasTreeDate ? treeDate : null,
            Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
            Haltung: haltung);
        var holdingFolder = DistributionDirectoryTreeController.ResolveObjectFolder(
            destRoot,
            directoryConfig,
            treeContext,
            "{Haltung}");
        holdingFolder = writePaths.EnsureDirectoryTarget(holdingFolder);
        Directory.CreateDirectory(holdingFolder);

        var suffix = pr.IsSchacht ? "SP" : "DP";
        var destPdfName = $"{pr.DateStamp}_{haltung}_{suffix}.pdf";
        // Einzelseite(n) zuerst in eine Temp-Datei, damit ein zweiter Lauf die gleiche
        // Auszugs-PDF erkennt und wiederverwendet statt eine Kopie mit «_01» anzulegen.
        var tempPdf = Path.Combine(Path.GetTempPath(), $"dp_{Guid.NewGuid():N}.pdf");
        string destPath;
        try
        {
            Distributor.WritePdfPages(pdfPath, pr.PageNumbers, tempPdf);
            destPath = DistributionTargetReuse.Lege(
                writePaths, holdingFolder, Path.Combine(holdingFolder, destPdfName),
                tempPdf, moveInsteadOfCopy: false, overwrite).Pfad;
        }
        finally
        {
            AuswertungPro.Next.Application.Common.BestEffort.Try(
                () => { if (File.Exists(tempPdf)) File.Delete(tempPdf); },
                "Dichtheitsverteilung: Temp loeschen");
        }

        return new Distributor.DistributionResult(true,
            $"OK -> {haltung} (S{pr.MainPage}, {pr.PageNumbers.Count} Seite(n))",
            pdfPath, null, destPath, null, null, holdingFolder, Distributor.VideoMatchStatus.NotChecked);
    }

    /// <summary>
    /// Legt eine Pegel-Dichtheitspruefung an einem Behaelter (Regenbecken, Referenzgefaess)
    /// in einem eigenen Bauwerksordner ab. Kuerzel BP statt DP; das Dokument bleibt ganz.
    /// Ohne lesbares Pruefobjekt wird bewusst NICHTS abgelegt - kein geratener Ordnername.
    /// </summary>
    private static Distributor.DistributionResult VerteileBehaelterpruefung(
        string pdfPath,
        BehaelterPruefung behaelter,
        string destGemeindeFolder,
        DistributionTargetConfig? directoryConfig,
        Project? project,
        DistributionWritePathGuard writePaths,
        bool moveInsteadOfCopy,
        bool overwrite)
    {
        if (string.IsNullOrWhiteSpace(behaelter.Objekt))
        {
            return new Distributor.DistributionResult(false,
                "Behälterprüfung erkannt, aber kein Prüfobjekt lesbar - nicht verteilt",
                pdfPath, null, null, null, null, null, Distributor.VideoMatchStatus.NotChecked);
        }

        var objekt = ProjectPathResolver.SanitizePathSegment(behaelter.Objekt!);
        var dateStamp = behaelter.Datum?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";
        var treeContext = new DistributionPatternContext(
            Datum: behaelter.Datum,
            Gemeinde: DistributionDirectoryTreeController.GetMunicipality(project),
            Haltung: objekt);
        var zielOrdner = DistributionDirectoryTreeController.ResolveObjectFolder(
            destGemeindeFolder,
            directoryConfig,
            treeContext,
            "{Haltung}");
        zielOrdner = writePaths.EnsureDirectoryTarget(zielOrdner);
        Directory.CreateDirectory(zielOrdner);

        var destPath = DistributionTargetReuse.Lege(
            writePaths, zielOrdner, Path.Combine(zielOrdner, $"{dateStamp}_{objekt}_BP.pdf"),
            pdfPath, moveInsteadOfCopy, overwrite).Pfad;

        return new Distributor.DistributionResult(true,
            $"OK -> {objekt} (Behälterprüfung)",
            pdfPath, null, destPath, null, null, zielOrdner, Distributor.VideoMatchStatus.NotChecked);
    }
}
