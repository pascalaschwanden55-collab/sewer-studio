using AuswertungPro.Next.Application.Import;

namespace AuswertungPro.Next.Infrastructure.Import.Pdf;

internal sealed record OcrPageExtractionResult(bool Success, string? Text, string? Message);

/// <summary>Kompatible interne API; Datei- und Prozessarbeit liegt im Instanzdienst.</summary>
public static class PdfOcrExtractor
{
    private static readonly IPdfOcrExtractor Default =
        new PdfOcrExtractionService(PdfTextExtractor.Current);

    public static IPdfOcrExtractor Current => Default;

    [Obsolete("Die PDF-OCR-Fassade ist unveraenderbar. Abhaengigkeit direkt uebergeben.")]
    public static void Use(IPdfOcrExtractor extractor)
    {
        ArgumentNullException.ThrowIfNull(extractor);
        throw new NotSupportedException(
            "Die PDF-OCR-Fassade kann nicht mehr global ersetzt werden.");
    }

    internal static OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
    {
        var result = Current.TryExtractPageText(pdfPath, pageNumber);
        return new OcrPageExtractionResult(result.Success, result.Text, result.Message);
    }
}
