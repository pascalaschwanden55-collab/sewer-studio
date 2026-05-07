using System.Threading;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Imaging;
using AuswertungPro.Next.UI.Ai.Training.Services;

namespace AuswertungPro.Next.UI.Imaging;

/// <summary>
/// Adapter zwischen <see cref="IOcrPdfFallback"/> und der bestehenden statischen
/// <see cref="OcrPdfFallbackService"/> (Windows.Media.Ocr + Docnet/PDFium).
/// Wird in App.xaml.cs registriert; Application-Services rufen den Provider
/// und wissen nichts von Windows-spezifischen APIs.
/// </summary>
public sealed class WindowsOcrPdfFallback : IOcrPdfFallback
{
    public Task<string> ExtractTextAsync(
        string pdfPath,
        int maxPages = 5,
        CancellationToken ct = default)
    {
        if (!System.OperatingSystem.IsWindowsVersionAtLeast(10, 0, 19041))
            return Task.FromResult(string.Empty);

        return OcrPdfFallbackService.ExtractTextAsync(pdfPath, maxPages, ct);
    }
}
