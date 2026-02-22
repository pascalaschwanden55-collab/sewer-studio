using System.Diagnostics;
using System.Text;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record OcrPageExtractionResult(bool Success, string? Text, string? Message);

internal static class PdfOcrExtractor
{
    public static OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return new OcrPageExtractionResult(false, null, "PDF not found");
        if (pageNumber <= 0)
            return new OcrPageExtractionResult(false, null, "Invalid page number");

        var pdftoppm = FindPdfToPpmPath();
        if (string.IsNullOrWhiteSpace(pdftoppm))
            return new OcrPageExtractionResult(false, null, "pdftoppm.exe not found");

        var tesseract = FindTesseractPath();
        if (string.IsNullOrWhiteSpace(tesseract))
            return new OcrPageExtractionResult(false, null, "tesseract.exe not found");

        var tempBase = Path.Combine(Path.GetTempPath(), $"pdf_ocr_{Guid.NewGuid():N}");
        var pngPath = $"{tempBase}.png";

        try
        {
            var renderArgs = $"-f {pageNumber} -l {pageNumber} -r 300 -gray -singlefile -png \"{pdfPath}\" \"{tempBase}\"";
            var render = RunProcess(pdftoppm, renderArgs, timeoutMs: 45_000);
            if (!render.Success)
                return new OcrPageExtractionResult(false, null, $"pdftoppm failed: {render.Message}");
            if (!File.Exists(pngPath))
                return new OcrPageExtractionResult(false, null, "pdftoppm produced no image");

            var ocrArgs = $"\"{pngPath}\" stdout -l deu+eng --oem 1 --psm 6";
            var ocr = RunProcess(tesseract, ocrArgs, timeoutMs: 60_000);
            if (!ocr.Success)
                return new OcrPageExtractionResult(false, null, $"tesseract failed: {ocr.Message}");

            var text = NormalizeText(ocr.StdOut);
            if (string.IsNullOrWhiteSpace(text))
                return new OcrPageExtractionResult(false, null, "OCR returned empty text");

            return new OcrPageExtractionResult(true, text, null);
        }
        catch (Exception ex)
        {
            return new OcrPageExtractionResult(false, null, ex.Message);
        }
        finally
        {
            try
            {
                var tempDir = Path.GetDirectoryName(tempBase);
                if (!string.IsNullOrWhiteSpace(tempDir) && Directory.Exists(tempDir))
                {
                    var prefix = Path.GetFileName(tempBase);
                    foreach (var path in Directory.EnumerateFiles(tempDir, $"{prefix}*"))
                    {
                        try { File.Delete(path); } catch { }
                    }
                }
            }
            catch { }
        }
    }

    private static string NormalizeText(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n");

    private static string? FindPdfToPpmPath()
    {
        // Prefer sibling of configured/discovered pdftotext.exe.
        try
        {
            var pdfToText = PdfTextExtractor.FindPdfToTextPath();
            var sibling = Path.Combine(Path.GetDirectoryName(pdfToText) ?? string.Empty, "pdftoppm.exe");
            if (File.Exists(sibling))
                return sibling;
        }
        catch
        {
            // Best effort only.
        }

        var appTools = Path.Combine(AppContext.BaseDirectory, "tools", "pdftoppm.exe");
        if (File.Exists(appTools))
            return appTools;

        var besideApp = Path.Combine(AppContext.BaseDirectory, "pdftoppm.exe");
        if (File.Exists(besideApp))
            return besideApp;

        return FindExecutable("pdftoppm.exe");
    }

    private static string? FindTesseractPath()
    {
        var fromEnv = Environment.GetEnvironmentVariable("TESSERACT_PATH");
        if (!string.IsNullOrWhiteSpace(fromEnv) && File.Exists(fromEnv))
            return fromEnv;

        var appTools = Path.Combine(AppContext.BaseDirectory, "tools", "tesseract.exe");
        if (File.Exists(appTools))
            return appTools;

        var besideApp = Path.Combine(AppContext.BaseDirectory, "tesseract.exe");
        if (File.Exists(besideApp))
            return besideApp;

        var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
        var pfCandidate = Path.Combine(programFiles, "Tesseract-OCR", "tesseract.exe");
        if (File.Exists(pfCandidate))
            return pfCandidate;

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var pfx86Candidate = Path.Combine(programFilesX86, "Tesseract-OCR", "tesseract.exe");
        if (File.Exists(pfx86Candidate))
            return pfx86Candidate;

        return FindExecutable("tesseract.exe");
    }

    private static string? FindExecutable(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var raw in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var dir = raw.Trim();
                if (string.IsNullOrWhiteSpace(dir))
                    continue;
                var candidate = Path.Combine(dir, executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Keep searching.
            }
        }

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var winget = Path.Combine(local, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(winget))
            {
                return Directory.EnumerateFiles(winget, executableName, SearchOption.AllDirectories)
                    .FirstOrDefault();
            }
        }
        catch
        {
            // Keep fallback null.
        }

        return null;
    }

    private static ProcessRunResult RunProcess(string exePath, string arguments, int timeoutMs)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exePath,
            Arguments = arguments,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };

        using var process = Process.Start(psi);
        if (process is null)
            return new ProcessRunResult(false, "Failed to start process", string.Empty);

        var stdOut = process.StandardOutput.ReadToEnd();
        var stdErr = process.StandardError.ReadToEnd();
        if (!process.WaitForExit(timeoutMs))
        {
            try { process.Kill(entireProcessTree: true); } catch { }
            return new ProcessRunResult(false, "Timeout", stdOut);
        }

        if (process.ExitCode != 0)
            return new ProcessRunResult(false, string.IsNullOrWhiteSpace(stdErr) ? $"ExitCode {process.ExitCode}" : stdErr.Trim(), stdOut);

        return new ProcessRunResult(true, null, stdOut);
    }

    private sealed record ProcessRunResult(bool Success, string? Message, string StdOut);
}
