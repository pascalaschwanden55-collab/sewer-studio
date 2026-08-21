using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests;

public sealed class HoldingFolderDistributorWritePathGuardTests
{
    [JunctionFact]
    public void Haltungsverteilung_VorhandenerHaltungsordnerIstVerknuepft_SchreibtUndVerschiebtNichtsNachAussen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var foreignFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Fremd")).FullName;
        var holdingLink = Path.Combine(destinationRoot, "1000-2000");
        var sourcePdf = Path.Combine(sourceFolder, "Haltungsprotokoll.pdf");
        WriteHoldingPdf(sourcePdf, "1000-2000", new DateTime(2026, 7, 12));
        var sourceBytes = File.ReadAllBytes(sourcePdf);
        JunctionTestSupport.CreateDirectoryLink(holdingLink, foreignFolder);

        try
        {
            var result = Assert.Single(HoldingFolderDistributor.DistributeFiles(
                pdfFiles: [sourcePdf],
                videoSourceFolder: sourceFolder,
                destGemeindeFolder: destinationRoot,
                moveInsteadOfCopy: true));

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePdf));
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
        }
        finally
        {
            DeleteDirectoryLink(holdingLink);
        }
    }

    [JunctionFact]
    public void Haltungsverteilung_VorhandeneZieldateiIstSymlink_UeberschreibtKeineFremdeDatei()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var holdingFolder = Directory.CreateDirectory(Path.Combine(destinationRoot, "1000-2000")).FullName;
        var sourcePdf = Path.Combine(sourceFolder, "Haltungsprotokoll.pdf");
        var foreignPdf = Path.Combine(temp.Path, "FremdesProtokoll.pdf");
        var targetLink = Path.Combine(holdingFolder, "20260712_1000-2000.pdf");
        WriteHoldingPdf(sourcePdf, "1000-2000", new DateTime(2026, 7, 12));
        File.WriteAllText(foreignPdf, "fremder-bestand");
        var sourceBytes = File.ReadAllBytes(sourcePdf);
        var foreignBytes = File.ReadAllBytes(foreignPdf);
        File.CreateSymbolicLink(targetLink, foreignPdf);

        try
        {
            var result = Assert.Single(HoldingFolderDistributor.DistributeFiles(
                pdfFiles: [sourcePdf],
                videoSourceFolder: sourceFolder,
                destGemeindeFolder: destinationRoot,
                moveInsteadOfCopy: true,
                overwrite: true));

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePdf));
            Assert.Equal(foreignBytes, File.ReadAllBytes(foreignPdf));
        }
        finally
        {
            try
            {
                if (File.Exists(targetLink))
                    File.Delete(targetLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }

    [JunctionFact]
    public void Haltungsverteilung_UnmatchedOrdnerIstVerknuepft_KopiertKeineVideokandidatenNachAussen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var videoFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Videos")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var foreignFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Fremd")).FullName;
        var unmatchedLink = Path.Combine(destinationRoot, "__UNMATCHED");
        var firstVideoFolder = Directory.CreateDirectory(Path.Combine(videoFolder, "A")).FullName;
        var secondVideoFolder = Directory.CreateDirectory(Path.Combine(videoFolder, "B")).FullName;
        var firstVideo = Path.Combine(firstVideoFolder, "ABC001.MPG");
        var secondVideo = Path.Combine(secondVideoFolder, "ABC001.MPG");
        var sourceTxt = Path.Combine(sourceFolder, "kiDVDaten.txt");
        File.WriteAllText(Path.Combine(sourceFolder, "kiDVinfo.txt"), "Aufnahmen: 12.07.2026");
        File.WriteAllText(firstVideo, "erstes-kundenvideo");
        File.WriteAllText(secondVideo, "zweites-kundenvideo");
        File.WriteAllText(
            sourceTxt,
            string.Join(Environment.NewLine,
            [
                "Schmutzwasser 100 -> 200 UV 300 @Datei=ABC001.MPG",
                "  0.0m Rohranfang  @Pos=0:00:00",
                "  1.0m Rohrende  @Pos=0:00:05"
            ]));
        JunctionTestSupport.CreateDirectoryLink(unmatchedLink, foreignFolder);

        try
        {
            var result = Assert.Single(HoldingFolderDistributor.DistributeTxtFiles(
                txtFiles: [sourceTxt],
                videoSourceFolder: videoFolder,
                destGemeindeFolder: destinationRoot));

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
            Assert.Equal("erstes-kundenvideo", File.ReadAllText(firstVideo));
            Assert.Equal("zweites-kundenvideo", File.ReadAllText(secondVideo));
        }
        finally
        {
            DeleteDirectoryLink(unmatchedLink);
        }
    }

    [JunctionFact]
    public void Dichtheitsverteilung_EinzelzielIstVerknuepft_SchreibtUndVerschiebtNichtsNachAussen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var foreignFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Fremd")).FullName;
        var holdingLink = Path.Combine(destinationRoot, "6927-6928");
        var sourcePdf = Path.Combine(sourceFolder, "Dichtheit.pdf");
        WriteDichtheitPdf(sourcePdf, "6928", "6927");
        var sourceBytes = File.ReadAllBytes(sourcePdf);
        JunctionTestSupport.CreateDirectoryLink(holdingLink, foreignFolder);

        try
        {
            var project = CreateProjectWithHoldings("6927-6928");
            var result = Assert.Single(HoldingFolderDistributor.DistributeDichtheitFiles(
                pdfFiles: [sourcePdf],
                destGemeindeFolder: destinationRoot,
                moveInsteadOfCopy: true,
                project: project));

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePdf));
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
        }
        finally
        {
            DeleteDirectoryLink(holdingLink);
        }
    }

    [JunctionFact]
    public void Dichtheitsverteilung_MehrseitenzielIstVerknuepft_SchreibtKeineTeilPdfNachAussen()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var foreignFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Fremd")).FullName;
        var holdingLink = Path.Combine(destinationRoot, "6927-6928");
        var sourcePdf = Path.Combine(sourceFolder, "Dichtheit_Mehrere.pdf");
        WriteMultiDichtheitPdf(
            sourcePdf,
            ("6928", "6927"),
            ("7002", "7001"));
        var sourceBytes = File.ReadAllBytes(sourcePdf);
        JunctionTestSupport.CreateDirectoryLink(holdingLink, foreignFolder);

        try
        {
            var project = CreateProjectWithHoldings("6927-6928", "7001-7002");
            var results = HoldingFolderDistributor.DistributeDichtheitFiles(
                pdfFiles: [sourcePdf],
                destGemeindeFolder: destinationRoot,
                project: project);

            Assert.Contains(results, result =>
                !result.Success
                && result.Message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePdf));
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
        }
        finally
        {
            DeleteDirectoryLink(holdingLink);
        }
    }

    [JunctionFact]
    public void Schachtverteilung_ZielWirdVorAppendZurVerknuepfung_SperrtZusammenfuehrung()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var destinationRoot = Directory.CreateDirectory(Path.Combine(temp.Path, "Ziel")).FullName;
        var foreignFolder = Path.Combine(temp.Path, "Fremd");
        var shaftFolder = Path.Combine(destinationRoot, "74467");
        var firstPdf = Path.Combine(sourceFolder, "A-Schachtprotokoll.pdf");
        var secondPdf = Path.Combine(sourceFolder, "B-Schachtprotokoll.pdf");
        WriteShaftPdf(firstPdf, "74467", new DateTime(2026, 7, 12), "Erste Seite");
        WriteShaftPdf(secondPdf, "74467", new DateTime(2026, 7, 12), "Zweite Seite");
        var firstSourceBytes = File.ReadAllBytes(firstPdf);
        var secondSourceBytes = File.ReadAllBytes(secondPdf);
        byte[]? publishedBytes = null;
        var linkCreated = false;
        var progress = new InlineProgress<HoldingFolderDistributor.DistributionProgress>(value =>
        {
            if (value.Processed != 1)
                return;

            var publishedPath = Path.Combine(shaftFolder, "20260712_74467.pdf");
            publishedBytes = File.ReadAllBytes(publishedPath);
            Directory.Move(shaftFolder, foreignFolder);
            JunctionTestSupport.CreateDirectoryLink(shaftFolder, foreignFolder);
            linkCreated = true;
        });

        try
        {
            var results = HoldingFolderDistributor.DistributeShaftFiles(
                pdfFiles: [firstPdf, secondPdf],
                destGemeindeFolder: destinationRoot,
                progress: progress);

            Assert.Equal(2, results.Count);
            Assert.True(results[0].Success, results[0].Message);
            Assert.False(results[1].Success);
            Assert.Contains("Verknuepfung", results[1].Message, StringComparison.OrdinalIgnoreCase);
            Assert.NotNull(publishedBytes);
            Assert.Equal(publishedBytes, File.ReadAllBytes(Path.Combine(foreignFolder, "20260712_74467.pdf")));
            Assert.Equal(firstSourceBytes, File.ReadAllBytes(firstPdf));
            Assert.Equal(secondSourceBytes, File.ReadAllBytes(secondPdf));
        }
        finally
        {
            if (linkCreated)
                DeleteDirectoryLink(shaftFolder);
        }
    }

    [JunctionFact]
    public void VerknuepfterZielrootWirdBeimSchreibenFailClosedGesperrt()
    {
        using var temp = new TempDirectory();
        var sourceFolder = Directory.CreateDirectory(Path.Combine(temp.Path, "Quelle")).FullName;
        var physicalDestination = Directory.CreateDirectory(Path.Combine(temp.Path, "PhysischesZiel")).FullName;
        var selectedRootLink = Path.Combine(temp.Path, "Gewaehlt");
        var sourcePdf = Path.Combine(sourceFolder, "Haltungsprotokoll.pdf");
        WriteHoldingPdf(sourcePdf, "1000-2000", new DateTime(2026, 7, 12));
        var sourceBytes = File.ReadAllBytes(sourcePdf);
        JunctionTestSupport.CreateDirectoryLink(selectedRootLink, physicalDestination);

        try
        {
            var result = Assert.Single(HoldingFolderDistributor.DistributeFiles(
                pdfFiles: [sourcePdf],
                videoSourceFolder: sourceFolder,
                destGemeindeFolder: selectedRootLink,
                moveInsteadOfCopy: true));

            Assert.False(result.Success);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(sourceBytes, File.ReadAllBytes(sourcePdf));
            Assert.Empty(Directory.EnumerateFileSystemEntries(physicalDestination));
        }
        finally
        {
            DeleteDirectoryLink(selectedRootLink);
        }
    }

    private static Project CreateProjectWithHoldings(params string[] holdingNames)
    {
        var project = new Project();
        foreach (var holdingName in holdingNames)
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", holdingName, FieldSource.Manual, userEdited: false);
            project.AddRecord(record);
        }

        return project;
    }

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

    private static void WriteShaftPdf(
        string path,
        string shaftNumber,
        DateTime date,
        string marker)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText($"Projekt: Test Datum: {date:dd.MM.yyyy}", 12, new PdfPoint(40, 780), font);
        page.AddText($"Schachtprotokoll Schacht Nr. {shaftNumber}", 18, new PdfPoint(40, 740), font);
        page.AddText("STAMMDATEN & SKIZZE", 12, new PdfPoint(40, 700), font);
        page.AddText(marker, 12, new PdfPoint(40, 660), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteDichtheitPdf(string path, string from, string to)
        => WriteMultiDichtheitPdf(path, (from, to));

    private static void WriteMultiDichtheitPdf(
        string path,
        params (string From, string To)[] holdings)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        foreach (var (from, to) in holdings)
        {
            var page = builder.AddPage(PageSize.A4);
            page.AddText(
                $"Prufgegenstand / Haltung {from} -> {to}",
                14,
                new PdfPoint(40, 780),
                font);
            page.AddText("Datum 2026/07/12", 12, new PdfPoint(40, 740), font);
        }

        File.WriteAllBytes(path, builder.Build());
    }

    private static void DeleteDirectoryLink(string link)
    {
        try
        {
            if (Directory.Exists(link))
                Directory.Delete(link);
        }
        catch
        {
            // Nur Test-Aufraeumen.
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    private sealed class TempDirectory : IDisposable
    {
        public TempDirectory()
        {
            Path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "sewerstudio-distribution-write-guard-" + Guid.NewGuid().ToString("N"));
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
