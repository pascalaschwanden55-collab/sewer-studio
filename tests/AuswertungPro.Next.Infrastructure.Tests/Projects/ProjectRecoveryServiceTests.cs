using AuswertungPro.Next.Domain.Models;
using AuswertungPro.Next.Application.Common;
using AuswertungPro.Next.Infrastructure.Import;
using AuswertungPro.Next.Infrastructure.Projects;
using AuswertungPro.Next.Infrastructure.Tests.Backup;

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

    [Fact]
    public void Materialization_claims_no_folder_change_when_quarantine_was_not_created()
    {
        var projectFile = Path.Combine(_root, "projekt.json");
        var repository = new JsonProjectRepository();
        IProjectRecoveryService service = new ProjectRecoveryService();
        var recovery = new ProjectRecoveryResult(
            Recovered: true,
            Project: new Project { Name = "Gepruefte Sicherung" },
            RecoveredFromPath: projectFile + ".bak",
            QuarantinedPath: null);

        var result = service.MaterializeRecoveredProjectForRetry(
            projectFile,
            recovery,
            repository);

        Assert.False(result.ProjectFolderModified);
        Assert.Contains("Quarantaene", result.Detail, StringComparison.Ordinal);
        Assert.False(File.Exists(projectFile));
    }

    [JunctionFact]
    public void TryRecover_laesst_externe_Sicherung_hinter_RestorePoint_Verknuepfung_ausser_Acht()
    {
        Directory.CreateDirectory(_root);
        var projectFile = Path.Combine(_root, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(projectFile, "{ keine gueltige Projektdatei");

        var external = Path.Combine(_root, "externes-archiv");
        Directory.CreateDirectory(external);
        var externalBackup = Path.Combine(external, ProjectFileLocator.ProjectFileName);
        var repository = new JsonProjectRepository();
        Assert.True(repository.Save(
            new Project { Name = "Fremdes Projekt" },
            externalBackup).Ok);

        var restoreParent = Path.Combine(_root, ProjectStructure.RestorePoints);
        Directory.CreateDirectory(restoreParent);
        var linkedRestoreRoot = Path.Combine(restoreParent, "projekt");
        JunctionTestSupport.CreateDirectoryLink(linkedRestoreRoot, external);
        try
        {
            var result = new ProjectRecoveryService().TryRecover(projectFile, repository);

            Assert.False(result.Recovered);
            Assert.True(File.Exists(projectFile));
            Assert.Equal("{ keine gueltige Projektdatei", File.ReadAllText(projectFile));
            Assert.True(File.Exists(externalBackup));
        }
        finally
        {
            if (Directory.Exists(linkedRestoreRoot))
                Directory.Delete(linkedRestoreRoot);
        }
    }

    [JunctionFact]
    public void TryRecover_laesst_Dateiverknuepfung_als_Bak_ausser_Acht()
    {
        Directory.CreateDirectory(_root);
        var projectFile = Path.Combine(_root, ProjectFileLocator.ProjectFileName);
        File.WriteAllText(projectFile, "{ keine gueltige Projektdatei");

        var externalBackup = Path.Combine(_root, "fremde-sicherung.json");
        var repository = new JsonProjectRepository();
        Assert.True(repository.Save(
            new Project { Name = "Fremdes Projekt" },
            externalBackup).Ok);
        var linkedBackup = projectFile + ".bak";
        File.CreateSymbolicLink(linkedBackup, externalBackup);
        try
        {
            var result = new ProjectRecoveryService().TryRecover(projectFile, repository);

            Assert.False(result.Recovered);
            Assert.True(File.Exists(projectFile));
            Assert.True(File.Exists(externalBackup));
        }
        finally
        {
            if (File.Exists(linkedBackup))
                File.Delete(linkedBackup);
        }
    }

    [JunctionFact]
    public void Materialization_blockiert_Projektdateien_Verknuepfung_vor_dem_Schreiben()
    {
        Directory.CreateDirectory(_root);
        var external = Path.Combine(_root, "externes-ziel");
        var linkedProjectFiles = Path.Combine(_root, ProjectStructure.Projektdateien);
        Directory.CreateDirectory(external);
        JunctionTestSupport.CreateDirectoryLink(linkedProjectFiles, external);
        try
        {
            var quarantine = Path.Combine(_root, "projekt.corrupt-20260821_120000.json");
            File.WriteAllText(quarantine, "kaputt");
            var projectFile = Path.Combine(linkedProjectFiles, ProjectFileLocator.ProjectFileName);
            var recovery = new ProjectRecoveryResult(
                Recovered: true,
                Project: new Project { Name = "Gepruefte Sicherung" },
                RecoveredFromPath: Path.Combine(_root, "projekt.json.bak"),
                QuarantinedPath: quarantine);

            var result = new ProjectRecoveryService().MaterializeRecoveredProjectForRetry(
                projectFile,
                recovery,
                new JsonProjectRepository());

            Assert.Contains("Verknuepfung", result.Detail, StringComparison.OrdinalIgnoreCase);
            Assert.True(result.ProjectFolderModified);
            Assert.False(File.Exists(Path.Combine(external, ProjectFileLocator.ProjectFileName)));
        }
        finally
        {
            if (Directory.Exists(linkedProjectFiles))
                Directory.Delete(linkedProjectFiles);
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
