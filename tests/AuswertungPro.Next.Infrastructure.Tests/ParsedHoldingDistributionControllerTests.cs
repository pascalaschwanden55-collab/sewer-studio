using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.HoldingDistribution;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class ParsedHoldingDistributionControllerTests
{
    [Fact]
    public void Distribute_ohneVideo_kopiertPdf_und_schreibt_Fehlhinweis()
    {
        using var temp = new TempDirectory();
        var sourcePdf = Path.Combine(temp.Path, "quelle.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        WritePdf(sourcePdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var parsed = new HoldingFolderDistributor.ParsedPdf(
            true,
            null,
            new DateTime(2026, 7, 12),
            "1000-2000",
            null);

        var result = ParsedHoldingDistributionController.Distribute(
            parsed,
            sourcePdf,
            sourcePdf,
            videoFolder,
            destination,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            pageRange: null);

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.NotFound, result.VideoStatus);
        Assert.NotNull(result.DestPdfPath);
        Assert.True(File.Exists(result.DestPdfPath));
        Assert.NotNull(result.InfoPath);
        Assert.True(File.Exists(result.InfoPath));
        Assert.Contains("Video missing", result.Message);
    }

    [Fact]
    public void Distribute_mitVideo_setzt_portablen_RecordLink()
    {
        using var temp = new TempDirectory();
        var sourcePdf = Path.Combine(temp.Path, "quelle.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        var sourceVideo = Path.Combine(videoFolder, "aufnahme.mpg");
        File.WriteAllText(sourceVideo, "video");
        WritePdf(sourcePdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");
        var project = new Project();
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "1000-2000", FieldSource.Manual, userEdited: false);
        project.AddRecord(record);
        var parsed = new HoldingFolderDistributor.ParsedPdf(
            true,
            null,
            new DateTime(2026, 7, 12),
            "1000-2000",
            "aufnahme.mpg");

        var result = ParsedHoldingDistributionController.Distribute(
            parsed,
            sourcePdf,
            sourcePdf,
            videoFolder,
            destination,
            moveInsteadOfCopy: false,
            overwrite: false,
            recursiveVideoSearch: true,
            unmatchedFolderName: "__UNMATCHED",
            pageRange: null,
            project);

        Assert.True(result.Success, result.Message);
        Assert.Equal(HoldingFolderDistributor.VideoMatchStatus.Matched, result.VideoStatus);
        Assert.NotNull(result.DestVideoPath);
        Assert.True(File.Exists(result.DestVideoPath));
        Assert.EndsWith("20260712_1000-2000.mpg", record.GetFieldValue("Link"));
        Assert.True(project.Dirty);
    }

    [Fact]
    public void DistributeFiles_faengt_defektePdf_ab_und_verarbeitet_naechste_Datei()
    {
        using var temp = new TempDirectory();
        var invalidPdf = Path.Combine(temp.Path, "01_defekt.pdf");
        var validPdf = Path.Combine(temp.Path, "02_gueltig.pdf");
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "videos")).FullName;
        var destination = Directory.CreateDirectory(Path.Combine(temp.Path, "ziel")).FullName;
        File.WriteAllText(invalidPdf, "kein PDF");
        WritePdf(validPdf, "Haltungsinspektion - 12.07.2026 - 1000-2000");

        var results = HoldingFolderDistributor.DistributeFiles(
            [invalidPdf, validPdf],
            videoFolder,
            destination);

        Assert.Contains(results, result => !result.Success && result.SourcePdfPath == invalidPdf);
        Assert.Contains(results, result => result.Success && result.SourcePdfPath == validPdf);
        Assert.True(File.Exists(Path.Combine(destination, "1000-2000", "20260712_1000-2000.pdf")));
    }

    private static void WritePdf(string path, params string[] lines)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        var y = 780m;
        foreach (var line in lines)
        {
            page.AddText(line, 12, new PdfPoint(40, y), font);
            y -= 18;
        }
        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-parsed-distribution-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path))
                Directory.Delete(Path, recursive: true);
        }
    }
}
