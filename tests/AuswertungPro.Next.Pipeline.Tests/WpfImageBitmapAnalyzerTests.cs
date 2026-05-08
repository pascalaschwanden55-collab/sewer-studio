using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Ai.Imaging;
using AuswertungPro.Next.UI.Ai.Imaging;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

/// <summary>
/// Phase 6.2 (ARCH-H5 Vorbereitung): Stellt sicher, dass der WPF-Adapter fuer
/// <see cref="IImageBitmapAnalyzer"/> ein PNG korrekt liest und Pixel-/DPI-
/// Metadaten zurueckgibt. Damit kann <c>MultiModelAnalysisService</c> in
/// Phase 6.3 ohne direkten <c>BitmapDecoder</c>-Aufruf migriert werden.
/// </summary>
[Trait("Category", "Recommendation")]
[Trait("Category", "Slow")]
public sealed class WpfImageBitmapAnalyzerTests
{
    /// <summary>Erzeugt ein 1x1 PNG mit fester Farbe und 96 DPI.</summary>
    private static byte[] CreateOnePixelPng()
    {
        const int width = 1;
        const int height = 1;
        int stride = width * 4;
        byte[] pixels = { 0, 0, 0, 255 }; // 1 Pixel Bgra32

        var bmp = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));

        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }

    [Fact]
    public void GetMetadata_OnePixelPng_LiefertWidthHeightUndDpi()
    {
        var png = CreateOnePixelPng();
        var analyzer = new WpfImageBitmapAnalyzer();

        AuswertungPro.Next.Application.Ai.Imaging.ImageMetadata meta = analyzer.GetMetadata(png);

        Assert.Equal(1, meta.PixelWidth);
        Assert.Equal(1, meta.PixelHeight);
        // PNG kodiert DPI in pHYs als Pixel-pro-Meter; round-trip ergibt 95.986...
        // statt exakt 96 (96 dpi = 3779.527... px/m, gerundet auf int = 3779,
        // entspricht 95.9866 dpi). Toleranz 0.1.
        Assert.Equal(96.0, meta.DpiX, precision: 0);
        Assert.Equal(96.0, meta.DpiY, precision: 0);
    }

    [Fact]
    public void GetMetadata_GroesseresPng_LiefertKorrekteDimensionen()
    {
        const int width = 32;
        const int height = 24;
        int stride = width * 4;
        byte[] pixels = new byte[stride * height];

        var bmp = BitmapSource.Create(
            width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bmp));
        using var ms = new MemoryStream();
        encoder.Save(ms);

        var analyzer = new WpfImageBitmapAnalyzer();
        var meta = analyzer.GetMetadata(ms.ToArray());

        Assert.Equal(width, meta.PixelWidth);
        Assert.Equal(height, meta.PixelHeight);
    }
}
