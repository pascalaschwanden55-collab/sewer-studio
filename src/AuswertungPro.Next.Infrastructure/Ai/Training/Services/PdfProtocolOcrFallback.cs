using AuswertungPro.Next.Infrastructure.Import.Pdf;

namespace AuswertungPro.Next.Infrastructure.Ai.Training.Services;

internal interface IPdfProtocolOcrFallback
{
    OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber);
}

internal sealed class PdfProtocolOcrFallback : IPdfProtocolOcrFallback
{
    public static PdfProtocolOcrFallback Instance { get; } = new();

    private PdfProtocolOcrFallback()
    {
    }

    public OcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber)
        => PdfOcrExtractor.TryExtractPageText(pdfPath, pageNumber);
}
