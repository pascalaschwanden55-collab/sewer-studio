using System.Diagnostics;
using System.Text;
using UglyToad.PdfPig;
using AuswertungPro.Next.Infrastructure.Common;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

public sealed record PdfTextExtraction(IReadOnlyList<string> Pages, string FullText);

public static class PdfTextExtractor
{
    public static string FindPdfToTextPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
                return explicitPath;

            throw new FileNotFoundException($"pdftotext.exe nicht gefunden unter: {explicitPath}");
        }

        // 1) Gebundelt (empfohlen): <App>\tools\pdftotext.exe
        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "pdftotext.exe");
        if (File.Exists(bundled))
            return bundled;

        // 2) Neben der App
        var beside = Path.Combine(AppContext.BaseDirectory, "pdftotext.exe");
        if (File.Exists(beside))
            return beside;

        // 3) PATH durchsuchen
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var cand = Path.Combine(dir.Trim(), "pdftotext.exe");
                if (File.Exists(cand))
                    return cand;
            }
            catch { /* ignore */ }
        }

        // 4) WinGet Packages (wie PS-Version)
        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var winget = Path.Combine(local, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(winget))
            {
                // Audit 2026-05-17 (Nachzieh): SafeFileEnumeration.
                var match = SafeFileEnumeration.EnumerateFilesSafe(winget, "pdftotext.exe", recursive: true)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }
        }
        catch { /* ignore */ }

        throw new FileNotFoundException(
            "pdftotext.exe nicht gefunden. Lege es unter <App>\\tools\\pdftotext.exe ab oder installiere Poppler (pdftotext).");
    }

    public static PdfTextExtraction ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF nicht gefunden: {pdfPath}");
        try
        {
            return ExtractPagesWithPdfToText(pdfPath, explicitPdfToTextPath);
        }
        catch
        {
            // Fallback ohne externe Abhängigkeit, damit PDF-Import auch ohne pdftotext funktioniert.
            return ExtractPagesWithPdfPig(pdfPath);
        }
    }

    private static PdfTextExtraction ExtractPagesWithPdfToText(string pdfPath, string? explicitPdfToTextPath)
    {
        var pdftotext = FindPdfToTextPath(explicitPdfToTextPath);
        var tempOut = Path.Combine(Path.GetTempPath(), $"pdf_extract_{Guid.NewGuid():N}.txt");

        try
        {
            // Zentraler ProcessRunner: asynchroner Drain + harter Timeout statt
            // synchronem ReadToEnd ohne Timeout (Audit STAB-H1).
            var args = new[] { "-enc", "UTF-8", "-layout", pdfPath, tempOut };
            var result = AuswertungPro.Next.Application.Common.ProcessRunner
                .RunAsync(pdftotext, args, timeout: TimeSpan.FromMinutes(2))
                .GetAwaiter().GetResult();

            if (result.StartFailed)
                throw new InvalidOperationException("pdftotext Prozess konnte nicht gestartet werden.");
            if (result.TimedOut)
                throw new InvalidOperationException($"pdftotext Timeout nach {result.Duration.TotalSeconds:F0}s.");
            if (result.ExitCode != 0)
                throw new InvalidOperationException($"pdftotext fehlgeschlagen (ExitCode {result.ExitCode}). {result.Stderr}".Trim());

            var content = File.ReadAllText(tempOut, Encoding.UTF8);
            content = (content ?? "").Replace("\r\n", "\n");
            if (string.IsNullOrWhiteSpace(content))
                return new PdfTextExtraction(Array.Empty<string>(), "");

            var pages = content.Split('\f')
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToList();

            return new PdfTextExtraction(pages, content);
        }
        finally
        {
            try { if (File.Exists(tempOut)) File.Delete(tempOut); } catch { }
        }
    }

    private static PdfTextExtraction ExtractPagesWithPdfPig(string pdfPath)
    {
        using var doc = PdfDocument.Open(pdfPath);
        var pages = new List<string>();

        foreach (var page in doc.GetPages())
        {
            var text = ExtractPageWithLayout(page);
            if (!string.IsNullOrWhiteSpace(text))
                pages.Add(text);
        }

        var fullText = string.Join("\f", pages);
        return new PdfTextExtraction(pages, fullText);
    }

    /// <summary>
    /// Layout-erhaltende Textextraktion aus einer PdfPig-Seite.
    /// Rekonstruiert Zeilen anhand der Y-Koordinaten und berechnet
    /// Leerzeichen aus den Zeichenbreiten (gleiche Logik wie PdfProtocolExtractor).
    /// </summary>
    private static string ExtractPageWithLayout(UglyToad.PdfPig.Content.Page page)
    {
        var letters = page.Letters;
        if (letters.Count == 0)
            return (page.Text ?? "").Replace("\r\n", "\n").Trim();

        var avgW = letters
            .Where(l => l.Width > 0 && l.Value?.Length == 1 && !char.IsWhiteSpace(l.Value[0]))
            .Select(l => l.Width)
            .DefaultIfEmpty(5.5)
            .Average();
        if (avgW < 0.5) avgW = 5.5;

        var lineGroups = letters
            .GroupBy(l => Math.Round(l.StartBaseLine.Y / 2.0) * 2.0)
            .OrderByDescending(g => g.Key);

        var sb = new StringBuilder();

        foreach (var lineGroup in lineGroups)
        {
            var sorted = lineGroup.OrderBy(l => l.StartBaseLine.X).ToList();
            if (sorted.Count == 0) continue;

            var line = new StringBuilder();

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
            if (lineStr.Any(c => !char.IsWhiteSpace(c)))
                sb.AppendLine(lineStr);
        }

        return sb.ToString().TrimEnd();
    }
}
