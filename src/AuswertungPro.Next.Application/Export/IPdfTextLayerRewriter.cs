namespace AuswertungPro.Next.Application.Export;

/// <summary>Ergebnis einer Textkorrektur in einer PDF-Kopie.</summary>
public sealed record PdfTextLayerRewriteResult(
    bool Success,
    bool Corrected,
    string OutputPdfPath,
    int MatchCount,
    int PageCount,
    string Message);

/// <summary>Ergebnis einer sicheren Stapelkorrektur veroeffentlichter PDF-Dateien.</summary>
public sealed record PdfTextLayerBatchFailure(
    string PdfPath,
    string Message);

/// <summary>Summen und Fehlerdetails einer sicheren PDF-Stapelkorrektur.</summary>
public sealed record PdfTextLayerBatchRewriteResult(
    int Rewritten,
    int Skipped,
    int Failed,
    IReadOnlyList<PdfTextLayerBatchFailure> Failures)
{
    public PdfTextLayerBatchRewriteResult(int rewritten, int skipped, int failed)
        : this(rewritten, skipped, failed, Array.Empty<PdfTextLayerBatchFailure>())
    {
    }
}

/// <summary>Erzeugt korrigierte PDF-Kopien oder veroeffentlicht sie kontrolliert am Zielpfad.</summary>
public interface IPdfTextLayerRewriter
{
    bool CanRewrite(string? oldValue, string? newValue);

    /// <summary>Erzeugt eine korrigierte Kopie und laesst die Quelldatei unveraendert.</summary>
    PdfTextLayerRewriteResult TryRewriteHoldingNumber(
        string sourcePdfPath,
        string? oldValue,
        string? newValue);

    /// <summary>
    /// Korrigiert Kennungen in mehreren PDF-Dateien atomar am bestehenden Pfad.
    /// Ersetzte Originaldateien bleiben als Sicherung erhalten.
    /// </summary>
    PdfTextLayerBatchRewriteResult RewriteIdentifierInPlace(
        IReadOnlyList<string> pdfPaths,
        string? oldValue,
        string? newValue);
}
