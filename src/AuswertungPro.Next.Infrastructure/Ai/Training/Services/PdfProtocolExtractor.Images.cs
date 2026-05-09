// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

using AuswertungPro.Next.Application.Ai.Training.Models;
using AuswertungPro.Next.Application.Imaging;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// PdfProtocolExtractor Bild-Extraktion: ExtractAndAssignPdfImages (PyMuPDF
// Primary, PdfPig Fallback), ExtractImagesViaPyMuPdf (Python-Subprocess fuer
// CMYK->RGB Konvertierung), ExtractImagesViaPdfPig (Position-aware Sortierung),
// IsLikelyLogoOrSymbol (Farbvielfalt-Heuristik gegen Logos). Aus dem Hauptdatei
// extrahiert (Slice 16b).
public sealed partial class PdfProtocolExtractor
{
    private static IReadOnlyList<GroundTruthEntry> ExtractAndAssignPdfImages(
        UglyToad.PdfPig.PdfDocument doc,
        IReadOnlyList<GroundTruthEntry> entries,
        string pdfPath,
        string framesDir)
    {
        // Diagnose-Log fuer Foto-Zuweisung
        void DiagLog(string msg)
        {
            try
            {
                var diagPath = Path.Combine(framesDir, "_diag_assignment.txt");
                File.AppendAllText(diagPath, $"{DateTime.Now:HH:mm:ss} {msg}\n");
            }
            catch { /* Diagnose darf nie crashen */ }
        }

        try
        {
            Directory.CreateDirectory(framesDir);
            DiagLog($"Start: {entries.Count} Eintraege, PDF={Path.GetFileName(pdfPath)}");

            // ── PyMuPDF-Extraktion (korrekte CMYK→RGB Konvertierung) ──
            // PyMuPDF filtert bereits in Python: Mindestgroesse, Seitenverhaeltnis,
            // Deduplizierung und is_likely_photo (Luminanz/Farbvarianz).
            // Der C#-Logo-Filter wird NUR fuer PdfPig-Fallback angewendet.
            var imagePaths = ExtractImagesViaPyMuPdf(pdfPath, framesDir);
            bool fromPyMuPdf = imagePaths.Count > 0;
            DiagLog($"PyMuPDF: {imagePaths.Count} Bilder");

            // Fallback: PdfPig-Extraktion wenn PyMuPDF fehlschlaegt
            if (imagePaths.Count == 0)
            {
                imagePaths = ExtractImagesViaPdfPig(doc, pdfPath, framesDir);
                DiagLog($"PdfPig-Fallback: {imagePaths.Count} Bilder");
            }

            // Logo-Filter nur fuer PdfPig-Bilder (PyMuPDF hat eigenen Filter in Python)
            if (!fromPyMuPdf && imagePaths.Count > 0)
            {
                var beforeFilter = imagePaths.Count;
                imagePaths = imagePaths
                    .Where(p => !IsLikelyLogoOrSymbol(File.ReadAllBytes(p), Path.GetExtension(p)))
                    .ToList();
                DiagLog($"Logo-Filter: {beforeFilter} → {imagePaths.Count} Bilder");
            }

            if (imagePaths.Count == 0)
            {
                DiagLog("ABBRUCH: 0 Bilder nach Logo-Filter");
                return entries;
            }

            // Zuordnung: Bilder den Entries zuweisen.
            //
            // Bei manchen Codes werden 2 Fotos pro Eintrag gemacht:
            //   BCA/BAG/BAH (Anschluss): Foto 1 = Axialansicht (codierbar),
            //     Foto 2 = 90° Einblick (NICHT codierbar → ueberspringen)
            //   BAB/BAC/BAF etc. (Schaeden): Foto 1 = Axial, Foto 2 = Nahaufnahme
            //     (beide nuetzlich → beide behalten)
            //
            // Strategie: Wenn mehr Bilder als Eintraege UND Anschluss-Codes vorhanden →
            // Einblick-Fotos bei Anschluss-Codes filtern. Sonst alle behalten.

            // Codes bei denen das zweite Foto (Einblick) verwirrend ist
            var anschlussCodesSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
                { "BCA", "BAG", "BAH" };

            var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfPath), @"[^\w\-]", "_");

