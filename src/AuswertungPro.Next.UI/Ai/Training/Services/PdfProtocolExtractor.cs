// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.UI.Ai.Training.Models;
using AuswertungPro.Next.UI.Services.CodeCatalog;

namespace AuswertungPro.Next.UI.Ai.Training.Services;

/// <summary>
/// Extrahiert <see cref="GroundTruthEntry"/>-Einträge aus einem Kanalinspektion-PDF.
///
/// Strategien (in Priorität):
/// 1. IKAS Leitungsgrafik: "Entf. Kode Foto Video Beschreibung" (Zeit VOR Text)
/// 2. Fretz/IBAK-Tabelle: "Foto Zeit Meter Code Beschreibung" (Zeit VOR Meter)
/// 3. Tabellenzeilen mit Timestamp am Ende (Zeit NACH Text)
/// 4. IKAS Bildbericht: Label-Value Blöcke (Zustand/Entf./Video)
/// 5. Regelbasiertes Fallback (Bereichsmuster / Einzelmeter)
/// 6. JSON-Protokolldatei als direkter Fallback
/// </summary>
public sealed class PdfProtocolExtractor
{
    // ── Regex-Muster ────────────────────────────────────────────────────────

    // VSA-Code: 2-6 Buchstaben, optional mit Punkt-Suffixen (.A, .C, .AB, .Y.B)
    private const string CodePattern = @"[A-Z]{2,6}(?:\.[A-Z]{1,2})*";

