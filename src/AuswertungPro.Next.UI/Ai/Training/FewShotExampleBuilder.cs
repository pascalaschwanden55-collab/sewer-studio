// SewerStudio – Automatischer Aufbau der Few-Shot Beispiel-Bibliothek
// Extrahiert Fotos + Codes aus PDF-Protokollen und speichert sie als kuratierte Beispiele.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Ai.Training.Services;

namespace AuswertungPro.Next.UI.Ai.Training;

/// <summary>
/// Fortschritt beim Aufbau der Beispiel-Bibliothek.
/// </summary>
public sealed record FewShotBuildProgress(
    int CurrentFolder,
    int TotalFolders,
    string FolderName,
    int ExamplesAdded,
    int ExamplesSkipped,
    string? Message);

/// <summary>
/// Ergebnis des Bibliothek-Aufbaus.
/// </summary>
public sealed record FewShotBuildResult(
    int FoldersScanned,
    int ExamplesAdded,
    int ExamplesSkipped,
    int Errors,
    TimeSpan Duration);

/// <summary>
/// Scannt Ordner mit Video+PDF-Paaren und extrahiert Few-Shot Beispiele:
/// - PDF parsen → Protokoll-Eintraege mit VSA-Codes
/// - Eingebettete Fotos extrahieren
/// - Foto + Code + Beschreibung als FewShotExample speichern
///
/// Filtert automatisch:
/// - Nur "echte" Schadensfotos (kein Rohranfang/Rohrende, kein BCD/BCE)
/// - Nur Fotos mit erkennbarem Schaden (nicht BDA Allgemeinzustand)
/// - Vermeidet Duplikate (gleicher Code + aehnlicher Meter = Skip)
/// </summary>
public sealed class FewShotExampleBuilder
{
    private readonly FewShotExampleStore _store;
    private readonly PdfProtocolExtractor _pdfExtractor;

