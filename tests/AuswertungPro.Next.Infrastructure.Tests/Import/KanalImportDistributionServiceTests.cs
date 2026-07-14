using AuswertungPro.Next.Domain.Models;
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
