namespace AuswertungPro.Next.Application.Import;

/// <summary>Ergebnis der Texterkennung einer einzelnen PDF-Seite.</summary>
public sealed record PdfOcrPageExtractionResult(bool Success, string? Text, string? Message);

/// <summary>Ergebnis der seitenweisen Texterkennung einer ganzen PDF-Datei.</summary>
public sealed record PdfOcrDocumentExtractionResult(IReadOnlyList<string> Pages, string? Message);

/// <summary>Erkennt Text in gescannten PDF-Seiten mit Poppler und Tesseract.</summary>
public interface IPdfOcrExtractor
{
    PdfOcrPageExtractionResult TryExtractPageText(string pdfPath, int pageNumber);

    PdfOcrDocumentExtractionResult TryExtractAllPages(string pdfPath);
}
