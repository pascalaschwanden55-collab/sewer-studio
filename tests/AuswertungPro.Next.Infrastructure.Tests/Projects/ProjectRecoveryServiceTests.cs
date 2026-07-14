using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectRecoveryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"project-recovery-instance-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_RecoversBackupAndQuarantinesCorruptProject()
    {
        Directory.CreateDirectory(_root);
        var projectFile = Path.Combine(_root, "projekt.json");
        var repository = new JsonProjectRepository();
        var backupResult = repository.Save(
            new Project { Name = "Gerettetes Projekt" },
            projectFile + ".bak");
        Assert.True(backupResult.Ok, backupResult.ErrorMessage);
        File.WriteAllText(projectFile, "{ keine gueltige Projektdatei");

        IProjectRecoveryService service = new ProjectRecoveryService();

        var result = service.TryRecover(projectFile, repository);

        Assert.True(result.Recovered);
        Assert.Equal("Gerettetes Projekt", result.Project?.Name);
        Assert.Equal(projectFile + ".bak", result.RecoveredFromPath);
        Assert.NotNull(result.QuarantinedPath);
        Assert.True(File.Exists(result.QuarantinedPath));
        Assert.False(File.Exists(projectFile));
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
            // Temp-Aufraeumen darf den Testlauf nicht verdecken.
        }
    }
}
