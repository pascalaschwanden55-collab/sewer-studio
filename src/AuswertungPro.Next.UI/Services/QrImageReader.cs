using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.Import;
using ZXing;
using ZXing.Common;

namespace AuswertungPro.Next.UI.Services;

/// <summary>Windows-Bildadapter. Pixeldecodierung bleibt ausserhalb der Fenster und Fachlogik.</summary>
public sealed class QrImageReader : IQrImageReader
{
    public IReadOnlyList<string> Read(string path, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!new[] { ".png", ".jpg", ".jpeg" }.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
            throw new InvalidDataException("Bitte einen PNG- oder JPG-Screenshot wählen.");
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (stream.Length > 32 * 1024 * 1024) throw new InvalidDataException("QR-Bild ist grösser als 32 MB.");
        var decoder = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnDemand);
        if (decoder.Frames.Count != 1) throw new InvalidDataException("Bitte ein Bild mit genau einer Seite verwenden.");
        var frame = decoder.Frames[0];
        var pixelsCount = (long)frame.PixelWidth * frame.PixelHeight;
        if (pixelsCount <= 0 || pixelsCount > 24_000_000)
            throw new InvalidDataException("QR-Bild ist zu gross (maximal 24 Millionen Bildpunkte).");
        cancellationToken.ThrowIfCancellationRequested();
        var bitmap = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        var pixels = new byte[checked((int)pixelsCount * 4)];
        bitmap.CopyPixels(pixels, frame.PixelWidth * 4, 0);
        var reader = new BarcodeReaderGeneric
        {
            AutoRotate = true,
            Options = new DecodingOptions { TryHarder = true, TryInverted = true, PossibleFormats = [BarcodeFormat.QR_CODE] }
        };
        var results = reader.DecodeMultiple(pixels, frame.PixelWidth, frame.PixelHeight, RGBLuminanceSource.BitmapFormat.BGRA32);
        cancellationToken.ThrowIfCancellationRequested();
        return results?.Select(r => r.Text).ToArray() ?? [];
    }
}
