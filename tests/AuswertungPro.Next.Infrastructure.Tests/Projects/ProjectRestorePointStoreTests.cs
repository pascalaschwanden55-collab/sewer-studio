using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectRestorePointStoreTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"project-restore-instance-{Guid.NewGuid():N}");

    [Fact]
    public void InstanceService_CreatesReadableCopyThroughContract()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{\"Name\":\"Instanzdienst\"}");

        IProjectRestorePointService service = new ProjectRestorePointStore();

        var result = service.TryCreateForProjectFile(projectFile);

        Assert.True(result.Created, result.Message);
        Assert.NotNull(result.SnapshotPath);
        Assert.Equal(
            "{\"Name\":\"Instanzdienst\"}",
            File.ReadAllText(result.SnapshotPath!));
    }

    [Fact]
    public async Task InstanceService_SerializesParallelRestorePoints()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");
        IProjectRestorePointService service = new ProjectRestorePointStore();

        var results = await Task.WhenAll(
            Enumerable.Range(0, 8)
                .Select(_ => Task.Run(() => service.TryCreateForProjectFile(projectFile))));

        Assert.All(results, result => Assert.True(result.Created, result.Message));
        Assert.Equal(
            8,
            results.Select(result => result.SnapshotPath).Distinct(StringComparer.OrdinalIgnoreCase).Count());
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
