namespace AuswertungPro.Next.Application.Export;

/// <summary>Ergebnis einer Textkorrektur in einer PDF-Kopie.</summary>
public sealed record PdfTextLayerRewriteResult(
    bool Success,
    bool Corrected,
    string OutputPdfPath,
    int MatchCount,
    int PageCount,
    string Message);

/// <summary>Erzeugt eine korrigierte PDF-Kopie, ohne die Quelldatei zu verändern.</summary>
public interface IPdfTextLayerRewriter
{
    bool CanRewrite(string? oldValue, string? newValue);

    PdfTextLayerRewriteResult TryRewriteHoldingNumber(
        string sourcePdfPath,
        string? oldValue,
        string? newValue);
}
