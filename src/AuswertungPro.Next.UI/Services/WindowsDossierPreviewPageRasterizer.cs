using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Zeichnet eine Seite der erzeugten Vorschau-PDF als WPF-Bild.</summary>
public interface IDossierPreviewPageRasterizer
{
    Task<BitmapSource> RenderAsync(
        byte[] pdfBytes,
        int pageIndex,
        uint destinationWidth,
        CancellationToken ct = default);
}

/// <summary>
/// Nutzt den eingebauten Windows-PDF-Renderer. Es wird immer die echte PDF-Seite
/// gezeichnet; es gibt keine zweite Nachbildung von Tabellen oder Abständen.
/// </summary>
public sealed class WindowsDossierPreviewPageRasterizer : IDossierPreviewPageRasterizer
{
    public async Task<BitmapSource> RenderAsync(
        byte[] pdfBytes,
        int pageIndex,
        uint destinationWidth,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(pdfBytes);
        if (pdfBytes.Length == 0)
            throw new ArgumentException("Die Vorschau-PDF ist leer.", nameof(pdfBytes));
        if (pageIndex < 0)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));
        if (destinationWidth == 0)
            throw new ArgumentOutOfRangeException(nameof(destinationWidth));

        using var source = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
        // Der .NET-Stream besitzt sonst den WinRT-Stream mit und schliesst ihn
        // beim Dispose. Er muss bis nach dem Laden des PDF offen bleiben.
        using var writer = source.AsStreamForWrite();
        await writer.WriteAsync(pdfBytes, ct).ConfigureAwait(true);
        await writer.FlushAsync(ct).ConfigureAwait(true);

        source.Seek(0);
        var document = await global::Windows.Data.Pdf.PdfDocument
            .LoadFromStreamAsync(source)
            .AsTask(ct)
            .ConfigureAwait(true);

        if ((uint)pageIndex >= document.PageCount)
            throw new ArgumentOutOfRangeException(nameof(pageIndex));

        using var page = document.GetPage((uint)pageIndex);
        using var rendered = new global::Windows.Storage.Streams.InMemoryRandomAccessStream();
        var options = new global::Windows.Data.Pdf.PdfPageRenderOptions
        {
            DestinationWidth = destinationWidth
        };

        await page.RenderToStreamAsync(rendered, options).AsTask(ct).ConfigureAwait(true);
        rendered.Seek(0);

        var bitmap = new BitmapImage();
        using var reader = rendered.AsStreamForRead();
        bitmap.BeginInit();
        bitmap.CacheOption = BitmapCacheOption.OnLoad;
        bitmap.StreamSource = reader;
        bitmap.EndInit();
        bitmap.Freeze();
        return bitmap;
    }
}
