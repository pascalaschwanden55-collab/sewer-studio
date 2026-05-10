using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.UI.Views.Windows;
using Xunit;

namespace AuswertungPro.Next.Pipeline.Tests;

// Slice 8a.6.A 2026-05-10: Tests fuer CodingModeWindow.IsFrameValid.
// Statische Helper-Methode, deshalb klassisch testbar — Window-State
// wird nicht beruehrt.
[Trait("Category", "Unit")]
public sealed class CodingModeFrameValidityTests
{
    [Fact]
    public void IsFrameValid_NullBytes_ReturnsFalse()
    {
        Assert.False(CodingModeWindow.IsFrameValid(null));
    }

    [Fact]
    public void IsFrameValid_EmptyBytes_ReturnsFalse()
    {
        Assert.False(CodingModeWindow.IsFrameValid(System.Array.Empty<byte>()));
    }

    [Fact]
    public void IsFrameValid_TooSmallBytes_ReturnsFalse()
    {
        // Unter 200 Bytes — wird sofort abgelehnt.
        var bytes = new byte[100];
        Assert.False(CodingModeWindow.IsFrameValid(bytes));
    }

    [Fact]
    public void IsFrameValid_RandomGarbageBytes_ReturnsFalse()
    {
        // 500 Random Bytes ohne PNG-Header — Decoder schlaegt fehl,
        // IsFrameValid faengt das ab.
        var rng = new System.Random(42);
        var bytes = new byte[500];
        rng.NextBytes(bytes);
        Assert.False(CodingModeWindow.IsFrameValid(bytes));
    }

    [Fact]
    public void IsFrameValid_AllBlack_ReturnsFalse()
    {
        var pngBytes = MakeSolidColorPng(Colors.Black, 64, 64);
        Assert.False(CodingModeWindow.IsFrameValid(pngBytes));
    }

    [Fact]
    public void IsFrameValid_AllWhite_ReturnsFalse()
    {
        var pngBytes = MakeSolidColorPng(Colors.White, 64, 64);
        Assert.False(CodingModeWindow.IsFrameValid(pngBytes));
    }

    [Fact]
    public void IsFrameValid_AllUniformGray_ReturnsFalse()
    {
        var pngBytes = MakeSolidColorPng(Color.FromRgb(128, 128, 128), 64, 64);
        Assert.False(CodingModeWindow.IsFrameValid(pngBytes));
    }

    [Fact]
    public void IsFrameValid_TooSmallResolution_ReturnsFalse()
    {
        // Weniger als 32x32 wird abgelehnt, auch wenn Helligkeits-Spread ok.
        var pngBytes = MakeHorizontalGradientPng(20, 20);
        Assert.False(CodingModeWindow.IsFrameValid(pngBytes));
    }

    [Fact]
    public void IsFrameValid_HorizontalGradient_ReturnsTrue()
    {
        // Schwarz-zu-Weiss-Gradient quer ueber 100px → Spread > 20.
        var pngBytes = MakeHorizontalGradientPng(100, 64);
        Assert.True(CodingModeWindow.IsFrameValid(pngBytes));
    }

    [Fact]
    public void IsFrameValid_DarkButVarying_ReturnsTrue()
    {
        // Inspektions-typisch: dunkles Bild, aber mit Spread (Rohrwand vs.
        // Lichtkegel). Spread = 60..80 ist realistisch.
        var pngBytes = MakeBoundedGradientPng(64, 64, minLuma: 20, maxLuma: 100);
        Assert.True(CodingModeWindow.IsFrameValid(pngBytes));
    }

    // ─── Helper: synthetische PNGs erzeugen ─────────────────────────

    private static byte[] MakeSolidColorPng(Color c, int w, int h)
    {
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var dv = new System.Windows.Media.DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            dc.DrawRectangle(new SolidColorBrush(c), null,
                new System.Windows.Rect(0, 0, w, h));
        }
        rtb.Render(dv);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }

    private static byte[] MakeHorizontalGradientPng(int w, int h)
    {
        return MakeBoundedGradientPng(w, h, minLuma: 0, maxLuma: 255);
    }

    private static byte[] MakeBoundedGradientPng(int w, int h, byte minLuma, byte maxLuma)
    {
        var rtb = new RenderTargetBitmap(w, h, 96, 96, PixelFormats.Pbgra32);
        var dv = new System.Windows.Media.DrawingVisual();
        using (var dc = dv.RenderOpen())
        {
            for (int x = 0; x < w; x++)
            {
                double t = w <= 1 ? 0 : x / (double)(w - 1);
                byte luma = (byte)(minLuma + t * (maxLuma - minLuma));
                var brush = new SolidColorBrush(Color.FromRgb(luma, luma, luma));
                dc.DrawRectangle(brush, null, new System.Windows.Rect(x, 0, 1, h));
            }
        }
        rtb.Render(dv);

        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(rtb));
        using var ms = new MemoryStream();
        enc.Save(ms);
        return ms.ToArray();
    }
}
