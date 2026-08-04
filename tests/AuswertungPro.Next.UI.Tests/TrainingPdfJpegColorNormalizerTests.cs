using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.UI.Services;

namespace AuswertungPro.Next.UI.Tests;

public sealed class TrainingPdfJpegColorNormalizerTests
{
    [Fact]
    public async Task DeviceCmykYcck_MitQuellkorrekturUndPdfIdentitaet_WirdRot()
    {
        var request = new TrainingPdfJpegColorNormalizationRequest(
            YcckJpeg,
            8,
            8,
            8,
            TrainingPdfJpegColorModel.Cmyk,
            [0, 1, 0, 1, 0, 1, 0, 1],
            InvertSourceSamples: true);
        var normalizer = new TrainingPdfJpegColorNormalizer();

        var result = await Task.Run(
            () =>
            {
                var success = normalizer.TryNormalizeToRgbPng(
                    request,
                    out var pngBytes);
                return (success, pngBytes);
            });

        Assert.True(result.success);
        var pixel = ReadCenterPixel(result.pngBytes);
        Assert.Equal(8, pixel.Width);
        Assert.Equal(8, pixel.Height);
        Assert.InRange(pixel.R, (byte)250, byte.MaxValue);
        Assert.InRange(pixel.G, byte.MinValue, (byte)5);
        Assert.InRange(pixel.B, byte.MinValue, (byte)5);
    }

    [Fact]
    public void DeviceCmyk_MitExpliziterInvertierung_WirdRot()
    {
        var request = new TrainingPdfJpegColorNormalizationRequest(
            CmykJpeg,
            8,
            8,
            8,
            TrainingPdfJpegColorModel.Cmyk,
            [1, 0, 1, 0, 1, 0, 1, 0],
            InvertSourceSamples: true);
        var normalizer = new TrainingPdfJpegColorNormalizer();

        var success = normalizer.TryNormalizeToRgbPng(
            request,
            out var pngBytes);

        Assert.True(success);
        var pixel = ReadCenterPixel(pngBytes);
        Assert.InRange(pixel.R, (byte)250, byte.MaxValue);
        Assert.InRange(pixel.G, byte.MinValue, (byte)5);
        Assert.InRange(pixel.B, byte.MinValue, (byte)5);
    }

    [Fact]
    public void DeviceCmyk_MitPdfIdentitaet_BleibtPdfGetreuDunkel()
    {
        var request = new TrainingPdfJpegColorNormalizationRequest(
            CmykJpeg,
            8,
            8,
            8,
            TrainingPdfJpegColorModel.Cmyk,
            [0, 1, 0, 1, 0, 1, 0, 1],
            InvertSourceSamples: true);
        var normalizer = new TrainingPdfJpegColorNormalizer();

        var success = normalizer.TryNormalizeToRgbPng(
            request,
            out var pngBytes);

        Assert.True(success);
        var pixel = ReadCenterPixel(pngBytes);
        Assert.InRange(pixel.R, byte.MinValue, (byte)5);
        Assert.InRange(pixel.G, byte.MinValue, (byte)5);
        Assert.InRange(pixel.B, byte.MinValue, (byte)5);
    }

    [Fact]
    public void DeviceCmykYcck_QuellkorrekturErfolgtVorExplizitemPdfDecode()
    {
        var request = new TrainingPdfJpegColorNormalizationRequest(
            YcckJpeg,
            8,
            8,
            8,
            TrainingPdfJpegColorModel.Cmyk,
            [1, 0, 1, 0, 1, 0, 1, 0],
            InvertSourceSamples: true);
        var normalizer = new TrainingPdfJpegColorNormalizer();

        var success = normalizer.TryNormalizeToRgbPng(
            request,
            out var pngBytes);

        Assert.True(success);
        var pixel = ReadCenterPixel(pngBytes);
        Assert.InRange(pixel.R, byte.MinValue, (byte)5);
        Assert.InRange(pixel.G, byte.MinValue, (byte)5);
        Assert.InRange(pixel.B, byte.MinValue, (byte)5);
    }

    [Fact]
    public void AbweichendePdfUndJpegAbmessungen_WerdenAbgelehnt()
    {
        var request = new TrainingPdfJpegColorNormalizationRequest(
            YcckJpeg,
            9,
            8,
            8,
            TrainingPdfJpegColorModel.Cmyk,
            [0, 1, 0, 1, 0, 1, 0, 1],
            InvertSourceSamples: true);
        var normalizer = new TrainingPdfJpegColorNormalizer();

        var success = normalizer.TryNormalizeToRgbPng(
            request,
            out var pngBytes);

        Assert.False(success);
        Assert.Empty(pngBytes);
    }

    private static (int Width, int Height, byte R, byte G, byte B) ReadCenterPixel(
        byte[] pngBytes)
    {
        using var stream = new MemoryStream(pngBytes, writable: false);
        var decoder = BitmapDecoder.Create(
            stream,
            BitmapCreateOptions.PreservePixelFormat,
            BitmapCacheOption.OnLoad);
        var source = new FormatConvertedBitmap(
            decoder.Frames[0],
            PixelFormats.Bgra32,
            null,
            0);
        var stride = checked(source.PixelWidth * 4);
        var pixels = new byte[checked(stride * source.PixelHeight)];
        source.CopyPixels(pixels, stride, 0);

        var x = source.PixelWidth / 2;
        var y = source.PixelHeight / 2;
        var offset = checked((y * stride) + (x * 4));
        return (
            source.PixelWidth,
            source.PixelHeight,
            pixels[offset + 2],
            pixels[offset + 1],
            pixels[offset]);
    }

    private static readonly byte[] YcckJpeg = Convert.FromBase64String(
        "/9j/7gAOQWRvYmUAZAAAAAAC/9sAQwABAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB" +
        "AQEBAQEBAQEBAQEB/8AAFAgACAAIBEMRAE0RAFkRAEsRAP/EAB8AAAEFAQEBAQEBAAAAAAAAAAABAgMEBQYHCAkKC//EALUQAAIB" +
        "AwMCBAMFBQQEAAABfQECAwAEEQUSITFBBhNRYQcicRQygZGhCCNCscEVUtHwJDNicoIJChYXGBkaJSYnKCkqNDU2Nzg5OkNERUZH" +
        "SElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6g4SFhoeIiYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV" +
        "1tfY2drh4uPk5ebn6Onq8fLz9PX29/j5+v/aAA4EQwBNAFkASwAAPwD8X6/J+v7+K/z/AOv/2Q==");

    private static readonly byte[] CmykJpeg = Convert.FromBase64String(
        "/9j/7gAOQWRvYmUAZAAAAAAA/9sAQwABAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB" +
        "AQEBAQEBAQEBAQEB/8AAFAgACAAIBEMRAE0RAFkRAEsRAP/EAB8AAAEFAQEBAQEBAAAAAAAAAAABAgMEBQYHCAkKC//EALUQAAIB" +
        "AwMCBAMFBQQEAAABfQECAwAEEQUSITFBBhNRYQcicRQygZGhCCNCscEVUtHwJDNicoIJChYXGBkaJSYnKCkqNDU2Nzg5OkNERUZH" +
        "SElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6g4SFhoeIiYqSk5SVlpeYmZqio6Slpqeoqaqys7S1tre4ubrCw8TFxsfIycrS09TV" +
        "1tfY2drh4uPk5ebn6Onq8fLz9PX29/j5+v/aAA4EQwBNAFkASwAAPwD+/iv8/wDr/P8A6/v4r//Z");
}
