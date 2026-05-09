// AuswertungPro – KI Videoanalyse Modul
using System;
using System.Diagnostics;
using System.IO;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

// PdfProtocolExtractor Text-Extraktion: ExtractTextViaPdfToText (poppler
// pdftotext.exe Subprocess), ExtractTextFromPdfDoc (PdfPig direkt mit
// Caesar-Shift-Decodierung fuer kaputte Encodings), TryDecodeShiftedText,
// CountWordMatches, ShiftAllChars (-3..+3 Stellen probieren), GetPyMuPdf
// ScriptPath (Resolution des Python-Subprocess-Skripts).
// Aus dem Hauptdatei extrahiert (Slice 16c).
public sealed partial class PdfProtocolExtractor
{
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
}
