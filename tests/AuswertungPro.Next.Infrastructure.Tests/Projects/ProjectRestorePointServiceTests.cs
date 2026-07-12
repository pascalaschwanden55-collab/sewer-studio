using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;

namespace AuswertungPro.Next.Infrastructure.Tests.Projects;

public sealed class ProjectRestorePointServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(
        Path.GetTempPath(),
        $"project-restore-{Guid.NewGuid():N}");

    [Fact]
    public void TryCreateForProjectFolder_FindsNewProjectStructureAndCopiesContent()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{\"Name\":\"Vor Import\"}");

        var result = ProjectRestorePointService.TryCreateForProjectFolder(_root);

        Assert.True(result.Created, result.Message);
        Assert.NotNull(result.SnapshotPath);
        Assert.True(File.Exists(result.SnapshotPath));
        Assert.Equal("{\"Name\":\"Vor Import\"}", File.ReadAllText(result.SnapshotPath!));
        Assert.StartsWith(
            Path.Combine(_root, ProjectStructure.RestorePoints, "projekt"),
            result.SnapshotPath,
            StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateForProjectFolder_FindsLegacyProjectInRoot()
    {
        Directory.CreateDirectory(_root);
        var projectFile = Path.Combine(_root, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(projectFile, "{}");

        var result = ProjectRestorePointService.TryCreateForProjectFolder(_root);

        Assert.True(result.Created, result.Message);
        Assert.True(File.Exists(result.SnapshotPath));
    }

    [Fact]
    public void TryCreateForProjectFolder_WithoutProjectFileReturnsVisibleReason()
    {
        Directory.CreateDirectory(_root);

        var result = ProjectRestorePointService.TryCreateForProjectFolder(_root);

        Assert.False(result.Created);
        Assert.Null(result.SnapshotPath);
        Assert.Contains("keine projekt.json", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryCreateForProjectFile_KeepsOnlyNewestTwentySnapshots()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        for (var index = 0; index < ProjectRestorePointService.MaxRestorePoints + 5; index++)
        {
            var result = ProjectRestorePointService.TryCreateForProjectFile(projectFile);
            Assert.True(result.Created, result.Message);
        }

        var restoreDir = Path.Combine(_root, ProjectStructure.RestorePoints, "projekt");
        var snapshots = Directory.GetFiles(
            restoreDir,
            $"*_{ProjectFileLocator.ProjectFileName}",
            SearchOption.TopDirectoryOnly);
        Assert.Equal(ProjectRestorePointService.MaxRestorePoints, snapshots.Length);
    }

    [Fact]
    public void TryCreateForProjectFile_DoesNotDeleteUnrelatedJsonFiles()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{}");

        var restoreDir = Path.Combine(_root, ProjectStructure.RestorePoints, "projekt");
        Directory.CreateDirectory(restoreDir);
        var metadataPath = Path.Combine(restoreDir, "hinweise.json");
        File.WriteAllText(metadataPath, "{}");

        for (var index = 0; index < ProjectRestorePointService.MaxRestorePoints + 1; index++)
        {
            var result = ProjectRestorePointService.TryCreateForProjectFile(projectFile);
            Assert.True(result.Created, result.Message);
        }

        Assert.True(File.Exists(metadataPath));
    }

    [Fact]
    public void TryCreateForProjectFile_DoesNotStoreCorruptProjectOrPruneGoodSnapshots()
    {
        var projectFile = ProjectFileLocator.TargetPath(_root);
        Directory.CreateDirectory(Path.GetDirectoryName(projectFile)!);
        File.WriteAllText(projectFile, "{ keine gueltige Projektdatei");

        var restoreDir = Path.Combine(_root, ProjectStructure.RestorePoints, "projekt");
        Directory.CreateDirectory(restoreDir);
        var goodSnapshot = Path.Combine(restoreDir, "20260712-120000000_projekt.json");
        File.WriteAllText(goodSnapshot, "{}");

        var result = ProjectRestorePointService.TryCreateForProjectFile(projectFile);

        Assert.False(result.Created);
        Assert.Contains("nicht lesbar", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.True(File.Exists(goodSnapshot));
        Assert.Single(Directory.GetFiles(restoreDir, "*_projekt.json"));
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