            // Mehr Bilder als Eintraege → Anschluss-Einblick-Fotos rausfiltern
            var filteredImages = imagePaths.ToList();
            if (imagePaths.Count > entries.Count && entries.Count > 0)
            {
                // Zaehle wie viele Anschluss-Codes es gibt (die haben je 2 Fotos)
                int anschlussCount = entries.Count(e =>
                    e.VsaCode.Length >= 3
                    && anschlussCodesSet.Contains(e.VsaCode.Replace(".", "")[..3].ToUpperInvariant()));
                int expectedImages = entries.Count + anschlussCount; // Anschluss = 2 Fotos

                if (imagePaths.Count >= expectedImages - 2 && imagePaths.Count <= expectedImages + 2)
                {
                    // Bilder intelligent zuordnen: Bei Anschluss-Codes nur 1. Bild nehmen
                    filteredImages = new List<string>(entries.Count);
                    int imgIdx = 0;
                    foreach (var entry in entries)
                    {
                        if (imgIdx >= imagePaths.Count) break;
                        filteredImages.Add(imagePaths[imgIdx]);
                        imgIdx++;

                        // Bei Anschluss-Code: 2. Bild (Einblick) ueberspringen
                        bool isAnschluss = entry.VsaCode.Length >= 3
                            && anschlussCodesSet.Contains(entry.VsaCode.Replace(".", "")[..3].ToUpperInvariant());
                        if (isAnschluss && imgIdx < imagePaths.Count)
                            imgIdx++; // Einblick-Foto ueberspringen
                    }
                }
                else
                {
                    // Fallback: gleichmaessig verteilt
                    double ratio = (double)imagePaths.Count / entries.Count;
                    filteredImages = new List<string>(entries.Count);
                    for (int i = 0; i < entries.Count; i++)
                    {
                        int idx = (int)(i * ratio);
                        if (idx < imagePaths.Count)
                            filteredImages.Add(imagePaths[idx]);
                    }
                }
            }

            int assignable = Math.Min(filteredImages.Count, entries.Count);
            double coverageRatio = entries.Count > 0 ? (double)assignable / entries.Count : 0;
            DiagLog($"Zuweisung: {filteredImages.Count} gefilterte Bilder, {entries.Count} Eintraege, Coverage={coverageRatio:P0}");
            if (coverageRatio < 0.30 && Math.Abs(filteredImages.Count - entries.Count) > 3)
            {
                DiagLog($"ABBRUCH: Coverage zu niedrig ({coverageRatio:P0} < 30%)");
                System.Diagnostics.Debug.WriteLine(
                    $"[PdfExtractor] Zuordnung unsicher: {filteredImages.Count} Bilder vs {entries.Count} Eintraege " +
                    $"(Coverage {coverageRatio:P0}) in {Path.GetFileName(pdfPath)}");
                return entries;
            }

            var result = new List<GroundTruthEntry>(entries.Count);
            int assigned = 0;
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string? framePath = null;

                if (i < filteredImages.Count)
                {
                    var srcPath = filteredImages[i];
                    var targetName = $"{safeName}_{entry.VsaCode}_{entry.MeterStart:F1}m_{i}.png";
                    var targetPath = Path.Combine(framesDir, targetName);
                    try
                    {
                        if (srcPath != targetPath)
                        {
                            if (File.Exists(targetPath)) File.Delete(targetPath);
                            File.Move(srcPath, targetPath);
                        }
                        framePath = targetPath;
                        assigned++;
                    }
                    catch (Exception ex)
                    {
                        DiagLog($"  Move FEHLER [{i}]: {ex.GetType().Name}: {ex.Message}");
                        framePath = File.Exists(srcPath) ? srcPath : null;
                        if (framePath != null) assigned++;
                    }
                }

