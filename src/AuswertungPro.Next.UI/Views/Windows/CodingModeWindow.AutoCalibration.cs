using System.IO;
using System.Windows.Media.Imaging;

namespace AuswertungPro.Next.UI.Views.Windows;

// Slice 8a Auto-Kalibrierung-Wiring Step 1 — PNG-Decoder-Helper.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-auto-kalibrierung.md
//
// Step 2 fuegt TryAutoCalibrateOnceAsync hinzu, Step 3 verdrahtet das
// Ganze im LiveLoop. Step 1 liefert nur den getesteten Decoder.
public partial class CodingModeWindow
{
    /// <summary>Decodiert PNG-Bytes (wie sie CaptureCurrentFrameAsync liefert)
    /// in eine BitmapSource fuer AutoCalibrationService.TryAutoCalibrate.
    /// Returns null wenn pngBytes null/empty/korrupt sind — wirft nicht.</summary>
    internal static BitmapSource? DecodePngToBitmap(byte[]? pngBytes)
    {
        if (pngBytes is null || pngBytes.Length == 0) return null;
        try
        {
            using var stream = new MemoryStream(pngBytes);
            var decoder = new PngBitmapDecoder(
                stream,
                BitmapCreateOptions.PreservePixelFormat,
                BitmapCacheOption.OnLoad);
            if (decoder.Frames.Count == 0) return null;
            var frame = decoder.Frames[0];
            frame.Freeze();
            return frame;
        }
        catch
        {
            // Korrupte / nicht-PNG-Bytes — silently null statt Throw,
            // damit der Auto-Calibration-Pfad einen erkennbaren Fehler-
            // Indikator hat ohne den Live-Loop zu kippen.
            return null;
        }
    }
}
