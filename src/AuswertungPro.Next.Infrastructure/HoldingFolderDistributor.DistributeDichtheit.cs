using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure;

/// <summary>
/// HoldingFolderDistributor — Dichtheitspruefungs-PDF-Distribution
/// (partial class).
///
/// Refactor 2026-05-07 (Etappe 4, Charge R11): Public DistributeDichtheit*
/// + DistributeDichtheitCore + Multi-Seiten-Helpers (ExtractDichtheitPerPage,
/// TryExtractDichtheitShafts, ResolveDichtheitHaltungOrder) ausgegliedert.
/// Mechanisch — keine Verhaltensaenderung.
/// </summary>
public static partial class HoldingFolderDistributor
{
    /// <summary>
    /// Verteilt Dichtheitspruefungsprotokolle (DP) aus einem Quellordner in die
    /// Haltungsordner-Struktur. Liest "oberer Schacht" / "unterer Schacht" aus dem PDF
    /// um die Haltung zu ermitteln.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheit(
        string pdfSourceFolder,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        if (!Directory.Exists(pdfSourceFolder))
            return new[] { new DistributionResult(false, $"PDF folder not found: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        // Audit 2026-05-17: SafeFileEnumeration statt Directory.EnumerateFiles.
        var pdfFiles = SafeFileEnumeration.EnumerateFilesSafe(pdfSourceFolder, "*.pdf", recursive: true)
            .Where(p => !Path.GetFileName(p).StartsWith("split_", StringComparison.OrdinalIgnoreCase))
            .ToList();

        if (pdfFiles.Count == 0)
            return new[] { new DistributionResult(false, $"No PDF files found in: {pdfSourceFolder}", pdfSourceFolder, null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(pdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    /// <summary>
    /// Verteilt ausgewaehlte Dichtheitspruefungsprotokolle (DP) in die
    /// Haltungsordner-Struktur.
    /// </summary>
    public static IReadOnlyList<DistributionResult> DistributeDichtheitFiles(
        IEnumerable<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy = false,
        bool overwrite = false,
        Project? project = null,
        IProgress<DistributionProgress>? progress = null)
    {
        var validPdfFiles = pdfFiles
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(p => p.Trim())
            .Where(File.Exists)
            .Where(p => string.Equals(Path.GetExtension(p), ".pdf", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (validPdfFiles.Count == 0)
            return new[] { new DistributionResult(false, "No valid PDF files selected.", "", null, null, null, null, null, VideoMatchStatus.NotChecked) };

        return DistributeDichtheitCore(validPdfFiles, destGemeindeFolder, moveInsteadOfCopy, overwrite, project, progress);
    }

    private static IReadOnlyList<DistributionResult> DistributeDichtheitCore(
        IReadOnlyList<string> pdfFiles,
        string destGemeindeFolder,
        bool moveInsteadOfCopy,
        bool overwrite,
        Project? project,
        IProgress<DistributionProgress>? progress)
    {
        var results = new List<DistributionResult>();
        var processed = 0;

        foreach (var pdfPath in pdfFiles)
        {
            try
            {
                var pages = ReadPdfPages(pdfPath);

                // Multi-Seiten-Erkennung: Jede Seite einzeln auf Haltungspaar pruefen.
                // KIT Bauinspekt PDFs haben pro Seite eine andere Haltung/Schacht.
                // Kontrollinformations-Seiten (Messdaten) gehoeren zur vorherigen Pruefseite.
                var pageResults = ExtractDichtheitPerPage(pages, project, destGemeindeFolder);

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
                            results.Add(new DistributionResult(false,
                                $"Seite {pr.MainPage}: Haltung nicht erkannt",
                                pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                            continue;
                        }

                        var haltung = SanitizePathSegment(NormalizeHaltungId(pr.HaltungId));
                        var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
                        Directory.CreateDirectory(holdingFolder);

                        var suffix = pr.IsSchacht ? "SP" : "DP";
                        var destPdfName = $"{pr.DateStamp}_{haltung}_{suffix}.pdf";
                        var destPath = EnsureUniquePath(
                            Path.Combine(holdingFolder, destPdfName), overwrite);

                        // Einzelseite(n) als neues PDF schreiben
                        WritePdfPages(pdfPath, pr.PageNumbers, destPath);

                        results.Add(new DistributionResult(true,
                            $"OK -> {haltung} (S{pr.MainPage}, {pr.PageNumbers.Count} Seite(n))",
                            pdfPath, null, destPath, null, null, holdingFolder, VideoMatchStatus.NotChecked));
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
                        var (shaftA, shaftB) = TryExtractDichtheitShafts(pdfText);
                        if (!string.IsNullOrWhiteSpace(shaftA) && !string.IsNullOrWhiteSpace(shaftB))
                            haltungId = ResolveDichtheitHaltungOrder(shaftA, shaftB, project, destGemeindeFolder);
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        var parsed = ParsePdfWithOcrFallback(pages);
                        if (parsed.Success && !string.IsNullOrWhiteSpace(parsed.Haltung))
                            haltungId = parsed.Haltung;
                    }
                    if (string.IsNullOrWhiteSpace(haltungId))
                        haltungId = TryExtractFromShafts(pdfText);

                    if (string.IsNullOrWhiteSpace(haltungId))
                    {
                        results.Add(new DistributionResult(false,
                            "Haltung nicht erkannt (oberer/unterer Schacht nicht gefunden)",
                            pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
                        continue;
                    }

                    var date = TryFindInspectionDate(pdfText);
                    var dateStamp = date?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

                    var haltung = SanitizePathSegment(NormalizeHaltungId(haltungId));
                    var holdingFolder = Path.Combine(destGemeindeFolder, haltung);
                    Directory.CreateDirectory(holdingFolder);

                    var destPdfName = $"{dateStamp}_{haltung}_DP.pdf";
                    var destPath = EnsureUniquePath(
                        Path.Combine(holdingFolder, destPdfName), overwrite);
                    MoveOrCopy(pdfPath, destPath, moveInsteadOfCopy);

                    results.Add(new DistributionResult(true, $"OK -> {haltung}",
                        pdfPath, null, destPath, null, null, holdingFolder, VideoMatchStatus.NotChecked));
                }
            }
            catch (Exception ex)
            {
                results.Add(new DistributionResult(false, ex.Message, pdfPath, null, null, null, null, null, VideoMatchStatus.NotChecked));
            }
            finally
            {
                processed++;
                progress?.Report(new DistributionProgress(processed, pdfFiles.Count, pdfPath));
            }
        }

        return results;
    }

    // ── Multi-Seiten-Dichtheitspruefung (KIT-Format u.a.) ──────────────────

    private sealed record DichtheitPageResult(
        int MainPage,
        IReadOnlyList<int> PageNumbers,
        string? HaltungId,
        string DateStamp,
        bool IsSchacht);

    /// <summary>
    /// Extrahiert pro Seite die Haltung/Schacht-Zuordnung.
    /// Kontrollinformations-Seiten werden der vorherigen Pruefseite zugeordnet.
    /// Gibt eine Liste mit einem Eintrag pro Pruefbericht zurueck.
    /// </summary>
    private static IReadOnlyList<DichtheitPageResult> ExtractDichtheitPerPage(
        IReadOnlyList<PageInfo> pages,
        Project? project,
        string destGemeindeFolder)
    {
        var results = new List<DichtheitPageResult>();

        foreach (var page in pages)
        {
            var text = page.Text;

            // Kontrollinformation = Folgeseite einer Pruefung → zur vorherigen anhaengen
            if (text.Contains("Kontrollinformation"))
            {
                if (results.Count > 0)
                {
                    var prev = results[^1];
                    var extPages = new List<int>(prev.PageNumbers) { page.PageNumber };
                    results[^1] = prev with { PageNumbers = extPages };
                }
                continue;
            }

            // Datum aus Seiteninhalt (YYYY/MM/DD Format, typisch fuer KIT)
            var dateMatch = Regex.Match(text, @"(\d{4})/(\d{2})/(\d{2})");
            var dateStamp = dateMatch.Success
                ? $"{dateMatch.Groups[1].Value}{dateMatch.Groups[2].Value}{dateMatch.Groups[3].Value}"
                : TryFindInspectionDate(text)?.ToString("yyyyMMdd", CultureInfo.InvariantCulture) ?? "00000000";

            // Schachtpruefung? (Label: "Prüfgegenstand / Schacht")
            bool isSchacht = text.Contains("Prufgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Pruefgegenstand / Schacht", StringComparison.OrdinalIgnoreCase)
                          || text.Contains("Prüfgegenstand / Schacht", StringComparison.OrdinalIgnoreCase);

            string? haltungId = null;

            // Haltungspaar suchen: zwei 5-stellige Nummern auf einer Zeile
            // (OCR kann ^ als diverse Zeichen rendern — deshalb robust: 2 Nummern auf gleicher Zeile)
            foreach (var line in text.Split('\n'))
            {
                // Zeilen mit bekannten Nicht-Haltungs-Mustern ueberspringen
                if (line.Contains("Ebikon", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("Altdorf", StringComparison.OrdinalIgnoreCase)
                    || line.Contains("GPS", StringComparison.OrdinalIgnoreCase))
                    continue;
                // "gepruft bei 40693,6473" — nur eine Nummer vor Komma, kein Paar
                if (Regex.IsMatch(line, @"gepr[uü]ft\s+bei", RegexOptions.IgnoreCase))
                    continue;

                var nums = Regex.Matches(line, @"\b(\d{5})\b");
                if (nums.Count >= 2)
                {
                    var a = nums[0].Groups[1].Value;
                    var b = nums[1].Groups[1].Value;
                    if (!string.Equals(a, b, StringComparison.Ordinal))
                    {
                        haltungId = ResolveDichtheitHaltungOrder(a, b, project, destGemeindeFolder)
                                    ?? $"{a}-{b}";
                        break;
                    }
                }
            }

            // Schacht: einzelne Nummer neben "Strang"
            if (haltungId == null && isSchacht)
            {
                var schachtMatch = Regex.Match(text, @":\s*(\d{5})\s*:?\s*Strang", RegexOptions.IgnoreCase);
                if (schachtMatch.Success)
                    haltungId = $"Schacht_{schachtMatch.Groups[1].Value}";
            }

            // Standard-Fallbacks
            if (haltungId == null)
            {
                var (shA, shB) = TryExtractDichtheitShafts(text);
                if (!string.IsNullOrWhiteSpace(shA) && !string.IsNullOrWhiteSpace(shB))
                    haltungId = ResolveDichtheitHaltungOrder(shA, shB, project, destGemeindeFolder)
                                ?? $"{shA}-{shB}";
            }
            if (haltungId == null)
                haltungId = TryExtractFromShafts(text);

            results.Add(new DichtheitPageResult(
                MainPage: page.PageNumber,
                PageNumbers: new List<int> { page.PageNumber },
                HaltungId: haltungId,
                DateStamp: dateStamp,
                IsSchacht: isSchacht && haltungId?.StartsWith("Schacht_") == true));
        }

        return results;
    }

    /// <summary>
    /// Extrahiert die beiden Schachtnummern aus einem Dichtheitspruefungsprotokoll-PDF.
    /// Gibt (schachtA, schachtB) zurueck – die Reihenfolge kann vertauscht sein.
    /// </summary>
    private static (string? A, string? B) TryExtractDichtheitShafts(string text)
    {
        // "oberer Schacht: XXXXX" / "unterer Schacht: XXXXX"
        var upperM = DichtheitUpperRx.Match(text);
        var lowerM = DichtheitLowerRx.Match(text);
        if (upperM.Success && lowerM.Success)
        {
            var up = upperM.Groups["v"].Value;
            var low = lowerM.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return (up, low);
        }

        // Fallback: "Schacht oben" / "Schacht unten"
        var upperS = SchachtObenRx.Match(text);
        var lowerS = SchachtUntenRx.Match(text);
        if (upperS.Success && lowerS.Success)
        {
            var up = upperS.Groups["v"].Value;
            var low = lowerS.Groups["v"].Value;
            if (!string.Equals(up, low, StringComparison.OrdinalIgnoreCase))
                return (up, low);
        }

        return (null, null);
    }

    /// <summary>
    /// Ermittelt die korrekte Haltungs-ID-Reihenfolge fuer zwei Schachtnummern.
    /// Prueft A-B und B-A gegen Projekt-Daten und vorhandene Ordner im Zielverzeichnis.
    /// </summary>
    private static string ResolveDichtheitHaltungOrder(
        string a, string b, Project? project, string destGemeindeFolder)
    {
        var ab = $"{a}-{b}";
        var ba = $"{b}-{a}";

        // 1) Gegen Projekt-Haltungsnamen pruefen
        if (project is not null)
        {
            foreach (var rec in project.Data)
            {
                var name = rec.GetFieldValue("Haltungsname")?.Trim() ?? "";
                if (string.IsNullOrWhiteSpace(name)) continue;
                var normalized = NormalizeHaltungId(name);
                var stripped = StripNodePrefixes(SanitizePathSegment(normalized));

                if (string.Equals(normalized, ab, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stripped, StripNodePrefixes(SanitizePathSegment(ab)), StringComparison.OrdinalIgnoreCase))
                    return ab;
                if (string.Equals(normalized, ba, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(stripped, StripNodePrefixes(SanitizePathSegment(ba)), StringComparison.OrdinalIgnoreCase))
                    return ba;
            }
        }

        // 2) Gegen vorhandene Ordner im Ziel pruefen
        if (Directory.Exists(destGemeindeFolder))
        {
            var abSanitized = SanitizePathSegment(NormalizeHaltungId(ab));
            var baSanitized = SanitizePathSegment(NormalizeHaltungId(ba));
            var abStripped = StripNodePrefixes(abSanitized);
            var baStripped = StripNodePrefixes(baSanitized);

            foreach (var dir in Directory.EnumerateDirectories(destGemeindeFolder))
            {
                var dirName = Path.GetFileName(dir) ?? "";
                var dirStripped = StripNodePrefixes(dirName);

                if (string.Equals(dirName, abSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dirStripped, abStripped, StringComparison.OrdinalIgnoreCase))
                    return ab;
                if (string.Equals(dirName, baSanitized, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(dirStripped, baStripped, StringComparison.OrdinalIgnoreCase))
                    return ba;
            }
        }

        // 3) Kein Treffer – PDF-Reihenfolge beibehalten (A-B)
        return ab;
    }
}
