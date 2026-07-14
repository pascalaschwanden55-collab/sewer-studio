using AuswertungPro.Next.Application.Export;
using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingFolderDistributorDirectoryTreeTests
{
    [Fact]
    public void DistributeTxtFiles_OhneVerzeichnisKonfiguration_BehaeltRootUndHaltungsordner()
    {
        using var temp = new TempDirectory();
        var (txtPath, videoFolder, destinationRoot) = CreateTxtFixture(temp.Path);
        var project = CreateProject("Altdorf");

        var result = Assert.Single(HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: [txtPath],
            videoSourceFolder: videoFolder,
            destGemeindeFolder: destinationRoot,
            project: project));

        var expectedFolder = Path.Combine(destinationRoot, "100-200");
        var expectedTxt = Path.Combine(expectedFolder, "20260712_100-200.txt");

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedFolder, result.HoldingFolder);
        Assert.Equal(expectedTxt, result.DestPdfPath);
        Assert.True(File.Exists(expectedTxt));
    }

    [Fact]
    public void DistributeTxtFiles_MitGemeindeUndJahr_VerwendetBaumUndBehaeltSicherenDateinamen()
    {
        using var temp = new TempDirectory();
        var (txtPath, videoFolder, destinationRoot) = CreateTxtFixture(temp.Path);
        var project = CreateProject("Altdorf");
        var directoryConfig = CreateDirectoryConfig(destinationRoot);

        var result = Assert.Single(HoldingFolderDistributor.DistributeTxtFiles(
            txtFiles: [txtPath],
            videoSourceFolder: videoFolder,
            destGemeindeFolder: destinationRoot,
            project: project,
            directoryConfig: directoryConfig));

        var expectedFolder = Path.Combine(destinationRoot, "Altdorf", "2026", "100-200");
        var expectedTxt = Path.Combine(expectedFolder, "20260712_100-200.txt");

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedFolder, result.HoldingFolder);
        Assert.Equal(expectedTxt, result.DestPdfPath);
        Assert.True(File.Exists(expectedTxt));
        Assert.False(File.Exists(Path.Combine(expectedFolder, "NICHT_VERWENDEN.txt")));
    }

    [Fact]
    public void DistributeFiles_MitGemeindeUndJahr_VerwendetBaumUndBehaeltSicherenDateinamen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Videos")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Haltungsprotokoll.pdf");
        WriteHoldingPdf(sourcePdf, "1000-2000", new DateTime(2026, 7, 12));
        var project = CreateProject("Altdorf");

        var result = Assert.Single(HoldingFolderDistributor.DistributeFiles(
            pdfFiles: [sourcePdf],
            videoSourceFolder: videoFolder,
            destGemeindeFolder: destinationRoot,
            project: project,
            directoryConfig: CreateDirectoryConfig(destinationRoot)));

        var expectedFolder = Path.Combine(destinationRoot, "Altdorf", "2026", "1000-2000");
        var expectedPdf = Path.Combine(expectedFolder, "20260712_1000-2000.pdf");

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedFolder, result.HoldingFolder);
        Assert.Equal(expectedPdf, result.DestPdfPath);
        Assert.True(File.Exists(expectedPdf));
        Assert.False(File.Exists(Path.Combine(expectedFolder, "NICHT_VERWENDEN.pdf")));
    }

    [Fact]
    public void DistributeShaftFiles_MitGemeindeUndJahr_VerwendetBaumUndBehaeltSicherenDateinamen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Schachtprotokoll.pdf");
        WriteShaftPdf(sourcePdf, "74467", new DateTime(2026, 7, 12));
        var project = CreateProject("Altdorf");
        var directoryConfig = CreateDirectoryConfig(destinationRoot);

        var result = Assert.Single(HoldingFolderDistributor.DistributeShaftFiles(
            pdfFiles: [sourcePdf],
            destGemeindeFolder: destinationRoot,
            project: project,
            directoryConfig: directoryConfig));

        var expectedFolder = Path.Combine(destinationRoot, "Altdorf", "2026", "74467");
        var expectedPdf = Path.Combine(expectedFolder, "20260712_74467.pdf");

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedFolder, result.HoldingFolder);
        Assert.Equal(expectedPdf, result.DestPdfPath);
        Assert.True(File.Exists(expectedPdf));
        Assert.False(File.Exists(Path.Combine(expectedFolder, "NICHT_VERWENDEN.pdf")));
    }

    [Fact]
    public void DistributeDichtheitFiles_MitGemeindeUndJahr_VerwendetHaltungsbaumUndDpDateiname()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Dichtheit.pdf");
        WriteDichtheitPdf(sourcePdf);
        var project = CreateProject("Altdorf");
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "6927-6928", FieldSource.Manual, userEdited: false);
        project.AddRecord(record);

        var result = Assert.Single(HoldingFolderDistributor.DistributeDichtheitFiles(
            pdfFiles: [sourcePdf],
            destGemeindeFolder: destinationRoot,
            project: project,
            directoryConfig: CreateDirectoryConfig(destinationRoot)));

        var expectedFolder = Path.Combine(destinationRoot, "Altdorf", "2026", "6927-6928");
        var expectedPdf = Path.Combine(expectedFolder, "20260712_6927-6928_DP.pdf");

        Assert.True(result.Success, result.Message);
        Assert.Equal(expectedFolder, result.HoldingFolder);
        Assert.Equal(expectedPdf, result.DestPdfPath);
        Assert.True(File.Exists(expectedPdf));
    }

    [Fact]
    public void DistributeDichtheitFiles_OhneBaum_BehaeltBisherigenDatumsFallback()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Dichtheit.pdf");
        WriteDichtheitPdf(sourcePdf);
        var project = CreateProject("Altdorf");
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "6927-6928", FieldSource.Manual, userEdited: false);
        project.AddRecord(record);

        var result = Assert.Single(HoldingFolderDistributor.DistributeDichtheitFiles(
            pdfFiles: [sourcePdf],
            destGemeindeFolder: destinationRoot,
            project: project));

        var expected = Path.Combine(destinationRoot, "6927-6928", "00000000_6927-6928_DP.pdf");
        Assert.True(result.Success, result.Message);
        Assert.Equal(expected, result.DestPdfPath);
        Assert.True(File.Exists(expected));
    }

    [Fact]
    public void DistributeDichtheitFiles_MehrereHaltungen_VerwendetBaumFuerJedeHaltung()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Dichtheit_Mehrere.pdf");
        WriteMultiDichtheitPdf(
            sourcePdf,
            ("6928", "6927"),
            ("7002", "7001"));
        var project = CreateProject("Altdorf");
        AddHolding(project, "6927-6928");
        AddHolding(project, "7001-7002");

        var results = HoldingFolderDistributor.DistributeDichtheitFiles(
            pdfFiles: [sourcePdf],
            destGemeindeFolder: destinationRoot,
            project: project,
            directoryConfig: CreateDirectoryConfig(destinationRoot));

        Assert.Equal(2, results.Count);
        Assert.All(results, result => Assert.True(result.Success, result.Message));
        Assert.True(File.Exists(Path.Combine(
            destinationRoot, "Altdorf", "2026", "6927-6928", "20260712_6927-6928_DP.pdf")));
        Assert.True(File.Exists(Path.Combine(
            destinationRoot, "Altdorf", "2026", "7001-7002", "20260712_7001-7002_DP.pdf")));
    }

    private static (string TxtPath, string VideoFolder, string DestinationRoot) CreateTxtFixture(
        string tempRoot)
    {
        var sourceFolder = Directory.CreateDirectory(Path.Combine(tempRoot, "Quelle")).FullName;
        var videoFolder = Directory.CreateDirectory(Path.Combine(tempRoot, "Videos")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(tempRoot, "Ziel")).FullName;
        var txtPath = Path.Combine(sourceFolder, "kiDVDaten.txt");

        File.WriteAllText(
            Path.Combine(sourceFolder, "kiDVinfo.txt"),
            "Aufnahmen: 12.07.2026");
        File.WriteAllText(
            txtPath,
            string.Join(Environment.NewLine,
            [
                "Schmutzwasser 100 -> 200 UV 300 @Datei=FEHLT.MPG",
                "  0.0m Rohranfang  @Pos=0:00:00",
                "  1.0m Rohrende  @Pos=0:00:05"
            ]));

        return (txtPath, videoFolder, destinationRoot);
    }

    private static Project CreateProject(string municipality)
    {
        var project = new Project();
        project.Metadata["Gemeinde"] = municipality;
        return project;
    }

    private static void AddHolding(Project project, string holdingName)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", holdingName, FieldSource.Manual, userEdited: false);
        project.AddRecord(record);
    }

    private static DistributionTargetConfig CreateDirectoryConfig(string root)
        => new()
        {
            Root = root,
            OrdnerPattern = "{Gemeinde}",
            UnterordnerPattern = "{Jahr}",
            // Der Verteiler muss seinen bewaehrten Dateinamen unabhaengig davon beibehalten.
            DateiPattern = "NICHT_VERWENDEN"
        };

    private static void WriteHoldingPdf(string path, string holding, DateTime date)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(
            $"Haltungsinspektion - {date:dd.MM.yyyy} - {holding}",
            14,
            new PdfPoint(40, 780),
            font);
        page.AddText("Leitungsbericht", 12, new PdfPoint(40, 740), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteShaftPdf(string path, string shaftNumber, DateTime date)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(
            $"Projekt: Test Datum: {date:dd.MM.yyyy}",
            12,
            new PdfPoint(40, 780),
            font);
        page.AddText(
            $"Schachtprotokoll Schacht Nr. {shaftNumber}",
            18,
            new PdfPoint(40, 740),
            font);
        page.AddText("STAMMDATEN & SKIZZE", 12, new PdfPoint(40, 700), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteDichtheitPdf(string path)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(
            "Prufgegenstand / Haltung 6928 -> 6927",
            14,
            new PdfPoint(40, 780),
            font);
        page.AddText("Datum 2026/07/12", 12, new PdfPoint(40, 740), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteMultiDichtheitPdf(
        string path,
        params (string Von, string Nach)[] haltungen)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (von, nach) in haltungen)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText(
                $"Prufgegenstand / Haltung {von} -> {nach}",
                14,
                new PdfPoint(40, 780),
                font);
            page.AddText("Datum 2026/07/12", 12, new PdfPoint(40, 740), font);
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-distribution-tree-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(Path))
                    Directory.Delete(Path, recursive: true);
            }
            catch
            {
                // Aufraeumfehler duerfen das Testergebnis nicht verdecken.
            }
        }
    }
}
