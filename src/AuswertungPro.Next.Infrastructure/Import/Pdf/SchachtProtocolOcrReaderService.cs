using AuswertungPro.Next.Application.Import;
using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

/// <summary>
/// Liest gescannte Schachtprotokolle seitenweise per OCR. Die engere
/// Seitengrenze schuetzt den manuellen Einzelimport vor sehr langen Laeufen.
/// </summary>
public sealed class SchachtProtocolOcrReaderService : ISchachtProtocolOcrReader
{
    public const int DefaultMaxOcrPages = 40;

    private readonly IPdfFileSafetyChecker _fileSafety;
    private readonly IPdfOcrExtractor _ocrExtractor;

    public SchachtProtocolOcrReaderService(
        IPdfFileSafetyChecker fileSafety,
        IPdfOcrExtractor ocrExtractor)
    {
        _fileSafety = fileSafety ?? throw new ArgumentNullException(nameof(fileSafety));
        _ocrExtractor = ocrExtractor ?? throw new ArgumentNullException(nameof(ocrExtractor));
    }

    public SchachtProtocolOcrReadResult TryRead(
        string pdfPath,
        int maxPages = DefaultMaxOcrPages)
    {
        var fileBudget = _fileSafety.CheckFileBudget(pdfPath);
        if (!fileBudget.Allowed)
            return Failed(fileBudget.Message);

        int pageCount;
        try
        {
            using var document = PdfDocument.Open(pdfPath);
            pageCount = document.NumberOfPages;
        }
        catch (Exception ex)
        {
            return Failed($"PDF konnte fuer die Texterkennung nicht geoeffnet werden: {ex.Message}");
        }

        var pageBudget = PdfImportSafetyPolicy.CheckPageBudget(pageCount, maxPages);
        if (!pageBudget.Allowed)
        {
            return new SchachtProtocolOcrReadResult(
                false,
                string.Empty,
                pageCount,
                0,
                pageBudget.Message);
        }

        var pages = new List<string>();
        string? firstError = null;

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var page = _ocrExtractor.TryExtractPageText(pdfPath, pageNumber);
            if (page.Success && !string.IsNullOrWhiteSpace(page.Text))
            {
                pages.Add(page.Text.Replace("\r\n", "\n").Trim());
                continue;
            }

            if (string.IsNullOrWhiteSpace(firstError) && !string.IsNullOrWhiteSpace(page.Message))
                firstError = $"Seite {pageNumber}: {page.Message}";
        }

        if (pages.Count == 0)
        {
            return new SchachtProtocolOcrReadResult(
                false,
                string.Empty,
                pageCount,
                0,
                firstError ?? "Die Texterkennung lieferte keinen lesbaren Text.");
        }

        return new SchachtProtocolOcrReadResult(
            true,
            string.Join("\n\n", pages),
            pageCount,
            pages.Count,
            firstError);
    }

    private static SchachtProtocolOcrReadResult Failed(string? message)
        => new(
            false,
            string.Empty,
            0,
            0,
            message ?? "Die Texterkennung konnte nicht gestartet werden.");
}
