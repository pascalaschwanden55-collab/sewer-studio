using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

namespace AuswertungPro.Next.Infrastructure.Tests.Import;

public sealed class KanalImportDistributionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"kanal-distribution-instance-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_EmptyProjectReturnsEmptyResultThroughContract()
    {
        var projectFolder = Path.Combine(_root, "Projekt");
        var pdfFolder = Path.Combine(_root, "PDF");
        var videoFolder = Path.Combine(_root, "Video");
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(pdfFolder);
        Directory.CreateDirectory(videoFolder);
        IKanalImportDistributor service = new KanalImportDistributionService();

        var result = service.Distribute(
            new Project(),
            projectFolder,
            pdfFolder,
            videoFolder);

        Assert.Equal(0, result.VideosDistributed);
        Assert.Equal(0, result.OriginalProtocolsDistributed);
        Assert.Equal(0, result.Errors);
        Assert.Empty(result.Messages);
    }

    [Fact]
    public void InstanceService_bereitet_FallbackVideo_mit_Staging_nur_vor()
    {
        var projectFolder = Path.Combine(_root, "Projekt");
        var projectPath = Path.Combine(projectFolder, "Projektdateien", "projekt.json");
        var pdfFolder = Path.Combine(projectFolder, "Importdateien", "PDF");
        var videoFolder = Path.Combine(_root, "Video");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(videoFolder);
        File.WriteAllText(projectPath, "{}");
        var video = Path.Combine(videoFolder, "film.mp4");
        File.WriteAllText(video, "video");
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "1-2", FieldSource.Manual, false);
        record.SetFieldValue("Datum_Jahr", "14.08.2026", FieldSource.Manual, false);
        record.SetFieldValue("Link", video, FieldSource.Manual, false);
        var project = new Project();
        project.Data.Add(record);
        using var staging = new ImportFileStagingService().Begin(projectPath)!;
        IKanalImportDistributor service = new KanalImportDistributionService();

        var result = service.Distribute(
            project,
            projectFolder,
            pdfFolder,
            videoFolder,
            splitPdf: false,
            primaryProtocolPdf: null,
            fileStaging: staging);

        Assert.Equal(1, result.VideosDistributed);
        var relative = record.GetFieldValue("Link");
        Assert.False(string.IsNullOrWhiteSpace(relative));
        var target = Path.Combine(projectFolder, relative!);
        Assert.False(File.Exists(target));
        Assert.Equal("video", File.ReadAllText(staging.ResolveReadPath(target)));
    }

    [Fact]
    public void FallbackVideo_ProjektnameAlsPraefixEinesNachbarordners_WirdInsProjektKopiert()
    {
        var projectFolder = Path.Combine(_root, "Projekt");
        var siblingFolder = Path.Combine(_root, "Projekt-Quelle");
        var pdfFolder = Path.Combine(projectFolder, "Importdateien", "PDF");
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(siblingFolder);
        var source = Path.Combine(siblingFolder, "film.mp4");
        File.WriteAllText(source, "video");
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, false);
        record.SetFieldValue("Datum_Jahr", "14.08.2026", FieldSource.Manual, false);
        record.SetFieldValue("Link", source, FieldSource.Manual, false);
        var project = new Project();
        project.Data.Add(record);

        var result = new KanalImportDistributionService().Distribute(
            project,
            projectFolder,
            pdfFolder,
            siblingFolder,
            splitPdf: false);

        Assert.Equal(1, result.VideosDistributed);
        Assert.Equal("video", File.ReadAllText(source));
        var relative = record.GetFieldValue("Link") ?? "";
        Assert.False(Path.IsPathRooted(relative));
        Assert.DoesNotContain("..", relative, StringComparison.Ordinal);
        Assert.Equal("video", File.ReadAllText(Path.Combine(projectFolder, relative)));
    }

    [JunctionFact]
    public void FallbackVideo_ZielwurzelIstVerknuepft_SchreibtNichtInFremdenOrdner()
    {
        var projectFolder = Path.Combine(_root, "Projekt");
        var sourceFolder = Path.Combine(_root, "Quelle");
        var foreignFolder = Path.Combine(_root, "Fremd");
        var targetLink = Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt);
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(sourceFolder);
        Directory.CreateDirectory(foreignFolder);
        JunctionTestSupport.CreateDirectoryLink(targetLink, foreignFolder);
        var source = Path.Combine(sourceFolder, "film.mp4");
        File.WriteAllText(source, "kundenvideo");
        try
        {
            var record = new HaltungRecord();
            record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, false);
            record.SetFieldValue("Datum_Jahr", "14.08.2026", FieldSource.Manual, false);
            record.SetFieldValue("Link", source, FieldSource.Manual, false);
            var project = new Project();
            project.Data.Add(record);

            var result = new KanalImportDistributionService().Distribute(
                project,
                projectFolder,
                Path.Combine(projectFolder, "Importdateien", "PDF"),
                sourceFolder,
                splitPdf: false);

            Assert.Equal(0, result.VideosDistributed);
            Assert.Equal(1, result.Errors);
            Assert.Contains(result.Messages, message =>
                message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase)
                || message.Contains("Junction", StringComparison.OrdinalIgnoreCase));
            Assert.Equal(source, record.GetFieldValue("Link"));
            Assert.Equal("kundenvideo", File.ReadAllText(source));
            Assert.Empty(Directory.EnumerateFileSystemEntries(foreignFolder));
        }
        finally
        {
            try
            {
                if (Directory.Exists(targetLink))
                    Directory.Delete(targetLink);
            }
            catch
            {
                // Nur Test-Aufraeumen.
            }
        }
    }

    [JunctionFact]
    public void FallbackVideo_DateisymlinkMitVideoendung_WirdVorDirektkopieAbgewiesen()
    {
        var projectFolder = Path.Combine(_root, "Projekt-Direkt");
        var sourceFolder = Path.Combine(_root, "Quelle-Direkt");
        Directory.CreateDirectory(projectFolder);
        Directory.CreateDirectory(sourceFolder);
        var protectedSource = Path.Combine(sourceFolder, "kundendaten.xlsx");
        var disguisedLink = Path.Combine(sourceFolder, "film.mp4");
        File.WriteAllText(protectedSource, "kundendaten");
        File.CreateSymbolicLink(disguisedLink, protectedSource);
        var record = CreateVideoRecord(disguisedLink);
        var project = new Project();
        project.Data.Add(record);

        var result = new KanalImportDistributionService().Distribute(
            project,
            projectFolder,
            Path.Combine(projectFolder, "Importdateien", "PDF"),
            sourceFolder,
            splitPdf: false);

        Assert.Equal(0, result.VideosDistributed);
        Assert.Equal(disguisedLink, record.GetFieldValue("Link"));
        Assert.Contains(result.Messages, message =>
            message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("kundendaten", File.ReadAllText(protectedSource));
        Assert.False(Directory.Exists(Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt)));
    }

    [JunctionFact]
    public void FallbackVideo_DateisymlinkMitVideoendung_WirdVorStageCopyAbgewiesen()
    {
        var projectFolder = Path.Combine(_root, "Projekt-Staging");
        var projectPath = Path.Combine(projectFolder, "Projektdateien", "projekt.json");
        var sourceFolder = Path.Combine(_root, "Quelle-Staging");
        Directory.CreateDirectory(Path.GetDirectoryName(projectPath)!);
        Directory.CreateDirectory(sourceFolder);
        File.WriteAllText(projectPath, "{}");
        var protectedSource = Path.Combine(sourceFolder, "kundendaten.xlsx");
        var disguisedLink = Path.Combine(sourceFolder, "film.mp4");
        File.WriteAllText(protectedSource, "kundendaten");
        File.CreateSymbolicLink(disguisedLink, protectedSource);
        var record = CreateVideoRecord(disguisedLink);
        var project = new Project();
        project.Data.Add(record);
        using var staging = new ImportFileStagingService().Begin(projectPath)!;

        var result = new KanalImportDistributionService().Distribute(
            project,
            projectFolder,
            Path.Combine(projectFolder, "Importdateien", "PDF"),
            sourceFolder,
            splitPdf: false,
            primaryProtocolPdf: null,
            fileStaging: staging);

        Assert.Equal(0, result.VideosDistributed);
        Assert.Equal(disguisedLink, record.GetFieldValue("Link"));
        Assert.Contains(result.Messages, message =>
            message.Contains("Verknuepfung", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("kundendaten", File.ReadAllText(protectedSource));
        Assert.False(Directory.Exists(Path.Combine(projectFolder, ProjectStructure.HaltungenVerteilt)));
    }

    [JunctionFact]
    public void SelectPrimaryProtocolPdf_DateisymlinkMitPdfendung_WirdAbgewiesen()
    {
        var archiveFolder = Path.Combine(_root, "PDF-Dateilink");
        Directory.CreateDirectory(archiveFolder);
        var protectedSource = Path.Combine(archiveFolder, "kundendaten.xlsx");
        var disguisedLink = Path.Combine(archiveFolder, "protokoll.pdf");
        File.WriteAllText(protectedSource, "kundendaten");
        File.CreateSymbolicLink(disguisedLink, protectedSource);

        var selected = new KanalImportDistributionService()
            .SelectPrimaryProtocolPdf(archiveFolder);

        Assert.Null(selected);
        Assert.Equal("kundendaten", File.ReadAllText(protectedSource));
    }

    [JunctionFact]
    public void SelectPrimaryProtocolPdf_VerknuepfterArchivordner_WirdAbgewiesen()
    {
        var foreignFolder = Path.Combine(_root, "PDF-Fremd");
        var archiveLink = Path.Combine(_root, "PDF-Link");
        Directory.CreateDirectory(foreignFolder);
        var protectedSource = Path.Combine(foreignFolder, "protokoll.pdf");
        File.WriteAllText(protectedSource, "kundendaten");
        JunctionTestSupport.CreateDirectoryLink(archiveLink, foreignFolder);

        var selected = new KanalImportDistributionService()
            .SelectPrimaryProtocolPdf(archiveLink);

        Assert.Null(selected);
        Assert.Equal("kundendaten", File.ReadAllText(protectedSource));
    }

    private static HaltungRecord CreateVideoRecord(string source)
    {
        var record = new HaltungRecord();
        record.SetFieldValue("Haltungsname", "100-200", FieldSource.Manual, false);
        record.SetFieldValue("Datum_Jahr", "14.08.2026", FieldSource.Manual, false);
        record.SetFieldValue("Link", source, FieldSource.Manual, false);
        return record;
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
