// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.CodeCatalog;
using AuswertungPro.Next.Application.Ai.Training.Models;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

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
public sealed partial class PdfProtocolExtractor
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

    // Fretz-Klartext: Meter + Klartext (kein VSA-Code) + optionaler Timestamp + optionale Fotonummer
    // Beispiel: "              0.00             Rohranfang                                    00:00:20      1"
    // Beispiel: "              0.20             Infiltration, Wasser fliesst, von 10 Uhr      00:00:41      2"
    // Timeout schuetzt vor Catastrophic-Backtracking bei OCR-Muell (viele Tabs, kein Timestamp).
    private static readonly Regex KlartextLinePattern = new(
        @"^[ \t]*(?<meter>\d{1,4}[.,]\d{1,3})[ \t]{2,}(?<text>[A-ZÄÖÜ].{3,}?)(?:[ \t]{2,}(?<time>\d{2}:\d{2}:\d{2}))?[ \t]*(?:\d{1,5})?\s*$",
        RegexOptions.Compiled | RegexOptions.Multiline,
        TimeSpan.FromSeconds(2));

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
            // Task.Run: PDF-Extraktion (PyMuPDF-Subprocess + PdfPig I/O) auf ThreadPool
            // statt den aufrufenden Thread zu blockieren
            ".pdf"  => Task.Run(() => ExtractFromPdf(filePath, framesDir), ct),
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
    /// <remarks>internal: wird auch von PdfProtocolTableParser geteilt (V4.3).</remarks>
    internal static readonly string[] NonProtocolKeywords =
        ["faktura", "rechnung", "offerte", "angebot", "lieferschein",
         "quittung", "mahnung", "vertrag", "auftrag", "kostenvor",
         "linerdatenblatt", "linerbestellung", "aush\u00E4rtungsprotokoll",
         "einbauprotokoll", "injektion", "situation", "lageplan",
         "schlussrechnung", "bestellung", "datenblatt",
         // V4.3: Dichtheitspruefungen (DP) sind keine Inspektionsprotokolle.
         // Filename-Varianten: "_dp", " dp", "-dp", "dichtheit", "luftpr".
         "_dp", " dp", "-dp", "dichtheit", "luftpr"];

    /// <summary>Text-Marker die zeigen dass das PDF keine Inspektion sondern eine Pruefung ist.</summary>
    /// <remarks>internal: wird auch von PdfProtocolTableParser geteilt (V4.3).</remarks>
    internal static readonly string[] NonProtocolTextMarkers =
        ["dichtheitspr\u00FCfung", "dichtheitspruefung", "sia190", "sia 190",
         "rohrleitungspr\u00FCfung", "luftpr\u00FCfung", "luftpruefung",
         "pr\u00FCfdruck", "pr\u00FCfresultat"];

    private static IReadOnlyList<GroundTruthEntry> ExtractFromPdf(string path, string? framesDir)
    {
        try
        {
            // Rechnungen, Offerten, Lieferscheine etc. ueberspringen
            var fileName = Path.GetFileNameWithoutExtension(path).ToLowerInvariant();
            if (NonProtocolKeywords.Any(kw => fileName.Contains(kw)))
                return Array.Empty<GroundTruthEntry>();

            // Primaer: pdftotext -layout (bewahrt Tabellenstruktur, viel besser als PdfPig)
            var text = ExtractTextViaPdfToText(path);

            // Fallback: PdfPig wenn pdftotext nicht verfuegbar
            UglyToad.PdfPig.PdfDocument? doc = null;
            if (string.IsNullOrWhiteSpace(text))
            {
                try
                {
                    doc = UglyToad.PdfPig.PdfDocument.Open(path);
                    text = ExtractTextFromPdfDoc(doc);
                }
                catch { /* PdfPig darf scheitern — OCR-Fallback versucht's gleich */ }
            }

            // V4.3: OCR-Fallback BEVOR wir aufgeben. Bei Scan-PDFs liefern weder
            // pdftotext noch PdfPig Text. Windows OCR kann sie trotzdem lesen.
            // Phase 5.3 Sub-A: via Provider-Pattern entkoppelt (war Windows.Media.Ocr).
            if (string.IsNullOrWhiteSpace(text)
                && AuswertungPro.Next.Application.Imaging.OcrPdfFallbackProvider.HasFallback)
            {
                try
                {
                    text = AuswertungPro.Next.Application.Imaging.OcrPdfFallbackProvider
                        .ExtractTextAsync(path, maxPages: 5)
                        .GetAwaiter().GetResult();
                }
                catch { /* OCR ist best-effort */ }
            }

            if (string.IsNullOrWhiteSpace(text))
            {
                doc?.Dispose();
                return Array.Empty<GroundTruthEntry>();
            }

            // V4.3: Content-Check nach Textextraktion — DP-PDFs ohne klares Filename-Muster
            // (z.B. wenn Filename nur Datum/Haltung ist) trotzdem skippen.
            var textLower = text.ToLowerInvariant();
            if (NonProtocolTextMarkers.Any(m => textLower.Contains(m)))
            {
                doc?.Dispose();
                return Array.Empty<GroundTruthEntry>();
            }

            // V4.2 Nachbesserung: Caesar-Decoder auch auf pdftotext-Output anwenden.
            // IKAS-PDFs mit Custom-Font-Encoding liefern verschluesselten Text (nicht leer),
            // der Decoder wurde bisher nur beim PdfPig-Fallback aktiv.
            text = TryDecodeShiftedText(text);

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

            // V4.3: OCR-Fallback wenn weder pdftotext noch PdfPig brauchbare Eintraege liefern.
            // Phase 5.3 Sub-A: via Provider-Pattern entkoppelt.
            if (entries.Count == 0 && AuswertungPro.Next.Application.Imaging.OcrPdfFallbackProvider.HasFallback)
            {
                try
                {
                    var ocrText = AuswertungPro.Next.Application.Imaging.OcrPdfFallbackProvider
                        .ExtractTextAsync(path, maxPages: 5)
                        .GetAwaiter().GetResult();
                    if (!string.IsNullOrWhiteSpace(ocrText))
                    {
                        var ocrEntries = ParseEntriesFromText(ocrText);
                        if (ocrEntries.Count > 0)
                        {
                            entries = ocrEntries;
                            text = ocrText;
                        }
                    }
                }
                catch { /* OCR ist best-effort Fallback */ }
            }

            // Fotos aus PDF-Bildbericht extrahieren und Einträgen zuordnen
            if (entries.Count > 0 && !string.IsNullOrWhiteSpace(framesDir))
            {
                doc ??= UglyToad.PdfPig.PdfDocument.Open(path);
                entries = ExtractAndAssignPdfImages(doc, entries, path, framesDir);
            }

            doc?.Dispose();
            return entries;
        }
        catch
        {
            return Array.Empty<GroundTruthEntry>();
        }
    }

    /// <summary>Extrahiert Text via pdftotext -layout (bewahrt Tabellenstruktur).</summary>
    private static string ExtractTextViaPdfToText(string pdfPath)
    {
        // pdftotext-Pfad: 1) AppSettings 2) tools-Ordner 3) PATH
        var exePath = PdfProtocolTableParser.PdfToTextExePath;
        if (string.IsNullOrWhiteSpace(exePath) || !File.Exists(exePath))
        {
            var toolsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "tools", "pdftotext.exe");
            exePath = File.Exists(toolsPath) ? toolsPath : "pdftotext";
        }

        try
        {
            // Phase D2.3: ProcessRunner — sicherer ArgumentList + asynchroner Drain
            // + Tree-Kill bei Timeout. GetAwaiter().GetResult() ist akzeptabel,
            // weil dieser Aufrufer synchron ist (keine Cancellation-Quelle).
            var result = AuswertungPro.Next.Application.Common.ProcessRunner.RunAsync(
                fileName: exePath,
                arguments: ["-layout", pdfPath, "-"],
                timeout: TimeSpan.FromSeconds(30)).GetAwaiter().GetResult();

            return result.IsSuccess ? result.Stdout : "";
        }
        catch
        {
            return "";
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
    /// V4.2: Public damit PdfProtocolTableParser und andere den Decoder nutzen koennen.
    /// </summary>
    public static string TryDecodeShiftedText(string text)
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
            // BDA=Allgemeinzustand und BDB=Allgemeine Anmerkung behalten (BDBG-J=Kamera nicht einsetzbar)
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
