using System;
using System.Threading.Tasks;
using AuswertungPro.Next.Application.Ai;
using AuswertungPro.Next.Infrastructure.Ai.Pipeline;
using Xunit;

namespace AuswertungPro.Next.UI.Tests;

/// <summary>
/// Tests fuer den ausgelagerten SAM-Box-Segmentierungs-Service (haelt die Logik aus
/// dem Codiermodus-Window heraus und macht sie ohne Sidecar/UI testbar).
/// </summary>
public class MarkBoxSegmentationServiceTests
{
    // Minimaler PNG-Header (24 Bytes): Signatur + Breite/Hoehe (Big-Endian) ab Offset 16.
    private static byte[] FakePng(int width, int height)
    {
        var b = new byte[24];
        b[0] = 0x89; b[1] = 0x50; b[2] = 0x4E; b[3] = 0x47;
        b[16] = (byte)(width >> 24); b[17] = (byte)(width >> 16); b[18] = (byte)(width >> 8); b[19] = (byte)width;
        b[20] = (byte)(height >> 24); b[21] = (byte)(height >> 16); b[22] = (byte)(height >> 8); b[23] = (byte)height;
        return b;
    }

    private static NormalizedBoundingBox Box(double xc, double yc, double w, double h)
        => new() { XCenter = xc, YCenter = yc, Width = w, Height = h };

    private static SamMaskResult Mask() => new(
        Label: "manuell", Confidence: 0.9,
        Bbox: new double[] { 400, 320, 600, 480 },
        MaskRle: "", MaskAreaPixels: 5000, ImageAreaPixels: 1000 * 800,
        HeightPixels: 160, WidthPixels: 200,
        CentroidX: 700, CentroidY: 400);

    [Fact]
    public async Task SegmentBoxAsync_konvertiert_Box_in_Pixel_und_quantifiziert()
    {
        SamRequest? captured = null;
        var service = new MarkBoxSegmentationService((req, ct) =>
        {
            captured = req;
            return Task.FromResult(new SamResponse(new[] { Mask() }, 1000, 800, 12.0));
        });

        var result = await service.SegmentBoxAsync(
            FakePng(1000, 800), Box(0.5, 0.5, 0.2, 0.2), pipeDiameterMm: 300, calibration: null);

        Assert.NotNull(result);
        Assert.NotNull(captured);
        Assert.Single(captured!.BoundingBoxes);
        // Mitte 0.5/0.5, Groesse 0.2 -> Ecken (0.4..0.6) * Bildmasse.
        Assert.Equal(400, captured.BoundingBoxes[0].X1, 0);
        Assert.Equal(600, captured.BoundingBoxes[0].X2, 0);
        Assert.Equal(320, captured.BoundingBoxes[0].Y1, 0);
        Assert.Equal(480, captured.BoundingBoxes[0].Y2, 0);
        Assert.Equal(300, captured.PipeDiameterMm);
        Assert.True(result!.Quant.HeightMm > 0);
        Assert.True(result.Quant.WidthMm > 0);
        Assert.False(string.IsNullOrEmpty(result.Quant.ClockPosition));
    }

    [Fact]
    public async Task SegmentBoxAsync_ohne_Maske_gibt_null()
    {
        var service = new MarkBoxSegmentationService((req, ct) =>
            Task.FromResult(new SamResponse(Array.Empty<SamMaskResult>(), 1000, 800, 1.0)));

        var result = await service.SegmentBoxAsync(FakePng(1000, 800), Box(0.5, 0.5, 0.2, 0.2), 300, null);

        Assert.Null(result);
    }

    [Fact]
    public async Task SegmentBoxAsync_ungueltiger_Frame_gibt_null_ohne_SAM_Aufruf()
    {
        var called = false;
        var service = new MarkBoxSegmentationService((req, ct) =>
        {
            called = true;
            return Task.FromResult(new SamResponse(new[] { Mask() }, 1000, 800, 1.0));
        });

        // Kein gueltiger PNG-Header -> frueher Abbruch, SAM wird nicht aufgerufen.
        var result = await service.SegmentBoxAsync(new byte[] { 1, 2, 3 }, Box(0.5, 0.5, 0.2, 0.2), 300, null);

        Assert.Null(result);
        Assert.False(called);
    }
}
