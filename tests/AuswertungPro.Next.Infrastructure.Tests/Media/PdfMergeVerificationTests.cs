using AuswertungPro.Next.Application.Reports;
using AuswertungPro.Next.Infrastructure.Media;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Media;

public sealed class PdfMergeVerificationTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(
        Path.GetTempPath(),
        "SewerStudio_PdfMergeVerificationTests_" + Guid.NewGuid().ToString("N"));

    public PdfMergeVerificationTests()
        => Directory.CreateDirectory(_tempRoot);

    [Fact]
    public void Fehlende_Pflichtbeilage_stoppt_das_Dossier_vor_dem_Merge()
    {
        var merge = new RecordingMergeService(CreatePdf(1));

        var error = Assert.Throws<InvalidOperationException>(() =>
            PdfMergeVerification.MergeWithRequiredOriginals(
                merge,
                CreatePdf(1),
                [Path.Combine(_tempRoot, "fehlt.pdf")]));

        Assert.Contains("fehlt", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0, merge.Calls);
    }

    [Fact]
    public void Still_ausgelassene_Beilagenseite_wird_erkannt()
    {
        var attachment = Path.Combine(_tempRoot, "beilage.pdf");
        File.WriteAllBytes(attachment, CreatePdf(2));
        var merge = new RecordingMergeService(CreatePdf(1));

        var error = Assert.Throws<InvalidOperationException>(() =>
            PdfMergeVerification.MergeWithRequiredOriginals(
                merge,
                CreatePdf(1),
                [attachment]));

        Assert.Contains("unvollstaendig", error.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("3", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Vollstaendige_Ausgabe_wird_zugelassen()
    {
        var attachment = Path.Combine(_tempRoot, "beilage.pdf");
        File.WriteAllBytes(attachment, CreatePdf(2));
        var merge = new RecordingMergeService(CreatePdf(3));

        var result = PdfMergeVerification.MergeWithRequiredOriginals(
            merge,
            CreatePdf(1),
            [attachment]);

        Assert.Same(merge.Result, result);
    }

    public void Dispose()
    {
        try { Directory.Delete(_tempRoot, recursive: true); } catch { }
    }

    private static byte[] CreatePdf(int pageCount)
    {
        using var builder = new PdfDocumentBuilder();
        for (var i = 0; i < pageCount; i++)
            builder.AddPage(PageSize.A4);
        return builder.Build();
    }

    private sealed class RecordingMergeService(byte[] result) : IPdfMergeService
    {
        public byte[] Result { get; } = result;
        public int Calls { get; private set; }

        public byte[] MergeWithOriginals(byte[] generatedPdf, IReadOnlyList<string> originalPdfPaths)
        {
            Calls++;
            return Result;
        }

        public byte[] MergeOriginals(IReadOnlyList<string> originalPdfPaths)
        {
            Calls++;
            return Result;
        }
    }
}
