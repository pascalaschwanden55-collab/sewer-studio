// AuswertungPro – KI Videoanalyse Modul
using System;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Ai;

/// <summary>
/// Korrigiert Farbprobleme bei PDF-extrahierten Bildern.
/// Manche WinCan/IKAS-PDFs speichern JPEGs im CMYK-Farbraum mit invertiertem
/// Decode-Array. PdfPig extrahiert die rohen Bytes ohne Farbraum-Konvertierung.
///
/// Hauptfunktion: <see cref="ConvertCmykJpegToRgbPng"/> — erkennt CMYK-JPEGs
/// und konvertiert sie korrekt zu RGB-PNGs.
/// </summary>
public static class ImageInversionHelper
{
    private const double InvertedLuminanceThreshold = 170.0;
    private const double ColorChannelMinLuminance = 100.0;
    private const double BlueRedDifferenceThreshold = 20.0;

    /// <summary>
    /// Prueft ob ein Bild (PNG oder JPEG) wahrscheinlich farbinvertiert ist.
    /// Nutzt Luminanz-Heuristik UND Farbkanal-Analyse (Blau > Rot = lila-invertiert).
    /// </summary>
    public static bool IsLikelyInverted(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length < 100)
            return false;

        try
        {
            using var ms = new MemoryStream(imageBytes);
            var bi = new BitmapImage();
            bi.BeginInit();
            bi.StreamSource = ms;
            bi.CacheOption = BitmapCacheOption.OnLoad;
            bi.DecodePixelWidth = 80; // Klein laden fuer Geschwindigkeit
            bi.EndInit();
            bi.Freeze();

            var wb = new WriteableBitmap(bi);
            int stride = wb.PixelWidth * 4;
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            long totalLum = 0;
            long totalR = 0, totalG = 0, totalB = 0;
            int count = 0;

            for (int i = 0; i < pixels.Length - 3; i += 4)
            {
                int b = pixels[i], g = pixels[i + 1], r = pixels[i + 2];
                totalLum += (r * 299 + g * 587 + b * 114) / 1000;
                totalR += r;
                totalG += g;
                totalB += b;
                count++;
            }

            if (count == 0) return false;

            double avgLum = (double)totalLum / count;
            double avgR = (double)totalR / count;
            double avgB = (double)totalB / count;

            // Heuristik 1: Klassisches helles Negativ
            if (avgLum > InvertedLuminanceThreshold)
                return true;

            // Heuristik 2: Lila-invertiertes Kanalbild (WinCan-PDF Decode-Array)
            // Normale Kanalbilder: Rot >= Blau (braun/grau Rohrwandung)
            // Invertierte Kanalbilder: Blau >> Rot (lila/violetter Stich)
            if (avgLum > ColorChannelMinLuminance && avgB > avgR + BlueRedDifferenceThreshold)
                return true;

            return false;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Invertiert die Farben eines Bildes (Negativ → Positiv).
    /// Funktioniert mit PNG und JPEG. Gibt PNG-Bytes zurueck.
    /// </summary>
    public static byte[] InvertColors(byte[] imageBytes)
    {
        try
        {
            using var inputMs = new MemoryStream(imageBytes);
            var decoder = BitmapDecoder.Create(
                inputMs,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];
            var wb = new WriteableBitmap(frame);
            int bytesPerPixel = wb.Format.BitsPerPixel / 8;
            int stride = wb.PixelWidth * bytesPerPixel;
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            // RGB invertieren, Alpha-Kanal behalten
            int alphaOffset = bytesPerPixel == 4 ? 3 : -1;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (alphaOffset >= 0 && (i % bytesPerPixel) == alphaOffset)
                    continue;
                pixels[i] = (byte)(255 - pixels[i]);
            }

            wb.WritePixels(
                new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight),
                pixels, stride, 0);

            // Als PNG encodieren
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(wb));

            using var outputMs = new MemoryStream();
            encoder.Save(outputMs);
            return outputMs.ToArray();
        }
        catch
        {
            return imageBytes; // Fallback: Original zurueckgeben
        }
    }

    /// <summary>
    /// Prueft und korrigiert automatisch invertierte Bilder.
    /// Gibt die (ggf. korrigierten) Bytes zurueck.
    /// </summary>
    public static byte[] AutoCorrect(byte[] imageBytes)
    {
        if (imageBytes is null || imageBytes.Length == 0)
            return imageBytes!;

        if (IsLikelyInverted(imageBytes))
            return InvertColors(imageBytes);

        return imageBytes;
    }

    /// <summary>
    /// Konvertiert ein JPEG-Bild (moeglicherweise CMYK/YCCK aus PDF) zu einem
    /// korrekt dargestellten PNG. WPF's BitmapDecoder handhabt CMYK→RGB
    /// automatisch, daher laden wir einfach das JPEG und speichern als PNG.
    /// </summary>
    public static byte[]? ConvertJpegToPng(byte[] jpegBytes)
    {
        if (jpegBytes is null || jpegBytes.Length < 100)
            return null;

        try
        {
            using var inputMs = new MemoryStream(jpegBytes);
            var decoder = BitmapDecoder.Create(
                inputMs,
                BitmapCreateOptions.None,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];

            // Sicherstellen dass wir RGB bekommen (CMYK → RGB Konvertierung)
            BitmapSource source = frame;
            if (frame.Format != PixelFormats.Bgr32 &&
                frame.Format != PixelFormats.Bgra32 &&
                frame.Format != PixelFormats.Rgb24 &&
                frame.Format != PixelFormats.Pbgra32)
            {
                source = new FormatConvertedBitmap(frame, PixelFormats.Bgr32, null, 0);
            }

            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(source));

            using var outputMs = new MemoryStream();
            encoder.Save(outputMs);
            return outputMs.ToArray();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// JPEG-spezifische Variante fuer PdfProtocolExtractor.
    /// Gibt JPEG-Bytes zurueck (nicht PNG).
    /// </summary>
    public static byte[] InvertJpegColors(byte[] jpegBytes)
    {
        try
        {
            using var inputMs = new MemoryStream(jpegBytes);
            var decoder = new JpegBitmapDecoder(
                inputMs,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);

            var frame = decoder.Frames[0];
            var wb = new WriteableBitmap(frame);
            int bytesPerPixel = wb.Format.BitsPerPixel / 8;
            int stride = wb.PixelWidth * bytesPerPixel;
            byte[] pixels = new byte[stride * wb.PixelHeight];
            wb.CopyPixels(pixels, stride, 0);

            int alphaOffset = bytesPerPixel == 4 ? 3 : -1;
            for (int i = 0; i < pixels.Length; i++)
            {
                if (alphaOffset >= 0 && (i % bytesPerPixel) == alphaOffset)
                    continue;
                pixels[i] = (byte)(255 - pixels[i]);
            }

            wb.WritePixels(
                new System.Windows.Int32Rect(0, 0, wb.PixelWidth, wb.PixelHeight),
                pixels, stride, 0);

            var encoder = new JpegBitmapEncoder { QualityLevel = 92 };
            encoder.Frames.Add(BitmapFrame.Create(wb));

            using var outputMs = new MemoryStream();
            encoder.Save(outputMs);
            return outputMs.ToArray();
        }
        catch
        {
            return jpegBytes;
        }
    }
}
