using System.IO;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Common;

/// <summary>
/// Frame-Capture-Robustheits-Helper (Deep-Dive Punkt #8, Cherry-Pick aus
/// archive/2026-05-10-robustifizierungen). Pure-Function, ohne Window-State.
///
/// Zweck: vor SAM/Qwen-Aufruf pruefen ob ein gecaptureter Frame
/// brauchbar ist. Vorher konnten schwarze/uniforme Frames an SAM oder
/// Qwen gehen — Falsch-Klassifikation oder leere Maske die Pipeline
/// kaputt macht. Jetzt: vor dem Aufruf Validierung, bei Failure einen
/// User-sichtbaren Hint zurueck statt stillem null.
///
/// Heuristik:
///   - Mindest-Bytes (anti-truncated PNG)
///   - Mindest-Aufloesung 32x32
///   - Helligkeits-Spread auf Mittellinie >= 20 (von 255)
///     -> verhindert komplett-schwarze/-weisse/-uniforme Frames.
///
/// Statisch, testbar — keine Window-State-Abhaengigkeit.
/// </summary>
public static class FrameValidation
{
    /// <summary>true wenn der PNG-Bytes-Array einen brauchbaren Frame
    /// enthaelt. Catch-all bei Decoder-Fehlern oder Format-Problemen.</summary>
    public static bool IsFrameValid(byte[]? pngBytes)
    {
        if (pngBytes == null || pngBytes.Length < 200) return false;

        try
        {
            using var ms = new MemoryStream(pngBytes);
            var decoder = BitmapDecoder.Create(
                ms,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return false;

            var frame = decoder.Frames[0];
            int w = frame.PixelWidth;
            int h = frame.PixelHeight;
            if (w < 32 || h < 32) return false;

            int bytesPerPixel = (frame.Format.BitsPerPixel + 7) / 8;
            if (bytesPerPixel < 1) return false;

            int stride = (w * frame.Format.BitsPerPixel + 7) / 8;
            var pixels = new byte[stride * h];
            frame.CopyPixels(pixels, stride, 0);

            // Sampling: 100 Pixel auf der Mittellinie quer durchs Bild.
            const int sampleCount = 100;
            int midY = h / 2;
            byte minVal = 255, maxVal = 0;
            for (int i = 0; i < sampleCount; i++)
            {
                int x = (i * (w - 1)) / (sampleCount - 1);
                int idx = midY * stride + x * bytesPerPixel;
                if (idx + 2 >= pixels.Length) continue;

                byte c1 = pixels[idx];
                byte c2 = bytesPerPixel >= 2 ? pixels[idx + 1] : c1;
                byte c3 = bytesPerPixel >= 3 ? pixels[idx + 2] : c1;
                byte luma = (byte)((c1 + c2 + c3) / 3);

                if (luma < minVal) minVal = luma;
                if (luma > maxVal) maxVal = luma;
            }

            return (maxVal - minVal) >= 20;
        }
        catch
        {
            return false;
        }
    }
}
