using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Fuehrt den begrenzten Poppler-/Tesseract-Rueckfall fuer Bild-PDFs aus.</summary>
public sealed class PdfOcrExtractionService : IPdfOcrExtractor
{
    private readonly IPdfFileSafetyChecker _fileSafety;
    private readonly IPdfTextExtractor _pdfTextExtractor;

    public PdfOcrExtractionService(IPdfTextExtractor pdfTextExtractor)
        : this(pdfTextExtractor, PdfImportSafetyPolicy.Current)
    {
    }

    public PdfOcrExtractionService(
        IPdfTextExtractor pdfTextExtractor,
        IPdfFileSafetyChecker fileSafety)
    {
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _fileSafety = fileSafety ?? throw new ArgumentNullException(nameof(fileSafety));
    }

    public PdfOcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            return new PdfOcrPageExtractionResult(false, null, "PDF wurde nicht gefunden.");

        try
        {
            _fileSafety.ThrowIfFileTooLarge(pdfPath);
        }
        catch (Exception ex)
        {
            return new PdfOcrPageExtractionResult(false, null, ex.Message);
        }

        if (pageNumber <= 0)
            return new PdfOcrPageExtractionResult(false, null, "Ungueltige Seitennummer.");

        var pdftoppm = FindPdfToPpmPath();
        if (string.IsNullOrWhiteSpace(pdftoppm))
        {
            return new PdfOcrPageExtractionResult(
                false,
                null,
                "pdftoppm.exe wurde nicht gefunden. Poppler muss installiert oder im App-Ordner tools vorhanden sein.");
        }

        var tesseract = FindTesseractPath();
        if (string.IsNullOrWhiteSpace(tesseract))
        {
            return new PdfOcrPageExtractionResult(
                false,
                null,
                "tesseract.exe wurde nicht gefunden. Tesseract muss installiert oder im App-Ordner tools vorhanden sein.");
        }

        var tempBase = Path.Combine(Path.GetTempPath(), $"pdf_ocr_{Guid.NewGuid():N}");
        var pngPath = $"{tempBase}.png";

