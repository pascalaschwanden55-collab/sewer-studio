using System.Threading;
using System.Threading.Tasks;

namespace AuswertungPro.Next.Application.Imaging;

/// <summary>
/// Phase 5.3 Sub-A: OCR-Fallback fuer PDFs aus denen pdftotext/PdfPig
/// keinen brauchbaren Text liefert. Implementierung lebt in der UI-Schicht
/// (Windows.Media.Ocr braucht ein Windows-Target-Framework).
/// </summary>
public interface IOcrPdfFallback
{
    /// <summary>
    /// Rendert bis zu <paramref name="maxPages"/> Seiten des PDFs und laesst
    /// einen OCR-Engine drueberlaufen. Gibt den zusammengesetzten Text zurueck.
    /// Bei Fehler: leerer String.
    /// </summary>
    Task<string> ExtractTextAsync(
        string pdfPath,
        int maxPages = 5,
        CancellationToken ct = default);
}

/// <summary>
/// Provider-Pattern fuer den OCR-Fallback. Wird in App.xaml.cs einmalig
/// registriert (z.B. mit einem WindowsOcrPdfFallback-Adapter aus der UI).
/// Wenn nicht registriert, gibt <see cref="ExtractTextAsync"/> den leeren
/// String zurueck — die KI-Pipeline laeuft dann ohne OCR-Fallback weiter.
/// </summary>
public static class OcrPdfFallbackProvider
{
    private static IOcrPdfFallback? _impl;

    public static void SetFallback(IOcrPdfFallback impl) => _impl = impl;

    /// <summary>True wenn ein Fallback registriert ist.</summary>
    public static bool HasFallback => _impl is not null;

    /// <summary>
    /// Best-effort. Wenn kein Fallback registriert ist, sofort leerer String
    /// (Pipeline laeuft ohne OCR weiter).
    /// </summary>
    public static Task<string> ExtractTextAsync(
        string pdfPath,
        int maxPages = 5,
        CancellationToken ct = default)
    {
        return _impl?.ExtractTextAsync(pdfPath, maxPages, ct) ?? Task.FromResult(string.Empty);
    }
}
