using System.Text;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Application.Import;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>Fuehrt die begrenzte PDF-Textextraktion und den externen pdftotext-Aufruf aus.</summary>
public sealed class PdfTextExtractionService : IPdfTextExtractor
{
    private readonly IPdfFileSafetyChecker _fileSafety;

    public PdfTextExtractionService()
        : this(PdfImportSafetyPolicy.Current)
    {
    }

    public PdfTextExtractionService(IPdfFileSafetyChecker fileSafety)
    {
        _fileSafety = fileSafety ?? throw new ArgumentNullException(nameof(fileSafety));
    }

    public string FindPdfToTextPath(string? explicitPath = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitPath))
        {
            if (File.Exists(explicitPath))
                return explicitPath;

            throw new FileNotFoundException($"pdftotext.exe nicht gefunden unter: {explicitPath}");
        }

        var bundled = Path.Combine(AppContext.BaseDirectory, "tools", "pdftotext.exe");
        if (File.Exists(bundled))
            return bundled;

        var beside = Path.Combine(AppContext.BaseDirectory, "pdftotext.exe");
        if (File.Exists(beside))
            return beside;

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        foreach (var dir in pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            try
            {
                var candidate = Path.Combine(dir.Trim(), "pdftotext.exe");
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
                var match = AuswertungPro.Next.Infrastructure.Common.SafeFileEnumeration
                    .EnumerateFilesSafe(winget, "pdftotext.exe", recursive: true)
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault();
                if (!string.IsNullOrWhiteSpace(match))
                    return match;
            }
        }
        catch
        {
            // Nicht lesbare WinGet-Ordner duerfen den PdfPig-Rueckfall nicht verhindern.
        }

        throw new FileNotFoundException(
            "pdftotext.exe nicht gefunden. Lege es unter <App>\\tools\\pdftotext.exe ab oder installiere Poppler (pdftotext).");
    }

    public PdfTextExtractionResult ExtractPages(string pdfPath, string? explicitPdfToTextPath = null)
    {
        if (string.IsNullOrWhiteSpace(pdfPath) || !File.Exists(pdfPath))
            throw new FileNotFoundException($"PDF nicht gefunden: {pdfPath}");

        _fileSafety.ThrowIfFileTooLarge(pdfPath);
        try
        {
            return ExtractPagesWithPdfToText(pdfPath, explicitPdfToTextPath);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch
        {
            // Der eingebaute Leser haelt den Import auch ohne pdftotext funktionsfaehig.
            return ExtractPagesWithPdfPig(pdfPath);
        }
    }

    public void ThrowIfPageBudgetExceeded(string pdfPath, int? maxPages = null)
    {
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages, maxPages);
    }

    private PdfTextExtractionResult ExtractPagesWithPdfToText(string pdfPath, string? explicitPdfToTextPath)
    {
        var pdftotext = FindPdfToTextPath(explicitPdfToTextPath);
        ThrowIfPageBudgetExceeded(pdfPath);
        var tempOut = Path.Combine(Path.GetTempPath(), $"pdf_extract_{Guid.NewGuid():N}.txt");

        try
        {
            var result = ExternalProcessRunner.RunAsync(
                pdftotext,
                ["-enc", "UTF-8", "-layout", pdfPath, tempOut],
                TimeSpan.FromSeconds(60),
                Encoding.UTF8,
                Encoding.UTF8).GetAwaiter().GetResult();

            if (!result.Success)
                throw new InvalidOperationException($"pdftotext fehlgeschlagen. {result.Message}".Trim());

            var content = PdfExtractedTextBudget.ReadUtf8AtMost(tempOut);
            content = (content ?? "").Replace("\r\n", "\n");
            if (string.IsNullOrWhiteSpace(content))
                return new PdfTextExtractionResult(Array.Empty<string>(), "");

            var pages = content.Split('\f')
                .Select(page => page.Trim())
                .Where(page => !string.IsNullOrWhiteSpace(page))
                .ToList();

            return new PdfTextExtractionResult(pages, content);
        }
        finally
        {
            try
            {
                if (File.Exists(tempOut))
                    File.Delete(tempOut);
            }
            catch
            {
                // Eine gesperrte Temp-Datei darf das bereits gelesene Ergebnis nicht verwerfen.
            }
        }
    }

    private static PdfTextExtractionResult ExtractPagesWithPdfPig(string pdfPath)
    {
        using var document = PdfDocument.Open(pdfPath);
        PdfImportSafetyPolicy.ThrowIfTooManyPages(document.NumberOfPages);
        var pages = new List<string>();
        var remainingCharacters = PdfExtractedTextBudget.MaxCharacters;

        foreach (var page in document.GetPages())
        {
            var text = ExtractPageWithLayout(page);
            var limitedText = PdfExtractedTextBudget.TakeAtMost(text, remainingCharacters);
            remainingCharacters -= limitedText.Length;
            if (!string.IsNullOrWhiteSpace(limitedText))
                pages.Add(limitedText);
            if (remainingCharacters == 0)
                break;
        }

        var fullText = string.Join("\f", pages);
        return new PdfTextExtractionResult(pages, fullText);
    }

    private static string ExtractPageWithLayout(UglyToad.PdfPig.Content.Page page)
    {
        var letters = page.Letters;
        if (letters.Count == 0)
            return (page.Text ?? "").Replace("\r\n", "\n").Trim();

        var averageWidth = letters
            .Where(letter => letter.Width > 0
                && letter.Value?.Length == 1
                && !char.IsWhiteSpace(letter.Value[0]))
            .Select(letter => letter.Width)
            .DefaultIfEmpty(5.5)
            .Average();
        if (averageWidth < 0.5)
            averageWidth = 5.5;

        var lineGroups = letters
            .GroupBy(letter => Math.Round(letter.StartBaseLine.Y / 2.0) * 2.0)
            .OrderByDescending(group => group.Key);
        var result = new StringBuilder();

        foreach (var lineGroup in lineGroups)
        {
            var sorted = lineGroup.OrderBy(letter => letter.StartBaseLine.X).ToList();
            if (sorted.Count == 0)
                continue;

            var line = new StringBuilder();
            var indent = (int)(sorted[0].StartBaseLine.X / averageWidth);
            if (indent > 0)
                line.Append(new string(' ', Math.Min(indent, 30)));

            var previousEndX = sorted[0].StartBaseLine.X;
            foreach (var letter in sorted)
            {
                var gap = letter.StartBaseLine.X - previousEndX;
                if (gap > averageWidth * 0.5)
                {
                    var spaces = Math.Max(1, (int)Math.Round(gap / averageWidth));
                    line.Append(new string(' ', Math.Min(spaces, 80)));
                }

                line.Append(letter.Value ?? string.Empty);
                previousEndX = letter.StartBaseLine.X + (letter.Width > 0 ? letter.Width : averageWidth);
            }

            var lineText = line.ToString().TrimEnd();
            if (lineText.Any(character => !char.IsWhiteSpace(character)))
                result.AppendLine(lineText);
        }

        return result.ToString().TrimEnd();
    }
}