        try
        {
            var render = RunProcess(
                pdftoppm,
                ["-f", pageNumber.ToString(), "-l", pageNumber.ToString(),
                    "-r", "300", "-gray", "-singlefile", "-png", pdfPath, tempBase],
                timeoutMs: 45_000);
            if (!render.Success)
            {
                return new PdfOcrPageExtractionResult(
                    false,
                    null,
                    $"PDF-Seite konnte nicht in ein Bild umgewandelt werden: {render.Message}");
            }

            if (!File.Exists(pngPath))
                return new PdfOcrPageExtractionResult(false, null, "pdftoppm hat kein Seitenbild erzeugt.");

            var ocr = RunProcess(
                tesseract,
                [pngPath, "stdout", "-l", "deu+eng", "--oem", "1", "--psm", "6"],
                timeoutMs: 60_000);
            if (!ocr.Success)
            {
                return new PdfOcrPageExtractionResult(
                    false,
                    null,
                    $"Tesseract-Texterkennung fehlgeschlagen: {ocr.Message}");
            }

            var text = NormalizeText(ocr.StdOut);
            if (string.IsNullOrWhiteSpace(text))
                return new PdfOcrPageExtractionResult(false, null, "Die Texterkennung lieferte keinen Text.");

            return new PdfOcrPageExtractionResult(true, text, null);
        }
        catch (Exception ex)
        {
            return new PdfOcrPageExtractionResult(false, null, ex.Message);
        }
        finally
        {
            CleanupTempFiles(tempBase);
        }
    }

    public PdfOcrDocumentExtractionResult TryExtractAllPages(string pdfPath)
    {
        var pageCount = TryGetPdfPageCount(pdfPath);
        if (pageCount <= 0)
        {
            return new PdfOcrDocumentExtractionResult(
                Array.Empty<string>(),
                "Keine Seiten fuer OCR erkannt.");
        }

        var pages = new List<string>();
        string? firstError = null;

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var ocr = TryExtractPageText(pdfPath, pageNumber);
            if (ocr.Success && !string.IsNullOrWhiteSpace(ocr.Text))
            {
                pages.Add(ocr.Text.Replace("\r\n", "\n").Trim());
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstError) && !string.IsNullOrWhiteSpace(ocr.Message))
                firstError = ocr.Message;
        }

        if (pages.Count == 0)
        {
            return new PdfOcrDocumentExtractionResult(
                Array.Empty<string>(),
                firstError ?? "OCR lieferte keinen verwertbaren Text.");
        }

        return new PdfOcrDocumentExtractionResult(pages, firstError);
    }

    private int TryGetPdfPageCount(string pdfPath)
    {
        try
        {
            _fileSafety.ThrowIfFileTooLarge(pdfPath);
            using var document = PdfDocument.Open(pdfPath);
            PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
            return document.NumberOfPages;
        }
        catch
        {
            return 0;
        }
    }

    private static string NormalizeText(string? text)
        => (text ?? string.Empty).Replace("\r\n", "\n");

    private string? FindPdfToPpmPath()
    {
        try
        {
            var pdfToText = _pdfTextExtractor.FindPdfToTextPath();
            var sibling = Path.Combine(Path.GetDirectoryName(pdfToText) ?? string.Empty, "pdftoppm.exe");
            if (File.Exists(sibling))
                return sibling;
        }
        catch
        {
            // Die weiteren festen Suchorte bleiben verfuegbar.
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
        var programFilesCandidate = Path.Combine(programFiles, "Tesseract-OCR", "tesseract.exe");
        if (File.Exists(programFilesCandidate))
            return programFilesCandidate;

        var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
        var programFilesX86Candidate = Path.Combine(programFilesX86, "Tesseract-OCR", "tesseract.exe");
        if (File.Exists(programFilesX86Candidate))
            return programFilesX86Candidate;

        return FindExecutable("tesseract.exe");
    }

    private static string? FindExecutable(string executableName)
    {
        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        foreach (var raw in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var directory = raw.Trim();
                if (string.IsNullOrWhiteSpace(directory))
                    continue;

                var candidate = Path.Combine(directory, executableName);
                if (File.Exists(candidate))
                    return candidate;
            }
            catch
            {
                // Ein ungueltiger PATH-Eintrag darf die weitere Suche nicht abbrechen.
            }
        }

        try
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var winget = Path.Combine(local, "Microsoft", "WinGet", "Packages");
            if (Directory.Exists(winget))
            {
                return AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                    .EnumerateFilesSafe(winget, executableName, recursive: true)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
            }
        }
        catch
        {
            // Ohne Treffer endet die Suche wie bisher mit null.
        }

        return null;
    }

    private static ProcessRunResult RunProcess(string executablePath, string[] arguments, int timeoutMs)
    {
        var result = ExternalProcessRunner.RunAsync(
            executablePath,
            arguments,
            TimeSpan.FromMilliseconds(timeoutMs),
            Encoding.UTF8,
            Encoding.UTF8).GetAwaiter().GetResult();

        return new ProcessRunResult(result.Success, result.Message, result.StdOut);
    }

    private static void CleanupTempFiles(string tempBase)
    {
        try
        {
            var tempDirectory = Path.GetDirectoryName(tempBase);
            if (string.IsNullOrWhiteSpace(tempDirectory) || !Directory.Exists(tempDirectory))
                return;

            var prefix = Path.GetFileName(tempBase);
            foreach (var path in Directory.EnumerateFiles(tempDirectory, $"{prefix}*"))
                BestEffort.Try(() => File.Delete(path), "PDF-OCR: Temp-Datei loeschen");
        }
        catch (Exception ex)
        {
            BestEffort.ReportWarning(
                $"[PdfOcr] Temp-Cleanup uebersprungen: {ex.GetType().Name}: {ex.Message}");
        }
    }

    private sealed record ProcessRunResult(bool Success, string? Message, string StdOut);
}
