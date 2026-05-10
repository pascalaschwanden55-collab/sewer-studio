using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Tests fuer Slice 8a Auto-Kalibrierung-Wiring Step 1 — DecodePngToBitmap.
// Mini-ADR: docs/adrs/2026-05-10-slice-8a-auto-kalibrierung.md
//
// Bewusst klein gehalten (User-Praezisierung 2026-05-10): Roundtrip mit
// einem programmatisch erzeugten PNG, plus zwei Fehlfaelle. Calibration-
// Logik selbst lebt im AutoCalibrationService und ist nicht Teil dieses
// Slices.
public class CodingModeWindowAutoCalibrationDecoderTests
{
    [Fact]
    public void DecodePngToBitmap_RoundTripsValidPng()
    {
        var pngBytes = BuildSmallSolidPng(width: 32, height: 32);

        var bitmap = CodingModeWindow.DecodePngToBitmap(pngBytes);

        Assert.NotNull(bitmap);
        Assert.Equal(32, bitmap!.PixelWidth);
        Assert.Equal(32, bitmap.PixelHeight);
        Assert.True(bitmap.IsFrozen);
    }

    [Fact]
    public void DecodePngToBitmap_NullOrEmpty_ReturnsNull()
    {
        Assert.Null(CodingModeWindow.DecodePngToBitmap(null));
        Assert.Null(CodingModeWindow.DecodePngToBitmap(System.Array.Empty<byte>()));
    }

    [Fact]
    public void DecodePngToBitmap_CorruptBytes_ReturnsNullWithoutThrow()
    {
        var notAPng = new byte[] { 0x00, 0x01, 0x02, 0x03, 0x42, 0x43, 0x44 };

        // Darf nicht throwen — der Live-Loop verlaesst sich auf null als
        // Indikator.
        var bitmap = CodingModeWindow.DecodePngToBitmap(notAPng);

        Assert.Null(bitmap);
    }

    /// <summary>Erzeugt ein einfaches Solid-Color-PNG via PngBitmapEncoder.</summary>
    private static byte[] BuildSmallSolidPng(int width, int height)
    {
        var pixels = new byte[width * height * 4];
        for (int i = 0; i < pixels.Length; i += 4)
        {
            pixels[i + 0] = 0x40; // B
            pixels[i + 1] = 0x80; // G
            pixels[i + 2] = 0xC0; // R
            pixels[i + 3] = 0xFF; // A
        }
        var bitmap = BitmapSource.Create(
            width, height, 96, 96,
            PixelFormats.Bgra32, null,
            pixels, width * 4);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return ms.ToArray();
    }
}
