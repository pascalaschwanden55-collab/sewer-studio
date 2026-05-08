using System.IO;
using AuswertungPro.Next.Application.Ai.Imaging;

namespace AuswertungPro.Next.UI.Ai.Imaging;

/// <summary>
/// Phase 6.2: WPF-Adapter fuer <see cref="IImageBitmapAnalyzer"/>.
///
/// Liest Bild-Metadaten ueber <see cref="System.Windows.Media.Imaging.BitmapDecoder"/>.
/// Wird in einer kommenden Phase per DI in den
/// <c>MultiModelAnalysisService</c> gespiegelt, sobald dieser nach
/// Application/Infrastructure migriert wird (Phase 6.3).
/// </summary>
public sealed class WpfImageBitmapAnalyzer : IImageBitmapAnalyzer
{
    public ImageMetadata GetMetadata(byte[] imageBytes)
    {
        using var ms = new MemoryStream(imageBytes);
        var decoder = System.Windows.Media.Imaging.BitmapDecoder.Create(
            ms,
            System.Windows.Media.Imaging.BitmapCreateOptions.None,
            System.Windows.Media.Imaging.BitmapCacheOption.OnLoad);
        var f = decoder.Frames[0];
        return new ImageMetadata(f.PixelWidth, f.PixelHeight, f.DpiX, f.DpiY);
    }
}
