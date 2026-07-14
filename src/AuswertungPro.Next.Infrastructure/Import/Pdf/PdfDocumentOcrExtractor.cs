using UglyToad.PdfPig;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record OcrDocumentExtractionResult(
    bool Success,
    string Text,
    int TotalPages,
    int ExtractedPages,
    string? Message);

/// <summary>
/// Liest kleine Bild-PDFs seitenweise per OCR. Die Begrenzung verhindert,
/// dass ein versehentlich gewaehlter Gesamtauszug die Anwendung lange blockiert.
/// </summary>
internal static class PdfDocumentOcrExtractor
{
    internal const int DefaultMaxOcrPages = 40;

    public static OcrDocumentExtractionResult TryExtract(
        string pdfPath,
        int maxPages = DefaultMaxOcrPages)
    {
        var fileBudget = PdfImportSafetyPolicy.CheckFileBudget(pdfPath);
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
            return new OcrDocumentExtractionResult(false, "", pageCount, 0, pageBudget.Message);

        var pages = new List<string>();
        string? firstError = null;

        for (var pageNumber = 1; pageNumber <= pageCount; pageNumber++)
        {
            var page = PdfOcrExtractor.TryExtractPageText(pdfPath, pageNumber);
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
            return new OcrDocumentExtractionResult(
                false,
                "",
                pageCount,
                0,
                firstError ?? "Die Texterkennung lieferte keinen lesbaren Text.");
        }

        return new OcrDocumentExtractionResult(
            true,
            string.Join("\n\n", pages),
            pageCount,
            pages.Count,
            firstError);
    }

    private static OcrDocumentExtractionResult Failed(string? message)
        => new(false, "", 0, 0, message ?? "Die Texterkennung konnte nicht gestartet werden.");
}
