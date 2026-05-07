using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Imaging;

namespace AuswertungPro.Next.UI.Imaging;

/// <summary>
/// WPF-Implementation des <see cref="IImagePixelDecoder"/>. Nutzt
/// <see cref="BitmapImage"/> + <see cref="FormatConvertedBitmap"/>, um
/// PNG/JPG-Bytes nach Bgra32-Pixeln zu wandeln.
///
/// Wird in App.xaml.cs einmalig registriert:
/// <code>ImagePixelDecoderProvider.SetDecoder(new WpfImagePixelDecoder());</code>
/// </summary>
public sealed class WpfImagePixelDecoder : IImagePixelDecoder
{
    public DecodedImage? DecodeBgra32(byte[] imageBytes, int? maxWidth = null)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return null;

        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            if (maxWidth is { } width && width > 0)
                bi.DecodePixelWidth = width;
            bi.EndInit();
            bi.Freeze();

            BitmapSource src = bi;
            if (bi.Format != PixelFormats.Bgra32)
            {
                var converted = new FormatConvertedBitmap(bi, PixelFormats.Bgra32, null, 0);
                converted.Freeze();
                src = converted;
            }

            int stride = src.PixelWidth * 4;
            byte[] pixels = new byte[stride * src.PixelHeight];
            src.CopyPixels(pixels, stride, 0);

            return new DecodedImage(
                Bgra32Pixels: pixels,
                Width: src.PixelWidth,
                Height: src.PixelHeight,
                Stride: stride);
        }
        catch
        {
            return null;
        }
    }
}