    // IKAS Leitungsgrafik: "[meter]  [CODE]  [foto?]  [HH:MM:SS]  [text]"
    // Zeit kommt VOR der Beschreibung
    private static readonly Regex IkasTablePattern = new(
        $@"^[ \t]*(?<meter>\d{{1,4}}[.,]\d{{1,3}})[ \t]+(?<code>{CodePattern})[ \t]+(?:\d{{1,5}}[ \t]+)?(?<time>\d{{2}}:\d{{2}}:\d{{2}})[ \t]+(?<text>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // IKAS Fortsetzungszeile (kein Meter): "[CODE]  [foto?]  [HH:MM:SS]  [text]"
    private static readonly Regex IkasContinuationPattern = new(
        $@"^[ \t]{{4,}}(?<code>{CodePattern})[ \t]+(?:\d{{1,5}}[ \t]+)?(?<time>\d{{2}}:\d{{2}}:\d{{2}})[ \t]+(?<text>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Fretz-Format: "[Foto?]  [HH:MM:SS]  [meter]  [CODE]  [text]"
    // Foto-Nummer und Timestamp kommen VOR dem Meterwert (Fretz/IBAK-PDFs)
    private static readonly Regex FretzTablePattern = new(
        $@"^[ \t]*(?:\d{{1,5}}[ \t]+)?(?<time>\d{{2}}:\d{{2}}:\d{{2}})[ \t]+(?<meter>\d{{1,4}}[.,]\d{{1,3}})[ \t]+(?<code>{CodePattern})[ \t]+(?<text>[^\r\n]+)",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Standard-Format: "[meter]  [CODE]  [text...]  [HH:MM:SS]"
    // Optional: Operator-Code (z.B. A01, B01) zwischen Meter und VSA-Code (KIT Bauinspekt)
    // Zeit kommt NACH der Beschreibung
    private static readonly Regex TableRowPattern = new(
        $@"^[ \t]*(?<meter>\d{{1,4}}[.,]\d{{1,3}})[ \t]+(?:[A-Z]\d{{1,3}}[ \t]+)?(?<code>{CodePattern})[ \t]+(?<text>[^\r\n]+?)[ \t]+(?<time>\d{{2}}:\d{{2}}:\d{{2}})\b",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // IKAS Bildbericht: Label-Value Blöcke mit "Zustand" + "Entf."
    private static readonly Regex BildberichtCodePattern = new(
        $@"^\s*Zustand\s+(?<code>{CodePattern})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    // Meter mit "Entf." - Flie.{1,2}r statt Flie.r wegen Multi-Byte-Encoding (z.B. IKAS ¦ → Â¦)
    private static readonly Regex BildberichtMeterPattern = new(
        @"^\s*Entf\.?\s+(?:gegen\s+Flie.{1,2}r\.?\s+)?(?<meter>\d{1,4}[.,]\d{1,3})\s*m\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);
    private static readonly Regex BildberichtVideoPattern = new(
        @"^\s*Video\s+(?<time>\d{2}:\d{2}:\d{2})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Fallback: "12.45 BAB B Querriss..." oder "@12.45m BAB Querriss"
    private static readonly Regex EntryPattern = new(
        $@"@?(?<m1>\d{{1,4}}[.,]\d{{1,3}})\s*m?\s*[-–]?\s*(?<m2>\d{{1,4}}[.,]\d{{1,3}})?\s*m?\s+(?<code>{CodePattern})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{{3,}})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Fallback: "@12.45 BAB ..."
    private static readonly Regex SingleMeterPattern = new(
        $@"@?(?<m>\d{{1,4}}[.,]\d{{1,3}})\s*m?\s+(?<code>{CodePattern})(?:\s+(?<char>[ABCD]))?\s+(?<text>[^\r\n]{{3,}})",
        RegexOptions.Compiled | RegexOptions.Multiline | RegexOptions.IgnoreCase);

    // Quantifizierung: "3mm", "15%", "5 cm"
    private static readonly Regex QuantPattern = new(
        @"(?<val>\d+(?:[.,]\d+)?)\s*(?<unit>mm|cm|%|Stück|Stueck)",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    // ── Öffentliche API ─────────────────────────────────────────────────────

    /// <param name="filePath">Pfad zur Protokoll-Datei (PDF oder JSON).</param>
    /// <param name="framesDir">Optionaler Ordner zum Speichern extrahierter Bildbericht-Fotos.</param>
    /// <param name="ct">Cancellation-Token.</param>
    public Task<IReadOnlyList<GroundTruthEntry>> ExtractAsync(
        string filePath,
        string? framesDir = null,
        CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
            return Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>());

        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        return ext switch
        {
            ".json" => Task.FromResult(ExtractFromJson(filePath)),
            ".pdf"  => Task.FromResult(ExtractFromPdf(filePath, framesDir)),
            _       => Task.FromResult<IReadOnlyList<GroundTruthEntry>>(Array.Empty<GroundTruthEntry>())
        };
    }

    // ── JSON ────────────────────────────────────────────────────────────────

    private static IReadOnlyList<GroundTruthEntry> ExtractFromJson(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            using var doc = System.Text.Json.JsonDocument.Parse(json);

            var results = new List<GroundTruthEntry>();

            if (doc.RootElement.TryGetProperty("Current", out var current)
                && current.TryGetProperty("Entries", out var entries))
            {
                foreach (var entry in entries.EnumerateArray())
                {
                    var gtEntry = ParseJsonEntry(entry);
                    if (gtEntry is not null)
                        results.Add(gtEntry);
                }
            }

            return results;
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    private static GroundTruthEntry? ParseJsonEntry(System.Text.Json.JsonElement e)
    {
        if (e.TryGetProperty("IsDeleted", out var deleted) && deleted.GetBoolean())
            return null;

        var rawCode = e.TryGetProperty("Code", out var c) ? c.GetString() : null;
        if (string.IsNullOrWhiteSpace(rawCode)) return null;
        var code = NormalizeVsaCode(rawCode.Trim().ToUpperInvariant());
        if (code is null) return null; // Nicht trainingsrelevant

        var text  = e.TryGetProperty("Beschreibung", out var t) ? t.GetString() ?? "" : "";
        var mStart = e.TryGetProperty("MeterStart", out var ms) && ms.ValueKind != System.Text.Json.JsonValueKind.Null
            ? ms.GetDouble() : 0.0;
        var mEnd = e.TryGetProperty("MeterEnd", out var me) && me.ValueKind != System.Text.Json.JsonValueKind.Null
            ? me.GetDouble() : mStart;
        var isStreck = e.TryGetProperty("IsStreckenschaden", out var iss) && iss.GetBoolean();

        return new GroundTruthEntry
        {
            VsaCode          = code,
            Text             = text,
            MeterStart       = mStart,
            MeterEnd         = mEnd,
            IsStreckenschaden = isStreck
        };
    }

    // ── PDF ─────────────────────────────────────────────────────────────────

    /// <summary>Dateinamen-Muster die KEINE Inspektionsprotokolle sind.</summary>
    private static readonly string[] NonProtocolKeywords =
        ["faktura", "rechnung", "offerte", "angebot", "lieferschein",
         "quittung", "mahnung", "vertrag", "auftrag", "kostenvor"];

    private static IReadOnlyList<GroundTruthEntry> ExtractFromPdf(string path, string? framesDir)
    {
        try
        {
            // Rechnungen, Offerten, Lieferscheine etc. ueberspringen
            var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (NonProtocolKeywords.Any(kw => fileName.Contains(kw)))
                return Array.Empty<GroundTruthEntry>();

            using var doc = UglyToad.PdfPig.PdfDocument.Open(path);

            var text = ExtractTextFromPdfDoc(doc);
            if (string.IsNullOrWhiteSpace(text))
                return Array.Empty<GroundTruthEntry>();

            // Diagnose: extrahierten Text speichern
            try
            {
                var diagDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "AuswertungPro", "diag");
                Directory.CreateDirectory(diagDir);
                var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(path), @"[^\w\-]", "_");
                File.WriteAllText(
                    Path.Combine(diagDir, $"pdf_text_{safeName}.txt"),
                    text);
            }
            catch { /* Diagnose ist optional */ }

            var entries = ParseEntriesFromText(text);

            // Fotos aus PDF-Bildbericht extrahieren und Einträgen zuordnen
            if (entries.Count > 0 && !string.IsNullOrWhiteSpace(framesDir))
            {
                entries = ExtractAndAssignPdfImages(doc, entries, path, framesDir);
            }

            return entries;
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    private static string ExtractTextFromPdfDoc(UglyToad.PdfPig.PdfDocument doc)
    {
        var sb = new System.Text.StringBuilder();

        foreach (var page in doc.GetPages())
        {
            var letters = page.Letters;
            if (letters.Count == 0)
                continue;

            var avgW = letters
                .Where(l => l.Width > 0 && l.Value?.Length == 1 && !char.IsWhiteSpace(l.Value[0]))
                .Select(l => l.Width)
                .DefaultIfEmpty(5.5)
                .Average();
            if (avgW < 0.5) avgW = 5.5;

            var lineGroups = letters
                .GroupBy(l => Math.Round(l.StartBaseLine.Y / 2.0) * 2.0)
                .OrderByDescending(g => g.Key);

            foreach (var lineGroup in lineGroups)
            {
                var sorted = lineGroup.OrderBy(l => l.StartBaseLine.X).ToList();
                if (sorted.Count == 0) continue;

                var line = new System.Text.StringBuilder();

                var indent = (int)(sorted[0].StartBaseLine.X / avgW);
                if (indent > 0)
                    line.Append(new string(' ', Math.Min(indent, 30)));

                double prevEndX = sorted[0].StartBaseLine.X;

                foreach (var letter in sorted)
                {
                    var gap = letter.StartBaseLine.X - prevEndX;
                    if (gap > avgW * 0.5)
                    {
                        var nSpaces = Math.Max(1, (int)Math.Round(gap / avgW));
                        line.Append(new string(' ', Math.Min(nSpaces, 80)));
                    }
                    var v = letter.Value ?? string.Empty;
                    line.Append(v);
                    prevEndX = letter.StartBaseLine.X + (letter.Width > 0 ? letter.Width : avgW);
                }

                var lineStr = line.ToString().TrimEnd();
                // Zeilen mit Inhalt beibehalten (auch wenn nur Steuerzeichen enthalten)
                if (lineStr.Any(c => !char.IsWhiteSpace(c)))
                    sb.AppendLine(lineStr);
            }
        }

        // Erkennung und Korrektur von Custom-Font-Encoding (z.B. IKAS-PDFs)
        var rawText = sb.ToString();
        return TryDecodeShiftedText(rawText);
    }

    // ── Font-Encoding-Korrektur ──────────────────────────────────────────────

    /// <summary>
    /// Erkennt PDFs mit verschobener Zeichencodierung (Custom Font Encoding)
    /// und korrigiert den Text automatisch. Manche PDF-Generatoren (z.B. IKAS)
    /// verwenden Schriften, bei denen alle Zeichen um einen festen Offset
    /// verschoben sind. PdfPig kann diese nicht korrekt decodieren.
    /// </summary>
    private static string TryDecodeShiftedText(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        string[] knownWords =
        {
            "Leitung", "Video", "Foto", "Zustand", "Material",
            "Schacht", "Kanal", "Haltung", "Inspektion", "Dimension",
            "Profil", "Rohr", "Position", "Entf", "Strasse", "Wetter"
        };

        int existingMatches = CountWordMatches(text, knownWords);
        if (existingMatches >= 3)
            return text;

        int bestShift = 0;
        int bestCount = existingMatches;

        for (int shift = 1; shift <= 60; shift++)
        {
            var decoded = ShiftAllChars(text, shift);
            int count = CountWordMatches(decoded, knownWords);
            if (count > bestCount)
            {
                bestCount = count;
                bestShift = shift;
            }
        }

        if (bestShift > 0 && bestCount >= 3)
            return ShiftAllChars(text, bestShift);

        return text;
    }

    private static int CountWordMatches(string text, string[] words)
    {
        int count = 0;
        foreach (var word in words)
        {
            if (text.Contains(word, StringComparison.OrdinalIgnoreCase))
                count++;
        }
        return count;
    }

    private static string ShiftAllChars(string text, int shift)
    {
        var sb = new System.Text.StringBuilder(text.Length);
        foreach (var ch in text)
        {
            if (ch == '\r' || ch == '\n' || ch == '\t' || ch == ' ')
                sb.Append(ch);
            else
                sb.Append((char)(ch + shift));
        }
        return sb.ToString();
    }

    // ── Bild-Extraktion aus PDF ─────────────────────────────────────────────

    // Filter-Konstanten fuer echte Inspektionsfotos
    // Echte Kanalfotos: min 640x480 (PAL) oder 1920x1080 (HD), typisch 4:3 oder 16:9
    // Logos/Banner: klein, breite Seitenverhaeltnisse, wiederholen sich auf jeder Seite
    // PDF-Seitenrender: sehr gross (3508x2480 = A4 @ 300dpi), keine echten Fotos
    private const int MinPhotoWidth = 400;
    private const int MinPhotoHeight = 300;
    private const int MaxPhotoDimension = 2500;     // Echte Fotos ≤ 1920px, PDF-Render > 3000px
    private const double MinAspect = 1.15;           // Echte Fotos 4:3=1.33, Leitungsgrafiken ~1.12
    private const double MaxAspect = 2.0;            // 16:9 = 1.78, Logos oft > 2.0
    private const int MinPhotoBytes = 30_000;        // 30KB — echte JPEGs aus Video > 50KB
    private const int MinUniqueColors = 500;         // Echte Fotos > 1000 Farben, Logos/Symbole < 100

    /// <summary>
    /// Extrahiert echte Inspektionsfotos aus dem PDF-Bildbericht und ordnet sie den Eintraegen zu.
    /// Filtert Logos, Icons, Banner, PDF-Seitenrender und Querschnitt-Zeichnungen heraus.
    ///
    /// Filter-Kriterien:
    /// - Min 400x300 Pixel (echte Fotos sind mind. PAL-Aufloesung)
    /// - Max 2500px pro Dimension (PDF-Seitenrender sind 3508x2480+)
    /// - Seitenverhaeltnis 1.1–2.0 (echte Fotos: 4:3=1.33 oder 16:9=1.78; Logos oft >2.0)
    /// - Min 30KB Bilddaten (Logos/Icons sind kleiner)
    /// - Deduplizierung: Identische Bilder (Logos auf jeder Seite) werden entfernt
    /// - JPEG-Bilder bevorzugt (echte Inspektionsfotos sind fast immer JPEG)
    ///
    /// Zuordnung: Strikte 1:1 wenn Bildanzahl == Entryanzahl, sonst keine Zuordnung.
    /// </summary>
    /// <summary>
    /// Pfad zum Python-Helper-Script fuer PyMuPDF-Bildextraktion.
    /// PyMuPDF konvertiert CMYK→RGB korrekt, im Gegensatz zu PdfPig/WPF.
    /// </summary>
    private static string GetPyMuPdfScriptPath()
    {
        var baseDir = Path.GetDirectoryName(typeof(PdfProtocolExtractor).Assembly.Location)
                      ?? AppContext.BaseDirectory;

        // Build-Output: Ai/Training/Scripts/ (csproj-Struktur wird beibehalten)
        var scriptPath = Path.Combine(baseDir, "Ai", "Training", "Scripts", "extract_pdf_images.py");
        if (File.Exists(scriptPath)) return scriptPath;

        // Fallback: direkt unter Scripts/
        scriptPath = Path.Combine(baseDir, "Scripts", "extract_pdf_images.py");
        if (File.Exists(scriptPath)) return scriptPath;

        // Fallback: AppContext.BaseDirectory
        scriptPath = Path.Combine(AppContext.BaseDirectory, "Ai", "Training", "Scripts", "extract_pdf_images.py");
        if (File.Exists(scriptPath)) return scriptPath;

        return Path.Combine(baseDir, "Ai", "Training", "Scripts", "extract_pdf_images.py");
    }

    private static IReadOnlyList<GroundTruthEntry> ExtractAndAssignPdfImages(
        UglyToad.PdfPig.PdfDocument doc,
        IReadOnlyList<GroundTruthEntry> entries,
        string pdfPath,
        string framesDir)
    {
        try
        {
            Directory.CreateDirectory(framesDir);

            // ── PyMuPDF-Extraktion (korrekte CMYK→RGB Konvertierung) ──
            var imagePaths = ExtractImagesViaPyMuPdf(pdfPath, framesDir);

            // Fallback: PdfPig-Extraktion wenn PyMuPDF fehlschlaegt
            if (imagePaths.Count == 0)
                imagePaths = ExtractImagesViaPdfPig(doc, pdfPath, framesDir);

            // Logos/Symbole filtern (geometrische Grafiken, wenige Farben)
            imagePaths = imagePaths
                .Where(p => !IsLikelyLogoOrSymbol(File.ReadAllBytes(p), Path.GetExtension(p)))
                .ToList();

            if (imagePaths.Count == 0)
                return entries;

            // Zuordnung: Bilder den Entries zuweisen (1:1 nach Index)
            int assignable = Math.Min(imagePaths.Count, entries.Count);
            double coverageRatio = entries.Count > 0 ? (double)assignable / entries.Count : 0;
            if (coverageRatio < 0.30 && Math.Abs(imagePaths.Count - entries.Count) > 3)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[PdfExtractor] Zuordnung unsicher: {imagePaths.Count} Bilder vs {entries.Count} Eintraege " +
                    $"(Coverage {coverageRatio:P0}) in {Path.GetFileName(pdfPath)}");
                return entries;
            }

            var safeName = Regex.Replace(Path.GetFileNameWithoutExtension(pdfPath), @"[^\w\-]", "_");
            var result = new List<GroundTruthEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                var entry = entries[i];
                string? framePath = null;

                if (i < imagePaths.Count)
                {
                    var srcPath = imagePaths[i];
                    // Umbenennen: Code + Meterposition im Dateinamen
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
                    }
                    catch
                    {
                        framePath = File.Exists(srcPath) ? srcPath : null;
                    }
                }

                result.Add(entry with { ExtractedFramePath = framePath });
            }

            return result;
        }
        catch
        {
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

            var psi = new System.Diagnostics.ProcessStartInfo
            {
                FileName = "python",
                Arguments = $"\"{scriptPath}\" \"{pdfPath}\" \"{framesDir}\" {MinPhotoWidth} {MinPhotoHeight}",
                UseShellExecute = false,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            using var p = System.Diagnostics.Process.Start(psi);
            if (p == null) return Array.Empty<string>();

            var output = p.StandardOutput.ReadToEnd();
            p.WaitForExit(30_000); // Max 30 Sekunden

            if (p.ExitCode != 0 || string.IsNullOrWhiteSpace(output))
                return Array.Empty<string>();

            // JSON parsen: [{"page": 1, "index": 0, "path": "...", "width": 788, "height": 576}, ...]
            using var jsonDoc = System.Text.Json.JsonDocument.Parse(output);
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
                catch { }

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

    // ── Parsing ─────────────────────────────────────────────────────────────

    // Strategie 0: Mehrzeiliges Spalten-Format (KIT Bauinspekt / Fretz neue PDFs)
    // Meter + optional OP-Code + VSA-Code auf einer Zeile, Text auf der naechsten.
    // Beispiel:
    //   0.00
    //   BCD
    //   Rohranfang
    //   00:00:00 35644-06...
    // Oder: "9.60 A01 BDDC\nWasserspiegel...\n00:03:59"
    private static readonly Regex MultiLineHeaderPattern = new(
        $@"^[ \t]*(?<meter>\d{{1,4}}[.,]\d{{1,3}})(?:[ \t]+[A-Z]\d{{1,3}})?[ \t]+(?<code>{CodePattern})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    // Variante: Meter allein auf einer Zeile, Code auf der naechsten
    private static readonly Regex MeterAlonePattern = new(
        @"^[ \t]*(?<meter>\d{1,4}[.,]\d{1,3})\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline);

    private static IReadOnlyList<GroundTruthEntry> ParseEntriesFromText(string text)
    {
        var results = new List<GroundTruthEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Strategie 0: Mehrzeiliges Spalten-Format
        // Erkennung: Wenn der Text "m +\n" oder "m +" gefolgt von Spaltenheadern hat
        // UND Zeilen mit nur Meter oder Meter+Code existieren
        if (text.Contains("m +") || text.Contains("m+"))
        {
            var multiLineResults = ParseMultiLineTable(text, seen);
            if (multiLineResults.Count > 0)
                return multiLineResults;
        }

        // Strategie 1: IKAS Leitungsgrafik (Zeit VOR Beschreibung)
        // Format: "0.00  BCD  [1777]  00:00:09  Rohranfang"
        var lastMeter = 0.0;
        foreach (Match m in IkasTablePattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                m.Groups["code"].Value,
                "",
                m.Groups["text"].Value,
                ParseTimestamp(m.Groups["time"].Value));

            if (entry is not null && seen.Add(Sig(entry)))
            {
                results.Add(entry);
                lastMeter = entry.MeterStart;
            }
        }

        // IKAS Fortsetzungszeilen (kein Meter → vorherigen Meter verwenden)
        if (results.Count > 0)
        {
            foreach (Match m in IkasContinuationPattern.Matches(text))
            {
                // Prüfen ob diese Zeile nicht schon als Hauptzeile gematcht wurde
                var code = m.Groups["code"].Value.Trim().ToUpperInvariant();
                var time = ParseTimestamp(m.Groups["time"].Value);
                var textVal = m.Groups["text"].Value.Trim();

                // Finde den nächsten bekannten Meter davor
                var meter = FindPrecedingMeter(text, m.Index, lastMeter);

                var entry = BuildEntryDirect(meter, meter, code, textVal, time);
                if (entry is not null && seen.Add(Sig(entry)))
                    results.Add(entry);
            }
        }

        if (results.Count > 0)
            return results;

        // Strategie 2: Fretz-Tabellenformat (Foto + Zeit VOR Meter)
        // Format: "040  00:00:16  0.00  BCD  Rohranfang"
        foreach (Match m in FretzTablePattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                m.Groups["code"].Value,
                "",
                m.Groups["text"].Value,
                ParseTimestamp(m.Groups["time"].Value));

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        // Fretz-PDFs haben auch Zeilen ohne Timestamp (z.B. "27.70 BCE Rohrende").
        // Diese werden weiter unten durch die Fallback-Patterns ergaenzt.
        // Deshalb hier KEIN fruehes Return — stattdessen weiter zu Strategie 5/6.
        if (results.Count > 0)
            goto fretzFallback;

        // Strategie 3: Standard-Tabellenformat (Zeit NACH Beschreibung, z.B. WinCan)
        // Format: "2.24  BCCBA  Beschreibung...  00:01:07"
        foreach (Match m in TableRowPattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["meter"].Value,
                "",
                m.Groups["code"].Value,
                "",
                m.Groups["text"].Value,
                ParseTimestamp(m.Groups["time"].Value));

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        if (results.Count > 0)
            return results;

        // Strategie 4: IKAS Bildbericht (Label-Value Blöcke)
        results = ParseBildberichtBlocks(text, seen);
        if (results.Count > 0)
            return results;

        // Strategie 5: Bereichs-Muster (m1 – m2 CODE)
        // Wird auch als Ergaenzung nach Fretz-Strategie erreicht (fretzFallback),
        // um Zeilen ohne Timestamp zu erfassen (z.B. "27.70  BCE  Rohrende").
        fretzFallback:
        foreach (Match m in EntryPattern.Matches(text))
        {
            var entry = BuildEntry(
                m.Groups["m1"].Value,
                m.Groups["m2"].Value,
                m.Groups["code"].Value,
                m.Groups["char"].Value,
                m.Groups["text"].Value,
                null);

            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        // Strategie 6: Einzel-Meter-Muster (@m CODE)
        if (results.Count == 0)
        {
            foreach (Match m in SingleMeterPattern.Matches(text))
            {
                var entry = BuildEntry(
                    m.Groups["m"].Value,
                    "",
                    m.Groups["code"].Value,
                    m.Groups["char"].Value,
                    m.Groups["text"].Value,
                    null);

                if (entry is not null && seen.Add(Sig(entry)))
                    results.Add(entry);
            }
        }

        return results;
    }

    /// <summary>
    /// Strategie 0: Mehrzeiliges Spalten-Format (KIT Bauinspekt / Fretz neue PDFs).
    /// PDF-Text hat Meter+Code auf einer Zeile, Beschreibung auf der naechsten.
    /// Erkennt auch: Meter allein → Code auf naechster Zeile → Text → Zeit.
    /// </summary>
    private static List<GroundTruthEntry> ParseMultiLineTable(string text, HashSet<string> seen)
    {
        var results = new List<GroundTruthEntry>();
        var lines = text.Split('\n').Select(l => l.TrimEnd('\r')).ToArray();
        var codeRegex = new Regex($@"^\s*(?<code>{CodePattern})\s*$", RegexOptions.Compiled);
        var meterRegex = new Regex(@"^\s*(?<meter>\d{1,4}[.,]\d{1,3})(?:\s+[A-Z]\d{1,3})?\s*$", RegexOptions.Compiled);
        var meterCodeRegex = new Regex($@"^\s*(?<meter>\d{1,4}[.,]\d{1,3})(?:\s+[A-Z]\d{{1,3}})?\s+(?<code>{CodePattern})\s*$", RegexOptions.Compiled);
        var timeRegex = new Regex(@"(?<time>\d{2}:\d{2}:\d{2})", RegexOptions.Compiled);

        for (int i = 0; i < lines.Length; i++)
        {
            string? meterStr = null;
            string? code = null;
            int textLineStart = -1;

            // Variante A: Meter + Code auf gleicher Zeile
            var mcMatch = meterCodeRegex.Match(lines[i]);
            if (mcMatch.Success)
            {
                meterStr = mcMatch.Groups["meter"].Value;
                code = mcMatch.Groups["code"].Value;
                textLineStart = i + 1;
            }
            else
            {
                // Variante B: Meter allein, Code auf naechster Zeile
                var mMatch = meterRegex.Match(lines[i]);
                if (mMatch.Success && i + 1 < lines.Length)
                {
                    var cMatch = codeRegex.Match(lines[i + 1]);
                    if (cMatch.Success)
                    {
                        meterStr = mMatch.Groups["meter"].Value;
                        code = cMatch.Groups["code"].Value;
                        textLineStart = i + 2;
                    }
                }
            }

            if (meterStr == null || code == null || textLineStart >= lines.Length)
                continue;

            // Text sammeln: alles bis zur naechsten Zeile mit Timestamp oder naechstem Meter/Code
            var textParts = new List<string>();
            TimeSpan? zeit = null;
            for (int j = textLineStart; j < Math.Min(textLineStart + 5, lines.Length); j++)
            {
                var line = lines[j].Trim();
                if (string.IsNullOrWhiteSpace(line)) break;

                // Timestamp gefunden → Zeit merken und aufhoeren
                var tMatch = timeRegex.Match(line);
                if (tMatch.Success)
                {
                    zeit = ParseTimestamp(tMatch.Groups["time"].Value);
                    break;
                }

                // Naechster Meter oder Code → aufhoeren (gehoert zum naechsten Eintrag)
                if (meterRegex.IsMatch(line) || meterCodeRegex.IsMatch(line))
                    break;

                // "Stufe" / "Seite" Zeilen ueberspringen
                if (line.StartsWith("Stufe", StringComparison.OrdinalIgnoreCase)
                    || line.StartsWith("Seite", StringComparison.OrdinalIgnoreCase))
                    break;

                textParts.Add(line);
            }

            var beschreibung = string.Join(" ", textParts).Trim();
            if (beschreibung.Length < 2) beschreibung = code;

            var entry = BuildEntry(meterStr, "", code, "", beschreibung, zeit);
            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Parst IKAS Bildbericht-Seiten: Blöcke mit Zustand/Entf./Video Labels.
    /// Findet den jeweils NÄCHSTEN Entf.- und Video-Match zu jedem Zustand-Match.
    /// </summary>
    private static List<GroundTruthEntry> ParseBildberichtBlocks(string text, HashSet<string> seen)
    {
        var results = new List<GroundTruthEntry>();

        var codeMatches = BildberichtCodePattern.Matches(text);
        var meterMatches = BildberichtMeterPattern.Matches(text);
        var videoMatches = BildberichtVideoPattern.Matches(text);

        foreach (Match cm in codeMatches)
        {
            var code = cm.Groups["code"].Value.Trim().ToUpperInvariant();
            var pos = cm.Index;

            double meter = 0;
            TimeSpan? zeit = null;

            // Nächsten Meter-Match finden (minimale Distanz innerhalb 500 Zeichen)
            int bestMeterDist = int.MaxValue;
            foreach (Match mm in meterMatches)
            {
                var dist = Math.Abs(mm.Index - pos);
                if (dist < bestMeterDist && dist < 500)
                {
                    bestMeterDist = dist;
                    TryParseMeter(mm.Groups["meter"].Value, out meter);
                }
            }

            // Nächsten Video-Match finden
            int bestVideoDist = int.MaxValue;
            foreach (Match vm in videoMatches)
            {
                var dist = Math.Abs(vm.Index - pos);
                if (dist < bestVideoDist && dist < 500)
                {
                    bestVideoDist = dist;
                    zeit = ParseTimestamp(vm.Groups["time"].Value);
                }
            }

            var entry = BuildEntryDirect(meter, meter, code, code, zeit);
            if (entry is not null && seen.Add(Sig(entry)))
                results.Add(entry);
        }

        return results;
    }

    /// <summary>
    /// Findet den Meterwert der vorherigen Tabellenzeile für Fortsetzungszeilen.
    /// </summary>
    private static double FindPrecedingMeter(string text, int position, double fallback)
    {
        // Suche rückwärts nach einer Zeile mit Meterwert
        var preceding = text[..position];
        var meterMatch = Regex.Match(preceding,
            @"^[ \t]*(\d{1,4}[.,]\d{1,3})[ \t]+[A-Z]",
            RegexOptions.Multiline | RegexOptions.RightToLeft);

        if (meterMatch.Success && TryParseMeter(meterMatch.Groups[1].Value, out var m))
            return m;

        return fallback;
    }

    // ── Hilfsmethoden ───────────────────────────────────────────────────────

    private static TimeSpan? ParseTimestamp(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;
        var parts = raw.Split(':');
        if (parts.Length != 3) return null;
        if (int.TryParse(parts[0], out var h)
            && int.TryParse(parts[1], out var min)
            && int.TryParse(parts[2], out var sec))
            return new TimeSpan(h, min, sec);
        return null;
    }

    private static GroundTruthEntry? BuildEntry(
        string meterStartRaw, string meterEndRaw,
        string code, string charRaw, string text,
        TimeSpan? zeit)
    {
        if (!TryParseMeter(meterStartRaw, out var mStart)) return null;
        if (!TryParseMeter(meterEndRaw, out var mEnd)) mEnd = mStart;
        if (mEnd < mStart) mEnd = mStart;

        code = code.Trim().ToUpperInvariant();
        if (code.Length < 2 || code.Length > 8) return null;

        // Kurzbezeichnungen normalisieren (BEGINN→BCD, BOGEN→BCC, LAGE→skip)
        var normalized = NormalizeVsaCode(code);
        if (normalized is null) return null; // Nicht trainingsrelevant
        code = normalized;

        text = text.Trim();
        if (text.Length < 2) return null;

        var characterization = charRaw?.Trim().ToUpperInvariant() switch
        {
            "A" or "B" or "C" or "D" => charRaw.Trim().ToUpperInvariant(),
            _                         => null
        };

        var quant = TryParseQuantification(text);

        return new GroundTruthEntry
        {
            MeterStart        = mStart,
            MeterEnd          = mEnd,
            VsaCode           = code,
            Text              = text,
            Characterization  = characterization,
            Quantification    = quant,
            IsStreckenschaden = mEnd > mStart + 0.05,
            Zeit              = zeit
        };
    }

    /// <summary>BuildEntry ohne Rohtext-Parsing (für Bildbericht/Continuation).</summary>
    private static GroundTruthEntry? BuildEntryDirect(
        double meterStart, double meterEnd,
        string code, string text, TimeSpan? zeit)
    {
        code = code.Trim().ToUpperInvariant();
        if (code.Length < 2 || code.Length > 8) return null;

        // Kurzbezeichnungen normalisieren (BEGINN→BCD, BOGEN→BCC, LAGE→skip)
        var normalized = NormalizeVsaCode(code);
        if (normalized is null) return null;
        code = normalized;

        text = text.Trim();
        if (text.Length < 2) text = code; // Fallback: Code als Beschreibung

        return new GroundTruthEntry
        {
            MeterStart        = meterStart,
            MeterEnd          = meterEnd,
            VsaCode           = code,
            Text              = text,
            Characterization  = null,
            Quantification    = TryParseQuantification(text),
            IsStreckenschaden = meterEnd > meterStart + 0.05,
            Zeit              = zeit
        };
    }

    private static QuantificationDetail? TryParseQuantification(string text)
    {
        var m = QuantPattern.Match(text);
        if (!m.Success) return null;

        if (!double.TryParse(m.Groups["val"].Value.Replace(',', '.'),
                NumberStyles.Float, CultureInfo.InvariantCulture, out var val))
            return null;

        var unit = m.Groups["unit"].Value.ToLowerInvariant() switch
        {
            "stueck" => "Stück",
            var u    => u
        };

        var type = unit switch
        {
            "%"      => "Querschnittsverminderung",
            "mm"     => "Spaltbreite",
            "cm"     => "Spaltbreite",
            "Stück"  => "Anzahl",
            _        => "Unbekannt"
        };

        return new QuantificationDetail { Value = val, Unit = unit, Type = type };
    }

    private static bool TryParseMeter(string raw, out double value)
    {
        if (string.IsNullOrWhiteSpace(raw)) { value = 0; return false; }
        return double.TryParse(raw.Replace(',', '.'), NumberStyles.Float,
            CultureInfo.InvariantCulture, out value);
    }

    private static string Sig(GroundTruthEntry e)
        => $"{e.VsaCode}|{e.MeterStart:F2}|{e.MeterEnd:F2}";

    // ── Logo/Symbol-Erkennung ────────────────────────────────────────────

    /// <summary>
    /// Erkennt Logos, Symbole und geometrische Grafiken anhand der Farbvielfalt.
    /// Echte Kanalfotos haben tausende verschiedene Farben (natuerliche Szene).
    /// Logos/Symbole haben typisch 2-50 verschiedene Farben (Vektorgrafik/Flaechen).
    /// </summary>
    private static bool IsLikelyLogoOrSymbol(byte[] imageBytes, string ext)
    {
        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bi = new System.Windows.Media.Imaging.BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 100; // Klein laden fuer Speed
            bi.EndInit();
            bi.Freeze();

            var wb = new System.Windows.Media.Imaging.WriteableBitmap(bi);
            int stride = wb.PixelWidth * 4;
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            // Eindeutige Farben zaehlen (auf 5-Bit quantisiert fuer Robustheit)
            var colors = new HashSet<int>();
            for (int i = 0; i < pixels.Length - 3; i += 4)
            {
                // Quantisieren: 256 Farben → 32 Stufen pro Kanal
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

    // ── Code-Normalisierung ────────────────────────────────────────────────

    /// <summary>
    /// Normalisiert PDF-Kurzbezeichnungen zu VSA-Codes.
    /// Manche PDFs (WinCan, IKAS) verwenden Kurzbezeichnungen statt VSA-Codes.
    /// Gibt null zurueck wenn der Code nicht trainingsrelevant ist (skip).
    /// </summary>
    private static string? NormalizeVsaCode(string code)
    {
        var upper = code.ToUpperInvariant();
        return upper switch
        {
            // Bestandsaufnahme → VSA BC-Codes
            "BEGINN" or "ROHRANFANG" or "ANFANG"      => "BCD",
            "ENDE" or "ROHRENDE"                       => "BCE",
            "BOGEN" or "KURVE" or "RICHTUNGSWECHSEL"   => "BCC",
            "ANSCHLUSS" or "ABZWEIG" or "STUTZEN"      => "BCA",

            // Nicht trainingsrelevant (Metadaten, Verwaltung) → skip
            "LAGE" or "LAGEBESTIMMUNG"                 => null,
            "ORT" or "ORTUNG"                          => null,
            "IN" or "INSPEKTION"                       => null,
            "NEUE" or "NEUEROHR" or "NEUELAENGE"       => null,
            "TEXT" or "BEMERKUNG" or "NOTIZ"            => null,
            "IVECO" or "FAHRZEUG" or "KAMERA"          => null, // Fahrzeug/Kamera-Info
            "BREITE" or "HOEHE" or "LAENGE"            => null, // Massangaben
            "ROHR" or "ROHRART" or "MATERIAL"          => null, // Rohrinformation
            "PROFIL" or "PROFILART"                     => null, // Profilangabe
            "FOTO" or "BILD" or "VIDEO"                => null, // Medienreferenz
            "SCHACHT" or "DECKEL"                       => null, // Schachtinfo
            "REINIGUNG" or "SPUELUNG"                   => null, // Betriebsinfo
            "WETTER" or "TEMPERATUR"                    => null, // Umgebungsbedingungen
            "DATUM" or "ZEIT" or "UHRZEIT"              => null, // Zeitangaben
            "INSPEKTEUR" or "OPERATEUR"                 => null, // Personal
            "AUFTRAGGEBER" or "KUNDE"                   => null, // Verwaltung
            "HALTUNG" or "HALTUNGSNAME"                 => null, // Haltungsbezeichnung
            "DN" or "NENNWEITE" or "DIMENSION"          => null, // Dimensionsangabe
            "STRASSE" or "GEMEINDE" or "KANALNUTZUNG"   => null, // Adressinfo
            "HARTE" or "HAERTE" or "AUSHAERTUNG"       => null, // Aushaertungsprotokoll

            // Bereits ein VSA-Code (beginnt mit B + 2. Buchstabe A-D) → unveraendert
            _ when upper.Length >= 2
                  && upper[0] == 'B'
                  && upper[1] is >= 'A' and <= 'D' => upper,

            // AE-Codes (Profilwechsel, Materialwechsel, Neue Laenge) → durchlassen
            // Im Video erkennbar als Wechsel des Rohrmaterials/Profils
            _ when upper.StartsWith("AE", StringComparison.Ordinal) => upper,

            // BD-Codes: Administrative (BDBA=Beginn TV, BDBB=Ende TV, BDBC=Inspektion spaeter)
            // BDA=Allgemeinzustand und BDB=Kamera nicht einsetzbar behalten (visuell erkennbar)
            "BDBA" or "BDBB" or "BDBC" or "BDBD" or "BDBE" => null,

            // Unbekannter Code → Reverse-Lookup: Langtext → VSA-Code
            // z.B. "Rohranfang" → BCD, "Bogen nach links" → BCCAY
            _ => VsaCodeTree.ReverseLookup(upper) ?? VsaCodeTree.ReverseLookup(code)
        };
    }

    /// <summary>
    /// Versucht aus einem reinen Langtext (ohne Code-Praefix) den VSA-Code aufzuloesen.
    /// Wird in den PDF-Parse-Strategien aufgerufen wenn kein Code erkannt wurde.
    /// </summary>
    private static string? TryResolveFromLangtext(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 4)
            return null;

        return VsaCodeTree.ReverseLookup(text.Trim());
    }
}