                result.Add(entry with { ExtractedFramePath = framePath });
            }

            DiagLog($"Ergebnis: {assigned}/{entries.Count} Fotos zugewiesen");
            return result;
        }
        catch (Exception ex)
        {
            DiagLog($"EXCEPTION: {ex.GetType().Name}: {ex.Message}");
            return entries;
        }
    }

    /// <summary>
    /// Extrahiert Fotos per PyMuPDF (Python-Subprocess).
    /// Konvertiert CMYK→RGB korrekt — WinCan/IKAS-PDFs haben oft CMYK-JPEGs.
    /// </summary>
    private static IReadOnlyList<string> ExtractImagesViaPyMuPdf(string pdfPath, string framesDir)
    {
        try
        {
            var scriptPath = GetPyMuPdfScriptPath();
            if (!File.Exists(scriptPath))
            {
                System.Diagnostics.Debug.WriteLine($"[PdfExtractor] PyMuPDF-Script nicht gefunden: {scriptPath}");
                return Array.Empty<string>();
            }

            // Phase D2.3: ProcessRunner — sicherer ArgumentList + asynchroner Drain
            // (verhindert Deadlock bei vollem stderr) + Tree-Kill bei Timeout.
            var result = AuswertungPro.Next.Application.Common.ProcessRunner.RunAsync(
                fileName: "python",
                arguments: [scriptPath, pdfPath, framesDir,
                            MinPhotoWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                            MinPhotoHeight.ToString(System.Globalization.CultureInfo.InvariantCulture)],
                timeout: TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

            if (result.TimedOut)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PdfExtractor] PyMuPDF Timeout nach 30s fuer {Path.GetFileName(pdfPath)}");
                return Array.Empty<string>();
            }

            if (!result.IsSuccess || string.IsNullOrWhiteSpace(result.Stdout))
            {
                if (!string.IsNullOrWhiteSpace(result.Stderr))
                    System.Diagnostics.Debug.WriteLine(
                        $"[PdfExtractor] PyMuPDF stderr: {result.Stderr[..Math.Min(result.Stderr.Length, 300)]}");
                return Array.Empty<string>();
            }

            // JSON parsen: [{"page": 1, "index": 0, "path": "...", "width": 788, "height": 576}, ...]
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(result.Stdout);
            if (jsonDoc.RootElement.ValueKind == System.Text.Json.JsonValueKind.Object
                && jsonDoc.RootElement.TryGetProperty("error", out _))
                return Array.Empty<string>(); // Fehler-Objekt

            var paths = new List<string>();
            foreach (var item in jsonDoc.RootElement.EnumerateArray())
            {
                var path = item.GetProperty("path").GetString();
                if (path != null && File.Exists(path))
                    paths.Add(path);
            }

            return paths;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PdfExtractor] PyMuPDF fehlgeschlagen: {ex.Message}");
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Fallback: PdfPig-Bildextraktion (ohne Farbraum-Konvertierung).
    /// Wird nur verwendet wenn PyMuPDF nicht verfuegbar ist.
    /// </summary>
    private static IReadOnlyList<string> ExtractImagesViaPdfPig(
        UglyToad.PdfPig.PdfDocument doc, string pdfPath, string framesDir)
    {
        var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfPath), @"[^\w\-]", "_");
        var paths = new List<string>();
        var seenSizes = new HashSet<int>();
        int imgCounter = 0;

        foreach (var page in doc.GetPages())
        {
            foreach (var img in page.GetImages())
            {
                int w = (int)img.WidthInSamples;
                int h = (int)img.HeightInSamples;
                if (w < MinPhotoWidth || h < MinPhotoHeight) continue;
                if (w > MaxPhotoDimension || h > MaxPhotoDimension) continue;
                double aspect = (double)w / h;
                if (aspect < MinAspect || aspect > MaxAspect) continue;

                byte[]? photoBytes = null;
                string ext = ".jpg";
                try
                {
                    var raw = img.RawBytes;
                    if (raw.Count >= MinPhotoBytes && raw.Count >= 3
                        && raw[0] == 0xFF && raw[1] == 0xD8 && raw[2] == 0xFF)
                    {
                        photoBytes = raw.ToArray();
                    }
                }
                catch (Exception ex)
                {
                    // Phase 1.2: Empty-catch-Sweep — Fallback-Pfad probiert TryGetPng,
                    // aber Photo-Extraktions-Bugs sollen sichtbar sein.
                    Debug.WriteLine($"[PdfProtocolExtractor] RawBytes-JPG-Probe fehlgeschlagen: {ex.GetType().Name}: {ex.Message}");
                }

                if (photoBytes == null && img.TryGetPng(out var pngBytes) && pngBytes.Length >= MinPhotoBytes)
                {
                    photoBytes = pngBytes;
                    ext = ".png";
                }

                if (photoBytes == null) continue;
                if (!seenSizes.Add(photoBytes.Length)) continue;

                // Logos/Symbole filtern: echte Kanalfotos haben viele Farben
                if (IsLikelyLogoOrSymbol(photoBytes, ext))
                    continue;

                var fileName = $"{safeName}_fallback_{imgCounter++}{ext}";
                var filePath = Path.Combine(framesDir, fileName);
                File.WriteAllBytes(filePath, photoBytes);
                paths.Add(filePath);
            }
        }
        return paths;
    }

    /// <summary>
    /// Erkennt Logos, Symbole und geometrische Grafiken anhand der Farbvielfalt.
    /// Echte Kanalfotos haben tausende verschiedene Farben (natuerliche Szene).
    /// Logos/Symbole haben typisch 2-50 verschiedene Farben (Vektorgrafik/Flaechen).
    /// </summary>
    private static bool IsLikelyLogoOrSymbol(byte[] imageBytes, string ext)
    {
        try
        {
            // Phase 5.3 Sub-A: Bitmap-Decode via Application-Adapter (WPF entkoppelt).
            // DecodePixelWidth=100 fuer Speed: kleine Auflösung reicht zur Farbzaehlung.
            var decoded = AuswertungPro.Next.Application.Imaging.ImagePixelDecoderProvider
                .TryDecode(imageBytes, maxWidth: 100);
            if (decoded is null) return false; // Decode fehlgeschlagen → durchlassen

            var pixels = decoded.Bgra32Pixels;

            // Eindeutige Farben zaehlen (auf 5-Bit quantisiert fuer Robustheit)
            var colors = new HashSet<int>();
            for (int i = 0; i < pixels.Length - 3; i += 4)
            {
                // Quantisieren: 256 Farben → 32 Stufen pro Kanal (BGRA: B=i, G=i+1, R=i+2)
                int r = pixels[i + 2] >> 3;
                int g = pixels[i + 1] >> 3;
                int b = pixels[i] >> 3;
                colors.Add((r << 10) | (g << 5) | b);
            }

            // Weniger als MinUniqueColors → wahrscheinlich Logo/Symbol
            return colors.Count < MinUniqueColors;
        }
        catch
        {
            return false; // Im Zweifel durchlassen
        }
    }
}
