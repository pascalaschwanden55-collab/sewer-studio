using System.IO;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Services;

/// <summary>
/// Liest nur den Bildkopf und die echten Pixelmasse eines Trainingsbilds. Der
/// Pixelinhalt wird nicht vollständig dekodiert; der Dateistream wird sofort wieder
/// freigegeben, damit die Warteschlange keine Kunden- oder Golddatei sperrt.
/// </summary>
internal static class TrainingImageFileProbe
{
    private const long MaximumDecodedBytes = 256L * 1024 * 1024;

    public static (int Width, int Height)? ReadDimensions(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return null;

        using var stream = File.OpenRead(imagePath);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
            BitmapCacheOption.None);
        var frame = decoder.Frames.FirstOrDefault();
        return frame is null || frame.PixelWidth <= 0 || frame.PixelHeight <= 0
            ? null
            : (frame.PixelWidth, frame.PixelHeight);
    }

    /// <summary>
    /// Erzwingt für ausgewählte Reparaturbilder einmal das vollständige Dekodieren.
    /// So gelangt keine abgeschnittene Datei mit noch lesbarem Bildkopf zur Anzeige.
    /// </summary>
    public static bool CanDecode(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath))
            return false;

        try
        {
            using var stream = File.OpenRead(imagePath);
            var decoder = BitmapDecoder.Create(
                stream,
                BitmapCreateOptions.PreservePixelFormat | BitmapCreateOptions.IgnoreColorProfile,
                BitmapCacheOption.OnLoad);
            var frame = decoder.Frames.FirstOrDefault();
            if (frame is null
                || frame.PixelWidth <= 0
                || frame.PixelHeight <= 0
                || frame.Format.BitsPerPixel <= 0)
            {
                return false;
            }

            var stride = checked((frame.PixelWidth * frame.Format.BitsPerPixel + 7) / 8);
            var decodedBytes = checked((long)stride * frame.PixelHeight);
            if (decodedBytes <= 0 || decodedBytes > MaximumDecodedBytes)
                return false;

            var pixels = new byte[(int)decodedBytes];
            frame.CopyPixels(pixels, stride, 0);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
