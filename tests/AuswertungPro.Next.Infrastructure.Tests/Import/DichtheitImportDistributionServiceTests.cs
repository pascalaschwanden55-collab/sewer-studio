using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Content;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class DichtheitImportDistributionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"dichtheit-distribution-instance-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_EmptySourceReturnsEmptyResultThroughContract()
    {
        var sourceFolder = Path.Combine(_root, "Quelle");
        var projectFolder = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(projectFolder);
        IDichtheitImportDistributor service = new DichtheitImportDistributionService();

        var result = service.Distribute(new Project(), projectFolder, sourceFolder);

        Assert.Equal(0, result.Verteilt);
        Assert.Equal(0, result.NichtZugeordnet);
        Assert.Equal(0, result.Uebersprungen);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void InstanceService_bereitet_DichtheitsPdf_mit_Staging_nur_vor()
    {
        var sourceFolder = Path.Combine(_root, "Quelle", "DP");
        var projectFolder = Path.Combine(_root, "Projekt");
        var projectPath = Path.Combine(projectFolder, "Projektdateien", "projekt.json");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        File.WriteAllText(projectPath, "{}");
        var sourcePdf = Path.Combine(sourceFolder, "pruefung.pdf");
        using (var builder = new PdfDocumentBuilder())
        {
            var font = builder.AddStandard14Font(Standard14Font.Helvetica);
            var page = builder.AddPage(PageSize.A4);
            page.AddText("Dichtheitspruefung nach SIA 190", 12, new PdfPoint(40, 780), font);
            page.AddText("oberer Schacht: 100", 12, new PdfPoint(40, 750), font);
            page.AddText("unterer Schacht: 200", 12, new PdfPoint(40, 720), font);
            page.AddText("14.08.2026", 12, new PdfPoint(40, 690), font);
            File.WriteAllBytes(sourcePdf, builder.Build());
        }
        using var staging = new ImportFileStagingService().Begin(projectPath)!;
        IDichtheitImportDistributor service = new DichtheitImportDistributionService();

        var result = service.Distribute(
            new Project(),
            projectFolder,
            Path.GetDirectoryName(sourceFolder)!,
            ki: null,
            fileStaging: staging);

        Assert.Equal(1, result.Verteilt);
        var prepared = Assert.Single(staging.PreparedFiles);
        var target = Path.Combine(projectFolder, prepared.RelativePath);
        Assert.False(File.Exists(target));
        Assert.True(File.Exists(staging.ResolveReadPath(target)));
    }

    [Fact]
    public void InstanceService_FachlichAnderePdfMitGleicherGroesseWirdNichtUebersprungen()
    {
        var sourceFolder = Path.Combine(_root, "Quelle", "DP");
        var projectFolder = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(sourceFolder);
        var sourcePdf = Path.Combine(sourceFolder, "pruefung.pdf");
        WriteDichtheitsPdf(sourcePdf, "100", "200");

        var existingFolder = Path.Combine(projectFolder, "Haltungen_Verteilt", "300-400");
        Directory.CreateDirectory(existingFolder);
        var existingPath = Path.Combine(existingFolder, "20260814_300-400_DP.pdf");
        WriteDichtheitsPdf(existingPath, "300", "400");
        var commonLength = Math.Max(new FileInfo(sourcePdf).Length, new FileInfo(existingPath).Length);
        PadPdfWithSpaces(sourcePdf, commonLength);
        PadPdfWithSpaces(existingPath, commonLength);
        Assert.Equal(new FileInfo(sourcePdf).Length, new FileInfo(existingPath).Length);
        Assert.False(File.ReadAllBytes(sourcePdf).SequenceEqual(File.ReadAllBytes(existingPath)));

        IDichtheitImportDistributor service = new DichtheitImportDistributionService();
        var result = service.Distribute(
            new Project(),
            projectFolder,
            Path.GetDirectoryName(sourceFolder)!);

        Assert.Equal(1, result.Verteilt);
        Assert.Equal(0, result.Uebersprungen);
        Assert.Single(Directory.EnumerateFiles(existingFolder, "*_DP*.pdf"));
        Assert.Single(Directory.EnumerateFiles(
            Path.Combine(projectFolder, "Haltungen_Verteilt", "100-200"),
            "*_DP*.pdf"));
    }

    [Fact]
    public void InstanceService_KiZuordnungMitNamenskonfliktUndGleicherGroesseErzeugtNeueDatei()
    {
        var sourceFolder = Path.Combine(_root, "Quelle", "DP");
        var projectFolder = Path.Combine(_root, "Projekt");
        Directory.CreateDirectory(sourceFolder);
        var sourcePdf = Path.Combine(sourceFolder, "unklar.pdf");
        WriteTextPdf(sourcePdf, "Unbekanntes Dokument");

        var targetFolder = Path.Combine(projectFolder, "Haltungen_Verteilt", "100-200");
        Directory.CreateDirectory(targetFolder);
        var existingPath = Path.Combine(targetFolder, "20260814_100-200_DP.pdf");
        WriteTextPdf(existingPath, "Anderer PDF-Inhalt");
        var commonLength = Math.Max(new FileInfo(sourcePdf).Length, new FileInfo(existingPath).Length);
        PadPdfWithSpaces(sourcePdf, commonLength);
        PadPdfWithSpaces(existingPath, commonLength);
        var existingBytes = File.ReadAllBytes(existingPath);

        var ki = new PdfKiSchiedsrichter((_, _) => Task.FromResult(
            """{ "typ": "Dichtheitspruefung", "schacht_von": "100", "schacht_bis": "200", "datum": "14.08.2026" }"""));
        IDichtheitImportDistributor service = new DichtheitImportDistributionService();

        var result = service.Distribute(
            new Project(),
            projectFolder,
            Path.GetDirectoryName(sourceFolder)!,
            ki);

        Assert.Equal(1, result.Verteilt);
        Assert.Equal(existingBytes, File.ReadAllBytes(existingPath));
        var conflictCopy = Path.Combine(targetFolder, "20260814_100-200_DP_1.pdf");
        Assert.True(File.Exists(conflictCopy));
        Assert.Equal(File.ReadAllBytes(sourcePdf), File.ReadAllBytes(conflictCopy));
    }

    [JunctionFact]
    public void InstanceService_KiZuordnungMitVerknuepftemHaltungsordner_SchreibtKeineDateiNachAussen()
    {
        var sourceFolder = Path.Combine(_root, "Quelle", "DP");
        var projectFolder = Path.Combine(_root, "Projekt");
        var targetRoot = Path.Combine(projectFolder, "Haltungen_Verteilt");
        var externalFolder = Path.Combine(_root, "Fremd");
        var holdingLink = Path.Combine(targetRoot, "100-200");
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(targetRoot);
        Directory.CreateDirectory(externalFolder);
        var sourcePdf = Path.Combine(sourceFolder, "unklar.pdf");
        WriteTextPdf(sourcePdf, "Unbekanntes Dokument");
        JunctionTestSupport.CreateDirectoryLink(holdingLink, externalFolder);

        try
        {
            var ki = new PdfKiSchiedsrichter((_, _) => Task.FromResult(
                """{ "typ": "Dichtheitspruefung", "schacht_von": "100", "schacht_bis": "200", "datum": "14.08.2026" }"""));
            IDichtheitImportDistributor service = new DichtheitImportDistributionService();

            var result = service.Distribute(
                new Project(),
                projectFolder,
                Path.GetDirectoryName(sourceFolder)!,
                ki);

            Assert.Equal(0, result.Verteilt);
            Assert.Equal(1, result.NichtZugeordnet);
            Assert.Contains(result.Messages, message =>
                message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
            Assert.Empty(Directory.EnumerateFileSystemEntries(externalFolder));
        }
        finally
        {
            try
            {
                if (Directory.Exists(holdingLink))
                    Directory.Delete(holdingLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }

    private static void WriteDichtheitsPdf(string path, string obererSchacht, string untererSchacht)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText("Dichtheitspruefung nach SIA 190", 12, new PdfPoint(40, 780), font);
        page.AddText($"oberer Schacht: {obererSchacht}", 12, new PdfPoint(40, 750), font);
        page.AddText($"unterer Schacht: {untererSchacht}", 12, new PdfPoint(40, 720), font);
        page.AddText("14.08.2026", 12, new PdfPoint(40, 690), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void WriteTextPdf(string path, string text)
    {
        using var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(PageSize.A4);
        page.AddText(text, 12, new PdfPoint(40, 780), font);
        File.WriteAllBytes(path, builder.Build());
    }

    private static void PadPdfWithSpaces(string path, long length)
    {
        var bytes = File.ReadAllBytes(path);
        var originalLength = bytes.Length;
        Array.Resize(ref bytes, checked((int)length));
        bytes.AsSpan(originalLength).Fill((byte)' ');
        File.WriteAllBytes(path, bytes);
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Temp-Aufraeumen darf das Testergebnis nicht verdecken.
        }
    }
}
