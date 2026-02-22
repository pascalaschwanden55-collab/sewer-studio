using System.Diagnostics;
using System.Text;
using UglyToad.PdfPig;

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
                var match = Directory.EnumerateFiles(winget, "pdftotext.exe", SearchOption.AllDirectories)
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
            var psi = new ProcessStartInfo
            {
                FileName = pdftotext,
                Arguments = $"-enc UTF-8 -layout \"{pdfPath}\" \"{tempOut}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true
            };

            using var proc = Process.Start(psi);
            if (proc is null)
                throw new InvalidOperationException("pdftotext Prozess konnte nicht gestartet werden.");

            var stderr = proc.StandardError.ReadToEnd();
            proc.WaitForExit();

            if (proc.ExitCode != 0)
                throw new InvalidOperationException($"pdftotext fehlgeschlagen (ExitCode {proc.ExitCode}). {stderr}".Trim());

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
            var text = (page.Text ?? "").Replace("\r\n", "\n").Trim();
            if (!string.IsNullOrWhiteSpace(text))
                pages.Add(text);
        }

        var fullText = string.Join("\f", pages);
        return new PdfTextExtraction(pages, fullText);
    }
}
