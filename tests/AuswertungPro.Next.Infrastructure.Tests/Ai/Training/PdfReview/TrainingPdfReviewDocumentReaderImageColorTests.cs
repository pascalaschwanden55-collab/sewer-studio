using System.Security.Cryptography;
using System.Text;
using AuswertungPro.Next.Application.UseCases.PdfTrainingReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfReviewDocumentReaderImageColorTests
    : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        $"sewerstudio_pdf_image_color_{Guid.NewGuid():N}");

    [Fact]
    public void Read_DeviceCmykYcckJpeg_NormalisiertDiePdfFarbenVorDerAblage()
    {
        var pdfPath = WritePhotoPdf(CmykYcckJpeg, declareDeviceCmyk: true);
        var originalHash = ComputeSha256(pdfPath);
        var normalizer = new RecordingJpegColorNormalizer();

        var result = new TrainingPdfReviewDocumentReader(normalizer)
            .Read(pdfPath, CancellationToken.None);

        Assert.Empty(result.Issues);
        var photo = Assert.Single(result.Photos);
        Assert.Equal(".png", photo.Extension);
        var request = Assert.IsType<TrainingPdfJpegColorNormalizationRequest>(
            normalizer.LastRequest);
        Assert.Equal(TrainingPdfJpegColorModel.Cmyk, request.ColorModel);
        Assert.Equal(8, request.PixelWidth);
        Assert.Equal(8, request.PixelHeight);
        Assert.Equal(8, request.BitsPerComponent);
        Assert.Equal(
            new decimal[] { 0, 1, 0, 1, 0, 1, 0, 1 },
            request.Decode);
        Assert.True(request.InvertSourceSamples);
        Assert.Equal(CmykYcckJpeg, request.JpegBytes);

        Assert.Equal(RedPng, photo.ImageBytes);
        Assert.Equal(originalHash, ComputeSha256(pdfPath));
    }

    [Fact]
    public void Read_DeviceCmykYcckJpeg_OhneNormalisierer_SpeichertKeineFalschfarben()
    {
        var pdfPath = WritePhotoPdf(CmykYcckJpeg, declareDeviceCmyk: true);

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Empty(result.Photos);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains(
                "Bildformat nicht lesbar",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Read_DeviceCmykJpeg_OhneAdobeMarker_WirdSicherAusgelassen()
    {
        var pdfPath = WritePhotoPdf(
            RemoveAdobeApp14(CmykJpeg),
            declareDeviceCmyk: true);
        var normalizer = new RecordingJpegColorNormalizer();

        var result = new TrainingPdfReviewDocumentReader(normalizer)
            .Read(pdfPath, CancellationToken.None);

        Assert.Empty(result.Photos);
        Assert.Null(normalizer.LastRequest);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains(
                "Bildformat nicht lesbar",
                StringComparison.Ordinal));
    }

    [Fact]
    public void Read_DeviceCmykJpeg_OhneDecode_VerwendetPdfIdentitaet()
    {
        var pdfPath = WritePhotoPdf(CmykJpeg, declareDeviceCmyk: true);
        var normalizer = new RecordingJpegColorNormalizer();

        var result = new TrainingPdfReviewDocumentReader(normalizer)
            .Read(pdfPath, CancellationToken.None);

        Assert.Empty(result.Issues);
        Assert.Single(result.Photos);
        var request = Assert.IsType<TrainingPdfJpegColorNormalizationRequest>(
            normalizer.LastRequest);
        Assert.Equal(
            new decimal[] { 0, 1, 0, 1, 0, 1, 0, 1 },
            request.Decode);
        Assert.True(request.InvertSourceSamples);
    }

    [Fact]
    public void Read_NormalesRgbJpeg_BleibtBytegleichUndBrauchtKeinenNormalisierer()
    {
        var pdfPath = WritePhotoPdf(RgbJpeg);
        var normalizer = new RecordingJpegColorNormalizer();

        var result = new TrainingPdfReviewDocumentReader(normalizer)
            .Read(pdfPath, CancellationToken.None);

        var photo = Assert.Single(result.Photos);
        Assert.Equal(".jpg", photo.Extension);
        Assert.Equal(RgbJpeg, photo.ImageBytes);
        Assert.Null(normalizer.LastRequest);
    }

    [Fact]
    public void Read_JpegMitAbweichenderPdfBreite_WirdAusgelassen()
    {
        var pdfPath = WritePhotoPdf(
            RgbJpeg,
            declareWrongWidth: true);

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Empty(result.Photos);
        Assert.Contains(
            result.Issues,
            issue => issue.Contains(
                "Bildformat nicht lesbar",
                StringComparison.Ordinal));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private string WritePhotoPdf(
        byte[] jpegBytes,
        bool declareDeviceCmyk = false,
        bool declareWrongWidth = false)
    {
        Directory.CreateDirectory(_tempRoot);
        var path = Path.Combine(_tempRoot, "100-200.pdf");
        using var builder = new PdfDocumentBuilder();
        var page = builder.AddPage(PageSize.A4);
        page.AddJpeg(
            jpegBytes,
            new PdfRectangle(40, 390, 340, 615));
        var pdfBytes = builder.Build();
        if (declareDeviceCmyk)
            DeclareEmbeddedJpegAsDeviceCmyk(pdfBytes);
        if (declareWrongWidth)
            ChangeDeclaredImageWidth(pdfBytes);

        File.WriteAllBytes(path, pdfBytes);
        return path;
    }

    private static void DeclareEmbeddedJpegAsDeviceCmyk(byte[] pdfBytes)
    {
        var rgbToken = Encoding.ASCII.GetBytes("/DeviceRGB");
        var cmykToken = Encoding.ASCII.GetBytes("/DeviceCMYK");
        var tokenIndex = pdfBytes.AsSpan().IndexOf(rgbToken);
        Assert.True(tokenIndex >= 0, "Das synthetische PDF enthält keinen DeviceRGB-Eintrag.");

        var delimiterIndex = tokenIndex + rgbToken.Length;
        Assert.True(delimiterIndex < pdfBytes.Length);
        Assert.True(
            IsPdfWhitespace(pdfBytes[delimiterIndex]),
            "Hinter DeviceRGB fehlt das erwartete PDF-Trennzeichen.");

        cmykToken.CopyTo(pdfBytes.AsSpan(tokenIndex, cmykToken.Length));
    }

    private static bool IsPdfWhitespace(byte value) =>
        value is 0 or 9 or 10 or 12 or 13 or 32;

    private static void ChangeDeclaredImageWidth(byte[] pdfBytes)
    {
        var original = Encoding.ASCII.GetBytes("/Width 8");
        var replacement = Encoding.ASCII.GetBytes("/Width 9");
        var tokenIndex = pdfBytes.AsSpan().IndexOf(original);
        Assert.True(
            tokenIndex >= 0,
            "Das synthetische PDF enthält nicht die erwartete Bildbreite.");
        replacement.CopyTo(pdfBytes.AsSpan(tokenIndex, replacement.Length));
    }

    private static byte[] RemoveAdobeApp14(byte[] jpegBytes)
    {
        Assert.True(jpegBytes.Length > 18);
        Assert.Equal((byte)0xff, jpegBytes[2]);
        Assert.Equal((byte)0xee, jpegBytes[3]);
        var segmentLength = (jpegBytes[4] << 8) | jpegBytes[5];
        var bytesToRemove = 2 + segmentLength;
        var result = new byte[jpegBytes.Length - bytesToRemove];
        jpegBytes.AsSpan(0, 2).CopyTo(result);
        jpegBytes.AsSpan(2 + bytesToRemove).CopyTo(result.AsSpan(2));
        return result;
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(SHA256.HashData(stream));
    }

    private sealed class RecordingJpegColorNormalizer
        : ITrainingPdfJpegColorNormalizer
    {
        public TrainingPdfJpegColorNormalizationRequest? LastRequest { get; private set; }

        public bool TryNormalizeToRgbPng(
            TrainingPdfJpegColorNormalizationRequest request,
            out byte[] pngBytes)
        {
            LastRequest = request;
            pngBytes = RedPng;
            return true;
        }
    }

    private static readonly byte[] RedPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAgAAAAICAIAAABLbSncAAAAEklEQVR4nGP8z4AdMOEQH6QSAM1BAQ/oQeJvAAAAAElFTkSuQmCC");

    // Rein synthetisches 8x8-Adobe-YCCK-JPEG. Die vier Rohkanaele ergeben
    // nach der PDF-Standard-Dekodierung ein rotes RGB-Bild.
    private static readonly byte[] CmykYcckJpeg = Convert.FromBase64String(
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

    private static readonly byte[] RgbJpeg = Convert.FromBase64String(
        "/9j/4AAQSkZJRgABAQAAAQABAAD/2wBDAAEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB" +
        "AQEBAQEBAQEBAQEBAQH/2wBDAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEBAQEB" +
        "AQEBAQEBAQH/wAARCAAIAAgDAREAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUF" +
        "BAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVW" +
        "V1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi" +
        "4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAEC" +
        "AxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVm" +
        "Z2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq" +
        "8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD+Z+v+pg/kc//Z");
}
