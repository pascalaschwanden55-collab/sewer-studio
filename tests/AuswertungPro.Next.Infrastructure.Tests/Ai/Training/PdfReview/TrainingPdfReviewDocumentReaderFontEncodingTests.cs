using AuswertungPro.Next.Infrastructure.Ai.Training.PdfReview;
using AuswertungPro.Next.Infrastructure.Ai.Training.Services;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;
using Xunit;

namespace AuswertungPro.Next.Infrastructure.Tests.Ai.Training.PdfReview;

public sealed class TrainingPdfReviewDocumentReaderFontEncodingTests : IDisposable
{
    private const int EncodingShift = 29;

    private readonly string _tempRoot =
        Path.Combine(Path.GetTempPath(), $"sewerstudio_pdf_font_{Guid.NewGuid():N}");

    [Fact]
    public void Read_SeitenshiftDekodiertAuchKurzenFotoKontextMitDemselbenShift()
    {
        Directory.CreateDirectory(_tempRoot);
        var pdfPath = Path.Combine(_tempRoot, "shifted-font.pdf");
        var encodedLocalContext =
            EncodeShifted("Foto") + Environment.NewLine
            + EncodeShifted("Zustand BCCAY");
        Assert.Equal(
            encodedLocalContext,
            PdfFontEncodingDecoder.TryDecodeShiftedText(encodedLocalContext));

        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText(
                EncodeShifted("Leitung Material Haltung"),
                11,
                new PdfPoint(40, 780),
                font);
            page.AddPng(TestPng, new PdfRectangle(40, 390, 340, 615));
            page.AddText(
                EncodeShifted("Foto"),
                11,
                new PdfPoint(365, 580),
                font);
            page.AddText(
                EncodeShifted("Zustand BCCAY"),
                11,
                new PdfPoint(365, 555),
                font);
            File.WriteAllBytes(pdfPath, builder.Build());
        }

        var result = new TrainingPdfReviewDocumentReader()
            .Read(pdfPath, CancellationToken.None);

        Assert.Contains(
            "Leitung Material Haltung",
            result.DocumentText,
            StringComparison.Ordinal);
        var photo = Assert.Single(result.Photos);
        Assert.Contains("Foto", photo.ContextText, StringComparison.Ordinal);
        Assert.Contains("Zustand BCCAY", photo.ContextText, StringComparison.Ordinal);
        Assert.DoesNotContain(
            EncodeShifted("Zustand"),
            photo.ContextText,
            StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }

    private static string EncodeShifted(string clearText)
    {
        var chars = clearText
            .Select(character =>
                char.IsWhiteSpace(character)
                    ? character
                    : (char)(character - EncodingShift))
            .ToArray();
        return new string(chars);
    }

    private static readonly byte[] TestPng = Convert.FromBase64String(
        "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=");
}
