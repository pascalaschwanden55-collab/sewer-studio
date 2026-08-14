using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Import;
using AuswertungPro.Next.Infrastructure.Import;

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
