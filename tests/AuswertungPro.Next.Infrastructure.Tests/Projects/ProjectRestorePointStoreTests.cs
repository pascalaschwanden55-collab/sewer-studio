using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

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

    [JunctionFact]
    public void InstanceService_Blockiert_Projektroot_als_Verknuepfung_vor_der_Kopie()
    {
        Directory.CreateDirectory(_root);
        var externalProject = Path.Combine(_root, "externes-projekt");
        var linkedProject = Path.Combine(_root, "projekt-link");
        Directory.CreateDirectory(externalProject);
        JunctionTestSupport.CreateDirectoryLink(linkedProject, externalProject);
        try
        {
            var projectFile = ProjectFileLocator.TargetPath(linkedProject);
            Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
            File.WriteAllText(projectFile, "{}");

            var result = new ProjectRestorePointStore().TryCreateForProjectFile(projectFile);

            Assert.False(result.Created);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.False(Directory.Exists(Path.Combine(
                externalProject,
                ProjectStructure.RestorePoints,
                "projekt")));
        }
        finally
        {
            if (Directory.Exists(linkedProject))
                Directory.Delete(linkedProject);
        }
    }

    [JunctionFact]
    public void InstanceService_Blockiert_RestorePointRoot_als_Verknuepfung_vor_der_Kopie()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        var external = Path.Combine(_root, "externes-archiv");
        var restoreParent = Path.Combine(_root, ProjectStructure.RestorePoints);
        var linkedRestoreRoot = Path.Combine(restoreParent, "projekt");
        Directory.CreateDirectory(external);
        Directory.CreateDirectory(restoreParent);
        JunctionTestSupport.CreateDirectoryLink(linkedRestoreRoot, external);
        try
        {
            var result = new ProjectRestorePointStore().TryCreateForProjectFile(projectFile);

            Assert.False(result.Created);
            Assert.Contains("Verknuepfung", result.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Empty(Directory.EnumerateFileSystemEntries(external));
        }
        finally
        {
            if (Directory.Exists(linkedRestoreRoot))
                Directory.Delete(linkedRestoreRoot);
        }
    }

    [JunctionFact]
    public void InstanceService_Prune_folgt_keiner_verschachtelten_Verknuepfung()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        var restoreDir = Path.Combine(_root, ProjectStructure.RestorePoints, "projekt");
        Directory.CreateDirectory(restoreDir);
        for (var index = 0; index < ProjectRestorePointStore.MaxRestorePoints; index++)
        {
            var snapshot = Path.Combine(
                restoreDir,
                $"20250101-0000{index:000}_projekt.json");
            File.WriteAllText(snapshot, "{}");
            File.SetCreationTimeUtc(snapshot, new DateTime(2025, 1, 1).AddMinutes(index));
        }

        var external = Path.Combine(_root, "externes-archiv");
        var linkedArchive = Path.Combine(restoreDir, "altbestand");
        Directory.CreateDirectory(external);
        var externalProject = Path.Combine(external, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(externalProject, "{\"Name\":\"Fremd\"}");
        File.SetCreationTimeUtc(externalProject, new DateTime(2000, 1, 1));
        JunctionTestSupport.CreateDirectoryLink(linkedArchive, external);
        try
        {
            var result = new ProjectRestorePointStore().TryCreateForProjectFile(projectFile);

            Assert.True(result.Created, result.Message);
            Assert.True(File.Exists(externalProject));
            Assert.True(Directory.Exists(linkedArchive));
        }
        finally
        {
            if (Directory.Exists(linkedArchive))
                Directory.Delete(linkedArchive);
        }
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