    // Codes die KEINE Schadensbeispiele sind (Rohrstart/-ende, Beginn TV etc.)
    private static readonly HashSet<string> SkipCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BCD",   // Rohranfang
        "BCE",   // Rohrende
        "BDBA",  // Beginn TV-Untersuchung
        "BDBB",  // Ende TV-Untersuchung
        "AEF",   // Neue Laenge
        "AECXC", // Profilwechsel
        "AEDXP", // Materialwechsel
    };

    // Codes die interessante Trainingsbeispiele sind
    // (hoehere Qualitaet weil sie spezifische Schaeden zeigen)
    private static readonly HashSet<string> HighValuePrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "BAA",  // Deformation
        "BAB",  // Riss
        "BAC",  // Bruch/Einsturz
        "BAD",  // Defekte Wandung
        "BAE",  // Fehlender Moertel
        "BAF",  // Scherbe
        "BAG",  // Einragender Anschluss
        "BAH",  // Versatz
        "BAI",  // Abzweig/Anschluss
        "BAJ",  // Verformung
        "BBA",  // Wurzeleinwuchs
        "BBB",  // Anhaftungen
        "BBC",  // Infiltration
        "BBD",  // Exfiltration
        "BBE",  // Hindernisse
        "BBF",  // Ablagerung
    };

    // Uhrzeitlage aus Beschreibungstext extrahieren
    private static readonly Regex ClockRegex = new(
        @"(?:von\s+)?(\d{1,2})\s*Uhr\s*(?:bis\s+(\d{1,2})\s*Uhr)?",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Material aus PDF-Header extrahieren
    private static readonly Regex MaterialRegex = new(
        @"Material\s+(.+?)(?:\r|\n|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // Profil aus PDF-Header extrahieren
    private static readonly Regex ProfileRegex = new(
        @"Profil\s+(.+?)(?:\r|\n|$)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public FewShotExampleBuilder(FewShotExampleStore store)
    {
        _store = store;
        _pdfExtractor = new PdfProtocolExtractor();
    }

    /// <summary>
    /// Scannt einen Ordner rekursiv nach Video+PDF-Paaren und baut die Beispiel-Bibliothek auf.
    /// </summary>
    public async Task<FewShotBuildResult> BuildFromFolderAsync(
        string rootFolder,
        IProgress<FewShotBuildProgress>? progress = null,
        CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();

        if (!Directory.Exists(rootFolder))
            return new FewShotBuildResult(0, 0, 0, 0, sw.Elapsed);

        // Alle Unterordner mit PDFs finden
        var pdfFolders = FindPdfFolders(rootFolder);
        int totalAdded = 0, totalSkipped = 0, totalErrors = 0;

        await _store.LoadAsync(ct);

        for (int i = 0; i < pdfFolders.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var (folder, pdfPath) = pdfFolders[i];
            var folderName = Path.GetFileName(folder);

            progress?.Report(new FewShotBuildProgress(
                i + 1, pdfFolders.Count, folderName, totalAdded, totalSkipped, null));

            try
            {
                var (added, skipped) = await ProcessPdfAsync(pdfPath, folder, folderName, ct);
                totalAdded += added;
                totalSkipped += skipped;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                totalErrors++;
                progress?.Report(new FewShotBuildProgress(
                    i + 1, pdfFolders.Count, folderName, totalAdded, totalSkipped,
                    $"Fehler: {ex.Message}"));
            }
        }

        sw.Stop();

        progress?.Report(new FewShotBuildProgress(
            pdfFolders.Count, pdfFolders.Count, "Fertig",
            totalAdded, totalSkipped,
            $"Bibliothek: {_store.Examples.Count} Beispiele total"));

        return new FewShotBuildResult(pdfFolders.Count, totalAdded, totalSkipped, totalErrors, sw.Elapsed);
    }

    /// <summary>Verarbeitet ein einzelnes PDF-Protokoll.</summary>
    private async Task<(int Added, int Skipped)> ProcessPdfAsync(
        string pdfPath,
        string folder,
        string caseId,
        CancellationToken ct)
    {
        // Frames-Ordner fuer extrahierte Bilder
        var framesDir = Path.Combine(folder, "fewshot_frames");

        // PDF parsen und Fotos extrahieren
        var entries = await _pdfExtractor.ExtractAsync(pdfPath, framesDir, ct);

        // Header-Infos extrahieren (Material, Profil)
        string? material = null, profile = null;
        try
        {
            using var doc = UglyToad.PdfPig.PdfDocument.Open(pdfPath);
            var firstPageText = string.Join(" ", doc.GetPage(1).Letters.Select(l => l.Value));
            var matMatch = MaterialRegex.Match(firstPageText);
            if (matMatch.Success) material = matMatch.Groups[1].Value.Trim();
            var profMatch = ProfileRegex.Match(firstPageText);
            if (profMatch.Success) profile = profMatch.Groups[1].Value.Trim();
        }
        catch { /* Header-Parsing ist optional */ }

        int added = 0, skipped = 0;

        foreach (var entry in entries)
        {
            ct.ThrowIfCancellationRequested();

            // Nur Eintraege MIT extrahiertem Foto
            if (string.IsNullOrEmpty(entry.ExtractedFramePath) || !File.Exists(entry.ExtractedFramePath))
            {
                skipped++;
                continue;
            }

            // Skip-Codes filtern (Rohranfang, Rohrende etc.)
            if (SkipCodes.Contains(entry.VsaCode))
            {
                skipped++;
                continue;
            }

            // Pruefe ob aehnliches Beispiel schon existiert
            if (IsDuplicate(entry.VsaCode, entry.MeterStart, caseId))
            {
                skipped++;
                continue;
            }

            // Qualitaet bestimmen
            double quality = DetermineQuality(entry);

            // Uhrzeitlage aus Text extrahieren
            string? clock = ExtractClockPosition(entry.Text);

            // Bild laden
            byte[] imageBytes;
            try
            {
                imageBytes = await File.ReadAllBytesAsync(entry.ExtractedFramePath, ct);
            }
            catch
            {
                skipped++;
                continue;
            }

            // Mindestgroesse: 5 KB (zu kleine Bilder sind unbrauchbar)
            if (imageBytes.Length < 5_000)
            {
                skipped++;
                continue;
            }

            var ext = Path.GetExtension(entry.ExtractedFramePath).ToLowerInvariant();

            await _store.AddExampleAsync(
                imageBytes, ext,
                entry.VsaCode,
                entry.Text,
                clock,
                entry.MeterStart,
                material, profile,
                $"pdf:{caseId}",
                quality,
                ct);

            added++;
        }

        // Temporaere Frames aufraeumen (die Bilder sind jetzt im Knowledge-Ordner)
        try
        {
            if (Directory.Exists(framesDir))
                Directory.Delete(framesDir, recursive: true);
        }
        catch { /* best effort */ }

        return (added, skipped);
    }

    /// <summary>Prueft ob ein aehnliches Beispiel schon in der Bibliothek ist.</summary>
    private bool IsDuplicate(string vsaCode, double meter, string source)
    {
        return _store.Examples.Any(e =>
            e.VsaCode.Equals(vsaCode, StringComparison.OrdinalIgnoreCase)
            && e.Source == $"pdf:{source}"
            && Math.Abs(e.MeterPosition - meter) < 0.5);
    }

    /// <summary>Bestimmt die Qualitaet eines Beispiels basierend auf dem Code.</summary>
    private static double DetermineQuality(GroundTruthEntry entry)
    {
        var code = entry.VsaCode.ToUpperInvariant();

        // Hochwertiger Schaden mit spezifischem Code
        if (code.Length >= 3 && HighValuePrefixes.Any(p => code.StartsWith(p)))
            return 0.9;

        // BDA = Allgemeinzustand — niedrigere Qualitaet weil wenig spezifisch
        if (code.StartsWith("BDA", StringComparison.OrdinalIgnoreCase))
            return 0.3;

        // Anschluss-Codes (BC*) → mittlere Qualitaet
        if (code.StartsWith("BC", StringComparison.OrdinalIgnoreCase))
            return 0.7;

        // A-Codes (Streckenschaeden Start/Ende)
        if (code.StartsWith("A0", StringComparison.OrdinalIgnoreCase)
            || code.StartsWith("B0", StringComparison.OrdinalIgnoreCase))
            return 0.6;

        return 0.5;
    }

    /// <summary>Extrahiert Uhrzeitlage aus Beschreibungstext.</summary>
    private static string? ExtractClockPosition(string text)
    {
        var match = ClockRegex.Match(text);
        if (!match.Success) return null;

        var from = match.Groups[1].Value;
        var to = match.Groups[2].Success ? match.Groups[2].Value : null;

        return to != null ? $"{from} Uhr bis {to} Uhr" : $"{from} Uhr";
    }

    /// <summary>Findet alle Ordner die ein PDF-Protokoll enthalten.</summary>
    private static List<(string Folder, string PdfPath)> FindPdfFolders(string rootFolder)
    {
        var results = new List<(string, string)>();

        foreach (var dir in EnumerateAllDirs(rootFolder))
        {
            try
            {
                var pdfs = Directory.EnumerateFiles(dir, "*.pdf", SearchOption.TopDirectoryOnly).ToList();
                if (pdfs.Count == 0) continue;

                // Groesstes PDF nehmen (das ist typischerweise das Protokoll, nicht ein Deckblatt)
                var bestPdf = pdfs
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(fi => fi.Length)
                    .First().FullName;

                results.Add((dir, bestPdf));
            }
            catch { }
        }

        return results;
    }

    private static IEnumerable<string> EnumerateAllDirs(string root)
    {
        yield return root;
        foreach (var dir in Directory.EnumerateDirectories(root, "*", SearchOption.AllDirectories))
            yield return dir;
    }
}
