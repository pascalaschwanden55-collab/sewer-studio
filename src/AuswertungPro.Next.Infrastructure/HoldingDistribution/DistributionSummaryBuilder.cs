using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.HoldingDistribution;

/// <summary>
/// Erzeugt menschenlesbare Zusammenfassungstexte aus Verteilungs-Ergebnislisten.
/// Reine Aggregations-/Formatierungslogik ohne UI-Abhängigkeit.
/// </summary>
public static class DistributionSummaryBuilder
{
    /// <summary>
    /// Gibt <c>true</c> zurück, wenn der Dateipfad ein GIS-/Datei-Sidecar ist
    /// (Erweiterung .xtf, .m150, .mdb oder .xml).
    /// </summary>
    public static bool IsDataSidecar(string path)
    {
        var ext = Path.GetExtension(path);
        return ext.Equals(".xtf",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".m150", StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".mdb",  StringComparison.OrdinalIgnoreCase)
            || ext.Equals(".xml",  StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Sortierpriorität für die Vorschauzeilen: Matched zuerst, dann Ambiguous, dann NotFound.
    /// </summary>
    public static int PreviewRank(HoldingFolderDistributor.DistributionResult r) =>
        r.VideoStatus switch
        {
            HoldingFolderDistributor.VideoMatchStatus.Matched   => 0,
            HoldingFolderDistributor.VideoMatchStatus.Ambiguous => 1,
            HoldingFolderDistributor.VideoMatchStatus.NotFound  => 2,
            _                                                    => 3
        };

    /// <summary>
    /// Erstellt den Zusammenfassungstext für eine Haltungs-Verteilung (PDF- oder TXT-Modus).
    /// </summary>
    /// <param name="results">Alle Verteilungs-Ergebnisse (roh, ungefiltert).</param>
    /// <param name="useTxtImport">
    /// <c>true</c> wenn TXT-Import-Modus, <c>false</c> wenn PDF-Import-Modus.
    /// </param>
    public static string BuildHoldingDistributionSummary(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> results,
        bool useTxtImport)
    {
        var sidecarResults = useTxtImport
            ? new List<HoldingFolderDistributor.DistributionResult>()
            : results.Where(r => IsDataSidecar(r.SourcePdfPath)).ToList();

        var importResults = useTxtImport
            ? results.ToList()
            : results.Where(r => !IsDataSidecar(r.SourcePdfPath)).ToList();

        var ok        = importResults.Count(r => r.Success);
        var failed    = importResults.Count - ok;
        var matched   = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched);
        var missing   = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound);
        var ambiguous = importResults.Count(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Ambiguous);

        var sb = new StringBuilder();
        sb.AppendLine($"Modus: {(useTxtImport ? "TXT-Import" : "PDF-Import")}");
        sb.AppendLine($"Verarbeitet: {importResults.Count} | OK: {ok} | Fehler: {failed}");
        sb.AppendLine($"Video: Matched {matched}, Missing {missing}, Ambiguous {ambiguous}");

        if (sidecarResults.Count > 0)
        {
            var sidecarOk = sidecarResults.Count(r => r.Success);
            sb.AppendLine($"XTF/M150/MDB/XML: {sidecarOk}/{sidecarResults.Count} kopiert");
        }

        sb.AppendLine("Matched (Top 20):");
        foreach (var r in importResults
                     .Where(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.Matched)
                     .OrderByDescending(r => r.Success)
                     .Take(20))
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

        sb.AppendLine("Missing (Top 20):");
        foreach (var r in importResults
                     .Where(r => r.VideoStatus == HoldingFolderDistributor.VideoMatchStatus.NotFound)
                     .OrderByDescending(r => r.Success)
                     .Take(20))
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

        sb.AppendLine("Preview (Top 50):");
        foreach (var r in importResults
                     .OrderBy(PreviewRank)
                     .ThenByDescending(r => r.Success)
                     .Take(50))
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

        foreach (var r in sidecarResults)
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message}");

        return sb.ToString();
    }

    /// <summary>
    /// Erstellt den Zusammenfassungstext für eine Schacht-Verteilung.
    /// </summary>
    public static string BuildShaftDistributionSummary(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> results)
    {
        var ok     = results.Count(r => r.Success);
        var failed = results.Count - ok;

        var sb = new StringBuilder();
        sb.AppendLine($"Schachtprotokolle: {results.Count} | OK: {ok} | Fehler: {failed}");
        foreach (var r in results.Take(50))
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

        return sb.ToString();
    }

    /// <summary>
    /// Erstellt den Zusammenfassungstext für eine Dichtheitsprüfungs-Verteilung.
    /// </summary>
    public static string BuildDichtheitDistributionSummary(
        IReadOnlyList<HoldingFolderDistributor.DistributionResult> results)
    {
        var ok     = results.Count(r => r.Success);
        var failed = results.Count - ok;

        var sb = new StringBuilder();
        sb.AppendLine($"Dichtheitsprüfung: {results.Count} | OK: {ok} | Fehler: {failed}");
        foreach (var r in results.Take(50))
            sb.AppendLine($"{(r.Success ? "OK" : "FAIL")} - {r.Message} - {r.SourcePdfPath}");

        return sb.ToString();
    }
}
