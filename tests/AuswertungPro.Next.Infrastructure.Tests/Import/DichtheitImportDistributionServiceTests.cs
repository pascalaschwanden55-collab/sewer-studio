using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Import;

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
